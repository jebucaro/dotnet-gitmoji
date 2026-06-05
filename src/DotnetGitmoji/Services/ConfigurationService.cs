using System.Text.Json;
using System.Text.Json.Serialization;
using DotnetGitmoji.Models;

namespace DotnetGitmoji.Services;

public sealed class ConfigurationService : IConfigurationService
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IGitService _gitService;

    public ConfigurationService(IGitService gitService)
    {
        _gitService = gitService;
    }

    public async Task<ToolConfiguration> LoadAsync()
    {
        var localPath = await FindLocalConfigPathAsync();
        if (localPath is not null) return await LoadFromPathAsync(localPath);

        var globalConfigPath = DotnetGitmojiPaths.GlobalConfigPath;
        if (File.Exists(globalConfigPath)) return await LoadFromPathAsync(globalConfigPath);

        return new ToolConfiguration();
    }

    private async Task<string?> FindLocalConfigPathAsync()
    {
        try
        {
            var repoRoot = await _gitService.GetRepositoryRootAsync();
            var configPath = Path.Combine(repoRoot, ".gitmojirc.json");
            return File.Exists(configPath) ? configPath : null;
        }
        catch
        {
            return null; // not in a git repo
        }
    }

    public async Task SaveAsync(ToolConfiguration config, ConfigSaveTarget target = ConfigSaveTarget.Auto)
    {
        ArgumentNullException.ThrowIfNull(config);

        string savePath;
        if (target == ConfigSaveTarget.Global)
            savePath = DotnetGitmojiPaths.GlobalConfigPath;
        else
            try
            {
                savePath = Path.Combine(await _gitService.GetRepositoryRootAsync(), ".gitmojirc.json");
            }
            catch
            {
                savePath = DotnetGitmojiPaths.GlobalConfigPath; // not in a git repo
            }

        try
        {
            if (savePath == DotnetGitmojiPaths.GlobalConfigPath)
                Directory.CreateDirectory(DotnetGitmojiPaths.UserDataDirectory);

            await using var stream = File.Create(savePath);
            await JsonSerializer.SerializeAsync(stream, config, WriteOptions);
        }
        catch (UnauthorizedAccessException)
        {
            await Console.Error.WriteLineAsync(
                $"Error: Permission denied writing config to {savePath}. " +
                "Check file/directory permissions.");
            throw;
        }
    }

    public async Task<string?> CreateRepoConfigAsync()
    {
        var repoRoot = await _gitService.GetRepositoryRootAsync();
        var configPath = Path.Combine(repoRoot, ".gitmojirc.json");

        if (File.Exists(configPath)) return null;

        await using var stream = File.Create(configPath);
        await JsonSerializer.SerializeAsync(stream, new ToolConfiguration(), WriteOptions);
        return configPath;
    }

    private static async Task<ToolConfiguration> LoadFromPathAsync(string path)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var config = await JsonSerializer.DeserializeAsync<ToolConfiguration>(stream, ReadOptions)
                         ?? new ToolConfiguration();
            var defaults = new ToolConfiguration();

            if (config.MaxTitleLength is <= 0)
            {
                await Console.Error.WriteLineAsync(
                    $"Warning: Invalid MaxTitleLength in config at {path}, using default.");
                config.MaxTitleLength = defaults.MaxTitleLength;
            }

            if (Uri.TryCreate(config.GitmojisUrl, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps) return config;
            await Console.Error.WriteLineAsync(
                $"Warning: Invalid GitmojisUrl in config at {path}, using default.");
            config.GitmojisUrl = defaults.GitmojisUrl;

            return config;
        }
        catch (JsonException)
        {
            await Console.Error.WriteLineAsync($"Warning: Could not parse config at {path}, using defaults.");
            return new ToolConfiguration();
        }
    }
}