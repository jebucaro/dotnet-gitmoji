using DotnetGitmoji.Models;
using DotnetGitmoji.Services;

namespace DotnetGitmoji.Tests;

public class CommitTitlePolicyTests
{
    [Fact]
    public void ValidateExplicitTitle_WhenLimitDisabled_ReturnsNull()
    {
        ToolConfiguration config = new() { MaxTitleLength = null };

        string? error = CommitTitlePolicy.ValidateExplicitTitle(new string('a', 200), config);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateExplicitTitle_WhenLengthExceedsConfiguredMax_ReturnsError()
    {
        ToolConfiguration config = new() { MaxTitleLength = 10 };

        string? error = CommitTitlePolicy.ValidateExplicitTitle("This title is too long", config);

        Assert.NotNull(error);
        Assert.Contains("10", error);
    }

    [Fact]
    public void ApplyPromptPolicy_WhenWithinLimit_ReturnsUnchangedTitle()
    {
        ToolConfiguration config = new() { MaxTitleLength = 10, TrimTitleWhenExceeded = true };

        CommitTitlePromptResult result = CommitTitlePolicy.ApplyPromptPolicy("short", config);

        Assert.Equal("short", result.Title);
        Assert.False(result.ExceededLimit);
        Assert.False(result.WasTrimmed);
    }

    [Fact]
    public void ApplyPromptPolicy_WhenTrimEnabled_TrimsToWordBoundary()
    {
        ToolConfiguration config = new() { MaxTitleLength = 12, TrimTitleWhenExceeded = true };

        CommitTitlePromptResult result = CommitTitlePolicy.ApplyPromptPolicy("fix login crash now", config);

        Assert.Equal("fix login", result.Title);
        Assert.True(result.ExceededLimit);
        Assert.True(result.WasTrimmed);
    }

    [Fact]
    public void ApplyPromptPolicy_WhenTrimEnabledAndNoSpaces_FallsBackToHardCutoff()
    {
        ToolConfiguration config = new() { MaxTitleLength = 8, TrimTitleWhenExceeded = true };

        CommitTitlePromptResult result = CommitTitlePolicy.ApplyPromptPolicy("abcdefghijk", config);

        Assert.Equal("abcdefgh", result.Title);
        Assert.True(result.ExceededLimit);
        Assert.True(result.WasTrimmed);
    }

    [Fact]
    public void ApplyPromptPolicy_WhenTrimDisabled_ReportsExceededWithoutTrimming()
    {
        ToolConfiguration config = new() { MaxTitleLength = 10, TrimTitleWhenExceeded = false };

        CommitTitlePromptResult result = CommitTitlePolicy.ApplyPromptPolicy("this title is too long", config);

        Assert.Equal("this title is too long", result.Title);
        Assert.True(result.ExceededLimit);
        Assert.False(result.WasTrimmed);
    }
}