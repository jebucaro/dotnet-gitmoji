using CliFx.Infrastructure;
using DotnetGitmoji.Commands;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using DotnetGitmoji.Theming;
using NSubstitute;

namespace DotnetGitmoji.Tests;

public class SearchCommandTests
{
    private const string TestKeyword = "bug";
    private const string MarkupKeyword = "<bug>";

    private readonly IGitmojiProvider _gitmojiProvider = Substitute.For<IGitmojiProvider>();
    private readonly IConfigurationService _configService = Substitute.For<IConfigurationService>();

    private SearchCommand CreateCommand(string keyword = TestKeyword)
    {
        _configService.LoadAsync().Returns(new ToolConfiguration());
        return new SearchCommand(_gitmojiProvider, _configService) { Keyword = keyword };
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoResults_CallsSearchAsyncAndDoesNotThrow()
    {
        _gitmojiProvider.SearchAsync(TestKeyword).Returns(Array.Empty<Gitmoji>());
        SearchCommand command = CreateCommand();
        FakeInMemoryConsole console = new();

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
        SearchCommand command = CreateCommand();
        FakeInMemoryConsole console = new();

        await command.ExecuteAsync(console);

        await _gitmojiProvider.Received(1).SearchAsync(TestKeyword);
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeywordContainsMarkupChars_EscapesKeywordSafely()
    {
        _gitmojiProvider.SearchAsync(MarkupKeyword).Returns(Array.Empty<Gitmoji>());
        SearchCommand command = CreateCommand(MarkupKeyword);
        FakeInMemoryConsole console = new();

        await command.ExecuteAsync(console);

        await _gitmojiProvider.Received(1).SearchAsync(MarkupKeyword);
    }

    [Fact]
    public void HighlightKeyword_WhenKeywordFound_WrapsMatchInEmphasisMarkup()
    {
        ThemePalette theme = Themes.Default;

        string result = SearchCommand.HighlightKeyword("Fix a bug", "bug", theme);

        Assert.Equal($"Fix a [{theme.EmphasisMarkup}]bug[/]", result);
    }

    [Fact]
    public void HighlightKeyword_IsCaseInsensitive()
    {
        ThemePalette theme = Themes.Default;

        string result = SearchCommand.HighlightKeyword("Fix a Bug", "bug", theme);

        Assert.Equal($"Fix a [{theme.EmphasisMarkup}]Bug[/]", result);
    }

    [Fact]
    public void HighlightKeyword_WhenKeywordNotFound_ReturnsValueUnchanged()
    {
        string result = SearchCommand.HighlightKeyword("Improve structure", "xyz", Themes.Default);

        Assert.Equal("Improve structure", result);
    }

    [Fact]
    public void HighlightKeyword_WhenKeywordEmpty_ReturnsValueUnchanged()
    {
        string result = SearchCommand.HighlightKeyword("Improve structure", "", Themes.Default);

        Assert.Equal("Improve structure", result);
    }

    [Fact]
    public void HighlightKeyword_WhenValueContainsMarkupChars_EscapesNonMatchingParts()
    {
        ThemePalette theme = Themes.Default;

        string result = SearchCommand.HighlightKeyword("Improve [core] structure", "core", theme);

        Assert.Equal($"Improve [[[{theme.EmphasisMarkup}]core[/]]] structure", result);
    }

    [Fact]
    public void HighlightKeyword_WhenValueIsColonWrappedAndKeywordNotFound_NeutralizesSoEmojiNotConverted()
    {
        // :building_construction: is a valid emoji shortcode. Without neutralization,
        // Spectre.Console's Markup parser would convert it to the bare emoji glyph.
        // The zero-width space after the leading colon breaks the pattern.
        string result = SearchCommand.HighlightKeyword(":building_construction:", "xyz", Themes.Default);

        // Should contain the zero-width space after first colon, which prevents emoji conversion
        Assert.Contains(":​", result);
    }

    [Fact]
    public void HighlightKeyword_WhenValueIsColonWrappedAndKeywordMatches_HighlightsCorrectly()
    {
        ThemePalette theme = Themes.Default;

        string result = SearchCommand.HighlightKeyword(":bug:", "bug", theme);

        // Should neutralize the colon-wrapped pattern and still highlight the keyword
        Assert.Contains($"[{theme.EmphasisMarkup}]bug[/]", result);
        Assert.Contains(":​", result); // Zero-width space for neutralization
    }

    [Fact]
    public void HighlightKeyword_WhenValueIsColonWrappedAndKeywordEmpty_NeutralizesSoEmojiNotConverted()
    {
        // With empty keyword, should still neutralize to prevent emoji conversion
        string result = SearchCommand.HighlightKeyword(":rocket:", "", Themes.Default);

        // Should contain the zero-width space after first colon, which prevents emoji conversion
        Assert.Contains(":​", result);
    }
}