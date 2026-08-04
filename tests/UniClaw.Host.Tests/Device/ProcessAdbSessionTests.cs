using UniClaw.Device;
using Xunit;

namespace UniClaw.Host.Tests.Device;

public sealed class ProcessAdbSessionTests
{
    [Fact]
    public void Constructor_NullRunner_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ProcessAdbSession((AdbCommandRunner)null!));
    }

    [Fact]
    public void Constructor_ValidOptions_SerialMatches()
    {
        var options = new AdbCommandRunnerOptions("emulator-5554");

        var session = new ProcessAdbSession(options);

        Assert.Equal("emulator-5554", session.Serial);
    }

    [Fact]
    public async Task ExecuteShellAsync_EmptyCommand_ThrowsArgumentException()
    {
        var session = new ProcessAdbSession(
            new AdbCommandRunnerOptions("emulator-5554"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => session.ExecuteShellAsync(""));
    }

    [Fact]
    public async Task ExecuteShellAsync_WhitespaceCommand_ThrowsArgumentException()
    {
        var session = new ProcessAdbSession(
            new AdbCommandRunnerOptions("emulator-5554"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => session.ExecuteShellAsync("  "));
    }

    [Fact]
    public void DisposeAsync_CompletesSuccessfully()
    {
        var session = new ProcessAdbSession(
            new AdbCommandRunnerOptions("emulator-5554"));

        var task = session.DisposeAsync();

        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public void Serial_MatchesUnderlyingRunner()
    {
        var session = new ProcessAdbSession(
            new AdbCommandRunnerOptions("device-123"));

        Assert.Equal("device-123", session.Serial);
    }
}
