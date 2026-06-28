using CliFx;
using CliFx.Infrastructure;
using DotnetGitmoji.Commands;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using NSubstitute;

namespace DotnetGitmoji.Tests;

public class CommitCommandValidationTests
{
    private const string HookInstalledFragment = "Cannot use client mode";
    private const string NotInteractiveFragment = "Cannot run in client mode";

    private readonly IGitmojiProvider _gitmojiProvider = Substitute.For<IGitmojiProvider>();
    private readonly IPromptService _promptService = Substitute.For<IPromptService>();
    private readonly IConfigurationService _configService = Substitute.For<IConfigurationService>();
    private readonly IGitService _gitService = Substitute.For<IGitService>();

    public CommitCommandValidationTests()
    {
        _gitService.IsHookInstalledAsync().Returns(false);
        _gitService.HasStagedChangesAsync().Returns(true);
        _promptService.IsInteractive.Returns(true);
        _configService.LoadAsync().Returns(new ToolConfiguration());
        _gitmojiProvider.GetAllAsync().Returns(new[] { new Gitmoji("🎨", "entity", ":art:", "desc", "art", null) });
    }

    private CommitCommand CreateCommand(string? title = null, string? scope = null)
    {
        return new CommitCommand(_gitmojiProvider, _promptService, _configService, _gitService)
        {
            Title = title, Scope = scope
        };
    }

