using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using DotnetGitmoji.Theming;
using Spectre.Console;

namespace DotnetGitmoji.Commands;

[Command("list")]
public sealed partial class ListCommand : ICommand
{
    private readonly IGitmojiProvider _gitmojiProvider;
    private readonly IConfigurationService _configurationService;

    public ListCommand(IGitmojiProvider gitmojiProvider, IConfigurationService configurationService)
    {
        _gitmojiProvider = gitmojiProvider;
        _configurationService = configurationService;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        ToolConfiguration config = await _configurationService.LoadAsync();
        ThemePalette theme = Themes.Resolve(config.Theme);
        IReadOnlyList<Gitmoji> gitmojis = await _gitmojiProvider.GetAllAsync();

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

        foreach (Gitmoji g in gitmojis)
        {
            table.AddRow(new Text(g.Emoji), new Text(g.Code), new Text(g.Description),
                new Markup(GitmojiTableFormatting.FormatSemver(g.Semver, theme)));
        }

        AnsiConsole.Write(table);
    }
}
