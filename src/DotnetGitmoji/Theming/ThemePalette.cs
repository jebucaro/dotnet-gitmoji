using Spectre.Console;

namespace DotnetGitmoji.Theming;

/// <summary>
/// Foreground-only color palette for the tool's terminal output. Palettes deliberately expose no
/// background roles so the user's terminal background and transparency are always respected.
/// </summary>
public sealed record ThemePalette
{
    public required string Name { get; init; }
    public required Color BrandPrimary { get; init; }
    public required Color BrandSecondary { get; init; }
    public required Color BrandTertiary { get; init; }
    public required Color Success { get; init; }
    public required Color Warning { get; init; }
    public required Color Error { get; init; }
    public required Color Muted { get; init; }
    public required Color Emphasis { get; init; }
    public required Color Accent { get; init; }
    public required Color Border { get; init; }
    public required Color SelectionMarker { get; init; }

    public string BrandPrimaryMarkup => BrandPrimary.ToMarkup();
    public string BrandSecondaryMarkup => BrandSecondary.ToMarkup();
    public string BrandTertiaryMarkup => BrandTertiary.ToMarkup();
    public string SuccessMarkup => Success.ToMarkup();
    public string WarningMarkup => Warning.ToMarkup();
    public string ErrorMarkup => Error.ToMarkup();
    public string MutedMarkup => Muted.ToMarkup();
    public string EmphasisMarkup => Emphasis.ToMarkup();
    public string AccentMarkup => Accent.ToMarkup();
    public string BorderMarkup => Border.ToMarkup();
    public string SelectionMarkerMarkup => SelectionMarker.ToMarkup();
}