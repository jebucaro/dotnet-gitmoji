using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using DotnetGitmoji.Services;
using Spectre.Console;

namespace DotnetGitmoji.Commands;

[Command("search")]
public sealed class SearchCommand : ICommand
{
    private readonly IGitmojiProvider _gitmojiProvider;

    public SearchCommand(IGitmojiProvider gitmojiProvider)
    {
        _gitmojiProvider = gitmojiProvider;
    }

    [CommandParameter(0, Name = "keyword",
        Description = "Search term to match against emoji name, code, or description")]
    public string Keyword { get; init; } = "";

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var results = await _gitmojiProvider.SearchAsync(Keyword);

        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine($"[grey]No gitmojis found matching '[white]{Keyword}[/]'.[/]");
            return;
        }

        var table = new Table()
            .AddColumn("Emoji")
            .AddColumn("Code")
            .AddColumn("Description");

        foreach (var g in results)
            table.AddRow(g.Emoji, g.Code, g.Description);

        AnsiConsole.MarkupLine($"[grey]Found {results.Count} gitmoji(s) matching '[white]{Keyword}[/]':[/]");
        AnsiConsole.Write(table);
    }
}