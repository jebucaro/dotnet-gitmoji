using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace DotnetGitmoji.Tests;

public sealed class ToolIntegrationFixture : IAsyncLifetime
{
    private string _workDirectory = string.Empty;

    public string RepositoryRoot { get; private set; } = string.Empty;
    public string ToolPathDirectory { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        RepositoryRoot = FindRepositoryRoot();

        _workDirectory = Path.Combine(Path.GetTempPath(), $"dotnet-gitmoji-it-{Guid.NewGuid():N}");
        var nupkgDirectory = Path.Combine(_workDirectory, "nupkg");
        ToolPathDirectory = Path.Combine(_workDirectory, "tool");

        Directory.CreateDirectory(nupkgDirectory);
        Directory.CreateDirectory(ToolPathDirectory);

        var projectPath = Path.Combine(RepositoryRoot, "src", "DotnetGitmoji", "DotnetGitmoji.csproj");
        var toolVersion = ReadToolVersion(projectPath);

        var packResult = await RunProcessAsync(
            "dotnet",
            ["pack", projectPath, "-c", "Release", "-o", nupkgDirectory, "--nologo"],
            RepositoryRoot,
            timeoutSeconds: 180);

        if (packResult.ExitCode != 0)
            throw new InvalidOperationException(
                $"dotnet pack failed with exit code {packResult.ExitCode}.{Environment.NewLine}" +
                $"STDOUT:{Environment.NewLine}{packResult.StandardOutput}{Environment.NewLine}" +
                $"STDERR:{Environment.NewLine}{packResult.StandardError}");

        var installResult = await RunProcessAsync(
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
            throw new InvalidOperationException(
                $"dotnet tool install failed with exit code {installResult.ExitCode}.{Environment.NewLine}" +
                $"STDOUT:{Environment.NewLine}{installResult.StandardOutput}{Environment.NewLine}" +
                $"STDERR:{Environment.NewLine}{installResult.StandardError}");
    }

    public Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_workDirectory) && Directory.Exists(_workDirectory))
            Directory.Delete(_workDirectory, true);

        return Task.CompletedTask;
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
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        if (environment is not null)
            foreach (var (key, value) in environment)
                startInfo.Environment[key] = value ?? string.Empty;

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(true);

            throw new TimeoutException(
                $"Process timed out after {timeoutSeconds} seconds: {fileName} {string.Join(" ", arguments)}");
        }

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "dotnet-gitmoji.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test base directory.");
    }

    private static string ReadToolVersion(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        var version = document
            .Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Version")
            ?.Value
            ?.Trim();

        if (string.IsNullOrWhiteSpace(version))
            throw new InvalidOperationException($"Could not read <Version> from {projectPath}.");

        return version;
    }

    private string GetToolExecutablePath()
    {
        var preferredNames = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { "dotnet-gitmoji.exe", "dotnet-gitmoji.cmd", "dotnet-gitmoji.bat", "dotnet-gitmoji" }
            : new[] { "dotnet-gitmoji", "dotnet-gitmoji.exe", "dotnet-gitmoji.cmd" };

        foreach (var name in preferredNames)
        {
            var candidate = Path.Combine(ToolPathDirectory, name);
            if (File.Exists(candidate))
                return candidate;
        }

        var discoveredLauncher = Directory
            .EnumerateFiles(ToolPathDirectory, "dotnet-gitmoji*", SearchOption.TopDirectoryOnly)
            .OrderBy(GetLauncherPriority)
            .FirstOrDefault();

        if (discoveredLauncher is not null)
            return discoveredLauncher;

        throw new FileNotFoundException(
            $"Could not find installed dotnet-gitmoji executable in {ToolPathDirectory}.");
    }

    private static int GetLauncherPriority(string launcherPath)
    {
        var fileName = Path.GetFileName(launcherPath).ToLowerInvariant();

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