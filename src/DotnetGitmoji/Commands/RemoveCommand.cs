using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;

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
                    await console.Output.WriteLineAsync(
                        $"Hook found in Husky.Net managed file: {hookFile}\n" +
                        "To remove, run:\n" +
                        "  dotnet husky remove prepare-commit-msg");
                    break;

                case HuskyInstallKind.JsHusky:
                    await console.Output.WriteLineAsync(
                        $"Hook found in JavaScript Husky managed file: {hookFile}\n" +
                        "Remove the dotnet-gitmoji line from this file manually.");
                    break;

                default:
                    await console.Output.WriteLineAsync(
                        $"Hook found at: {hookFile}\n" +
                        "Remove the dotnet-gitmoji line from this file manually.");
                    break;
            }

            return;
        }

        await _gitService.RemoveHookDirectAsync();
        await console.Output.WriteLineAsync("prepare-commit-msg hook removed successfully.");
    }
}