using DotnetGitmoji.Models;
using DotnetGitmoji.Services;

namespace DotnetGitmoji.Tests;

public class GitmojiFuzzyMatcherTests
{
    private static readonly Gitmoji[] Gitmojis =
    [
        new("🎨", "entity", ":art:", "Improve structure / format of the code.", "art", null),
        new("🐛", "entity", ":bug:", "Fix a bug.", "bug", null),
        new("📝", "entity", ":memo:", "Add or update documentation.", "memo", null)
    ];

    [Fact]
    public void RankGitmojis_WhenQueryIsEmpty_ReturnsOriginalOrder()
    {
        GitmojiFuzzyMatcher matcher = new();

        IReadOnlyList<Gitmoji> result = matcher.RankGitmojis(Gitmojis, " ");

        Assert.Equal(Gitmojis, result);
    }

    [Fact]
    public void RankGitmojis_WhenQueryIsMisspelled_MatchesByFuzzyDescription()
    {
        GitmojiFuzzyMatcher matcher = new();

        IReadOnlyList<Gitmoji> result = matcher.RankGitmojis(Gitmojis, "strcture");

        Assert.NotEmpty(result);
        Assert.Equal(":art:", result[0].Code);
    }

    [Fact]
    public void RankGitmojis_WhenQueryMatchesCode_RanksCodeMatchFirst()
    {
        GitmojiFuzzyMatcher matcher = new();

        IReadOnlyList<Gitmoji> result = matcher.RankGitmojis(Gitmojis, ":bug:");

        Assert.NotEmpty(result);
        Assert.Equal(":bug:", result[0].Code);
    }

    [Fact]
    public void RankScopes_WhenQueryIsMisspelled_MatchesClosestScopeFirst()
    {
        GitmojiFuzzyMatcher matcher = new();
        string[] scopes = new[] { "core", "documentation", "api" };

        IReadOnlyList<string> result = matcher.RankScopes(scopes, "documnt");

        Assert.NotEmpty(result);
        Assert.Equal("documentation", result[0]);
    }

    [Fact]
    public void RankScopes_WhenQueryHasNoSignal_ReturnsNoMatches()
    {
        GitmojiFuzzyMatcher matcher = new();
        string[] scopes = new[] { "core", "documentation", "api" };

        IReadOnlyList<string> result = matcher.RankScopes(scopes, "zzzzzz");

        Assert.Empty(result);
    }

    [Fact]
    public void RankGitmojis_WhenQueryIsSubsequenceOfDescription_ReturnsMatch()
    {
        GitmojiFuzzyMatcher matcher = new();

        // "fxbg" is a subsequence of "Fix a bug." characters
        IReadOnlyList<Gitmoji> result = matcher.RankGitmojis(Gitmojis, "fxbg");

        Assert.NotEmpty(result);
        Assert.Equal(":bug:", result[0].Code);
    }

    [Fact]
    public void RankGitmojis_WhenQueryContainsMultipleTokens_MatchesMultiwordDescription()
    {
        GitmojiFuzzyMatcher matcher = new();

        // Two-token query that matches "Improve structure"
        IReadOnlyList<Gitmoji> result = matcher.RankGitmojis(Gitmojis, "improve format");

        Assert.NotEmpty(result);
        Assert.Equal(":art:", result[0].Code);
    }

    [Fact]
    public void RankScopes_WhenQueryMatchesScope_ReturnsScopeAboveThreshold()
    {
        GitmojiFuzzyMatcher matcher = new();
        string[] scopes = new[] { "authentication", "database", "api" };

        IReadOnlyList<string> result = matcher.RankScopes(scopes, "auth");

        Assert.NotEmpty(result);
        Assert.Equal("authentication", result[0]);
    }
}