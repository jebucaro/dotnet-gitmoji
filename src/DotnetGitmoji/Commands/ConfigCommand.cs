using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using Spectre.Console;

namespace DotnetGitmoji.Commands;

[Command("config")]
public sealed partial class ConfigCommand : ICommand
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
                .UseConverter(format => format == EmojiFormat.Emoji
                    ? "Emoji (🐛)"
                    : "Code (:bug:)")
                .AddChoices(EmojiFormat.Emoji, EmojiFormat.Code));

        var scopePrompt = AnsiConsole.Confirm("Prompt for scope?", config.ScopePrompt);
        var messagePrompt = AnsiConsole.Confirm("Prompt for commit message?", config.MessagePrompt);
        var capitalizeTitle = AnsiConsole.Confirm("Capitalize commit title?", config.CapitalizeTitle);
        var autoAdd = AnsiConsole.Confirm("Auto-add changes before commit? (client mode only)", config.AutoAdd);
        var signedCommit = AnsiConsole.Confirm("Sign commits with GPG? (client mode only)", config.SignedCommit);

        var gitmojisUrl = AnsiConsole.Prompt(
            new TextPrompt<string>("Gitmojis API URL:")
                .DefaultValue(config.GitmojisUrl)
                .Validate(url =>
                    Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
                        ? ValidationResult.Success()
                        : ValidationResult.Error("[red]Must be a valid HTTPS URL[/]")));

        var scopesDefault = config.Scopes is { Length: > 0 }
            ? string.Join(", ", config.Scopes)
            : string.Empty;
        var scopesInput = AnsiConsole.Prompt(
            new TextPrompt<string>("Predefined scopes (comma-separated, leave empty to clear):")
                .AllowEmpty()
                .DefaultValue(scopesDefault));
        var scopes = string.IsNullOrWhiteSpace(scopesInput)
            ? null
            : scopesInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        config.EmojiFormat = emojiFormat;
        config.ScopePrompt = scopePrompt;
        config.MessagePrompt = messagePrompt;
        config.CapitalizeTitle = capitalizeTitle;
        config.AutoAdd = autoAdd;
        config.SignedCommit = signedCommit;
        config.GitmojisUrl = gitmojisUrl;
        config.Scopes = scopes;

        await _configurationService.SaveAsync(config);
        await console.Output.WriteLineAsync("Configuration saved.");
    }
}