using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using DotnetGitmoji.Theming;
using Spectre.Console;

namespace DotnetGitmoji.Commands;

[Command("search")]
public sealed partial class SearchCommand : ICommand
{
    private readonly IGitmojiProvider _gitmojiProvider;
    private readonly IConfigurationService _configurationService;

    public SearchCommand(IGitmojiProvider gitmojiProvider, IConfigurationService configurationService)
    {
        _gitmojiProvider = gitmojiProvider;
        _configurationService = configurationService;
    }

    [CommandParameter(0, Name = "keyword",
        Description = "Search term for fuzzy matching against gitmoji name, code, or description")]
    public string Keyword { get; set; } = "";

    public async ValueTask ExecuteAsync(IConsole console)
    {
        ToolConfiguration config = await _configurationService.LoadAsync();
        ThemePalette theme = Themes.Resolve(config.Theme);
        IReadOnlyList<Gitmoji> results = await _gitmojiProvider.SearchAsync(Keyword);

        string escapedKeyword = Markup.Escape(Keyword);

        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine(
                $"[{theme.MutedMarkup}]No gitmojis found matching '[{theme.EmphasisMarkup}]{escapedKeyword}[/]'.[/]");
            return;
        }

        // TableBorder.Simple has no vertical CellSeparator/HeaderSeparator characters.
        // Roughly a quarter of the embedded gitmoji set are multi-codepoint (VS16/ZWJ)
        // sequences that Spectre's width calculator measures differently than terminals
        // render them; a boxed border style (Rounded/Square/Minimal/...) would turn that
        // pre-existing misalignment into a visibly broken vertical line. Keep Simple.
        Table table = new Table()
            .Border(TableBorder.Simple)
            .BorderColor(theme.Border)
            .AddColumn(new TableColumn($"[bold {theme.BorderMarkup}]Emoji[/]"))
            .AddColumn(new TableColumn($"[bold {theme.BorderMarkup}]Code[/]"))
            .AddColumn(new TableColumn($"[bold {theme.BorderMarkup}]Description[/]"))
            .AddColumn(new TableColumn($"[bold {theme.BorderMarkup}]Semver[/]"));

        foreach (Gitmoji g in results)
        {
            table.AddRow(
                new Text(g.Emoji),
                new Markup(HighlightKeyword(g.Code, Keyword, theme)),
                new Markup(HighlightKeyword(g.Description, Keyword, theme)),
                new Markup(GitmojiTableFormatting.FormatSemver(g.Semver, theme)));
        }

        AnsiConsole.MarkupLine(
            $"[{theme.MutedMarkup}]Found {results.Count} gitmoji(s) matching '[{theme.EmphasisMarkup}]{escapedKeyword}[/]':[/]");
        AnsiConsole.Write(table);
    }

    internal static string HighlightKeyword(string value, string keyword, ThemePalette theme)
    {
        // Spectre.Console's Markup parser converts ":word:"-shaped text to an emoji glyph if
        // the word matches a known emoji shortcode name — gitmoji Code values are always
        // exactly that shape. Insert a zero-width space to break the pattern while staying
        // visually invisible, same technique as ConfigCommand.FormatEmojiChoice.
        string safeValue = NeutralizeEmojiShortcode(value);

        if (string.IsNullOrEmpty(keyword))
        {
            return Markup.Escape(safeValue);
        }

        int index = safeValue.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return Markup.Escape(safeValue);
        }

        string before = Markup.Escape(safeValue[..index]);
        string match = Markup.Escape(safeValue[index..(index + keyword.Length)]);
        string after = Markup.Escape(safeValue[(index + keyword.Length)..]);
        return $"{before}[{theme.EmphasisMarkup}]{match}[/]{after}";
    }

    private static string NeutralizeEmojiShortcode(string value)
    {
        if (value.Length >= 2 && value[0] == ':' && value[^1] == ':')
        {
            return $":​{value[1..]}";
        }

        return value;
    }
}
