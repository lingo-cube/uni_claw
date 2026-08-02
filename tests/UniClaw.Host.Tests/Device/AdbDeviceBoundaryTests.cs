using System.Collections.Immutable;
using UniClaw.Device;
using Xunit;

namespace UniClaw.Host.Tests.Device;

public sealed class AdbDeviceBoundaryTests
{
    [Fact]
    public async Task ScreenCapture_RoutesSelectedSerialAndReturnsNonEmptyPng()
    {
        var runner = new FakeAdbRunner("emulator-5556");
        runner.Enqueue(Success(binary: [1, 2, 3, 4]));
        var capture = new AdbScreenCapture(runner);

        var bytes = await capture.CaptureAsync();

        Assert.Equal([1, 2, 3, 4], bytes);
        Assert.Equal("emulator-5556", runner.Serial);
        Assert.Equal(
            ["exec-out", "screencap", "-p"],
            runner.Requests.Single().Arguments);
        Assert.True(runner.Requests.Single().CaptureBinaryOutput);
    }

    [Fact]
    public async Task ScreenCapture_EmptyOutputHasStructuredDiagnosticFailure()
    {
        var runner = new FakeAdbRunner();
        runner.Enqueue(Success(binary: []));

        var exception = await Assert.ThrowsAsync<AdbCommandException>(
            () => new AdbScreenCapture(runner).CaptureAsync());

        Assert.Equal("invalid_output", exception.Result.Failure?.Kind);
        Assert.Contains("no bytes", exception.Message);
    }

    [Fact]
    public async Task ScreenCapture_TimeoutRemainsClassified()
    {
        var runner = new FakeAdbRunner();
        runner.Enqueue(Failure("timeout", "timed out"));

        var exception = await Assert.ThrowsAsync<AdbCommandException>(
            () => new AdbScreenCapture(runner).CaptureAsync());

        Assert.Equal("timeout", exception.Result.Failure?.Kind);
    }

    [Fact]
    public async Task ScreenCapture_CallerCancellationPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var runner = new FakeAdbRunner();
        runner.Enqueue(Failure("cancelled", "cancelled"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new AdbScreenCapture(runner).CaptureAsync(cancellation.Token));
    }

    [Fact]
    public async Task ActionExecutor_FormsTapBackScrollAndLaunchArguments()
    {
        var runner = new FakeAdbRunner();
        runner.Enqueue(Success(stdout: "Physical size: 1080x1920"));
        runner.Enqueue(Success());
        runner.Enqueue(Success());
        runner.Enqueue(Success());
        runner.Enqueue(Success());
        var action = new AdbActionExecutor(runner);

        Assert.True(await action.TapAsync(0.5, 0.25));
        Assert.True(await action.PressBackAsync());
        Assert.True(await action.SwipeAsync(0.5, 0.8, 0.5, 0.2, 350));
        Assert.True(await action.LaunchPackageAsync("com.android.settings"));

        Assert.Equal(
            ["shell", "wm", "size"],
            runner.Requests[0].Arguments);
        Assert.Equal(
            [
                "shell", "input", "mouse", "-d", "0", "tap",
                "540", "480",
            ],
            runner.Requests[1].Arguments);
        Assert.Equal(
            ["shell", "input", "keyevent", "KEYCODE_BACK"],
            runner.Requests[2].Arguments);
        Assert.Equal(
            ["shell", "input", "swipe", "540", "1536", "540", "384", "350"],
            runner.Requests[3].Arguments);
        Assert.Equal(
            [
                "shell", "monkey", "-p", "com.android.settings",
                "-c", "android.intent.category.LAUNCHER", "1",
            ],
            runner.Requests[4].Arguments);
    }

