namespace DotnetGitmoji.Services;

public interface IGitService
{
    Task<string> GetRepositoryRootAsync();
    Task<string?> GetConfigValueAsync(string key);
    Task<bool> IsHookInstalledAsync();
}