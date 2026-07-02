using Spectre.Console;

namespace DotnetGitmoji.Theming;

/// <summary>
/// Registry of built-in themes. The default theme uses ANSI palette colors (indices 0-15 plus
/// gold1) so it follows the user's terminal color scheme; named themes use RGB values that
/// Spectre.Console downgrades to the detected color system on legacy terminals.
/// </summary>
public static class Themes
{
    public const string DefaultName = "default";

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
        Accent = Color.Blue,
        Border = Color.Green,
        SelectionMarker = Color.Green
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
        Color green = new(0xA6, 0xE2, 0x2E);
        Color pink = new(0xF9, 0x26, 0x72);

        return new ThemePalette
        {
            Name = "monokai",
            BrandPrimary = new Color(0xAE, 0x81, 0xFF),
            BrandSecondary = foreground,
            BrandTertiary = new Color(0xFD, 0x97, 0x1F),
            Success = green,
            Warning = new Color(0xE6, 0xDB, 0x74),
            Error = pink,
            Muted = new Color(0x75, 0x71, 0x5E),
            Emphasis = foreground,
            Accent = new Color(0x66, 0xD9, 0xEF),
            Border = green,
            SelectionMarker = pink
        };
    }

    private static ThemePalette CreateCatppuccinLatte()
    {
        Color mauve = new(0x88, 0x39, 0xEF);
        Color text = new(0x4C, 0x4F, 0x69);
        Color green = new(0x40, 0xA0, 0x2B);

        return new ThemePalette
        {
            Name = "catppuccin-latte",
            BrandPrimary = mauve,
            BrandSecondary = text,
            BrandTertiary = new Color(0xFE, 0x64, 0x0B),
            Success = green,
            Warning = new Color(0xDF, 0x8E, 0x1D),
            Error = new Color(0xD2, 0x0F, 0x39),
            Muted = new Color(0x8C, 0x8F, 0xA1),
            Emphasis = text,
            Accent = new Color(0x1E, 0x66, 0xF5),
            Border = green,
            SelectionMarker = mauve
        };
    }

    private static ThemePalette CreateCatppuccinFrappe()
    {
        Color mauve = new(0xCA, 0x9E, 0xE6);
        Color text = new(0xC6, 0xD0, 0xF5);
        Color green = new(0xA6, 0xD1, 0x89);

        return new ThemePalette
        {
            Name = "catppuccin-frappe",
            BrandPrimary = mauve,
            BrandSecondary = text,
            BrandTertiary = new Color(0xEF, 0x9F, 0x76),
            Success = green,
            Warning = new Color(0xE5, 0xC8, 0x90),
            Error = new Color(0xE7, 0x82, 0x84),
            Muted = new Color(0x83, 0x8B, 0xA7),
            Emphasis = text,
            Accent = new Color(0x8C, 0xAA, 0xEE),
            Border = green,
            SelectionMarker = mauve
        };
    }

    private static ThemePalette CreateCatppuccinMacchiato()
    {
        Color mauve = new(0xC6, 0xA0, 0xF6);
        Color text = new(0xCA, 0xD3, 0xF5);
        Color green = new(0xA6, 0xDA, 0x95);

        return new ThemePalette
        {
            Name = "catppuccin-macchiato",
            BrandPrimary = mauve,
            BrandSecondary = text,
            BrandTertiary = new Color(0xF5, 0xA9, 0x7F),
            Success = green,
            Warning = new Color(0xEE, 0xD4, 0x9F),
            Error = new Color(0xED, 0x87, 0x96),
            Muted = new Color(0x80, 0x87, 0xA2),
            Emphasis = text,
            Accent = new Color(0x8A, 0xAD, 0xF4),
            Border = green,
            SelectionMarker = mauve
        };
    }

    private static ThemePalette CreateCatppuccinMocha()
    {
        Color mauve = new(0xCB, 0xA6, 0xF7);
        Color text = new(0xCD, 0xD6, 0xF4);
        Color green = new(0xA6, 0xE3, 0xA1);

        return new ThemePalette
        {
            Name = "catppuccin-mocha",
            BrandPrimary = mauve,
            BrandSecondary = text,
            BrandTertiary = new Color(0xFA, 0xB3, 0x87),
            Success = green,
            Warning = new Color(0xF9, 0xE2, 0xAF),
            Error = new Color(0xF3, 0x8B, 0xA8),
            Muted = new Color(0x7F, 0x84, 0x9C),
            Emphasis = text,
            Accent = new Color(0x89, 0xB4, 0xFA),
            Border = green,
            SelectionMarker = mauve
        };
    }
}