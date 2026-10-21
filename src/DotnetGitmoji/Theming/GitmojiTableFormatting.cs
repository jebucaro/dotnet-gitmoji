namespace DotnetGitmoji.Theming;

internal static class GitmojiTableFormatting
{
    // Fixed cell width for the emoji column so Code/Description/Semver line up the same
    // on every terminal. Spectre measures one glyph as 2 cells, this width keeps a comfortable
    // gutter and leaves room for the "Emoji" header so it never truncates. The
    // GitmojiTableFormattingTests guard asserts every shipped gitmoji fits this once normalized.
    internal const int EmojiColumnWidth = 5;

    // Makes an emoji render as a single predictable glyph in the table. Collapses a ZWJ sequence
    // (e.g. technologist) to its leading emoji so terminals that don't fuse the parts stop drawing
    // it double width, and drops the VS16 selector (U+FE0F) some terminals show as a stray cell.
    // Display only, callers keep the original emoji for the commit message.
    internal static string NormalizeEmojiForDisplay(string emoji)
    {
        if (string.IsNullOrEmpty(emoji))
        {
            return emoji;
        }

        int zwj = emoji.IndexOf('\u200D');
        string collapsed = zwj >= 0 ? emoji[..zwj] : emoji;
        return collapsed.Replace("\uFE0F", string.Empty);
    }

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