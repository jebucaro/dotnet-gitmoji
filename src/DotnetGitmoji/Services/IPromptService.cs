using DotnetGitmoji.Models;

namespace DotnetGitmoji.Services;

public interface IPromptService
{
    /// <summary>
    /// Returns true if the terminal supports interactive prompts (stdin is a TTY).
    /// </summary>
    bool IsInteractive { get; }

    Gitmoji SelectGitmoji(IReadOnlyList<Gitmoji> gitmojis, bool showSemverBadge = true);
    string? AskScope(IReadOnlyList<string>? predefinedScopes = null);
    string? AskTitle(ToolConfiguration config, string? defaultValue = null);
    string? AskMessage();
}