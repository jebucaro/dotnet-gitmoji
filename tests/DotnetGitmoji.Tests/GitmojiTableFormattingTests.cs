using DotnetGitmoji.Theming;

namespace DotnetGitmoji.Tests;

public class GitmojiTableFormattingTests
{
    [Theory]
    [InlineData("patch")]
    [InlineData("minor")]
    [InlineData("major")]
    public void FormatSemver_ForKnownSeverity_UsesMatchingThemeRole(string semver)
    {
        ThemePalette theme = Themes.Default;
        string expectedColor = semver switch
        {
            "patch" => theme.SuccessMarkup,
            "minor" => theme.WarningMarkup,
            "major" => theme.ErrorMarkup,
            _ => throw new InvalidOperationException("Unexpected semver value in test data.")
        };

        string result = GitmojiTableFormatting.FormatSemver(semver, theme);

        Assert.Equal($"[{expectedColor}]{semver}[/]", result);
    }

    [Fact]
    public void FormatSemver_WhenNull_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, GitmojiTableFormatting.FormatSemver(null, Themes.Default));
    }

    [Fact]
    public void FormatSemver_ForUnknownSeverity_FallsBackToAccentColor()
    {
        ThemePalette theme = Themes.Default;

        string result = GitmojiTableFormatting.FormatSemver("unexpected", theme);

        Assert.Equal($"[{theme.AccentMarkup}]unexpected[/]", result);
    }
}
