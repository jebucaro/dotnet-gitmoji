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
}