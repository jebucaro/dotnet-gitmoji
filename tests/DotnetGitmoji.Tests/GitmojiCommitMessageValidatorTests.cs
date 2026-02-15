using DotnetGitmoji.Models;
using DotnetGitmoji.Validators;

namespace DotnetGitmoji.Tests;

public class GitmojiCommitMessageValidatorTests
{
    [Fact]
    public void Validate_WhenMessageStartsWithEmoji_ReturnsMatch()
    {
        var gitmojis = new[]
        {
            new Gitmoji("🎨", "entity", ":art:", "desc", "art", null),
            new Gitmoji("🐛", "entity", ":bug:", "desc", "bug", null)
        };

        var validator = new GitmojiCommitMessageValidator();

        var result = validator.Validate("🎨  Improve structure", gitmojis);

        Assert.True(result.IsValid);
        Assert.Equal(gitmojis[0], result.MatchedGitmoji);
        Assert.Equal("Improve structure", result.RemainingMessage);
    }

    [Fact]
    public void Validate_WhenMessageStartsWithShortcode_ReturnsMatch()
    {
        var gitmojis = new[]
        {
            new Gitmoji("🐛", "entity", ":bug:", "desc", "bug", null)
        };

        var validator = new GitmojiCommitMessageValidator();

        var result = validator.Validate(":bug: Fix issue", gitmojis);

        Assert.True(result.IsValid);
        Assert.Equal(gitmojis[0], result.MatchedGitmoji);
        Assert.Equal("Fix issue", result.RemainingMessage);
    }

    [Fact]
    public void Validate_WhenNoMatch_ReturnsInvalid()
    {
        var gitmojis = new[]
        {
            new Gitmoji("🐛", "entity", ":bug:", "desc", "bug", null)
        };

        var validator = new GitmojiCommitMessageValidator();

        var result = validator.Validate("Fix issue", gitmojis);

        Assert.False(result.IsValid);
        Assert.Null(result.MatchedGitmoji);
        Assert.Equal("Fix issue", result.RemainingMessage);
    }
}