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
        var services = new ServiceCollection();

        // Services
        services.AddHttpClient();
        services.AddSingleton<ToolConfiguration>();
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

        using var serviceProvider = services.BuildServiceProvider();

        var app = new CliApplicationBuilder()
            .AddCommandsFromThisAssembly()
            .UseTypeActivator(type => serviceProvider.GetRequiredService(type))
            .Build();

        return await app.RunAsync(args);
    }
}