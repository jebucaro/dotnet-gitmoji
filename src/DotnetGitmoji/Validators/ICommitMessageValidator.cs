using DotnetGitmoji.Models;
using DotnetGitmoji.Services;

namespace DotnetGitmoji.Validators;

public interface ICommitMessageValidator
{
    ValidationResult Validate(CommitMessageContent message, IReadOnlyList<Gitmoji> gitmojis);
}