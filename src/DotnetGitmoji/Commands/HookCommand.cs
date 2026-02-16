using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using DotnetGitmoji.Validators;

namespace DotnetGitmoji.Commands;

[Command]
public sealed class HookCommand : ICommand
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
    public string CommitMessageFile { get; init; } = "";

    [CommandParameter(1, Name = "commit-source", IsRequired = false,
        Description = "Source of the commit (message, template, merge, squash, commit)")]
    public string? CommitSource { get; init; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (CommitSource is "merge" or "squash")
            return;

        if (!File.Exists(CommitMessageFile))
            throw new CommandException($"Commit message file not found: {CommitMessageFile}");

        var message = await _commitMessageService.ReadMessageAsync(CommitMessageFile);

        var config = await _configService.LoadAsync();
        var gitmojis = await _gitmojiProvider.GetAllAsync();

        var result = _validator.Validate(message, gitmojis);

        if (result.IsValid)
            return;

        if (!_promptService.IsInteractive)
        {
            await console.Error.WriteLineAsync(
                "⚠ dotnet-gitmoji: no interactive terminal available, " +
                "keeping original commit message.");
            return;
        }

        var selectedGitmoji = _promptService.SelectGitmoji(gitmojis);

        var prefix = config.EmojiFormat == EmojiFormat.Unicode
            ? selectedGitmoji.Emoji
            : selectedGitmoji.Code;

        var scope = config.ScopePrompt
            ? _promptService.AskScope(config.Scopes)
            : null;

        var scopePart = string.IsNullOrWhiteSpace(scope) ? "" : $"({scope}): ";
        var title = config.CapitalizeTitle && !string.IsNullOrEmpty(message)
            ? char.ToUpper(message[0]) + message[1..]
            : message;
        var newMessage = $"{prefix} {scopePart}{title}";

        if (config.MessagePrompt && _promptService.IsInteractive)
        {
            var body = _promptService.AskMessage();
            if (!string.IsNullOrWhiteSpace(body))
                newMessage = $"{newMessage}\n\n{body}";
        }

        await _commitMessageService.WriteMessageAsync(CommitMessageFile, newMessage);
    }
}