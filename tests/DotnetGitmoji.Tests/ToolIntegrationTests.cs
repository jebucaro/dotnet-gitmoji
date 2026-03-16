namespace DotnetGitmoji.Tests;

public sealed class ToolIntegrationTests : IClassFixture<ToolIntegrationFixture>
{
    private readonly ToolIntegrationFixture _fixture;

    public ToolIntegrationTests(ToolIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Hook_WhenMessageAlreadyHasGitmoji_LeavesMessageUnchanged()
    {
        await WithTemporaryRepositoryAsync(async repositoryRoot =>
        {
            var commitMessageFile = Path.Combine(repositoryRoot, ".git", "COMMIT_EDITMSG");
            await File.WriteAllTextAsync(commitMessageFile, ":art: Keep naming consistent");

            var result = await _fixture.RunToolAsync(repositoryRoot, commitMessageFile, "message");

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(":art: Keep naming consistent", (await File.ReadAllLinesAsync(commitMessageFile))[0]);
            Assert.DoesNotContain("no interactive terminal available", result.StandardError,
                StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Hook_WhenInvalidInNonInteractiveMode_LeavesMessageAndWarns()
    {
        await WithTemporaryRepositoryAsync(async repositoryRoot =>
        {
            var commitMessageFile = Path.Combine(repositoryRoot, ".git", "COMMIT_EDITMSG");
            await File.WriteAllTextAsync(commitMessageFile, "Bad message title");

            var result = await _fixture.RunToolAsync(repositoryRoot, commitMessageFile, "message");

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("Bad message title", (await File.ReadAllLinesAsync(commitMessageFile))[0]);
            Assert.Contains("no interactive terminal available", result.StandardError,
                StringComparison.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [InlineData("merge")]
    [InlineData("squash")]
    public async Task Hook_WhenCommitSourceIsMergeOrSquash_SkipsValidation(string commitSource)
    {
        await WithTemporaryRepositoryAsync(async repositoryRoot =>
        {
            var commitMessageFile = Path.Combine(repositoryRoot, ".git", "COMMIT_EDITMSG");
            await File.WriteAllTextAsync(commitMessageFile, "Message without gitmoji");

            var result = await _fixture.RunToolAsync(repositoryRoot, commitMessageFile, commitSource);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("Message without gitmoji", (await File.ReadAllLinesAsync(commitMessageFile))[0]);
            Assert.DoesNotContain("no interactive terminal available", result.StandardError,
                StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Init_WhenHuskyNetDetected_PrintsHuskyAddCommandWithoutDirectInstall()
    {
        await WithTemporaryRepositoryAsync(async repositoryRoot =>
        {
            var huskyShellFile = Path.Combine(repositoryRoot, ".husky", "_", "husky.sh");
            Directory.CreateDirectory(Path.GetDirectoryName(huskyShellFile)!);
            await File.WriteAllTextAsync(huskyShellFile, "#!/bin/sh");

            var result = await _fixture.RunToolAsync(repositoryRoot, "init");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Husky.Net detected", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains(
                "dotnet husky add prepare-commit-msg -c \"dotnet-gitmoji \\\"$1\\\" \\\"$2\\\"\"",
                result.StandardOutput, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(repositoryRoot, ".git", "hooks", "prepare-commit-msg")));
        });
    }

    [Fact]
    public async Task Init_WhenHuskyNetIsMissing_InstallsDirectHookScript()
    {
        await WithTemporaryRepositoryAsync(async repositoryRoot =>
        {
            var result = await _fixture.RunToolAsync(repositoryRoot, "init");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("prepare-commit-msg hook installed successfully.", result.StandardOutput,
                StringComparison.Ordinal);

            var hookFile = Path.Combine(repositoryRoot, ".git", "hooks", "prepare-commit-msg");
            Assert.True(File.Exists(hookFile));

            var hookContent = await File.ReadAllTextAsync(hookFile);
            Assert.Contains("dotnet-gitmoji \"$1\" \"$2\"", hookContent, StringComparison.Ordinal);
        });
    }

    private static async Task WithTemporaryRepositoryAsync(Func<string, Task> action)
    {
        var repositoryRoot = await CreateTemporaryRepositoryAsync();
        try
        {
            await action(repositoryRoot);
        }
        finally
        {
            if (Directory.Exists(repositoryRoot))
                Directory.Delete(repositoryRoot, true);
        }
    }

    private static async Task<string> CreateTemporaryRepositoryAsync()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-repo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repositoryRoot);

        var gitInitResult = await ToolIntegrationFixture.RunProcessAsync(
            "git",
            ["init", "--quiet"],
            repositoryRoot);

        if (gitInitResult.ExitCode != 0)
            throw new InvalidOperationException(
                $"git init failed with exit code {gitInitResult.ExitCode}.{Environment.NewLine}" +
                $"STDOUT:{Environment.NewLine}{gitInitResult.StandardOutput}{Environment.NewLine}" +
                $"STDERR:{Environment.NewLine}{gitInitResult.StandardError}");

        await File.WriteAllTextAsync(
            Path.Combine(repositoryRoot, ".gitmojirc.json"),
            "{ \"gitmojisUrl\": \"http://localhost/gitmojis\" }");

        return repositoryRoot;
    }
}