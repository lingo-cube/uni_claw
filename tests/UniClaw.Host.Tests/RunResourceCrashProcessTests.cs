using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RunResourceCrashProcessTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonterminalProcessKillThenIndependentReaderPreservesOriginalAccounting(bool pending)
    {
        var directory = Directory.CreateTempSubdirectory("run-recovery-kill-").FullName;
        try
        {
            using var original = Start("write", directory, pending ? "pending" : "settled");
            var error = original.StandardError.ReadToEndAsync();
            try
            {
                var until = DateTime.UtcNow.AddSeconds(20);
                while (!File.Exists(Path.Combine(directory, "ready.json")) && !original.HasExited && DateTime.UtcNow < until)
                    await Task.Delay(25);
                if (!File.Exists(Path.Combine(directory, "ready.json")))
                    throw new InvalidOperationException(original.HasExited ? await error : "Original process did not reach readiness");
                using var ready = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "ready.json")));
                Assert.False(ready.RootElement.GetProperty("Terminal").GetBoolean());
                Assert.Equal(1, ready.RootElement.GetProperty("Consultations").GetInt32());
                Assert.Equal(0, ready.RootElement.GetProperty("Deliveries").GetInt32());
                var runId = ready.RootElement.GetProperty("RunId").GetString()!;
                var writerId = original.Id;
                original.Kill(entireProcessTree: true);
                await original.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                Assert.NotEqual(0, original.ExitCode);
                File.WriteAllText(Path.Combine(directory, "kill-observed.json"), JsonSerializer.Serialize(new
                { WriterProcessId = writerId, original.ExitCode, HasExited = original.HasExited, UsedKill = true, Pending = pending }));
                var originals = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                    .ToDictionary(file => Path.GetRelativePath(directory, file), file => SHA256.HashData(File.ReadAllBytes(file)));
                using var reader = Start("read", directory, runId);
                try
                {
                    await reader.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                    Assert.True(reader.ExitCode == 0, await reader.StandardError.ReadToEndAsync());
                    Assert.NotEqual(writerId, reader.Id);
                    var read = JsonSerializer.Deserialize<RunResourceRead>(File.ReadAllText(Path.Combine(directory, "reader-result.json")))!;
                    Assert.Equal(runId, read.Identity!.RunId); Assert.False(read.ContinuationSupported);
                    Assert.Equal(0, read.Confirmed!.StepsConsumed);
                    if (pending)
                    {
                        Assert.False(read.AccountingComplete); Assert.Equal("consult", read.PendingOperation);
                        Assert.Null(read.RoundsRemaining); Assert.Equal(0, read.Confirmed.RoundsConsumed);
                    }
                    else
                    {
                        Assert.True(read.AccountingComplete, read.Reason);
                        Assert.Equal(1, read.Confirmed.RoundsConsumed);
                        Assert.Equal(2, read.Confirmed.GrantedSteps); Assert.Equal(2, read.Confirmed.GrantedRounds);
                        Assert.Equal(12, read.StepsRemaining); Assert.Equal(5, read.RoundsRemaining);
                        Assert.True(Assert.Single(read.Confirmed.Renewals).Granted);
                        Assert.Equal(new[] { "known:a" }, read.Confirmed.FundedItems["pending:a"]);
                        Assert.Contains("known:a", read.Confirmed.UsedProgressFacts);
                        Assert.Contains(read.Confirmed.Reviews, review => review.NextItemKey == "pending:a" && review.RoundsGranted == 2);
                    }
                    foreach (var (name, hash) in originals)
                        Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, name))));
                    Export(pending ? "kill-pending" : "kill-settled", directory);
                }
                finally { if (!reader.HasExited) { reader.Kill(true); await reader.WaitForExitAsync(); } }
            }
            finally { if (!original.HasExited) { original.Kill(true); await original.WaitForExitAsync(); } }
        }
        finally { Directory.Delete(directory, true); }
    }

    private static Process Start(string mode, string directory, string argument)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "tools/RunRecoveryProbe/RunRecoveryProbe.csproj"))) root = root.Parent;
        if (root is null) throw new DirectoryNotFoundException("Probe project root missing");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
        var dll = Path.Combine(root.FullName, "tools/RunRecoveryProbe/bin", configuration, "net10.0/UniClaw.RunRecoveryProbe.dll");
        var start = new ProcessStartInfo("dotnet") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        start.ArgumentList.Add(dll); start.ArgumentList.Add(mode); start.ArgumentList.Add(directory); start.ArgumentList.Add(argument);
        return Process.Start(start)!;
    }

    private static void Export(string label, string directory)
    {
        var output = Environment.GetEnvironmentVariable("RECOVERY_EVIDENCE_DIR");
        if (string.IsNullOrEmpty(output)) return;
        var target = Path.Combine(output, label); Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(directory, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
    }
}
