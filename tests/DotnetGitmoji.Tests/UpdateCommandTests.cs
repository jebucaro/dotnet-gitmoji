using CliFx;
using CliFx.Infrastructure;
using DotnetGitmoji.Commands;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using NSubstitute;

namespace DotnetGitmoji.Tests;

public class UpdateCommandTests
{
    private readonly IGitmojiProvider _gitmojiProvider = Substitute.For<IGitmojiProvider>();
    private readonly IConfigurationService _configService = Substitute.For<IConfigurationService>();

    private UpdateCommand CreateCommand()
    {
        _configService.LoadAsync().Returns(new ToolConfiguration());
        return new UpdateCommand(_gitmojiProvider, _configService);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRefreshSucceeds_CallsForceRefreshAsync()
    {
        _gitmojiProvider.ForceRefreshAsync().Returns(new[]
        {
            new Gitmoji("🎨", "entity", ":art:", "Improve structure", "art", null)
        });
        UpdateCommand command = CreateCommand();
        FakeInMemoryConsole console = new();

        await command.ExecuteAsync(console);

        await _gitmojiProvider.Received(1).ForceRefreshAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenForceRefreshThrows_PropagatesException()
    {
        _gitmojiProvider.ForceRefreshAsync()
            .Returns(Task.FromException<IReadOnlyList<Gitmoji>>(new InvalidOperationException("network error")));
        UpdateCommand command = CreateCommand();
        FakeInMemoryConsole console = new();

        await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync(console).AsTask());
    }
}
