using CliFx;
using DotnetGitmoji.Commands;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using DotnetGitmoji.Theming;
using DotnetGitmoji.Validators;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace DotnetGitmoji;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Reopen stdin from terminal device before anything caches IsInputRedirected.
        // Harmless no-op when stdin is already a TTY (client mode).
        TtyConsoleInput.TryReopenStdin();

        ServiceCollection services = new();

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

        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        if (IsRootHelpInvocation(args))
        {
            try
            {
                IConfigurationService configService = serviceProvider.GetRequiredService<IConfigurationService>();
                ToolConfiguration bannerConfig = await configService.LoadAsync();
                ThemePalette bannerTheme = Themes.Resolve(bannerConfig.Theme);
                AnsiConsole.MarkupLine(PromptService.BuildBannerMarkup(bannerTheme));
                AnsiConsole.WriteLine();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Banner is decorative; if the global config can't be read, skip it
                // and let the command proceed normally (including --help itself).
            }
        }

        CommandLineApplication app = new CommandLineApplicationBuilder()
            .AddCommandsFromThisAssembly()
            .UseTypeInstantiator(type => serviceProvider.GetRequiredService(type))
            .Build();

        return await app.RunAsync(args);
    }

    internal static bool IsRootHelpInvocation(string[] args)
    {
        return args.Length == 0 || (args.Length == 1 && (args[0] == "--help" || args[0] == "-h"));
    }
}