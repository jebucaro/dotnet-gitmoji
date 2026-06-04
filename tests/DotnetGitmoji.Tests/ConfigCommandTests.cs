using CliFx;
using CliFx.Infrastructure;
using DotnetGitmoji.Commands;
using DotnetGitmoji.Services;
using NSubstitute;

namespace DotnetGitmoji.Tests;

public class ConfigCommandTests
{
    private readonly IConfigurationService _configService = Substitute.For<IConfigurationService>();

    private ConfigCommand CreateCommand(bool global = false, bool local = false)
    {
        return new ConfigCommand(_configService) { Global = global, Local = local };
    }

    [Fact]
    public async Task ExecuteAsync_WhenBothGlobalAndLocalSpecified_ThrowsCommandException()
    {
        var command = CreateCommand(true, true);
        var console = new FakeInMemoryConsole();

        var ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains("Cannot specify both", ex.Message, StringComparison.OrdinalIgnoreCase);
        await _configService.DidNotReceive().LoadAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenLoadConfigThrowsIoException_ThrowsFriendlyCommandException()
    {
        _configService.LoadAsync()
            .Returns(Task.FromException<Models.ToolConfiguration>(
                new IOException("config file locked")));

        var command = CreateCommand();
        var console = new FakeInMemoryConsole();

        var ex = await Assert.ThrowsAsync<CommandException>(() => command.ExecuteAsync(console).AsTask());

        Assert.Contains("Failed to load configuration", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("config file locked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}