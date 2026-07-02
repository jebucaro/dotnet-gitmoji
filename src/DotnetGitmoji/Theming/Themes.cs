using Spectre.Console;

namespace DotnetGitmoji.Theming;

/// <summary>
/// Registry of built-in themes. The default theme uses ANSI palette colors (indices 0-15 plus
/// the fixed golds) so it follows the user's terminal color scheme; named themes use RGB values
/// that Spectre.Console downgrades to the detected color system on legacy terminals.
/// </summary>
public static class Themes
{
    public const string DefaultName = "default";

    // Brand identity: purple structures the frame (borders, headers, badges), gold spotlights the
    // single selected item, semantic colors (green/yellow/red) keep their conventional meaning.
    public static ThemePalette Default { get; } = new()
    {
        Name = DefaultName,
        BrandPrimary = Color.Purple,
        BrandSecondary = Color.White,
        BrandTertiary = Color.Gold1,
        Success = Color.Green,
        Warning = Color.Yellow,
        Error = Color.Red,
        Muted = Color.Grey,
        Emphasis = Color.White,
        Accent = Color.Purple,
        Border = Color.Purple,
        SelectionMarker = Color.Gold3_1
    };

    public static ThemePalette Monokai { get; } = CreateMonokai();
    public static ThemePalette CatppuccinLatte { get; } = CreateCatppuccinLatte();
    public static ThemePalette CatppuccinFrappe { get; } = CreateCatppuccinFrappe();
    public static ThemePalette CatppuccinMacchiato { get; } = CreateCatppuccinMacchiato();
    public static ThemePalette CatppuccinMocha { get; } = CreateCatppuccinMocha();

    private static readonly ThemePalette[] _all =
    [
        Default, Monokai, CatppuccinLatte, CatppuccinFrappe, CatppuccinMacchiato, CatppuccinMocha
    ];

    private static readonly Dictionary<string, ThemePalette> _registry =
        _all.ToDictionary(theme => theme.Name, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Names { get; } = Array.ConvertAll(_all, theme => theme.Name);

    public static bool IsKnown(string? name)
    {
        return name is not null && _registry.ContainsKey(name);
    }

    public static ThemePalette Resolve(string? name)
    {
        if (name is not null && _registry.TryGetValue(name, out ThemePalette? palette))
        {
            return palette;
        }

        return Default;
    }

    private static ThemePalette CreateMonokai()
    {
        Color foreground = new(0xF8, 0xF8, 0xF2);
        Color pink = new(0xF9, 0x26, 0x72);
        Color orange = new(0xFD, 0x97, 0x1F);

        return new ThemePalette
        {
            Name = "monokai",
            BrandPrimary = new Color(0xAE, 0x81, 0xFF),
            BrandSecondary = foreground,
            BrandTertiary = orange,
            Success = new Color(0xA6, 0xE2, 0x2E),
            Warning = new Color(0xE6, 0xDB, 0x74),
            Error = pink,
            Muted = new Color(0x75, 0x71, 0x5E), // comment gray
            Emphasis = foreground,
            Accent = new Color(0x66, 0xD9, 0xEF),
            Border = orange,
            SelectionMarker = pink
        };
    }

    private static ThemePalette CreateCatppuccinLatte()
    {
        Color mauve = new(0x88, 0x39, 0xEF);
        Color text = new(0x4C, 0x4F, 0x69);

        return new ThemePalette
        {
            Name = "catppuccin-latte",
            BrandPrimary = mauve,
            BrandSecondary = text,
            BrandTertiary = new Color(0xFE, 0x64, 0x0B),
            Success = new Color(0x40, 0xA0, 0x2B),
            Warning = new Color(0xDF, 0x8E, 0x1D),
            Error = new Color(0xD2, 0x0F, 0x39),
            Muted = new Color(0x6C, 0x6F, 0x85), // subtext0
            Emphasis = text,
            Accent = new Color(0x1E, 0x66, 0xF5),
            Border = new Color(0x72, 0x87, 0xFD), // lavender
            SelectionMarker = mauve
        };
    }

    private static ThemePalette CreateCatppuccinFrappe()
    {
        Color mauve = new(0xCA, 0x9E, 0xE6);
        Color text = new(0xC6, 0xD0, 0xF5);

        return new ThemePalette
        {
            Name = "catppuccin-frappe",
            BrandPrimary = mauve,
            BrandSecondary = text,
            BrandTertiary = new Color(0xEF, 0x9F, 0x76),
            Success = new Color(0xA6, 0xD1, 0x89),
            Warning = new Color(0xE5, 0xC8, 0x90),
            Error = new Color(0xE7, 0x82, 0x84),
            Muted = new Color(0xA5, 0xAD, 0xCE), // subtext0
            Emphasis = text,
            Accent = new Color(0x8C, 0xAA, 0xEE),
            Border = new Color(0xBA, 0xBB, 0xF1), // lavender
            SelectionMarker = mauve
        };
    }

    private static ThemePalette CreateCatppuccinMacchiato()
    {
        Color mauve = new(0xC6, 0xA0, 0xF6);
        Color text = new(0xCA, 0xD3, 0xF5);

        return new ThemePalette
        {
            Name = "catppuccin-macchiato",
            BrandPrimary = mauve,
            BrandSecondary = text,
            BrandTertiary = new Color(0xF5, 0xA9, 0x7F),
            Success = new Color(0xA6, 0xDA, 0x95),
            Warning = new Color(0xEE, 0xD4, 0x9F),
            Error = new Color(0xED, 0x87, 0x96),
            Muted = new Color(0xA5, 0xAD, 0xCB), // subtext0
            Emphasis = text,
            Accent = new Color(0x8A, 0xAD, 0xF4),
            Border = new Color(0xB7, 0xBD, 0xF8), // lavender
            SelectionMarker = mauve
        };
    }

    private static ThemePalette CreateCatppuccinMocha()
    {
        Color mauve = new(0xCB, 0xA6, 0xF7);
        Color text = new(0xCD, 0xD6, 0xF4);

        return new ThemePalette
        {
            Name = "catppuccin-mocha",
            BrandPrimary = mauve,
            BrandSecondary = text,
            BrandTertiary = new Color(0xFA, 0xB3, 0x87),
            Success = new Color(0xA6, 0xE3, 0xA1),
            Warning = new Color(0xF9, 0xE2, 0xAF),
            Error = new Color(0xF3, 0x8B, 0xA8),
            Muted = new Color(0xA6, 0xAD, 0xC8), // subtext0
            Emphasis = text,
            Accent = new Color(0x89, 0xB4, 0xFA),
            Border = new Color(0xB4, 0xBE, 0xFE), // lavender
            SelectionMarker = mauve
        };
    }
}