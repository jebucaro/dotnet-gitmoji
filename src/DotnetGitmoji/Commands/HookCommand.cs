using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using DotnetGitmoji.Validators;

namespace DotnetGitmoji.Commands;

[Command]
public sealed partial class HookCommand : ICommand
{
    private readonly ICommitMessageService _commitMessageService;
    private readonly ICommitMessageValidator _validator;
    private readonly IGitmojiProvider _gitmojiProvider;
    private readonly IPromptService _promptService;
    private readonly IConfigurationService _configService;

    public HookCommand(
        ICommitMessageService commitMessageService,
        ICommitMessageValidator validator,
        IGitmojiProvider gitmojiProvider,
        IPromptService promptService,
        IConfigurationService configService)
    {
        _commitMessageService = commitMessageService;
        _validator = validator;
        _gitmojiProvider = gitmojiProvider;
        _promptService = promptService;
        _configService = configService;
    }

    [CommandParameter(0, Name = "commit-msg-file", Description = "Path to the commit message file")]
    public required string CommitMessageFile { get; set; }

    [CommandParameter(1, Name = "commit-source",
        Description = "Source of the commit (message, template, merge, squash, commit)")]
    public string? CommitSource { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (ShouldSkipCommit(CommitSource, CommitMessageFile))
            return;

        if (!File.Exists(CommitMessageFile))
            throw new CommandException($"Commit message file not found: {CommitMessageFile}");

        CommitMessageContent commitMessage;
        try
        {
            commitMessage = await _commitMessageService.ReadMessageAsync(CommitMessageFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await console.Error.WriteLineAsync(
                $"⚠ dotnet-gitmoji: could not read commit message file, skipping. ({ex.Message})");
            return;
        }

        var config = await _configService.LoadAsync();
        var gitmojis = await _gitmojiProvider.GetAllAsync();

        var result = _validator.Validate(commitMessage, gitmojis);

        if (result.IsValid)
        {
            var missingScope = config.ScopePrompt && result.ParsedScope is null;
            var missingBody = config.MessagePrompt && result.ParsedBody is null;

            if (!missingScope && !missingBody)
                return;

            await HandleIncompleteMessageAsync(console, result, config, missingScope, missingBody);
            return;
        }

        await PrependGitmojiAsync(console, commitMessage.Subject, config, gitmojis);
    }

    // Skip during an interactive rebase. Git doesn't pass a source argument in
    // this case, but the rebase state directories are present inside .git/.
    private static bool ShouldSkipCommit(string? commitSource, string commitMessageFile)
    {
        if (commitSource is "merge" or "squash" or "commit")
            return true;

        var gitDir = Path.GetDirectoryName(Path.GetFullPath(commitMessageFile))!;
        return Directory.Exists(Path.Combine(gitDir, "rebase-merge")) ||
               Directory.Exists(Path.Combine(gitDir, "rebase-apply"));
    }

    private async Task HandleIncompleteMessageAsync(
        IConsole console,
        ValidationResult result,
        ToolConfiguration config,
        bool missingScope,
        bool missingBody)
    {
        if (!_promptService.IsInteractive)
        {
            if (config.EnforceConvention)
            {
                var missing = BuildMissingPartsList(missingScope, missingBody);
                throw new CommandException(
                    $"dotnet-gitmoji: commit rejected — {missing}.\n" +
                    "Add the required parts to your commit message or disable the corresponding prompt option.",
                    1);
            }

            await console.Error.WriteLineAsync(
                "⚠ dotnet-gitmoji: no interactive terminal available, keeping original commit message.");
            return;
        }

        var scope = result.ParsedScope
                    ?? (missingScope ? _promptService.AskScope(config.Scopes) : null);
        var body = result.ParsedBody
                   ?? (missingBody ? _promptService.AskMessage() : null);

        var prefix = config.EmojiFormat == EmojiFormat.Emoji
            ? result.MatchedGitmoji!.Emoji
            : result.MatchedGitmoji!.Code;

        var rawTitle = result.ParsedTitle ?? string.Empty;
        var title = config.CapitalizeTitle && rawTitle.Length > 0
            ? char.ToUpper(rawTitle[0]) + rawTitle[1..]
            : rawTitle;

        var scopePart = string.IsNullOrWhiteSpace(scope) ? "" : $"({scope}): ";
        var newSubject = $"{prefix} {scopePart}{title}";

        try
        {
            await _commitMessageService.WriteMessageAsync(CommitMessageFile, newSubject, body);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await console.Error.WriteLineAsync(
                $"⚠ dotnet-gitmoji: could not write commit message, keeping original. ({ex.Message})");
        }
    }

    private async Task PrependGitmojiAsync(
        IConsole console,
        string message,
        ToolConfiguration config,
        IReadOnlyList<Gitmoji> gitmojis)
    {
        if (!_promptService.IsInteractive)
        {
            if (config.EnforceConvention)
                throw new CommandException(
                    "dotnet-gitmoji: commit rejected — message does not follow the gitmoji convention.\n" +
                    "Start your commit title with a gitmoji emoji or shortcode (e.g. \":bug: Fix login crash\").",
                    1);

            await console.Error.WriteLineAsync(
                "⚠ dotnet-gitmoji: no interactive terminal available, " +
                "keeping original commit message.");
            return;
        }

        var selectedGitmoji = _promptService.SelectGitmoji(gitmojis);

        var prefix = config.EmojiFormat == EmojiFormat.Emoji
            ? selectedGitmoji.Emoji
            : selectedGitmoji.Code;

        var scope = config.ScopePrompt
            ? _promptService.AskScope(config.Scopes)
            : null;

        var scopePart = string.IsNullOrWhiteSpace(scope) ? "" : $"({scope}): ";
        var rawTitle = _promptService.AskTitle(config, message);

        if (string.IsNullOrWhiteSpace(rawTitle))
        {
            await console.Error.WriteLineAsync(
                "⚠ dotnet-gitmoji: empty title, keeping original commit message.");
            return;
        }

        var titleValidationError = CommitTitlePolicy.ValidateExplicitTitle(rawTitle, config);
        if (titleValidationError is not null)
        {
            await console.Error.WriteLineAsync(
                $"⚠ dotnet-gitmoji: {titleValidationError} Keeping original commit message.");
            return;
        }

        var title = config.CapitalizeTitle
            ? char.ToUpper(rawTitle[0]) + rawTitle[1..]
            : rawTitle;
        var newSubject = $"{prefix} {scopePart}{title}";

        var body = config.MessagePrompt ? _promptService.AskMessage() : null;

        try
        {
            await _commitMessageService.WriteMessageAsync(CommitMessageFile, newSubject, body);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await console.Error.WriteLineAsync(
                $"⚠ dotnet-gitmoji: could not write commit message, keeping original. ({ex.Message})");
        }
    }

    private static string BuildMissingPartsList(bool missingScope, bool missingBody)
    {
        if (missingScope && missingBody)
            return "scope and message body are required";
        if (missingScope)
            return "scope is required (scopePrompt is enabled)";
        return "message body is required (messagePrompt is enabled)";
    }
}