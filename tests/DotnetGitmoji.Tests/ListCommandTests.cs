using CliFx.Infrastructure;
using DotnetGitmoji.Commands;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using NSubstitute;

namespace DotnetGitmoji.Tests;

public class ListCommandTests
{
    private readonly IGitmojiProvider _gitmojiProvider = Substitute.For<IGitmojiProvider>();

    private ListCommand CreateCommand()
    {
        return new ListCommand(_gitmojiProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGitmojisAvailable_CallsGetAllAsync()
    {
        _gitmojiProvider.GetAllAsync().Returns(new[]
        {
            new Gitmoji("🎨", "entity", ":art:", "Improve structure", "art", null),
            new Gitmoji("🐛", "entity", ":bug:", "Fix a bug", "bug", null)
        });
        ListCommand command = CreateCommand();
        FakeInMemoryConsole console = new();

        await command.ExecuteAsync(console);

        await _gitmojiProvider.Received(1).GetAllAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoGitmojis_SucceedsWithEmptyTable()
    {
        _gitmojiProvider.GetAllAsync().Returns(Array.Empty<Gitmoji>());
        ListCommand command = CreateCommand();
        FakeInMemoryConsole console = new();

        await command.ExecuteAsync(console);

        await _gitmojiProvider.Received(1).GetAllAsync();
    }
}