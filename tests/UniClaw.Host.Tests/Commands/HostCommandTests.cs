using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.UniBrain;
using UniClaw.Device;
using UniClaw.Host.Artifacts;
using UniClaw.Host.Commands;
using UniClaw.Host.Safety;
using UniClaw.Host.Scenarios;
using Xunit;

namespace UniClaw.Host.Tests.Commands;

public sealed class HostCommandTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"uniclaw-host-{Guid.NewGuid():N}");

    [Fact]
    public async Task Doctor_ReportsReadinessWithoutSendingDeviceActions()
    {
        var runner = new FakeRunner(
            Result(stdout: "device\n"),
            Result(stdout: "1\n"),
            Result(stdout: "<hierarchy rotation=\"0\" />"));
        var doctor = new DeviceDoctor(
            runner,
            new FakeCapture([1, 2, 3]),
            _root,
            providerReady: true);

        var report = await doctor.InspectAsync();

        Assert.True(report.Ready);
        Assert.Equal(
            ["device", "boot", "screenshot", "uiautomator", "provider", "output"],
            report.Checks.Select(check => check.Name));
        Assert.Equal(3, runner.Requests.Count);
        Assert.DoesNotContain(
            runner.Requests.SelectMany(request => request.Arguments),
            argument => argument is "input" or "am" or "monkey");
    }

    [Fact]
    public async Task Analyze_EmitsPageAndTraceWithZeroDeviceActions()
    {
        var storage = new InMemoryTraceStorage();
        var options = Options("analyze");
        var analyzer = new PageAnalysisDeviceAnalyzer(
            new FakePageAnalyzer(Analysis()),
            new InMemoryTraceRecorder(storage),
            options);

        var report = await analyzer.AnalyzeAsync();

        Assert.Equal(0, report.DeviceActionsSent);
        Assert.Equal("Settings", report.Analysis.CurrentPath.Single());
        Assert.True(storage.CurrentSession?.IsCompleted);
        Assert.Single(storage.GetAICalls());
        Assert.Single(storage.GetExecutions());
        Assert.Equal(SpanType.PageAnalysis, storage.GetExecutions()[0].SpanType);
        Assert.True(File.Exists(
            Path.Combine(_root, $"{report.RunId}.analysis.json")));
    }

    [Fact]
    public async Task Application_ClassifiesSuccessPreparationRuntimeAndCancellation()
    {
        var successOutput = new StringWriter();
        var success = new HostApplication(
            new FakeFactory(
                new FakeDoctor(ready: true),
                new FakeDeviceAnalyzer()),
            successOutput,
            new StringWriter());
        Assert.Equal(
            HostExitCodes.Success,
            await success.RunAsync(["doctor", "--device", "emulator-5554"]));
        Assert.Contains("\"ready\": true", successOutput.ToString());

        var preparation = new HostApplication(
            new ThrowingFactory(new HostPreparationException("missing provider")),
            new StringWriter(),
            new StringWriter());
        Assert.Equal(
            HostExitCodes.PreparationFailure,
            await preparation.RunAsync(["analyze", "--device", "emulator-5554"]));

        var runtime = new HostApplication(
            new ThrowingFactory(new IOException("trace unavailable")),
            new StringWriter(),
            new StringWriter());
        Assert.Equal(
            HostExitCodes.RuntimeFailure,
            await runtime.RunAsync(["analyze", "--device", "emulator-5554"]));

        var cancelled = new HostApplication(
            new FakeFactory(
                new FakeDoctor(ready: true),
                new CancellingAnalyzer()),
            new StringWriter(),
            new StringWriter());
        Assert.Equal(
            HostExitCodes.Cancelled,
            await cancelled.RunAsync(["analyze", "--device", "emulator-5554"]));
    }

    [Fact]
    public async Task Application_RejectsMissingDeviceBeforeComposition()
    {
        var application = new HostApplication(
            new ThrowingFactory(new InvalidOperationException("must not compose")),
            new StringWriter(),
            new StringWriter());

        var exitCode = await application.RunAsync(["doctor"]);

        Assert.Equal(HostExitCodes.InvalidArguments, exitCode);
    }

    [Fact]
    public async Task Analyze_ClosesTraceWhenAnalysisFails()
    {
        var storage = new InMemoryTraceStorage();
        var analyzer = new PageAnalysisDeviceAnalyzer(
            new ThrowingPageAnalyzer(),
            new InMemoryTraceRecorder(storage),
            Options("analyze"));

        await Assert.ThrowsAsync<IOException>(() => analyzer.AnalyzeAsync());

        Assert.True(storage.CurrentSession?.IsCompleted);
        Assert.Single(storage.GetErrors());
    }

    [Fact]
    public async Task RunComposition_UsesOneGatedDeviceAndTraceBoundary()
    {
        var snapshot = new ScenarioCatalog().LoadSnapshot(
            Path.Combine(
                AppContext.BaseDirectory,
                "Scenarios",
                "locate-one-item.v1.json"));
        var assets = await new RunAssetStore().CreateAsync(
            _root,
            snapshot,
            new { plan = "pending" },
            new RunManifestInput(
                "composition-run",
                null,
                null,
                "revision",
                "emulator-5554",
                "AOSP API 35",
                "mock",
                "deterministic-settings-v1",
                "mode-a"));

        var services = new HostCompositionFactory().CreateRunServices(
            Options("run") with
            {
                ProviderId = "mock",
                Model = "deterministic-settings-v1",
            },
            snapshot,
            assets);

        Assert.Equal("emulator-5554", services.Adb.Serial);
        Assert.IsType<SafeActionExecutor>(services.ActionExecutor);
        Assert.IsType<SafeEntryActionDriver>(services.EntryActionDriver);
        Assert.IsType<AdbScreenStateProvider>(services.ScreenState);
        Assert.Empty(services.ActionExecutor.GetHistory());
        Assert.Same(assets, services.Assets);
    }

    private HostCommandOptions Options(string command) =>
        new(
            command,
            "emulator-5554",
            _root,
            "mock",
            "deterministic",
            "mode-a");

    private static PageAnalysis Analysis() =>
        new(
            Direction.Left,
            Direction.Left,
            CurrentPath: ["Settings"],
            Items: [],
            HasScroll: true,
            IsEndOfList: false);

    private static AdbCommandResult Result(string stdout = "") =>
        new(
            "emulator-5554",
            [],
            0,
            stdout,
            string.Empty,
            [],
            TimeSpan.FromMilliseconds(1),
            null);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class FakeRunner : IAdbCommandRunner
    {
        private readonly Queue<AdbCommandResult> _results;

        public FakeRunner(params AdbCommandResult[] results)
        {
            _results = new Queue<AdbCommandResult>(results);
        }

        public string Serial => "emulator-5554";

        public List<AdbCommandRequest> Requests { get; } = [];

        public Task<AdbCommandResult> RunAsync(
            AdbCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class FakeCapture(byte[] bytes) : IScreenCapture
    {
        public Task<byte[]> CaptureAsync(CancellationToken ct = default) =>
            Task.FromResult(bytes);
    }

    private sealed class FakePageAnalyzer(PageAnalysis analysis) : IPageAnalyzer
    {
        public Task<PageAnalysis?> AnalyzeCurrentPageAsync(
            CancellationToken ct = default) =>
            Task.FromResult<PageAnalysis?>(analysis);

        public Task<AppEntryPoint?> FindAppEntryAsync(
            string targetApp,
            CancellationToken ct = default) =>
            Task.FromResult<AppEntryPoint?>(null);

        public Task<PageTypeVerification> VerifyPageTypeAsync(
            PageAnalysis pageAnalysis,
            string expectedType,
            string? expectedPageName = null,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingPageAnalyzer : IPageAnalyzer
    {
        public Task<PageAnalysis?> AnalyzeCurrentPageAsync(
            CancellationToken ct = default) =>
            throw new IOException("provider failed");

        public Task<AppEntryPoint?> FindAppEntryAsync(
            string targetApp,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PageTypeVerification> VerifyPageTypeAsync(
            PageAnalysis pageAnalysis,
            string expectedType,
            string? expectedPageName = null,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeDoctor(bool ready) : IDeviceDoctor
    {
        public Task<DoctorReport> InspectAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                new DoctorReport(
                    "1",
                    "emulator-5554",
                    ready,
                    [],
                    DateTimeOffset.UtcNow));
    }

    private sealed class FakeDeviceAnalyzer : IDeviceAnalyzer
    {
        public Task<AnalyzeReport> AnalyzeAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                new AnalyzeReport(
                    "1",
                    "run",
                    "emulator-5554",
                    "mock",
                    "deterministic",
                    "mode-a",
                    Analysis(),
                    "trace/run/trace.jsonl",
                    0,
                    DateTimeOffset.UtcNow));
    }

    private sealed class CancellingAnalyzer : IDeviceAnalyzer
    {
        public Task<AnalyzeReport> AnalyzeAsync(
            CancellationToken cancellationToken = default) =>
            throw new OperationCanceledException();
    }

    private sealed class FakeFactory(
        IDeviceDoctor doctor,
        IDeviceAnalyzer analyzer) : IHostCommandFactory
    {
        public IDeviceDoctor CreateDoctor(HostCommandOptions options) => doctor;

        public IDeviceAnalyzer CreateAnalyzer(HostCommandOptions options) => analyzer;
    }

    private sealed class ThrowingFactory(Exception exception) : IHostCommandFactory
    {
        public IDeviceDoctor CreateDoctor(HostCommandOptions options) =>
            throw exception;

        public IDeviceAnalyzer CreateAnalyzer(HostCommandOptions options) =>
            throw exception;
    }
}
