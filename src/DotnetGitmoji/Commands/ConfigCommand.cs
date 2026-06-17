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

        var emojiFormat = await AnsiConsole.PromptAsync(
            new SelectionPrompt<EmojiFormat>()
                .Title("Select emoji format:")
                .PageSize(5)
                .UseConverter(FormatEmojiChoice)
                .AddChoices(EmojiFormat.Emoji, EmojiFormat.Code));

        var scopePrompt = await AnsiConsole.ConfirmAsync("Prompt for scope?", config.ScopePrompt);
        var messagePrompt = await AnsiConsole.ConfirmAsync("Prompt for commit message?", config.MessagePrompt);
        var capitalizeTitle = await AnsiConsole.ConfirmAsync("Capitalize commit title?", config.CapitalizeTitle);

        var (maxTitleLength, trimTitleWhenExceeded) = await PromptMaxTitleLengthAsync(config);

        var autoAdd =
            await AnsiConsole.ConfirmAsync("Auto-add changes before commit? (client mode only)", config.AutoAdd);
        var signedCommit =
            await AnsiConsole.ConfirmAsync("Sign commits with GPG? (client mode only)", config.SignedCommit);
        var enforceConvention = await AnsiConsole.ConfirmAsync(
            "Enforce gitmoji convention on all commits (including IDE/non-interactive)?",
            config.EnforceConvention);

        var gitmojisUrl = await AnsiConsole.PromptAsync(
            new TextPrompt<string>("Gitmojis API URL:")
                .DefaultValue(config.GitmojisUrl)
                .Validate(ValidateGitmojisUrl));

        var scopes = await PromptScopesAsync(config);

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

        var target = DetermineTarget();
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

    internal ConfigSaveTarget DetermineTarget()
    {
        if (Global) return ConfigSaveTarget.Global;
        if (Local) return ConfigSaveTarget.Local;
        return ConfigSaveTarget.Auto;
    }

    private static async Task<(int? MaxTitleLength, bool TrimTitleWhenExceeded)> PromptMaxTitleLengthAsync(
        ToolConfiguration config)
    {
        var hint = config.MaxTitleLength is not null
            ? $"current: {config.MaxTitleLength.Value}, "
            : string.Empty;
        var input = await AnsiConsole.PromptAsync(
            new TextPrompt<string>($"Maximum commit title length ({hint}leave empty to disable):")
                .AllowEmpty()
                .Validate(ValidateMaxTitleLengthInput));

        int? maxTitleLength = string.IsNullOrWhiteSpace(input)
            ? null
            : int.Parse(input, NumberStyles.None, CultureInfo.InvariantCulture);

        var trim = config.TrimTitleWhenExceeded;
        if (maxTitleLength is not null)
            trim = await AnsiConsole.ConfirmAsync(
                "Trim titles that exceed the maximum length? (interactive prompts only)",
                config.TrimTitleWhenExceeded);

        return (maxTitleLength, trim);
    }

    private static async Task<string[]?> PromptScopesAsync(ToolConfiguration config)
    {
        if (config.Scopes is not { Length: > 0 })
        {
            var input = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Predefined scopes (comma-separated, leave empty to skip):")
                    .AllowEmpty());
            return ParseScopes(input);
        }

        var keep = await AnsiConsole.ConfirmAsync(
            $"Keep existing predefined scopes ({string.Join(", ", config.Scopes)})?", true);
        if (keep)
            return config.Scopes;

        var scopesInput = await AnsiConsole.PromptAsync(
            new TextPrompt<string>("Enter predefined scopes (comma-separated, leave empty to clear):")
                .AllowEmpty());
        return ParseScopes(scopesInput);
    }

    internal static string[]? ParseScopes(string input)
    {
        return string.IsNullOrWhiteSpace(input)
            ? null
            : input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    internal static string FormatEmojiChoice(EmojiFormat format)
    {
        return format == EmojiFormat.Emoji ? "Emoji (🐛)" : "Code (:\u200Bbug:)";
        // zero-width space breaks Spectre.Console :name: emoji pattern
    }

    internal static ValidationResult ValidateMaxTitleLengthInput(string input)
    {
        return string.IsNullOrWhiteSpace(input) ||
               (int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0)
            ? ValidationResult.Success()
            : ValidationResult.Error("[red]Must be a positive integer or empty[/]");
    }

    internal static ValidationResult ValidateGitmojisUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? ValidationResult.Success()
            : ValidationResult.Error("[red]Must be a valid HTTPS URL[/]");
    }
}