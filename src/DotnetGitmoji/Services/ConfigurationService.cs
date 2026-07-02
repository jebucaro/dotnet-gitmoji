using System.Text.Json;
using System.Text.Json.Serialization;
using DotnetGitmoji.Models;
using DotnetGitmoji.Theming;

namespace DotnetGitmoji.Services;

public sealed class ConfigurationService : IConfigurationService
{
    public const string ThemeEnvironmentVariable = "DOTNET_GITMOJI_THEME";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() }
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
        string? localPath = await FindLocalConfigPathAsync();
        if (localPath is not null)
        {
            ToolConfiguration config = await LoadFromPathAsync(localPath);
            await DiscardRepoThemeAsync(config, localPath);
            config.Theme = await ResolveEnvironmentThemeAsync() ?? await TryReadGlobalThemeAsync();
            return config;
        }

        string globalConfigPath = DotnetGitmojiPaths.GlobalConfigPath;
        ToolConfiguration result = File.Exists(globalConfigPath)
            ? await LoadFromPathAsync(globalConfigPath)
            : new ToolConfiguration();
        result.Theme = await ResolveEnvironmentThemeAsync() ?? result.Theme;
        return result;
    }

    // The theme is a personal setting: a repo .gitmojirc.json is shared with the whole team,
    // so a theme committed there is never honored.
    private static async Task DiscardRepoThemeAsync(ToolConfiguration config, string path)
    {
        if (config.Theme is null)
        {
            return;
        }

        await Console.Error.WriteLineAsync(
            $"Note: theme in {path} is ignored — the theme is a personal setting. " +
            $"Set {ThemeEnvironmentVariable} or run 'dotnet-gitmoji config --global' instead.");
        config.Theme = null;
    }

    private static async Task<string?> ResolveEnvironmentThemeAsync()
    {
        string? envTheme = Environment.GetEnvironmentVariable(ThemeEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(envTheme))
        {
            return null;
        }

        if (Themes.IsKnown(envTheme))
        {
            return envTheme;
        }

        await Console.Error.WriteLineAsync(
            $"Warning: Invalid theme '{envTheme}' in {ThemeEnvironmentVariable}, ignoring.");
        return null;
    }

    // Lightweight read of only the theme from the global config, used when a repo config is the
    // active one. Deliberately avoids LoadFromPathAsync so unrelated fields of the inactive file
    // don't produce warnings.
    private static async Task<string?> TryReadGlobalThemeAsync()
    {
        string globalConfigPath = DotnetGitmojiPaths.GlobalConfigPath;
        if (!File.Exists(globalConfigPath))
        {
            return null;
        }

        try
        {
            await using FileStream stream = File.OpenRead(globalConfigPath);
            ToolConfiguration? config = await JsonSerializer.DeserializeAsync<ToolConfiguration>(stream, ReadOptions);
            string? theme = config?.Theme;
            if (theme is null || Themes.IsKnown(theme))
            {
                return theme;
            }

            await Console.Error.WriteLineAsync(
                $"Warning: Invalid Theme in config at {globalConfigPath}, using default.");
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SaveThemePreferenceAsync(string theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        string globalConfigPath = DotnetGitmojiPaths.GlobalConfigPath;
        ToolConfiguration config = File.Exists(globalConfigPath)
            ? await LoadFromPathAsync(globalConfigPath)
            : new ToolConfiguration();

        config.Theme = theme;
        await SaveAsync(config, ConfigSaveTarget.Global);
    }

    private async Task<string?> FindLocalConfigPathAsync()
    {
        try
        {
            string repoRoot = await _gitService.GetRepositoryRootAsync();
            string configPath = Path.Combine(repoRoot, ".gitmojirc.json");
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
        {
            savePath = DotnetGitmojiPaths.GlobalConfigPath;
        }
        else
        {
            try
            {
                savePath = Path.Combine(await _gitService.GetRepositoryRootAsync(), ".gitmojirc.json");
            }
            catch
            {
                savePath = DotnetGitmojiPaths.GlobalConfigPath; // not in a git repo
            }
        }

        try
        {
            if (savePath == DotnetGitmojiPaths.GlobalConfigPath)
            {
                Directory.CreateDirectory(DotnetGitmojiPaths.UserDataDirectory);
            }

            await using FileStream stream = File.Create(savePath);
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
        string repoRoot = await _gitService.GetRepositoryRootAsync();
        string configPath = Path.Combine(repoRoot, ".gitmojirc.json");

        if (File.Exists(configPath))
        {
            return null;
        }

        await using FileStream stream = File.Create(configPath);
        await JsonSerializer.SerializeAsync(stream, new ToolConfiguration(), WriteOptions);
        return configPath;
    }

    private static async Task<ToolConfiguration> LoadFromPathAsync(string path)
    {
        try
        {
            await using FileStream stream = File.OpenRead(path);
            ToolConfiguration config = await JsonSerializer.DeserializeAsync<ToolConfiguration>(stream, ReadOptions)
                                       ?? new ToolConfiguration();
            ToolConfiguration defaults = new();

            if (config.MaxTitleLength is <= 0)
            {
                await Console.Error.WriteLineAsync(
                    $"Warning: Invalid MaxTitleLength in config at {path}, using default.");
                config.MaxTitleLength = defaults.MaxTitleLength;
            }

            if (config.Theme is not null && !Themes.IsKnown(config.Theme))
            {
                await Console.Error.WriteLineAsync(
                    $"Warning: Invalid Theme in config at {path}, using default.");
                config.Theme = null;
            }

            if (Uri.TryCreate(config.GitmojisUrl, UriKind.Absolute, out Uri? uri)
                && uri.Scheme == Uri.UriSchemeHttps)
            {
                return config;
            }

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