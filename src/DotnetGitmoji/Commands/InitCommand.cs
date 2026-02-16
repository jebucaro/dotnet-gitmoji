using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using DotnetGitmoji.Services;

namespace DotnetGitmoji.Commands;

[Command("init")]
public sealed class InitCommand : ICommand
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

        if (await _gitService.IsHuskyInstalledAsync())
        {
            await console.Output.WriteLineAsync(
                "Husky.Net detected. To add the hook, run:\n" +
                "  dotnet husky add prepare-commit-msg -c \"dotnet dotnet-gitmoji \\\"$1\\\" \\\"$2\\\"\"");
            return;
        }

        await _gitService.InstallHookDirectAsync();
        await console.Output.WriteLineAsync("prepare-commit-msg hook installed successfully.");
    }
}