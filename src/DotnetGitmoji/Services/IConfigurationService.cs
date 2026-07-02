using DotnetGitmoji.Models;

namespace DotnetGitmoji.Services;

public interface IConfigurationService
{
    Task<ToolConfiguration> LoadAsync();
    Task SaveAsync(ToolConfiguration config, ConfigSaveTarget target = ConfigSaveTarget.Auto);

    /// <summary>Returns the path of the created file, or null if it already existed.</summary>
    Task<string?> CreateRepoConfigAsync();

    /// <summary>Persists the personal theme preference to the global config file.</summary>
    Task SaveThemePreferenceAsync(string theme);
}