using System.Text;
using DotnetGitmoji.Models;

namespace DotnetGitmoji.Services;

public sealed class GitmojiFuzzyMatcher : IGitmojiFuzzyMatcher
{
    private const double DescriptionWeight = 0.67;
    private const double NameWeight = 0.33;
    private const double GitmojiThreshold = 0.28;
    private const double ScopeThreshold = 0.30;

    public IReadOnlyList<Gitmoji> RankGitmojis(IReadOnlyList<Gitmoji> gitmojis, string? query)
    {
        ArgumentNullException.ThrowIfNull(gitmojis);

        if (string.IsNullOrWhiteSpace(query))
            return gitmojis;

        var normalizedQuery = Normalize(query);
        if (normalizedQuery.Length == 0)
            return gitmojis;

        return gitmojis
            .Select(gitmoji =>
            {
                var score = ScoreGitmoji(gitmoji, normalizedQuery);
                var strongMatch = IsStrongLiteralMatch(gitmoji.Name, normalizedQuery)
                                  || IsStrongLiteralMatch(gitmoji.Description, normalizedQuery)
                                  || IsStrongLiteralMatch(gitmoji.Code, normalizedQuery);
                return new RankedItem<Gitmoji>(gitmoji, score, strongMatch);
            })
            .Where(result => result.StrongMatch || result.Score >= GitmojiThreshold)
            .OrderByDescending(result => result.StrongMatch)
            .ThenByDescending(result => result.Score)
            .ThenBy(result => result.Item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(result => result.Item)
            .ToArray();
    }

    public IReadOnlyList<string> RankScopes(IReadOnlyList<string> scopes, string? query)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        if (string.IsNullOrWhiteSpace(query))
            return scopes;

        var normalizedQuery = Normalize(query);
        if (normalizedQuery.Length == 0)
            return scopes;

        return scopes
            .Select(scope =>
            {
                var score = ScoreField(scope, normalizedQuery);
                var strongMatch = IsStrongLiteralMatch(scope, normalizedQuery);
                return new RankedItem<string>(scope, score, strongMatch);
            })
            .Where(result => result.StrongMatch || result.Score >= ScopeThreshold)
            .OrderByDescending(result => result.StrongMatch)
            .ThenByDescending(result => result.Score)
            .ThenBy(result => result.Item, StringComparer.OrdinalIgnoreCase)
            .Select(result => result.Item)
            .ToArray();
    }

    private static double ScoreGitmoji(Gitmoji gitmoji, string normalizedQuery)
    {
        var descriptionScore = ScoreField(gitmoji.Description, normalizedQuery);
        var nameScore = ScoreField(gitmoji.Name, normalizedQuery);
        var codeScore = ScoreField(gitmoji.Code, normalizedQuery);

        var weightedScore = descriptionScore * DescriptionWeight + nameScore * NameWeight;

        var normalizedCode = Normalize(gitmoji.Code);
        if (normalizedCode.Equals(normalizedQuery, StringComparison.Ordinal))
            return 1.0;

        if (normalizedCode.StartsWith(normalizedQuery, StringComparison.Ordinal))
            weightedScore = Math.Max(weightedScore, 0.90);
        else if (normalizedCode.Contains(normalizedQuery, StringComparison.Ordinal))
            weightedScore = Math.Max(weightedScore, 0.82);
        else
            weightedScore = Math.Max(weightedScore, codeScore * 0.72);

        if (Normalize(gitmoji.Name).Equals(normalizedQuery, StringComparison.Ordinal))
            weightedScore = Math.Max(weightedScore, 0.96);

        return Math.Clamp(weightedScore, 0.0, 1.0);
    }

    private static double ScoreField(string value, string normalizedQuery)
    {
        var normalizedValue = Normalize(value);
        if (normalizedValue.Length == 0 || normalizedQuery.Length == 0)
            return 0.0;

        if (normalizedValue.Equals(normalizedQuery, StringComparison.Ordinal))
            return 1.0;

        if (normalizedValue.StartsWith(normalizedQuery, StringComparison.Ordinal))
            return 0.95;

        var containsIndex = normalizedValue.IndexOf(normalizedQuery, StringComparison.Ordinal);
        if (containsIndex >= 0)
        {
            var positionBonus = 1.0 - (double)containsIndex / Math.Max(1, normalizedValue.Length - 1);
            var lengthBonus = Math.Min(1.0, (double)normalizedQuery.Length / normalizedValue.Length);
            return Math.Clamp(0.80 + positionBonus * 0.12 + lengthBonus * 0.08, 0.0, 1.0);
        }

        var tokenScore = ScoreByTokens(normalizedValue, normalizedQuery);
        var subsequenceScore = ScoreBySubsequence(normalizedValue, normalizedQuery);
        return Math.Clamp(Math.Max(tokenScore, subsequenceScore), 0.0, 1.0);
    }

    private static double ScoreByTokens(string value, string query)
    {
        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return 0.0;

        var score = 0.0;
        foreach (var token in tokens)
        {
            var containsIndex = value.IndexOf(token, StringComparison.Ordinal);
            if (containsIndex >= 0)
            {
                var positionBonus = 1.0 - (double)containsIndex / Math.Max(1, value.Length - 1);
                score += 0.78 + positionBonus * 0.10;
                continue;
            }

            score += ScoreBySubsequence(value, token) * 0.85;
        }

        var average = score / tokens.Length;
        return Math.Clamp(average * 0.85, 0.0, 1.0);
    }

    private static double ScoreBySubsequence(string value, string query)
    {
        if (value.Length == 0 || query.Length == 0)
            return 0.0;

        var firstMatch = -1;
        var lastMatch = -1;
        var queryIndex = 0;
        var matches = 0;

        for (var valueIndex = 0; valueIndex < value.Length && queryIndex < query.Length; valueIndex++)
        {
            if (value[valueIndex] != query[queryIndex])
                continue;

            if (firstMatch < 0)
                firstMatch = valueIndex;

            lastMatch = valueIndex;
            matches++;
            queryIndex++;
        }

        if (matches == 0)
            return 0.0;

        var coverage = (double)matches / query.Length;
        if (coverage < 0.4)
            return 0.0;

        if (matches < query.Length)
            return coverage * 0.42;

        var span = Math.Max(1, lastMatch - firstMatch + 1);
        var compactness = (double)query.Length / span;
        var density = (double)query.Length / value.Length;
        return Math.Clamp(0.44 + compactness * 0.36 + density * 0.20, 0.0, 1.0);
    }

    private static bool IsStrongLiteralMatch(string value, string normalizedQuery)
    {
        var normalizedValue = Normalize(value);
        return normalizedValue.Equals(normalizedQuery, StringComparison.Ordinal)
               || normalizedValue.Contains(normalizedQuery, StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var source = value.Trim().ToLowerInvariant();
        var normalized = new StringBuilder(source.Length);
        var previousWasWhitespace = false;

        foreach (var character in source)
        {
            if (char.IsLetterOrDigit(character) || character is ':' or '_' or '-' or '/')
            {
                normalized.Append(character);
                previousWasWhitespace = false;
                continue;
            }

            if (char.IsWhiteSpace(character) && !previousWasWhitespace)
            {
                normalized.Append(' ');
                previousWasWhitespace = true;
            }
        }

        return normalized.ToString().Trim();
    }

    private readonly record struct RankedItem<T>(T Item, double Score, bool StrongMatch);
}