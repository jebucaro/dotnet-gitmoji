using System.Globalization;
using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using DotnetGitmoji.Theming;
using Spectre.Console;

namespace DotnetGitmoji.Commands;

[Command("config")]
public sealed partial class ConfigCommand : ICommand
{
    private readonly IConfigurationService _configurationService;
    private readonly IGitService _gitService;

    public ConfigCommand(IConfigurationService configurationService, IGitService gitService)
    {
        _configurationService = configurationService;
        _gitService = gitService;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        // The wizard saves to .gitmojirc.json at the repo root, so like init it is part of the
        // in-repo setup flow.
        try
        {
            await _gitService.GetRepositoryRootAsync();
        }
        catch (InvalidOperationException ex)
        {
            throw new CommandException(ex.Message, 1);
        }

        ToolConfiguration config = null!;
        try
        {
            await AnsiConsole.Status()
                .StartAsync("Loading configuration...",
                    async _ => { config = await _configurationService.LoadAsync(); });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CommandException($"Failed to load configuration: {ex.Message}", 1);
        }

        ThemePalette theme = Themes.Resolve(config.Theme);

        WriteSection(theme, "Message Format", "How commit subjects are structured");
        EmojiFormat emojiFormat = await AnsiConsole.PromptAsync(
            new SelectionPrompt<EmojiFormat>()
                .Title("Select emoji format:")
                .PageSize(5)
                .UseConverter(FormatEmojiChoice)
                .AddChoices(EmojiFormat.Emoji, EmojiFormat.Code));
        AnsiConsole.MarkupLine($"Emoji format: {Markup.Escape(FormatEmojiChoice(emojiFormat))}");
        bool normalizeCommitFormat = await AnsiConsole.ConfirmAsync(
            "Normalize commit format to 'emoji: title' (adds ': ' even without scope)?",
            config.NormalizeCommitFormat);
        bool capitalizeTitle = await AnsiConsole.ConfirmAsync("Capitalize commit title?", config.CapitalizeTitle);

        WriteSection(theme, "Scope", "Scope prompting and predefined scope list");
        bool scopePrompt = await AnsiConsole.ConfirmAsync("Prompt for scope?", config.ScopePrompt);
        string[]? scopes = await PromptScopesAsync(config);

        WriteSection(theme, "Body & Title", "Message body and title length constraints");
        bool messagePrompt = await AnsiConsole.ConfirmAsync("Prompt for commit message?", config.MessagePrompt);
        (int? maxTitleLength, bool trimTitleWhenExceeded) = await PromptMaxTitleLengthAsync(config, theme);

        WriteSection(theme, "Display", "Selector contents");
        bool showSemverBadge =
            await AnsiConsole.ConfirmAsync("Show semver badge in gitmoji selector?", config.ShowSemverBadge);

        WriteSection(theme, "Git Behavior", "Staging, signing, and convention enforcement");
        bool autoAdd =
            await AnsiConsole.ConfirmAsync("Auto-add changes before commit? (client mode only)", config.AutoAdd);
        bool signedCommit =
            await AnsiConsole.ConfirmAsync("Sign commits with GPG? (client mode only)", config.SignedCommit);
        bool enforceConvention = await AnsiConsole.ConfirmAsync(
            "Enforce gitmoji convention on all commits (including IDE/non-interactive)?",
            config.EnforceConvention);

        WriteSection(theme, "Advanced", "API endpoint for gitmoji data");
        string gitmojisUrl = await AnsiConsole.PromptAsync(
            new TextPrompt<string>("Gitmojis API URL:")
                .DefaultValue(config.GitmojisUrl)
                .Validate(url => ValidateGitmojisUrl(url, theme)));

        WriteSection(theme, "Theme",
            "Personal preference, saved to your global config, never to the team's .gitmojirc.json");
        string selectedTheme = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Select color theme:")
                .PageSize(Themes.Names.Count + 1)
                .AddChoices(Themes.Names));
        AnsiConsole.MarkupLine(
            $"Theme: {Markup.Escape(selectedTheme)} (personal setting, saved to the global config)");

