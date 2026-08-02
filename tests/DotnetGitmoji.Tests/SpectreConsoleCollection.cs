namespace DotnetGitmoji.Tests;

// AnsiConsole.Status()/Progress()/Prompt() share Spectre.Console's global exclusivity
// lock — only one Live-display operation may run at a time in the whole process. Test
// classes that exercise it (ConfigCommandTests, UpdateCommandTests) must share this
// collection so xUnit runs their tests sequentially relative to each other, instead of
// racing across classes (xUnit parallelizes across collections by default, but never
// within one).
[CollectionDefinition(Name)]
public sealed class SpectreConsoleCollection
{
    public const string Name = "Spectre.Console serial";
}