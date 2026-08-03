namespace DotnetGitmoji.Theming;

internal static class GitmojiTableFormatting
{
    internal static string FormatSemver(string? semver, ThemePalette theme)
    {
        if (semver is null)
        {
            return string.Empty;
        }

        string colorMarkup = semver switch
        {
            "patch" => theme.SuccessMarkup,
            "minor" => theme.WarningMarkup,
            "major" => theme.ErrorMarkup,
            _ => theme.AccentMarkup
        };

        return $"[{colorMarkup}]{semver}[/]";
    }
}