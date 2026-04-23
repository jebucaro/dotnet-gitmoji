using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using CliWrap;
using CliWrap.Buffered;
using DotnetGitmoji.Models;

namespace DotnetGitmoji.Services;

public sealed class GitService : IGitService
{
    private const string PrepareCommitMessageHookName = "prepare-commit-msg";
    private const string DotnetGitmojiTaskName = "dotnet-gitmoji";
    private const string TaskRunnerHookCommand = "dotnet husky run --name dotnet-gitmoji -- \"$1\" \"$2\"";

    public async Task<string> GetRepositoryRootAsync()
    {
        var result = await Cli.Wrap("git")
            .WithArguments(["rev-parse", "--show-toplevel"])
            .ExecuteBufferedAsync();

        return result.StandardOutput.Trim();
    }

    public async Task<string?> GetConfigValueAsync(string key)
    {
        var result = await Cli.Wrap("git")
            .WithArguments(["config", "--get", key])
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        if (result.ExitCode == 0)
        {
            var value = result.StandardOutput.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        if (result.ExitCode == 1) return null;

        var error = result.StandardError.Trim();
        throw new InvalidOperationException(
            $"git config --get {key} failed with exit code {result.ExitCode}{(string.IsNullOrWhiteSpace(error) ? string.Empty : $": {error}")}");
    }

    public async Task<bool> IsHookInstalledAsync()
    {
        var repoRoot = await GetRepositoryRootAsync();
        var hookPaths = new[]
        {
            Path.Combine(repoRoot, ".husky", "prepare-commit-msg"),
            Path.Combine(repoRoot, ".git", "hooks", "prepare-commit-msg")
        };

        foreach (var hookPath in hookPaths)
        {
            if (!File.Exists(hookPath)) continue;

            var content = await File.ReadAllTextAsync(hookPath);
            if (ContainsActiveDotnetGitmojiInvocation(content)) return true;
        }

        return false;
    }

    public async Task<HuskyInstallKind> DetectHuskyKindAsync()
    {
        var repoRoot = await GetRepositoryRootAsync();
        var huskyDir = Path.Combine(repoRoot, ".husky");

        if (!Directory.Exists(huskyDir))
            return HuskyInstallKind.None;

        if (File.Exists(Path.Combine(huskyDir, "task-runner.json")))
            return HuskyInstallKind.HuskyNetTaskRunner;

        if (File.Exists(Path.Combine(huskyDir, "_", "husky.sh")))
            return HuskyInstallKind.JsHusky;

        return HuskyInstallKind.HuskyNetShell;
    }

    public async Task<bool> IsHuskyInstalledAsync()
    {
        return await DetectHuskyKindAsync() != HuskyInstallKind.None;
    }

    public async Task InstallHuskyNetShellHookAsync()
    {
        var repoRoot = await GetRepositoryRootAsync();
        var command = await BuildShellHookCommandAsync();
        await RunDotnetHuskyAddAsync(repoRoot, command);
    }

    public async Task InstallHuskyNetTaskRunnerHookAsync()
    {
        var repoRoot = await GetRepositoryRootAsync();
        var taskRunnerPath = Path.Combine(repoRoot, ".husky", "task-runner.json");
        var isLocal = await IsLocalToolManifestAsync();

        await EnsureTaskRunnerContainsDotnetGitmojiTaskAsync(taskRunnerPath, isLocal);
        await RunDotnetHuskyAddAsync(repoRoot, TaskRunnerHookCommand);
    }

    public async Task InstallHookDirectAsync()
    {
        var repoRoot = await GetRepositoryRootAsync();
        var hooksDir = Path.Combine(repoRoot, ".git", "hooks");
        Directory.CreateDirectory(hooksDir);

        var hookPath = Path.Combine(hooksDir, PrepareCommitMessageHookName);
        var command = await BuildShellHookCommandAsync();
        var script = $"#!/bin/sh\nexec < /dev/tty\n{command}\n";
        await File.WriteAllTextAsync(hookPath, script);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            await Cli.Wrap("chmod")
                .WithArguments(["+x", hookPath])
                .ExecuteAsync();
    }

    public async Task<string?> FindHookFileAsync()
    {
        var repoRoot = await GetRepositoryRootAsync();
        var hookPaths = new[]
        {
            Path.Combine(repoRoot, ".husky", "prepare-commit-msg"),
            Path.Combine(repoRoot, ".git", "hooks", "prepare-commit-msg")
        };

        foreach (var hookPath in hookPaths)
        {
            if (!File.Exists(hookPath)) continue;

            var content = await File.ReadAllTextAsync(hookPath);
            if (ContainsActiveDotnetGitmojiInvocation(content))
                return hookPath;
        }

        return null;
    }

    public async Task RemoveHookDirectAsync()
    {
        var repoRoot = await GetRepositoryRootAsync();
        var hookPath = Path.Combine(repoRoot, ".git", "hooks", PrepareCommitMessageHookName);

        if (!File.Exists(hookPath)) return;

        var content = await File.ReadAllTextAsync(hookPath);
        var lines = content.Split('\n').ToList();
        var filtered = lines.Where(l => !l.Contains("dotnet-gitmoji", StringComparison.OrdinalIgnoreCase)).ToList();

        // If only shebang or empty lines remain, delete the file
        if (filtered.All(l => string.IsNullOrWhiteSpace(l) || l.StartsWith("#!")))
            File.Delete(hookPath);
        else
            await File.WriteAllTextAsync(hookPath, string.Join('\n', filtered));
    }

    public async Task StageAllAsync()
    {
        await Cli.Wrap("git")
            .WithArguments(["add", "."])
            .ExecuteAsync();
    }

    private static async Task RunDotnetHuskyAddAsync(string repoRoot, string hookCommand)
    {
        var result = await Cli.Wrap("dotnet")
            .WithWorkingDirectory(repoRoot)
            .WithArguments(["husky", "add", PrepareCommitMessageHookName, "-c", hookCommand])
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        if (result.ExitCode == 0)
            return;

        var error = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();

        var message = string.IsNullOrWhiteSpace(error)
            ? $"Failed to run 'dotnet husky add {PrepareCommitMessageHookName}' (exit code {result.ExitCode}). Ensure Husky.Net is installed and available."
            : $"Failed to run 'dotnet husky add {PrepareCommitMessageHookName}'. Ensure Husky.Net is installed and available. Details: {error}";

        throw new InvalidOperationException(message);
    }

    private async Task<bool> IsLocalToolManifestAsync()
    {
        var repoRoot = await GetRepositoryRootAsync();
        var manifestPath = Path.Combine(repoRoot, ".config", "dotnet-tools.json");

        if (!File.Exists(manifestPath))
            return false;

        var json = await File.ReadAllTextAsync(manifestPath);
        var node = JsonNode.Parse(json);
        var tools = node?["tools"] as JsonObject;
        return tools?.ContainsKey("dotnet-gitmoji") ?? false;
    }

    private async Task<string> BuildShellHookCommandAsync()
    {
        var isLocal = await IsLocalToolManifestAsync();
        var invocation = isLocal ? "dotnet tool run dotnet-gitmoji" : "dotnet-gitmoji";
        return $"{invocation} \"$1\" \"$2\"";
    }

    private static async Task EnsureTaskRunnerContainsDotnetGitmojiTaskAsync(string taskRunnerPath, bool isLocal)
    {
        JsonObject rootObject;

        if (File.Exists(taskRunnerPath))
        {
            var existingJson = await File.ReadAllTextAsync(taskRunnerPath);
            var parsedNode = JsonNode.Parse(existingJson);
            rootObject = parsedNode as JsonObject
                         ?? throw new InvalidOperationException(
                             $"Invalid task-runner file at {taskRunnerPath}: root value must be a JSON object.");
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(taskRunnerPath)!);
            rootObject = new JsonObject
            {
                ["$schema"] = "https://alirezanet.github.io/Husky.Net/schema.json"
            };
        }

        JsonArray tasks;
        if (rootObject["tasks"] is null)
        {
            tasks = new JsonArray();
            rootObject["tasks"] = tasks;
        }
        else if (rootObject["tasks"] is JsonArray existingTasks)
        {
            tasks = existingTasks;
        }
        else
        {
            throw new InvalidOperationException(
                $"Invalid task-runner file at {taskRunnerPath}: 'tasks' must be a JSON array.");
        }

        if (!ContainsTaskNamed(tasks, DotnetGitmojiTaskName))
        {
            var taskEntry = isLocal
                ? new JsonObject
                {
                    ["name"] = DotnetGitmojiTaskName,
                    ["command"] = "dotnet",
                    ["args"] = new JsonArray("tool", "run", "dotnet-gitmoji", "${args}")
                }
                : new JsonObject
                {
                    ["name"] = DotnetGitmojiTaskName,
                    ["command"] = "dotnet-gitmoji",
                    ["args"] = new JsonArray("${args}")
                };
            tasks.Add(taskEntry);
        }

        var json = rootObject.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(taskRunnerPath, json + Environment.NewLine);
    }

    private static bool ContainsTaskNamed(JsonArray tasks, string taskName)
    {
        foreach (var taskNode in tasks)
        {
            if (taskNode is not JsonObject taskObject)
                continue;

            if (taskObject["name"] is JsonValue taskNameValue &&
                taskNameValue.TryGetValue<string>(out var existingName) &&
                string.Equals(existingName, taskName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool ContainsActiveDotnetGitmojiInvocation(string hookContent)
    {
        foreach (var rawLine in hookContent.Split('\n'))
        {
            var line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            if (line.Contains("dotnet-gitmoji", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}