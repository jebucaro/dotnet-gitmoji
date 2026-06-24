using DotnetGitmoji.Models;

namespace DotnetGitmoji.Services;

public interface IGitService
{
    Task<string> GetRepositoryRootAsync();
    Task<string?> GetConfigValueAsync(string key);
    Task<bool> IsHookInstalledAsync();
    Task<HuskyInstallKind> DetectHuskyKindAsync();

    /// <summary>Returns true when any Husky variant is detected.</summary>
    Task<bool> IsHuskyInstalledAsync();

    Task InstallHuskyNetShellHookAsync();
    Task InstallHuskyNetTaskRunnerHookAsync();
    Task InstallHookDirectAsync();
    Task<string?> FindHookFileAsync();
    Task RemoveHookDirectAsync();
    Task StageAllAsync();
    Task<bool> HasStagedChangesAsync();

    /// <summary>Runs <c>git commit</c> with the given subject/body. Returns git's stdout on success;
    /// throws <see cref="InvalidOperationException"/> if the commit fails.</summary>
    Task<string> CommitAsync(string subject, string? body, bool signed);
}