using System.Diagnostics;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Host.Tests;

public sealed class CoreHistoryStorageCostTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ActualControlGrowthStoresFactsAndVersionsWithinMeasuredBudget()
    {
        var directory = Directory.CreateTempSubdirectory("control-version-growth-").FullName;
        try
        {
            var repo = new DirectoryInfo(AppContext.BaseDirectory);
            while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "tools/RunRecoveryProbe/RunRecoveryProbe.csproj"))) repo = repo.Parent;
            Assert.NotNull(repo);
            var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Path.Combine(repo!.FullName, "tools/RunRecoveryProbe/bin", configuration, "net10.0/UniClaw.RunRecoveryProbe.dll"));
            foreach (var argument in new[] { "measure-control-plan-growth", directory, "control", "128" }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90));
                Assert.True(process.ExitCode == 0, await stdout + await stderr);
            }
            finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); } }
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "growth.json")));
            var root = document.RootElement;
            Assert.Equal(128, root.GetProperty("CompletedWork").GetInt32());
            Assert.True(root.GetProperty("FinalDecisionBlocked").GetBoolean());
            Assert.All(root.GetProperty("SourceSha256Preserved").EnumerateObject(), property => Assert.True(property.Value.GetBoolean()));
            var stored = root.GetProperty("JournalAndHead").EnumerateObject()
                .Where(property => property.Name.StartsWith("control-traversal.journal.jsonl", StringComparison.Ordinal))
                .Sum(property => property.Value.GetProperty("Bytes").GetInt64());
            output.WriteLine($"Actual 128-candidate Control journal, head and baselines: {stored} bytes");
            Assert.True(stored < 9L * 1024 * 1024,
                $"Actual Control source, head and baselines stored {stored} bytes; budget is 9MiB");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
