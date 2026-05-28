using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using DotnetGitmoji.Services;
using Spectre.Console;

namespace DotnetGitmoji.Commands;

[Command("list")]
public sealed partial class ListCommand : ICommand
{
    private readonly IGitmojiProvider _gitmojiProvider;

    public ListCommand(IGitmojiProvider gitmojiProvider)
    {
        _gitmojiProvider = gitmojiProvider;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var gitmojis = await _gitmojiProvider.GetAllAsync();
        var table = new Table()
            .Border(TableBorder.Simple)
            .AddColumn("Emoji")
            .AddColumn("Code")
            .AddColumn("Description");

        foreach (var g in gitmojis)
            table.AddRow(new Text(g.Emoji), new Text(g.Code), new Text(g.Description));

        AnsiConsole.Write(table);
    }
}