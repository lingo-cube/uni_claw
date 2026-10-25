using System.Text.Json;
using System.Diagnostics;
using System.Security.Cryptography;
using UniClaw.Host.Runtime;
using UniClaw.Kernel;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class WorldAsyncStorageTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("world-native-async-").FullName;
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public async Task AsyncWorldOwnerAndNativeIncrementalFileReturnTheOriginalRevisions()
    {
        var wp = Path.Combine(_root, "world"); var ep = Path.Combine(_root, "evidence");
        var run = Guid.NewGuid().ToString("N"); string worldId, evidenceId; string[] expected;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        await using (var evidence = new FileEvidenceJournal(ep))
        await using (var journal = new FileWorldJournal(wp))
        {
            worldId = journal.RecordingId; evidenceId = evidence.RecordingId;
            Assert.IsAssignableFrom<IAsyncWorldJournal>(journal);
            var world = new WorldModel(new HashSet<string> { "switch" }, history: new(journal, run, evidenceId));
            var kernel = new UniKernel(new EvidenceLedger(evidence), world, DisabledRunTrace.Instance);
            var seed = new ObservationProposal(new("switch", "on"), IngressKind.Observation, ObservationContext.External,
                new("native-world-async", DateTimeOffset.Parse("2026-10-25T00:00:00Z"), "original", ["synthetic"]));
            await kernel.ProcessBatchAsync([seed, seed with { Claim = new("switch", "off"), Provenance = seed.Provenance with { CaptureTime = seed.Provenance.CaptureTime.AddSeconds(1) } }]);
            expected = world.RevisionHistory.Select(r => JsonSerializer.Serialize(JsonSerializer.SerializeToElement(r, json), json)).ToArray();
            Assert.Equal(2, expected.Length);
        }
        using var er = new FileEvidenceJournal(ep, true);
        using var wr = new FileWorldJournal(wp, true);
        var read = WorldHistoryRead.ReadOriginal(wr, worldId, run, EvidenceLedger.RestoreReadOnly(er, evidenceId));
        Assert.Equal(expected, read.HistoricalRevisions.Select(r => r.GetRawText()));
        Assert.Contains("uniclaw.world.history.v3", File.ReadAllText(wp));
    }

    [Fact]
    public async Task CancelBeforeNativeAcceptanceLeavesTheOriginalFileUntouchedAndDoesNotPoisonIt()
    {
        var path = Path.Combine(_root, "cancel-before");
        await using var writer = new FileWorldJournal(path);
        var original = File.ReadAllBytes(path); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var row = new WorldHistoryRecord(1, "relevance", JsonSerializer.SerializeToElement(new { input = new { evidenceId = "original" } }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.AppendOperationAsync(row, [], cancellation.Token).AsTask());
        Assert.Equal(original, File.ReadAllBytes(path));
        await writer.AppendOperationAsync(row, []);
        using var reader = new FileWorldJournal(path, true);
        Assert.Single(reader.ReadRecords());
    }

    [Fact]
    public async Task ALogicalOperationExceedingTheConfiguredByteLimitFailsBeforeWritingAndLatches()
    {
        var path = Path.Combine(_root, "byte-limit");
        await using var writer = new FileWorldJournal(path, maxOperationBytes: 512);
        var original = File.ReadAllBytes(path);
        var row = new WorldHistoryRecord(1, "relevance", JsonSerializer.SerializeToElement(new { input = new { text = new string('x', 2048) } }));
        await Assert.ThrowsAsync<InvalidDataException>(() => writer.AppendOperationAsync(row, []).AsTask());
        Assert.Equal(original, File.ReadAllBytes(path));
        await Assert.ThrowsAsync<IOException>(() => writer.AppendOperationAsync(row, []).AsTask());
    }

    [Fact]
    public async Task ActualDriverKillAfterWorldCommitBeforeOwnerAcknowledgementPreservesOriginalHistory()
    {
        using var writer = StartProbe("write-world-async", _root, "original");
        try
        {
            var marker = Path.Combine(_root, "ready.json"); var deadline = DateTime.UtcNow.AddSeconds(40);
            while (!File.Exists(marker) && !writer.HasExited && DateTime.UtcNow < deadline) await Task.Delay(25);
            Assert.True(File.Exists(marker), writer.HasExited ? await writer.StandardError.ReadToEndAsync() : "World commit marker timeout");
            using var readyDocument = JsonDocument.Parse(File.ReadAllText(marker)); var ready = readyDocument.RootElement;
            Assert.True(ready.GetProperty("DriverPending").GetBoolean()); Assert.False(ready.GetProperty("IsTerminal").GetBoolean());
            Assert.Equal(0, ready.GetProperty("PublishedWorldRevisions").GetInt32()); Assert.Equal(JsonValueKind.Null, ready.GetProperty("Belief").ValueKind);
            Assert.Equal(2, ready.GetProperty("OwnerPosition").GetProperty("Sequence").GetInt32());
            Assert.Equal(3, ready.GetProperty("SourceRows").GetInt32()); Assert.Equal(1, ready.GetProperty("EvidenceAdmissions").GetInt32());
            writer.Kill(true); await writer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); Assert.NotEqual(0, writer.ExitCode);
            File.WriteAllText(Path.Combine(_root, "kill-observed.json"), JsonSerializer.Serialize(new { writer.Id, writer.HasExited, writer.ExitCode }));
            var hashes = Directory.GetFiles(_root).ToDictionary(file => Path.GetFileName(file)!, file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))));
            File.WriteAllText(Path.Combine(_root, "source-hashes.json"), JsonSerializer.Serialize(hashes));
            using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "expected.json")));
            var readerIds = new HashSet<int>();
            for (var repeat = 0; repeat < 2; repeat++)
            {
                using var reader = StartProbe("read-world", _root, ready.GetProperty("RunId").GetString()!, ready.GetProperty("WorldRecordingId").GetString()!);
                try
                {
                    await reader.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
                    Assert.True(reader.ExitCode == 0, await reader.StandardError.ReadToEndAsync());
                    using var result = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "world-reader-result.json"))); var read = result.RootElement;
                    var readerId = read.GetProperty("ProcessId").GetInt32();
                    Assert.NotEqual(writer.Id, readerId); Assert.True(readerIds.Add(readerId));
                    Assert.True(read.GetProperty("RunIsReadOnly").GetBoolean()); Assert.True(read.GetProperty("EvidenceIsReadOnly").GetBoolean());
                    Assert.False(read.GetProperty("ContinuationSupported").GetBoolean()); Assert.False(read.GetProperty("IsTerminal").GetBoolean());
                    Assert.Single(read.GetProperty("HistoricalRevisions").EnumerateArray());
                    Assert.True(JsonElement.DeepEquals(expected.RootElement.GetProperty("Rows"), read.GetProperty("Operations")));
                    foreach (var (file, hash) in hashes) Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(_root, file!)))));
                    File.Copy(Path.Combine(_root, "world-reader-result.json"), Path.Combine(_root, "reader-" + repeat + ".json"));
                }
                finally { if (!reader.HasExited) { reader.Kill(true); await reader.WaitForExitAsync(); } }
            }
            if (Environment.GetEnvironmentVariable("UNICLAW_WORLD_ASYNC_EXPORT") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                foreach (var file in Directory.GetFiles(_root)) File.Copy(file, Path.Combine(output, Path.GetFileName(file)), true);
            }
        }
        finally { if (!writer.HasExited) { writer.Kill(true); await writer.WaitForExitAsync(); } }
    }

    private static Process StartProbe(params string[] arguments)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "tools/RunRecoveryProbe/RunRecoveryProbe.csproj"))) root = root.Parent;
        if (root is null) throw new DirectoryNotFoundException("original probe missing");
        var config = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(root.FullName, "tools/RunRecoveryProbe/bin", config, "net10.0/UniClaw.RunRecoveryProbe.dll"));
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return Process.Start(start)!;
    }
}
