using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using Spectre.Console;

namespace DotnetGitmoji.Commands;

[Command("init")]
public sealed partial class InitCommand : ICommand
{
    private const string ShellModeValue = "shell";
    private const string TaskRunnerModeValue = "task-runner";

    private readonly IGitService _gitService;
    private readonly IConfigurationService _configurationService;

    public InitCommand(IGitService gitService, IConfigurationService configurationService)
    {
        _gitService = gitService;
        _configurationService = configurationService;
    }

    [CommandOption("mode", 'm',
        Description = "Husky.Net setup mode: shell or task-runner")]
    public string? Mode { get; set; }

    [CommandOption("config",
        Description = "Create .gitmojirc.json with defaults in the repo root")]
    public bool CreateConfig { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        try
        {
            if (await _gitService.IsHookInstalledAsync())
            {
                throw new CommandException("The prepare-commit-msg hook is already installed.", 1);
            }

            HuskySetupMode? huskySetupMode = ParseHuskySetupMode();
            HuskyInstallKind huskyKind = await _gitService.DetectHuskyKindAsync();

            await InstallHookAsync(huskyKind, huskySetupMode);

            if (CreateConfig)
            {
                string? createdPath = await _configurationService.CreateRepoConfigAsync();
                if (createdPath is null)
                {
                    AnsiConsole.MarkupLine("[yellow]![/] [grey].gitmojirc.json[/] already exists, skipping.");
                }
                else
                {
                    AnsiConsole.MarkupLine("[green]✓[/] [grey].gitmojirc.json[/] created with defaults.");
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            throw new CommandException(ex.Message, 1);
        }
    }

    private async Task InstallHookAsync(HuskyInstallKind huskyKind, HuskySetupMode? huskySetupMode)
    {
        if (huskyKind == HuskyInstallKind.JsHusky)
        {
            AnsiConsole.MarkupLine(
                "[yellow]JavaScript Husky detected ([grey].husky/_/husky.sh[/]).[/]\n" +
                "[yellow]This command does not modify JavaScript Husky-managed hooks.[/]\n" +
                "No files were modified.\n\n" +
                "Consider migrating to Husky.Net: [link]https://alirezanet.github.io/Husky.Net/[/]");
            return;
        }

        if (huskyKind is HuskyInstallKind.HuskyNetTaskRunner or HuskyInstallKind.HuskyNetShell)
        {
            if (huskySetupMode is null)
            {
                throw new CommandException(
                    "Husky.Net detected. Select setup mode with '--mode shell' or '--mode task-runner'.",
                    1);
            }

            if (huskySetupMode == HuskySetupMode.Shell)
            {
                await _gitService.InstallHuskyNetShellHookAsync();
            }
            else
            {
                await _gitService.InstallHuskyNetTaskRunnerHookAsync();
            }

            AnsiConsole.MarkupLine(
                $"[green]✓[/] Husky.Net [grey]prepare-commit-msg[/] hook configured " +
                $"using [white]{GetModeLabel(huskySetupMode.Value)}[/] mode.");
        }
        else
        {
            if (huskySetupMode is not null)
            {
                throw new CommandException("The --mode option is only valid when Husky.Net is detected.", 1);
            }

            await _gitService.InstallHookDirectAsync();
            AnsiConsole.MarkupLine("[green]✓[/] [grey]prepare-commit-msg[/] hook installed successfully.");
        }
    }

    private HuskySetupMode? ParseHuskySetupMode()
    {
        if (string.IsNullOrWhiteSpace(Mode))
        {
            return null;
        }

        string normalizedMode = Mode.Trim().ToLowerInvariant();
        return normalizedMode switch
        {
            ShellModeValue => HuskySetupMode.Shell,
            TaskRunnerModeValue => HuskySetupMode.TaskRunner,
            _ => throw new CommandException(
                $"Invalid value for --mode: '{Mode}'. Supported values are '{ShellModeValue}' and '{TaskRunnerModeValue}'.",
                1)
        };
    }

    private static string GetModeLabel(HuskySetupMode mode)
    {
        return mode == HuskySetupMode.Shell ? ShellModeValue : TaskRunnerModeValue;
    }

    private enum HuskySetupMode
    {
        Shell,
        TaskRunner
    }
}