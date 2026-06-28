using System.Text.RegularExpressions;
using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;

namespace DotnetGitmoji.Commands;

[Command("commit")]
public sealed partial class CommitCommand : ICommand
{
    private const string NoStagedChangesMessage =
        "No staged changes found. Use 'git add' to stage changes or enable autoAdd in dotnet-gitmoji config.";

    private const string NoStagedChangesAfterAutoAddMessage =
        "No staged changes found after autoAdd. Ensure you have file changes to commit.";

    private readonly IGitmojiProvider _gitmojiProvider;
    private readonly IPromptService _promptService;
    private readonly IConfigurationService _configService;
    private readonly IGitService _gitService;

    public CommitCommand(
        IGitmojiProvider gitmojiProvider,
        IPromptService promptService,
        IConfigurationService configService,
        IGitService gitService)
    {
        _gitmojiProvider = gitmojiProvider;
        _promptService = promptService;
        _configService = configService;
        _gitService = gitService;
    }

    [GeneratedRegex(@"^[a-zA-Z0-9_\-]+$")]
    private static partial Regex ScopePattern();

    [CommandOption("title", 't', Description = "Commit title")]
    public string? Title { get; set; }

    [CommandOption("scope", 's', Description = "Commit scope")]
    public string? Scope { get; set; }

    [CommandOption("message", 'm', Description = "Commit message body")]
    public string? Message { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (await _gitService.IsHookInstalledAsync())
        {
            await console.Error.WriteLineAsync(
                "Error: The prepare-commit-msg hook is already configured to use dotnet-gitmoji.\n" +
                "Using both hook mode and client mode would apply the emoji twice.\n\n" +
                "Either:\n" +
                "  • Use 'git commit' and let the hook handle it (hook mode)\n" +
                "  • Remove the hook from .husky/prepare-commit-msg and use 'dotnet-gitmoji commit' (client mode)");
            throw new CommandException("Cannot use client mode while hook is installed.", 1);
        }

        if (!_promptService.IsInteractive)
        {
            throw new CommandException(
                "Cannot run in client mode without an interactive terminal.\n" +
                "Use 'git commit' with the hook instead, or ensure stdin is connected to a TTY.", 1);
        }

        ToolConfiguration config = await _configService.LoadAsync();
        IReadOnlyList<Gitmoji> gitmojis = await _gitmojiProvider.GetAllAsync();

        ValidateCommandOptions(config);
        await EnsureStagedChangesAsync(config);

        Gitmoji selected = _promptService.SelectGitmoji(gitmojis, config.ShowSemverBadge);
        string? scope = Scope ?? (config.ScopePrompt ? _promptService.AskScope(config.Scopes) : null);
        string? rawTitle = Title ?? _promptService.AskTitle(config);

        if (string.IsNullOrWhiteSpace(rawTitle))
        {
            throw new CommandException("A commit title is required.");
        }

        string? promptedTitleValidationError = CommitTitlePolicy.ValidateExplicitTitle(rawTitle, config);
        if (promptedTitleValidationError is not null)
        {
            throw new CommandException(promptedTitleValidationError);
        }

        string title = config.CapitalizeTitle
            ? char.ToUpper(rawTitle[0]) + rawTitle[1..]
            : rawTitle;

        string prefix = config.EmojiFormat == EmojiFormat.Emoji
            ? selected.Emoji
            : selected.Code;
        string commitMessage = BuildSubject(prefix, scope, title, config.NormalizeCommitFormat);

        string? body = Message;
        if (body is null && config.MessagePrompt)
        {
            body = _promptService.AskMessage();
        }

        string commitOutput;
        try
        {
            commitOutput = await _gitService.CommitAsync(commitMessage, body, config.SignedCommit);
        }
        catch (Exception ex)
        {
            throw new CommandException($"Failed to execute 'git commit': {ex.Message}", 1);
        }

        if (!string.IsNullOrWhiteSpace(commitOutput))
        {
            await console.Output.WriteAsync(commitOutput);
        }
    }

    private void ValidateCommandOptions(ToolConfiguration config)
    {
        if (Title is not null)
        {
            string? titleValidationError = CommitTitlePolicy.ValidateExplicitTitle(Title, config);
            if (titleValidationError is not null)
            {
                throw new CommandException(titleValidationError);
            }
        }

        if (Scope is not null)
        {
            if (!ScopePattern().IsMatch(Scope))
            {
                throw new CommandException(
                    "Scope contains invalid characters. Only alphanumeric, underscore and hyphen are allowed.");
            }

            if (Scope.Length > PromptService.MaxScopeLength)
            {
                throw new CommandException(
                    $"Scope exceeds maximum length of {PromptService.MaxScopeLength} characters.");
            }
        }
    }

    private async Task EnsureStagedChangesAsync(ToolConfiguration config)
    {
        if (config.AutoAdd)
        {
            try
            {
                await _gitService.StageAllAsync();
            }
            catch (Exception ex)
            {
                throw new CommandException($"Failed to auto-stage changes: {ex.Message}", 1);
            }
        }

        bool hasStagedChanges;
        try
        {
            hasStagedChanges = await _gitService.HasStagedChangesAsync();
        }
        catch (Exception ex)
        {
            throw new CommandException($"Failed to check staged changes: {ex.Message}", 1);
        }

        if (!hasStagedChanges)
        {
            throw new CommandException(
                config.AutoAdd ? NoStagedChangesAfterAutoAddMessage : NoStagedChangesMessage, 1);
        }
    }

    internal static string BuildSubject(string prefix, string? scope, string title, bool normalize)
    {
        if (normalize)
        {
            string scopePart = string.IsNullOrWhiteSpace(scope) ? ": " : $" ({scope}): ";
            return $"{prefix}{scopePart}{title}";
        }
        else
        {
            string scopePart = string.IsNullOrWhiteSpace(scope) ? "" : $"({scope}): ";
            return $"{prefix} {scopePart}{title}";
        }
    }
}