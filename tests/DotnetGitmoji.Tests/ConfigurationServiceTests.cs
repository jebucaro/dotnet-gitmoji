using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
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

    [Fact]
    public async Task SaveAsync_WhenAutoTargetInGitRepo_SavesRepoFile()
    {
        IGitService? gitService = Substitute.For<IGitService>();
        string tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        gitService.GetRepositoryRootAsync().Returns(tempDir);

        try
        {
            ConfigurationService service = new(gitService);
            ToolConfiguration config = new() { CapitalizeTitle = false };

            await service.SaveAsync(config, ConfigSaveTarget.Auto);

            string savedPath = Path.Combine(tempDir, ".gitmojirc.json");
            Assert.True(File.Exists(savedPath));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SaveAsync_WhenAutoTargetAndGitServiceThrows_SavesGlobalConfig()
    {
        IGitService? gitService = Substitute.For<IGitService>();
        gitService.GetRepositoryRootAsync()
            .Returns(Task.FromException<string>(new InvalidOperationException("not a git repo")));

        string globalPath = DotnetGitmojiPaths.GlobalConfigPath;
        bool hadGlobal = File.Exists(globalPath);
        byte[]? backup = hadGlobal
            ? await File.ReadAllBytesAsync(globalPath, TestContext.Current.CancellationToken)
            : null;

        try
        {
            ConfigurationService service = new(gitService);
            ToolConfiguration config = new() { CapitalizeTitle = false };

            await service.SaveAsync(config, ConfigSaveTarget.Auto);

            Assert.True(File.Exists(globalPath));
        }
        finally
        {
            if (backup is not null)
            {
                await File.WriteAllBytesAsync(globalPath, backup, TestContext.Current.CancellationToken);
            }
            else if (!hadGlobal && File.Exists(globalPath))
            {
                File.Delete(globalPath);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_WhenLocalTarget_SavesRepoFile()
    {
        IGitService? gitService = Substitute.For<IGitService>();
        string tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        gitService.GetRepositoryRootAsync().Returns(tempDir);

        try
        {
            ConfigurationService service = new(gitService);
            ToolConfiguration config = new() { CapitalizeTitle = false };

            await service.SaveAsync(config, ConfigSaveTarget.Local);

            Assert.True(File.Exists(Path.Combine(tempDir, ".gitmojirc.json")));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenGlobalConfigExistsAndNoRepoConfig_LoadsGlobalConfig()
    {
        IGitService? gitService = Substitute.For<IGitService>();
        gitService.GetRepositoryRootAsync()
            .Returns(Task.FromException<string>(new InvalidOperationException("not a git repo")));

        string globalPath = DotnetGitmojiPaths.GlobalConfigPath;
        bool hadGlobal = File.Exists(globalPath);
        byte[]? backup = hadGlobal
            ? await File.ReadAllBytesAsync(globalPath, TestContext.Current.CancellationToken)
            : null;

        try
        {
            Directory.CreateDirectory(DotnetGitmojiPaths.UserDataDirectory);
            string globalConfig = """{ "CapitalizeTitle": false }""";
            await File.WriteAllTextAsync(globalPath, globalConfig, TestContext.Current.CancellationToken);

            ConfigurationService service = new(gitService);
            ToolConfiguration config = await service.LoadAsync();

            Assert.False(config.CapitalizeTitle);
        }
        finally
        {
            if (backup is not null)
            {
                await File.WriteAllBytesAsync(globalPath, backup, TestContext.Current.CancellationToken);
            }
            else if (File.Exists(globalPath))
            {
                File.Delete(globalPath);
            }
        }
    }
}