    [Fact]
    public async Task ExecuteAsync_WhenTitleExceedsMaxLength_ThrowsCommandException()
    {
        string longTitle = new('a', ToolConfiguration.DefaultMaxTitleLength + 1);
        CommitCommand command = CreateCommand(longTitle);
        FakeInMemoryConsole console = new();

        CommandException ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains("maximum length", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLimitDisabled_DoesNotRejectLongTitleArg()
    {
        _configService.LoadAsync().Returns(new ToolConfiguration { MaxTitleLength = null });
        _gitService.HasStagedChangesAsync().Returns(false);
        string longTitle = new('a', ToolConfiguration.DefaultMaxTitleLength + 20);
        CommitCommand command = CreateCommand(longTitle);
        FakeInMemoryConsole console = new();

        CommandException ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.DoesNotContain("maximum length", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoStagedChanges_ThrowsFriendlyCommandException()
    {
        _gitService.HasStagedChangesAsync().Returns(false);
        CommitCommand command = CreateCommand("fix something");
        FakeInMemoryConsole console = new();

        CommandException ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains("No staged changes found", ex.Message);
        Assert.Contains("enable autoAdd", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAutoAddEnabledAndNoStagedChanges_ThrowsAutoAddSpecificMessage()
    {
        _configService.LoadAsync().Returns(new ToolConfiguration { AutoAdd = true });
        _gitService.HasStagedChangesAsync().Returns(false);
        CommitCommand command = CreateCommand("fix something");
        FakeInMemoryConsole console = new();

        CommandException ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains("after autoAdd", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("enable autoAdd", ex.Message, StringComparison.OrdinalIgnoreCase);
        await _gitService.Received(1).StageAllAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenAutoAddEnabledAndStageAllFails_ThrowsFriendlyCommandException()
    {
        _configService.LoadAsync().Returns(new ToolConfiguration { AutoAdd = true });
        _gitService.StageAllAsync().Returns(Task.FromException(new InvalidOperationException("permission denied")));
        CommitCommand command = CreateCommand("fix something");
        FakeInMemoryConsole console = new();

        CommandException ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains("Failed to auto-stage changes", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("permission denied", ex.Message, StringComparison.OrdinalIgnoreCase);
        await _gitService.DidNotReceive().HasStagedChangesAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenHasStagedChangesThrows_ThrowsFriendlyCommandException()
    {
        _gitService.HasStagedChangesAsync()
            .Returns(Task.FromException<bool>(new InvalidOperationException("git not available")));
        CommitCommand command = CreateCommand("fix something");
        FakeInMemoryConsole console = new();

        CommandException ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains("Failed to check staged changes", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("git not available", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenScopeHasInvalidChars_ThrowsCommandException()
    {
        CommitCommand command = CreateCommand("test", "scope@");
        FakeInMemoryConsole console = new();

        CommandException ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains("invalid characters", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenScopeExceedsMaxLength_ThrowsCommandException()
    {
        string longScope = new('a', PromptService.MaxScopeLength + 1);
        CommitCommand command = CreateCommand("test", longScope);
        FakeInMemoryConsole console = new();

        CommandException ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains("maximum length", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessagePromptIsFalseAndNoTitleArg_StillPromptsForTitle()
    {
        // Title prompt must be independent of MessagePrompt — title is always required.
        _configService.LoadAsync().Returns(new ToolConfiguration { MessagePrompt = false });
        _promptService.SelectGitmoji(Arg.Any<IReadOnlyList<Gitmoji>>()).Returns(
            new Gitmoji("🎨", "entity", ":art:", "desc", "art", null));
        _promptService.AskTitle(Arg.Any<ToolConfiguration>(), Arg.Any<string?>()).Returns((string?)null);

        CommitCommand command = CreateCommand(); // No title arg
        FakeInMemoryConsole console = new();

        await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        _promptService.Received().AskTitle(Arg.Any<ToolConfiguration>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTitleArgIsProvided_DoesNotPromptForTitle()
    {
        _promptService.SelectGitmoji(Arg.Any<IReadOnlyList<Gitmoji>>()).Returns(
            new Gitmoji("🎨", "entity", ":art:", "desc", "art", null));
        _promptService.AskTitle(Arg.Any<ToolConfiguration>(), Arg.Any<string?>()).Returns((string?)null);
        _gitService.HasStagedChangesAsync().Returns(false);

        CommitCommand command = CreateCommand("fix something");
        FakeInMemoryConsole console = new();

        await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        _promptService.DidNotReceive().AskTitle(Arg.Any<ToolConfiguration>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenHookIsInstalled_ThrowsCommandException()
    {
        _gitService.IsHookInstalledAsync().Returns(true);
        CommitCommand command = CreateCommand("fix something");
        FakeInMemoryConsole console = new();

        CommandException ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains(HookInstalledFragment, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotInteractive_ThrowsCommandException()
    {
        _promptService.IsInteractive.Returns(false);
        CommitCommand command = CreateCommand("fix something");
        FakeInMemoryConsole console = new();

        CommandException ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains(NotInteractiveFragment, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEmojiFormatIsCode_UsesCodePrefixInCommit()
    {
        _configService.LoadAsync().Returns(new ToolConfiguration { EmojiFormat = EmojiFormat.Code });
        _promptService.SelectGitmoji(Arg.Any<IReadOnlyList<Gitmoji>>()).Returns(
            new Gitmoji("🎨", "entity", ":art:", "desc", "art", null));

        CommitCommand command = CreateCommand("fix something");
        FakeInMemoryConsole console = new();

        await command.ExecuteAsync(console);

        await _gitService.Received(1).CommitAsync(
            Arg.Is<string>(s => s.Contains(":art:") && !s.Contains("🎨")),
            Arg.Any<string?>(),
            false);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCapitalizeTitleIsTrue_CapitalizesTitleInCommit()
    {
        _configService.LoadAsync().Returns(new ToolConfiguration { CapitalizeTitle = true });
        _promptService.SelectGitmoji(Arg.Any<IReadOnlyList<Gitmoji>>()).Returns(
            new Gitmoji("🎨", "entity", ":art:", "desc", "art", null));

        CommitCommand command = CreateCommand("fix something");
        FakeInMemoryConsole console = new();

        await command.ExecuteAsync(console);

        await _gitService.Received(1).CommitAsync(
            Arg.Is<string>(s => s.Contains("Fix something")),
            Arg.Any<string?>(),
            false);
    }

    [Fact]
    public async Task ExecuteAsync_WhenScopePromptIsTrue_PromptsForScope()
    {
        _configService.LoadAsync().Returns(new ToolConfiguration { ScopePrompt = true });
        _promptService.SelectGitmoji(Arg.Any<IReadOnlyList<Gitmoji>>()).Returns(
            new Gitmoji("🎨", "entity", ":art:", "desc", "art", null));
        _promptService.AskScope(Arg.Any<string[]?>()).Returns((string?)null);
        _promptService.AskTitle(Arg.Any<ToolConfiguration>(), Arg.Any<string?>()).Returns((string?)null);

        CommitCommand command = CreateCommand();
        FakeInMemoryConsole console = new();

        await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        _promptService.Received(1).AskScope(Arg.Any<string[]?>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessagePromptIsTrue_PromptsForBody()
    {
        _configService.LoadAsync().Returns(new ToolConfiguration { MessagePrompt = true });
        _promptService.SelectGitmoji(Arg.Any<IReadOnlyList<Gitmoji>>()).Returns(
            new Gitmoji("🎨", "entity", ":art:", "desc", "art", null));
        _promptService.AskMessage().Returns("some body");

        CommitCommand command = CreateCommand("fix something");
        FakeInMemoryConsole console = new();

        await command.ExecuteAsync(console);

        _promptService.Received(1).AskMessage();
        await _gitService.Received(1).CommitAsync(Arg.Any<string>(), "some body", false);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSignedCommitIsTrue_PassesSignedFlagToCommit()
    {
        _configService.LoadAsync().Returns(new ToolConfiguration { SignedCommit = true });
        _promptService.SelectGitmoji(Arg.Any<IReadOnlyList<Gitmoji>>()).Returns(
            new Gitmoji("🎨", "entity", ":art:", "desc", "art", null));

        CommitCommand command = CreateCommand("fix something");
        FakeInMemoryConsole console = new();

        await command.ExecuteAsync(console);

        await _gitService.Received(1).CommitAsync(Arg.Any<string>(), Arg.Any<string?>(), true);
    }

    [Fact]
    public async Task ExecuteAsync_WhenScopePromptIsTrueAndPredefinedScopes_PassesScopesToPrompt()
    {
        string[] predefinedScopes = new[] { "api", "core" };
        _configService.LoadAsync().Returns(new ToolConfiguration { ScopePrompt = true, Scopes = predefinedScopes });
        _promptService.SelectGitmoji(Arg.Any<IReadOnlyList<Gitmoji>>()).Returns(
            new Gitmoji("🎨", "entity", ":art:", "desc", "art", null));
        _promptService.AskScope(Arg.Any<string[]?>()).Returns((string?)null);
        _promptService.AskTitle(Arg.Any<ToolConfiguration>(), Arg.Any<string?>()).Returns((string?)null);

        CommitCommand command = CreateCommand();
        FakeInMemoryConsole console = new();

        await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        _promptService.Received(1).AskScope(
            Arg.Is<string[]?>(s => s != null && s.Contains("api") && s.Contains("core")));
    }

    [Theory]
    [InlineData("🐛", null, "Fix bug", false, "🐛 Fix bug")]
    [InlineData("🐛", "auth", "Fix bug", false, "🐛 (auth): Fix bug")]
    [InlineData("🐛", null, "Fix bug", true, "🐛: Fix bug")]
    [InlineData("🐛", "auth", "Fix bug", true, "🐛 (auth): Fix bug")]
    [InlineData(":bug:", null, "Fix bug", false, ":bug: Fix bug")]
    [InlineData(":bug:", "auth", "Fix bug", false, ":bug: (auth): Fix bug")]
    [InlineData(":bug:", null, "Fix bug", true, ":bug:: Fix bug")]
    [InlineData(":bug:", "auth", "Fix bug", true, ":bug: (auth): Fix bug")]
    public void BuildSubject_ProducesCorrectFormat(
        string prefix, string? scope, string title, bool normalize, string expected)
    {
        string result = CommitCommand.BuildSubject(prefix, scope, title, normalize);

        Assert.Equal(expected, result);
    }
}