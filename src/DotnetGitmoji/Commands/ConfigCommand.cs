using System.Globalization;
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

    [CommandOption("global", 'g', Description = "Save to global config (~/.dotnet-gitmoji/config.json)")]
    public bool Global { get; set; }

    [CommandOption("local", 'l', Description = "Save to local repo config (.gitmojirc.json)")]
    public bool Local { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (Global && Local)
            throw new CommandException("Cannot specify both --global and --local.", 1);

        ToolConfiguration config;
        try
        {
            config = await _configurationService.LoadAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CommandException($"Failed to load configuration: {ex.Message}", 1);
        }

        var emojiFormat = AnsiConsole.Prompt(
            new SelectionPrompt<EmojiFormat>()
                .Title("Select emoji format:")
                .PageSize(5)
                .UseConverter(format => format == EmojiFormat.Emoji
                    ? "Emoji (🐛)"
                    : "Code (:​bug:)") // zero-width space breaks Spectre.Console :name: emoji pattern
                .AddChoices(EmojiFormat.Emoji, EmojiFormat.Code));

        var scopePrompt = AnsiConsole.Confirm("Prompt for scope?", config.ScopePrompt);
        var messagePrompt = AnsiConsole.Confirm("Prompt for commit message?", config.MessagePrompt);
        var capitalizeTitle = AnsiConsole.Confirm("Capitalize commit title?", config.CapitalizeTitle);

        var maxTitleLengthPrompt = new TextPrompt<string>("Maximum commit title length (leave empty to disable):")
            .AllowEmpty()
            .Validate(input =>
                string.IsNullOrWhiteSpace(input) ||
                (int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]Must be a positive integer or empty[/]"));
        if (config.MaxTitleLength is not null)
            maxTitleLengthPrompt.DefaultValue(config.MaxTitleLength.Value.ToString(CultureInfo.InvariantCulture));

        var maxTitleLengthInput = AnsiConsole.Prompt(maxTitleLengthPrompt);
        int? maxTitleLength = string.IsNullOrWhiteSpace(maxTitleLengthInput)
            ? null
            : int.Parse(maxTitleLengthInput, NumberStyles.None, CultureInfo.InvariantCulture);

        var trimTitleWhenExceeded = config.TrimTitleWhenExceeded;
        if (maxTitleLength is not null)
            trimTitleWhenExceeded = AnsiConsole.Confirm(
                "Trim titles that exceed the maximum length? (interactive prompts only)",
                config.TrimTitleWhenExceeded);

        var autoAdd = AnsiConsole.Confirm("Auto-add changes before commit? (client mode only)", config.AutoAdd);
        var signedCommit = AnsiConsole.Confirm("Sign commits with GPG? (client mode only)", config.SignedCommit);
        var enforceConvention = AnsiConsole.Confirm(
            "Enforce gitmoji convention on all commits (including IDE/non-interactive)?",
            config.EnforceConvention);

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
        config.MaxTitleLength = maxTitleLength;
        config.TrimTitleWhenExceeded = trimTitleWhenExceeded;
        config.AutoAdd = autoAdd;
        config.SignedCommit = signedCommit;
        config.EnforceConvention = enforceConvention;
        config.GitmojisUrl = gitmojisUrl;
        config.Scopes = scopes;

        var target = Global ? ConfigSaveTarget.Global : Local ? ConfigSaveTarget.Local : ConfigSaveTarget.Auto;
        try
        {
            await _configurationService.SaveAsync(config, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CommandException($"Failed to save configuration: {ex.Message}", 1);
        }

        await console.Output.WriteLineAsync("Configuration saved.");
    }
}