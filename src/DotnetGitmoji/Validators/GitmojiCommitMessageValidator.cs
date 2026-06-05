using System.Text.RegularExpressions;
using DotnetGitmoji.Models;

namespace DotnetGitmoji.Validators;

public sealed partial class GitmojiCommitMessageValidator : ICommitMessageValidator
{
    [GeneratedRegex(@"^(:[a-z0-9_]+:)\s*")]
    private static partial Regex ShortcodePattern();

    public ValidationResult Validate(string message, IReadOnlyList<Gitmoji> gitmojis)
    {
        var emojiMatch = gitmojis.FirstOrDefault(g => message.StartsWith(g.Emoji, StringComparison.Ordinal));
        if (emojiMatch is not null)
            return new ValidationResult(true, emojiMatch, message[emojiMatch.Emoji.Length..].TrimStart());

        var shortcodeMatch = ShortcodePattern().Match(message);
        if (shortcodeMatch.Success)
        {
            var code = shortcodeMatch.Groups[1].Value;
            var matched = gitmojis.FirstOrDefault(g => g.Code == code);
            if (matched is not null)
                return new ValidationResult(true, matched, message[shortcodeMatch.Length..]);
        }

        return new ValidationResult(false, null, message);
    }
}