using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Host.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class ControlTraversalHistoryFileTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("torn")]
    [InlineData("head-missing")]
    [InlineData("head-stale")]
    [InlineData("tail-loss")]
    [InlineData("wrong-control-source")]
    [InlineData("wrong-run-source")]
    [InlineData("unknown-state")]
    [InlineData("schema")]
    [InlineData("legacy-v3")]
    [InlineData("legacy-v2")]
    [InlineData("missing-source-link")]
    public void MissingLegacyOrInvalidControlSourcesNeverBecomeAnEmptyRecoveredCursor(string fault)
    {
        var directory = Directory.CreateTempSubdirectory("control-history-boundary-").FullName;
        try
        {
            string? runId = null; var result = RunResourceRecoveryTests.Run(directory, context => runId = context.RunId);
            var archive = new FileRunRecoveryArchive(result.RunDir); var identityPath = Path.Combine(result.RunDir, "run-identity.json");
            var sourcePath = Path.Combine(result.RunDir, "control-traversal.journal.jsonl");
            var original = archive.ReadControlTraversalHistory(runId!); Assert.False(original.ContinuationSupported);
            if (fault == "missing") File.Delete(sourcePath);
            if (fault == "torn") File.WriteAllBytes(sourcePath, File.ReadAllBytes(sourcePath)[..^2]);
            if (fault == "head-missing") File.Delete(sourcePath + ".head");
            if (fault == "head-stale")
            {
                var head = JsonNode.Parse(File.ReadAllText(sourcePath + ".head"))!; head["sequence"] = 1000;
                File.WriteAllText(sourcePath + ".head", head.ToJsonString() + "\n");
            }
            if (fault == "tail-loss") File.WriteAllText(sourcePath, string.Join("\n", File.ReadAllLines(sourcePath)[..^1]) + "\n");
            if (fault is "wrong-control-source" or "legacy-v3" or "legacy-v2" or "missing-source-link")
            {
                var frame = JsonNode.Parse(File.ReadAllText(identityPath))!; var payload = JsonNode.Parse(frame["payload"]!.GetValue<string>())!;
                if (fault == "wrong-control-source") payload["controlTraversalRecordingId"] = Guid.NewGuid().ToString("N");
                if (fault.StartsWith("legacy", StringComparison.Ordinal)) payload["schema"] = "uniclaw.runtime.original-identity." + fault.Split('-')[1];
                if (fault != "wrong-control-source") payload["controlTraversalRecordingId"] = null;
                frame["payload"] = payload.ToJsonString(); frame["hash"] = Hash(frame["consultationFile"]!.GetValue<string>() + "\n" + payload.ToJsonString());
                File.WriteAllText(identityPath, frame.ToJsonString() + "\n");
                Assert.True(archive.Inspect(runId!).AccountingComplete);
            }
            if (fault is "wrong-run-source" or "unknown-state" or "schema")
            {
                var originalHistory = archive.ReadControlTraversalHistory(runId!);
                CoreHistoryLegacyFixture.WriteControl(sourcePath, originalHistory.Position.RecordingId, originalHistory.Records, rows =>
                {
                    var row = rows[0]!;
                    if (fault == "wrong-run-source") row["runRecordingId"] = Guid.NewGuid().ToString("N");
                    if (fault == "unknown-state") row["state"]!["unrecognizedField"] = "unrecognized";
                    if (fault == "schema") row["schema"] = "unknown-control-schema";
                });
            }
            var error = Record.Exception(() => archive.ReadControlTraversalHistory(runId!));
            Assert.True(error is IOException or InvalidDataException or JsonException, error?.ToString() ?? "invalid Control source was accepted");
            Assert.True(archive.RestoreRunState(runId!).IsReadOnly);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    [Theory]
    [InlineData("select")]
    [InlineData("navigated")]
    public async Task ActualKillKeepsOriginalControlOperationBeforeItsAcknowledgement(string operation)
    {
        var directory = Directory.CreateTempSubdirectory("control-history-kill-").FullName;
        try
        {
            using var original = Start("write-control", directory, operation);
            try
            {
                var until = DateTime.UtcNow.AddSeconds(20);
                while (!File.Exists(Path.Combine(directory, "ready.json")) && !original.HasExited && DateTime.UtcNow < until) await Task.Delay(25);
                Assert.True(File.Exists(Path.Combine(directory, "ready.json")), original.HasExited ? await original.StandardError.ReadToEndAsync() : "readiness timeout");
                var ready = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "ready.json")))!; var runId = ready["RunId"]!.GetValue<string>();
                Assert.False(ready["IsTerminal"]!.GetValue<bool>());
                Assert.Equal(ready["PublishedSequence"]!.GetValue<int>() + 1, ready["SourceSequence"]!.GetValue<int>());
                Assert.Equal(operation == "select" ? 0 : 1, ready["Deliveries"]!.GetValue<int>());
                original.Kill(true); await original.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); Assert.NotEqual(0, original.ExitCode);
                File.WriteAllText(Path.Combine(directory, "kill-observed.json"), JsonSerializer.Serialize(new { original.Id, original.ExitCode, original.HasExited }));
                var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "control-expected.json")))!;
                var sourceHashes = Directory.GetFiles(directory).Where(p => p.EndsWith(".jsonl", StringComparison.Ordinal) || p.EndsWith(".head", StringComparison.Ordinal)
                    || Path.GetFileName(p) is "run-identity.json" or "exec.journal").ToDictionary(p => Path.GetFileName(p)!, p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
                File.WriteAllText(Path.Combine(directory, "readonly-source-hashes.json"), JsonSerializer.Serialize(sourceHashes));
                for (var index = 0; index < 2; index++)
                {
                    using var reader = Start("read-control", directory, runId);
                    try
                    {
                        await reader.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); Assert.True(reader.ExitCode == 0, await reader.StandardError.ReadToEndAsync());
                        var result = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "control-reader.json")))!; var read = result["Read"]!;
                        Assert.NotEqual(original.Id, result["ProcessId"]!.GetValue<int>());
                        Assert.False(read["ContinuationSupported"]!.GetValue<bool>());
                        Assert.True(JsonNode.DeepEquals(expected["Row"]!["State"], read["State"]));
                        Assert.True(JsonNode.DeepEquals(expected["SourceRows"], read["Records"]));
                        Assert.Equal(operation, read["Records"]!.AsArray()[^1]!["Operation"]!.GetValue<string>());
                        var state = read["State"]!;
                        Assert.Equal(operation == "select" ? 1 : 2, state["path"]!.AsArray().Count);
                        if (operation == "select")
                        {
                            Assert.Equal(1, state["invocationOrdinal"]!.GetValue<int>());
                            Assert.NotEmpty(state["path"]![0]!["assignedCandidates"]!.AsArray());
                            Assert.Empty(ready["RunOperations"]!.AsArray());
                        }
                        else
                        {
                            Assert.NotNull(state["returnOperation"]);
                            Assert.Equal("completed", state["progressEvents"]![0]!["kind"]!.GetValue<string>());
                            Assert.False(ready["RunOperations"]![0]!["Completed"]!.GetValue<bool>());
                        }
                        foreach (var (name, hash) in sourceHashes) Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, name!)))));
                        File.Copy(Path.Combine(directory, "control-reader.json"), Path.Combine(directory, $"reader-{index}.json"));
                    }
                    finally { if (!reader.HasExited) { reader.Kill(true); await reader.WaitForExitAsync(); } }
                }
                var output = Environment.GetEnvironmentVariable("CONTROL_TRAVERSAL_EVIDENCE_DIR");
                if (!string.IsNullOrEmpty(output))
                {
                    var target = Path.Combine(output, operation); Directory.CreateDirectory(target);
                    foreach (var file in Directory.GetFiles(directory)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
                }
            }
            finally { if (!original.HasExited) { original.Kill(true); await original.WaitForExitAsync(); } }
        }
        finally { Directory.Delete(directory, true); }
    }

    private static Process Start(string mode, string directory, string argument)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "tools/RunRecoveryProbe/RunRecoveryProbe.csproj"))) root = root.Parent;
        if (root is null) throw new DirectoryNotFoundException("Probe missing");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        start.ArgumentList.Add(Path.Combine(root.FullName, "tools/RunRecoveryProbe/bin", configuration, "net10.0/UniClaw.RunRecoveryProbe.dll"));
        start.ArgumentList.Add(mode); start.ArgumentList.Add(directory); start.ArgumentList.Add(argument); return Process.Start(start)!;
    }
}
