using CliFx;
using DotnetGitmoji.Commands;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using DotnetGitmoji.Validators;
using Microsoft.Extensions.DependencyInjection;

namespace DotnetGitmoji;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Reopen stdin from terminal device before anything caches IsInputRedirected.
        // Harmless no-op when stdin is already a TTY (client mode).
        TtyConsoleInput.TryReopenStdin();

        var services = new ServiceCollection();

        // Services
        services.AddHttpClient();
        services.AddSingleton<ToolConfiguration>();
        services.AddSingleton<IGitmojiFuzzyMatcher, GitmojiFuzzyMatcher>();
        services.AddSingleton<IGitmojiProvider, GitmojiProvider>();
        services.AddSingleton<ICommitMessageService, CommitMessageService>();
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IPromptService, PromptService>();

        // Validators
        services.AddSingleton<ICommitMessageValidator, GitmojiCommitMessageValidator>();

        // Commands (transient — one per invocation)
        services.AddTransient<HookCommand>();
        services.AddTransient<CommitCommand>();
        services.AddTransient<ListCommand>();
        services.AddTransient<SearchCommand>();
        services.AddTransient<ConfigCommand>();
        services.AddTransient<UpdateCommand>();
        services.AddTransient<InitCommand>();
        services.AddTransient<RemoveCommand>();

        using var serviceProvider = services.BuildServiceProvider();

        var app = new CommandLineApplicationBuilder()
            .AddCommandsFromThisAssembly()
            .UseTypeInstantiator(type => serviceProvider.GetRequiredService(type))
            .Build();

        return await app.RunAsync(args);
    }
}