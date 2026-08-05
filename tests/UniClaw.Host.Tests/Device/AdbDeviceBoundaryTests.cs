using UniClaw.Core.UniBrain;
using UniClaw.Device;
using Xunit;

namespace UniClaw.Host.Tests.Device;

public sealed class AdbDeviceBoundaryTests
{
    [Fact]
    public async Task ScreenCapture_RoutesSelectedSerialAndReturnsNonEmptyPng()
    {
        var runner = new FakeAdbRunner("emulator-5556");
        runner.EnqueueScreenshot(ScreenshotBytes(1, 2, 3, 4));
        var capture = new AdbScreenCapture(runner);

        var bytes = await capture.CaptureAsync();

        Assert.Equal([1, 2, 3, 4], bytes);
        Assert.Equal("emulator-5556", runner.Serial);
        Assert.Equal(1, runner.ScreenshotRequestCount);
    }

    [Fact]
    public async Task ScreenCapture_TimeoutRemainsClassified()
    {
        var runner = new FakeAdbRunner();
        runner.EnqueueScreenshotFailure("timed out");

        var exception = await Assert.ThrowsAsync<AdbCommandException>(
            () => new AdbScreenCapture(runner).CaptureAsync());

        Assert.Equal("timed out", exception.Result.StandardError);
        Assert.Contains("timed out", exception.Message);
    }

