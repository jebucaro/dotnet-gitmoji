namespace DotnetGitmoji.Models;

/// <summary>
/// Represents the response from the gitmoji API.
/// The API returns a JSON object with a "gitmojis" array containing all available gitmojis.
/// </summary>
/// <param name="Gitmojis">The collection of gitmoji entries from the API.</param>
public record GitmojiResponse(
    Gitmoji[] Gitmojis
);