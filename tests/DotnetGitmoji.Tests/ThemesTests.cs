using DotnetGitmoji.Services;
using DotnetGitmoji.Theming;

namespace DotnetGitmoji.Tests;

public class ThemesTests
{
    [Fact]
    public void Names_ContainsAllBuiltInThemes_DefaultFirst()
    {
        Assert.Equal(6, Themes.Names.Count);
        Assert.Equal(Themes.DefaultName, Themes.Names[0]);
        Assert.Contains("monokai", Themes.Names);
        Assert.Contains("catppuccin-latte", Themes.Names);
        Assert.Contains("catppuccin-frappe", Themes.Names);
        Assert.Contains("catppuccin-macchiato", Themes.Names);
        Assert.Contains("catppuccin-mocha", Themes.Names);
    }

    [Fact]
    public void IsKnown_ForEveryBuiltInName_ReturnsTrue()
    {
        Assert.All(Themes.Names, name => Assert.True(Themes.IsKnown(name)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("solarized")]
    public void IsKnown_ForUnknownName_ReturnsFalse(string? name)
    {
        Assert.False(Themes.IsKnown(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("solarized")]
    public void Resolve_ForUnknownName_FallsBackToDefault(string? name)
    {
        Assert.Same(Themes.Default, Themes.Resolve(name));
    }

    [Theory]
    [InlineData("MONOKAI")]
    [InlineData("Catppuccin-Mocha")]
    public void Resolve_IsCaseInsensitive(string name)
    {
        Assert.NotSame(Themes.Default, Themes.Resolve(name));
    }

    [Fact]
    public void Resolve_ForEveryBuiltInName_ReturnsMatchingPalette()
    {
        Assert.All(Themes.Names, name => Assert.Equal(name, Themes.Resolve(name).Name));
    }

    [Fact]
    public void DefaultTheme_ReproducesLegacyMarkupColors()
    {
        // These exact strings were the hardcoded markup literals before theming existed;
        // the default theme must keep emitting them so existing users see zero change.
        Assert.Equal("purple", Themes.Default.BrandPrimaryMarkup);
        Assert.Equal("white", Themes.Default.BrandSecondaryMarkup);
        Assert.Equal("gold1", Themes.Default.BrandTertiaryMarkup);
        Assert.Equal("green", Themes.Default.SuccessMarkup);
        Assert.Equal("yellow", Themes.Default.WarningMarkup);
        Assert.Equal("red", Themes.Default.ErrorMarkup);
        Assert.Equal("grey", Themes.Default.MutedMarkup);
        Assert.Equal("white", Themes.Default.EmphasisMarkup);
        Assert.Equal("blue", Themes.Default.AccentMarkup);
        Assert.Equal("green", Themes.Default.SelectionMarkerMarkup);
    }

    [Fact]
    public void BuildBannerMarkup_WithDefaultTheme_ReproducesLegacyBanner()
    {
        // Verbatim value of the BannerTitle constant that theming replaced.
        Assert.Equal(
            "[bold][purple]dotnet[/][white]-[/][gold1]gitmoji[/][/]",
            PromptService.BuildBannerMarkup(Themes.Default));
    }

    [Theory]
    [InlineData("monokai")]
    [InlineData("catppuccin-latte")]
    [InlineData("catppuccin-frappe")]
    [InlineData("catppuccin-macchiato")]
    [InlineData("catppuccin-mocha")]
    public void NamedThemes_UseRgbForegroundColorsOnly(string name)
    {
        ThemePalette theme = Themes.Resolve(name);

        // RGB markup keeps named themes independent of the terminal's ANSI palette.
        Assert.All(AllMarkupValues(theme), markup => Assert.StartsWith("#", markup));
    }

    private static string[] AllMarkupValues(ThemePalette theme)
    {
        return
        [
            theme.BrandPrimaryMarkup,
            theme.BrandSecondaryMarkup,
            theme.BrandTertiaryMarkup,
            theme.SuccessMarkup,
            theme.WarningMarkup,
            theme.ErrorMarkup,
            theme.MutedMarkup,
            theme.EmphasisMarkup,
            theme.AccentMarkup,
            theme.SelectionMarkerMarkup
        ];
    }
}