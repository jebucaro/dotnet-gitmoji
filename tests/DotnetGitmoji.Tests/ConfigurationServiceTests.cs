using System.Text.Json;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using DotnetGitmoji.Theming;
using NSubstitute;

namespace DotnetGitmoji.Tests;

public class ConfigurationServiceTests
{
    [Fact]
    public void ToolConfiguration_Defaults_MatchUpstream()
    {
        ToolConfiguration config = new();

        Assert.False(config.MessagePrompt);
        Assert.False(config.ScopePrompt);
        Assert.True(config.CapitalizeTitle);
        Assert.Equal(ToolConfiguration.DefaultMaxTitleLength, config.MaxTitleLength);
        Assert.True(config.TrimTitleWhenExceeded);
        Assert.False(config.AutoAdd);
        Assert.False(config.SignedCommit);
        Assert.Equal(EmojiFormat.Emoji, config.EmojiFormat);
        Assert.Equal("https://gitmoji.dev/api/gitmojis", config.GitmojisUrl);
        Assert.True(config.ShowSemverBadge);
        Assert.False(config.NormalizeCommitFormat);
        Assert.Null(config.Scopes);
        Assert.Null(config.Theme);
    }

    [Fact]
    public async Task LoadAsync_WhenNoConfigFileExists_ReturnsDefaults()
    {
        IGitService? gitService = Substitute.For<IGitService>();
        string tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        gitService.GetRepositoryRootAsync().Returns(tempDir);

        try
        {
            ConfigurationService service = new(gitService);
            ToolConfiguration config = await service.LoadAsync();

            Assert.False(config.MessagePrompt);
            Assert.Equal("https://gitmoji.dev/api/gitmojis", config.GitmojisUrl);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenRepoConfigExists_LoadsRepoConfig()
    {
        IGitService? gitService = Substitute.For<IGitService>();
        string tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        gitService.GetRepositoryRootAsync().Returns(tempDir);

        string configJson = """{ "MessagePrompt": false, "CapitalizeTitle": false }""";
        await File.WriteAllTextAsync(Path.Combine(tempDir, ".gitmojirc.json"), configJson,
            TestContext.Current.CancellationToken);

        try
        {
            ConfigurationService service = new(gitService);
            ToolConfiguration config = await service.LoadAsync();

            Assert.False(config.MessagePrompt);
            Assert.False(config.CapitalizeTitle);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenConfigHasMalformedJson_ReturnsDefaults()
    {
        IGitService? gitService = Substitute.For<IGitService>();
        string tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        gitService.GetRepositoryRootAsync().Returns(tempDir);

        await File.WriteAllTextAsync(Path.Combine(tempDir, ".gitmojirc.json"), "NOT JSON {{{",
            TestContext.Current.CancellationToken);

        try
        {
            ConfigurationService service = new(gitService);
            ToolConfiguration config = await service.LoadAsync();

            Assert.False(config.MessagePrompt);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CreateRepoConfigAsync_WhenNoConfigExists_CreatesFileWithDefaults()
    {
        IGitService? gitService = Substitute.For<IGitService>();
        string tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        gitService.GetRepositoryRootAsync().Returns(tempDir);

        try
        {
            ConfigurationService service = new(gitService);
            string? createdPath = await service.CreateRepoConfigAsync();

            Assert.NotNull(createdPath);
            Assert.True(File.Exists(createdPath));

            ToolConfiguration config = await service.LoadAsync();
            ToolConfiguration defaults = new();
            Assert.Equal(defaults.EmojiFormat, config.EmojiFormat);
            Assert.Equal(defaults.ScopePrompt, config.ScopePrompt);
            Assert.Equal(defaults.MessagePrompt, config.MessagePrompt);
            Assert.Equal(defaults.CapitalizeTitle, config.CapitalizeTitle);
            Assert.Equal(defaults.MaxTitleLength, config.MaxTitleLength);
            Assert.Equal(defaults.TrimTitleWhenExceeded, config.TrimTitleWhenExceeded);
            Assert.Equal(defaults.AutoAdd, config.AutoAdd);
            Assert.Equal(defaults.SignedCommit, config.SignedCommit);
            Assert.Equal(defaults.GitmojisUrl, config.GitmojisUrl);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CreateRepoConfigAsync_WhenConfigAlreadyExists_ReturnsNull()
    {
        IGitService? gitService = Substitute.For<IGitService>();
        string tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        gitService.GetRepositoryRootAsync().Returns(tempDir);

        string configPath = Path.Combine(tempDir, ".gitmojirc.json");
        string originalContent = """{ "CapitalizeTitle": false }""";
        await File.WriteAllTextAsync(configPath, originalContent, TestContext.Current.CancellationToken);

        try
        {
            ConfigurationService service = new(gitService);
            string? createdPath = await service.CreateRepoConfigAsync();

            Assert.Null(createdPath);
            Assert.Equal(originalContent,
                await File.ReadAllTextAsync(configPath, TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenConfigHasInvalidGitmojisUrl_FallsBackToDefault()
    {
        IGitService? gitService = Substitute.For<IGitService>();
        string tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        gitService.GetRepositoryRootAsync().Returns(tempDir);

        string configJson = """{ "GitmojisUrl": "http://insecure.example.com/gitmojis" }""";
        await File.WriteAllTextAsync(Path.Combine(tempDir, ".gitmojirc.json"), configJson,
            TestContext.Current.CancellationToken);

        try
        {
            ConfigurationService service = new(gitService);
            ToolConfiguration config = await service.LoadAsync();

            Assert.Equal("https://gitmoji.dev/api/gitmojis", config.GitmojisUrl);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenConfigHasInvalidMaxTitleLength_FallsBackToDefault()
    {
        IGitService? gitService = Substitute.For<IGitService>();
        string tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        gitService.GetRepositoryRootAsync().Returns(tempDir);

        string configJson = """{ "MaxTitleLength": 0 }""";
        await File.WriteAllTextAsync(Path.Combine(tempDir, ".gitmojirc.json"), configJson,
            TestContext.Current.CancellationToken);

        try
        {
            ConfigurationService service = new(gitService);
            ToolConfiguration config = await service.LoadAsync();

            Assert.Equal(ToolConfiguration.DefaultMaxTitleLength, config.MaxTitleLength);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Theory]
    [InlineData("monokai")]
    [InlineData("solarized")]
    public async Task LoadAsync_WhenRepoConfigHasTheme_IgnoresIt(string theme)
    {
        IGitService? gitService = Substitute.For<IGitService>();
        string tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        gitService.GetRepositoryRootAsync().Returns(tempDir);

        string configJson = $$"""{ "theme": "{{theme}}" }""";
        await File.WriteAllTextAsync(Path.Combine(tempDir, ".gitmojirc.json"), configJson,
            TestContext.Current.CancellationToken);

        byte[]? globalBackup = await BackupAndDeleteGlobalConfigAsync();
        string? envBackup = SwapThemeEnvironmentVariable(null);
        try
        {
            ConfigurationService service = new(gitService);
            ToolConfiguration config = await service.LoadAsync();

            Assert.Null(config.Theme);
        }
        finally
        {
            SwapThemeEnvironmentVariable(envBackup);
            await RestoreGlobalConfigAsync(globalBackup);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenGlobalConfigHasTheme_AppliesItOverRepoConfig()
    {
        IGitService? gitService = Substitute.For<IGitService>();
        string tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        gitService.GetRepositoryRootAsync().Returns(tempDir);

        await File.WriteAllTextAsync(Path.Combine(tempDir, ".gitmojirc.json"), """{ "scopePrompt": true }""",
            TestContext.Current.CancellationToken);

        byte[]? globalBackup = await BackupAndDeleteGlobalConfigAsync();
        string? envBackup = SwapThemeEnvironmentVariable(null);
        try
        {
            Directory.CreateDirectory(DotnetGitmojiPaths.UserDataDirectory);
            await File.WriteAllTextAsync(DotnetGitmojiPaths.GlobalConfigPath,
                """{ "theme": "catppuccin-mocha" }""", TestContext.Current.CancellationToken);

            ConfigurationService service = new(gitService);
            ToolConfiguration config = await service.LoadAsync();

            Assert.Equal("catppuccin-mocha", config.Theme);
            Assert.True(config.ScopePrompt); // repo config still wins for everything else
        }
        finally
        {
            SwapThemeEnvironmentVariable(envBackup);
            await RestoreGlobalConfigAsync(globalBackup);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenEnvironmentThemeSet_OverridesGlobalConfig()
    {
        IGitService? gitService = Substitute.For<IGitService>();
        gitService.GetRepositoryRootAsync()
            .Returns(Task.FromException<string>(new InvalidOperationException("not a git repo")));

        byte[]? globalBackup = await BackupAndDeleteGlobalConfigAsync();
        string? envBackup = SwapThemeEnvironmentVariable("catppuccin-mocha");
        try
        {
            Directory.CreateDirectory(DotnetGitmojiPaths.UserDataDirectory);
            await File.WriteAllTextAsync(DotnetGitmojiPaths.GlobalConfigPath,
                """{ "theme": "monokai" }""", TestContext.Current.CancellationToken);

            ConfigurationService service = new(gitService);
            ToolConfiguration config = await service.LoadAsync();

            Assert.Equal("catppuccin-mocha", config.Theme);
        }
        finally
        {
            SwapThemeEnvironmentVariable(envBackup);
            await RestoreGlobalConfigAsync(globalBackup);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenEnvironmentThemeInvalid_FallsBackToGlobalConfig()
    {
        IGitService? gitService = Substitute.For<IGitService>();
        gitService.GetRepositoryRootAsync()
            .Returns(Task.FromException<string>(new InvalidOperationException("not a git repo")));

        byte[]? globalBackup = await BackupAndDeleteGlobalConfigAsync();
        string? envBackup = SwapThemeEnvironmentVariable("solarized");
        try
        {
            Directory.CreateDirectory(DotnetGitmojiPaths.UserDataDirectory);
            await File.WriteAllTextAsync(DotnetGitmojiPaths.GlobalConfigPath,
                """{ "theme": "monokai" }""", TestContext.Current.CancellationToken);

            ConfigurationService service = new(gitService);
            ToolConfiguration config = await service.LoadAsync();

            Assert.Equal("monokai", config.Theme);
        }
        finally
        {
            SwapThemeEnvironmentVariable(envBackup);
            await RestoreGlobalConfigAsync(globalBackup);
        }
    }

    [Fact]
    public async Task SaveThemePreferenceAsync_WritesGlobalConfigAndLeavesRepoConfigUntouched()
    {
        IGitService? gitService = Substitute.For<IGitService>();
        string tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        gitService.GetRepositoryRootAsync().Returns(tempDir);

        string repoConfigPath = Path.Combine(tempDir, ".gitmojirc.json");
        string repoContent = """{ "scopePrompt": true }""";
        await File.WriteAllTextAsync(repoConfigPath, repoContent, TestContext.Current.CancellationToken);

        byte[]? globalBackup = await BackupAndDeleteGlobalConfigAsync();
        try
        {
            // A legacy global config with shared settings gets replaced by theme-only content.
            Directory.CreateDirectory(DotnetGitmojiPaths.UserDataDirectory);
            await File.WriteAllTextAsync(DotnetGitmojiPaths.GlobalConfigPath,
                """{ "capitalizeTitle": false, "theme": "default" }""",
                TestContext.Current.CancellationToken);

            ConfigurationService service = new(gitService);
            await service.SaveThemePreferenceAsync("monokai");

            string globalContent = await File.ReadAllTextAsync(DotnetGitmojiPaths.GlobalConfigPath,
                TestContext.Current.CancellationToken);
            using JsonDocument document = JsonDocument.Parse(globalContent);
            JsonProperty themeProperty = Assert.Single(document.RootElement.EnumerateObject());
            Assert.Equal("theme", themeProperty.Name);
            Assert.Equal("monokai", themeProperty.Value.GetString());
            Assert.Equal(repoContent,
                await File.ReadAllTextAsync(repoConfigPath, TestContext.Current.CancellationToken));
        }
        finally
        {
            await RestoreGlobalConfigAsync(globalBackup);
            Directory.Delete(tempDir, true);
        }
    }

    private static string? SwapThemeEnvironmentVariable(string? value)
    {
        string? previous = Environment.GetEnvironmentVariable(ConfigurationService.ThemeEnvironmentVariable);
        Environment.SetEnvironmentVariable(ConfigurationService.ThemeEnvironmentVariable, value);
        return previous;
    }

    private static async Task<byte[]?> BackupAndDeleteGlobalConfigAsync()
    {
        string globalPath = DotnetGitmojiPaths.GlobalConfigPath;
        if (!File.Exists(globalPath))
        {
            return null;
        }

        byte[] backup = await File.ReadAllBytesAsync(globalPath, TestContext.Current.CancellationToken);
        File.Delete(globalPath);
        return backup;
    }

    private static async Task RestoreGlobalConfigAsync(byte[]? backup)
    {
        string globalPath = DotnetGitmojiPaths.GlobalConfigPath;
        if (backup is not null)
        {
            Directory.CreateDirectory(DotnetGitmojiPaths.UserDataDirectory);
            await File.WriteAllBytesAsync(globalPath, backup, TestContext.Current.CancellationToken);
        }
        else if (File.Exists(globalPath))
        {
            File.Delete(globalPath);
        }
    }

    [Fact]
    public async Task SaveAsync_WhenInGitRepo_SavesRepoFile()
    {
        IGitService? gitService = Substitute.For<IGitService>();
        string tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        gitService.GetRepositoryRootAsync().Returns(tempDir);

        try
        {
            ConfigurationService service = new(gitService);
            ToolConfiguration config = new() { CapitalizeTitle = false };

            await service.SaveAsync(config);

            string savedPath = Path.Combine(tempDir, ".gitmojirc.json");
            Assert.True(File.Exists(savedPath));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SaveAsync_WhenNotInGitRepo_Throws()
    {
        IGitService? gitService = Substitute.For<IGitService>();
        gitService.GetRepositoryRootAsync()
            .Returns(Task.FromException<string>(new InvalidOperationException("not a git repo")));

        ConfigurationService service = new(gitService);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(new ToolConfiguration()));
    }

    [Fact]
    public async Task LoadAsync_WhenGlobalConfigHasNonThemeSettings_IgnoresThemWithNote()
    {
        IGitService? gitService = Substitute.For<IGitService>();
        gitService.GetRepositoryRootAsync()
            .Returns(Task.FromException<string>(new InvalidOperationException("not a git repo")));

        byte[]? globalBackup = await BackupAndDeleteGlobalConfigAsync();
        string? envBackup = SwapThemeEnvironmentVariable(null);
        try
        {
            Directory.CreateDirectory(DotnetGitmojiPaths.UserDataDirectory);
            await File.WriteAllTextAsync(DotnetGitmojiPaths.GlobalConfigPath,
                """{ "CapitalizeTitle": false, "theme": "monokai" }""",
                TestContext.Current.CancellationToken);

            ConfigurationService service = new(gitService);
            (ToolConfiguration config, string stderr) = await LoadCapturingStdErrAsync(service);

            Assert.True(config.CapitalizeTitle); // shared setting in the global config is ignored
            Assert.Equal("monokai", config.Theme); // the theme is still honored
            Assert.Contains("only 'theme' is read", stderr);
        }
        finally
        {
            SwapThemeEnvironmentVariable(envBackup);
            await RestoreGlobalConfigAsync(globalBackup);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenGlobalConfigHasOnlyTheme_EmitsNoNote()
    {
        IGitService? gitService = Substitute.For<IGitService>();
        gitService.GetRepositoryRootAsync()
            .Returns(Task.FromException<string>(new InvalidOperationException("not a git repo")));

        byte[]? globalBackup = await BackupAndDeleteGlobalConfigAsync();
        string? envBackup = SwapThemeEnvironmentVariable(null);
        try
        {
            Directory.CreateDirectory(DotnetGitmojiPaths.UserDataDirectory);
            await File.WriteAllTextAsync(DotnetGitmojiPaths.GlobalConfigPath,
                """{ "theme": "monokai" }""", TestContext.Current.CancellationToken);

            ConfigurationService service = new(gitService);
            (ToolConfiguration config, string stderr) = await LoadCapturingStdErrAsync(service);

            Assert.Equal("monokai", config.Theme);
            Assert.Empty(stderr);
        }
        finally
        {
            SwapThemeEnvironmentVariable(envBackup);
            await RestoreGlobalConfigAsync(globalBackup);
        }
    }

    private static async Task<(ToolConfiguration Config, string StdErr)> LoadCapturingStdErrAsync(
        ConfigurationService service)
    {
        TextWriter originalError = Console.Error;
        await using StringWriter stderr = new();
        Console.SetError(stderr);
        try
        {
            ToolConfiguration config = await service.LoadAsync();
            return (config, stderr.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }
}