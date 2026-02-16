using DotnetGitmoji.Services;

namespace DotnetGitmoji.Tests;

public class CommitMessageServiceTests
{
    private readonly CommitMessageService _service = new();

    [Fact]
    public async Task ReadMessageAsync_WhenFileOutsideGitDir_ThrowsArgumentException()
    {
        var path = Path.Combine(Path.GetTempPath(), "COMMIT_EDITMSG");

        await Assert.ThrowsAsync<ArgumentException>(() => _service.ReadMessageAsync(path));
    }

    [Fact]
    public async Task ReadMessageAsync_WhenFileInsideGitDir_ReturnsFirstLine()
    {
        var gitDir = Path.Combine(Path.GetTempPath(), ".git");
        Directory.CreateDirectory(gitDir);
        var filePath = Path.Combine(gitDir, "COMMIT_EDITMSG");

        try
        {
            await File.WriteAllTextAsync(filePath, "first line\nsecond line");

            var result = await _service.ReadMessageAsync(filePath);

            Assert.Equal("first line", result);
        }
        finally
        {
            File.Delete(filePath);
            Directory.Delete(gitDir);
        }
    }

    [Fact]
    public async Task ReadMessageAsync_WhenFileStartsWithComments_SkipsCommentLines()
    {
        var gitDir = Path.Combine(Path.GetTempPath(), ".git");
        Directory.CreateDirectory(gitDir);
        var filePath = Path.Combine(gitDir, "COMMIT_EDITMSG");

        try
        {
            await File.WriteAllTextAsync(filePath, "# This is a comment\n# Another comment\nActual message\n");

            var result = await _service.ReadMessageAsync(filePath);

            Assert.Equal("Actual message", result);
        }
        finally
        {
            File.Delete(filePath);
            Directory.Delete(gitDir);
        }
    }

    [Fact]
    public async Task ReadMessageAsync_WhenFileIsAllComments_ReturnsEmpty()
    {
        var gitDir = Path.Combine(Path.GetTempPath(), ".git");
        Directory.CreateDirectory(gitDir);
        var filePath = Path.Combine(gitDir, "COMMIT_EDITMSG");

        try
        {
            await File.WriteAllTextAsync(filePath, "# comment only\n# another comment\n");

            var result = await _service.ReadMessageAsync(filePath);

            Assert.Equal(string.Empty, result);
        }
        finally
        {
            File.Delete(filePath);
            Directory.Delete(gitDir);
        }
    }

    [Fact]
    public async Task WriteMessageAsync_WhenFileOutsideGitDir_ThrowsArgumentException()
    {
        var path = Path.Combine(Path.GetTempPath(), "COMMIT_EDITMSG");

        await Assert.ThrowsAsync<ArgumentException>(() => _service.WriteMessageAsync(path, "new message"));
    }
}