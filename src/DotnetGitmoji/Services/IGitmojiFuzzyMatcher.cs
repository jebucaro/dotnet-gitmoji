using DotnetGitmoji.Models;

namespace DotnetGitmoji.Services;

public interface IGitmojiFuzzyMatcher
{
    IReadOnlyList<Gitmoji> RankGitmojis(IReadOnlyList<Gitmoji> gitmojis, string? query);
    IReadOnlyList<string> RankScopes(IReadOnlyList<string> scopes, string? query);
}