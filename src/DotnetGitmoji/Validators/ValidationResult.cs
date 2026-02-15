using DotnetGitmoji.Models;

namespace DotnetGitmoji.Validators;

public sealed record ValidationResult(
    bool IsValid,
    Gitmoji? MatchedGitmoji,
    string? RemainingMessage
);