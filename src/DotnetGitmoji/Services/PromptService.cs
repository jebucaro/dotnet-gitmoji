using System.Text.RegularExpressions;
using DotnetGitmoji.Models;
using Spectre.Console;

namespace DotnetGitmoji.Services;

public sealed partial class PromptService : IPromptService
{
    public const int MaxScopeLength = 32;
    public const int MaxTitleLength = 48;
    private const string NoneScopeOption = "(none)";
    private const int GitmojiPageSize = 15;

    [GeneratedRegex(@"^[a-zA-Z0-9_\-]+$")]
    private static partial Regex ScopePattern();

    private readonly IGitmojiFuzzyMatcher _fuzzyMatcher;
    private readonly IAnsiConsole _console;

    public PromptService(IGitmojiFuzzyMatcher fuzzyMatcher)
    {
        _fuzzyMatcher = fuzzyMatcher ?? throw new ArgumentNullException(nameof(fuzzyMatcher));
        _console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Interactive = InteractionSupport.Yes,
            Ansi = AnsiSupport.Yes
        });
    }

    public bool IsInteractive =>
        !Console.IsOutputRedirected &&
        Environment.UserInteractive &&
        !Console.IsInputRedirected;

    public Gitmoji SelectGitmoji(IReadOnlyList<Gitmoji> gitmojis)
    {
        ArgumentNullException.ThrowIfNull(gitmojis);
        if (gitmojis.Count == 0)
            throw new InvalidOperationException("Cannot show an empty gitmoji list.");

        return SelectWithFuzzySearch(
            gitmojis,
            "Choose a gitmoji:",
            "Type to fuzzy search gitmojis...",
            GitmojiPageSize,
            gitmoji =>
                $"{Markup.Escape(gitmoji.Emoji)} - {Markup.Escape(gitmoji.Description)} [grey]({Markup.Escape(gitmoji.Code)})[/]",
            (items, query) => _fuzzyMatcher.RankGitmojis(items, query));
    }

    public string? AskScope(IReadOnlyList<string>? predefinedScopes = null)
    {
        if (predefinedScopes is { Count: > 0 })
        {
            var scopes = predefinedScopes
                .Where(scope => !string.IsNullOrWhiteSpace(scope))
                .Select(scope => scope.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (scopes.Length == 0)
                return null;

            var selected = SelectWithFuzzySearch(
                BuildScopeOptions(scopes),
                "Select scope:",
                "Type to fuzzy search scopes...",
                12,
                scope => scope == NoneScopeOption ? "[grey](none)[/]" : Markup.Escape(scope),
                (_, query) => RankScopeOptions(scopes, query));

            return selected == NoneScopeOption ? null : selected;
        }

        var scope = _console.Prompt(
            new TextPrompt<string>("[grey]Enter scope (optional, press Enter to skip):[/]")
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(scope))
            return null;

        scope = scope.Trim();

        if (!ScopePattern().IsMatch(scope))
        {
            _console.MarkupLine(
                "[yellow]Warning: scope contains invalid characters (only alphanumeric, _ and - allowed). Scope ignored.[/]");
            return null;
        }

        if (scope.Length > MaxScopeLength)
        {
            scope = scope[..MaxScopeLength];
            _console.MarkupLine($"[yellow]Warning: scope truncated to {MaxScopeLength} characters.[/]");
        }

        return scope;
    }

    public string? AskTitle(string? defaultValue = null)
    {
        var prompt = new TextPrompt<string>("[grey]Enter commit title:[/]")
            .AllowEmpty();
        if (!string.IsNullOrEmpty(defaultValue))
            prompt.DefaultValue(defaultValue);

        var title = _console.Prompt(prompt);

        if (string.IsNullOrWhiteSpace(title))
            return null;

        if (title.Length > MaxTitleLength)
        {
            var lastSpace = title.LastIndexOf(' ', MaxTitleLength - 1);
            title = lastSpace > 0 ? title[..lastSpace] : title[..MaxTitleLength];
            _console.MarkupLine(
                $"[yellow]Warning: title truncated to {title.Length} characters (nearest word boundary).[/]");
        }

        return title;
    }

    public string? AskMessage()
    {
        var message = _console.Ask<string?>("[grey]Enter commit message:[/]");
        return string.IsNullOrWhiteSpace(message) ? null : message;
    }

    private static IReadOnlyList<string> BuildScopeOptions(IReadOnlyList<string> scopes)
    {
        var options = new List<string> { NoneScopeOption };
        options.AddRange(scopes);
        return options;
    }

    private IReadOnlyList<string> RankScopeOptions(IReadOnlyList<string> scopes, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BuildScopeOptions(scopes);

        var rankedScopes = _fuzzyMatcher.RankScopes(scopes, query).ToList();
        var trimmedQuery = query.Trim();
        if (trimmedQuery.Length > 0 &&
            (NoneScopeOption.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase) ||
             "none".Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase)))
            rankedScopes.Insert(0, NoneScopeOption);

        return rankedScopes;
    }

    private T SelectWithFuzzySearch<T>(
        IReadOnlyList<T> items,
        string title,
        string searchPlaceholder,
        int pageSize,
        Func<T, string> renderItem,
        Func<IReadOnlyList<T>, string, IReadOnlyList<T>> rankItems)
    {
        if (items.Count == 0)
            throw new InvalidOperationException("Cannot show an empty selection prompt.");

        var query = string.Empty;
        var selectedIndex = 0;

        while (true)
        {
            var rankedItems = string.IsNullOrWhiteSpace(query) ? items : rankItems(items, query);
            if (rankedItems.Count == 0)
                selectedIndex = 0;
            else if (selectedIndex >= rankedItems.Count)
                selectedIndex = rankedItems.Count - 1;

            RenderFuzzySelection(title, searchPlaceholder, query, rankedItems, selectedIndex, pageSize, renderItem);

            var keyAction = FuzzySelectorInputRouter.Route(Console.ReadKey(true));
            switch (keyAction.Kind)
            {
                case FuzzySelectorInputActionKind.MoveUp:
                    if (rankedItems.Count > 0)
                        selectedIndex = selectedIndex > 0 ? selectedIndex - 1 : rankedItems.Count - 1;
                    break;

                case FuzzySelectorInputActionKind.MoveDown:
                    if (rankedItems.Count > 0)
                        selectedIndex = selectedIndex < rankedItems.Count - 1 ? selectedIndex + 1 : 0;
                    break;

                case FuzzySelectorInputActionKind.Submit:
                    if (rankedItems.Count > 0)
                        return rankedItems[selectedIndex];
                    break;

                case FuzzySelectorInputActionKind.DeleteCharacter:
                    if (query.Length > 0)
                    {
                        query = query[..^1];
                        selectedIndex = 0;
                    }

                    break;

                case FuzzySelectorInputActionKind.ClearQuery:
                    if (query.Length > 0)
                    {
                        query = string.Empty;
                        selectedIndex = 0;
                    }

                    break;

                case FuzzySelectorInputActionKind.AppendCharacter:
                    query += keyAction.Character;
                    selectedIndex = 0;
                    break;
            }
        }
    }

    private void RenderFuzzySelection<T>(
        string title,
        string searchPlaceholder,
        string query,
        IReadOnlyList<T> rankedItems,
        int selectedIndex,
        int pageSize,
        Func<T, string> renderItem)
    {
        Console.Clear();
        _console.MarkupLine($"[bold]{Markup.Escape(title)}[/]");
        _console.MarkupLine("[grey]Type to fuzzy search. Use ↑/↓ to navigate, Enter to select, Esc to clear.[/]");

        var searchDisplay = string.IsNullOrWhiteSpace(query)
            ? $"[grey]{Markup.Escape(searchPlaceholder)}[/]"
            : $"[white]{Markup.Escape(query)}[/]";
        _console.MarkupLine($"[grey]Search:[/] {searchDisplay}");
        _console.MarkupLine(" ");

        if (rankedItems.Count == 0)
        {
            _console.MarkupLine("[yellow]No matches. Keep typing or press Backspace to refine.[/]");
            return;
        }

        var pageStart = CalculatePageStart(selectedIndex, rankedItems.Count, pageSize);
        var visibleItems = rankedItems.Skip(pageStart).Take(pageSize).ToArray();

        for (var index = 0; index < visibleItems.Length; index++)
        {
            var absoluteIndex = pageStart + index;
            var marker = absoluteIndex == selectedIndex ? "[green]❯[/]" : " ";
            _console.MarkupLine($"{marker} {renderItem(visibleItems[index])}");
        }

        if (rankedItems.Count > pageSize)
        {
            var first = pageStart + 1;
            var last = pageStart + visibleItems.Length;
            _console.MarkupLine($"[grey]Showing {first}-{last} of {rankedItems.Count} matches.[/]");
        }
    }

    private static int CalculatePageStart(int selectedIndex, int itemCount, int pageSize)
    {
        if (itemCount <= pageSize)
            return 0;

        var halfWindow = pageSize / 2;
        var start = Math.Max(0, selectedIndex - halfWindow);
        return Math.Min(start, Math.Max(0, itemCount - pageSize));
    }
}