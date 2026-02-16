using System.Text.RegularExpressions;
using DotnetGitmoji.Models;

namespace DotnetGitmoji.Validators;

public sealed class GitmojiCommitMessageValidator : ICommitMessageValidator
{
    public ValidationResult Validate(string message, IReadOnlyList<Gitmoji> gitmojis)
    {
        foreach (var g in gitmojis)
            if (message.StartsWith(g.Emoji, StringComparison.Ordinal))
                return new ValidationResult(true, g, message[g.Emoji.Length..].TrimStart());

        var shortcodeMatch = Regex.Match(message, @"^(:[a-z_]+:)\s*");
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