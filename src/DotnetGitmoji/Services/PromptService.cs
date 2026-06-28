using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using DotnetGitmoji.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace DotnetGitmoji.Services;

public sealed partial class PromptService : IPromptService
{
    public const int MaxScopeLength = 32;
    private const int GitmojiPageSize = 15;
    private const int MinPageSize = 3;
    private const int ChromeLines = 5; // title, instructions, search, blank, "showing" line
    private const int HeaderLines = 2; // banner + trailing blank
    private const int DetailLines = 6; // blank + panel borders + wrapped content (generous margin)
    private const string ScopeNoneOption = "(none)";
    private const string BlankLine = " ";

    // Begin/end synchronized output (CSI ?2026 h/l): terminals that support it present the repaint
    // atomically (no tearing); others ignore the sequence. Erase-to-end clears stale lines below.
    private const string BeginSyncUpdate = "\u001b[?2026h";
    private const string EndSyncUpdate = "\u001b[?2026l";
    private const string EraseToEndOfScreen = "\u001b[0J";
    private const string BannerTitle = "[bold][purple]dotnet[/][white]-[/][gold1]gitmoji[/][/]";

    private static readonly IRenderable _banner = new Markup(BannerTitle);

    [GeneratedRegex(@"^[a-zA-Z0-9_\-]+$")]
    private static partial Regex ScopePattern();

    private readonly IGitmojiFuzzyMatcher _fuzzyMatcher;
    private readonly IAnsiConsole _console;

    public PromptService(IGitmojiFuzzyMatcher fuzzyMatcher)
    {
        _fuzzyMatcher = fuzzyMatcher ?? throw new ArgumentNullException(nameof(fuzzyMatcher));
        _console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Interactive = InteractionSupport.Yes, Ansi = AnsiSupport.Yes
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
        {
            throw new InvalidOperationException("Cannot show an empty gitmoji list.");
        }

        Gitmoji result = SelectWithFuzzySearch(
            gitmojis,
            new FuzzyPickerOptions<Gitmoji>(
                "Choose a gitmoji:",
                "Type to fuzzy search gitmojis...",
                GitmojiPageSize,
                gitmoji => new Text($"{gitmoji.Emoji} {gitmoji.Code}"),
                "[bold green]Description[/]",
                gitmoji =>
                    $"{Markup.Escape(gitmoji.Description)}{FormatSemverBadge(gitmoji, showSemverBadge)}",
                _banner),
            (items, query) => _fuzzyMatcher.RankGitmojis(items, query));

        _console.MarkupLine(
            $"[green]✔[/] [bold]Gitmoji:[/] {Markup.Escape(result.Emoji)} [grey]{Markup.Escape(result.Description)}[/]{FormatSemverBadge(result, showSemverBadge)}");

        if (showSemverBadge && result.Semver is "patch" or "minor")
        {
            _console.MarkupLine(
                "[yellow]⚠ For breaking changes, consider 💥 [bold](boom)[/] — it signals MAJOR semver impact.[/]");
        }

        return result;
    }

    private static string FormatSemverBadge(Gitmoji gitmoji, bool showSemverBadge)
    {
        if (!showSemverBadge || gitmoji.Semver is null)
        {
            return string.Empty;
        }

        return $" [blue]({gitmoji.Semver})[/]";
    }

