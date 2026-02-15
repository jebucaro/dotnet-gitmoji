using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using DotnetGitmoji.Services;
using Spectre.Console;

namespace DotnetGitmoji.Commands;

[Command("list")]
public sealed class ListCommand : ICommand
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
            .AddColumn("Emoji")
            .AddColumn("Code")
            .AddColumn("Description");

        foreach (var g in gitmojis)
            table.AddRow(Markup.Escape(g.Emoji), Markup.Escape(g.Code), Markup.Escape(g.Description));

        AnsiConsole.Write(table);
    }
}