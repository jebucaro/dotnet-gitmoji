using System.Text.RegularExpressions;
using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using CliWrap;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;

namespace DotnetGitmoji.Commands;

[Command("commit")]
public sealed class CommitCommand : ICommand
{
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

    [CommandOption("title", 't', Description = "Commit title")]
    public string? Title { get; init; }

    [CommandOption("scope", 's', Description = "Commit scope")]
    public string? Scope { get; init; }

    [CommandOption("message", 'm', Description = "Commit message body")]
    public string? Message { get; init; }

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
            throw new CommandException(
                "Cannot run in client mode without an interactive terminal.\n" +
                "Use 'git commit' with the hook instead, or ensure stdin is connected to a TTY.", 1);

        var config = await _configService.LoadAsync();
        var gitmojis = await _gitmojiProvider.GetAllAsync();

        if (Title is not null && Title.Length > PromptService.MaxTitleLength)
            throw new CommandException(
                $"Title exceeds maximum length of {PromptService.MaxTitleLength} characters.");

        if (Scope is not null)
        {
            if (!Regex.IsMatch(Scope, @"^[a-zA-Z0-9_\-]+$"))
                throw new CommandException(
                    "Scope contains invalid characters. Only alphanumeric, underscore and hyphen are allowed.");
            if (Scope.Length > PromptService.MaxScopeLength)
                throw new CommandException(
                    $"Scope exceeds maximum length of {PromptService.MaxScopeLength} characters.");
        }

        if (config.AutoAdd)
            await _gitService.StageAllAsync();

        var selected = _promptService.SelectGitmoji(gitmojis);
        var scope = Scope ?? (config.ScopePrompt ? _promptService.AskScope(config.Scopes) : null);
        var title = Title ?? (config.MessagePrompt ? _promptService.AskTitle() : null);

        if (string.IsNullOrWhiteSpace(title))
            throw new CommandException("A commit title is required.");

        var prefix = config.EmojiFormat == EmojiFormat.Unicode
            ? selected.Emoji
            : selected.Code;
        var scopePart = string.IsNullOrWhiteSpace(scope) ? "" : $"({scope}): ";
        var commitMessage = $"{prefix} {scopePart}{title}";

        var body = Message ?? (config.MessagePrompt ? _promptService.AskMessage() : null);

        var args = new List<string> { "commit", "-m", commitMessage };
        if (!string.IsNullOrWhiteSpace(body))
        {
            args.Add("-m");
            args.Add(body);
        }

        await Cli.Wrap("git")
            .WithArguments(args)
            .WithStandardOutputPipe(PipeTarget.ToStream(console.Output.BaseStream))
            .WithStandardErrorPipe(PipeTarget.ToStream(console.Error.BaseStream))
            .ExecuteAsync();
    }
}