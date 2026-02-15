using DotnetGitmoji.Models;

namespace DotnetGitmoji.Validators;

public interface ICommitMessageValidator
{
    ValidationResult Validate(string message, IReadOnlyList<Gitmoji> gitmojis);
}