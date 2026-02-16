using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using DotnetGitmoji.Services;

namespace DotnetGitmoji.Commands;

[Command("remove")]
public sealed class RemoveCommand : ICommand
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

        if (hookFile.Contains(Path.Combine(".husky", "prepare-commit-msg")))
        {
            await console.Output.WriteLineAsync(
                $"Hook found in Husky.Net managed file: {hookFile}\n" +
                "Remove the dotnet-gitmoji line from this file, or run:\n" +
                "  dotnet husky remove prepare-commit-msg");
            return;
        }

        await _gitService.RemoveHookDirectAsync();
        await console.Output.WriteLineAsync("prepare-commit-msg hook removed successfully.");
    }
}