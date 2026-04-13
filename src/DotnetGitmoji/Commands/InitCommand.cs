using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;

namespace DotnetGitmoji.Commands;

[Command("init")]
public sealed partial class InitCommand : ICommand
{
    private readonly IGitService _gitService;

    public InitCommand(IGitService gitService)
    {
        _gitService = gitService;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (await _gitService.IsHookInstalledAsync())
            throw new CommandException("The prepare-commit-msg hook is already installed.", 1);

        switch (await _gitService.DetectHuskyKindAsync())
        {
            case HuskyInstallKind.JsHusky:
                await console.Output.WriteLineAsync(
                    "JavaScript Husky detected (.husky/_/husky.sh).\n" +
                    "This tool is designed for Husky.Net. To add the hook anyway, run:\n" +
                    "  dotnet husky add prepare-commit-msg -c \"dotnet-gitmoji \\\"$1\\\" \\\"$2\\\"\"\n\n" +
                    "Consider migrating to Husky.Net: https://alirezanet.github.io/Husky.Net/");
                return;

            case HuskyInstallKind.HuskyNetTaskRunner:
                await console.Output.WriteLineAsync(
                    "Husky.Net (task-runner) detected. Add the following entry to your task-runner.json:\n\n" +
                    "  {\n" +
                    "    \"name\": \"dotnet-gitmoji\",\n" +
                    "    \"command\": \"dotnet-gitmoji\",\n" +
                    "    \"args\": [\"${args[0]}\", \"${args[1]}\"],\n" +
                    "    \"pathMode\": \"task\"\n" +
                    "  }\n\n" +
                    "Then register the hook:\n" +
                    "  dotnet husky add prepare-commit-msg -c \"dotnet husky run --name dotnet-gitmoji -- \\\"$1\\\" \\\"$2\\\"\"");
                return;

            case HuskyInstallKind.HuskyNetShell:
                await console.Output.WriteLineAsync(
                    "Husky.Net detected. Choose a setup option:\n\n" +
                    "Option 1 — Shell-command hook (simpler):\n" +
                    "  dotnet husky add prepare-commit-msg -c \"dotnet-gitmoji \\\"$1\\\" \\\"$2\\\"\"\n\n" +
                    "Option 2 — Task-runner hook (if using task-runner.json):\n" +
                    "  Add a task entry in task-runner.json, then run:\n" +
                    "  dotnet husky add prepare-commit-msg -c \"dotnet husky run --name dotnet-gitmoji -- \\\"$1\\\" \\\"$2\\\"\"");
                return;
        }

        await _gitService.InstallHookDirectAsync();
        await console.Output.WriteLineAsync("prepare-commit-msg hook installed successfully.");
    }
}