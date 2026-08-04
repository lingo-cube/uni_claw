using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Host.Artifacts;
using UniClaw.Host.Commands;
using UniClaw.Host.Scenarios;
using Xunit;

namespace UniClaw.Host.Tests.Artifacts;

/// <summary>
/// Task 7.5 — run-metadata-enrichment: Purpose / TaskId / SystemInfo / MachineInfo are
/// optional on RunManifestInput and RunManifest (old manifests deserialize with nulls),
/// flow from UNICLAW_RUN_PURPOSE through CLI parsing, and are written into manifest.json.
/// </summary>
public sealed class ManifestMetadataTests
{
    [Fact]
    public void RunManifestInput_NewFields_HaveDefaultNull()
    {
        var input = new RunManifestInput(
            "run-1", null, null, "revision",
            "emulator-5554", null, "mock", null, "mode-a");

        Assert.Null(input.Purpose);
        Assert.Null(input.TaskId);
        Assert.Null(input.SystemInfo);
        Assert.Null(input.MachineInfo);
    }

    [Fact]
    public void RunManifest_CanBeConstructedWithNewFields()
    {
        var systemInfo = new RunSystemInfo("35", "15", "fingerprint", "VanillaIceCream", "arm64-v8a");
        var machineInfo = new RunMachineInfo("Darwin 24.6.0", "Arm64", ".NET 10.0.0", "host-1");
        var manifest = new RunManifest(
            "1",
            "run-1",
            null,
            null,
            "locate-one-item",
            "scenario-hash",
            "settings-read-only-v1",
            "1.0.0",
            "policy-hash",
            "revision",
            "emulator-5554",
            null,
            "com.android.settings",
            "mock",
            null,
            "mode-a",
            ImmutableDictionary<string, string>.Empty,
            DateTimeOffset.UtcNow,
            ImmutableDictionary<string, string>.Empty,
            Purpose: "regression-trace",
            TaskId: "task-42",
            SystemInfo: systemInfo,
            MachineInfo: machineInfo);

        Assert.Equal("regression-trace", manifest.Purpose);
        Assert.Equal("task-42", manifest.TaskId);
        Assert.Same(systemInfo, manifest.SystemInfo);
        Assert.Same(machineInfo, manifest.MachineInfo);
    }

    [Fact]
    public async Task EnvVar_UniclawRunPurpose_FlowsIntoCliParsing()
    {
        var original = Environment.GetEnvironmentVariable("UNICLAW_RUN_PURPOSE");
        try
        {
            Environment.SetEnvironmentVariable("UNICLAW_RUN_PURPOSE", "test-purpose");
            var factory = new RecordingFactory();
            var app = new HostApplication(factory, new StringWriter(), new StringWriter());

            var exitCode = await app.RunAsync(
                new[] { "doctor", "--device", "emulator-5554" });

            Assert.Equal(HostExitCodes.Success, exitCode);
            Assert.NotNull(factory.LastOptions);
            Assert.Equal("test-purpose", factory.LastOptions.Purpose);
        }
        finally
        {
            Environment.SetEnvironmentVariable("UNICLAW_RUN_PURPOSE", original);
        }
    }

    [Fact]
    public async Task RunAssetStore_WritesNewFieldsIntoManifestJson()
    {
        var root = Path.Combine(Path.GetTempPath(), $"uniclaw-manifest-meta-{Guid.NewGuid():N}");
        try
        {
            var snapshot = new ScenarioCatalog().LoadSnapshot(
                Path.Combine(AppContext.BaseDirectory, "Scenarios", "locate-one-item.v1.json"));
            var input = new RunManifestInput(
                "meta-run", null, null, "revision",
                "emulator-5554", null, "mock", null, "mode-a",
                Purpose: "regression-trace",
                TaskId: "task-42",
                SystemInfo: new RunSystemInfo("35", "15", "fingerprint", "VanillaIceCream", "arm64-v8a"),
                MachineInfo: new RunMachineInfo("Darwin 24.6.0", "Arm64", ".NET 10.0.0", "host-1"));

            var session = await new RunAssetStore().CreateAsync(
                root, snapshot, new { }, input);

            using var document = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(session.RunDirectory, "manifest.json")));
            var manifest = document.RootElement;
            Assert.Equal("regression-trace", manifest.GetProperty("purpose").GetString());
            Assert.Equal("task-42", manifest.GetProperty("taskId").GetString());
            Assert.Equal(
                "35",
                manifest.GetProperty("systemInfo").GetProperty("sdkLevel").GetString());
            Assert.Equal(
                "Darwin 24.6.0",
                manifest.GetProperty("machineInfo").GetProperty("os").GetString());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Captures the parsed options instead of touching ADB.</summary>
    private sealed class RecordingFactory : IHostCommandFactory
    {
        public HostCommandOptions? LastOptions { get; private set; }

        public IDeviceDoctor CreateDoctor(HostCommandOptions options)
        {
            LastOptions = options;
            return new FakeDoctor();
        }

        public IDeviceAnalyzer CreateAnalyzer(HostCommandOptions options)
        {
            LastOptions = options;
            return new FakeAnalyzer();
        }
    }

    private sealed class FakeDoctor : IDeviceDoctor
    {
        public Task<DoctorReport> InspectAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new DoctorReport(
                "1", "emulator-5554", Ready: true, [], DateTimeOffset.UtcNow));
    }

    private sealed class FakeAnalyzer : IDeviceAnalyzer
    {
        public Task<AnalyzeReport> AnalyzeAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("analyze not exercised by these tests");
    }
}
