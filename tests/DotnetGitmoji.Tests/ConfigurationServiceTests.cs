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