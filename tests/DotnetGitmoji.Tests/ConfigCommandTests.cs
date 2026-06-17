using CliFx;
using CliFx.Infrastructure;
using DotnetGitmoji.Commands;
using DotnetGitmoji.Services;
using NSubstitute;

namespace DotnetGitmoji.Tests;

public class ConfigCommandTests
{
    private const string PositiveIntegerErrorFragment = "positive integer";
    private const string HttpsUrlErrorFragment = "HTTPS URL";

    private readonly IConfigurationService _configService = Substitute.For<IConfigurationService>();

    private ConfigCommand CreateCommand(bool global = false, bool local = false)
    {
        return new ConfigCommand(_configService) { Global = global, Local = local };
    }

    [Fact]
    public async Task ExecuteAsync_WhenBothGlobalAndLocalSpecified_ThrowsCommandException()
    {
        var command = CreateCommand(true, true);
        var console = new FakeInMemoryConsole();

        var ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains("Cannot specify both", ex.Message, StringComparison.OrdinalIgnoreCase);
        await _configService.DidNotReceive().LoadAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenLoadConfigThrowsIoException_ThrowsFriendlyCommandException()
    {
        _configService.LoadAsync()
            .Returns(Task.FromException<Models.ToolConfiguration>(
                new IOException("config file locked")));

        var command = CreateCommand();
        var console = new FakeInMemoryConsole();

        var ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains("Failed to load configuration", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("config file locked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateMaxTitleLengthInput_WhenEmpty_ReturnsSuccess()
    {
        var result = ConfigCommand.ValidateMaxTitleLengthInput(string.Empty);

        Assert.True(result.Successful);
    }

    [Fact]
    public void ValidateMaxTitleLengthInput_WhenPositiveInteger_ReturnsSuccess()
    {
        var result = ConfigCommand.ValidateMaxTitleLengthInput("72");

        Assert.True(result.Successful);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("1.5")]
    public void ValidateMaxTitleLengthInput_WhenInvalidInput_ReturnsError(string input)
    {
        var result = ConfigCommand.ValidateMaxTitleLengthInput(input);

        Assert.False(result.Successful);
        Assert.Contains(PositiveIntegerErrorFragment, result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateGitmojisUrl_WhenHttpsUrl_ReturnsSuccess()
    {
        var result = ConfigCommand.ValidateGitmojisUrl("https://gitmoji.dev/api/gitmojis");

        Assert.True(result.Successful);
    }

    [Theory]
    [InlineData("http://gitmoji.dev/api/gitmojis")]
    [InlineData("ftp://gitmoji.dev")]
    [InlineData("not-a-url")]
    public void ValidateGitmojisUrl_WhenNonHttpsOrInvalidUrl_ReturnsError(string url)
    {
        var result = ConfigCommand.ValidateGitmojisUrl(url);

        Assert.False(result.Successful);
        Assert.Contains(HttpsUrlErrorFragment, result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatEmojiChoice_WhenEmoji_ReturnsEmojiLabel()
    {
        var result = ConfigCommand.FormatEmojiChoice(Models.EmojiFormat.Emoji);

        Assert.Contains("Emoji", result);
    }

    [Fact]
    public void FormatEmojiChoice_WhenCode_ReturnsCodeLabel()
    {
        var result = ConfigCommand.FormatEmojiChoice(Models.EmojiFormat.Code);

        Assert.Contains("Code", result);
    }

    [Fact]
    public void ParseScopes_WhenInputIsEmpty_ReturnsNull()
    {
        Assert.Null(ConfigCommand.ParseScopes(string.Empty));
    }

    [Fact]
    public void ParseScopes_WhenInputIsWhitespace_ReturnsNull()
    {
        Assert.Null(ConfigCommand.ParseScopes("   "));
    }

    [Theory]
    [InlineData("api,core", new[] { "api", "core" })]
    [InlineData(" api , core , ui ", new[] { "api", "core", "ui" })]
    [InlineData("single", new[] { "single" })]
    public void ParseScopes_WhenCommaSeparated_ReturnsTrimmedArray(string input, string[] expected)
    {
        var result = ConfigCommand.ParseScopes(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void DetermineTarget_WhenGlobalTrue_ReturnsGlobal()
    {
        var command = CreateCommand(true);

        Assert.Equal(Models.ConfigSaveTarget.Global, command.DetermineTarget());
    }

    [Fact]
    public void DetermineTarget_WhenLocalTrue_ReturnsLocal()
    {
        var command = CreateCommand(local: true);

        Assert.Equal(Models.ConfigSaveTarget.Local, command.DetermineTarget());
    }

    [Fact]
    public void DetermineTarget_WhenNeither_ReturnsAuto()
    {
        var command = CreateCommand();

        Assert.Equal(Models.ConfigSaveTarget.Auto, command.DetermineTarget());
    }
}