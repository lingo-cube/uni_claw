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
    public async Task ScreenState_AdbFailureIsNotEndOfList()
    {
        var runner = new FakeAdbRunner();
        runner.EnqueueHierarchyFailure("device offline");
        var provider = new AdbScreenStateProvider(runner);

        var result = await provider.RefreshAsync();

        Assert.Equal("adb_failure", result.Status);
        Assert.False(provider.HasScroll());
        Assert.False(provider.IsEndOfList());
        Assert.Equal("non_zero_exit", result.Failure?.Kind);
        Assert.Contains("device offline", result.Failure?.Message);
    }

    [Fact]
    public async Task ScreenState_XmlParseFailureIsDistinct()
    {
        var runner = new FakeAdbRunner();
        runner.EnqueueHierarchy("<not-closed");
        var provider = new AdbScreenStateProvider(runner);

        var result = await provider.RefreshAsync();

        Assert.Equal("xml_parse_failure", result.Status);
        Assert.False(result.IsEndOfList);
        Assert.Equal("xml_parse_failure", result.Failure?.Kind);
    }

    [Fact(DisplayName = "AC5: 首次 RefreshAsync 失败 → UIA 标记为不可用（会话内保持）")]
    public async Task ScreenState_FirstFailureMarksUiAutomatorUnavailable()
    {
        var runner = new FakeAdbRunner();
        runner.EnqueueHierarchyFailure("device offline");
        var provider = new AdbScreenStateProvider(runner);

        var result = await provider.RefreshAsync();

        Assert.False(result.Succeeded);
        Assert.False(provider.IsUiAutomatorAvailable);

        // Once unavailable it stays unavailable even when a later dump succeeds.
        runner.EnqueueHierarchy(NoScrollXml);
        var later = await provider.RefreshAsync();
        Assert.True(later.Succeeded);
        Assert.False(provider.IsUiAutomatorAvailable);
    }

    [Fact(DisplayName = "AC5: 首次 RefreshAsync 成功 → UIA 保持可用")]
    public async Task ScreenState_SuccessKeepsUiAutomatorAvailable()
    {
        var runner = new FakeAdbRunner();
        runner.EnqueueHierarchy(NoScrollXml);
        var provider = new AdbScreenStateProvider(runner);

        var result = await provider.RefreshAsync();

        Assert.True(result.Succeeded);
        Assert.True(provider.IsUiAutomatorAvailable);
    }

    [Fact]
    public async Task ScreenState_TrueNoScrollIsSuccessfulAndDistinct()
    {
        var runner = new FakeAdbRunner();
        runner.EnqueueHierarchy(NoScrollXml);
        var provider = new AdbScreenStateProvider(runner);

        var result = await provider.RefreshAsync();

        Assert.Equal("no_scroll", result.Status);
        Assert.True(result.Succeeded);
        Assert.False(result.HasScroll);
        Assert.True(result.IsEndOfList);
    }

    [Fact]
    public async Task ScreenState_UnchangedScrollableHierarchyProvesEndAfterScroll()
    {
        var runner = new FakeAdbRunner();
        runner.EnqueueHierarchy(ScrollXml);
        var provider = new AdbScreenStateProvider(runner);

        var result = await provider.RefreshAsync(
            previousHierarchyXml: ScrollXml,
            afterScroll: true);

        Assert.Equal("verified_end_of_list", result.Status);
        Assert.True(result.HasScroll);
        Assert.True(result.IsEndOfList);
        // Progress 不在 ScreenStateResult (决策 2026-07-30) —— 由锁定的 GetScrollProgress() 拥有。
        // verified_end_of_list 时 progress 应为 1。
        Assert.Equal(1, provider.GetScrollProgress());
    }

    [Fact]
    public async Task ScreenState_ChangedHierarchyDoesNotClaimEnd()
    {
        var runner = new FakeAdbRunner();
        runner.EnqueueHierarchy(ScrollXml.Replace("Wi-Fi", "Battery"));
        var provider = new AdbScreenStateProvider(runner);

        var result = await provider.RefreshAsync(
            previousHierarchyXml: ScrollXml,
            afterScroll: true);

        Assert.Equal("scrollable", result.Status);
        Assert.True(result.HasScroll);
        Assert.False(result.IsEndOfList);
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

    private static ShellResult ShellSuccess(
        string stdout = "",
        string stderr = "") =>
        new(true, stdout, stderr);

    private static ShellResult ShellFailure(string stderr = "command failed") =>
        new(false, string.Empty, stderr);

    private static byte[] ScreenshotBytes(params byte[] bytes) => bytes;

    private const string NoScrollXml =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <hierarchy rotation="0">
          <node class="android.widget.FrameLayout" text="Settings" scrollable="false" />
        </hierarchy>
        """;

    private const string ScrollXml =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <hierarchy rotation="0">
          <node class="android.widget.ScrollView" scrollable="true">
            <node class="android.widget.TextView" text="Wi-Fi" resource-id="android:id/title" />
          </node>
        </hierarchy>
        """;

    private sealed class FakeAdbRunner : IAdbSession
    {
        private readonly Queue<byte[]> _screenshots = new();
        private readonly Queue<ShellResult> _shellResults = new();
        private readonly Queue<string> _hierarchies = new();
        private readonly Queue<AdbCommandException> _screenshotFailures = new();
        private readonly Queue<AdbCommandException> _hierarchyFailures = new();

        public string Serial { get; }

        public List<string> Commands { get; } = new();

        public int ScreenshotRequestCount { get; private set; }

        public int HierarchyRequestCount { get; private set; }

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

        public void EnqueueHierarchy(string xml) => _hierarchies.Enqueue(xml);

        public void EnqueueHierarchyFailure(string stderr) =>
            _hierarchyFailures.Enqueue(
                new AdbCommandException("UI dump", ShellFailure(stderr)));

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

        public Task<string> DumpUiHierarchyAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            HierarchyRequestCount++;
            if (_hierarchyFailures.Count > 0)
                throw _hierarchyFailures.Dequeue();
            if (_hierarchies.Count == 0)
                throw new InvalidOperationException("No fake hierarchy XML was queued.");
            return Task.FromResult(_hierarchies.Dequeue());
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
