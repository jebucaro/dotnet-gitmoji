using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using DotnetGitmoji.Theming;
using NSubstitute;
using Spectre.Console;

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

    [Fact]
    public void NormalizeEmojiForDisplay_WhenEmojiHasVariationSelector_StripsIt()
    {
        Assert.Equal("\u2b07", GitmojiTableFormatting.NormalizeEmojiForDisplay("\u2b07\uFE0F"));
    }

    [Fact]
    public void NormalizeEmojiForDisplay_WhenPlainEmoji_LeavesItUnchanged()
    {
        Assert.Equal("\U0001F512", GitmojiTableFormatting.NormalizeEmojiForDisplay("\U0001F512"));
    }

    [Fact]
    public void NormalizeEmojiForDisplay_WhenZwjSequence_CollapsesToLeadingGlyph()
    {
        // technologist is person + ZWJ + laptop. Terminals that don't fuse the parts draw it
        // double width, so for display we keep only the leading glyph. The stored emoji is untouched.
        string technologist = "\U0001F9D1\u200D\U0001F4BB";

        Assert.Equal("\U0001F9D1", GitmojiTableFormatting.NormalizeEmojiForDisplay(technologist));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void NormalizeEmojiForDisplay_WhenEmptyOrNull_ReturnsInput(string? input)
    {
        Assert.Equal(input, GitmojiTableFormatting.NormalizeEmojiForDisplay(input!));
    }

    [Fact]
    public async Task EmojiColumnWidth_FitsEveryShippedGitmojiOnceNormalized()
    {
        IReadOnlyList<Gitmoji> gitmojis = await LoadShippedGitmojis();

        Assert.NotEmpty(gitmojis);
        foreach (Gitmoji g in gitmojis)
        {
            int width = GitmojiTableFormatting.NormalizeEmojiForDisplay(g.Emoji).GetCellWidth();
            Assert.True(width <= GitmojiTableFormatting.EmojiColumnWidth,
                $"{g.Code} normalizes to width {width}, over the {GitmojiTableFormatting.EmojiColumnWidth}-cell column.");
        }
    }

    private static async Task<IReadOnlyList<Gitmoji>> LoadShippedGitmojis()
    {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(new UnreachableHandler()));

        // Non-https URL forces the provider past cache and API straight to the embedded default.
        ToolConfiguration config = new() { GitmojisUrl = "http://unused.invalid" };
        GitmojiProvider provider = new(factory, config, new GitmojiFuzzyMatcher());
        return await provider.ForceRefreshAsync();
    }

    private sealed class UnreachableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Network disabled in tests.");
        }
    }
}