using System.Text.Json;
using DotnetGitmoji.Models;

namespace DotnetGitmoji.Services;

public sealed class ConfigurationService : IConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IGitService _gitService;

    public ConfigurationService(IGitService gitService)
    {
        _gitService = gitService;
    }

    public async Task<ToolConfiguration> LoadAsync()
    {
        var repoRoot = await _gitService.GetRepositoryRootAsync();
        var repoConfigPath = Path.Combine(repoRoot, ".gitmojirc.json");
        var globalConfigPath = DotnetGitmojiPaths.GlobalConfigPath;

        if (File.Exists(repoConfigPath)) return await LoadFromPathAsync(repoConfigPath);

        if (File.Exists(globalConfigPath)) return await LoadFromPathAsync(globalConfigPath);

        return new ToolConfiguration();
    }

    public async Task SaveAsync(ToolConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Directory.CreateDirectory(DotnetGitmojiPaths.UserDataDirectory);
        await using var stream = File.Create(DotnetGitmojiPaths.GlobalConfigPath);
        await JsonSerializer.SerializeAsync(stream, config, JsonOptions);
    }

    private static async Task<ToolConfiguration> LoadFromPathAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        var config = await JsonSerializer.DeserializeAsync<ToolConfiguration>(stream, JsonOptions);
        return config ?? new ToolConfiguration();
    }
}