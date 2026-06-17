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
    public async Task ReadMessageAsync_WhenFileInsideGitDir_ReturnsSubject()
    {
        var gitDir = Path.Combine(Path.GetTempPath(), ".git");
        Directory.CreateDirectory(gitDir);
        var filePath = Path.Combine(gitDir, "COMMIT_EDITMSG");

        try
        {
            await File.WriteAllTextAsync(filePath, "first line\nsecond line", TestContext.Current.CancellationToken);

            var result = await _service.ReadMessageAsync(filePath);

            Assert.Equal("first line", result.Subject);
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

            Assert.Equal("Actual message", result.Subject);
        }
        finally
        {
            File.Delete(filePath);
            Directory.Delete(gitDir);
        }
    }

    [Fact]
    public async Task ReadMessageAsync_WhenFileIsAllComments_ReturnsEmptySubject()
    {
        var gitDir = Path.Combine(Path.GetTempPath(), ".git");
        Directory.CreateDirectory(gitDir);
        var filePath = Path.Combine(gitDir, "COMMIT_EDITMSG");

        try
        {
            await File.WriteAllTextAsync(filePath, "# comment only\n# another comment\n",
                TestContext.Current.CancellationToken);

            var result = await _service.ReadMessageAsync(filePath);

            Assert.Equal(string.Empty, result.Subject);
        }
        finally
        {
            File.Delete(filePath);
            Directory.Delete(gitDir);
        }
    }

    [Fact]
    public async Task ReadMessageAsync_WhenFileHasSubjectAndBody_ReturnsBoth()
    {
        var gitDir = Path.Combine(Path.GetTempPath(), ".git");
        Directory.CreateDirectory(gitDir);
        var filePath = Path.Combine(gitDir, "COMMIT_EDITMSG");

        try
        {
            await File.WriteAllTextAsync(filePath, "Fix bug\n\nThis is the body.",
                TestContext.Current.CancellationToken);

            var result = await _service.ReadMessageAsync(filePath);

            Assert.Equal("Fix bug", result.Subject);
            Assert.Equal("This is the body.", result.Body);
        }
        finally
        {
            File.Delete(filePath);
            Directory.Delete(gitDir);
        }
    }

    [Fact]
    public async Task ReadMessageAsync_WhenBodyHasCommentLines_ExcludesComments()
    {
        var gitDir = Path.Combine(Path.GetTempPath(), ".git");
        Directory.CreateDirectory(gitDir);
        var filePath = Path.Combine(gitDir, "COMMIT_EDITMSG");

        try
        {
            await File.WriteAllTextAsync(filePath, "Fix bug\n\nBody text.\n# git comment",
                TestContext.Current.CancellationToken);

            var result = await _service.ReadMessageAsync(filePath);

            Assert.Equal("Fix bug", result.Subject);
            Assert.Equal("Body text.", result.Body);
        }
        finally
        {
            File.Delete(filePath);
            Directory.Delete(gitDir);
        }
    }

    [Fact]
    public async Task ReadMessageAsync_WhenNoBlankLine_ReturnsNullBody()
    {
        var gitDir = Path.Combine(Path.GetTempPath(), ".git");
        Directory.CreateDirectory(gitDir);
        var filePath = Path.Combine(gitDir, "COMMIT_EDITMSG");

        try
        {
            await File.WriteAllTextAsync(filePath, "Fix bug\n# comment only after",
                TestContext.Current.CancellationToken);

            var result = await _service.ReadMessageAsync(filePath);

            Assert.Equal("Fix bug", result.Subject);
            Assert.Null(result.Body);
        }
        finally
        {
            File.Delete(filePath);
            Directory.Delete(gitDir);
        }
    }

    [Fact]
    public async Task ReadMessageAsync_WhenOnlyCommentsAfterBlankLine_ReturnsNullBody()
    {
        var gitDir = Path.Combine(Path.GetTempPath(), ".git");
        Directory.CreateDirectory(gitDir);
        var filePath = Path.Combine(gitDir, "COMMIT_EDITMSG");

        try
        {
            await File.WriteAllTextAsync(filePath, "Fix bug\n\n# only a comment",
                TestContext.Current.CancellationToken);

            var result = await _service.ReadMessageAsync(filePath);

            Assert.Equal("Fix bug", result.Subject);
            Assert.Null(result.Body);
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

    [Fact]
    public async Task WriteMessageAsync_WithSubjectAndBody_WritesSubjectBlankLineBody()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        var gitDir = Path.Combine(tempRoot, ".git");
        Directory.CreateDirectory(gitDir);
        var filePath = Path.Combine(gitDir, "COMMIT_EDITMSG");

        try
        {
            await File.WriteAllTextAsync(filePath, "old subject\n# comment\n",
                TestContext.Current.CancellationToken);

            await _service.WriteMessageAsync(filePath, "new subject", "body text");

            var lines = await File.ReadAllLinesAsync(filePath, TestContext.Current.CancellationToken);
            Assert.Equal("new subject", lines[0]);
            Assert.Equal(string.Empty, lines[1]);
            Assert.Equal("body text", lines[2]);
            Assert.Equal("# comment", lines[3]);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public async Task WriteMessageAsync_WithSubjectAndNullBody_WritesSubjectOnly()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-test-{Guid.NewGuid():N}");
        var gitDir = Path.Combine(tempRoot, ".git");
        Directory.CreateDirectory(gitDir);
        var filePath = Path.Combine(gitDir, "COMMIT_EDITMSG");

        try
        {
            await File.WriteAllTextAsync(filePath, "old subject\n# comment\n",
                TestContext.Current.CancellationToken);

            await _service.WriteMessageAsync(filePath, "new subject", null);

            var lines = await File.ReadAllLinesAsync(filePath, TestContext.Current.CancellationToken);
            Assert.Equal("new subject", lines[0]);
            Assert.Equal("# comment", lines[1]);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }
}