using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;

namespace DotnetGitmoji.Commands;

[Command("init")]
public sealed partial class InitCommand : ICommand
{
    private const string ShellModeValue = "shell";
    private const string TaskRunnerModeValue = "task-runner";

    private readonly IGitService _gitService;

    public InitCommand(IGitService gitService)
    {
        _gitService = gitService;
    }

    [CommandOption("mode", 'm',
        Description = "Husky.Net setup mode: shell or task-runner")]
    public string? Mode { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (await _gitService.IsHookInstalledAsync())
            throw new CommandException("The prepare-commit-msg hook is already installed.", 1);

        var huskySetupMode = ParseHuskySetupMode();
        var huskyKind = await _gitService.DetectHuskyKindAsync();

        switch (huskyKind)
        {
            case HuskyInstallKind.JsHusky:
                await console.Output.WriteLineAsync(
                    "JavaScript Husky detected (.husky/_/husky.sh).\n" +
                    "This command does not modify JavaScript Husky-managed hooks.\n" +
                    "No files were modified.\n\n" +
                    "Consider migrating to Husky.Net: https://alirezanet.github.io/Husky.Net/");
                return;

            case HuskyInstallKind.HuskyNetTaskRunner:
            case HuskyInstallKind.HuskyNetShell:
                if (huskySetupMode is null)
                    throw new CommandException(
                        "Husky.Net detected. Select setup mode with '--mode shell' or '--mode task-runner'.",
                        1);

                try
                {
                    if (huskySetupMode == HuskySetupMode.Shell)
                        await _gitService.InstallHuskyNetShellHookAsync();
                    else
                        await _gitService.InstallHuskyNetTaskRunnerHookAsync();
                }
                catch (InvalidOperationException exception)
                {
                    throw new CommandException(exception.Message, 1);
                }

                await console.Output.WriteLineAsync(
                    $"Husky.Net prepare-commit-msg hook configured using '{GetModeLabel(huskySetupMode.Value)}' mode.");
                return;
        }

        if (huskySetupMode is not null)
            throw new CommandException("The --mode option is only valid when Husky.Net is detected.", 1);

        await _gitService.InstallHookDirectAsync();
        await console.Output.WriteLineAsync("prepare-commit-msg hook installed successfully.");
    }

    private HuskySetupMode? ParseHuskySetupMode()
    {
        if (string.IsNullOrWhiteSpace(Mode))
            return null;

        var normalizedMode = Mode.Trim().ToLowerInvariant();
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
