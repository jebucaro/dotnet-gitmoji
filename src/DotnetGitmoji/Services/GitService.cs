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
}