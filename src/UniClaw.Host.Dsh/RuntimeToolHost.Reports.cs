using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using UniClaw.Host.Runtime;

namespace UniClaw.Host.Dsh;

public sealed partial class RuntimeToolHost
{
    private readonly ConcurrentDictionary<string, RequiredReportDelivery> _reportStores = new(StringComparer.Ordinal);
    private readonly RequiredReportDeliveryProcess _executor = new();
    public sealed record ReportProcessDrain(bool Settled, IReadOnlyList<string> UnconfirmedOperations);
    private static readonly JsonSerializerOptions ReportJson = new() { PropertyNameCaseInsensitive = true };
    private RequiredReportDelivery ReportStore(string runsRoot) => _reportStores.GetOrAdd(
        RequiredReportDeliveryPaths.ResolveStoreRoot(runsRoot, _reportStorage), root => new RequiredReportDelivery(root));
    public sealed record RequiredReportResult(ReportDeliverySnapshot Delivery, InvokeResult Result)
    {
        public bool Ok => Delivery.Record.Status == ReportDeliveryStatus.Delivered && Delivery.IsReadable && Result.Ok;
    }

    public async Task<RequiredReportResult> InvokeRequiredReportAsync(string runsRoot, string runDirName,
        string requestId = "primary", bool strict = false, CancellationToken cancellationToken = default)
    {
        _ = ResolveRunDir(runsRoot, runDirName);
        var requirement = await RequireReportAsync(runsRoot,
            "artifact-set:" + SanitizeLabel(runDirName), requestId, strict, cancellationToken);
        return await RetryRequiredReportAsync(runsRoot, requirement.DeliveryId, runDirName, cancellationToken);
    }

    public async Task<ReportDeliveryRecord> RequireReportAsync(string runsRoot, string ownerRef,
        string requestId = "primary", bool strict = false, CancellationToken cancellationToken = default)
    {
        var store = ReportStore(runsRoot);
        var existing = (await store.ListAsync(ownerRef, cancellationToken)).FirstOrDefault(x => x.Record.Request.RequestId == requestId);
        if (existing is not null)
        {
            if (existing.Record.Request.Strict != strict || existing.Record.Request.ReportKind != "run-report")
                throw new InvalidOperationException("report-request-conflict");
            return existing.Record;
        }
        var tool = Find("run-report") ?? throw new InvalidOperationException("tool-not-found");
        if (tool.Status != "implemented" || tool.Invocation != "deterministic-script" || tool.Posture != "local-write"
            || !tool.Surfaces.Contains("workbench", StringComparer.Ordinal) || string.IsNullOrWhiteSpace(tool.Entry))
            throw new InvalidOperationException("tool-not-invokable: run-report requires a local-write deterministic workbench entry");
        var entry = Path.GetFullPath(Path.Combine(_repoRoot, tool.Entry));
        var before = await GeneratorHashAsync(entry, cancellationToken);
        var described = await RunReportScriptAsync(entry, ["--describe-inputs"], cancellationToken);
        if (described.ExitCode != 0) throw new InvalidOperationException("report-input-contract-failed:" + described.Stderr);
        var inputs = JsonSerializer.Deserialize<ReportInputContract>(described.Stdout, ReportJson)
            ?? throw new InvalidDataException("report-input-contract-empty");
        if (before != await GeneratorHashAsync(entry, cancellationToken))
            throw new InvalidDataException("report-generator-changed-during-description");
        return await store.AcceptAsync(new(ownerRef, requestId, "run-report", entry, inputs, strict, before), cancellationToken);
    }

