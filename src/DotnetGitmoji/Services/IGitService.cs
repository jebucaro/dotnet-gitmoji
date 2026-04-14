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
}