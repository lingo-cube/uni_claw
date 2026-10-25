using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace UniClaw.Host.Dsh.Tests;

public sealed partial class RuntimeToolHostProcedureTests
{
    private const string AssetChange = "DEV-ASSET-TEST";

    private sealed class ManagedFixture : IDisposable
    {
        public Fixture Input { get; }
        public string Root => Path.GetDirectoryName(Input.RunsRoot)!;
        public string Repo => Path.Combine(Root, "registry");
        public string Config => Path.Combine(Root, "layout.json");
        public string Namespace { get; } = "diagnosis-fixture-" + Guid.NewGuid().ToString("N");
        public string Index => Path.Combine(Repo, "changes", AssetChange, "assets.json");
        public RuntimeToolHost.DevelopmentAssetRequest Request => new(AssetChange, Config);

        public ManagedFixture(bool signals = false, bool badExit = false)
        {
            Input = CreateFixture(exitAdapter: signals ? Path.Combine(RuntimeToolHostTests.RepoRoot(), "tools", "evolution-signal.py") : null);
            if (badExit)
            {
                var script = Path.Combine(Repo, "exit.py");
                File.WriteAllText(script, "raise SystemExit(3)\n");
                File.AppendAllText(Path.Combine(Repo, "tool-registry.yaml"), "    entry: " + script + "\n");
                Input = Input with { Host = RuntimeToolHost.Load(Path.Combine(Repo, "tool-registry.yaml")) };
            }
            Directory.CreateDirectory(Path.Combine(Repo, "tools"));
            foreach (var file in new[] { "test-assets", "test_assets.py", "test_asset_operations.py" })
                File.Copy(Path.Combine(RuntimeToolHostTests.RepoRoot(), "tools", file), Path.Combine(Repo, "tools", file));
            var change = Path.Combine(Repo, "changes", AssetChange);
            Directory.CreateDirectory(change);
            File.WriteAllText(Path.Combine(change, "state.md"), "lifecycle_state: implemented\n");
            File.WriteAllText(Config, JsonSerializer.Serialize(new { version = 1, parentRoot = Path.Combine(Root, "assets"),
                repositoryKey = Namespace, workPath = "work", archivePath = "archives",
                categoryPaths = new { raw = "raw/captures", logs = "logs", results = "output/results", scratch = "scratch" } }));
            File.WriteAllText(Path.Combine(Input.RunsRoot, Input.RunDirName, "facts.json"),
                """{"status":"TerminalNotProven","terminal":false,"reason":"controlled-test"}""");
        }

