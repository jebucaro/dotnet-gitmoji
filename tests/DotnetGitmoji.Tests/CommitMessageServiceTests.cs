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
            await File.WriteAllTextAsync(filePath, "first line\nsecond line", TestContext.Current.CancellationToken);

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
            await File.WriteAllTextAsync(filePath, "# This is a comment\n# Another comment\nActual message\n",
                TestContext.Current.CancellationToken);

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
            await File.WriteAllTextAsync(filePath, "# comment only\n# another comment\n",
                TestContext.Current.CancellationToken);

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

    [Fact]
    public async Task WriteMessageAsync_WhenFileHasLines_ReplacesFirstLine()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        var gitDir = Path.Combine(tempRoot, ".git");
        Directory.CreateDirectory(gitDir);
        var filePath = Path.Combine(gitDir, "COMMIT_EDITMSG");

        try
        {
            await File.WriteAllTextAsync(filePath, "old first line\n# comment\n",
                TestContext.Current.CancellationToken);

            await _service.WriteMessageAsync(filePath, "new message");

            var lines = await File.ReadAllLinesAsync(filePath, TestContext.Current.CancellationToken);
            Assert.Equal("new message", lines[0]);
            Assert.Equal("# comment", lines[1]);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public async Task WriteMessageAsync_WhenFileIsEmpty_WritesEntireMessage()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        var gitDir = Path.Combine(tempRoot, ".git");
        Directory.CreateDirectory(gitDir);
        var filePath = Path.Combine(gitDir, "COMMIT_EDITMSG");

        try
        {
            await File.WriteAllTextAsync(filePath, string.Empty, TestContext.Current.CancellationToken);

            await _service.WriteMessageAsync(filePath, "brand new message");

            var content = await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken);
            Assert.Equal("brand new message", content);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }
}