    [Fact]
    public async Task ScreenCapture_CallerCancellationPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var runner = new FakeAdbRunner();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new AdbScreenCapture(runner).CaptureAsync(cancellation.Token));
    }

    [Fact]
    public async Task ActionExecutor_FormsTapBackScrollAndLaunchArguments()
    {
        var runner = new FakeAdbRunner();
        runner.EnqueueShell(ShellSuccess(stdout: "Physical size: 1080x1920"));
        runner.EnqueueShell(ShellSuccess());
        runner.EnqueueShell(ShellSuccess());
        runner.EnqueueShell(ShellSuccess());
        runner.EnqueueShell(ShellSuccess());
        var action = new AdbActionExecutor(runner);

        Assert.True(await action.TapAsync(0.5, 0.25));
        Assert.True(await action.PressBackAsync());
        Assert.True(await action.SwipeAsync(0.5, 0.8, 0.5, 0.2, 350));
        Assert.True(await action.LaunchPackageAsync("com.android.settings"));

        Assert.Equal(
            [
                "wm size",
                "input mouse -d 0 tap 540 480",
                "input keyevent KEYCODE_BACK",
                "input swipe 540 1536 540 384 350",
                "monkey -p com.android.settings -c android.intent.category.LAUNCHER 1",
            ],
            runner.Commands);
    }

    [Fact]
    public async Task ActionExecutor_DoesNotPersistInputTextSecret()
    {
        const string secret = "p@ss word";
        var runner = new FakeAdbRunner();
        runner.EnqueueShell(ShellSuccess());
        var action = new AdbActionExecutor(runner);

        Assert.True(await action.InputTextAsync(secret));

        Assert.Equal("input text p@ss%sword", runner.Commands.Single());
        Assert.DoesNotContain(
            secret,
            string.Join("|", action.GetHistory().SelectMany(item => item.Parameters.Values)));
    }

    [Fact]
    public async Task ActionExecutor_RejectsPackageInjectionBeforeRunner()
    {
        var runner = new FakeAdbRunner();
        var action = new AdbActionExecutor(runner);

        await Assert.ThrowsAsync<ArgumentException>(
            () => action.LaunchPackageAsync("com.android.settings;rm"));

        Assert.Empty(runner.Commands);
    }

    [Fact]
    public void AdbCommandRequest_RetainsTimeoutAndRedactionMetadata()
    {
        var timeout = TimeSpan.FromSeconds(7);
        var request = AdbCommandRequest.Create(
            ["shell", "input", "text", "secret"],
            timeout,
            sensitiveArgumentIndexes: [3]);

        Assert.Equal(timeout, request.Timeout);
        Assert.Contains(3, request.SensitiveArgumentIndexes ?? []);
        Assert.False(request.CaptureBinaryOutput);
    }

    [Fact]
    public async Task EntryActionDriver_ColdLaunchExecutesRealStopAndLaunchCommands()
    {
        var runner = new FakeAdbRunner();
        runner.EnqueueShell(ShellSuccess());
        runner.EnqueueShell(ShellSuccess());
        var driver = new AdbEntryActionDriver(runner);

        var success = await driver.ColdLaunchAsync("com.android.settings");

        Assert.True(success);
        Assert.Equal(
            [
                "am force-stop com.android.settings",
                "monkey -p com.android.settings -c android.intent.category.LAUNCHER 1",
            ],
            runner.Commands);
    }

    [Fact]
    public async Task EntryActionDriver_FastConditionChecksCurrentPackage()
    {
        var runner = new FakeAdbRunner();
        runner.EnqueueShell(ShellSuccess(stdout: "mResumedActivity: com.android.settings/.Settings"));
        var driver = new AdbEntryActionDriver(runner);

        var success = await driver.CheckConditionAsync(
            new Dictionary<string, object>
            {
                ["package"] = "com.android.settings",
            });

        Assert.True(success);
        Assert.Equal(
            ["dumpsys activity activities"],
            runner.Commands);
    }

    [Fact]
    public async Task EntryActionDriver_TextConditionUnsupportedAfterUiaRemoval()
    {
        // UIA hierarchy conditions ("text") were removed with the UIA pipeline
        // (delete-uia): the condition fails closed without a device query.
        var runner = new FakeAdbRunner();
        var driver = new AdbEntryActionDriver(runner);

        var success = await driver.CheckConditionAsync(
            new Dictionary<string, object>
            {
                ["text"] = "Settings",
            });

        Assert.False(success);
        Assert.Empty(runner.Commands);
    }

    private static ShellResult ShellSuccess(
        string stdout = "",
        string stderr = "") =>
        new(true, stdout, stderr);

    private static ShellResult ShellFailure(string stderr = "command failed") =>
        new(false, string.Empty, stderr);

    private static byte[] ScreenshotBytes(params byte[] bytes) => bytes;

    private sealed class FakeAdbRunner : IAdbSession
    {
        private readonly Queue<byte[]> _screenshots = new();
        private readonly Queue<ShellResult> _shellResults = new();
        private readonly Queue<AdbCommandException> _screenshotFailures = new();

        public string Serial { get; }

        public List<string> Commands { get; } = new();

        public int ScreenshotRequestCount { get; private set; }

        public FakeAdbRunner(string serial = "emulator-5554")
        {
            Serial = serial;
        }

        public void EnqueueScreenshot(byte[] bytes) => _screenshots.Enqueue(bytes);

        public void EnqueueScreenshotFailure(string stderr) =>
            _screenshotFailures.Enqueue(
                new AdbCommandException(
                    "ADB screenshot capture",
                    ShellFailure(stderr)));

        public void EnqueueShell(ShellResult result) => _shellResults.Enqueue(result);

        public Task<byte[]> CaptureScreenshotAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            ScreenshotRequestCount++;
            if (_screenshotFailures.Count > 0)
                throw _screenshotFailures.Dequeue();
            if (_screenshots.Count == 0)
                throw new InvalidOperationException("No fake screenshot bytes were queued.");
            return Task.FromResult(_screenshots.Dequeue());
        }

        public Task<RawScreenBuffer> CaptureRawScreenBufferAsync(CancellationToken ct = default)
            => throw new NotSupportedException("Raw capture not supported in test fake");

        public Task<ShellResult> ExecuteShellAsync(
            string command,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Commands.Add(command);
            if (_shellResults.Count == 0)
                throw new InvalidOperationException("No fake shell result was queued.");
            return Task.FromResult(_shellResults.Dequeue());
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
