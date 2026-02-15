using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using DotnetGitmoji.Services;

namespace DotnetGitmoji.Commands;

[Command("update")]
public sealed class UpdateCommand : ICommand
{
    private readonly IGitmojiProvider _gitmojiProvider;

    public UpdateCommand(IGitmojiProvider gitmojiProvider)
    {
        _gitmojiProvider = gitmojiProvider;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        await _gitmojiProvider.ForceRefreshAsync();
        await console.Output.WriteLineAsync("Gitmoji list updated successfully! ✅");
    }
}