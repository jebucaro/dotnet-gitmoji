using CliFx.Infrastructure;
using DotnetGitmoji.Commands;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using NSubstitute;

namespace DotnetGitmoji.Tests;

public class SearchCommandTests
{
    private const string TestKeyword = "bug";
    private const string MarkupKeyword = "<bug>";

    private readonly IGitmojiProvider _gitmojiProvider = Substitute.For<IGitmojiProvider>();

    private SearchCommand CreateCommand(string keyword = TestKeyword)
    {
        return new SearchCommand(_gitmojiProvider) { Keyword = keyword };
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoResults_CallsSearchAsyncAndDoesNotThrow()
    {
        _gitmojiProvider.SearchAsync(TestKeyword).Returns(Array.Empty<Gitmoji>());
        var command = CreateCommand();
        var console = new FakeInMemoryConsole();

        await command.ExecuteAsync(console);

        await _gitmojiProvider.Received(1).SearchAsync(TestKeyword);
    }

    [Fact]
    public async Task ExecuteAsync_WhenResultsFound_CallsSearchAsyncAndDoesNotThrow()
    {
        _gitmojiProvider.SearchAsync(TestKeyword).Returns(new[]
        {
            new Gitmoji("🐛", "entity", ":bug:", "Fix a bug", "bug", null)
        });
        var command = CreateCommand();
        var console = new FakeInMemoryConsole();

        await command.ExecuteAsync(console);

        await _gitmojiProvider.Received(1).SearchAsync(TestKeyword);
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeywordContainsMarkupChars_EscapesKeywordSafely()
    {
        _gitmojiProvider.SearchAsync(MarkupKeyword).Returns(Array.Empty<Gitmoji>());
        var command = CreateCommand(MarkupKeyword);
        var console = new FakeInMemoryConsole();

        await command.ExecuteAsync(console);

        await _gitmojiProvider.Received(1).SearchAsync(MarkupKeyword);
    }
}