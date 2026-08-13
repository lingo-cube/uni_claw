using SkiaSharp;
using System.Text;
using UniClaw.Runtime.Adapters;
using UniClaw.Runtime.Adapters.Device;
using UniClaw.Runtime.Adapters.Operator;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario;
using Xunit;

namespace UniClaw.Runtime.Tests.Perception;

/// <summary>
/// PF-01 deterministic mechanism proofs.  These tests exercise only the
/// adapter-private process seam; a real ADB/emulator is deliberately not a
/// prerequisite for the ordinary test suite.
/// </summary>
public sealed class Pf01ConcreteAdbMechanismTests
{
    [Fact] public async Task PF01_01_ZeroOnlineDevices_FailsClosed() =>
        Assert.False((await Resolve("List of devices attached\n\n")).IsResolved);

    [Fact] public async Task PF01_02_OneOnlineDevice_SelectsIt() =>
        Assert.Equal("emulator-5554", (await Resolve(Devices("emulator-5554\tdevice"))).Serial);

    [Fact] public async Task PF01_03_MultipleOnlineDevices_FailsClosed() =>
        Assert.False((await Resolve(Devices("one\tdevice", "two\tdevice"))).IsResolved);

    [Fact] public async Task PF01_04_ExplicitEligibleSerial_SelectsExactDevice() =>
        Assert.Equal("two", (await Resolve(Devices("one\tdevice", "two\tdevice"), "two")).Serial);

    [Theory]
    [InlineData("missing\tdevice", "selected")]
    [InlineData("selected\toffline", "selected")]
    [InlineData("selected\tunauthorized", "selected")]
    public async Task PF01_05_06_ExplicitIneligibleSerial_FailsClosed(string device, string selected)
    {
        var resolution = await Resolve(Devices(device), selected);
        Assert.False(resolution.IsResolved);
        Assert.NotNull(resolution.FailureReason);
    }

    [Fact] public async Task PF01_07_MalformedListing_FailsClosed() =>
        Assert.False((await Resolve("not an adb listing")).IsResolved);

    [Fact] public async Task PF01_DuplicateConfiguredSerial_FailsClosedAsMalformed()
    {
        var result = await Resolve(Devices("same\tdevice", "same\tdevice"), "same");
        Assert.False(result.IsResolved);
    }

    [Fact] public async Task PF01_08_ExecutableUnavailable_FailsClosed()
    {
        var result = await new AdbDeviceResolver(
            new FakeRunner(new(false, false, null, [], "", "not found")), "missing-adb", null).ResolveAsync();
        Assert.False(result.IsResolved);
        Assert.Contains("unavailable", result.FailureReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task PF01_09_ScreenshotDecodesNonEmptyPng_AndScopesCommandToSerial()
    {
        var runner = new FakeRunner(Success(PngBytes()));
        var source = CreateScreenshot(runner, "device-A", "adb-test");
        var capture = await source.CaptureAsync(CancellationToken.None);
        using var screenshot = capture.ScreenshotData;
        Assert.True(capture.Width > 0 && capture.Height > 0);
        Assert.Equal(new[] { "-s", "device-A", "exec-out", "screencap", "-p" }, runner.LastArguments);
    }

    [Fact] public async Task PF01_10_12_13_ScreenshotFailures_NeverBecomeEmptySuccess()
    {
        var timeout = CreateScreenshot(new FakeRunner(new(true, true, null, [], "", null)), "s");
        await Assert.ThrowsAsync<TimeoutException>(() => timeout.CaptureAsync(CancellationToken.None));
        var failed = CreateScreenshot(new FakeRunner(new(true, false, 1, [], "bad", null)), "s");
        await Assert.ThrowsAsync<InvalidOperationException>(() => failed.CaptureAsync(CancellationToken.None));
        var empty = CreateScreenshot(new FakeRunner(Success([])), "s");
        await Assert.ThrowsAsync<InvalidOperationException>(() => empty.CaptureAsync(CancellationToken.None));

        var malformed = CreateScreenshot(new FakeRunner(Success([1, 2, 3])), "s");
        await Assert.ThrowsAsync<InvalidOperationException>(() => malformed.CaptureAsync(CancellationToken.None));
    }

    [Fact] public async Task PF01_11_ScreenshotCancellation_Propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var source = CreateScreenshot(new CancellingRunner(), "s");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.CaptureAsync(cts.Token));
    }

