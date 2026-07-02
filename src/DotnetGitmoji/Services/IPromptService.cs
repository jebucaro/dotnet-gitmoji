using DotnetGitmoji.Models;

namespace DotnetGitmoji.Services;

public interface IPromptService
{
    /// <summary>
    /// Returns true if the terminal supports interactive prompts (stdin is a TTY).
    /// </summary>
    bool IsInteractive { get; }

    Gitmoji SelectGitmoji(IReadOnlyList<Gitmoji> gitmojis, ToolConfiguration config);
    string? AskScope(ToolConfiguration config);
    string? AskTitle(ToolConfiguration config, string? defaultValue = null);
    string? AskMessage(ToolConfiguration config);
}