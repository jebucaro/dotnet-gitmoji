using CliFx;
using CliFx.Infrastructure;
using DotnetGitmoji.Commands;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using NSubstitute;

namespace DotnetGitmoji.Tests;

public class InitCommandTests
{
    private const string AlreadyInstalledFragment = "already installed";
    private const string SelectSetupModeFragment = "Select setup mode";
    private const string ModeOnlyValidFragment = "only valid when Husky.Net";
    private const string InvalidModeFragment = "Invalid value for --mode";

    private readonly IGitService _gitService = Substitute.For<IGitService>();
    private readonly IConfigurationService _configService = Substitute.For<IConfigurationService>();

    public InitCommandTests()
    {
        _gitService.IsHookInstalledAsync().Returns(false);
        _gitService.DetectHuskyKindAsync().Returns(HuskyInstallKind.None);
    }

    private InitCommand CreateCommand(string? mode = null, bool createConfig = false)
    {
        return new InitCommand(_gitService, _configService)
        {
            Mode = mode,
            CreateConfig = createConfig
        };
    }

    [Fact]
    public async Task ExecuteAsync_WhenHookAlreadyInstalled_ThrowsCommandException()
    {
        _gitService.IsHookInstalledAsync().Returns(true);
        var command = CreateCommand();
        var console = new FakeInMemoryConsole();

        var ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains(AlreadyInstalledFragment, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJsHuskyDetected_ShowsGuidanceWithoutInstallingHook()
    {
        _gitService.DetectHuskyKindAsync().Returns(HuskyInstallKind.JsHusky);
        var command = CreateCommand();
        var console = new FakeInMemoryConsole();

        await command.ExecuteAsync(console);

        await _gitService.DidNotReceive().InstallHookDirectAsync();
        await _gitService.DidNotReceive().InstallHuskyNetShellHookAsync();
        await _gitService.DidNotReceive().InstallHuskyNetTaskRunnerHookAsync();
    }

    [Theory]
    [InlineData(HuskyInstallKind.HuskyNetShell)]
    [InlineData(HuskyInstallKind.HuskyNetTaskRunner)]
    public async Task ExecuteAsync_WhenHuskyNetDetectedWithoutMode_ThrowsCommandException(HuskyInstallKind kind)
    {
        _gitService.DetectHuskyKindAsync().Returns(kind);
        var command = CreateCommand(null);
        var console = new FakeInMemoryConsole();

        var ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains(SelectSetupModeFragment, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(HuskyInstallKind.HuskyNetShell)]
    [InlineData(HuskyInstallKind.HuskyNetTaskRunner)]
    public async Task ExecuteAsync_WhenHuskyNetAndShellMode_CallsInstallShellHook(HuskyInstallKind kind)
    {
        _gitService.DetectHuskyKindAsync().Returns(kind);
        var command = CreateCommand("shell");
        var console = new FakeInMemoryConsole();

        await command.ExecuteAsync(console);

        await _gitService.Received(1).InstallHuskyNetShellHookAsync();
        await _gitService.DidNotReceive().InstallHuskyNetTaskRunnerHookAsync();
    }

    [Theory]
    [InlineData(HuskyInstallKind.HuskyNetShell)]
    [InlineData(HuskyInstallKind.HuskyNetTaskRunner)]
    public async Task ExecuteAsync_WhenHuskyNetAndTaskRunnerMode_CallsInstallTaskRunnerHook(HuskyInstallKind kind)
    {
        _gitService.DetectHuskyKindAsync().Returns(kind);
        var command = CreateCommand("task-runner");
        var console = new FakeInMemoryConsole();

        await command.ExecuteAsync(console);

        await _gitService.Received(1).InstallHuskyNetTaskRunnerHookAsync();
        await _gitService.DidNotReceive().InstallHuskyNetShellHookAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoHuskyAndModeSpecified_ThrowsCommandException()
    {
        _gitService.DetectHuskyKindAsync().Returns(HuskyInstallKind.None);
        var command = CreateCommand("shell");
        var console = new FakeInMemoryConsole();

        var ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains(ModeOnlyValidFragment, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoHusky_CallsInstallHookDirect()
    {
        _gitService.DetectHuskyKindAsync().Returns(HuskyInstallKind.None);
        var command = CreateCommand();
        var console = new FakeInMemoryConsole();

        await command.ExecuteAsync(console);

        await _gitService.Received(1).InstallHookDirectAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvalidModeValue_ThrowsCommandException()
    {
        var command = CreateCommand("invalid-mode");
        var console = new FakeInMemoryConsole();

        var ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains(InvalidModeFragment, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCreateConfigAndNoExistingConfig_CallsCreateRepoConfig()
    {
        _configService.CreateRepoConfigAsync().Returns("/repo/.gitmojirc.json");
        var command = CreateCommand(createConfig: true);
        var console = new FakeInMemoryConsole();

        await command.ExecuteAsync(console);

        await _configService.Received(1).CreateRepoConfigAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCreateConfigAndConfigAlreadyExists_Succeeds()
    {
        _configService.CreateRepoConfigAsync().Returns((string?)null);
        var command = CreateCommand(createConfig: true);
        var console = new FakeInMemoryConsole();

        await command.ExecuteAsync(console);

        await _configService.Received(1).CreateRepoConfigAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenInstallThrowsInvalidOperationException_WrapsAsCommandException()
    {
        _gitService.InstallHookDirectAsync()
            .Returns(Task.FromException(new InvalidOperationException("git hooks directory not found")));
        var command = CreateCommand();
        var console = new FakeInMemoryConsole();

        var ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains("git hooks directory not found", ex.Message);
    }
}