        public JsonDocument ReadIndex() => JsonDocument.Parse(File.ReadAllBytes(Index));
        public async Task<string> ReadReference(string reference)
        {
            var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("UNICLAW_TOOL_PYTHON") ?? "python3")
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var arg in new[] { Path.Combine(Repo, "tools", "test-assets"), "path-ref", reference }) info.ArgumentList.Add(arg);
            using var process = Process.Start(info)!;
            var text = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.True(process.ExitCode == 0, error);
            return text.Trim();
        }

        public void Dispose()
        {
            // Only this fixture's uniquely named namespace; never shared roots.
            var binding = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state", "uniclaw", "test-asset-bindings", Namespace);
            if (Directory.Exists(binding)) Directory.Delete(binding, recursive: true);
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    [Fact]
    public async Task ManagedAssets_ConfiguredPacketSignalsAndReaderShareRun()
    {
        using var fixture = new ManagedFixture(signals: true);
        var before = File.ReadAllBytes(fixture.Input.ReportPath);
        var result = await fixture.Input.Host.InvokeProcedureWithAssetsAsync("run-diagnosis", fixture.Input.RunsRoot,
            fixture.Input.RunDirName, FakeTransport(Packet), Model, fixture.Request);
        Assert.Equal(AssetChange, result.AssetChangeId);
        Assert.NotNull(result.AssetRunId);
        Assert.Null(result.AssetFinalizationError);
        var packetPath = await fixture.ReadReference(result.PacketRef);
        Assert.Contains(Path.Combine("raw", "captures"), packetPath);
        Assert.Equal(result.PacketSha256, RuntimeHash(File.ReadAllBytes(packetPath)));
        var signalPath = await fixture.ReadReference(Assert.Single(result.SignalRefs!));
        using var signal = JsonDocument.Parse(File.ReadAllBytes(signalPath));
        Assert.Equal(result.PacketSha256, signal.RootElement.GetProperty("diagnosis").GetProperty("sha256").GetString());
        Assert.Equal(before, File.ReadAllBytes(fixture.Input.ReportPath));
        Assert.False(Directory.Exists(Path.Combine(fixture.Repo, "evidence")));
        using var index = fixture.ReadIndex();
        var run = Assert.Single(index.RootElement.GetProperty("runs").EnumerateArray());
        Assert.Equal("SUCCEEDED", run.GetProperty("summary").GetProperty("status").GetString());
        Assert.True(run.GetProperty("summary").GetProperty("consumersCompleted").GetBoolean());
        Assert.True(run.GetProperty("summary").GetProperty("sourceStable").GetBoolean());
        using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(packetPath))!, "..", "output", "results", "finalization.json")));
        Assert.Equal("diagnosis", receipt.RootElement.GetProperty("operation").GetString());
    }

    private static string RuntimeHash(byte[] bytes) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

    [Fact]
    public async Task ManagedAssets_TransportFailurePreservesErrorAndRecordsPending()
    {
        using var fixture = new ManagedFixture();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Input.Host.InvokeProcedureWithAssetsAsync(
            "run-diagnosis", fixture.Input.RunsRoot, fixture.Input.RunDirName,
            (_, _, _, _) => throw new InvalidOperationException("controlled-transport-failure"), Model, fixture.Request));
        Assert.Equal("controlled-transport-failure", error.Message);
        using var index = fixture.ReadIndex();
        var summary = index.RootElement.GetProperty("runs")[0].GetProperty("summary");
        Assert.Equal("ERROR", summary.GetProperty("status").GetString());
        Assert.False(summary.GetProperty("consumersCompleted").GetBoolean());
        Assert.False(Directory.Exists(Path.Combine(fixture.Repo, "evidence")));
    }

    [Fact]
    public async Task ManagedAssets_CancellationSettlesBeforeFinalization()
    {
        using var fixture = new ManagedFixture();
        using var cancel = new CancellationTokenSource();
        var called = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Input.Host.InvokeProcedureWithAssetsAsync(
            "run-diagnosis", fixture.Input.RunsRoot, fixture.Input.RunDirName,
            async (_, _, _, token) => { called = true; cancel.Cancel(); await Task.Delay(100, token); return Packet; },
            Model, fixture.Request, cancellationToken: cancel.Token));
        Assert.True(called);
        using var index = fixture.ReadIndex();
        Assert.Equal("CANCELLED", index.RootElement.GetProperty("runs")[0].GetProperty("summary").GetProperty("status").GetString());
    }

    [Fact]
    public async Task ManagedAssets_InvalidLayoutRejectsBeforeModelAndProductWrites()
    {
        using var fixture = new ManagedFixture();
        File.WriteAllText(fixture.Config, "{}");
        var called = false;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Input.Host.InvokeProcedureWithAssetsAsync(
            "run-diagnosis", fixture.Input.RunsRoot, fixture.Input.RunDirName,
            (_, _, _, _) => { called = true; return Task.FromResult(Packet); }, Model, fixture.Request));
        Assert.Contains("development-assets-begin-failed", error.Message);
        Assert.False(called);
        Assert.False(File.Exists(fixture.Index));
    }

    [Fact]
    public async Task ManagedAssets_ExitFailureRetainsPacketAndOriginalFailure()
    {
        using var fixture = new ManagedFixture(badExit: true);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Input.Host.InvokeProcedureWithAssetsAsync(
            "run-diagnosis", fixture.Input.RunsRoot, fixture.Input.RunDirName, FakeTransport(Packet), Model, fixture.Request));
        Assert.Contains("diagnosis-signal-failed", error.Message);
        using var index = fixture.ReadIndex();
        Assert.Contains(index.RootElement.GetProperty("runs")[0].GetProperty("files").EnumerateArray(),
            file => file.GetProperty("path").GetString()!.EndsWith("diagnosis.packet.json", StringComparison.Ordinal));
        Assert.False(index.RootElement.GetProperty("runs")[0].GetProperty("summary").GetProperty("consumersCompleted").GetBoolean());
    }

    [Fact]
    public async Task ManagedAssets_LinkInsertedDuringTransportCannotRedirectPacketIntoProductRun()
    {
        using var fixture = new ManagedFixture();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Input.Host.InvokeProcedureWithAssetsAsync(
            "run-diagnosis", fixture.Input.RunsRoot, fixture.Input.RunDirName,
            (_, _, _, _) =>
            {
                var raw = Assert.Single(Directory.GetDirectories(Path.Combine(fixture.Root, "assets"), "captures", SearchOption.AllDirectories));
                Directory.Move(raw, raw + "-kept");
                Directory.CreateSymbolicLink(raw, Path.Combine(fixture.Input.RunsRoot, fixture.Input.RunDirName));
                return Task.FromResult(Packet);
            }, Model, fixture.Request));
        Assert.Contains("development-assets-output-invalid", error.Message);
        Assert.False(File.Exists(Path.Combine(fixture.Input.RunsRoot, fixture.Input.RunDirName, "diagnosis.packet.json")));
    }

    [Fact]
    public async Task ManagedAssets_FinalizationFailureDoesNotReplaceDiagnosisResult()
    {
        using var fixture = new ManagedFixture();
        var script = Path.Combine(fixture.Repo, "tools", "test-assets");
        File.WriteAllText(script, "import sys\nif len(sys.argv)>1 and sys.argv[1]=='finish': sys.exit(7)\n" + File.ReadAllText(script));
        var result = await fixture.Input.Host.InvokeProcedureWithAssetsAsync("run-diagnosis", fixture.Input.RunsRoot,
            fixture.Input.RunDirName, FakeTransport(Packet), Model, fixture.Request);
        var packetPath = Assert.Single(Directory.GetFiles(Path.Combine(fixture.Root, "assets"), "diagnosis.packet.json", SearchOption.AllDirectories));
        Assert.Contains("non-authoritative", File.ReadAllText(packetPath));
        Assert.NotNull(result.AssetFinalizationError);
        Assert.NotNull(result.AssetRunId);
    }
}

public sealed partial class RuntimeToolHostProcedureTests
{
    [Fact]
    public void ManagedAssets_RequestEnvelopeIsOptInAndKeepsLegacyDefaults()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var legacy = JsonSerializer.Deserialize<RuntimeHttpServer.ToolInvokeRequest>("""{"runDir":"run-a"}""", options)!;
        Assert.Null(legacy.DevelopmentAssets);
        Assert.False(legacy.StrictReport);
        var managed = JsonSerializer.Deserialize<RuntimeHttpServer.ToolInvokeRequest>(
            """{"runDir":"run-a","developmentAssets":{"changeId":"DEV-001","assetConfig":"layout.json","retryOf":"run-old"}}""", options)!;
        Assert.Equal("DEV-001", managed.DevelopmentAssets!.ChangeId);
        Assert.Equal("layout.json", managed.DevelopmentAssets.AssetConfig);
        Assert.Equal("run-old", managed.DevelopmentAssets.RetryOf);
    }
}
