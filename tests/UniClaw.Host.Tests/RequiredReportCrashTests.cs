using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RequiredReportCrashTests
{
    private const string InputText = "{\"businessRunId\":\"crash-survivor-42\",\"outcome\":\"completed\"}";
    private const string GeneratorText = "# inert deterministic report generator fixture; never execute";
    private const string BusinessSentinelText = "business-result:completed:run=crash-survivor-42";

    [Theory]
    [InlineData("accepted")]
    [InlineData("ready")]
    [InlineData("generating")]
    [InlineData("published")]
    public async Task ProcessKillAtDeliveryPhase_FreshReadersRecoverOnlyReport(string phase)
    {
        var repositoryRoot = FindRepositoryRoot();
        var evidenceRoot = Path.Combine(repositoryRoot, "changes", "RUNTIME-STORAGE-002", "evidence", "slice2", "crash-tests");
        Directory.CreateDirectory(evidenceRoot);
        var phaseDirectory = Path.Combine(evidenceRoot,
            phase + "-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N"));
        var sourceDirectory = Path.Combine(phaseDirectory, "source");
        Directory.CreateDirectory(sourceDirectory);
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "run.json"), InputText);
        await File.WriteAllTextAsync(Path.Combine(phaseDirectory, "generator.py"), GeneratorText);
        await File.WriteAllTextAsync(Path.Combine(phaseDirectory, "business-sentinel.txt"), BusinessSentinelText);

        var originalInputHash = HashFile(Path.Combine(sourceDirectory, "run.json"));
        var originalGeneratorHash = HashFile(Path.Combine(phaseDirectory, "generator.py"));
        var businessSentinelHash = HashFile(Path.Combine(phaseDirectory, "business-sentinel.txt"));
        JsonDocument writerReady;
        int writerProcessId;
        int writerExitCode;
        string writerStdout;
        string writerStderr;

        using (var writer = StartProbe(repositoryRoot, "write-report-delivery", phaseDirectory, phase))
        {
            var stdoutTask = writer.StandardOutput.ReadToEndAsync();
            var stderrTask = writer.StandardError.ReadToEndAsync();
            try
            {
                var deadline = DateTime.UtcNow.AddSeconds(20);
                while (!File.Exists(Path.Combine(phaseDirectory, "writer-ready.json"))
                    && !writer.HasExited && DateTime.UtcNow < deadline)
                    await Task.Delay(25);

                var readyPath = Path.Combine(phaseDirectory, "writer-ready.json");
                if (!File.Exists(readyPath))
                    throw new InvalidOperationException(writer.HasExited
                        ? "writer exited before readiness: " + await stderrTask
                        : "writer did not reach readiness before the bounded timeout");

                writerReady = JsonDocument.Parse(await File.ReadAllTextAsync(readyPath));
                writerProcessId = writer.Id;
                writer.Kill(entireProcessTree: true);
                await writer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                writerExitCode = writer.ExitCode;
                writerStdout = await stdoutTask;
                writerStderr = await stderrTask;
                await File.WriteAllTextAsync(Path.Combine(phaseDirectory, "writer.log"),
                    $"pid={writerProcessId}\nexit={writerExitCode}\nstdout:\n{writerStdout}\nstderr:\n{writerStderr}");
            }
            finally
            {
                if (!writer.HasExited)
                {
                    writer.Kill(entireProcessTree: true);
                    await writer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                }
            }
        }

        Assert.True(writerReady.RootElement.GetProperty("ProcessId").GetInt32() == writerProcessId);
        Assert.Equal(phase, writerReady.RootElement.GetProperty("Phase").GetString());
        var deliveryId = writerReady.RootElement.GetProperty("DeliveryId").GetString()!;
        var writerStatus = writerReady.RootElement.GetProperty("Status").GetString()!;
        var writerFingerprint = writerReady.RootElement.GetProperty("SourceFingerprint").ValueKind == JsonValueKind.Null
            ? null
            : writerReady.RootElement.GetProperty("SourceFingerprint").GetString();
        Assert.Equal(phase == "accepted" ? "AwaitingInputs" : phase == "ready" ? "Ready" : phase == "generating" ? "Generating" : "Published", writerStatus);
        Assert.NotEqual(0, writerExitCode);
        await File.WriteAllTextAsync(Path.Combine(phaseDirectory, "kill-observed.json"), JsonSerializer.Serialize(new
        {
            WriterProcessId = writerProcessId,
            WriterExitCode = writerExitCode,
            UsedKill = true,
            Phase = phase,
            DeliveryId = deliveryId,
        }));

        if (phase != "accepted")
        {
            Directory.Delete(sourceDirectory, recursive: true);
            File.Delete(Path.Combine(phaseDirectory, "generator.py"));
        }

        var storeRoot = Path.Combine(phaseDirectory, "store");
        var immutableFilesBefore = SnapshotImmutableFiles(storeRoot);
        Assert.NotEmpty(immutableFilesBefore);
        await File.WriteAllTextAsync(Path.Combine(phaseDirectory, "immutables-before.json"),
            JsonSerializer.Serialize(immutableFilesBefore.Select(file => new { RelativePath = file.Key, Sha256 = file.Value })));

        var readerOne = await RunReaderAsync(repositoryRoot, phaseDirectory, phase);
        var readerTwo = await RunReaderAsync(repositoryRoot, phaseDirectory, phase);
        Assert.NotEqual(writerProcessId, readerOne.ProcessId);
        Assert.NotEqual(writerProcessId, readerTwo.ProcessId);
        Assert.NotEqual(readerOne.ProcessId, readerTwo.ProcessId);
        Assert.Equal(deliveryId, readerOne.DeliveryId);
        Assert.Equal(deliveryId, readerTwo.DeliveryId);
        Assert.Equal(writerFingerprint, readerOne.InitialSourceFingerprint);
        Assert.Equal(phase == "accepted" ? "Ready" : writerStatus, readerOne.PreparedStatus);
        Assert.Equal(phase == "accepted" ? 1 : phase == "generating" ? 2 : 1, readerOne.PublishedAttempts);
        Assert.Equal("Delivered", readerOne.ConfirmedStatus);
        Assert.Equal("Delivered", readerOne.FinalStatus);
        Assert.True(readerOne.IsReadable);
        Assert.Equal("Delivered", readerTwo.InitialStatus);
        Assert.Equal("Delivered", readerTwo.ConfirmedStatus);
        Assert.Equal("Delivered", readerTwo.FinalStatus);
        Assert.True(readerTwo.IsReadable);
        Assert.Equal(phase == "published" ? 0 : 1, readerOne.GenerationCalls);
        Assert.Equal(0, readerTwo.GenerationCalls);
        if (phase == "published")
            Assert.Equal(GeneratorText, writerReady.RootElement.GetProperty("PrivateGeneratorText").GetString());
        else
            Assert.Equal(GeneratorText, readerOne.PrivateGeneratorText);
        Assert.Null(readerTwo.PrivateGeneratorText);
        Assert.Equal(InputText, readerOne.ReportSource);
        Assert.Equal(InputText, readerTwo.ReportSource);
        Assert.Contains(InputText, readerOne.ReportMarkdown, StringComparison.Ordinal);
        Assert.Contains(InputText, readerTwo.ReportMarkdown, StringComparison.Ordinal);

        var immutableFilesAfter = SnapshotImmutableFiles(storeRoot);
        var immutableChecks = immutableFilesBefore.Select(file =>
        {
            var stillExists = immutableFilesAfter.TryGetValue(file.Key, out var afterHash);
            return new
            {
                RelativePath = file.Key,
                BeforeSha256 = file.Value,
                AfterSha256 = afterHash,
                StillExists = stillExists,
                ShaUnchanged = stillExists && file.Value == afterHash,
            };
        }).ToArray();
        foreach (var check in immutableChecks)
        {
            Assert.True(check.StillExists, "Previously committed report file disappeared: " + check.RelativePath);
            Assert.True(check.ShaUnchanged, "Previously committed report file changed: " + check.RelativePath);
        }
        var immutablePathsAddedByReaders = immutableFilesAfter.Keys
            .Except(immutableFilesBefore.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        await File.WriteAllTextAsync(Path.Combine(phaseDirectory, "immutables-summary.json"), JsonSerializer.Serialize(new
        {
            Phase = phase,
            DeliveryId = deliveryId,
            BeforeCount = immutableFilesBefore.Count,
            AfterCount = immutableFilesAfter.Count,
            PriorFilesPreserved = immutableChecks.All(check => check.StillExists && check.ShaUnchanged),
            PriorFiles = immutableChecks,
            NewImmutablePathsAfterReaders = immutablePathsAddedByReaders,
            AddedFilesRequiredToMatchCount = false,
        }));

        Assert.Equal(businessSentinelHash, HashFile(Path.Combine(phaseDirectory, "business-sentinel.txt")));
        if (phase == "accepted")
        {
            Assert.Equal(originalInputHash, HashFile(Path.Combine(sourceDirectory, "run.json")));
            Assert.Equal(originalGeneratorHash, HashFile(Path.Combine(phaseDirectory, "generator.py")));
        }
        else
        {
            Assert.False(Directory.Exists(sourceDirectory));
            Assert.False(File.Exists(Path.Combine(phaseDirectory, "generator.py")));
        }

        await File.WriteAllTextAsync(Path.Combine(phaseDirectory, "phase-summary.json"), JsonSerializer.Serialize(new
        {
            Phase = phase,
            DeliveryId = deliveryId,
            WriterProcessId = writerProcessId,
            WriterExitCode = writerExitCode,
            WriterStatus = writerStatus,
            WriterAttempts = writerReady.RootElement.GetProperty("Attempts").GetInt32(),
            WriterSourceFingerprint = writerFingerprint,
            ReaderOne = readerOne,
            ReaderTwo = readerTwo,
            ImmutableFilesBefore = immutableChecks,
            ImmutablePathsAddedByReaders = immutablePathsAddedByReaders,
            OriginalInputSha256 = originalInputHash,
            OriginalGeneratorSha256 = originalGeneratorHash,
            BusinessSentinelSha256 = businessSentinelHash,
            BusinessSentinelUnchanged = true,
            OriginalSourceStillPresentAtReader = phase == "accepted",
        }));
    }

    private static async Task<ReaderResult> RunReaderAsync(string repositoryRoot, string phaseDirectory, string phase)
    {
        using var reader = StartProbe(repositoryRoot, "read-report-delivery", phaseDirectory, phase);
        var stdout = reader.StandardOutput.ReadToEndAsync();
        var stderr = reader.StandardError.ReadToEndAsync();
        try
        {
            await reader.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            var stdoutText = await stdout;
            var stderrText = await stderr;
            await File.WriteAllTextAsync(Path.Combine(phaseDirectory, "reader-process-" + reader.Id + ".log"),
                $"pid={reader.Id}\nexit={reader.ExitCode}\nstdout:\n{stdoutText}\nstderr:\n{stderrText}");
            Assert.True(reader.ExitCode == 0, stderrText);
            var path = Path.Combine(phaseDirectory, "reader-" + reader.Id + ".json");
            using var result = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var root = result.RootElement;
            return new ReaderResult(
                root.GetProperty("ProcessId").GetInt32(),
                root.GetProperty("DeliveryId").GetString()!,
                root.GetProperty("InitialStatus").GetString()!,
                root.GetProperty("PreparedStatus").GetString()!,
                NullableString(root.GetProperty("InitialSourceFingerprint")),
                root.GetProperty("PublishedStatus").GetString()!,
                root.GetProperty("PublishedAttempts").GetInt32(),
                root.GetProperty("GenerationCalls").GetInt32(),
                root.GetProperty("ConfirmedStatus").GetString()!,
                root.GetProperty("FinalStatus").GetString()!,
                root.GetProperty("IsReadable").GetBoolean(),
                NullableString(root.GetProperty("SourceFingerprint")),
                NullableString(root.GetProperty("PrivateGeneratorText")),
                root.GetProperty("ReportJson").GetString()!,
                root.GetProperty("ReportMarkdown").GetString()!);
        }
        finally
        {
            if (!reader.HasExited)
            {
                reader.Kill(entireProcessTree: true);
                await reader.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
        }
    }

    private static Process StartProbe(string repositoryRoot, string mode, string directory, string phase)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
        var dll = Path.Combine(repositoryRoot, "tools", "RunRecoveryProbe", "bin", configuration, "net10.0", "UniClaw.RunRecoveryProbe.dll");
        if (!File.Exists(dll)) throw new FileNotFoundException("RunRecoveryProbe executable was not built", dll);
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(dll);
        start.ArgumentList.Add(mode);
        start.ArgumentList.Add(directory);
        start.ArgumentList.Add(phase);
        return Process.Start(start) ?? throw new InvalidOperationException("could not start report-delivery probe");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "tools", "RunRecoveryProbe", "RunRecoveryProbe.csproj")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("repository root not found from test output");
    }

    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string? NullableString(JsonElement element) => element.ValueKind == JsonValueKind.Null ? null : element.GetString();

    private static SortedDictionary<string, string> SnapshotImmutableFiles(string storeRoot)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(storeRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(storeRoot, path);
            var name = Path.GetFileName(path);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (name is "head.json" or ".writer.lock" || name.Contains(".tmp-", StringComparison.Ordinal)
                || segments.Any(segment => segment.StartsWith(".attempt-", StringComparison.Ordinal)
                    || segment.StartsWith(".capture-", StringComparison.Ordinal)))
                continue;
            var isGenerator = name == "generator.py";
            var isVersionRecord = segments.Contains("versions", StringComparer.Ordinal)
                && Path.GetExtension(name).Equals(".json", StringComparison.OrdinalIgnoreCase);
            var isCapturedInputOrPublication = segments.Contains("inputs", StringComparer.Ordinal)
                || segments.Contains("published", StringComparer.Ordinal);
            if (isGenerator || isVersionRecord || isCapturedInputOrPublication)
                result[relative] = HashFile(path);
        }
        return result;
    }

    private sealed record ReaderResult(int ProcessId, string DeliveryId, string InitialStatus, string PreparedStatus,
        string? InitialSourceFingerprint, string PublishedStatus, int PublishedAttempts, int GenerationCalls,
        string ConfirmedStatus, string FinalStatus, bool IsReadable, string? SourceFingerprint,
        string? PrivateGeneratorText, string ReportJson, string ReportMarkdown)
    {
        public string? ReportSource => JsonDocument.Parse(ReportJson).RootElement.GetProperty("source").GetString();
    }
}
