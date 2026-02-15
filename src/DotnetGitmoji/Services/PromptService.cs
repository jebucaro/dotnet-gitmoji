using DotnetGitmoji.Models;
using Spectre.Console;

namespace DotnetGitmoji.Services;

public sealed class PromptService : IPromptService
{
    public bool IsInteractive => !Console.IsInputRedirected && Environment.UserInteractive;

    public Gitmoji SelectGitmoji(IReadOnlyList<Gitmoji> gitmojis)
    {
        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<Gitmoji>()
                .Title("Choose a gitmoji:")
                .PageSize(15)
                .MoreChoicesText("[grey]Scroll for more...[/]")
                .UseConverter(g => $"{g.Emoji} - {Markup.Escape(g.Description)}")
                .AddChoices(gitmojis));

        return selected;
    }

    public string? AskScope()
    {
        var scope = AnsiConsole.Prompt(
            new TextPrompt<string>("[grey]Enter scope (optional, press Enter to skip):[/]")
                .AllowEmpty());
        return string.IsNullOrWhiteSpace(scope) ? null : scope.Trim();
    }

    public string? AskMessage()
    {
        var message = AnsiConsole.Ask<string?>("[grey]Enter commit message:[/]");
        return string.IsNullOrWhiteSpace(message) ? null : message;
    }
}