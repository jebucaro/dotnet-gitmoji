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
    public async Task Init_WhenJsHuskyDetected_PrintsInformationalMessageWithoutDirectInstall()
    {
        await WithTemporaryRepositoryAsync(async repositoryRoot =>
        {
            var huskyShellFile = Path.Combine(repositoryRoot, ".husky", "_", "husky.sh");
            Directory.CreateDirectory(Path.GetDirectoryName(huskyShellFile)!);
            await File.WriteAllTextAsync(huskyShellFile, "#!/bin/sh");

            var result = await _fixture.RunToolAsync(repositoryRoot, "init");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("JavaScript Husky detected", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("No files were modified.", result.StandardOutput, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(repositoryRoot, ".git", "hooks", "prepare-commit-msg")));
        });
    }

    [Fact]
    public async Task Init_WhenHuskyNetDetectedWithoutMode_ReportsRequiredOption()
    {
        await WithTemporaryRepositoryAsync(async repositoryRoot =>
        {
            Directory.CreateDirectory(Path.Combine(repositoryRoot, ".husky"));

            var result = await _fixture.RunToolAsync(repositoryRoot, "init");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Select setup mode with '--mode shell' or '--mode task-runner'",
                result.StandardError + result.StandardOutput, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(repositoryRoot, ".git", "hooks", "prepare-commit-msg")));
        });
    }

    [Fact]
    public async Task Init_WhenHuskyNetShellDetected_ConfiguresShellHook()
    {
        await WithTemporaryRepositoryAsync(async repositoryRoot =>
        {
            Directory.CreateDirectory(Path.Combine(repositoryRoot, ".husky"));
            var environment = await CreateDotnetHuskyShimEnvironmentAsync(repositoryRoot, true);

            var result = await _fixture.RunToolAsync(repositoryRoot, environment, "init", "--mode", "shell");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("configured using shell mode", result.StandardOutput, StringComparison.Ordinal);

            var huskyHookFile = Path.Combine(repositoryRoot, ".husky", "prepare-commit-msg");
            Assert.True(File.Exists(huskyHookFile));
            var hookContent = await File.ReadAllTextAsync(huskyHookFile);
            Assert.Contains("dotnet-gitmoji", hookContent, StringComparison.Ordinal);
            Assert.Contains("$1", hookContent, StringComparison.Ordinal);
            Assert.Contains("$2", hookContent, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(repositoryRoot, ".git", "hooks", "prepare-commit-msg")));
        });
    }

    [Fact]
    public async Task Init_WhenHuskyNetTaskRunnerDetected_ConfiguresTaskRunnerHookAndTask()
    {
        await WithTemporaryRepositoryAsync(async repositoryRoot =>
        {
            var taskRunnerFile = Path.Combine(repositoryRoot, ".husky", "task-runner.json");
            Directory.CreateDirectory(Path.GetDirectoryName(taskRunnerFile)!);
            await File.WriteAllTextAsync(taskRunnerFile, "{}");

            var environment = await CreateDotnetHuskyShimEnvironmentAsync(repositoryRoot, true);
            var result = await _fixture.RunToolAsync(repositoryRoot, environment, "init", "--mode", "task-runner");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("configured using task-runner mode", result.StandardOutput, StringComparison.Ordinal);

            var huskyHookFile = Path.Combine(repositoryRoot, ".husky", "prepare-commit-msg");
            Assert.True(File.Exists(huskyHookFile));
            var hookContent = await File.ReadAllTextAsync(huskyHookFile);
            Assert.Contains("dotnet husky run --name dotnet-gitmoji", hookContent, StringComparison.Ordinal);
            Assert.Contains("$1", hookContent, StringComparison.Ordinal);
            Assert.Contains("$2", hookContent, StringComparison.Ordinal);

            var taskRunnerContent = await File.ReadAllTextAsync(taskRunnerFile);
            Assert.Contains("\"name\": \"dotnet-gitmoji\"", taskRunnerContent, StringComparison.Ordinal);
            Assert.Contains("\"command\": \"dotnet-gitmoji\"", taskRunnerContent, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(repositoryRoot, ".git", "hooks", "prepare-commit-msg")));
        });
    }

    [Fact]
    public async Task Init_WhenHuskyNetLayoutIncludesCommentedExamples_DoesNotTreatHookAsInstalled()
    {
        await WithTemporaryRepositoryAsync(async repositoryRoot =>
        {
            var huskyShellFile = Path.Combine(repositoryRoot, ".husky", "_", "husky.sh");
            var taskRunnerFile = Path.Combine(repositoryRoot, ".husky", "task-runner.json");
            var huskyHookFile = Path.Combine(repositoryRoot, ".husky", "prepare-commit-msg");

            Directory.CreateDirectory(Path.GetDirectoryName(huskyShellFile)!);
            await File.WriteAllTextAsync(huskyShellFile, "#!/bin/sh");
            await File.WriteAllTextAsync(taskRunnerFile, "{}");
            await File.WriteAllTextAsync(
                huskyHookFile,
                "#!/bin/sh\n" +
                ". \"$(dirname \"$0\")/_/husky.sh\"\n" +
                "# dotnet-gitmoji \"$1\" \"$2\"\n" +
                "# dotnet tool run dotnet-gitmoji -- \"$1\" \"$2\"\n");

            var environment = await CreateDotnetHuskyShimEnvironmentAsync(repositoryRoot, true);
            var result = await _fixture.RunToolAsync(repositoryRoot, environment, "init", "--mode", "shell");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("configured using shell mode", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("JavaScript Husky detected", result.StandardOutput, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(repositoryRoot, ".git", "hooks", "prepare-commit-msg")));
        });
    }

    [Fact]
    public async Task Init_WhenHuskyHookContainsActiveDotnetGitmoji_ReportsAlreadyInstalled()
    {
        await WithTemporaryRepositoryAsync(async repositoryRoot =>
        {
            var huskyHookFile = Path.Combine(repositoryRoot, ".husky", "prepare-commit-msg");
            Directory.CreateDirectory(Path.GetDirectoryName(huskyHookFile)!);
            await File.WriteAllTextAsync(huskyHookFile, "#!/bin/sh\ndotnet-gitmoji \"$1\" \"$2\"\n");

            var result = await _fixture.RunToolAsync(repositoryRoot, "init");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("already installed", result.StandardError + result.StandardOutput,
                StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Init_WhenModeIsSpecifiedWithoutHusky_ReportsInvalidUsage()
    {
        await WithTemporaryRepositoryAsync(async repositoryRoot =>
        {
            var result = await _fixture.RunToolAsync(repositoryRoot, "init", "--mode", "shell");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("The --mode option is only valid when Husky.Net is detected.",
                result.StandardError + result.StandardOutput, StringComparison.Ordinal);
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

    [Fact]
    public async Task Init_WhenDotnetHuskyAddFails_SurfacesExecutionError()
    {
        await WithTemporaryRepositoryAsync(async repositoryRoot =>
        {
            Directory.CreateDirectory(Path.Combine(repositoryRoot, ".husky"));
            var environment = await CreateDotnetHuskyShimEnvironmentAsync(repositoryRoot, false);

            var result = await _fixture.RunToolAsync(repositoryRoot, environment, "init", "--mode", "shell");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Failed to run 'dotnet husky add prepare-commit-msg'",
                result.StandardError + result.StandardOutput, StringComparison.Ordinal);
        });
    }

    private static async Task<IReadOnlyDictionary<string, string?>> CreateDotnetHuskyShimEnvironmentAsync(
        string repositoryRoot,
        bool shouldSucceed)
    {
        var shimDirectory = Path.Combine(repositoryRoot, ".dotnet-shim");
        Directory.CreateDirectory(shimDirectory);

        if (OperatingSystem.IsWindows())
        {
            var scriptPath = Path.Combine(shimDirectory, "dotnet.cmd");
            var script = shouldSucceed
                ? "@echo off\r\n" +
                  "setlocal\r\n" +
                  "if /I \"%1\"==\"husky\" if /I \"%2\"==\"add\" if /I \"%3\"==\"prepare-commit-msg\" if /I \"%4\"==\"-c\" (\r\n" +
                  "  if not exist \".husky\" mkdir \".husky\"\r\n" +
                  "  > \".husky\\prepare-commit-msg\" (\r\n" +
                  "    echo #!/bin/sh\r\n" +
                  "    echo %~5\r\n" +
                  "  )\r\n" +
                  "  exit /b 0\r\n" +
                  ")\r\n" +
                  "echo simulated husky failure>&2\r\n" +
                  "exit /b 1\r\n"
                : "@echo off\r\n" +
                  "echo simulated husky failure>&2\r\n" +
                  "exit /b 1\r\n";

            await File.WriteAllTextAsync(scriptPath, script);
        }
        else
        {
            var scriptPath = Path.Combine(shimDirectory, "dotnet");
            var script = shouldSucceed
                ? "#!/bin/sh\n" +
                  "if [ \"$1\" = \"husky\" ] && [ \"$2\" = \"add\" ] && [ \"$3\" = \"prepare-commit-msg\" ] && [ \"$4\" = \"-c\" ]; then\n" +
                  "  mkdir -p .husky\n" +
                  "  {\n" +
                  "    echo '#!/bin/sh'\n" +
                  "    echo \"$5\"\n" +
                  "  } > .husky/prepare-commit-msg\n" +
                  "  exit 0\n" +
                  "fi\n" +
                  "echo 'simulated husky failure' >&2\n" +
                  "exit 1\n"
                : "#!/bin/sh\n" +
                  "echo 'simulated husky failure' >&2\n" +
                  "exit 1\n";

            await File.WriteAllTextAsync(scriptPath, script);

            var chmodResult = await ToolIntegrationFixture.RunProcessAsync(
                "chmod",
                ["+x", scriptPath],
                repositoryRoot);

            if (chmodResult.ExitCode != 0)
                throw new InvalidOperationException(
                    $"chmod failed with exit code {chmodResult.ExitCode}.{Environment.NewLine}" +
                    $"STDOUT:{Environment.NewLine}{chmodResult.StandardOutput}{Environment.NewLine}" +
                    $"STDERR:{Environment.NewLine}{chmodResult.StandardError}");
        }

        var currentPath = Environment.GetEnvironmentVariable("PATH");
        var pathValue = string.IsNullOrWhiteSpace(currentPath)
            ? shimDirectory
            : $"{shimDirectory}{Path.PathSeparator}{currentPath}";

        return new Dictionary<string, string?>
        {
            ["PATH"] = pathValue
        };
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