using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UniClaw.Host.Runtime;

public sealed record CoverageReportDeliveryOptions
{
    public ReportDeliveryStorageOptions Storage { get; init; } = new();
    public ReportDeliveryLimits Limits { get; init; } = new();
    public IReadOnlyList<ReportAttachmentRequirement> Attachments { get; init; } = [];
    public string? RetainedCaptureDirectory { get; init; }
}

public sealed record CoverageReportSourceIdentity(string RunId, string RunRecordingId, string EvidenceRecordingId, string WorldRecordingId);
public sealed record CoverageReportMaterial(CoverageReportSourceIdentity Identity, string ReportJson, string StepsJson);
public sealed record CoverageReportDeliveryResult(ReportDeliverySnapshot Delivery, string? ProjectionProblem = null);

/// <summary>Delivery of the existing Coverage JSON outputs only. No task, Agent, Effect, or Ledger execution.</summary>
public sealed partial class CoverageReportDelivery : IAsyncDisposable
{
    public const string ReportFileName = "coverage-report.json";
    public const string StepsFileName = "coverage-steps.json";
    public const string SourceFileName = "coverage-source.json";
    public const string BundleFileName = "coverage-attachments.zip";
    public const string OwnerPrefix = "coverage-run:";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _productRoot, _storeRoot;
    private readonly CoverageReportDeliveryOptions _options;
    private readonly RequiredReportDelivery _store;
    private readonly RequiredReportDeliveryProcess _process;
    private readonly bool _ownsProcess;

    public CoverageReportDelivery(string productRoot, CoverageReportDeliveryOptions? options = null,
        RequiredReportDeliveryProcess? process = null)
    {
        _productRoot = Path.GetFullPath(productRoot);
        var selected = options ?? new();
        _options = selected with { Storage = selected.Storage with { }, Limits = selected.Limits with { }, Attachments = selected.Attachments.ToArray() };
        _storeRoot = RequiredReportDeliveryPaths.ResolveStoreRoot(_productRoot, _options.Storage);
        _store = new(_storeRoot, _options.Limits);
        _process = process ?? new(operationTimeout: _options.Limits.GenerationTimeout);
        _ownsProcess = process is null;
    }

    public static string Owner(string runDirectoryName)
    {
        RequiredReportDeliveryPaths.ValidateRelativePath(runDirectoryName);
        if (Path.GetFileName(runDirectoryName) != runDirectoryName) throw new ArgumentException("coverage-run-name-required");
        return OwnerPrefix + runDirectoryName;
    }

