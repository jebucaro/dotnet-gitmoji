using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using DotnetGitmoji.Theming;
using Spectre.Console;

namespace DotnetGitmoji.Commands;

[Command("remove")]
public sealed partial class RemoveCommand : ICommand
{
    private readonly IGitService _gitService;
    private readonly IConfigurationService _configurationService;

    public RemoveCommand(IGitService gitService, IConfigurationService configurationService)
    {
        _gitService = gitService;
        _configurationService = configurationService;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        try
        {
            ToolConfiguration config = await _configurationService.LoadAsync();
            ThemePalette theme = Themes.Resolve(config.Theme);
            string? hookFile = await _gitService.FindHookFileAsync();

            if (hookFile is null)
            {
                throw new CommandException("No dotnet-gitmoji hook found.", 1);
            }

            if (!hookFile.Contains(Path.Combine(".git", "hooks")))
            {
                // Hook is managed outside .git/hooks — give mode-specific guidance.
                switch (await _gitService.DetectHuskyKindAsync())
                {
                    case HuskyInstallKind.HuskyNetShell:
                    case HuskyInstallKind.HuskyNetTaskRunner:
                        AnsiConsole.MarkupLine(
                            $"[{theme.WarningMarkup}]Hook found in Husky.Net managed file:[/] [{theme.MutedMarkup}]{Markup.Escape(hookFile)}[/]\n" +
                            "To remove, run:\n" +
                            $"  [{theme.EmphasisMarkup}]dotnet husky remove prepare-commit-msg[/]\n\n" +
                            $"If init was configured with [{theme.EmphasisMarkup}]--mode task-runner[/], also remove the\n" +
                            $"[{theme.EmphasisMarkup}]dotnet-gitmoji[/] task from [{theme.MutedMarkup}].husky/task-runner.json[/].");
                        break;

                    case HuskyInstallKind.JsHusky:
                        AnsiConsole.MarkupLine(
                            $"[{theme.WarningMarkup}]Hook found in JavaScript Husky managed file:[/] [{theme.MutedMarkup}]{Markup.Escape(hookFile)}[/]\n" +
                            $"Remove the [{theme.EmphasisMarkup}]dotnet-gitmoji[/] line from this file manually.");
                        break;

                    default:
                        AnsiConsole.MarkupLine(
                            $"[{theme.WarningMarkup}]Hook found at:[/] [{theme.MutedMarkup}]{Markup.Escape(hookFile)}[/]\n" +
                            $"Remove the [{theme.EmphasisMarkup}]dotnet-gitmoji[/] line from this file manually.");
                        break;
                }

                return;
            }

            await _gitService.RemoveHookDirectAsync();
            AnsiConsole.MarkupLine(
                $"[{theme.SuccessMarkup}]✓[/] [{theme.MutedMarkup}]prepare-commit-msg[/] hook removed successfully.");
        }
        catch (InvalidOperationException ex)
        {
            throw new CommandException(ex.Message, 1);
        }
    }
}