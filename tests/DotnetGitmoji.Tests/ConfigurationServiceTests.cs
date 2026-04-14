using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using NSubstitute;

namespace DotnetGitmoji.Tests;

public class ConfigurationServiceTests
{
    [Fact]
    public void ToolConfiguration_Defaults_MatchUpstream()
    {
        var config = new ToolConfiguration();

        Assert.False(config.MessagePrompt);
        Assert.False(config.ScopePrompt);
        Assert.True(config.CapitalizeTitle);
        Assert.False(config.AutoAdd);
        Assert.False(config.SignedCommit);
        Assert.Equal(EmojiFormat.Unicode, config.EmojiFormat);
        Assert.Equal("https://gitmoji.dev/api/gitmojis", config.GitmojisUrl);
        Assert.Null(config.Scopes);
    }

    [Fact]
    public async Task LoadAsync_WhenNoConfigFileExists_ReturnsDefaults()
    {
        var gitService = Substitute.For<IGitService>();
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        gitService.GetRepositoryRootAsync().Returns(tempDir);

        try
        {
            var service = new ConfigurationService(gitService);
            var config = await service.LoadAsync();

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
        var gitService = Substitute.For<IGitService>();
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        gitService.GetRepositoryRootAsync().Returns(tempDir);

        var configJson = """{ "MessagePrompt": false, "CapitalizeTitle": false }""";
        await File.WriteAllTextAsync(Path.Combine(tempDir, ".gitmojirc.json"), configJson);

        try
        {
            var service = new ConfigurationService(gitService);
            var config = await service.LoadAsync();

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
        var gitService = Substitute.For<IGitService>();
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        gitService.GetRepositoryRootAsync().Returns(tempDir);

        await File.WriteAllTextAsync(Path.Combine(tempDir, ".gitmojirc.json"), "NOT JSON {{{");

        try
        {
            var service = new ConfigurationService(gitService);
            var config = await service.LoadAsync();

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
        var gitService = Substitute.For<IGitService>();
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        gitService.GetRepositoryRootAsync().Returns(tempDir);

        try
        {
            var service = new ConfigurationService(gitService);
            var createdPath = await service.CreateRepoConfigAsync();

            Assert.NotNull(createdPath);
            Assert.True(File.Exists(createdPath));

            var config = await service.LoadAsync();
            var defaults = new ToolConfiguration();
            Assert.Equal(defaults.EmojiFormat, config.EmojiFormat);
            Assert.Equal(defaults.ScopePrompt, config.ScopePrompt);
            Assert.Equal(defaults.MessagePrompt, config.MessagePrompt);
            Assert.Equal(defaults.CapitalizeTitle, config.CapitalizeTitle);
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
        var gitService = Substitute.For<IGitService>();
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        gitService.GetRepositoryRootAsync().Returns(tempDir);

        var configPath = Path.Combine(tempDir, ".gitmojirc.json");
        var originalContent = """{ "CapitalizeTitle": false }""";
        await File.WriteAllTextAsync(configPath, originalContent);

        try
        {
            var service = new ConfigurationService(gitService);
            var createdPath = await service.CreateRepoConfigAsync();

            Assert.Null(createdPath);
            Assert.Equal(originalContent, await File.ReadAllTextAsync(configPath));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenConfigHasInvalidGitmojisUrl_FallsBackToDefault()
    {
        var gitService = Substitute.For<IGitService>();
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        gitService.GetRepositoryRootAsync().Returns(tempDir);

        var configJson = """{ "GitmojisUrl": "http://insecure.example.com/gitmojis" }""";
        await File.WriteAllTextAsync(Path.Combine(tempDir, ".gitmojirc.json"), configJson);

        try
        {
            var service = new ConfigurationService(gitService);
            var config = await service.LoadAsync();

            Assert.Equal("https://gitmoji.dev/api/gitmojis", config.GitmojisUrl);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}