    public string? AskScope(IReadOnlyList<string>? predefinedScopes = null)
    {
        if (predefinedScopes is { Count: > 0 })
        {
            string[] scopes = predefinedScopes
                .Where(scope => !string.IsNullOrWhiteSpace(scope))
                .Select(scope => scope.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (scopes.Length == 0)
            {
                return null;
            }

            string[] scopesWithNone = scopes.Prepend(ScopeNoneOption).ToArray();
            string selected = SelectWithFuzzySearch(
                scopesWithNone,
                new FuzzyPickerOptions<string>(
                    "Select scope:",
                    "Type to fuzzy search scopes...",
                    12,
                    item => item == ScopeNoneOption
                        ? (IRenderable)new Markup("[grey](none)[/]")
                        : new Text(item)),
                (_, query) => _fuzzyMatcher.RankScopes(scopes, query)
                    .Prepend(ScopeNoneOption).ToList());

            return selected == ScopeNoneOption ? null : selected;
        }

        while (true)
        {
            string input = _console.Prompt(new TextPrompt<string>("[grey]Enter scope:[/]"));
            string scope = input.Trim();

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

        string? defaultTitle = defaultValue;
        while (true)
        {
            TextPrompt<string> prompt = new TextPrompt<string>("[grey]Enter commit title:[/]")
                .AllowEmpty();
            if (!string.IsNullOrEmpty(defaultTitle))
            {
                prompt.DefaultValue(defaultTitle);
            }

            string title = _console.Prompt(prompt);

            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            CommitTitlePromptResult result = CommitTitlePolicy.ApplyPromptPolicy(title, config);
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
        string? message = _console.Ask<string?>("[grey]Enter commit message:[/]");
        return string.IsNullOrWhiteSpace(message) ? null : message;
    }

    private T SelectWithFuzzySearch<T>(
        IReadOnlyList<T> items,
        FuzzyPickerOptions<T> options,
        Func<IReadOnlyList<T>, string, IReadOnlyList<T>> rankItems) where T : class
    {
        if (items.Count == 0)
        {
            throw new InvalidOperationException("Cannot show an empty selection prompt.");
        }

        // Terminals without an alternate buffer are effectively never hit interactively, but degrade
        // to a clear-and-redraw loop there (correct, may flicker) rather than risk anything fancier.
        if (!_console.Profile.Capabilities.AlternateBuffer)
        {
            return RunFuzzyLoop(items, options, rankItems, RenderFallbackFrame);
        }

        // Render on the alternate screen with absolute cursor positioning. This sidesteps
        // Spectre.Console's Live cursor math (which moves up by the tallest frame ever rendered and
        // desyncs/scrolls once our variable-height frame shrinks then grows), and keeps the user's
        // scrollback untouched — the original screen is restored on exit.
        T selected = null!;
        _console.AlternateScreen(() =>
        {
            _console.Cursor.Hide();
            try
            {
                selected = RunFuzzyLoop(items, options, rankItems, RenderPickerFrame);
            }
            finally
            {
                _console.Cursor.Show();
            }
        });

        return selected;
    }

    private static T RunFuzzyLoop<T>(
        IReadOnlyList<T> items,
        FuzzyPickerOptions<T> options,
        Func<IReadOnlyList<T>, string, IReadOnlyList<T>> rankItems,
        Action<IRenderable> render) where T : class
    {
        string query = string.Empty;
        int selectedIndex = 0;

        while (true)
        {
            IReadOnlyList<T> rankedItems = string.IsNullOrWhiteSpace(query) ? items : rankItems(items, query);
            if (rankedItems.Count == 0)
            {
                selectedIndex = 0;
            }
            else if (selectedIndex >= rankedItems.Count)
            {
                selectedIndex = rankedItems.Count - 1;
            }

            render(BuildFuzzySelectionFrame(options, query, rankedItems, selectedIndex));

            FuzzySelectorInputAction keyAction = FuzzySelectorInputRouter.Route(Console.ReadKey(true));
            if (TryApplyKeyAction(keyAction, rankedItems, ref query, ref selectedIndex, out T? result))
            {
                return result;
            }
        }
    }

    private void RenderPickerFrame(IRenderable frame)
    {
        // _console (default AnsiConsole) and Console.Out share the same stdout writer, so these raw
        // escapes interleave in order with Spectre's output. Synchronized output makes the repaint
        // atomic on terminals that support CSI ?2026; others ignore it harmlessly. Absolute home
        // positioning means there is no relative cursor math to desync, and erase-to-end clears any
        // tail left by a previous taller frame. The frame is padded to full width (see
        // BuildFuzzySelectionFrame) so shrinking lines are fully overwritten without a screen clear.
        Console.Out.Write(BeginSyncUpdate);
        _console.Cursor.SetPosition(1, 1);
        _console.Write(frame);
        Console.Out.Write(EraseToEndOfScreen);
        Console.Out.Write(EndSyncUpdate);
        Console.Out.Flush();
    }

    private void RenderFallbackFrame(IRenderable frame)
    {
        Console.Clear();
        _console.Write(frame);
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
                {
                    selectedIndex = WrapDecrementIndex(selectedIndex, rankedItems.Count);
                }

                break;

            case FuzzySelectorInputActionKind.MoveDown:
                if (rankedItems.Count > 0)
                {
                    selectedIndex = WrapIncrementIndex(selectedIndex, rankedItems.Count);
                }

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

    private static Grid BuildFuzzySelectionFrame<T>(
        FuzzyPickerOptions<T> options,
        string query,
        IReadOnlyList<T> rankedItems,
        int selectedIndex)
    {
        List<IRenderable> rows = new();

        if (options.Header is not null)
        {
            rows.Add(options.Header);
            rows.Add(new Markup(BlankLine));
        }

        rows.Add(new Markup($"[bold]{Markup.Escape(options.Title)}[/]"));
        rows.Add(new Markup("[grey]Type to fuzzy search. Use ↑/↓ to navigate, Enter to select, Esc to clear.[/]"));

        string searchDisplay = string.IsNullOrWhiteSpace(query)
            ? $"[grey]{Markup.Escape(options.SearchPlaceholder)}[/]"
            : $"[white]{Markup.Escape(query)}[/]";
        rows.Add(new Markup($"[grey]Search:[/] {searchDisplay}"));
        rows.Add(new Markup(BlankLine));

        if (rankedItems.Count == 0)
        {
            rows.Add(new Markup("[yellow]No matches. Keep typing or press Backspace to refine.[/]"));
            return WrapFullWidth(new Rows(rows));
        }

        int pageSize = ResolvePageSize(options);
        int pageStart = CalculatePageStart(selectedIndex, rankedItems.Count, pageSize);
        T[] visibleItems = rankedItems.Skip(pageStart).Take(pageSize).ToArray();

        rows.Add(BuildItemGrid(options, visibleItems, pageStart, selectedIndex));

        if (rankedItems.Count > pageSize)
        {
            int first = pageStart + 1;
            int last = pageStart + visibleItems.Length;
            rows.Add(new Markup($"[grey]Showing {first}-{last} of {rankedItems.Count} matches.[/]"));
        }

        AddDetailPanel(options, rankedItems[selectedIndex], rows);

        return WrapFullWidth(new Rows(rows));
    }

    // Pads every rendered line with trailing spaces to the full terminal width so a line that
    // shrinks between frames (e.g. the search query after Backspace) is fully overwritten in place,
    // no screen clear required. A single expanded grid column does the padding.
    private static Grid WrapFullWidth(IRenderable content)
    {
        Grid grid = new();
        grid.AddColumn(new GridColumn().Padding(0, 0, 0, 0));
        grid.Expand();
        grid.AddRow(content);
        return grid;
    }

    private static Grid BuildItemGrid<T>(
        FuzzyPickerOptions<T> options,
        T[] visibleItems,
        int pageStart,
        int selectedIndex)
    {
        Grid grid = new();
        grid.AddColumn(new GridColumn().NoWrap().Padding(0, 0, 0, 0));
        grid.AddColumn(new GridColumn().Padding(0, 0, 0, 0));

        for (int index = 0; index < visibleItems.Length; index++)
        {
            int absoluteIndex = pageStart + index;
            Markup marker = absoluteIndex == selectedIndex
                ? new Markup("[green]❯ [/]")
                : new Markup("  ");
            grid.AddRow(marker, options.RenderItem(visibleItems[index]));
        }

        return grid;
    }

    private static void AddDetailPanel<T>(FuzzyPickerOptions<T> options, T selectedItem, List<IRenderable> rows)
    {
        if (options.RenderDetail is null)
        {
            return;
        }

        string? detail = options.RenderDetail(selectedItem);
        if (detail is null)
        {
            return;
        }

        rows.Add(new Markup(BlankLine));
        rows.Add(
            new Panel(new Markup(detail))
                .Header(options.DetailTitle ?? string.Empty)
                .RoundedBorder()
                .BorderColor(Color.Green)
                .Expand());
    }

    private static int ResolvePageSize<T>(FuzzyPickerOptions<T> options)
    {
        int height;
        try
        {
            height = Console.WindowHeight;
        }
        catch (IOException)
        {
            return options.PageSize;
        }

        return CalculatePageSize(
            height,
            options.Header is not null,
            options.RenderDetail is not null,
            options.PageSize);
    }

    internal static int CalculatePageSize(int windowHeight, bool hasHeader, bool hasDetail, int maxPageSize)
    {
        if (windowHeight <= 0)
        {
            return maxPageSize;
        }

        int overhead = ChromeLines
                       + (hasHeader ? HeaderLines : 0)
                       + (hasDetail ? DetailLines : 0);
        int available = windowHeight - overhead;

        // When even the minimum list would overflow the window, prefer fitting (>= 1 row)
        // over the usability floor so the frame never scrolls and corrupts the Live render.
        if (available < MinPageSize)
        {
            return Math.Max(1, available);
        }

        return Math.Min(available, maxPageSize);
    }

    private static int CalculatePageStart(int selectedIndex, int itemCount, int pageSize)
    {
        if (itemCount <= pageSize)
        {
            return 0;
        }

        int halfWindow = pageSize / 2;
        int start = Math.Max(0, selectedIndex - halfWindow);
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

    private sealed record FuzzyPickerOptions<T>(
        string Title,
        string SearchPlaceholder,
        int PageSize,
        Func<T, IRenderable> RenderItem,
        string? DetailTitle = null,
        Func<T, string?>? RenderDetail = null,
        IRenderable? Header = null);
}