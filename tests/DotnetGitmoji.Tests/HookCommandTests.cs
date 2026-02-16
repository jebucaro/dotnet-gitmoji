using CliFx.Exceptions;
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
    public async Task ExecuteAsync_WhenMergeOrSquashSource_SkipsValidation(string source)
    {
        var command = CreateCommand("any_file.txt", source);
        var console = new FakeInMemoryConsole();

        await command.ExecuteAsync(console);

        await _commitMessageService.DidNotReceive().ReadMessageAsync(Arg.Any<string>());
    }
}