    public async Task<RequiredReportResult> RetryRequiredReportAsync(string runsRoot, string deliveryId,
        string? runDirName = null, CancellationToken cancellationToken = default)
    {
        var store = ReportStore(runsRoot);
        var snapshot = await store.InspectAsync(deliveryId, cancellationToken)
            ?? throw new KeyNotFoundException("report-delivery-not-found");
        var row = snapshot.Record;
        if (row.Request.ReportKind == "coverage-report")
        {
            await using var coverage = new CoverageReportDelivery(runsRoot, new() { Storage = _reportStorage }, _executor);
            var delivered = await coverage.RetryAsync(deliveryId, cancellationToken);
            var current = delivered.Delivery;
            var success = current.Record.Status == ReportDeliveryStatus.Delivered && current.IsReadable;
            string? Reference(string name) => success && current.Record.Outputs!.Any(file => file.FileName == name)
                ? Relative(runsRoot, Path.Combine(coverage.OutputDirectory(deliveryId), name)) : null;
            return new(current, new(success ? 0 : 1, "", current.IntegrityProblem ?? current.Record.Reason ?? delivered.ProjectionProblem ?? "",
                Reference(CoverageReportDelivery.ReportFileName), null));
        }
        if (row.SourceFingerprint is null)
        {
            var source = runDirName is not null ? ResolveRunDir(runsRoot, runDirName) : row.SourceDirectory;
            if (source is null) return new(snapshot, new(1, "", "report-source-required", null, null));
            await ValidateReportOwnerAsync(runsRoot, row.Request, source, cancellationToken);
            var label = row.SourceLabel ?? Path.GetFileName(source);
            row = await store.PrepareAsync(deliveryId, source, label, cancellationToken);
        }
        (int ExitCode, string Stdout, string Stderr) process = (0, "", "");
        if (row.SourceFingerprint is not null && row.Status != ReportDeliveryStatus.Delivered)
        {
            row = await store.PublishAsync(deliveryId, async (context, token) =>
            {
                var args = new List<string> { "--run-dir", context.InputDirectory, "--out-dir", context.OutputDirectory,
                    "--source-label", context.SourceLabel };
                if (context.Strict) args.Add("--strict");
                await ValidateReportOwnerAsync(runsRoot, row.Request, context.InputDirectory, token, row.SourceDirectory);
                process = await RunReportScriptAsync(context.GeneratorPath, args.ToArray(), token,
                    Path.Combine(Path.GetDirectoryName(context.GeneratorPath)!, ".generator.lock"));
                return new(process.ExitCode, process.Stderr);
            }, cancellationToken);
            if (row.Status is ReportDeliveryStatus.Published or ReportDeliveryStatus.Delivered)
                row = await store.ConfirmAsync(deliveryId, cancellationToken);
        }
        snapshot = (await store.InspectAsync(deliveryId, cancellationToken))!;
        var readable = snapshot.IsReadable && row.Status == ReportDeliveryStatus.Delivered;
        string? Ref(string name) => readable && row.Outputs!.Any(x => x.FileName == name)
            ? Relative(runsRoot, Path.Combine(store.OutputDirectory(deliveryId), name)) : null;
        // Legacy diagnosis consumes this projection. The verified publication remains authoritative.
        if (readable && row.SourceDirectory is { } original && Directory.Exists(original))
        {
            try
            {
                var output = ResolveOutputDir(runsRoot, Path.GetFileName(original), original);
                Directory.CreateDirectory(output);
                foreach (var file in row.Outputs!)
                {
                    var temporary = Path.Combine(output, ".report-" + Guid.NewGuid().ToString("N"));
                    try { File.Copy(Path.Combine(store.OutputDirectory(deliveryId), file.FileName), temporary);
                        File.Move(temporary, Path.Combine(output, file.FileName), overwrite: true); }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
            { process.Stderr = "report-legacy-projection-failed:" + error.Message; }
        }
        return new(snapshot, new(readable ? 0 : process.ExitCode == 0 ? 1 : process.ExitCode,
            process.Stdout, snapshot.IntegrityProblem ?? row.Reason ?? process.Stderr, Ref("report.json"), Ref("report.md")));
    }

    public Task<ReportDeliverySnapshot?> GetReportDeliveryAsync(string runsRoot, string deliveryId,
        CancellationToken cancellationToken = default) => Directory.Exists(RequiredReportDeliveryPaths.ResolveStoreRoot(runsRoot, _reportStorage))
            ? ReportStore(runsRoot).InspectAsync(deliveryId, cancellationToken) : Task.FromResult<ReportDeliverySnapshot?>(null);

    public Task<IReadOnlyList<ReportDeliverySnapshot>> ListRequiredReportsAsync(string runsRoot, string ownerRef,
        CancellationToken cancellationToken = default) => Directory.Exists(RequiredReportDeliveryPaths.ResolveStoreRoot(runsRoot, _reportStorage))
            ? ReportStore(runsRoot).ListAsync(ownerRef, cancellationToken)
            : Task.FromResult<IReadOnlyList<ReportDeliverySnapshot>>(Array.Empty<ReportDeliverySnapshot>());

    public Task<IReadOnlyList<ReportProtectedSource>> GetReportSourceProtectionAsync(string runsRoot, string deliveryId,
        CancellationToken cancellationToken = default) => Directory.Exists(RequiredReportDeliveryPaths.ResolveStoreRoot(runsRoot, _reportStorage))
            ? ReportStore(runsRoot).GetProtectedSourcesAsync(deliveryId, cancellationToken)
            : Task.FromResult<IReadOnlyList<ReportProtectedSource>>([]);

    internal string ResolveRequiredReportArtifact(string runsRoot, string deliveryId, string fileName) =>
        RequiredReportDeliveryPaths.ResolveSourcePath(ReportStore(runsRoot).OutputDirectory(deliveryId), fileName);

    private static async Task<string> GeneratorHashAsync(string path, CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        if (stream.Length > 1024 * 1024) throw new InvalidDataException("report-generator-too-large");
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).ToLowerInvariant();
    }

    private static async Task ValidateReportOwnerAsync(string runsRoot, ReportDeliveryRequest request, string directory,
        CancellationToken token, string? originalDirectory = null)
    {
        if (request.OwnerRef.StartsWith("artifact-set:", StringComparison.Ordinal))
        {
            // RUNTIME-TASK-001 S2-8：ownerRef 后缀是标签（路径分隔符已替换为
            // _）；验证时以源目录相对 runsRoot 的真实路径做同一替换后比对
            //（路径不撒谎：比较基准仍是磁盘真相）。
            var source = originalDirectory ?? directory;
            var relative = Path.GetRelativePath(Path.GetFullPath(runsRoot), Path.GetFullPath(source));
            if (request.OwnerRef[13..] != SanitizeLabel(relative))
                throw new InvalidOperationException("report-source-owner-mismatch");
            return;
        }
        if (!request.OwnerRef.StartsWith("runtime-run:", StringComparison.Ordinal)) return;
        var facts = request.Inputs.Files.SingleOrDefault(x => x.Name == "facts")
            ?? throw new InvalidOperationException("report-source-owner-mismatch");
        var path = Path.Combine(directory, facts.FileName);
        if (!File.Exists(path)) return; // Prepare records the required-source failure.
        await using var stream = File.OpenRead(path);
        if (stream.Length > 32 * 1024 * 1024) throw new InvalidDataException("report-source-byte-limit");
        // Keep this identity check bounded even if an external writer changes the original file.
        using var bytes = new MemoryStream(); var buffer = new byte[65536]; int count;
        while ((count = await stream.ReadAsync(buffer, token)) > 0)
        {
            if (bytes.Length + count > 32 * 1024 * 1024) throw new InvalidDataException("report-source-byte-limit");
            await bytes.WriteAsync(buffer.AsMemory(0, count), token);
        }
        using var parsed = JsonDocument.Parse(bytes.ToArray());
        if (parsed.RootElement.ValueKind != JsonValueKind.Object
            || !parsed.RootElement.TryGetProperty("runId", out var runId) || runId.ValueKind != JsonValueKind.String
            || runId.GetString() != request.OwnerRef[12..])
            throw new InvalidOperationException("report-source-owner-mismatch");
    }

    public async Task<ReportProcessDrain> CloseReportProcessesAsync(TimeSpan timeout)
    {
        var drain = await _executor.CloseAsync(timeout).ConfigureAwait(false);
        return new(drain.Settled, drain.UnconfirmedOperations);
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunReportScriptAsync(
        string entry, string[] arguments, CancellationToken cancellationToken, string? leasePath = null)
    {
        return await _executor.RunAsync(entry, arguments, cancellationToken, leasePath).ConfigureAwait(false);
    }
}
