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
    private const string NoInteractiveTerminalFragment = "no interactive terminal";
    private const string DoesNotFollowConventionFragment = "does not follow the gitmoji convention";
    private const string ScopeRequiredFragment = "scope is required";
    private const string MessageBodyRequiredFragment = "message body is required";
    private const string ScopeAndMessageBodyFragment = "scope and message body";

    private readonly ICommitMessageService _commitMessageService = Substitute.For<ICommitMessageService>();
    private readonly ICommitMessageValidator _validator = Substitute.For<ICommitMessageValidator>();
    private readonly IGitmojiProvider _gitmojiProvider = Substitute.For<IGitmojiProvider>();
    private readonly IPromptService _promptService = Substitute.For<IPromptService>();
    private readonly IConfigurationService _configService = Substitute.For<IConfigurationService>();

    private static readonly Gitmoji ArtGitmoji = new("🎨", "entity", ":art:", "desc", "art", null);
    private static readonly Gitmoji BugGitmoji = new("🐛", "entity", ":bug:", "desc", "bug", null);

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
            _gitmojiProvider.GetAllAsync().Returns([ArtGitmoji]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>())
                .Returns(new CommitMessageContent("bad message without gitmoji", null));
            _validator.Validate(Arg.Any<CommitMessageContent>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(false, null, null, null, null));
            _promptService.IsInteractive.Returns(true);
            _promptService.SelectGitmoji(Arg.Any<IReadOnlyList<Gitmoji>>()).Returns(ArtGitmoji);
            _promptService.AskTitle(Arg.Any<ToolConfiguration>(), Arg.Any<string?>()).Returns("Fix bug");
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
            _gitmojiProvider.GetAllAsync().Returns([ArtGitmoji]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>())
                .Returns(new CommitMessageContent("bad message without gitmoji", null));
            _validator.Validate(Arg.Any<CommitMessageContent>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(false, null, null, null, null));
            _promptService.IsInteractive.Returns(true);
            _promptService.SelectGitmoji(Arg.Any<IReadOnlyList<Gitmoji>>()).Returns(ArtGitmoji);
            _promptService.AskTitle(Arg.Any<ToolConfiguration>(), Arg.Any<string?>()).Returns("Fix bug");

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

    [Fact]
    public async Task ExecuteAsync_WhenPromptedTitleExceedsLimit_KeepsOriginalMessage()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _configService.LoadAsync().Returns(new ToolConfiguration
                { MaxTitleLength = 10, TrimTitleWhenExceeded = false });
            _gitmojiProvider.GetAllAsync().Returns([ArtGitmoji]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>())
                .Returns(new CommitMessageContent("bad message without gitmoji", null));
            _validator.Validate(Arg.Any<CommitMessageContent>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(false, null, null, null, null));
            _promptService.IsInteractive.Returns(true);
            _promptService.SelectGitmoji(Arg.Any<IReadOnlyList<Gitmoji>>()).Returns(ArtGitmoji);
            _promptService.AskTitle(Arg.Any<ToolConfiguration>(), Arg.Any<string?>())
                .Returns(new string('a', 11));

            var command = CreateCommand(tempFile);
            var console = new FakeInMemoryConsole();

            await command.ExecuteAsync(console);

            await _commitMessageService.DidNotReceive()
                .WriteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenReadMessageFails_WarnsToStderrAndSkipsProcessing()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _commitMessageService.ReadMessageAsync(Arg.Any<string>())
                .Returns(Task.FromException<CommitMessageContent>(new IOException("disk read error")));

            var command = CreateCommand(tempFile);
            var console = new FakeInMemoryConsole();

            await command.ExecuteAsync(console);

            Assert.Contains("could not read commit message file", console.ReadErrorString(),
                StringComparison.OrdinalIgnoreCase);
            await _commitMessageService.DidNotReceive()
                .WriteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenWriteMessageFails_WarnsToStderrWithoutThrowing()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _configService.LoadAsync().Returns(new ToolConfiguration { MessagePrompt = false });
            _gitmojiProvider.GetAllAsync().Returns([ArtGitmoji]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>())
                .Returns(new CommitMessageContent("bad message without gitmoji", null));
            _validator.Validate(Arg.Any<CommitMessageContent>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(false, null, null, null, null));
            _promptService.IsInteractive.Returns(true);
            _promptService.SelectGitmoji(Arg.Any<IReadOnlyList<Gitmoji>>()).Returns(ArtGitmoji);
            _promptService.AskTitle(Arg.Any<ToolConfiguration>(), Arg.Any<string?>()).Returns("Fix bug");
            _commitMessageService.WriteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
                .Returns(Task.FromException(new IOException("disk write error")));

            var command = CreateCommand(tempFile);
            var console = new FakeInMemoryConsole();

            await command.ExecuteAsync(console);

            Assert.Contains("could not write commit message", console.ReadErrorString(),
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenScopePromptEnabled_AsksForScope()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _configService.LoadAsync().Returns(new ToolConfiguration { MessagePrompt = false, ScopePrompt = true });
            _gitmojiProvider.GetAllAsync().Returns([ArtGitmoji]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>())
                .Returns(new CommitMessageContent("bad message", null));
            _validator.Validate(Arg.Any<CommitMessageContent>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(false, null, null, null, null));
            _promptService.IsInteractive.Returns(true);
            _promptService.SelectGitmoji(Arg.Any<IReadOnlyList<Gitmoji>>()).Returns(ArtGitmoji);
            _promptService.AskTitle(Arg.Any<ToolConfiguration>(), Arg.Any<string?>()).Returns("Fix bug");
            _promptService.AskScope(Arg.Any<string[]?>()).Returns((string?)null);

            var command = CreateCommand(tempFile);
            var console = new FakeInMemoryConsole();

            await command.ExecuteAsync(console);

            _promptService.Received(1).AskScope(Arg.Any<string[]?>());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenNonInteractiveAndEnforceConvention_ThrowsCommandException()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _configService.LoadAsync().Returns(new ToolConfiguration { EnforceConvention = true });
            _gitmojiProvider.GetAllAsync().Returns([ArtGitmoji]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>())
                .Returns(new CommitMessageContent("bad message", null));
            _validator.Validate(Arg.Any<CommitMessageContent>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(false, null, null, null, null));
            _promptService.IsInteractive.Returns(false);

            var command = CreateCommand(tempFile);
            var console = new FakeInMemoryConsole();

            var ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

            Assert.Contains(DoesNotFollowConventionFragment, ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenNonInteractiveAndNoEnforceConvention_WritesWarningAndSkips()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _configService.LoadAsync().Returns(new ToolConfiguration { EnforceConvention = false });
            _gitmojiProvider.GetAllAsync().Returns([ArtGitmoji]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>())
                .Returns(new CommitMessageContent("bad message", null));
            _validator.Validate(Arg.Any<CommitMessageContent>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(false, null, null, null, null));
            _promptService.IsInteractive.Returns(false);

            var command = CreateCommand(tempFile);
            var console = new FakeInMemoryConsole();

            await command.ExecuteAsync(console);

            Assert.Contains(NoInteractiveTerminalFragment, console.ReadErrorString(),
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenEmojiFormatIsCode_WritesCodePrefixedMessage()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _configService.LoadAsync().Returns(new ToolConfiguration
            {
                MessagePrompt = false,
                EmojiFormat = EmojiFormat.Code
            });
            _gitmojiProvider.GetAllAsync().Returns([ArtGitmoji]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>())
                .Returns(new CommitMessageContent("bad message", null));
            _validator.Validate(Arg.Any<CommitMessageContent>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(false, null, null, null, null));
            _promptService.IsInteractive.Returns(true);
            _promptService.SelectGitmoji(Arg.Any<IReadOnlyList<Gitmoji>>()).Returns(ArtGitmoji);
            _promptService.AskTitle(Arg.Any<ToolConfiguration>(), Arg.Any<string?>()).Returns("Fix bug");

            var command = CreateCommand(tempFile);
            var console = new FakeInMemoryConsole();

            await command.ExecuteAsync(console);

            await _commitMessageService.Received(1).WriteMessageAsync(
                Arg.Any<string>(),
                Arg.Is<string>(s => s.StartsWith(":art:")),
                Arg.Any<string?>());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenScopePromptEnabledAndMessageHasScope_PassesThrough()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _configService.LoadAsync().Returns(new ToolConfiguration { ScopePrompt = true, MessagePrompt = false });
            _gitmojiProvider.GetAllAsync().Returns([BugGitmoji]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>())
                .Returns(new CommitMessageContent(":bug: (api): Fix issue", null));
            _validator.Validate(Arg.Any<CommitMessageContent>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(true, BugGitmoji, "api", "Fix issue", null));

            var command = CreateCommand(tempFile);
            var console = new FakeInMemoryConsole();

            await command.ExecuteAsync(console);

            await _commitMessageService.DidNotReceive()
                .WriteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenScopePromptEnabledAndScopeMissingAndNonInteractiveAndEnforceConvention_Rejects()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _configService.LoadAsync()
                .Returns(new ToolConfiguration { ScopePrompt = true, MessagePrompt = false, EnforceConvention = true });
            _gitmojiProvider.GetAllAsync().Returns([BugGitmoji]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>())
                .Returns(new CommitMessageContent(":bug: Fix issue", null));
            _validator.Validate(Arg.Any<CommitMessageContent>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(true, BugGitmoji, null, "Fix issue", null));
            _promptService.IsInteractive.Returns(false);

            var command = CreateCommand(tempFile);
            var console = new FakeInMemoryConsole();

            var ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

            Assert.Contains(ScopeRequiredFragment, ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessagePromptEnabledAndBodyMissingAndNonInteractiveAndEnforceConvention_Rejects()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _configService.LoadAsync()
                .Returns(new ToolConfiguration { MessagePrompt = true, ScopePrompt = false, EnforceConvention = true });
            _gitmojiProvider.GetAllAsync().Returns([BugGitmoji]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>())
                .Returns(new CommitMessageContent(":bug: Fix issue", null));
            _validator.Validate(Arg.Any<CommitMessageContent>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(true, BugGitmoji, null, "Fix issue", null));
            _promptService.IsInteractive.Returns(false);

            var command = CreateCommand(tempFile);
            var console = new FakeInMemoryConsole();

            var ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

            Assert.Contains(MessageBodyRequiredFragment, ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenBothPromptsEnabledAndBothMissingAndNonInteractiveAndEnforceConvention_Rejects()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _configService.LoadAsync()
                .Returns(new ToolConfiguration { ScopePrompt = true, MessagePrompt = true, EnforceConvention = true });
            _gitmojiProvider.GetAllAsync().Returns([BugGitmoji]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>())
                .Returns(new CommitMessageContent(":bug: Fix issue", null));
            _validator.Validate(Arg.Any<CommitMessageContent>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(true, BugGitmoji, null, "Fix issue", null));
            _promptService.IsInteractive.Returns(false);

            var command = CreateCommand(tempFile);
            var console = new FakeInMemoryConsole();

            var ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

            Assert.Contains(ScopeAndMessageBodyFragment, ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task
        ExecuteAsync_WhenScopePromptEnabledAndScopeMissingAndNonInteractiveAndNoEnforceConvention_WarnsAndSkips()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _configService.LoadAsync()
                .Returns(new ToolConfiguration
                    { ScopePrompt = true, MessagePrompt = false, EnforceConvention = false });
            _gitmojiProvider.GetAllAsync().Returns([BugGitmoji]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>())
                .Returns(new CommitMessageContent(":bug: Fix issue", null));
            _validator.Validate(Arg.Any<CommitMessageContent>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(true, BugGitmoji, null, "Fix issue", null));
            _promptService.IsInteractive.Returns(false);

            var command = CreateCommand(tempFile);
            var console = new FakeInMemoryConsole();

            await command.ExecuteAsync(console);

            Assert.Contains(NoInteractiveTerminalFragment, console.ReadErrorString(),
                StringComparison.OrdinalIgnoreCase);
            await _commitMessageService.DidNotReceive()
                .WriteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidMessageMissingScopeAndInteractive_AsksForScopeAndWritesMessage()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _configService.LoadAsync()
                .Returns(new ToolConfiguration { ScopePrompt = true, MessagePrompt = false });
            _gitmojiProvider.GetAllAsync().Returns([BugGitmoji]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>())
                .Returns(new CommitMessageContent(":bug: fix issue", null));
            _validator.Validate(Arg.Any<CommitMessageContent>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(true, BugGitmoji, null, "fix issue", null));
            _promptService.IsInteractive.Returns(true);
            _promptService.AskScope(Arg.Any<string[]?>()).Returns("api");

            var command = CreateCommand(tempFile);
            var console = new FakeInMemoryConsole();

            await command.ExecuteAsync(console);

            _promptService.Received(1).AskScope(Arg.Any<string[]?>());
            await _commitMessageService.Received(1).WriteMessageAsync(
                Arg.Any<string>(),
                Arg.Is<string>(s => s.Contains("(api)")),
                Arg.Any<string?>());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidMessageMissingBodyAndInteractive_AsksForBodyAndWritesMessage()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _configService.LoadAsync()
                .Returns(new ToolConfiguration { ScopePrompt = false, MessagePrompt = true });
            _gitmojiProvider.GetAllAsync().Returns([BugGitmoji]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>())
                .Returns(new CommitMessageContent(":bug: fix issue", null));
            _validator.Validate(Arg.Any<CommitMessageContent>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(true, BugGitmoji, null, "fix issue", null));
            _promptService.IsInteractive.Returns(true);
            _promptService.AskMessage().Returns("some body text");

            var command = CreateCommand(tempFile);
            var console = new FakeInMemoryConsole();

            await command.ExecuteAsync(console);

            _promptService.Received(1).AskMessage();
            await _commitMessageService.Received(1).WriteMessageAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Is<string?>(b => b == "some body text"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidMessageMissingScopeAndInteractive_EmbedsScopeInSubject()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _configService.LoadAsync()
                .Returns(new ToolConfiguration
                    { ScopePrompt = true, MessagePrompt = false, EmojiFormat = EmojiFormat.Code });
            _gitmojiProvider.GetAllAsync().Returns([BugGitmoji]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>())
                .Returns(new CommitMessageContent(":bug: fix issue", null));
            _validator.Validate(Arg.Any<CommitMessageContent>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(true, BugGitmoji, null, "fix issue", null));
            _promptService.IsInteractive.Returns(true);
            _promptService.AskScope(Arg.Any<string[]?>()).Returns("core");

            var command = CreateCommand(tempFile);
            var console = new FakeInMemoryConsole();

            await command.ExecuteAsync(console);

            await _commitMessageService.Received(1).WriteMessageAsync(
                Arg.Any<string>(),
                Arg.Is<string>(s => s.StartsWith(":bug:") && s.Contains("(core)")),
                Arg.Any<string?>());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenPrependPathAndScopePromptEnabled_EmbedsScopeInSubject()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _configService.LoadAsync()
                .Returns(new ToolConfiguration { ScopePrompt = true, MessagePrompt = false });
            _gitmojiProvider.GetAllAsync().Returns([ArtGitmoji]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>())
                .Returns(new CommitMessageContent("bad message without gitmoji", null));
            _validator.Validate(Arg.Any<CommitMessageContent>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(false, null, null, null, null));
            _promptService.IsInteractive.Returns(true);
            _promptService.SelectGitmoji(Arg.Any<IReadOnlyList<Gitmoji>>()).Returns(ArtGitmoji);
            _promptService.AskScope(Arg.Any<string[]?>()).Returns("ui");
            _promptService.AskTitle(Arg.Any<ToolConfiguration>(), Arg.Any<string?>()).Returns("Fix layout");

            var command = CreateCommand(tempFile);
            var console = new FakeInMemoryConsole();

            await command.ExecuteAsync(console);

            _promptService.Received(1).AskScope(Arg.Any<string[]?>());
            await _commitMessageService.Received(1).WriteMessageAsync(
                Arg.Any<string>(),
                Arg.Is<string>(s => s.Contains("(ui)")),
                Arg.Any<string?>());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidMessageMissingScopeAndPredefinedScopes_PassesScopesToPrompt()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var predefinedScopes = new[] { "api", "core" };
            _configService.LoadAsync()
                .Returns(new ToolConfiguration
                    { ScopePrompt = true, MessagePrompt = false, Scopes = predefinedScopes });
            _gitmojiProvider.GetAllAsync().Returns([BugGitmoji]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>())
                .Returns(new CommitMessageContent(":bug: fix issue", null));
            _validator.Validate(Arg.Any<CommitMessageContent>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(true, BugGitmoji, null, "fix issue", null));
            _promptService.IsInteractive.Returns(true);
            _promptService.AskScope(Arg.Any<string[]?>()).Returns("api");

            var command = CreateCommand(tempFile);
            var console = new FakeInMemoryConsole();

            await command.ExecuteAsync(console);

            _promptService.Received(1).AskScope(
                Arg.Is<string[]?>(s => s != null && s.Contains("api") && s.Contains("core")));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenPrependPathAndPredefinedScopes_PassesScopesToPrompt()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var predefinedScopes = new[] { "api", "core" };
            _configService.LoadAsync()
                .Returns(new ToolConfiguration
                    { ScopePrompt = true, MessagePrompt = false, Scopes = predefinedScopes });
            _gitmojiProvider.GetAllAsync().Returns([ArtGitmoji]);
            _commitMessageService.ReadMessageAsync(Arg.Any<string>())
                .Returns(new CommitMessageContent("bad message without gitmoji", null));
            _validator.Validate(Arg.Any<CommitMessageContent>(), Arg.Any<IReadOnlyList<Gitmoji>>())
                .Returns(new ValidationResult(false, null, null, null, null));
            _promptService.IsInteractive.Returns(true);
            _promptService.SelectGitmoji(Arg.Any<IReadOnlyList<Gitmoji>>()).Returns(ArtGitmoji);
            _promptService.AskScope(Arg.Any<string[]?>()).Returns("api");
            _promptService.AskTitle(Arg.Any<ToolConfiguration>(), Arg.Any<string?>()).Returns("Fix layout");

            var command = CreateCommand(tempFile);
            var console = new FakeInMemoryConsole();

            await command.ExecuteAsync(console);

            _promptService.Received(1).AskScope(
                Arg.Is<string[]?>(s => s != null && s.Contains("api") && s.Contains("core")));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}