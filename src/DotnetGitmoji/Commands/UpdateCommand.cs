using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using DotnetGitmoji.Theming;
using Spectre.Console;

namespace DotnetGitmoji.Commands;

[Command("update")]
public sealed partial class UpdateCommand : ICommand
{
    private readonly IGitmojiProvider _gitmojiProvider;
    private readonly IConfigurationService _configurationService;

    public UpdateCommand(IGitmojiProvider gitmojiProvider, IConfigurationService configurationService)
    {
        _gitmojiProvider = gitmojiProvider;
        _configurationService = configurationService;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        ToolConfiguration config = await _configurationService.LoadAsync();
        ThemePalette theme = Themes.Resolve(config.Theme);

        await AnsiConsole.Status()
            .StartAsync("Fetching latest gitmojis...",
                async _ => { await _gitmojiProvider.ForceRefreshAsync(); });

        AnsiConsole.MarkupLine($"[{theme.SuccessMarkup}]✓[/] Gitmoji list updated successfully.");
    }
}