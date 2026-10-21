using CliFx.Infrastructure;
using DotnetGitmoji.Commands;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using NSubstitute;

namespace DotnetGitmoji.Tests;

public class ListCommandTests
{
    private readonly IGitmojiProvider _gitmojiProvider = Substitute.For<IGitmojiProvider>();
    private readonly IConfigurationService _configService = Substitute.For<IConfigurationService>();

    private ListCommand CreateCommand()
    {
        _configService.LoadAsync().Returns(new ToolConfiguration());
        return new ListCommand(_gitmojiProvider, _configService);
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

    [Fact]
    public async Task ExecuteAsync_WhenGitmojiHasVariationSelector_RendersWithoutThrowing()
    {
        // :arrow_down: ships as U+2B07 + VS16, the sequence that drifted the column across terminals.
        _gitmojiProvider.GetAllAsync().Returns(new[]
        {
            new Gitmoji("\u2b07\uFE0F", "entity", ":arrow_down:", "Downgrade dependencies", "arrow_down", "patch"),
            new Gitmoji("\U0001F9D1\u200D\U0001F4BB", "entity", ":technologist:", "Improve DX", "technologist", null)
        });
        ListCommand command = CreateCommand();
        FakeInMemoryConsole console = new();

        await command.ExecuteAsync(console);

        await _gitmojiProvider.Received(1).GetAllAsync();
    }
}