namespace DotnetGitmoji.Services;

public interface IGitService
{
    Task<string> GetRepositoryRootAsync();
    Task<string?> GetConfigValueAsync(string key);
    Task<bool> IsHookInstalledAsync();
    Task<bool> IsHuskyInstalledAsync();
    Task InstallHookDirectAsync();
    Task<string?> FindHookFileAsync();
    Task RemoveHookDirectAsync();
    Task StageAllAsync();
}