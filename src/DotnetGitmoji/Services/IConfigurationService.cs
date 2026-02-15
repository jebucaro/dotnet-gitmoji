using DotnetGitmoji.Models;

namespace DotnetGitmoji.Services;

public interface IConfigurationService
{
    Task<ToolConfiguration> LoadAsync();
    Task SaveAsync(ToolConfiguration config);
}