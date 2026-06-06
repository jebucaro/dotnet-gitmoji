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
        var matcher = new GitmojiFuzzyMatcher();

        var result = matcher.RankGitmojis(Gitmojis, " ");

        Assert.Equal(Gitmojis, result);
    }

    [Fact]
    public void RankGitmojis_WhenQueryIsMisspelled_MatchesByFuzzyDescription()
    {
        var matcher = new GitmojiFuzzyMatcher();

        var result = matcher.RankGitmojis(Gitmojis, "strcture");

        Assert.NotEmpty(result);
        Assert.Equal(":art:", result[0].Code);
    }

    [Fact]
    public void RankGitmojis_WhenQueryMatchesCode_RanksCodeMatchFirst()
    {
        var matcher = new GitmojiFuzzyMatcher();

        var result = matcher.RankGitmojis(Gitmojis, ":bug:");

        Assert.NotEmpty(result);
        Assert.Equal(":bug:", result[0].Code);
    }

    [Fact]
    public void RankScopes_WhenQueryIsMisspelled_MatchesClosestScopeFirst()
    {
        var matcher = new GitmojiFuzzyMatcher();
        var scopes = new[] { "core", "documentation", "api" };

        var result = matcher.RankScopes(scopes, "documnt");

        Assert.NotEmpty(result);
        Assert.Equal("documentation", result[0]);
    }

    [Fact]
    public void RankScopes_WhenQueryHasNoSignal_ReturnsNoMatches()
    {
        var matcher = new GitmojiFuzzyMatcher();
        var scopes = new[] { "core", "documentation", "api" };

        var result = matcher.RankScopes(scopes, "zzzzzz");

        Assert.Empty(result);
    }

    [Fact]
    public void RankGitmojis_WhenQueryIsSubsequenceOfDescription_ReturnsMatch()
    {
        var matcher = new GitmojiFuzzyMatcher();

        // "fxbg" is a subsequence of "Fix a bug." characters
        var result = matcher.RankGitmojis(Gitmojis, "fxbg");

        Assert.NotEmpty(result);
        Assert.Equal(":bug:", result[0].Code);
    }

    [Fact]
    public void RankGitmojis_WhenQueryContainsMultipleTokens_MatchesMultiwordDescription()
    {
        var matcher = new GitmojiFuzzyMatcher();

        // Two-token query that matches "Improve structure"
        var result = matcher.RankGitmojis(Gitmojis, "improve format");

        Assert.NotEmpty(result);
        Assert.Equal(":art:", result[0].Code);
    }

    [Fact]
    public void RankScopes_WhenQueryMatchesScope_ReturnsScopeAboveThreshold()
    {
        var matcher = new GitmojiFuzzyMatcher();
        var scopes = new[] { "authentication", "database", "api" };

        var result = matcher.RankScopes(scopes, "auth");

        Assert.NotEmpty(result);
        Assert.Equal("authentication", result[0]);
    }
}