using System.Text.Json.Serialization;

namespace DotnetGitmoji.Models;

public sealed class ToolConfiguration
{
    public const int DefaultMaxTitleLength = 48;

    public EmojiFormat EmojiFormat { get; set; } = EmojiFormat.Emoji;
    public bool ScopePrompt { get; set; } = false;
    public bool MessagePrompt { get; set; } = false;
    public bool CapitalizeTitle { get; set; } = true;
    public int? MaxTitleLength { get; set; } = DefaultMaxTitleLength;
    public bool TrimTitleWhenExceeded { get; set; } = true;
    public string GitmojisUrl { get; set; } = "https://gitmoji.dev/api/gitmojis";
    public bool AutoAdd { get; set; } = false;
    public bool SignedCommit { get; set; } = false;
    public string[]? Scopes { get; set; }
    public bool EnforceConvention { get; set; } = false;
    public bool ShowSemverBadge { get; set; } = true;
    public bool NormalizeCommitFormat { get; set; } = false;

    // Personal display preference, resolved from DOTNET_GITMOJI_THEME or the global config (whose
    // only content it is) — never from the shared repo config. Null means "no preference recorded".
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Theme { get; set; }
}