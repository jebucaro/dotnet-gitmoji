using CliFx;
using CliFx.Infrastructure;
using DotnetGitmoji.Commands;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using NSubstitute;

namespace DotnetGitmoji.Tests;

public class CommitCommandValidationTests
{
    private readonly IGitmojiProvider _gitmojiProvider = Substitute.For<IGitmojiProvider>();
    private readonly IPromptService _promptService = Substitute.For<IPromptService>();
    private readonly IConfigurationService _configService = Substitute.For<IConfigurationService>();
    private readonly IGitService _gitService = Substitute.For<IGitService>();

    public CommitCommandValidationTests()
    {
        _gitService.IsHookInstalledAsync().Returns(false);
        _promptService.IsInteractive.Returns(true);
        _configService.LoadAsync().Returns(new ToolConfiguration());
        _gitmojiProvider.GetAllAsync().Returns(new[]
        {
            new Gitmoji("🎨", "entity", ":art:", "desc", "art", null)
        });
    }

    private CommitCommand CreateCommand(string? title = null, string? scope = null)
    {
        return new CommitCommand(_gitmojiProvider, _promptService, _configService, _gitService)
        {
            Title = title,
            Scope = scope
        };
    }

    [Fact]
    public async Task ExecuteAsync_WhenTitleExceedsMaxLength_ThrowsCommandException()
    {
        var longTitle = new string('a', PromptService.MaxTitleLength + 1);
        var command = CreateCommand(longTitle);
        var console = new FakeInMemoryConsole();

        var ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains("maximum length", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenScopeHasInvalidChars_ThrowsCommandException()
    {
        var command = CreateCommand("test", "scope@");
        var console = new FakeInMemoryConsole();

        var ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains("invalid characters", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenScopeExceedsMaxLength_ThrowsCommandException()
    {
        var longScope = new string('a', PromptService.MaxScopeLength + 1);
        var command = CreateCommand("test", longScope);
        var console = new FakeInMemoryConsole();

        var ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains("maximum length", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessagePromptIsFalseAndNoTitleArg_StillPromptsForTitle()
    {
        // Title prompt must be independent of MessagePrompt — title is always required.
        _configService.LoadAsync().Returns(new ToolConfiguration { MessagePrompt = false });
        _promptService.SelectGitmoji(Arg.Any<IReadOnlyList<Gitmoji>>()).Returns(
            new Gitmoji("🎨", "entity", ":art:", "desc", "art", null));
        _promptService.AskTitle(Arg.Any<string?>()).Returns((string?)null);

        var command = CreateCommand(); // No title arg
        var console = new FakeInMemoryConsole();

        await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        _promptService.Received().AskTitle(Arg.Any<string?>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTitleArgIsProvided_DoesNotPromptForTitle()
    {
        _promptService.SelectGitmoji(Arg.Any<IReadOnlyList<Gitmoji>>()).Returns(
            new Gitmoji("🎨", "entity", ":art:", "desc", "art", null));
        _promptService.AskTitle(Arg.Any<string?>()).Returns((string?)null);

        var command = CreateCommand("fix something");
        var console = new FakeInMemoryConsole();

        // The command will attempt to run git and fail; we only care about prompt interactions.
        try
        {
            await command.ExecuteAsync(console);
        }
        catch
        {
            /* git execution will fail */
        }

        _promptService.DidNotReceive().AskTitle(Arg.Any<string?>());
    }
}