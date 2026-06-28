using System.Text.RegularExpressions;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;

namespace DotnetGitmoji.Validators;

public sealed partial class GitmojiCommitMessageValidator : ICommitMessageValidator
{
    [GeneratedRegex(@"^(:[a-z0-9_]+:)\s*")]
    private static partial Regex ShortcodePattern();

    [GeneratedRegex(@"^\(([a-zA-Z0-9_\-]+)\):\s*")]
    private static partial Regex ScopePattern();

    public ValidationResult Validate(CommitMessageContent message, IReadOnlyList<Gitmoji> gitmojis)
    {
        string subject = message.Subject;

        Gitmoji? emojiMatch = gitmojis.FirstOrDefault(g => subject.StartsWith(g.Emoji, StringComparison.Ordinal));
        if (emojiMatch is not null)
        {
            return BuildResult(emojiMatch, subject[emojiMatch.Emoji.Length..].TrimStart(), message.Body);
        }

        Match shortcodeMatch = ShortcodePattern().Match(subject);
        if (shortcodeMatch.Success)
        {
            string code = shortcodeMatch.Groups[1].Value;
            Gitmoji? matched = gitmojis.FirstOrDefault(g => g.Code == code);
            if (matched is not null)
            {
                return BuildResult(matched, subject[shortcodeMatch.Length..], message.Body);
            }
        }

        return new ValidationResult(false, null, null, subject, message.Body);
    }

    private static ValidationResult BuildResult(Gitmoji gitmoji, string remainder, string? body)
    {
        Match scopeMatch = ScopePattern().Match(remainder);
        if (scopeMatch.Success)
        {
            string scope = scopeMatch.Groups[1].Value;
            string title = remainder[scopeMatch.Length..];
            return new ValidationResult(true, gitmoji, scope, title, body);
        }

        // Strip ": " separator used in the normalized commit format (e.g. "🐛: Fix bug")
        string titlePart = remainder.StartsWith(": ", StringComparison.Ordinal)
            ? remainder[2..]
            : remainder;
        return new ValidationResult(true, gitmoji, null, titlePart, body);
    }
}