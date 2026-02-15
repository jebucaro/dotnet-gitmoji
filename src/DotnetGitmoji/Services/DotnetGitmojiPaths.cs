namespace DotnetGitmoji.Services;

internal static class DotnetGitmojiPaths
{
    public static string UserDataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet-gitmoji");

    public static string GitmojiCachePath { get; } = Path.Combine(UserDataDirectory, "gitmojis.json");
    public static string GlobalConfigPath { get; } = Path.Combine(UserDataDirectory, "config.json");
}