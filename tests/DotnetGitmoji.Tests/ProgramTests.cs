using DotnetGitmoji;

namespace DotnetGitmoji.Tests;

public class ProgramTests
{
    [Fact]
    public void IsRootHelpInvocation_WhenNoArgs_ReturnsTrue()
    {
        Assert.True(Program.IsRootHelpInvocation(Array.Empty<string>()));
    }

    [Fact]
    public void IsRootHelpInvocation_WhenLongHelpFlag_ReturnsTrue()
    {
        Assert.True(Program.IsRootHelpInvocation(new[] { "--help" }));
    }

    [Fact]
    public void IsRootHelpInvocation_WhenShortHelpFlag_ReturnsTrue()
    {
        Assert.True(Program.IsRootHelpInvocation(new[] { "-h" }));
    }

    [Fact]
    public void IsRootHelpInvocation_WhenSubcommand_ReturnsFalse()
    {
        Assert.False(Program.IsRootHelpInvocation(new[] { "commit" }));
    }

    [Fact]
    public void IsRootHelpInvocation_WhenSubcommandHelp_ReturnsFalse()
    {
        Assert.False(Program.IsRootHelpInvocation(new[] { "commit", "--help" }));
    }
}
