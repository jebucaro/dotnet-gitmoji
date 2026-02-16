using System.Runtime.InteropServices;
using CliWrap;
using CliWrap.Buffered;

namespace DotnetGitmoji.Services;

public sealed class GitService : IGitService
{
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
            if (content.Contains("dotnet-gitmoji", StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    public async Task<bool> IsHuskyInstalledAsync()
    {
        var repoRoot = await GetRepositoryRootAsync();
        return File.Exists(Path.Combine(repoRoot, ".husky", "_", "husky.sh"))
               || File.Exists(Path.Combine(repoRoot, ".husky", "task-runner.json"));
    }

    public async Task InstallHookDirectAsync()
    {
        var repoRoot = await GetRepositoryRootAsync();
        var hooksDir = Path.Combine(repoRoot, ".git", "hooks");
        Directory.CreateDirectory(hooksDir);

        var hookPath = Path.Combine(hooksDir, "prepare-commit-msg");
        var script = "#!/bin/sh\nexec < /dev/tty\ndotnet dotnet-gitmoji \"$1\" \"$2\"\n";
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
            if (content.Contains("dotnet-gitmoji", StringComparison.OrdinalIgnoreCase))
                return hookPath;
        }

        return null;
    }

    public async Task RemoveHookDirectAsync()
    {
        var repoRoot = await GetRepositoryRootAsync();
        var hookPath = Path.Combine(repoRoot, ".git", "hooks", "prepare-commit-msg");

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
}