using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using Spectre.Console;

namespace DotnetGitmoji.Commands;

[Command("remove")]
public sealed partial class RemoveCommand : ICommand
{
    private readonly IGitService _gitService;

    public RemoveCommand(IGitService gitService)
    {
        _gitService = gitService;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var hookFile = await _gitService.FindHookFileAsync();

        if (hookFile is null)
            throw new CommandException("No dotnet-gitmoji hook found.", 1);

        if (!hookFile.Contains(Path.Combine(".git", "hooks")))
        {
            // Hook is managed outside .git/hooks — give mode-specific guidance.
            switch (await _gitService.DetectHuskyKindAsync())
            {
                case HuskyInstallKind.HuskyNetShell:
                case HuskyInstallKind.HuskyNetTaskRunner:
                    AnsiConsole.MarkupLine(
                        $"[yellow]Hook found in Husky.Net managed file:[/] [grey]{Markup.Escape(hookFile)}[/]\n" +
                        "To remove, run:\n" +
                        "  [white]dotnet husky remove prepare-commit-msg[/]\n\n" +
                        "If init was configured with [white]--mode task-runner[/], also remove the\n" +
                        "[white]dotnet-gitmoji[/] task from [grey].husky/task-runner.json[/].");
                    break;

                case HuskyInstallKind.JsHusky:
                    AnsiConsole.MarkupLine(
                        $"[yellow]Hook found in JavaScript Husky managed file:[/] [grey]{Markup.Escape(hookFile)}[/]\n" +
                        "Remove the [white]dotnet-gitmoji[/] line from this file manually.");
                    break;

                default:
                    AnsiConsole.MarkupLine(
                        $"[yellow]Hook found at:[/] [grey]{Markup.Escape(hookFile)}[/]\n" +
                        "Remove the [white]dotnet-gitmoji[/] line from this file manually.");
                    break;
            }

            return;
        }

        await _gitService.RemoveHookDirectAsync();
        AnsiConsole.MarkupLine("[green]✓[/] [grey]prepare-commit-msg[/] hook removed successfully.");
    }
}