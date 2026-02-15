using DotnetGitmoji.Models;

namespace DotnetGitmoji.Services;

public interface IPromptService
{
    /// <summary>
    /// Returns true if the terminal supports interactive prompts (stdin is a TTY).
    /// </summary>
    bool IsInteractive { get; }

    Gitmoji SelectGitmoji(IReadOnlyList<Gitmoji> gitmojis);
    string? AskScope();
    string? AskMessage();
}