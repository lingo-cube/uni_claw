using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Host.Runtime;
using UniClaw.Host.SettingsCoverage;

internal static class CoverageReportProbe
{
    private const string RunName = "run-coverage-original";
    private const string DeliveryIdFile = "coverage-delivery-id.txt";
    private const string StageFile = "coverage-stage.json";
    private const string GeneratorEnteredFile = "generator-entered.json";
    private const string ReleaseFile = "release.flag";
    private const string RetryFile = "coverage-retry-result.json";
    // Product material is the exact Web JSON expected by the pinned coverage generator.
    // Probe control/result files deliberately retain their C# field names for the process tests.
    private static readonly JsonSerializerOptions MaterialJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly JsonSerializerOptions ControlJson = new() { WriteIndented = true };

    public static async Task Run(string mode, string directory, string stage)
    {
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        if (mode == "write-coverage-delivery")
        {
            await WriteAsync(directory, stage).ConfigureAwait(false);
            return;
        }
        if (mode == "retry-coverage-delivery")
        {
            await RetryAsync(directory).ConfigureAwait(false);
            return;
        }
        throw new ArgumentException("coverage-delivery-mode-required", nameof(mode));
    }

    private static async Task WriteAsync(string directory, string stage)
    {
        if (stage is not ("accepted" or "inputs" or "generating" or "published"))
            throw new ArgumentException("coverage-delivery-stage-required", nameof(stage));

        var original = Directory.CreateDirectory(Path.Combine(directory, RunName)).FullName;
        var options = new CoverageReportDeliveryOptions { Storage = new() { StoreRelativePath = "coverage-delivery" } };
        RequiredReportDeliveryProcess? sharedProcess = null;
        if (stage == "generating")
            sharedProcess = new RequiredReportDeliveryProcess(pythonExecutable:
                await WriteBarrierProcessProxyAsync(directory).ConfigureAwait(false));
        var material = Material();
        try
        {
            await using var delivery = new CoverageReportDelivery(directory, options, sharedProcess);
            var accepted = await delivery.RequireAsync(RunName).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(directory, DeliveryIdFile), accepted.DeliveryId).ConfigureAwait(false);

            if (stage == "accepted")
            {
                await SignalStageAsync(directory, stage, accepted, material, delivery, original).ConfigureAwait(false);
                await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
                return;
            }

            var ready = await delivery.CaptureAsync(accepted.DeliveryId, material).ConfigureAwait(false);
            if (ready.Record.Status != ReportDeliveryStatus.Ready || ready.Record.SourceFingerprint is null)
                throw new InvalidOperationException("coverage-inputs-not-fixed");
            await WriteSourceSnapshotAsync(directory, original).ConfigureAwait(false);

            if (stage == "inputs")
            {
                await SignalStageAsync(directory, stage, ready.Record, material, delivery, original).ConfigureAwait(false);
                await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
                return;
            }

            if (stage == "generating")
            {
                var publishing = delivery.RetryAsync(accepted.DeliveryId);
                await WaitForFileAsync(Path.Combine(directory, GeneratorEnteredFile), TimeSpan.FromSeconds(35)).ConfigureAwait(false);
                var generating = (await delivery.InspectAsync(accepted.DeliveryId).ConfigureAwait(false))?.Record
                    ?? throw new InvalidDataException("coverage-delivery-disappeared");
                if (generating.Status != ReportDeliveryStatus.Generating) throw new InvalidOperationException("coverage-not-generating");
                await SignalStageAsync(directory, stage, generating, material, delivery, original).ConfigureAwait(false);
                await publishing.ConfigureAwait(false);
                return;
            }

            // This is deliberately the same RequiredReportDelivery.PublishAsync callback used by CoverageReportDelivery.
            // Do not Confirm: the process-kill window must be a real published-but-unconfirmed record.
            var store = new RequiredReportDelivery(Path.Combine(directory, "coverage-delivery"));
            var process = new RequiredReportDeliveryProcess();
            try
            {
                var published = await store.PublishAsync(accepted.DeliveryId, async (context, token) =>
                {
                    var result = await process.RunAsync(context.GeneratorPath,
                        ["--run-dir", context.InputDirectory, "--out-dir", context.OutputDirectory, "--source-label", context.SourceLabel], token,
                        Path.Combine(Path.GetDirectoryName(context.GeneratorPath)!, ".generator.lock")).ConfigureAwait(false);
                    return new(result.ExitCode, result.Stderr);
                }).ConfigureAwait(false);
                if (published.Status != ReportDeliveryStatus.Published) throw new InvalidOperationException("coverage-not-published");
                await SignalStageAsync(directory, stage, published, material, delivery, original).ConfigureAwait(false);
                await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
            }
            finally { await process.DisposeAsync().ConfigureAwait(false); }
        }
        finally { if (sharedProcess is not null) await sharedProcess.DisposeAsync().ConfigureAwait(false); }
    }

    private static async Task RetryAsync(string directory)
    {
        var id = (await File.ReadAllTextAsync(Path.Combine(directory, DeliveryIdFile)).ConfigureAwait(false)).Trim();
        var options = new CoverageReportDeliveryOptions { Storage = new() { StoreRelativePath = "coverage-delivery" } };
        await using var delivery = new CoverageReportDelivery(directory, options);
        var result = await delivery.RetryAsync(id).ConfigureAwait(false);
        var snapshot = result.Delivery;
        var input = Path.Combine(Path.GetDirectoryName(delivery.OutputDirectory(id))!, "inputs");
        var artifacts = new Dictionary<string, string>(StringComparer.Ordinal);
        if (snapshot.IsReadable)
        {
            foreach (var name in new[] { CoverageReportDelivery.ReportFileName, CoverageReportDelivery.StepsFileName })
                artifacts[name] = Hash(await File.ReadAllBytesAsync(Path.Combine(delivery.OutputDirectory(id), name)).ConfigureAwait(false));
        }
        await AtomicJsonAsync(Path.Combine(directory, RetryFile), new
        {
            DeliveryId = id,
            HostProcessId = Environment.ProcessId,
            Status = snapshot.Record.Status.ToString(),
            Readable = snapshot.IsReadable,
            Attempts = snapshot.Record.Attempts,
            SourceFingerprint = snapshot.Record.SourceFingerprint,
            ArtifactSha256 = artifacts,
            InputReportSha256 = File.Exists(Path.Combine(input, CoverageReportDelivery.SourceFileName))
                ? Hash(await File.ReadAllBytesAsync(Path.Combine(input, CoverageReportDelivery.SourceFileName)).ConfigureAwait(false)) : null,
            InputBundleSha256 = File.Exists(Path.Combine(input, CoverageReportDelivery.BundleFileName))
                ? Hash(await File.ReadAllBytesAsync(Path.Combine(input, CoverageReportDelivery.BundleFileName)).ConfigureAwait(false)) : null,
            snapshot.IntegrityProblem,
            ProjectionProblem = result.ProjectionProblem,
        }).ConfigureAwait(false);
    }

    private static CoverageReportMaterial Material()
    {
        var report = new CoverageReport("BoundedStop", [new("root", "controlled", false, null)], 0, 0,
            ["root"], "controlled-stop", 0, 0, 0, 0, 1);
        var steps = new[] { new { stepId = "original-step", outcome = "verified", source = "original-ledger" } };
        return new(new("controlled-run", Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")),
            JsonSerializer.Serialize(report, MaterialJson), JsonSerializer.Serialize(steps, MaterialJson));
    }

    private static async Task<string> WriteBarrierProcessProxyAsync(string directory)
    {
        var path = Path.Combine(directory, "coverage-barrier-python.py");
        var root = JsonSerializer.Serialize(directory);
        var script = "#!/usr/bin/env python3\nimport json, os, pathlib, sys, time\n" +
            "root = pathlib.Path(" + root + ")\n" +
            "marker = root / '" + GeneratorEnteredFile + "'\n" +
            "temporary = marker.with_suffix('.tmp-' + str(os.getpid()))\n" +
            "temporary.write_text(json.dumps({'generatorPid': os.getpid(), 'hostDirectory': str(root)}), encoding='utf-8')\n" +
            "os.replace(temporary, marker)\n" +
            "while not (root / '" + ReleaseFile + "').exists(): time.sleep(0.02)\n" +
            "os.execvp('python3', ['python3'] + sys.argv[1:])\n";
        // A shebang must be the first bytes of the executable, before any UTF-8 BOM.
        await File.WriteAllTextAsync(path, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)).ConfigureAwait(false);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        return path;
    }

    private static async Task SignalStageAsync(string directory, string stage, ReportDeliveryRecord record,
        CoverageReportMaterial material, CoverageReportDelivery delivery, string original)
    {
        await AtomicJsonAsync(Path.Combine(directory, StageFile), new
        {
            Stage = stage,
            DeliveryId = record.DeliveryId,
            HostProcessId = Environment.ProcessId,
            Status = record.Status.ToString(),
            record.Attempts,
            record.SourceFingerprint,
            ExpectedReportSha256 = Hash(Encoding.UTF8.GetBytes(material.ReportJson)),
            ExpectedStepsSha256 = Hash(Encoding.UTF8.GetBytes(material.StepsJson)),
            OriginalDirectory = original,
            PrivateInputDirectory = Path.Combine(Path.GetDirectoryName(delivery.OutputDirectory(record.DeliveryId))!, "inputs"),
        }).ConfigureAwait(false);
    }

    private static async Task WriteSourceSnapshotAsync(string directory, string original)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(original, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).Equals(".coverage-source.lock", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal))
            facts.Add(Path.GetRelativePath(original, path).Replace(Path.DirectorySeparatorChar, '/'),
                Hash(await File.ReadAllBytesAsync(path).ConfigureAwait(false)));
        await AtomicJsonAsync(Path.Combine(directory, "coverage-original-facts.json"), facts).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(directory, "coverage-fixture-control.json"), JsonSerializer.Serialize(new
        {
            Fixture = "coverage-report-crash-probe-v1",
            OriginalDirectory = original,
        }, ControlJson)).ConfigureAwait(false);
    }

    private static async Task WaitForFileAsync(string path, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!File.Exists(path) && DateTime.UtcNow < deadline) await Task.Delay(20).ConfigureAwait(false);
        if (!File.Exists(path)) throw new TimeoutException("coverage-probe-marker-timeout:" + Path.GetFileName(path));
    }

    private static async Task AtomicJsonAsync<T>(string path, T value)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value, ControlJson)).ConfigureAwait(false);
        File.Move(temporary, path, true);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
