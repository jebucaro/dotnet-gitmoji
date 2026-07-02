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

        Table table = new Table()
            .Border(TableBorder.Simple)
            .AddColumn("Emoji")
            .AddColumn("Code")
            .AddColumn("Description")
            .AddColumn("Semver");

        foreach (Gitmoji g in results)
        {
            table.AddRow(new Text(g.Emoji), new Text(g.Code), new Text(g.Description),
                new Markup(FormatSemver(g.Semver, theme)));
        }

        AnsiConsole.MarkupLine(
            $"[{theme.MutedMarkup}]Found {results.Count} gitmoji(s) matching '[{theme.EmphasisMarkup}]{escapedKeyword}[/]':[/]");
        AnsiConsole.Write(table);
    }

    private static string FormatSemver(string? semver, ThemePalette theme)
    {
        return semver is null ? string.Empty : $"[{theme.AccentMarkup}]{semver}[/]";
    }
}