using UniClaw.Core.Observability;
using UniClaw.Core.UniBrain;
using UniClaw.Device;
using UniClaw.Host.Commands;
using Xunit;

namespace UniClaw.Host.Tests.Commands;

/// <summary>
/// M6 — doctor probe records diagnostics via ITraceRecorder (acceptance 8.24):
/// session + per-check executions are trace-correlated; no parallel diagnostic
/// output format is written.
/// </summary>
public sealed class DoctorTraceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"uniclaw-doctor-trace-{Guid.NewGuid():N}");

    [Fact]
    public async Task Doctor_RecordsTraceCorrelatedSessionAndChecks()
    {
        var storage = new InMemoryTraceStorage();
        var doctor = new DeviceDoctor(
            new FakeRunner(
                [Shell("device\n"), Shell("1\n")],
                ["<hierarchy rotation=\"0\" />"]),
            new FakeCapture([1, 2, 3]),
            _root,
            providerReady: true,
            new InMemoryTraceRecorder(storage));

        var report = await doctor.InspectAsync();

        Assert.True(report.Ready);
        var session = storage.CurrentSession;
        Assert.NotNull(session);
        Assert.True(session.IsCompleted);
        Assert.Equal("doctor", session.Metadata?["command"]);
        Assert.Equal("emulator-5554", session.Metadata?["deviceSerial"]);

        var executions = storage.GetExecutions();
        Assert.Equal(
            ["device", "boot", "screenshot", "uiautomator", "provider", "output", "doctor"],
            executions.Select(execution => execution.Action));
        // Every record is correlated to the same trace session.
        Assert.All(
            executions,
            execution => Assert.Equal(session.TraceId, execution.Context?.TraceId));
        Assert.All(
            executions.Take(6),
            execution => Assert.Equal("ready", execution.Status));
        Assert.Equal("ready", executions[^1].Status);
    }

    [Fact]
    public async Task Doctor_RecordsFailedCheckStatusOnTrace()
    {
        var storage = new InMemoryTraceStorage();
        var doctor = new DeviceDoctor(
            new FakeRunner(
                [Shell("device\n"), Shell("0\n")],
                ["<hierarchy rotation=\"0\" />"]),
            new FakeCapture([1, 2, 3]),
            _root,
            providerReady: true,
            new InMemoryTraceRecorder(storage));

        var report = await doctor.InspectAsync();

        // A failed check is not an exception — the status reflects it on trace.
        Assert.False(report.Ready);
        var executions = storage.GetExecutions();
        Assert.Equal("verification_failed", executions[1].Status);
        Assert.Equal("failed", executions[^1].Status);
    }

    [Fact]
    public async Task Doctor_WritesTraceOnlyUnderOutputRootTrace()
    {
        var doctor = new DeviceDoctor(
            new FakeRunner(
                [Shell("device\n"), Shell("1\n")],
                ["<hierarchy rotation=\"0\" />"]),
            new FakeCapture([1, 2, 3]),
            _root,
            providerReady: true,
            new InMemoryTraceRecorder(
                new FileTraceStorage(
                    new PhysicalFileProvider(),
                    Path.Combine(_root, "trace"))));

        var report = await doctor.InspectAsync();

        Assert.True(report.Ready);
        // The output root holds exactly one thing: the trace directory.
        // No parallel diagnostic output format/file (acceptance 8.24).
        Assert.Equal(
            new[] { "trace" },
            Directory.GetDirectories(_root).Select(Path.GetFileName));
        Assert.Empty(Directory.GetFiles(_root));
        var runDirs = Directory.GetDirectories(Path.Combine(_root, "trace"));
        Assert.Single(runDirs);
        Assert.True(File.Exists(Path.Combine(runDirs[0], "session.json")));
        var traceLines = File.ReadAllLines(Path.Combine(runDirs[0], "trace.jsonl"));
        Assert.True(
            traceLines.Length >= 6,
            $"expected one JSONL line per check, got {traceLines.Length}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static ShellResult Shell(string stdout = "") =>
        new(Success: true, StandardOutput: stdout, StandardError: string.Empty);

    private sealed class FakeRunner : IAdbSession
    {
        private readonly Queue<ShellResult> _shells;
        private readonly Queue<string> _hierarchies;

        public FakeRunner(
            IEnumerable<ShellResult> shells,
            IEnumerable<string> hierarchies)
        {
            _shells = new Queue<ShellResult>(shells);
            _hierarchies = new Queue<string>(hierarchies);
        }

        public string Serial => "emulator-5554";

        public Task<byte[]> CaptureScreenshotAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("ADB must not be used by fake runner.");

        public Task<ShellResult> ExecuteShellAsync(
            string command,
            CancellationToken ct = default) =>
            Task.FromResult(_shells.Dequeue());

        public Task<string> DumpUiHierarchyAsync(CancellationToken ct = default) =>
            Task.FromResult(_hierarchies.Dequeue());

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeCapture(byte[] bytes) : IScreenCapture
    {
        public Task<byte[]> CaptureAsync(CancellationToken ct = default) =>
            Task.FromResult(bytes);
    }
}
