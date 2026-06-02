using DotnetGitmoji.Models;
using DotnetGitmoji.Services;

namespace DotnetGitmoji.Tests;

public class CommitTitlePolicyTests
{
    [Fact]
    public void ValidateExplicitTitle_WhenLimitDisabled_ReturnsNull()
    {
        var config = new ToolConfiguration { MaxTitleLength = null };

        var error = CommitTitlePolicy.ValidateExplicitTitle(new string('a', 200), config);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateExplicitTitle_WhenLengthExceedsConfiguredMax_ReturnsError()
    {
        var config = new ToolConfiguration { MaxTitleLength = 10 };

        var error = CommitTitlePolicy.ValidateExplicitTitle("This title is too long", config);

        Assert.NotNull(error);
        Assert.Contains("10", error);
    }

    [Fact]
    public void ApplyPromptPolicy_WhenWithinLimit_ReturnsUnchangedTitle()
    {
        var config = new ToolConfiguration { MaxTitleLength = 10, TrimTitleWhenExceeded = true };

        var result = CommitTitlePolicy.ApplyPromptPolicy("short", config);

        Assert.Equal("short", result.Title);
        Assert.False(result.ExceededLimit);
        Assert.False(result.WasTrimmed);
    }

    [Fact]
    public void ApplyPromptPolicy_WhenTrimEnabled_TrimsToWordBoundary()
    {
        var config = new ToolConfiguration { MaxTitleLength = 12, TrimTitleWhenExceeded = true };

        var result = CommitTitlePolicy.ApplyPromptPolicy("fix login crash now", config);

        Assert.Equal("fix login", result.Title);
        Assert.True(result.ExceededLimit);
        Assert.True(result.WasTrimmed);
    }

    [Fact]
    public void ApplyPromptPolicy_WhenTrimEnabledAndNoSpaces_FallsBackToHardCutoff()
    {
        var config = new ToolConfiguration { MaxTitleLength = 8, TrimTitleWhenExceeded = true };

        var result = CommitTitlePolicy.ApplyPromptPolicy("abcdefghijk", config);

        Assert.Equal("abcdefgh", result.Title);
        Assert.True(result.ExceededLimit);
        Assert.True(result.WasTrimmed);
    }

    [Fact]
    public void ApplyPromptPolicy_WhenTrimDisabled_ReportsExceededWithoutTrimming()
    {
        var config = new ToolConfiguration { MaxTitleLength = 10, TrimTitleWhenExceeded = false };

        var result = CommitTitlePolicy.ApplyPromptPolicy("this title is too long", config);

        Assert.Equal("this title is too long", result.Title);
        Assert.True(result.ExceededLimit);
        Assert.False(result.WasTrimmed);
    }
}