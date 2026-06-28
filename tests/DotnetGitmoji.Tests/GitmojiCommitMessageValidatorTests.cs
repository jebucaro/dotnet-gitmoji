using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using DotnetGitmoji.Validators;

namespace DotnetGitmoji.Tests;

public class GitmojiCommitMessageValidatorTests
{
    [Fact]
    public void Validate_WhenMessageStartsWithEmoji_ReturnsMatch()
    {
        Gitmoji[] gitmojis = new[]
        {
            new Gitmoji("🎨", "entity", ":art:", "desc", "art", null),
            new Gitmoji("🐛", "entity", ":bug:", "desc", "bug", null)
        };

        GitmojiCommitMessageValidator validator = new();

        ValidationResult result = validator.Validate(new CommitMessageContent("🎨  Improve structure", null), gitmojis);

        Assert.True(result.IsValid);
        Assert.Equal(gitmojis[0], result.MatchedGitmoji);
        Assert.Equal("Improve structure", result.ParsedTitle);
    }

    [Fact]
    public void Validate_WhenMessageStartsWithShortcode_ReturnsMatch()
    {
        Gitmoji[] gitmojis = new[] { new Gitmoji("🐛", "entity", ":bug:", "desc", "bug", null) };

        GitmojiCommitMessageValidator validator = new();

        ValidationResult result = validator.Validate(new CommitMessageContent(":bug: Fix issue", null), gitmojis);

        Assert.True(result.IsValid);
        Assert.Equal(gitmojis[0], result.MatchedGitmoji);
        Assert.Equal("Fix issue", result.ParsedTitle);
    }

    [Fact]
    public void Validate_WhenMessageStartsWithVariationSelectorEmoji_ReturnsMatch()
    {
        Gitmoji[] gitmojis = new[] { new Gitmoji("⚡️", "entity", ":zap:", "desc", "zap", null) };

        GitmojiCommitMessageValidator validator = new();

        ValidationResult result =
            validator.Validate(new CommitMessageContent("⚡️ Improve performance", null), gitmojis);

        Assert.True(result.IsValid);
        Assert.Equal(gitmojis[0], result.MatchedGitmoji);
        Assert.Equal("Improve performance", result.ParsedTitle);
    }

    [Fact]
    public void Validate_WhenShortcodeContainsDigit_ReturnsMatch()
    {
        Gitmoji[] gitmojis = new[] { new Gitmoji("✏️", "entity", ":pencil2:", "desc", "pencil2", null) };

        GitmojiCommitMessageValidator validator = new();

        ValidationResult result = validator.Validate(new CommitMessageContent(":pencil2: Fix typo", null), gitmojis);

        Assert.True(result.IsValid);
        Assert.Equal(gitmojis[0], result.MatchedGitmoji);
        Assert.Equal("Fix typo", result.ParsedTitle);
    }

    [Fact]
    public void Validate_WhenNoMatch_ReturnsInvalid()
    {
        Gitmoji[] gitmojis = new[] { new Gitmoji("🐛", "entity", ":bug:", "desc", "bug", null) };

        GitmojiCommitMessageValidator validator = new();

        ValidationResult result = validator.Validate(new CommitMessageContent("Fix issue", null), gitmojis);

        Assert.False(result.IsValid);
        Assert.Null(result.MatchedGitmoji);
        Assert.Equal("Fix issue", result.ParsedTitle);
    }

    [Fact]
    public void Validate_WhenShortcodeFollowedByScope_ExtractsScopeAndTitle()
    {
        Gitmoji[] gitmojis = new[] { new Gitmoji("🐛", "entity", ":bug:", "desc", "bug", null) };

        GitmojiCommitMessageValidator validator = new();

        ValidationResult result =
            validator.Validate(new CommitMessageContent(":bug: (auth): Fix issue", null), gitmojis);

        Assert.True(result.IsValid);
        Assert.Equal("auth", result.ParsedScope);
        Assert.Equal("Fix issue", result.ParsedTitle);
    }

