using DotnetGitmoji;

namespace DotnetGitmoji.Tests;

public class ProgramTests
{
    private static readonly string[] LongHelpArgs = { "--help" };
    private static readonly string[] ShortHelpArgs = { "-h" };
    private static readonly string[] SubcommandArgs = { "commit" };
    private static readonly string[] SubcommandHelpArgs = { "commit", "--help" };

    [Fact]
    public void IsRootHelpInvocation_WhenNoArgs_ReturnsTrue()
    {
        Assert.True(Program.IsRootHelpInvocation(Array.Empty<string>()));
    }

    [Fact]
    public void IsRootHelpInvocation_WhenLongHelpFlag_ReturnsTrue()
    {
        Assert.True(Program.IsRootHelpInvocation(LongHelpArgs));
    }

    [Fact]
    public void IsRootHelpInvocation_WhenShortHelpFlag_ReturnsTrue()
    {
        Assert.True(Program.IsRootHelpInvocation(ShortHelpArgs));
    }

    [Fact]
    public void IsRootHelpInvocation_WhenSubcommand_ReturnsFalse()
    {
        Assert.False(Program.IsRootHelpInvocation(SubcommandArgs));
    }

    [Fact]
    public void IsRootHelpInvocation_WhenSubcommandHelp_ReturnsFalse()
    {
        Assert.False(Program.IsRootHelpInvocation(SubcommandHelpArgs));
    }
}
