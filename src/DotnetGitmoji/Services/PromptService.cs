using System.Text.RegularExpressions;
using DotnetGitmoji.Models;
using Spectre.Console;

namespace DotnetGitmoji.Services;

public sealed partial class PromptService : IPromptService
{
    public const int MaxScopeLength = 32;
    public const int MaxTitleLength = 48;

    [GeneratedRegex(@"^[a-zA-Z0-9_\-]+$")]
    private static partial Regex ScopePattern();

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

        if (string.IsNullOrWhiteSpace(scope))
            return null;

        scope = scope.Trim();

        if (!ScopePattern().IsMatch(scope))
        {
            AnsiConsole.MarkupLine(
                "[yellow]Warning: scope contains invalid characters (only alphanumeric, _ and - allowed). Scope ignored.[/]");
            return null;
        }

        if (scope.Length > MaxScopeLength)
        {
            scope = scope[..MaxScopeLength];
            AnsiConsole.MarkupLine($"[yellow]Warning: scope truncated to {MaxScopeLength} characters.[/]");
        }

        return scope;
    }

    public string? AskTitle()
    {
        var title = AnsiConsole.Ask<string?>("[grey]Enter commit title:[/]");

        if (string.IsNullOrWhiteSpace(title))
            return null;

        if (title.Length > MaxTitleLength)
        {
            title = title[..MaxTitleLength];
            AnsiConsole.MarkupLine($"[yellow]Warning: title truncated to {MaxTitleLength} characters.[/]");
        }

        return title;
    }

    public string? AskMessage()
    {
        var message = AnsiConsole.Ask<string?>("[grey]Enter commit message:[/]");
        return string.IsNullOrWhiteSpace(message) ? null : message;
    }
}