    [Fact]
    public async Task ActionExecutor_DoesNotPersistInputTextSecret()
    {
        const string secret = "p@ss word";
        var runner = new FakeAdbRunner();
        runner.Enqueue(Success());
        var action = new AdbActionExecutor(runner);

        Assert.True(await action.InputTextAsync(secret));

        var request = runner.Requests.Single();
        Assert.Contains(3, request.SensitiveArgumentIndexes ?? []);
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

        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task ScreenState_AdbFailureIsNotEndOfList()
    {
        var runner = new FakeAdbRunner();
        runner.Enqueue(Failure("non_zero_exit", "device offline"));
        var provider = new AdbScreenStateProvider(runner);

        var result = await provider.RefreshAsync();

        Assert.Equal("adb_failure", result.Status);
        Assert.False(provider.HasScroll());
        Assert.False(provider.IsEndOfList());
        Assert.Equal("non_zero_exit", result.Failure?.Kind);
    }

    [Fact]
    public async Task ScreenState_XmlParseFailureIsDistinct()
    {
        var runner = new FakeAdbRunner();
        runner.Enqueue(Success());
        runner.Enqueue(Success(stdout: "<not-closed"));
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
        runner.Enqueue(Failure("non_zero_exit", "device offline"));
        var provider = new AdbScreenStateProvider(runner);

        var result = await provider.RefreshAsync();

        Assert.False(result.Succeeded);
        Assert.False(provider.IsUiAutomatorAvailable);

        // Once unavailable it stays unavailable even when a later dump succeeds.
        runner.Enqueue(Success());
        runner.Enqueue(Success(stdout: NoScrollXml));
        var later = await provider.RefreshAsync();
        Assert.True(later.Succeeded);
        Assert.False(provider.IsUiAutomatorAvailable);
    }

    [Fact(DisplayName = "AC5: 首次 RefreshAsync 成功 → UIA 保持可用")]
    public async Task ScreenState_SuccessKeepsUiAutomatorAvailable()
    {
        var runner = new FakeAdbRunner();
        runner.Enqueue(Success());
        runner.Enqueue(Success(stdout: NoScrollXml));
        var provider = new AdbScreenStateProvider(runner);

        var result = await provider.RefreshAsync();

        Assert.True(result.Succeeded);
        Assert.True(provider.IsUiAutomatorAvailable);
    }

    [Fact]
    public async Task ScreenState_TrueNoScrollIsSuccessfulAndDistinct()
    {
        var runner = new FakeAdbRunner();
        runner.Enqueue(Success());
        runner.Enqueue(Success(stdout: NoScrollXml));
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
        runner.Enqueue(Success());
        runner.Enqueue(Success(stdout: ScrollXml));
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
        runner.Enqueue(Success());
        runner.Enqueue(Success(stdout: ScrollXml.Replace("Wi-Fi", "Battery")));
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
        runner.Enqueue(Success());
        runner.Enqueue(Success());
        var driver = new AdbEntryActionDriver(runner);

        var success = await driver.ColdLaunchAsync("com.android.settings");

        Assert.True(success);
        Assert.Equal(
            ["shell", "am", "force-stop", "com.android.settings"],
            runner.Requests[0].Arguments);
        Assert.Equal(
            [
                "shell", "monkey", "-p", "com.android.settings",
                "-c", "android.intent.category.LAUNCHER", "1",
            ],
            runner.Requests[1].Arguments);
    }

    [Fact]
    public async Task EntryActionDriver_FastConditionChecksCurrentPackage()
    {
        var runner = new FakeAdbRunner();
        runner.Enqueue(Success(stdout: "mResumedActivity: com.android.settings/.Settings"));
        var driver = new AdbEntryActionDriver(runner);

        var success = await driver.CheckConditionAsync(
            new Dictionary<string, object>
            {
                ["package"] = "com.android.settings",
            });

        Assert.True(success);
        Assert.Equal(
            ["shell", "dumpsys", "activity", "activities"],
            runner.Requests.Single().Arguments);
    }

    private static AdbCommandResult Success(
        string stdout = "",
        byte[]? binary = null) =>
        new(
            "emulator-5554",
            ImmutableArray<string>.Empty,
            0,
            stdout,
            string.Empty,
            (binary ?? []).ToImmutableArray(),
            TimeSpan.FromMilliseconds(1),
            null);

    private static AdbCommandResult Failure(string kind, string message) =>
        new(
            "emulator-5554",
            ImmutableArray<string>.Empty,
            null,
            string.Empty,
            string.Empty,
            ImmutableArray<byte>.Empty,
            TimeSpan.FromMilliseconds(1),
            new AdbCommandFailure(kind, message));

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

    private sealed class FakeAdbRunner : IAdbCommandRunner
    {
        private readonly Queue<AdbCommandResult> _results = new();

        public string Serial { get; }

        public List<AdbCommandRequest> Requests { get; } = new();

        public FakeAdbRunner(string serial = "emulator-5554")
        {
            Serial = serial;
        }

        public void Enqueue(AdbCommandResult result) => _results.Enqueue(result);

        public Task<AdbCommandResult> RunAsync(
            AdbCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (_results.Count == 0)
                throw new InvalidOperationException("No fake ADB result was queued.");
            var result = _results.Dequeue();
            var sensitive = request.SensitiveArgumentIndexes
                            ?? ImmutableHashSet<int>.Empty;
            var redacted = request.Arguments
                .Select((argument, index) =>
                    sensitive.Contains(index) ? "[REDACTED]" : argument)
                .ToImmutableArray();
            return Task.FromResult(result with
            {
                Serial = Serial,
                Arguments = redacted,
            });
        }
    }
}
