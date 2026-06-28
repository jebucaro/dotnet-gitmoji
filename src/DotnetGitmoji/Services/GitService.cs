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
    private const string HuskyDirectoryName = ".husky";
    private const string GitHooksSubdirectory = "hooks";
    private const string DotnetGitmojiLocalInvocation = "dotnet tool run " + DotnetGitmojiTaskName;

    private const string TaskRunnerHookCommand =
        "dotnet husky run --name " + DotnetGitmojiTaskName + " -- \"$1\" \"$2\"";

    public async Task<string> GetRepositoryRootAsync()
    {
        BufferedCommandResult result = await Cli.Wrap("git")
            .WithArguments(["rev-parse", "--show-toplevel"])
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        if (result.ExitCode == 0)
        {
            return result.StandardOutput.Trim();
        }

        throw new InvalidOperationException(
            "Not a git repository. Run 'git init' to initialize one.");
    }

    public async Task<string?> GetConfigValueAsync(string key)
    {
        BufferedCommandResult result = await Cli.Wrap("git")
            .WithArguments(["config", "--get", key])
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        if (result.ExitCode == 0)
        {
            string value = result.StandardOutput.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        if (result.ExitCode == 1)
        {
            return null;
        }

        string error = result.StandardError.Trim();
        throw new InvalidOperationException(
            $"git config --get {key} failed with exit code {result.ExitCode}{(string.IsNullOrWhiteSpace(error) ? string.Empty : $": {error}")}");
    }

    public async Task<bool> IsHookInstalledAsync()
    {
        string repoRoot = await GetRepositoryRootAsync();
        string[] hookPaths = new[]
        {
            Path.Combine(repoRoot, HuskyDirectoryName, PrepareCommitMessageHookName),
            Path.Combine(repoRoot, ".git", GitHooksSubdirectory, PrepareCommitMessageHookName)
        };

        foreach (string hookPath in hookPaths)
        {
            if (!File.Exists(hookPath))
            {
                continue;
            }

            string content = await File.ReadAllTextAsync(hookPath);
            if (ContainsActiveDotnetGitmojiInvocation(content))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<HuskyInstallKind> DetectHuskyKindAsync()
    {
        string repoRoot = await GetRepositoryRootAsync();
        string huskyDir = Path.Combine(repoRoot, HuskyDirectoryName);

        if (!Directory.Exists(huskyDir))
        {
            return HuskyInstallKind.None;
        }

        if (File.Exists(Path.Combine(huskyDir, "task-runner.json")))
        {
            return HuskyInstallKind.HuskyNetTaskRunner;
        }

        if (File.Exists(Path.Combine(huskyDir, "_", "husky.sh")))
        {
            return HuskyInstallKind.JsHusky;
        }

        return HuskyInstallKind.HuskyNetShell;
    }

    public async Task<bool> IsHuskyInstalledAsync()
    {
        return await DetectHuskyKindAsync() != HuskyInstallKind.None;
    }

    public async Task InstallHuskyNetShellHookAsync()
    {
        string repoRoot = await GetRepositoryRootAsync();
        string command = await BuildShellHookCommandAsync();
        await RunDotnetHuskyAddAsync(repoRoot, command);
    }

    public async Task InstallHuskyNetTaskRunnerHookAsync()
    {
        string repoRoot = await GetRepositoryRootAsync();
        string taskRunnerPath = Path.Combine(repoRoot, HuskyDirectoryName, "task-runner.json");
        bool isLocal = await IsLocalToolManifestAsync();

        await EnsureTaskRunnerContainsDotnetGitmojiTaskAsync(taskRunnerPath, isLocal);
        await RunDotnetHuskyAddAsync(repoRoot, TaskRunnerHookCommand);
    }

    public async Task InstallHookDirectAsync()
    {
        string repoRoot = await GetRepositoryRootAsync();
        string hooksDir = Path.Combine(repoRoot, ".git", GitHooksSubdirectory);

        try
        {
            Directory.CreateDirectory(hooksDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Could not create hooks directory at '{hooksDir}': {ex.Message}");
        }

        string hookPath = Path.Combine(hooksDir, PrepareCommitMessageHookName);
        string command = await BuildShellHookCommandAsync();
        string script = $"#!/bin/sh\nexec < /dev/tty\n{command}\n";

        try
        {
            await File.WriteAllTextAsync(hookPath, script);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Could not write hook file at '{hookPath}': {ex.Message}");
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            BufferedCommandResult chmodResult = await Cli.Wrap("chmod")
                .WithArguments(["+x", hookPath])
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            if (chmodResult.ExitCode != 0)
            {
                string error = string.IsNullOrWhiteSpace(chmodResult.StandardError)
                    ? chmodResult.StandardOutput.Trim()
                    : chmodResult.StandardError.Trim();
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? $"chmod +x failed on '{hookPath}' (exit code {chmodResult.ExitCode})."
                        : $"chmod +x failed on '{hookPath}': {error}");
            }
        }
    }

    public async Task<string?> FindHookFileAsync()
    {
        string repoRoot = await GetRepositoryRootAsync();
        string[] hookPaths = new[]
        {
            Path.Combine(repoRoot, HuskyDirectoryName, PrepareCommitMessageHookName),
            Path.Combine(repoRoot, ".git", GitHooksSubdirectory, PrepareCommitMessageHookName)
        };

        foreach (string hookPath in hookPaths)
        {
            if (!File.Exists(hookPath))
            {
                continue;
            }

            string content = await File.ReadAllTextAsync(hookPath);
            if (ContainsActiveDotnetGitmojiInvocation(content))
            {
                return hookPath;
            }
        }

        return null;
    }

    public async Task RemoveHookDirectAsync()
    {
        string repoRoot = await GetRepositoryRootAsync();
        string hookPath = Path.Combine(repoRoot, ".git", GitHooksSubdirectory, PrepareCommitMessageHookName);

        if (!File.Exists(hookPath))
        {
            return;
        }

        try
        {
            string content = await File.ReadAllTextAsync(hookPath);
            List<string> lines = content.Split('\n').ToList();
            List<string> filtered = lines
                .Where(l => !l.Contains(DotnetGitmojiTaskName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // If only shebang or empty lines remain, delete the file
            if (filtered.All(l => string.IsNullOrWhiteSpace(l) || l.StartsWith("#!")))
            {
                File.Delete(hookPath);
            }
            else
            {
                await File.WriteAllTextAsync(hookPath, string.Join('\n', filtered));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Could not modify hook file at '{hookPath}': {ex.Message}");
        }
    }

    public async Task StageAllAsync()
    {
        BufferedCommandResult result = await Cli.Wrap("git")
            .WithArguments(["add", "."])
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        if (result.ExitCode == 0)
        {
            return;
        }

        string error = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();

        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(error)
                ? $"Failed to stage changes (exit code {result.ExitCode})."
                : $"Failed to stage changes (exit code {result.ExitCode}): {error}");
    }

    public async Task<bool> HasStagedChangesAsync()
    {
        BufferedCommandResult result = await Cli.Wrap("git")
            .WithArguments(["diff", "--cached", "--name-only"])
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        if (result.ExitCode == 0)
        {
            return !string.IsNullOrWhiteSpace(result.StandardOutput);
        }

        string error = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();

        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(error)
                ? $"Failed to check staged changes (exit code {result.ExitCode})."
                : $"Failed to check staged changes (exit code {result.ExitCode}): {error}");
    }

    public async Task<string> CommitAsync(string subject, string? body, bool signed)
    {
        List<string> args = new() { "commit" };
        if (signed)
        {
            args.Add("-S");
        }

        args.AddRange(["-m", subject]);
        if (!string.IsNullOrWhiteSpace(body))
        {
            args.Add("-m");
            args.Add(body);
        }

        BufferedCommandResult result = await Cli.Wrap("git")
            .WithArguments(args)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        if (result.ExitCode == 0)
        {
            return result.StandardOutput;
        }

        string error = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();

        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(error)
                ? $"git commit failed with exit code {result.ExitCode}."
                : $"git commit failed with exit code {result.ExitCode}: {error}");
    }

    private static async Task RunDotnetHuskyAddAsync(string repoRoot, string hookCommand)
    {
        BufferedCommandResult result = await Cli.Wrap("dotnet")
            .WithWorkingDirectory(repoRoot)
            .WithArguments(["husky", "add", PrepareCommitMessageHookName, "-c", hookCommand])
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        if (result.ExitCode == 0)
        {
            return;
        }

        string error = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();

        string message = string.IsNullOrWhiteSpace(error)
            ? $"Failed to run 'dotnet husky add {PrepareCommitMessageHookName}' (exit code {result.ExitCode}). Ensure Husky.Net is installed and available."
            : $"Failed to run 'dotnet husky add {PrepareCommitMessageHookName}'. Ensure Husky.Net is installed and available. Details: {error}";

        throw new InvalidOperationException(message);
    }

    private async Task<bool> IsLocalToolManifestAsync()
    {
        string repoRoot = await GetRepositoryRootAsync();

        for (DirectoryInfo? directory = new(repoRoot); directory is not null; directory = directory.Parent)
        {
            string[] candidates = new[]
            {
                Path.Combine(directory.FullName, ".config", "dotnet-tools.json"),
                Path.Combine(directory.FullName, "dotnet-tools.json")
            };

            foreach (string manifestPath in candidates)
            {
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                string json = await File.ReadAllTextAsync(manifestPath);
                JsonNode? node = JsonNode.Parse(json);
                if (node?["tools"] is not JsonObject tools)
                {
                    return false;
                }

                return tools.Any(entry =>
                    string.Equals(entry.Key, DotnetGitmojiTaskName, StringComparison.OrdinalIgnoreCase));
            }
        }

        return false;
    }

    private async Task<string> BuildShellHookCommandAsync()
    {
        bool isLocal = await IsLocalToolManifestAsync();
        string invocation = isLocal ? DotnetGitmojiLocalInvocation : DotnetGitmojiTaskName;
        return $"{invocation} \"$1\" \"$2\"";
    }

    private static async Task EnsureTaskRunnerContainsDotnetGitmojiTaskAsync(string taskRunnerPath, bool isLocal)
    {
        JsonObject rootObject;

        if (File.Exists(taskRunnerPath))
        {
            string existingJson = await File.ReadAllTextAsync(taskRunnerPath);
            JsonNode? parsedNode = JsonNode.Parse(existingJson);
            rootObject = parsedNode as JsonObject
                         ?? throw new InvalidOperationException(
                             $"Invalid task-runner file at {taskRunnerPath}: root value must be a JSON object.");
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(taskRunnerPath)!);
            rootObject = new JsonObject { ["$schema"] = "https://alirezanet.github.io/Husky.Net/schema.json" };
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
            JsonObject taskEntry = isLocal
                ? new JsonObject
                {
                    ["name"] = DotnetGitmojiTaskName,
                    ["command"] = "dotnet",
                    ["args"] = new JsonArray("tool", "run", DotnetGitmojiTaskName, "${args}")
                }
                : new JsonObject
                {
                    ["name"] = DotnetGitmojiTaskName,
                    ["command"] = DotnetGitmojiTaskName,
                    ["args"] = new JsonArray("${args}")
                };
            tasks.Add(taskEntry);
        }

        string json = rootObject.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(taskRunnerPath, json + Environment.NewLine);
    }

    private static bool ContainsTaskNamed(JsonArray tasks, string taskName)
    {
        foreach (JsonNode? taskNode in tasks)
        {
            if (taskNode is not JsonObject taskObject)
            {
                continue;
            }

            if (taskObject["name"] is JsonValue taskNameValue &&
                taskNameValue.TryGetValue<string>(out string? existingName) &&
                string.Equals(existingName, taskName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsActiveDotnetGitmojiInvocation(string hookContent)
    {
        foreach (string rawLine in hookContent.Split('\n'))
        {
            string line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            if (line.Contains(DotnetGitmojiTaskName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}