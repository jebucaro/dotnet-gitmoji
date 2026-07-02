using DotnetGitmoji.Models;

namespace DotnetGitmoji.Services;

public interface IConfigurationService
{
    Task<ToolConfiguration> LoadAsync();

    /// <summary>Writes the shared configuration to .gitmojirc.json at the repo root. Throws when not in a git repo.</summary>
    Task SaveAsync(ToolConfiguration config);

    /// <summary>Returns the path of the created file, or null if it already existed.</summary>
    Task<string?> CreateRepoConfigAsync();

    /// <summary>Persists the personal theme preference to the global config file (its only content).</summary>
    Task SaveThemePreferenceAsync(string theme);
}