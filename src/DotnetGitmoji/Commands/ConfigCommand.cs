using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using Spectre.Console;

namespace DotnetGitmoji.Commands;

[Command("config")]
public sealed class ConfigCommand : ICommand
{
    private readonly IConfigurationService _configurationService;

    public ConfigCommand(IConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var config = await _configurationService.LoadAsync();

        var emojiFormat = AnsiConsole.Prompt(
            new SelectionPrompt<EmojiFormat>()
                .Title("Select emoji format:")
                .PageSize(5)
                .UseConverter(format => format == EmojiFormat.Unicode
                    ? "Unicode (🐛)"
                    : "Shortcode (:bug:)")
                .AddChoices(EmojiFormat.Unicode, EmojiFormat.Shortcode));

        var scopePrompt = AnsiConsole.Confirm("Prompt for scope?", config.ScopePrompt);
        var messagePrompt = AnsiConsole.Confirm("Prompt for commit message?", config.MessagePrompt);
        var capitalizeTitle = AnsiConsole.Confirm("Capitalize commit title?", config.CapitalizeTitle);

        config.EmojiFormat = emojiFormat;
        config.ScopePrompt = scopePrompt;
        config.MessagePrompt = messagePrompt;
        config.CapitalizeTitle = capitalizeTitle;

        await _configurationService.SaveAsync(config);
        await console.Output.WriteLineAsync("Configuration saved.");
    }
}