namespace DotnetGitmoji.Models;

/// <summary>
/// Represents a gitmoji entry from the gitmoji API.
/// Gitmojis are emojis used to express the intent of a git commit.
/// </summary>
/// <param name="Emoji">The unicode emoji character (e.g., "🎨").</param>
/// <param name="Entity">The HTML entity representation (e.g., "&amp;#x1f3a8;").</param>
/// <param name="Code">The shortcode format (e.g., ":art:").</param>
/// <param name="Description">Description of what the gitmoji represents (e.g., "Improve structure / format of the code.").</param>
/// <param name="Name">The name identifier for the gitmoji (e.g., "art").</param>
/// <param name="Semver">Optional semantic versioning impact: "patch", "minor", "major", or null.</param>
public record Gitmoji(
    string Emoji,
    string Entity,
    string Code,
    string Description,
    string Name,
    string? Semver
);