    [Fact] public async Task PF01_14_15_16_SupportedOperations_BuildExactCommandsAndDispatchOnly()
    {
        var runner = new FakeRunner(Success([]));
        var target = CreateDispatch(runner, "serial-1", "adb-test");
        var tap = await target.ExecuteAsync(new AdbOperation.Tap(10, 20), CancellationToken.None);
        Assert.Equal(ActionResultOutcome.Dispatched, tap.Outcome);
        Assert.Equal(new[] { "-s", "serial-1", "shell", "input", "tap", "10", "20" }, runner.LastArguments);
        Assert.Contains("unverified", tap.Info!, StringComparison.OrdinalIgnoreCase);

        await target.ExecuteAsync(new AdbOperation.Swipe(1, 2, 3, 4), CancellationToken.None);
        Assert.Equal(new[] { "-s", "serial-1", "shell", "input", "swipe", "1", "2", "3", "4" }, runner.LastArguments);
        await target.ExecuteAsync(new AdbOperation.Launch("com.android.settings"), CancellationToken.None);
        Assert.Equal(new[] { "-s", "serial-1", "shell", "monkey", "-p", "com.android.settings", "1" }, runner.LastArguments);
    }

    [Fact] public async Task PF01_17_UnsupportedDescriptor_IsRejectedWithoutProcessExecution()
    {
        var runner = new FakeRunner(Success([]));
        var target = CreateDispatch(runner, "s");
        var result = await target.ExecuteAsync(new AdbOperation.KeyEvent("HOME"), CancellationToken.None);
        Assert.Equal(ActionResultOutcome.Rejected, result.Outcome);
        Assert.Equal(0, runner.CallCount);
    }

    [Fact] public async Task PF01_18_19_DispatchTimeoutAndRejection_AreMechanismOutcomesOnly()
    {
        var timeout = CreateDispatch(new FakeRunner(new(true, true, null, [], "", null)), "s");
        Assert.Equal(ActionResultOutcome.TimedOut, (await timeout.ExecuteAsync(new AdbOperation.Tap(1, 1), CancellationToken.None)).Outcome);
        var rejected = CreateDispatch(new FakeRunner(new(true, false, 1, [], "adb says no", null)), "s");
        Assert.Equal(ActionResultOutcome.Rejected, (await rejected.ExecuteAsync(new AdbOperation.Tap(1, 1), CancellationToken.None)).Outcome);
    }

    [Fact] public async Task PF01_21_22_23_DeviceIdentity_IsFixedPerProviderAndParallelCalls()
    {
        var screenshotRunner = new FakeRunner(Success(PngBytes()));
        var dispatchRunner = new FakeRunner(Success([]));
        var screenshot = CreateScreenshot(screenshotRunner, "fixed-A");
        var dispatch = CreateDispatch(dispatchRunner, "fixed-B");
        var captures = await Task.WhenAll(Enumerable.Range(0, 3).Select(async _ =>
        {
            var capture = await screenshot.CaptureAsync(CancellationToken.None);
            using var image = capture.ScreenshotData;
            return capture.Width;
        }));
        Assert.All(captures, width => Assert.True(width > 0));
        await dispatch.ExecuteAsync(new AdbOperation.Tap(1, 1), CancellationToken.None);
        Assert.All(screenshotRunner.AllArguments, args => Assert.Equal("fixed-A", args[1]));
        Assert.All(dispatchRunner.AllArguments, args => Assert.Equal("fixed-B", args[1]));
    }

    [Fact] public async Task PF01_24_EachCaptureCallsProcessAgain_NoCache()
    {
        var runner = new FakeRunner(Success(PngBytes()));
        var source = CreateScreenshot(runner, "s");
        var one = await source.CaptureAsync(CancellationToken.None);
        var two = await source.CaptureAsync(CancellationToken.None);
        using var imageOne = one.ScreenshotData;
        using var imageTwo = two.ScreenshotData;
        Assert.Equal(2, runner.CallCount);
    }

    [Fact] public async Task PF01_ProcessRunner_TimeoutKillsShortLivedChildWithoutShellInterpolation()
    {
        var runner = new AdbProcessRunner();
        var started = DateTimeOffset.UtcNow;
        var result = await runner.RunAsync("/bin/sleep", ["5"], TimeSpan.FromMilliseconds(75), CancellationToken.None);
        Assert.True(result.Started);
        Assert.True(result.TimedOut);
        Assert.True(DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(2));
    }

