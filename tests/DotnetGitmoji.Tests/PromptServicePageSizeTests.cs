using DotnetGitmoji.Services;

namespace DotnetGitmoji.Tests;

public class PromptServicePageSizeTests
{
    [Fact]
    public void CalculatePageSize_WhenTerminalIsTall_ReturnsCap()
    {
        var pageSize = PromptService.CalculatePageSize(
            50, true, true, 15);

        Assert.Equal(15, pageSize);
    }

    [Fact]
    public void CalculatePageSize_WhenWindowCannotFitMinimum_ShrinksToFit()
    {
        // 12 - (5 chrome + 2 header + 6 detail) = -1; the minimum (3) would overflow,
        // so we shrink the list to fit rather than scroll (floor of 1 row).
        var pageSize = PromptService.CalculatePageSize(
            12, true, true, 15);

        Assert.Equal(1, pageSize);
    }

    [Fact]
    public void CalculatePageSize_WhenWindowJustFitsMinimum_ReturnsMinimum()
    {
        // 16 - 13 overhead = 3, exactly the usability minimum.
        var pageSize = PromptService.CalculatePageSize(
            16, true, true, 15);

        Assert.Equal(3, pageSize);
    }

    [Fact]
    public void CalculatePageSize_WhenTerminalIsMedium_FitsWithinAvailableHeight()
    {
        // 24 - (5 chrome + 2 header + 6 detail) = 11.
        var pageSize = PromptService.CalculatePageSize(
            24, true, true, 15);

        Assert.Equal(11, pageSize);
    }

    [Fact]
    public void CalculatePageSize_WithoutHeaderAndDetail_ReservesLessOverhead()
    {
        // 24 - 5 chrome = 19, clamped down to the cap of 12.
        var withChrome = PromptService.CalculatePageSize(
            24, false, false, 12);
        // Same height with header + detail reserves more, so fits fewer rows.
        var withHeaderAndDetail = PromptService.CalculatePageSize(
            24, true, true, 12);

        Assert.Equal(12, withChrome);
        Assert.True(withHeaderAndDetail < withChrome);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CalculatePageSize_WhenWindowHeightUnknown_ReturnsCap(int windowHeight)
    {
        var pageSize = PromptService.CalculatePageSize(
            windowHeight, true, true, 15);

        Assert.Equal(15, pageSize);
    }
}