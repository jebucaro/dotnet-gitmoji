namespace DotnetGitmoji.Models;

public sealed class ToolConfiguration
{
    public EmojiFormat EmojiFormat { get; set; } = EmojiFormat.Unicode;
    public bool ScopePrompt { get; set; } = false;
    public bool MessagePrompt { get; set; } = false;
    public bool CapitalizeTitle { get; set; } = true;
    public string GitmojisUrl { get; set; } = "https://gitmoji.dev/api/gitmojis";
}