    public async Task<ReportDeliveryRecord> RequireAsync(string runDirectoryName, string requestId = "primary",
        CancellationToken cancellationToken = default)
    {
        var owner = Owner(runDirectoryName);
        var source = RequiredReportDeliveryPaths.ResolveSourcePath(_productRoot, runDirectoryName);
        var old = (await _store.ListAsync(owner, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(item => item.Record.Request.RequestId == requestId)?.Record;
        if (old is not null)
        {
            if (old.Request.ReportKind != "coverage-report") throw new InvalidOperationException("report-request-conflict");
            return await _store.BindSourceAsync(old.DeliveryId, source, runDirectoryName, cancellationToken).ConfigureAwait(false);
        }
        var generator = await InstallGeneratorAsync(cancellationToken).ConfigureAwait(false);
        var attachments = _options.Attachments.ToList();
        if (_options.RetainedCaptureDirectory is { } capture)
            attachments.Add(new(capture, "configured-capture-retention", RetainAfterDelivery: true, Directory: true));
        var contract = new ReportInputContract("uniclaw.coverage-report.inputs.v1", "coverage-report", 1,
            [new("coverage-source", SourceFileName, true, true), new("coverage-attachments", BundleFileName, true, true)],
            [ReportFileName, StepsFileName], attachments);
        ReportProtectedSource[] retained = [new(SourceFileName, "original-coverage-projection", true),
            new(BundleFileName, "fixed-coverage-sources", true), new(ReportFileName, "coverage-result", true), new(StepsFileName, "coverage-step-proof", true)];
        var accepted = await _store.AcceptAsync(new(owner, requestId, "coverage-report", generator, contract,
            ExpectedGeneratorSha256: await HashFileAsync(generator, cancellationToken).ConfigureAwait(false), ProtectedSources: retained), cancellationToken).ConfigureAwait(false);
        return await _store.BindSourceAsync(accepted.DeliveryId, source, runDirectoryName, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CoverageReportDeliveryResult> CaptureAndDeliverAsync(string deliveryId, CoverageReportMaterial material,
        CancellationToken cancellationToken = default)
    {
        var captured = await CaptureAsync(deliveryId, material, cancellationToken).ConfigureAwait(false);
        if (captured.Record.SourceFingerprint is null) return new(captured);
        return await RetryAsync(deliveryId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Fix the original producer outputs before generation; uses the existing Ready state.</summary>
    public async Task<ReportDeliverySnapshot> CaptureAsync(string deliveryId, CoverageReportMaterial material,
        CancellationToken cancellationToken = default)
    {
        var row = await RowAsync(deliveryId, cancellationToken).ConfigureAwait(false);
        if (row.SourceFingerprint is null)
        {
            try
            {
                using var lease = SourceLease(row);
                await SaveMaterialAsync(row, material, cancellationToken).ConfigureAwait(false);
                await EnsureBundleAsync(row, cancellationToken).ConfigureAwait(false);
                await _store.PrepareAsync(deliveryId, row.SourceDirectory!, row.SourceLabel!, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) when (SourceError(error))
            {
                await _store.RecordSourceFailureAsync(deliveryId, SourceReason(error), CancellationToken.None).ConfigureAwait(false);
                return (await _store.InspectAsync(deliveryId, CancellationToken.None).ConfigureAwait(false))!;
            }
        }
        return (await _store.InspectAsync(deliveryId, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<CoverageReportDeliveryResult> RetryAsync(string deliveryId, CancellationToken cancellationToken = default)
    {
        var row = await RowAsync(deliveryId, cancellationToken).ConfigureAwait(false);
        if (row.SourceDirectory is null)
        {
            var name = row.Request.OwnerRef[OwnerPrefix.Length..];
            row = await _store.BindSourceAsync(deliveryId, RequiredReportDeliveryPaths.ResolveSourcePath(_productRoot, name), name, cancellationToken).ConfigureAwait(false);
        }
        if (row.SourceFingerprint is null)
        {
            try
            {
                using var lease = SourceLease(row);
                await EnsureBundleAsync(row, cancellationToken).ConfigureAwait(false);
                row = await _store.PrepareAsync(deliveryId, row.SourceDirectory!, row.SourceLabel!, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) when (SourceError(error))
            {
                await _store.RecordSourceFailureAsync(deliveryId, SourceReason(error), CancellationToken.None).ConfigureAwait(false);
                return new((await _store.InspectAsync(deliveryId, CancellationToken.None).ConfigureAwait(false))!);
            }
        }
        if (row.SourceFingerprint is not null && row.Status != ReportDeliveryStatus.Delivered)
        {
            row = await _store.PublishAsync(deliveryId, async (context, token) =>
            {
                var generated = await _process.RunAsync(context.GeneratorPath,
                    ["--run-dir", context.InputDirectory, "--out-dir", context.OutputDirectory, "--source-label", context.SourceLabel], token,
                    Path.Combine(Path.GetDirectoryName(context.GeneratorPath)!, ".generator.lock")).ConfigureAwait(false);
                return new(generated.ExitCode, generated.Stderr);
            }, cancellationToken).ConfigureAwait(false);
            if (row.Status is ReportDeliveryStatus.Published or ReportDeliveryStatus.Delivered)
                row = await _store.ConfirmAsync(deliveryId, cancellationToken).ConfigureAwait(false);
        }
        var snapshot = (await _store.InspectAsync(deliveryId, cancellationToken).ConfigureAwait(false))!;
        string? projection = null;
        if (snapshot.IsReadable && snapshot.Record.Status == ReportDeliveryStatus.Delivered && Directory.Exists(row.SourceDirectory))
        {
            try
            {
                foreach (var output in row.Outputs!)
                {
                    var bytes = await File.ReadAllBytesAsync(Path.Combine(_store.OutputDirectory(deliveryId), output.FileName), cancellationToken).ConfigureAwait(false);
                    await WriteOriginalAsync(RequiredReportDeliveryPaths.ResolveSourcePath(row.SourceDirectory!, output.FileName), bytes, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception error) when (SourceError(error)) { projection = SourceReason(error); }
        }
        return new(snapshot, projection);
    }

    public Task<ReportDeliverySnapshot?> InspectAsync(string deliveryId, CancellationToken token = default) => _store.InspectAsync(deliveryId, token);
    public Task<IReadOnlyList<ReportProtectedSource>> GetProtectedSourcesAsync(string deliveryId, CancellationToken token = default) => _store.GetProtectedSourcesAsync(deliveryId, token);
    public string OutputDirectory(string deliveryId) => _store.OutputDirectory(deliveryId);

    private async Task<ReportDeliveryRecord> RowAsync(string id, CancellationToken token)
    {
        var row = (await _store.InspectAsync(id, token).ConfigureAwait(false))?.Record ?? throw new KeyNotFoundException("report-delivery-not-found");
        if (row.Request.ReportKind != "coverage-report" || !row.Request.OwnerRef.StartsWith(OwnerPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("coverage-report-owner-required");
        _ = Owner(row.Request.OwnerRef[OwnerPrefix.Length..]);
        return row;
    }

    private static bool SourceError(Exception error) => error is IOException or UnauthorizedAccessException or InvalidDataException
        or JsonException or InvalidOperationException or ArgumentException or OperationCanceledException;
    private static string SourceReason(Exception error) => (error.GetType().Name + ":" + error.Message) is { Length: > 1024 } value ? value[..1024] : error.GetType().Name + ":" + error.Message;
    private static async Task<string> HashFileAsync(string path, CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false)).ToLowerInvariant();
    }
    public async ValueTask DisposeAsync() { if (_ownsProcess) await _process.DisposeAsync().ConfigureAwait(false); }
}
