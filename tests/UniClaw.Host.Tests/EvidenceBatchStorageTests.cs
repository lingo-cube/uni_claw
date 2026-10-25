using UniClaw.Host.Runtime;
using UniClaw.Kernel.Evidence;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class EvidenceBatchStorageTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("evidence-batch-").FullName;
    public void Dispose() => Directory.Delete(_directory, true);
    private static ObservationProposal Proposal(int n) => new(new("live.frame", "value-" + n), IngressKind.Observation,
        ObservationContext.External, new("batch-file", DateTimeOffset.Parse("2026-10-25T00:00:00Z"), "batch", ["original"]));

    [Fact]
    public async Task OriginalBatchUsesFewerActualFrameAndHeadWritesThanSingleAdmissions()
    {
        var input = Enumerable.Range(0, 64).Select(Proposal).ToArray();
        using var baseline = new FileEvidenceJournal(Path.Combine(_directory, "single.jsonl"));
        var single = new EvidenceLedger(baseline); var beforeSingle = baseline.WriteStatistics;
        foreach (var proposal in input) single.Admit(proposal);
        await using var batch = new FileEvidenceJournal(Path.Combine(_directory, "batch.jsonl"));
        var ledger = new EvidenceLedger(batch); var beforeBatch = batch.WriteStatistics;
        await ledger.AdmitBatchAsync(input);
        Assert.Equal(single.CanonicalRecords.Keys.Order(), ledger.CanonicalRecords.Keys.Order());
        Assert.Equal(64, baseline.WriteStatistics.FrameWrites - beforeSingle.FrameWrites);
        Assert.Equal(1, batch.WriteStatistics.FrameWrites - beforeBatch.FrameWrites);
        Assert.Equal(1, batch.WriteStatistics.FrameFlushes - beforeBatch.FrameFlushes);
        Assert.Equal(1, batch.WriteStatistics.HeadWrites - beforeBatch.HeadWrites);
        Assert.Equal(1, batch.WriteStatistics.HeadFlushes - beforeBatch.HeadFlushes);
        using var reader = new FileEvidenceJournal(Path.Combine(_directory, "batch.jsonl"), true);
        var read = EvidenceLedger.RestoreReadOnly(reader, batch.RecordingId);
        Assert.Equal(64, read.AdmissionLog.Count); Assert.Equal(64, read.CanonicalRecords.Count);
        Assert.Equal(ledger.HistoryPosition, read.HistoryPosition);
        Export("io-counts.json", new { Records = input.Length, Single = baseline.WriteStatistics, Batch = batch.WriteStatistics,
            Unit = "managed FileStream calls; not physical disk syscalls", PowerLossProof = false });
    }

    [Theory]
    [InlineData(3, 1048576)]
    [InlineData(128, 5000)]
    public async Task EncodedChunksAreBoundedAndPreserveEveryOriginalRecord(int maxRecords, int maxBytes)
    {
        var path = Path.Combine(_directory, "bounded.jsonl");
        await using var file = new FileEvidenceJournal(path, maxBatchRecords: maxRecords, maxBatchBytes: maxBytes);
        var ledger = new EvidenceLedger(file); var before = file.WriteStatistics;
        await ledger.AdmitBatchAsync(Enumerable.Range(0, 12).Select(Proposal).ToArray());
        Assert.True(file.WriteStatistics.PeakBatchRecords <= maxRecords); Assert.True(file.WriteStatistics.PeakBatchBytes <= maxBytes);
        Assert.True(file.WriteStatistics.FrameWrites - before.FrameWrites > 1);
        if (maxRecords == 3) Assert.Equal(4, file.WriteStatistics.FrameWrites - before.FrameWrites);
        using var source = new FileEvidenceJournal(path, true); var read = EvidenceLedger.RestoreReadOnly(source, file.RecordingId);
        Assert.Equal(12, read.AdmissionLog.Count); Assert.Equal(ledger.CanonicalRecords.Keys.Order(), read.CanonicalRecords.Keys.Order());
    }

    [Fact]
    public async Task FailedHeadPublicationNeverAcknowledgesOrPublishesTheCandidate()
    {
        var path = Path.Combine(_directory, "failure.jsonl"); await using var file = new FileEvidenceJournal(path);
        var ledger = new EvidenceLedger(file); File.Delete(path + ".head"); Directory.CreateDirectory(path + ".head");
        await Assert.ThrowsAnyAsync<IOException>(() => ledger.AdmitBatchAsync([Proposal(1), Proposal(2)]).AsTask());
        Assert.Empty(ledger.AdmissionLog); Assert.Empty(ledger.CanonicalRecords); var writes = file.WriteStatistics.FrameWrites;
        await Assert.ThrowsAnyAsync<IOException>(() => ledger.AdmitBatchAsync([Proposal(3)]).AsTask());
        Assert.Equal(writes, file.WriteStatistics.FrameWrites);
        var error = Record.Exception(() => new FileEvidenceJournal(path, true));
        Assert.True(error is IOException or InvalidDataException or UnauthorizedAccessException, error?.ToString());
    }

    [Fact]
    public async Task PartialBoundedCommitRemainsOriginalHistoryWithoutPublishingTheWholeCandidate()
    {
        var path = Path.Combine(_directory, "partial.jsonl");
        await using var file = new FileEvidenceJournal(path, maxBatchRecords: 1, maxBatchBytes: 5000);
        var ledger = new EvidenceLedger(file);
        await Assert.ThrowsAnyAsync<IOException>(() => ledger.AdmitBatchAsync([Proposal(1), Proposal(2) with
            { Claim = new("live.frame", new string('x', 10000)) }]).AsTask());
        Assert.Empty(ledger.AdmissionLog); Assert.Empty(ledger.CanonicalRecords);
        using var original = new FileEvidenceJournal(path, true);
        var read = EvidenceLedger.RestoreReadOnly(original, file.RecordingId);
        Assert.Single(read.AdmissionLog); Assert.Single(read.CanonicalRecords);
        await Assert.ThrowsAnyAsync<IOException>(() => ledger.AdmitBatchAsync([Proposal(3)]).AsTask());
        Assert.Equal(1, file.WriteStatistics.CommittedBatches);
    }

    [Fact]
    public async Task ActualKillAfterBatchCommitKeepsUnpublishedAdmissionsForTwoNewReaders()
    {
        var directory = Path.Combine(_directory, "kill"); Directory.CreateDirectory(directory);
        using var writer = Start("write-evidence-batch", directory, "unused");
        try
        {
            var until = DateTime.UtcNow.AddSeconds(20);
            while (!File.Exists(Path.Combine(directory, "ready.json")) && !writer.HasExited && DateTime.UtcNow < until) await Task.Delay(25);
            Assert.True(File.Exists(Path.Combine(directory, "ready.json")), writer.HasExited ? await writer.StandardError.ReadToEndAsync() : "readiness timeout");
            var ready = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "ready.json")))!;
            Assert.False(ready["IsTerminal"]!.GetValue<bool>()); Assert.Equal(0, ready["PublishedAdmissions"]!.GetValue<int>());
            Assert.Equal(0, ready["PublishedCanonical"]!.GetValue<int>()); Assert.Null(ready["Belief"]);
            Assert.Equal(4, ready["SourceRows"]!.GetValue<int>());
            writer.Kill(true); await writer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); Assert.NotEqual(0, writer.ExitCode);
            File.WriteAllText(Path.Combine(directory, "kill-observed.json"), JsonSerializer.Serialize(new { writer.Id, writer.ExitCode, writer.HasExited }));
            var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "expected.json")))!;
            var hashes = Directory.GetFiles(directory).Where(p => p.EndsWith(".jsonl", StringComparison.Ordinal) || p.EndsWith(".head", StringComparison.Ordinal)
                || Path.GetFileName(p) is "run-identity.json" or "exec.journal").ToDictionary(p => Path.GetFileName(p)!, HashFile);
            File.WriteAllText(Path.Combine(directory, "source-hashes.json"), JsonSerializer.Serialize(hashes));
            for (var n = 0; n < 2; n++)
            {
                using var reader = Start("read-evidence-batch", directory, ready["RunId"]!.GetValue<string>());
                try
                {
                    await reader.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); Assert.True(reader.ExitCode == 0, await reader.StandardError.ReadToEndAsync());
                    var result = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "reader.json")))!;
                    Assert.NotEqual(writer.Id, result["ProcessId"]!.GetValue<int>()); Assert.True(result["IsReadOnly"]!.GetValue<bool>());
                    Assert.Equal(4, result["Admissions"]!.GetValue<int>()); Assert.Equal(2, result["Canonical"]!.GetValue<int>());
                    Assert.True(JsonNode.DeepEquals(expected["Rows"], result["Rows"]));
                    foreach (var (name, hash) in hashes) Assert.Equal(hash, HashFile(Path.Combine(directory, name)));
                    File.Copy(Path.Combine(directory, "reader.json"), Path.Combine(directory, $"reader-{n}.json"));
                }
                finally { if (!reader.HasExited) { reader.Kill(true); await reader.WaitForExitAsync(); } }
            }
            var output = Environment.GetEnvironmentVariable("EVIDENCE_BATCH_EVIDENCE_DIR");
            if (!string.IsNullOrEmpty(output))
            {
                var target = Path.Combine(output, "kill"); Directory.CreateDirectory(target);
                foreach (var p in Directory.GetFiles(directory)) File.Copy(p, Path.Combine(target, Path.GetFileName(p)), true);
            }
        }
        finally { if (!writer.HasExited) { writer.Kill(true); await writer.WaitForExitAsync(); } }
    }

    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Export(string name, object value)
    {
        var output = Environment.GetEnvironmentVariable("EVIDENCE_BATCH_EVIDENCE_DIR"); if (string.IsNullOrEmpty(output)) return;
        Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value));
    }
    private static Process Start(string mode, string directory, string argument)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "tools/RunRecoveryProbe/RunRecoveryProbe.csproj"))) root = root.Parent;
        if (root is null) throw new DirectoryNotFoundException("Probe missing");
        var config = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        start.ArgumentList.Add(Path.Combine(root.FullName, "tools/RunRecoveryProbe/bin", config, "net10.0/UniClaw.RunRecoveryProbe.dll"));
        start.ArgumentList.Add(mode); start.ArgumentList.Add(directory); start.ArgumentList.Add(argument); return Process.Start(start)!;
    }
}
