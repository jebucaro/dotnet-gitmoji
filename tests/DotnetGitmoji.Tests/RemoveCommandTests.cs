using CliFx;
using CliFx.Infrastructure;
using DotnetGitmoji.Commands;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using NSubstitute;

namespace DotnetGitmoji.Tests;

public class RemoveCommandTests
{
    private const string NotInGitRepoFragment = "Not a git repository";
    private const string NoHookFoundFragment = "No dotnet-gitmoji hook found";

    private readonly IGitService _gitService = Substitute.For<IGitService>();

    private RemoveCommand CreateCommand()
    {
        return new RemoveCommand(_gitService);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotInGitRepo_ThrowsFriendlyCommandException()
    {
        _gitService.FindHookFileAsync()
            .Returns(Task.FromException<string?>(new InvalidOperationException("Not a git repository.")));

        RemoveCommand command = CreateCommand();
        FakeInMemoryConsole console = new();

        CommandException ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains(NotInGitRepoFragment, ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoHookFound_ThrowsCommandException()
    {
        _gitService.FindHookFileAsync().Returns((string?)null);

        RemoveCommand command = CreateCommand();
        FakeInMemoryConsole console = new();

        CommandException ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains(NoHookFoundFragment, ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHookIsInHuskyDirectory_ShowsGuidanceWithoutCallingRemoveDirect()
    {
        _gitService.FindHookFileAsync().Returns(".husky/prepare-commit-msg");
        _gitService.DetectHuskyKindAsync().Returns(HuskyInstallKind.HuskyNetShell);

        RemoveCommand command = CreateCommand();
        FakeInMemoryConsole console = new();

        await command.ExecuteAsync(console);

        await _gitService.DidNotReceive().RemoveHookDirectAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRemoveHookDirectFails_ThrowsFriendlyCommandException()
    {
        string hookPath = Path.Combine(".git", "hooks", "prepare-commit-msg");
        _gitService.FindHookFileAsync().Returns(hookPath);
        _gitService.RemoveHookDirectAsync()
            .Returns(Task.FromException(new InvalidOperationException("Permission denied.")));

        RemoveCommand command = CreateCommand();
        FakeInMemoryConsole console = new();

        CommandException ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains("Permission denied", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHookIsInGitHooksDirectory_CallsRemoveHookDirect()
    {
        string hookPath = Path.Combine(".git", "hooks", "prepare-commit-msg");
        _gitService.FindHookFileAsync().Returns(hookPath);

        RemoveCommand command = CreateCommand();
        FakeInMemoryConsole console = new();

        await command.ExecuteAsync(console);

        await _gitService.Received(1).RemoveHookDirectAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenHookIsInHuskyDirectoryAndJsHusky_ShowsGuidanceText()
    {
        _gitService.FindHookFileAsync().Returns(".husky/prepare-commit-msg");
        _gitService.DetectHuskyKindAsync().Returns(HuskyInstallKind.JsHusky);

        RemoveCommand command = CreateCommand();
        FakeInMemoryConsole console = new();

        await command.ExecuteAsync(console);

        await _gitService.DidNotReceive().RemoveHookDirectAsync();
    }
}