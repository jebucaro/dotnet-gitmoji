using DotnetGitmoji.Models;

namespace DotnetGitmoji.Services;

public interface IConfigurationService
{
    Task<ToolConfiguration> LoadAsync();
    Task SaveAsync(ToolConfiguration config);

    /// <summary>Returns the path of the created file, or null if it already existed.</summary>
    Task<string?> CreateRepoConfigAsync();
}