        config.EmojiFormat = emojiFormat;
        config.NormalizeCommitFormat = normalizeCommitFormat;
        config.CapitalizeTitle = capitalizeTitle;
        config.ScopePrompt = scopePrompt;
        config.Scopes = scopes;
        config.MessagePrompt = messagePrompt;
        config.MaxTitleLength = maxTitleLength;
        config.TrimTitleWhenExceeded = trimTitleWhenExceeded;
        config.ShowSemverBadge = showSemverBadge;
        config.Theme = null; // personal setting — kept out of .gitmojirc.json, saved globally below
        config.AutoAdd = autoAdd;
        config.SignedCommit = signedCommit;
        config.EnforceConvention = enforceConvention;
        config.GitmojisUrl = gitmojisUrl;

        try
        {
            await AnsiConsole.Status()
                .StartAsync("Saving configuration...",
                    async _ =>
                    {
                        await _configurationService.SaveAsync(config);
                        await _configurationService.SaveThemePreferenceAsync(selectedTheme);
                    });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CommandException($"Failed to save configuration: {ex.Message}", 1);
        }

        AnsiConsole.MarkupLine(
            $"[{theme.SuccessMarkup}]✔[/] Configuration saved to [{theme.MutedMarkup}].gitmojirc.json[/]");
    }

    private static async Task<(int? MaxTitleLength, bool TrimTitleWhenExceeded)> PromptMaxTitleLengthAsync(
        ToolConfiguration config, ThemePalette theme)
    {
        string hint = config.MaxTitleLength is not null
            ? $"current: {config.MaxTitleLength.Value}, "
            : string.Empty;
        string input = await AnsiConsole.PromptAsync(
            new TextPrompt<string>($"Maximum commit title length ({hint}leave empty to disable):")
                .AllowEmpty()
                .Validate(value => ValidateMaxTitleLengthInput(value, theme)));

        int? maxTitleLength = string.IsNullOrWhiteSpace(input)
            ? null
            : int.Parse(input, NumberStyles.None, CultureInfo.InvariantCulture);

        bool trim = false;
        if (maxTitleLength is not null)
        {
            trim = await AnsiConsole.ConfirmAsync(
                "Trim titles that exceed the maximum length? (interactive prompts only)",
                config.TrimTitleWhenExceeded);
        }

        return (maxTitleLength, trim);
    }

    private static async Task<string[]?> PromptScopesAsync(ToolConfiguration config)
    {
        if (config.Scopes is not { Length: > 0 })
        {
            string input = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Predefined scopes (comma-separated, leave empty to skip):")
                    .AllowEmpty());
            return ParseScopes(input);
        }

        bool keep = await AnsiConsole.ConfirmAsync(
            $"Keep existing predefined scopes ({string.Join(", ", config.Scopes)})?", true);
        if (keep)
        {
            return config.Scopes;
        }

        string scopesInput = await AnsiConsole.PromptAsync(
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

    internal static ValidationResult ValidateMaxTitleLengthInput(string input, ThemePalette theme)
    {
        return string.IsNullOrWhiteSpace(input) ||
               (int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value > 0)
            ? ValidationResult.Success()
            : ValidationResult.Error($"[{theme.ErrorMarkup}]Must be a positive integer or empty[/]");
    }

    internal static ValidationResult ValidateGitmojisUrl(string url, ThemePalette theme)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps
            ? ValidationResult.Success()
            : ValidationResult.Error($"[{theme.ErrorMarkup}]Must be a valid HTTPS URL[/]");
    }

    private static void WriteSection(ThemePalette theme, string title, string description)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Panel(new Markup(Markup.Escape(description)))
                .Header($"[bold {theme.BorderMarkup}]{Markup.Escape(title)}[/]")
                .RoundedBorder()
                .BorderColor(theme.Border)
                .Expand());
    }
}