    [Fact] public async Task PF01_ProcessRunner_CancellationPropagatesAndTerminatesChild()
    {
        var runner = new AdbProcessRunner();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(75));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync("/bin/sleep", ["5"], TimeSpan.FromSeconds(5), cancellation.Token));
    }

    [Fact] public async Task PF01_Preflight_ReportsStructuredReadinessWithoutDispatch()
    {
        var listing = Success(Encoding.UTF8.GetBytes(Devices("serial-1\tdevice")));
        var state = Success(Encoding.UTF8.GetBytes("device\n"));
        var dispatchChannel = Success([]);
        var screenshot = Success(PngBytes());
        var runner = new SequencedRunner(listing, state, dispatchChannel, screenshot);
        var resolver = new AdbDeviceResolver(runner, "adb-test", null);
        var preflight = await new AdbDevicePreflight(resolver).CheckAsync();
        Assert.True(preflight.IsReady);
        Assert.Equal("serial-1", preflight.Serial);
        Assert.Equal(4, runner.CallCount);
        Assert.Equal(new[] { "-s", "serial-1", "shell", "true" }, runner.AllArguments[2]);
        Assert.DoesNotContain(runner.AllArguments, arguments => arguments.Contains("input"));
    }

    [Fact] public async Task PF01_Preflight_DispatchProbeFailure_IsFalseAndNeverAnInputAction()
    {
        var listing = Success(Encoding.UTF8.GetBytes(Devices("serial-1\tdevice")));
        var state = Success(Encoding.UTF8.GetBytes("device\n"));
        var dispatchFailure = new AdbProcessResult(true, false, 1, [], "permission denied", null);
        var runner = new SequencedRunner(listing, state, dispatchFailure);
        var result = await new AdbDevicePreflight(new AdbDeviceResolver(runner, "adb-test", null)).CheckAsync();

        Assert.False(result.DispatchMechanismReady);
        Assert.False(result.IsReady);
        Assert.Contains("permission denied", result.FailureReason!, StringComparison.Ordinal);
        Assert.Equal(3, runner.CallCount);
        Assert.DoesNotContain(runner.AllArguments, arguments => arguments.Contains("input"));
    }

    [Fact] public void PF01_25_26_27_28_29_MechanicalAuthorityAndDependencyGuards()
    {
        var memberTypes = typeof(AdbScreenshotSource).GetMembers()
            .SelectMany(member => member switch
            {
                System.Reflection.MethodInfo method => method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType),
                System.Reflection.PropertyInfo property => [property.PropertyType],
                System.Reflection.FieldInfo field => [field.FieldType],
                _ => [],
            });
        Assert.DoesNotContain(memberTypes, type => type.Name.Contains("GoalEvidence", StringComparison.Ordinal));
        var root = TestRepositoryPaths.RepoPath();
        var runtimeProject = File.ReadAllText(Path.Combine(root, "src", "UniClaw.Runtime", "UniClaw.Runtime.csproj"));
        Assert.DoesNotContain("Runtime.Adapters", runtimeProject, StringComparison.Ordinal);
        Assert.False(Directory.EnumerateFiles(Path.Combine(root, "src"), "*ProviderRegistry*", SearchOption.AllDirectories).Any());
        var volatilePaths = new[] { "platforms/perception", "LocalVisionPerceptionSource.cs", "VisionServiceHost.cs" };
        var changed = RunGit(root, "diff", "--name-only").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.DoesNotContain(changed, path => path.Contains("Device/", StringComparison.Ordinal) && volatilePaths.Any(path.Contains));
    }

    private static async Task<AdbDeviceResolution> Resolve(string listing, string? configuredSerial = null) =>
        await new AdbDeviceResolver(new FakeRunner(Success(Encoding.UTF8.GetBytes(listing))), "adb-test", configuredSerial).ResolveAsync();

    private static string Devices(params string[] lines) => "List of devices attached\n" + string.Join("\n", lines) + "\n";
    private static AdbProcessResult Success(byte[] stdout) => new(true, false, 0, stdout, "", null);

    private static AdbScreenshotSource CreateScreenshot(IAdbProcessRunner runner, string serial, string executable = "adb") =>
        new(runner, serial, executable);

    private static AdbDispatchTarget CreateDispatch(IAdbProcessRunner runner, string serial, string executable = "adb") =>
        new(runner, serial, executable);

    private static byte[] PngBytes()
    {
        using var bitmap = new SKBitmap(2, 2);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static string RunGit(string root, params string[] args)
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = root, RedirectStandardOutput = true, UseShellExecute = false,
            Arguments = string.Join(' ', args),
        })!;
        return process.StandardOutput.ReadToEnd();
    }

    private sealed class FakeRunner(AdbProcessResult result) : IAdbProcessRunner
    {
        private readonly AdbProcessResult _result = result;
        public int CallCount { get; private set; }
        public string[] LastArguments { get; private set; } = [];
        public List<string[]> AllArguments { get; } = [];
        public Task<AdbProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
        {
            CallCount++;
            LastArguments = arguments.ToArray();
            AllArguments.Add(LastArguments);
            return Task.FromResult(_result);
        }
    }

    private sealed class CancellingRunner : IAdbProcessRunner
    {
        public Task<AdbProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromCanceled<AdbProcessResult>(cancellationToken);
    }

    private sealed class SequencedRunner(params AdbProcessResult[] results) : IAdbProcessRunner
    {
        private readonly Queue<AdbProcessResult> _results = new(results);
        public int CallCount { get; private set; }
        public List<string[]> AllArguments { get; } = [];
        public Task<AdbProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
        {
            CallCount++;
            AllArguments.Add(arguments.ToArray());
            return Task.FromResult(_results.Dequeue());
        }
    }
}
