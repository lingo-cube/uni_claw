using UniClaw.Device;
using UniClaw.Host.Tests.Integration;
using Xunit;

namespace UniClaw.Host.Tests.Device;

/// <summary>
/// ADB session 集成测试（emulator-gated，IntegrationFact）。
/// 验证 AdvancedSharpAdbSession 与 ProcessAdbSession 的行为等价性。
/// 运行前启动设备：scripts/android-emulator.sh start；可设 UNICLAW_ADB_SERIAL 指定串行。
/// 运行：
/// <code>
/// UNICLAW_INTEGRATION_SCOPES=adb-session dotnet test tests/UniClaw.Host.Tests --filter "IntegrationScope=adb-session"
/// </code>
/// </summary>
[Trait("Category", "Integration")]
public sealed class AdbSessionIntegrationTests
{
    [Trait("IntegrationScope", IntegrationTestScopes.AdbSession)]
    [IntegrationFact(IntegrationTestScopes.AdbSession)]
    public async Task CaptureScreenshot_ReturnsNonEmptyPng()
    {
        var serial = await AdbTestContext.ResolveSerialAsync();
        await using var session =new AdvancedSharpAdbSession(serial);

        var bytes = await session.CaptureScreenshotAsync();

        Assert.NotEmpty(bytes);
        // PNG magic bytes
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
    }

    [Trait("IntegrationScope", IntegrationTestScopes.AdbSession)]
    [IntegrationFact(IntegrationTestScopes.AdbSession)]
    public async Task ExecuteShell_Echo_ReturnsOutput()
    {
        var serial = await AdbTestContext.ResolveSerialAsync();
        await using var session =new AdvancedSharpAdbSession(serial);

        var result = await session.ExecuteShellAsync("echo hello");

        Assert.True(result.Success);
        Assert.Contains("hello", result.StandardOutput);
    }

    [Trait("IntegrationScope", IntegrationTestScopes.AdbSession)]
    [IntegrationFact(IntegrationTestScopes.AdbSession)]
    public async Task SelfHealing_AfterKillServer_AutoRecovers()
    {
        var serial = await AdbTestContext.ResolveSerialAsync();
        // Ensure ADB server is running first
        await using var session =new AdvancedSharpAdbSession(serial);
        await session.ExecuteShellAsync("echo pre-check");

        // Kill the server externally
        await RunAdbCommandAsync("kill-server");
        // Allow time for server teardown
        await Task.Delay(500);

        // Next command should auto-recover via 3-tier self-healing
        var result = await session.ExecuteShellAsync("echo post-recovery");
        Assert.True(result.Success);
    }

    [Trait("IntegrationScope", IntegrationTestScopes.AdbSession)]
    [IntegrationFact(IntegrationTestScopes.AdbSession)]
    public async Task ProcessAdbSession_And_AdvancedSharpAdbSession_ProduceSameOutput()
    {
        var serial = await AdbTestContext.ResolveSerialAsync();
        await using var processSession = new ProcessAdbSession(
            new AdbCommandRunnerOptions(serial));
        await using var sharpSession = new AdvancedSharpAdbSession(serial);

        var processResult = await processSession.ExecuteShellAsync("echo compare-test");
        var sharpResult = await sharpSession.ExecuteShellAsync("echo compare-test");

        Assert.True(processResult.Success);
        Assert.True(sharpResult.Success);
        Assert.Equal(
            processResult.StandardOutput.Trim(),
            sharpResult.StandardOutput.Trim());
    }

    private static async Task RunAdbCommandAsync(string arguments)
    {
        var adbPath = Environment.GetEnvironmentVariable("UNICLAW_ADB_PATH") ?? "adb";
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = adbPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.Start();
        await process.WaitForExitAsync();
    }
}
