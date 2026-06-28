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
            string commitMessageFile = Path.Combine(repositoryRoot, ".git", "COMMIT_EDITMSG");
            await File.WriteAllTextAsync(commitMessageFile, ":art: Keep naming consistent");

            ProcessResult result = await _fixture.RunToolAsync(repositoryRoot, commitMessageFile, "message");

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
            string commitMessageFile = Path.Combine(repositoryRoot, ".git", "COMMIT_EDITMSG");
            await File.WriteAllTextAsync(commitMessageFile, "Bad message title");

            ProcessResult result = await _fixture.RunToolAsync(repositoryRoot, commitMessageFile, "message");

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
            string commitMessageFile = Path.Combine(repositoryRoot, ".git", "COMMIT_EDITMSG");
            await File.WriteAllTextAsync(commitMessageFile, "Message without gitmoji");

            ProcessResult result = await _fixture.RunToolAsync(repositoryRoot, commitMessageFile, commitSource);

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
            string huskyShellFile = Path.Combine(repositoryRoot, ".husky", "_", "husky.sh");
            Directory.CreateDirectory(Path.GetDirectoryName(huskyShellFile)!);
            await File.WriteAllTextAsync(huskyShellFile, "#!/bin/sh");

            ProcessResult result = await _fixture.RunToolAsync(repositoryRoot, "init");

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

            ProcessResult result = await _fixture.RunToolAsync(repositoryRoot, "init");

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
            IReadOnlyDictionary<string, string?> environment =
                await CreateDotnetHuskyShimEnvironmentAsync(repositoryRoot, true);

            ProcessResult result = await _fixture.RunToolAsync(repositoryRoot, environment, "init", "--mode", "shell");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("configured using shell mode", result.StandardOutput, StringComparison.Ordinal);

            string huskyHookFile = Path.Combine(repositoryRoot, ".husky", "prepare-commit-msg");
            Assert.True(File.Exists(huskyHookFile));
            string hookContent = await File.ReadAllTextAsync(huskyHookFile);
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
            string taskRunnerFile = Path.Combine(repositoryRoot, ".husky", "task-runner.json");
            Directory.CreateDirectory(Path.GetDirectoryName(taskRunnerFile)!);
            await File.WriteAllTextAsync(taskRunnerFile, "{}");

            IReadOnlyDictionary<string, string?> environment =
                await CreateDotnetHuskyShimEnvironmentAsync(repositoryRoot, true);
            ProcessResult result =
                await _fixture.RunToolAsync(repositoryRoot, environment, "init", "--mode", "task-runner");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("configured using task-runner mode", result.StandardOutput, StringComparison.Ordinal);

            string huskyHookFile = Path.Combine(repositoryRoot, ".husky", "prepare-commit-msg");
            Assert.True(File.Exists(huskyHookFile));
            string hookContent = await File.ReadAllTextAsync(huskyHookFile);
            Assert.Contains("dotnet husky run --name dotnet-gitmoji", hookContent, StringComparison.Ordinal);
            Assert.Contains("$1", hookContent, StringComparison.Ordinal);
            Assert.Contains("$2", hookContent, StringComparison.Ordinal);

            string taskRunnerContent = await File.ReadAllTextAsync(taskRunnerFile);
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
            string huskyShellFile = Path.Combine(repositoryRoot, ".husky", "_", "husky.sh");
            string taskRunnerFile = Path.Combine(repositoryRoot, ".husky", "task-runner.json");
            string huskyHookFile = Path.Combine(repositoryRoot, ".husky", "prepare-commit-msg");

            Directory.CreateDirectory(Path.GetDirectoryName(huskyShellFile)!);
            await File.WriteAllTextAsync(huskyShellFile, "#!/bin/sh");
            await File.WriteAllTextAsync(taskRunnerFile, "{}");
            await File.WriteAllTextAsync(
                huskyHookFile,
                "#!/bin/sh\n" +
                ". \"$(dirname \"$0\")/_/husky.sh\"\n" +
                "# dotnet-gitmoji \"$1\" \"$2\"\n" +
                "# dotnet tool run dotnet-gitmoji -- \"$1\" \"$2\"\n");

            IReadOnlyDictionary<string, string?> environment =
                await CreateDotnetHuskyShimEnvironmentAsync(repositoryRoot, true);
            ProcessResult result = await _fixture.RunToolAsync(repositoryRoot, environment, "init", "--mode", "shell");

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
            string huskyHookFile = Path.Combine(repositoryRoot, ".husky", "prepare-commit-msg");
            Directory.CreateDirectory(Path.GetDirectoryName(huskyHookFile)!);
            await File.WriteAllTextAsync(huskyHookFile, "#!/bin/sh\ndotnet-gitmoji \"$1\" \"$2\"\n");

            ProcessResult result = await _fixture.RunToolAsync(repositoryRoot, "init");

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
            ProcessResult result = await _fixture.RunToolAsync(repositoryRoot, "init", "--mode", "shell");

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
            ProcessResult result = await _fixture.RunToolAsync(repositoryRoot, "init");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("prepare-commit-msg hook installed successfully.", result.StandardOutput,
                StringComparison.Ordinal);

            string hookFile = Path.Combine(repositoryRoot, ".git", "hooks", "prepare-commit-msg");
            Assert.True(File.Exists(hookFile));

            string hookContent = await File.ReadAllTextAsync(hookFile);
            Assert.Contains("dotnet-gitmoji \"$1\" \"$2\"", hookContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Init_WhenManifestAtRepoRoot_WritesLocalToolHookCommand()
    {
        await WithTemporaryRepositoryAsync(async repositoryRoot =>
        {
            Directory.CreateDirectory(Path.Combine(repositoryRoot, ".husky"));
            await WriteToolsManifestAsync(
                Path.Combine(repositoryRoot, "dotnet-tools.json"),
                "dotnet-gitmoji");
            IReadOnlyDictionary<string, string?> environment =
                await CreateDotnetHuskyShimEnvironmentAsync(repositoryRoot, true);

            ProcessResult result = await _fixture.RunToolAsync(repositoryRoot, environment, "init", "--mode", "shell");

            Assert.Equal(0, result.ExitCode);

            string hookContent = await File.ReadAllTextAsync(
                Path.Combine(repositoryRoot, ".husky", "prepare-commit-msg"));
            Assert.Contains("dotnet tool run dotnet-gitmoji", hookContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Init_WhenManifestAtParentDirectory_WritesLocalToolHookCommand()
    {
        await WithTemporaryParentRepositoryAsync(async (parentDirectory, repositoryRoot) =>
        {
            Directory.CreateDirectory(Path.Combine(repositoryRoot, ".husky"));
            await WriteToolsManifestAsync(
                Path.Combine(parentDirectory, ".config", "dotnet-tools.json"),
                "dotnet-gitmoji");
            IReadOnlyDictionary<string, string?> environment =
                await CreateDotnetHuskyShimEnvironmentAsync(repositoryRoot, true);

            ProcessResult result = await _fixture.RunToolAsync(repositoryRoot, environment, "init", "--mode", "shell");

            Assert.Equal(0, result.ExitCode);

            string hookContent = await File.ReadAllTextAsync(
                Path.Combine(repositoryRoot, ".husky", "prepare-commit-msg"));
            Assert.Contains("dotnet tool run dotnet-gitmoji", hookContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Init_WhenManifestKeyHasNonCanonicalCasing_WritesLocalToolHookCommand()
    {
        await WithTemporaryRepositoryAsync(async repositoryRoot =>
        {
            Directory.CreateDirectory(Path.Combine(repositoryRoot, ".husky"));
            await WriteToolsManifestAsync(
                Path.Combine(repositoryRoot, ".config", "dotnet-tools.json"),
                "Dotnet-Gitmoji");
            IReadOnlyDictionary<string, string?> environment =
                await CreateDotnetHuskyShimEnvironmentAsync(repositoryRoot, true);

            ProcessResult result = await _fixture.RunToolAsync(repositoryRoot, environment, "init", "--mode", "shell");

            Assert.Equal(0, result.ExitCode);

            string hookContent = await File.ReadAllTextAsync(
                Path.Combine(repositoryRoot, ".husky", "prepare-commit-msg"));
            Assert.Contains("dotnet tool run dotnet-gitmoji", hookContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Init_WhenDotnetHuskyAddFails_SurfacesExecutionError()
    {
        await WithTemporaryRepositoryAsync(async repositoryRoot =>
        {
            Directory.CreateDirectory(Path.Combine(repositoryRoot, ".husky"));
            IReadOnlyDictionary<string, string?> environment =
                await CreateDotnetHuskyShimEnvironmentAsync(repositoryRoot, false);

            ProcessResult result = await _fixture.RunToolAsync(repositoryRoot, environment, "init", "--mode", "shell");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Failed to run 'dotnet husky add prepare-commit-msg'",
                result.StandardError + result.StandardOutput, StringComparison.Ordinal);
        });
    }

    private static async Task<IReadOnlyDictionary<string, string?>> CreateDotnetHuskyShimEnvironmentAsync(
        string repositoryRoot,
        bool shouldSucceed)
    {
        string shimDirectory = Path.Combine(repositoryRoot, ".dotnet-shim");
        Directory.CreateDirectory(shimDirectory);

        if (OperatingSystem.IsWindows())
        {
            string scriptPath = Path.Combine(shimDirectory, "dotnet.cmd");
            string script = shouldSucceed
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
            string scriptPath = Path.Combine(shimDirectory, "dotnet");
            string script = shouldSucceed
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

            ProcessResult chmodResult = await ToolIntegrationFixture.RunProcessAsync(
                "chmod",
                ["+x", scriptPath],
                repositoryRoot);

            if (chmodResult.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"chmod failed with exit code {chmodResult.ExitCode}.{Environment.NewLine}" +
                    $"STDOUT:{Environment.NewLine}{chmodResult.StandardOutput}{Environment.NewLine}" +
                    $"STDERR:{Environment.NewLine}{chmodResult.StandardError}");
            }
        }

        string? currentPath = Environment.GetEnvironmentVariable("PATH");
        string pathValue = string.IsNullOrWhiteSpace(currentPath)
            ? shimDirectory
            : $"{shimDirectory}{Path.PathSeparator}{currentPath}";

        return new Dictionary<string, string?> { ["PATH"] = pathValue };
    }

    private static async Task WithTemporaryRepositoryAsync(Func<string, Task> action)
    {
        string repositoryRoot = await CreateTemporaryRepositoryAsync();
        try
        {
            await action(repositoryRoot);
        }
        finally
        {
            if (Directory.Exists(repositoryRoot))
            {
                Directory.Delete(repositoryRoot, true);
            }
        }
    }

    private static async Task WithTemporaryParentRepositoryAsync(Func<string, string, Task> action)
    {
        string parentDirectory = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-parent-{Guid.NewGuid():N}");
        Directory.CreateDirectory(parentDirectory);
        try
        {
            string repositoryRoot = await CreateTemporaryRepositoryAsync(parentDirectory);
            await action(parentDirectory, repositoryRoot);
        }
        finally
        {
            if (Directory.Exists(parentDirectory))
            {
                Directory.Delete(parentDirectory, true);
            }
        }
    }

    private static async Task WriteToolsManifestAsync(string manifestPath, string toolKey)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        string contents =
            "{\n" +
            "  \"version\": 1,\n" +
            "  \"isRoot\": true,\n" +
            "  \"tools\": {\n" +
            $"    \"{toolKey}\": {{ \"version\": \"0.0.0\", \"commands\": [\"dotnet-gitmoji\"] }}\n" +
            "  }\n" +
            "}\n";
        await File.WriteAllTextAsync(manifestPath, contents);
    }

    private static async Task<string> CreateTemporaryRepositoryAsync(string? parentDirectory = null)
    {
        string basePath = parentDirectory ?? Path.GetTempPath();
        string repositoryRoot = Path.Combine(basePath, $"dotnet-gitmoji-repo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repositoryRoot);

        ProcessResult gitInitResult = await ToolIntegrationFixture.RunProcessAsync(
            "git",
            ["init", "--quiet"],
            repositoryRoot);

        if (gitInitResult.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git init failed with exit code {gitInitResult.ExitCode}.{Environment.NewLine}" +
                $"STDOUT:{Environment.NewLine}{gitInitResult.StandardOutput}{Environment.NewLine}" +
                $"STDERR:{Environment.NewLine}{gitInitResult.StandardError}");
        }

        await File.WriteAllTextAsync(
            Path.Combine(repositoryRoot, ".gitmojirc.json"),
            "{ \"gitmojisUrl\": \"http://localhost/gitmojis\" }");

        return repositoryRoot;
    }
}