using System.Text.Json;
using DotnetGitmoji.Models;

namespace DotnetGitmoji.Services;

public sealed class ConfigurationService : IConfigurationService
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

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

        try
        {
            Directory.CreateDirectory(DotnetGitmojiPaths.UserDataDirectory);
            await using var stream = File.Create(DotnetGitmojiPaths.GlobalConfigPath);
            await JsonSerializer.SerializeAsync(stream, config, WriteOptions);
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine(
                $"Error: Permission denied writing config to {DotnetGitmojiPaths.GlobalConfigPath}. " +
                "Check file/directory permissions.");
            throw;
        }
    }

    private static async Task<ToolConfiguration> LoadFromPathAsync(string path)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var config = await JsonSerializer.DeserializeAsync<ToolConfiguration>(stream, ReadOptions);
            return config ?? new ToolConfiguration();
        }
        catch (JsonException)
        {
            Console.Error.WriteLine($"Warning: Could not parse config at {path}, using defaults.");
            return new ToolConfiguration();
        }
    }
}