    [Fact]
    public void Validate_WhenEmojiFollowedByScope_ExtractsScopeAndTitle()
    {
        Gitmoji[] gitmojis = new[] { new Gitmoji("🐛", "entity", ":bug:", "desc", "bug", null) };

        GitmojiCommitMessageValidator validator = new();

        ValidationResult result = validator.Validate(new CommitMessageContent("🐛 (api): Fix issue", null), gitmojis);

        Assert.True(result.IsValid);
        Assert.Equal("api", result.ParsedScope);
        Assert.Equal("Fix issue", result.ParsedTitle);
    }

    [Fact]
    public void Validate_WhenNoScope_ParsedScopeIsNull()
    {
        Gitmoji[] gitmojis = new[] { new Gitmoji("🐛", "entity", ":bug:", "desc", "bug", null) };

        GitmojiCommitMessageValidator validator = new();

        ValidationResult result = validator.Validate(new CommitMessageContent(":bug: Fix issue", null), gitmojis);

        Assert.True(result.IsValid);
        Assert.Null(result.ParsedScope);
        Assert.Equal("Fix issue", result.ParsedTitle);
    }

    [Fact]
    public void Validate_WhenBodyProvided_PassesThroughBody()
    {
        Gitmoji[] gitmojis = new[] { new Gitmoji("🐛", "entity", ":bug:", "desc", "bug", null) };

        GitmojiCommitMessageValidator validator = new();

        ValidationResult result =
            validator.Validate(new CommitMessageContent(":bug: Fix issue", "Some body"), gitmojis);

        Assert.True(result.IsValid);
        Assert.Equal("Some body", result.ParsedBody);
    }

    [Fact]
    public void Validate_WhenBodyIsNull_ParsedBodyIsNull()
    {
        Gitmoji[] gitmojis = new[] { new Gitmoji("🐛", "entity", ":bug:", "desc", "bug", null) };

        GitmojiCommitMessageValidator validator = new();

        ValidationResult result = validator.Validate(new CommitMessageContent(":bug: Fix issue", null), gitmojis);

        Assert.True(result.IsValid);
        Assert.Null(result.ParsedBody);
    }

    [Fact]
    public void Validate_WhenEmojiNormalizedFormat_StripsSeparatorFromTitle()
    {
        Gitmoji[] gitmojis = new[] { new Gitmoji("🐛", "entity", ":bug:", "desc", "bug", null) };

        GitmojiCommitMessageValidator validator = new();

        ValidationResult result =
            validator.Validate(new CommitMessageContent("🐛: Fix null ref crash", null), gitmojis);

        Assert.True(result.IsValid);
        Assert.Null(result.ParsedScope);
        Assert.Equal("Fix null ref crash", result.ParsedTitle);
    }

    [Fact]
    public void Validate_WhenShortcodeNormalizedFormat_StripsSeparatorFromTitle()
    {
        Gitmoji[] gitmojis = new[] { new Gitmoji("🐛", "entity", ":bug:", "desc", "bug", null) };

        GitmojiCommitMessageValidator validator = new();

        ValidationResult result =
            validator.Validate(new CommitMessageContent(":bug:: Fix null ref crash", null), gitmojis);

        Assert.True(result.IsValid);
        Assert.Null(result.ParsedScope);
        Assert.Equal("Fix null ref crash", result.ParsedTitle);
    }

    [Fact]
    public void Validate_WhenLegacyFormatNoScope_TitleUnchanged()
    {
        Gitmoji[] gitmojis = new[] { new Gitmoji("🐛", "entity", ":bug:", "desc", "bug", null) };

        GitmojiCommitMessageValidator validator = new();

        ValidationResult result = validator.Validate(new CommitMessageContent("🐛 Fix null ref crash", null), gitmojis);

        Assert.True(result.IsValid);
        Assert.Null(result.ParsedScope);
        Assert.Equal("Fix null ref crash", result.ParsedTitle);
    }
}