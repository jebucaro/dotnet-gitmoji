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
        ToolConfiguration config;
        if (localPath is not null)
        {
            config = await LoadFromPathAsync(localPath);
            await DiscardRepoThemeAsync(config, localPath);
        }
        else
        {
            config = new ToolConfiguration();
        }

        config.Theme = await ResolveEnvironmentThemeAsync() ?? await TryReadGlobalThemeAsync();
        return config;
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
            $"Note: theme in {path} is ignored because the theme is a personal setting. " +
            $"Set {ThemeEnvironmentVariable} or run 'dotnet-gitmoji config' to pick a theme.");
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

    // The global config carries nothing but the personal theme preference; shared settings live in
    // each repo's .gitmojirc.json. Reads the raw JSON so legacy files with extra keys still yield
    // their theme, with a note nudging the user to move the rest into a repo config.
    private static async Task<string?> TryReadGlobalThemeAsync()
    {
        string globalConfigPath = DotnetGitmojiPaths.GlobalConfigPath;
        if (!File.Exists(globalConfigPath))
        {
            return null;
        }

        try
        {
            string? theme = null;
            bool hasOtherKeys = false;
            await using (FileStream stream = File.OpenRead(globalConfigPath))
            {
                using JsonDocument document = await JsonDocument.ParseAsync(stream);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    if (string.Equals(property.Name, "theme", StringComparison.OrdinalIgnoreCase))
                    {
                        theme = property.Value.ValueKind == JsonValueKind.String
                            ? property.Value.GetString()
                            : null;
                    }
                    else
                    {
                        hasOtherKeys = true;
                    }
                }
            }

            if (hasOtherKeys)
            {
                await Console.Error.WriteLineAsync(
                    $"Note: only 'theme' is read from {globalConfigPath}. Shared settings belong in " +
                    "each repo's .gitmojirc.json; run 'dotnet-gitmoji config' inside a repo to configure them.");
            }

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
        try
        {
            Directory.CreateDirectory(DotnetGitmojiPaths.UserDataDirectory);
            await using FileStream stream = File.Create(globalConfigPath);
            await JsonSerializer.SerializeAsync(stream, new GlobalPreferences(theme), WriteOptions);
        }
        catch (UnauthorizedAccessException)
        {
            await Console.Error.WriteLineAsync(
                $"Error: Permission denied writing config to {globalConfigPath}. " +
                "Check file/directory permissions.");
            throw;
        }
    }

    // The entire content of the global config file: the theme is the only personal setting.
    private sealed record GlobalPreferences(string Theme);

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

    public async Task SaveAsync(ToolConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        string savePath = Path.Combine(await _gitService.GetRepositoryRootAsync(), ".gitmojirc.json");

        try
        {
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