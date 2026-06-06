using CliFx;
using CliFx.Infrastructure;
using DotnetGitmoji.Commands;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using NSubstitute;

namespace DotnetGitmoji.Tests;

public class UpdateCommandTests
{
    private const string SuccessMessageFragment = "updated successfully";

    private readonly IGitmojiProvider _gitmojiProvider = Substitute.For<IGitmojiProvider>();

    private UpdateCommand CreateCommand()
    {
        return new UpdateCommand(_gitmojiProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRefreshSucceeds_WritesSuccessMessageToOutput()
    {
        _gitmojiProvider.ForceRefreshAsync().Returns(new[]
        {
            new Gitmoji("🎨", "entity", ":art:", "Improve structure", "art", null)
        });
        var command = CreateCommand();
        var console = new FakeInMemoryConsole();

        await command.ExecuteAsync(console);

        var output = console.ReadOutputString();
        Assert.Contains(SuccessMessageFragment, output, StringComparison.OrdinalIgnoreCase);
        await _gitmojiProvider.Received(1).ForceRefreshAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenForceRefreshThrows_PropagatesException()
    {
        _gitmojiProvider.ForceRefreshAsync()
            .Returns(Task.FromException<IReadOnlyList<Gitmoji>>(new InvalidOperationException("network error")));
        var command = CreateCommand();
        var console = new FakeInMemoryConsole();

        await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync(console).AsTask());
    }
}