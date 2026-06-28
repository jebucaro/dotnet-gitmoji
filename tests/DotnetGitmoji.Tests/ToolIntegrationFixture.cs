using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace DotnetGitmoji.Tests;

public sealed class ToolIntegrationFixture : IAsyncLifetime
{
    private string _workDirectory = string.Empty;

    public string RepositoryRoot { get; private set; } = string.Empty;
    public string ToolPathDirectory { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        RepositoryRoot = FindRepositoryRoot();

        _workDirectory = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-it-{Guid.NewGuid():N}");
        string nupkgDirectory = Path.Combine(_workDirectory, "nupkg");
        ToolPathDirectory = Path.Combine(_workDirectory, "tool");

        Directory.CreateDirectory(nupkgDirectory);
        Directory.CreateDirectory(ToolPathDirectory);

        string projectPath = Path.Combine(RepositoryRoot, "src", "DotnetGitmoji", "DotnetGitmoji.csproj");
        string toolVersion = ReadToolVersion(projectPath);

        ProcessResult packResult = await RunProcessAsync(
            "dotnet",
            ["pack", projectPath, "-c", "Release", "-o", nupkgDirectory, "--nologo"],
            RepositoryRoot,
            timeoutSeconds: 180);

        if (packResult.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet pack failed with exit code {packResult.ExitCode}.{Environment.NewLine}" +
                $"STDOUT:{Environment.NewLine}{packResult.StandardOutput}{Environment.NewLine}" +
                $"STDERR:{Environment.NewLine}{packResult.StandardError}");
        }

        ProcessResult installResult = await RunProcessAsync(
            "dotnet",
            [
                "tool", "install",
                "--tool-path", ToolPathDirectory,
                "--add-source", nupkgDirectory,
                "--ignore-failed-sources",
                "dotnet-gitmoji",
                "--version", toolVersion
            ],
            RepositoryRoot,
            timeoutSeconds: 180);

        if (installResult.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet tool install failed with exit code {installResult.ExitCode}.{Environment.NewLine}" +
                $"STDOUT:{Environment.NewLine}{installResult.StandardOutput}{Environment.NewLine}" +
                $"STDERR:{Environment.NewLine}{installResult.StandardError}");
        }
    }

    public ValueTask DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_workDirectory) && Directory.Exists(_workDirectory))
        {
            Directory.Delete(_workDirectory, true);
        }

        return ValueTask.CompletedTask;
    }

    public Task<ProcessResult> RunToolAsync(string workingDirectory, params string[] arguments)
    {
        return RunProcessAsync(GetToolExecutablePath(), arguments, workingDirectory);
    }

    public Task<ProcessResult> RunToolAsync(
        string workingDirectory,
        IReadOnlyDictionary<string, string?> environment,
        params string[] arguments)
    {
        return RunProcessAsync(GetToolExecutablePath(), arguments, workingDirectory, environment);
    }

    public static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? environment = null,
        int timeoutSeconds = 60)
    {
        ProcessStartInfo startInfo = new(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["NO_COLOR"] = "1";

        if (environment is not null)
        {
            foreach ((string key, string? value) in environment)
            {
                startInfo.Environment[key] = value ?? string.Empty;
            }
        }

        using Process process = Process.Start(startInfo)
                                ?? throw new InvalidOperationException($"Failed to start process: {fileName}");

        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();

        using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }

            throw new TimeoutException(
                $"Process timed out after {timeoutSeconds} seconds: {fileName} {string.Join(" ", arguments)}");
        }

        string standardOutput = await standardOutputTask;
        string standardError = await standardErrorTask;

        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "dotnet-gitmoji.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test base directory.");
    }

    private static string ReadToolVersion(string projectPath)
    {
        XDocument document = XDocument.Load(projectPath);
        string? version = document
            .Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Version")
            ?.Value
            ?.Trim();

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidOperationException($"Could not read <Version> from {projectPath}.");
        }

        return version;
    }

    private string GetToolExecutablePath()
    {
        string[] preferredNames = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { "dotnet-gitmoji.exe", "dotnet-gitmoji.cmd", "dotnet-gitmoji.bat", "dotnet-gitmoji" }
            : new[] { "dotnet-gitmoji", "dotnet-gitmoji.exe", "dotnet-gitmoji.cmd" };

        foreach (string name in preferredNames)
        {
            string candidate = Path.Combine(ToolPathDirectory, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string? discoveredLauncher = Directory
            .EnumerateFiles(ToolPathDirectory, "dotnet-gitmoji*", SearchOption.TopDirectoryOnly)
            .OrderBy(GetLauncherPriority)
            .FirstOrDefault();

        if (discoveredLauncher is not null)
        {
            return discoveredLauncher;
        }

        throw new FileNotFoundException(
            $"Could not find installed dotnet-gitmoji executable in {ToolPathDirectory}.");
    }

    private static int GetLauncherPriority(string launcherPath)
    {
        string fileName = Path.GetFileName(launcherPath).ToLowerInvariant();

        return fileName switch
        {
            "dotnet-gitmoji" => 0,
            "dotnet-gitmoji.exe" => 1,
            "dotnet-gitmoji.cmd" => 2,
            "dotnet-gitmoji.bat" => 3,
            _ => 10
        };
    }
}

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);