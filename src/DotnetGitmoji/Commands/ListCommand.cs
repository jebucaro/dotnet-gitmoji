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
        Table table = new Table()
            .Border(TableBorder.Simple)
            .AddColumn("Emoji")
            .AddColumn("Code")
            .AddColumn("Description")
            .AddColumn("Semver");

        foreach (Gitmoji g in gitmojis)
        {
            table.AddRow(new Text(g.Emoji), new Text(g.Code), new Text(g.Description),
                new Markup(FormatSemver(g.Semver, theme)));
        }

        AnsiConsole.Write(table);
    }

    private static string FormatSemver(string? semver, ThemePalette theme)
    {
        return semver is null ? string.Empty : $"[{theme.AccentMarkup}]{semver}[/]";
    }
}