using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using DotnetGitmoji.Models;
using Spectre.Console;

namespace DotnetGitmoji.Services;

public sealed partial class PromptService : IPromptService
{
    public const int MaxScopeLength = 32;
    private const int GitmojiPageSize = 15;
    private const string ScopeNoneOption = "(none)";

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

    public Gitmoji SelectGitmoji(IReadOnlyList<Gitmoji> gitmojis, bool showSemverBadge = true)
    {
        ArgumentNullException.ThrowIfNull(gitmojis);
        if (gitmojis.Count == 0)
            throw new InvalidOperationException("Cannot show an empty gitmoji list.");

        var result = SelectWithFuzzySearch(
            gitmojis,
            "Choose a gitmoji:",
            "Type to fuzzy search gitmojis...",
            GitmojiPageSize,
            gitmoji => $"{Markup.Escape(gitmoji.Emoji)} {Markup.Escape(gitmoji.Code)}",
            (items, query) => _fuzzyMatcher.RankGitmojis(items, query),
            detailTitle: "[bold green]Description[/]",
            renderDetail: gitmoji =>
                $"{Markup.Escape(gitmoji.Description)}{FormatSemverBadge(gitmoji, showSemverBadge)}");

        Console.Clear();
        _console.MarkupLine(
            $"[green]✔[/] [bold]Gitmoji:[/] {Markup.Escape(result.Emoji)} [grey]{Markup.Escape(result.Description)}[/]{FormatSemverBadge(result, showSemverBadge)}");

        if (showSemverBadge && result.Semver is "patch" or "minor")
            _console.MarkupLine(
                "[yellow]⚠ For breaking changes, consider 💥 [bold](boom)[/] — it signals MAJOR semver impact.[/]");

        return result;
    }

    private static string FormatSemverBadge(Gitmoji gitmoji, bool showSemverBadge)
    {
        if (!showSemverBadge || gitmoji.Semver is null)
            return string.Empty;
        return $" [blue]({gitmoji.Semver})[/]";
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

            var scopesWithNone = scopes.Prepend(ScopeNoneOption).ToArray();
            var selected = SelectWithFuzzySearch(
                scopesWithNone,
                "Select scope:",
                "Type to fuzzy search scopes...",
                12,
                item => item == ScopeNoneOption ? "[grey](none)[/]" : Markup.Escape(item),
                (_, query) => _fuzzyMatcher.RankScopes(scopes, query)
                    .Prepend(ScopeNoneOption).ToList());

            return selected == ScopeNoneOption ? null : selected;
        }

        while (true)
        {
            var input = _console.Prompt(new TextPrompt<string>("[grey]Enter scope:[/]"));
            var scope = input.Trim();

            if (!ScopePattern().IsMatch(scope))
            {
                _console.MarkupLine(
                    "[yellow]Only alphanumeric, _ and - are allowed. Try again.[/]");
                continue;
            }

            if (scope.Length > MaxScopeLength)
            {
                scope = scope[..MaxScopeLength];
                _console.MarkupLine($"[yellow]Scope truncated to {MaxScopeLength} characters.[/]");
            }

            return scope;
        }
    }

    public string? AskTitle(ToolConfiguration config, string? defaultValue = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        var defaultTitle = defaultValue;
        while (true)
        {
            var prompt = new TextPrompt<string>("[grey]Enter commit title:[/]")
                .AllowEmpty();
            if (!string.IsNullOrEmpty(defaultTitle))
                prompt.DefaultValue(defaultTitle);

            var title = _console.Prompt(prompt);

            if (string.IsNullOrWhiteSpace(title))
                return null;

            var result = CommitTitlePolicy.ApplyPromptPolicy(title, config);
            if (result.WasTrimmed)
            {
                _console.MarkupLine(
                    $"[yellow]Warning: title truncated to {result.Title.Length} characters (nearest word boundary).[/]");
                return result.Title;
            }

            if (result.ExceededLimit && result.MaxLength is not null)
            {
                _console.MarkupLine(
                    $"[yellow]Warning: title exceeds configured maximum length of {result.MaxLength.Value} characters. Enter a shorter title or enable trimming.[/]");
                defaultTitle = null;
                continue;
            }

            return result.Title;
        }
    }

    public string? AskMessage()
    {
        var message = _console.Ask<string?>("[grey]Enter commit message:[/]");
        return string.IsNullOrWhiteSpace(message) ? null : message;
    }

    private T SelectWithFuzzySearch<T>(
        IReadOnlyList<T> items,
        string title,
        string searchPlaceholder,
        int pageSize,
        Func<T, string> renderItem,
        Func<IReadOnlyList<T>, string, IReadOnlyList<T>> rankItems,
        string? detailTitle = null,
        Func<T, string?>? renderDetail = null) where T : class
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

            RenderFuzzySelection(title, searchPlaceholder, query, rankedItems, selectedIndex, pageSize, renderItem, detailTitle, renderDetail);

            var keyAction = FuzzySelectorInputRouter.Route(Console.ReadKey(true));
            if (TryApplyKeyAction(keyAction, rankedItems, ref query, ref selectedIndex, out var result))
                return result;
        }
    }

    private static bool TryApplyKeyAction<T>(
        FuzzySelectorInputAction action,
        IReadOnlyList<T> rankedItems,
        ref string query,
        ref int selectedIndex,
        [NotNullWhen(true)] out T? result) where T : class
    {
        result = default;
        switch (action.Kind)
        {
            case FuzzySelectorInputActionKind.MoveUp:
                if (rankedItems.Count > 0)
                    selectedIndex = WrapDecrementIndex(selectedIndex, rankedItems.Count);
                break;

            case FuzzySelectorInputActionKind.MoveDown:
                if (rankedItems.Count > 0)
                    selectedIndex = WrapIncrementIndex(selectedIndex, rankedItems.Count);
                break;

            case FuzzySelectorInputActionKind.Submit:
                if (rankedItems.Count > 0)
                {
                    result = rankedItems[selectedIndex];
                    return true;
                }

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
                query += action.Character;
                selectedIndex = 0;
                break;
        }

        return false;
    }

    private void RenderFuzzySelection<T>(
        string title,
        string searchPlaceholder,
        string query,
        IReadOnlyList<T> rankedItems,
        int selectedIndex,
        int pageSize,
        Func<T, string> renderItem,
        string? detailTitle = null,
        Func<T, string?>? renderDetail = null)
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

        if (renderDetail != null)
        {
            var detail = renderDetail(rankedItems[selectedIndex]);
            if (detail is not null)
            {
                _console.WriteLine();
                _console.Write(
                    new Panel(new Markup(detail))
                        .Header(detailTitle ?? string.Empty)
                        .RoundedBorder()
                        .BorderColor(Color.Green)
                        .Expand());
            }
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

    private static int WrapDecrementIndex(int index, int count)
    {
        return index > 0 ? index - 1 : count - 1;
    }

    private static int WrapIncrementIndex(int index, int count)
    {
        return index < count - 1 ? index + 1 : 0;
    }
}