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

    private readonly IAnsiConsole _console = AnsiConsole.Create(new AnsiConsoleSettings
    {
        Interactive = InteractionSupport.Yes,
        Ansi = AnsiSupport.Yes
    });

    public bool IsInteractive =>
        !Console.IsOutputRedirected &&
        Environment.UserInteractive &&
        !Console.IsInputRedirected;

    public Gitmoji SelectGitmoji(IReadOnlyList<Gitmoji> gitmojis)
    {
        return _console.Prompt(
            new SelectionPrompt<Gitmoji>()
                .Title("Choose a gitmoji:")
                .PageSize(15)
                .MoreChoicesText("[grey]Scroll for more...[/]")
                .UseConverter(g => $"{g.Emoji} - {Markup.Escape(g.Description)}")
                .AddChoices(gitmojis));
    }

    public string? AskScope(IReadOnlyList<string>? predefinedScopes = null)
    {
        if (predefinedScopes is { Count: > 0 })
        {
            var choices = new List<string> { "(none)" };
            choices.AddRange(predefinedScopes);

            var selected = _console.Prompt(
                new SelectionPrompt<string>()
                    .Title("[grey]Select scope:[/]")
                    .AddChoices(choices));

            return selected == "(none)" ? null : selected;
        }

        var scope = _console.Prompt(
            new TextPrompt<string>("[grey]Enter scope (optional, press Enter to skip):[/]")
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(scope))
            return null;

        scope = scope.Trim();

        if (!ScopePattern().IsMatch(scope))
        {
            _console.MarkupLine(
                "[yellow]Warning: scope contains invalid characters (only alphanumeric, _ and - allowed). Scope ignored.[/]");
            return null;
        }

        if (scope.Length > MaxScopeLength)
        {
            scope = scope[..MaxScopeLength];
            _console.MarkupLine($"[yellow]Warning: scope truncated to {MaxScopeLength} characters.[/]");
        }

        return scope;
    }

    public string? AskTitle(string? defaultValue = null)
    {
        var prompt = new TextPrompt<string>("[grey]Enter commit title:[/]")
            .AllowEmpty();
        if (!string.IsNullOrEmpty(defaultValue))
            prompt.DefaultValue(defaultValue);

        var title = _console.Prompt(prompt);

        if (string.IsNullOrWhiteSpace(title))
            return null;

        if (title.Length > MaxTitleLength)
        {
            var lastSpace = title.LastIndexOf(' ', MaxTitleLength - 1);
            title = lastSpace > 0 ? title[..lastSpace] : title[..MaxTitleLength];
            _console.MarkupLine(
                $"[yellow]Warning: title truncated to {title.Length} characters (nearest word boundary).[/]");
        }

        return title;
    }

    public string? AskMessage()
    {
        var message = _console.Ask<string?>("[grey]Enter commit message:[/]");
        return string.IsNullOrWhiteSpace(message) ? null : message;
    }
}