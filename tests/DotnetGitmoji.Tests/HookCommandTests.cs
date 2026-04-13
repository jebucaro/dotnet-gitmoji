using CliFx;
using CliFx.Infrastructure;
using DotnetGitmoji.Commands;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using DotnetGitmoji.Validators;
using NSubstitute;

namespace DotnetGitmoji.Tests;

public class HookCommandTests
{
    private readonly ICommitMessageService _commitMessageService = Substitute.For<ICommitMessageService>();
    private readonly ICommitMessageValidator _validator = Substitute.For<ICommitMessageValidator>();
    private readonly IGitmojiProvider _gitmojiProvider = Substitute.For<IGitmojiProvider>();
    private readonly IPromptService _promptService = Substitute.For<IPromptService>();
    private readonly IConfigurationService _configService = Substitute.For<IConfigurationService>();

    private HookCommand CreateCommand(string commitMessageFile, string? commitSource = null)
    {
        return new HookCommand(_commitMessageService, _validator, _gitmojiProvider, _promptService, _configService)
        {
            CommitMessageFile = commitMessageFile,
            CommitSource = commitSource
        };
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommitMessageFileNotFound_ThrowsCommandException()
    {
        var command = CreateCommand("nonexistent_file.txt");
        var console = new FakeInMemoryConsole();

        var ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains("not found", ex.Message);
    }

    [Theory]
    [InlineData("merge")]
    [InlineData("squash")]
    [InlineData("commit")]
    public async Task ExecuteAsync_WhenSkippableSource_SkipsValidation(string source)
    {
        var command = CreateCommand("any_file.txt", source);
        var console = new FakeInMemoryConsole();

        await command.ExecuteAsync(console);

        await _commitMessageService.DidNotReceive().ReadMessageAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessagePromptIsTrue_PromptsForBody()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _configService.LoadAsync().Returns(new ToolConfiguration { MessagePrompt = true });
            _gitmojiProvider.GetAllAsync().Returns([new Gitmoji("🎨", "entity", ":art:", "desc", "art", null)]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>()).Returns("bad message without gitmoji");
            _validator.Validate(Arg.Any<string>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(false, null, null));
            _promptService.IsInteractive.Returns(true);
            _promptService.SelectGitmoji(Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new Gitmoji("🎨", "entity", ":art:", "desc", "art", null));
            _promptService.AskTitle(Arg.Any<string?>()).Returns("Fix bug");
            _promptService.AskMessage().Returns((string?)null);

            var command = CreateCommand(tempFile);
            var console = new FakeInMemoryConsole();

            await command.ExecuteAsync(console);

            _promptService.Received().AskMessage();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessagePromptIsFalse_DoesNotPromptForBody()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _configService.LoadAsync().Returns(new ToolConfiguration { MessagePrompt = false });
            _gitmojiProvider.GetAllAsync().Returns([new Gitmoji("🎨", "entity", ":art:", "desc", "art", null)]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>()).Returns("bad message without gitmoji");
            _validator.Validate(Arg.Any<string>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(false, null, null));
            _promptService.IsInteractive.Returns(true);
            _promptService.SelectGitmoji(Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new Gitmoji("🎨", "entity", ":art:", "desc", "art", null));
            _promptService.AskTitle(Arg.Any<string?>()).Returns("Fix bug");

            var command = CreateCommand(tempFile);
            var console = new FakeInMemoryConsole();

            await command.ExecuteAsync(console);

            _promptService.DidNotReceive().AskMessage();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}