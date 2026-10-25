using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniClaw.Host.Runtime;

public sealed record ReportInputFile(string Name, string FileName, bool StrictRequired, bool RequiredForDelivery = false);
public sealed record ReportInputContract([property: JsonPropertyName("schemaVersion")] string Schema, string GeneratorName, int GeneratorVersion,
    IReadOnlyList<ReportInputFile> Files, IReadOnlyList<string> OutputFiles,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ReportAttachmentRequirement>? Attachments = null);
public sealed record ReportAttachmentRequirement(string RelativePath, string Purpose, bool RequiredForDelivery = false,
    bool RetainAfterDelivery = false, string? ExpectedSha256 = null, bool Directory = false);
public sealed record ReportDeliveryRequest(string OwnerRef, string RequestId, string ReportKind,
    string GeneratorPath, ReportInputContract Inputs, bool Strict = false, string? ExpectedGeneratorSha256 = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ReportProtectedSource>? ProtectedSources = null);
public sealed record ReportProtectedSource(string RelativePath, string Purpose, bool RetainAfterDelivery = false);
public enum ReportDeliveryStatus { AwaitingInputs, Ready, Generating, Published, Delivered, Failed }
public sealed record ReportSourceSnapshot(string Name, string FileName, bool Present, long Bytes, string? Sha256);
public sealed record ReportOutputSnapshot(string FileName, long Bytes, string Sha256);
public sealed record ReportDeliveryRecord(string DeliveryId, ReportDeliveryRequest Request, string GeneratorSha256,
    ReportDeliveryStatus Status, int Revision, int Attempts, string? SourceDirectory = null, string? SourceLabel = null,
    string? SourceFingerprint = null, IReadOnlyList<ReportSourceSnapshot>? Sources = null,
    IReadOnlyList<ReportOutputSnapshot>? Outputs = null, string? Reason = null);
public sealed record ReportDeliverySnapshot(ReportDeliveryRecord Record, bool IsReadable, string? IntegrityProblem);
public sealed record ReportGenerationContext(string GeneratorPath, string InputDirectory, string OutputDirectory,
    string SourceLabel, bool Strict, ReportInputContract Inputs);
public sealed record ReportGenerationResult(int ExitCode, string? Detail = null);
public sealed record ReportDeliveryLimits
{
    public long MaxSourceFileBytes { get; init; } = 32 * 1024 * 1024;
    public long MaxSourceBytes { get; init; } = 128 * 1024 * 1024;
    public long MaxOutputFileBytes { get; init; } = 32 * 1024 * 1024;
    public int MaxGeneratorBytes { get; init; } = 1024 * 1024;
    public int MaxRecordBytes { get; init; } = 1024 * 1024;
    public int MaxConcurrentOperations { get; init; } = 4;
    public TimeSpan GenerationTimeout { get; init; } = TimeSpan.FromSeconds(120);
}

/// <summary>Host-owned report delivery only; never executes or changes the referenced task.</summary>
public sealed class RequiredReportDelivery
{
    private const string StoreSchema = "uniclaw.report-delivery.v1";
    private const string InputManifestName = ".delivery-inputs.json";
    private const string PublicationName = ".delivery-publication.json";
    private static readonly JsonSerializerOptions Json = CreateJson();
    private readonly string _root;
    private readonly ReportDeliveryLimits _limits;
    private readonly SemaphoreSlim _operations;
    private readonly ConcurrentDictionary<string, Task> _settling = new();
    private sealed record Head(string Schema, int Revision, string FileName, string Hash);
    private sealed record Frame(string Schema, string Payload, string? PreviousFile, string? PreviousHash, string Hash);
    private sealed record Inputs(string DeliveryId, string GeneratorSha256, string SourceLabel,
        ReportSourceSnapshot[] Sources, string Fingerprint);
    private sealed record Publication(string DeliveryId, string GeneratorSha256, string SourceFingerprint,
        ReportOutputSnapshot[] Outputs);
    private sealed class Lease(FileStream file, SemaphoreSlim operations) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() { try { await file.DisposeAsync().ConfigureAwait(false); } finally { operations.Release(); } }
    }

    public RequiredReportDelivery(string storageRoot, ReportDeliveryLimits? limits = null)
    {
        _root = Path.GetFullPath(storageRoot);
        _limits = limits ?? new();
        if (_limits.MaxSourceFileBytes <= 0 || _limits.MaxSourceBytes <= 0 || _limits.MaxOutputFileBytes <= 0
            || _limits.MaxGeneratorBytes <= 0 || _limits.MaxRecordBytes <= 0 || _limits.MaxConcurrentOperations < 1
            || _limits.GenerationTimeout <= TimeSpan.Zero || _limits.GenerationTimeout > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(limits));
        _operations = new(_limits.MaxConcurrentOperations, _limits.MaxConcurrentOperations);
        Directory.CreateDirectory(_root);
    }

    public async Task<ReportDeliveryRecord> AcceptAsync(ReportDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequest(request);
        request = Deserialize<ReportDeliveryRequest>(Serialize(request)); // Freeze caller-owned lists before I/O.
        request = request with { GeneratorPath = Path.GetFullPath(request.GeneratorPath) };
        var id = "report-" + Hash(Encoding.UTF8.GetBytes(Serialize(new { request.OwnerRef, request.RequestId })));
        Directory.CreateDirectory(Job(id));
        await using var lease = Acquire(id);
        if (File.Exists(Path.Combine(Job(id), "head.json")))
        {
            var old = await LoadAsync(id).ConfigureAwait(false);
            if (Serialize(old.Request) != Serialize(request)) throw new InvalidOperationException("report-request-conflict");
            return old;
        }
        var generator = await ReadBoundedAsync(request.GeneratorPath, _limits.MaxGeneratorBytes, cancellationToken).ConfigureAwait(false);
        var hash = Hash(generator);
        if (request.ExpectedGeneratorSha256 is { } expected && expected != hash)
            throw new InvalidDataException("report-generator-changed-during-acceptance");
        // Acceptance is acknowledged only after both the pinned implementation and the head commit.
        await AtomicWriteAsync(Path.Combine(Job(id), "generator.py"), generator).ConfigureAwait(false);
        return await CommitAsync(new(id, request, hash, ReportDeliveryStatus.AwaitingInputs, 1, 0), null).ConfigureAwait(false);
    }

    public async Task<ReportDeliverySnapshot?> InspectAsync(string deliveryId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(Path.Combine(Job(deliveryId), "head.json"))) return null;
        var row = await LoadAsync(deliveryId).ConfigureAwait(false);
        if (row.Status is not (ReportDeliveryStatus.Published or ReportDeliveryStatus.Delivered)) return new(row, false, null);
        try
        {
            await ReadPublicationAsync(row, cancellationToken).ConfigureAwait(false);
            return new(row, true, null);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        { return new(row, false, Reason(error)); }
    }

    public async Task<IReadOnlyList<ReportDeliverySnapshot>> ListAsync(string ownerRef, CancellationToken cancellationToken = default)
    {
        var result = new List<ReportDeliverySnapshot>();
        foreach (var path in Directory.EnumerateDirectories(_root, "report-*").Order(StringComparer.Ordinal))
        {
            var item = await InspectAsync(Path.GetFileName(path), cancellationToken).ConfigureAwait(false);
            if (item?.Record.Request.OwnerRef == ownerRef) result.Add(item);
        }
        return result;
    }

    public async Task<ReportDeliveryRecord> PrepareAsync(string deliveryId, string sourceDirectory, string sourceLabel,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var lease = Acquire(deliveryId);
        var row = await LoadAsync(deliveryId).ConfigureAwait(false);
        row = await BindSourceAsync(row, sourceDirectory, sourceLabel).ConfigureAwait(false);
        sourceDirectory = row.SourceDirectory!;
        if (row.SourceFingerprint is not null)
        {
            await VerifyInputsAsync(row, cancellationToken).ConfigureAwait(false);
            return row;
        }
        var staging = Path.Combine(Job(deliveryId), ".capture-" + Guid.NewGuid().ToString("N"));
        try
        {
            var destination = Path.Combine(Job(deliveryId), "inputs");
            Inputs manifest;
            if (Directory.Exists(destination))
                manifest = Deserialize<Inputs>(await ReadTextAsync(Path.Combine(destination, InputManifestName), cancellationToken).ConfigureAwait(false));
            else
            {
                Directory.CreateDirectory(staging);
                var sources = new List<ReportSourceSnapshot>();
                long total = 0;
                foreach (var spec in row.Request.Inputs.Files)
                {
                    var source = RequiredReportDeliveryPaths.ResolveSourcePath(sourceDirectory, spec.FileName);
                    if (!File.Exists(source))
                    {
                        if (spec.RequiredForDelivery || (row.Request.Strict && spec.StrictRequired))
                            throw new InvalidDataException("report-source-missing:" + spec.Name);
                        sources.Add(new(spec.Name, spec.FileName, false, 0, null));
                        continue;
                    }
                    var captured = await CaptureFileAsync(source, Path.Combine(staging, spec.FileName),
                        Math.Min(_limits.MaxSourceFileBytes, _limits.MaxSourceBytes - total), cancellationToken).ConfigureAwait(false);
                    total += captured.Bytes;
                    if (spec.RequiredForDelivery && Path.GetExtension(spec.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        using var input = JsonDocument.Parse(await ReadBoundedAsync(Path.Combine(staging, spec.FileName),
                            _limits.MaxSourceFileBytes, cancellationToken).ConfigureAwait(false));
                        if (input.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("report-required-source-shape:" + spec.Name);
                    }
                    sources.Add(new(spec.Name, spec.FileName, true, captured.Bytes, captured.Sha256));
                }
                manifest = new(deliveryId, row.GeneratorSha256, sourceLabel, sources.ToArray(),
                    Fingerprint(row, sources));
                await AtomicWriteAsync(Path.Combine(staging, InputManifestName), Encoding.UTF8.GetBytes(Serialize(manifest))).ConfigureAwait(false);
                Directory.Move(staging, destination);
            }
            var ready = row with { SourceFingerprint = manifest.Fingerprint, Sources = manifest.Sources,
                Status = ReportDeliveryStatus.Ready, Reason = null };
            if (manifest.DeliveryId != deliveryId || manifest.GeneratorSha256 != row.GeneratorSha256 || manifest.SourceLabel != row.SourceLabel)
                throw new InvalidDataException("report-input-manifest-binding-mismatch");
            await VerifyInputsAsync(ready, cancellationToken).ConfigureAwait(false);
            return await NextAsync(ready).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or OperationCanceledException)
        { return await NextAsync(row with { Status = ReportDeliveryStatus.Failed, Reason = Reason(error) }).ConfigureAwait(false); }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); }
    }

    /// <summary>Binds the immutable report source identity without capturing inputs or advancing delivery state.</summary>
    public async Task<ReportDeliveryRecord> BindSourceAsync(string deliveryId, string sourceDirectory, string sourceLabel,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var lease = Acquire(deliveryId);
        var row = await LoadAsync(deliveryId).ConfigureAwait(false);
        return await BindSourceAsync(row, sourceDirectory, sourceLabel).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ReportProtectedSource>> GetProtectedSourcesAsync(string deliveryId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var row = await LoadAsync(deliveryId).ConfigureAwait(false);
        var protectedSources = new List<ReportProtectedSource>();
        if (row.Status != ReportDeliveryStatus.Delivered)
            protectedSources.AddRange(row.Request.Inputs.Files.Select(file =>
                new ReportProtectedSource(file.FileName, "required-report-input")));
        if (row.Request.ProtectedSources is not null
            && row.Status != ReportDeliveryStatus.Delivered)
            protectedSources.AddRange(row.Request.ProtectedSources);
        else if (row.Request.ProtectedSources is not null)
            protectedSources.AddRange(row.Request.ProtectedSources.Where(source => source.RetainAfterDelivery));
        if (row.Request.Inputs.Attachments is not null && row.Status != ReportDeliveryStatus.Delivered)
            protectedSources.AddRange(row.Request.Inputs.Attachments.Select(attachment =>
                new ReportProtectedSource(attachment.RelativePath, attachment.Purpose, attachment.RetainAfterDelivery)));
        else if (row.Request.Inputs.Attachments is not null)
            protectedSources.AddRange(row.Request.Inputs.Attachments.Where(attachment => attachment.RetainAfterDelivery)
                .Select(attachment => new ReportProtectedSource(attachment.RelativePath, attachment.Purpose, true)));

        return protectedSources
            .GroupBy(source => source.RelativePath, StringComparer.Ordinal)
            .Select(group => new ReportProtectedSource(
                group.Key,
                string.Join("; ", group.Select(source => source.Purpose).Distinct(StringComparer.Ordinal)),
                group.Any(source => source.RetainAfterDelivery)))
            .ToArray();
    }

    /// <summary>Persists a producer failure before report inputs are fixed; never changes a fixed delivery.</summary>
    public async Task<ReportDeliveryRecord> RecordSourceFailureAsync(string deliveryId, string reason,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1024)
            throw new ArgumentException("report-source-failure-reason-invalid", nameof(reason));
        await using var lease = Acquire(deliveryId);
        var row = await LoadAsync(deliveryId).ConfigureAwait(false);
        if (row.SourceFingerprint is not null)
            throw new InvalidOperationException("report-source-failure-after-inputs-fixed");
        return await NextAsync(row with { Status = ReportDeliveryStatus.Failed, Reason = reason }).ConfigureAwait(false);
    }

    public async Task<ReportDeliveryRecord> PublishAsync(string deliveryId,
        Func<ReportGenerationContext, CancellationToken, Task<ReportGenerationResult>> generate,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var lease = Acquire(deliveryId);
        var releaseHere = true;
        CancellationTokenSource? budget = null;
        Task<ReportGenerationResult>? generation = null;
        var staging = Path.Combine(Job(deliveryId), ".attempt-" + Guid.NewGuid().ToString("N"));
        ReportDeliveryRecord? row = null;
        try
        {
            row = await LoadAsync(deliveryId).ConfigureAwait(false);
            if (row.Status == ReportDeliveryStatus.Delivered)
            {
                await ReadPublicationAsync(row, cancellationToken).ConfigureAwait(false);
                return row;
            }
            if (row.SourceFingerprint is null) return row;
            await VerifyInputsAsync(row, cancellationToken).ConfigureAwait(false);
            if (Directory.Exists(OutputDirectory(deliveryId)))
            {
                var publication = await ReadPublicationAsync(row, cancellationToken).ConfigureAwait(false);
                if (row.Status is ReportDeliveryStatus.Published or ReportDeliveryStatus.Delivered) return row;
                return await NextAsync(row with { Status = ReportDeliveryStatus.Published, Outputs = publication.Outputs, Reason = null }).ConfigureAwait(false);
            }
            if (row.Status == ReportDeliveryStatus.Delivered) throw new InvalidDataException("report-delivered-output-unavailable");
            row = await NextAsync(row with { Status = ReportDeliveryStatus.Generating, Attempts = row.Attempts + 1, Reason = null }).ConfigureAwait(false);
            Directory.CreateDirectory(staging);
            budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(_limits.GenerationTimeout);
            generation = generate(new(Path.Combine(Job(deliveryId), "generator.py"), Path.Combine(Job(deliveryId), "inputs"),
                staging, row.SourceLabel!, row.Request.Strict, row.Request.Inputs), budget.Token);
            var outcome = await generation.WaitAsync(budget.Token).ConfigureAwait(false);
            if (outcome.ExitCode != 0) throw new InvalidDataException("report-generation-failed:" + outcome.ExitCode + ":" + outcome.Detail);
            await VerifyInputsAsync(row, cancellationToken).ConfigureAwait(false);
            var outputs = new List<ReportOutputSnapshot>();
            foreach (var name in row.Request.Inputs.OutputFiles)
            {
                var path = Path.Combine(staging, name);
                var data = await ReadBoundedAsync(path, _limits.MaxOutputFileBytes, cancellationToken).ConfigureAwait(false);
                if (data.Length == 0) throw new InvalidDataException("report-empty-output:" + name);
                if (Path.GetExtension(name).Equals(".json", StringComparison.OrdinalIgnoreCase))
                { using var parsed = JsonDocument.Parse(data); }
                outputs.Add(new(name, data.LongLength, Hash(data)));
            }
            var receipt = new Publication(deliveryId, row.GeneratorSha256, row.SourceFingerprint, outputs.ToArray());
            await AtomicWriteAsync(Path.Combine(staging, PublicationName), Encoding.UTF8.GetBytes(Serialize(receipt))).ConfigureAwait(false);
            Directory.Move(staging, OutputDirectory(deliveryId));
            return await NextAsync(row with { Status = ReportDeliveryStatus.Published, Outputs = receipt.Outputs }).ConfigureAwait(false);
        }
        catch (Exception error) when (row is not null && row.Status != ReportDeliveryStatus.Delivered
            && error is not OutOfMemoryException and not StackOverflowException)
        {
            return await NextAsync(row with { Status = ReportDeliveryStatus.Failed, Reason = Reason(error) }).ConfigureAwait(false);
        }
        finally
        {
            // Retain the lease only AFTER the failure commit has settled, including commit failures.
            if (generation is { IsCompleted: false } && budget is not null)
            {
                var heldBudget = budget;
                releaseHere = false; budget = null;
                _settling[deliveryId] = SettleAsync(generation, heldBudget, lease, staging);
            }
            if (releaseHere)
            {
                budget?.Dispose();
                try { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); }
                finally { await lease.DisposeAsync().ConfigureAwait(false); }
            }
        }
    }

    public async Task<ReportDeliveryRecord> ConfirmAsync(string deliveryId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var lease = Acquire(deliveryId);
        var row = await LoadAsync(deliveryId).ConfigureAwait(false);
        var publication = await ReadPublicationAsync(row, cancellationToken).ConfigureAwait(false);
        if (row.Status == ReportDeliveryStatus.Delivered) return row;
        return await NextAsync(row with { Status = ReportDeliveryStatus.Delivered, Outputs = publication.Outputs, Reason = null }).ConfigureAwait(false);
    }

    public string OutputDirectory(string deliveryId) => Path.Combine(Job(deliveryId), "published");

    private Lease Acquire(string id)
    {
        foreach (var pending in _settling)
            if (pending.Value.IsCompleted && _settling.TryRemove(pending.Key, out var settled) && settled.IsFaulted)
            { _ = settled.Exception; } // Observe cleanup failure; completed entries cannot grow without bound.
        var directory = Job(id);
        if (!_operations.Wait(0)) throw new InvalidOperationException("report-delivery-backpressure");
        try { return new(new FileStream(Path.Combine(directory, ".writer.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None, 1, FileOptions.Asynchronous), _operations); }
        catch { _operations.Release(); throw; }
    }

    private static async Task SettleAsync(Task generation, CancellationTokenSource budget, Lease lease, string staging)
    {
        try { await budget.CancelAsync().ConfigureAwait(false); await generation.ConfigureAwait(false); }
        catch (Exception) { /* Generation failure is recorded independently from the task. */ }
        finally
        {
            budget.Dispose();
            try { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); }
            finally { await lease.DisposeAsync().ConfigureAwait(false); }
        }
    }

    private async Task VerifyInputsAsync(ReportDeliveryRecord row, CancellationToken cancellation)
    {
        if (row.Sources is null || row.SourceFingerprint is null) throw new InvalidDataException("report-inputs-not-fixed");
        if (Hash(await ReadBoundedAsync(Path.Combine(Job(row.DeliveryId), "generator.py"), _limits.MaxGeneratorBytes, cancellation).ConfigureAwait(false)) != row.GeneratorSha256)
            throw new InvalidDataException("report-generator-hash-mismatch");
        if (row.Sources.Count != row.Request.Inputs.Files.Count || row.SourceFingerprint != Fingerprint(row, row.Sources))
            throw new InvalidDataException("report-input-binding-mismatch");
        for (var i = 0; i < row.Sources.Count; i++)
        {
            var source = row.Sources[i]; var spec = row.Request.Inputs.Files[i];
            if (source.Name != spec.Name || source.FileName != spec.FileName || (!source.Present && (spec.RequiredForDelivery || row.Request.Strict && spec.StrictRequired)))
                throw new InvalidDataException("report-input-catalog-mismatch");
            var path = Path.Combine(Job(row.DeliveryId), "inputs", source.FileName);
            if (!source.Present) { if (File.Exists(path)) throw new InvalidDataException("report-absent-input-appeared"); continue; }
            var info = await MeasureAsync(path, _limits.MaxSourceFileBytes, cancellation).ConfigureAwait(false);
            if (info.Bytes != source.Bytes || info.Sha256 != source.Sha256) throw new InvalidDataException("report-input-hash-mismatch:" + source.Name);
        }
    }

    private async Task<Publication> ReadPublicationAsync(ReportDeliveryRecord row, CancellationToken cancellation)
    {
        if (row.SourceFingerprint is null) throw new InvalidDataException("report-inputs-not-fixed");
        var directory = OutputDirectory(row.DeliveryId);
        var receipt = Deserialize<Publication>(await ReadTextAsync(Path.Combine(directory, PublicationName), cancellation).ConfigureAwait(false));
        if (receipt.DeliveryId != row.DeliveryId || receipt.GeneratorSha256 != row.GeneratorSha256
            || receipt.SourceFingerprint != row.SourceFingerprint || receipt.Outputs.Length != row.Request.Inputs.OutputFiles.Count)
            throw new InvalidDataException("report-publication-binding-mismatch");
        for (var i = 0; i < receipt.Outputs.Length; i++)
        {
            var output = receipt.Outputs[i];
            if (output.FileName != row.Request.Inputs.OutputFiles[i]) throw new InvalidDataException("report-publication-files-mismatch");
            var measured = await MeasureAsync(Path.Combine(directory, output.FileName), _limits.MaxOutputFileBytes, cancellation).ConfigureAwait(false);
            if (measured.Bytes != output.Bytes || measured.Sha256 != output.Sha256) throw new InvalidDataException("report-output-hash-mismatch:" + output.FileName);
        }
        return receipt;
    }

    private async Task<ReportDeliveryRecord> LoadAsync(string id)
    {
        var head = Deserialize<Head>(await ReadTextAsync(Path.Combine(Job(id), "head.json"), default).ConfigureAwait(false));
        if (head.Schema != StoreSchema || head.Revision < 1 || !Leaf(head.FileName)) throw new InvalidDataException("report-delivery-head");
        var frame = Deserialize<Frame>(await ReadTextAsync(Path.Combine(Job(id), "versions", head.FileName), default).ConfigureAwait(false));
        if (frame.Schema != StoreSchema || frame.Hash != head.Hash || frame.Hash != Hash(Encoding.UTF8.GetBytes((frame.PreviousHash ?? "") + "\n" + frame.Payload)))
            throw new InvalidDataException("report-delivery-integrity");
        var row = Deserialize<ReportDeliveryRecord>(frame.Payload);
        if (row.DeliveryId != id || row.Revision != head.Revision) throw new InvalidDataException("report-delivery-identity");
        ValidateRequest(row.Request);
        return row;
    }

    private async Task<ReportDeliveryRecord> NextAsync(ReportDeliveryRecord row)
    {
        var previous = Deserialize<Head>(await ReadTextAsync(Path.Combine(Job(row.DeliveryId), "head.json"), default).ConfigureAwait(false));
        if (previous.Revision != row.Revision) throw new InvalidDataException("report-delivery-version-conflict");
        return await CommitAsync(row with { Revision = row.Revision + 1 }, previous).ConfigureAwait(false);
    }

    private async Task<ReportDeliveryRecord> CommitAsync(ReportDeliveryRecord row, Head? previous)
    {
        var payload = Serialize(row);
        var hash = Hash(Encoding.UTF8.GetBytes((previous?.Hash ?? "") + "\n" + payload));
        var frame = new Frame(StoreSchema, payload, previous?.FileName, previous?.Hash, hash);
        var fileName = row.Revision + "-" + hash + ".json";
        var directory = Path.Combine(Job(row.DeliveryId), "versions"); Directory.CreateDirectory(directory);
        var data = Encoding.UTF8.GetBytes(Serialize(frame));
        if (data.Length > _limits.MaxRecordBytes) throw new InvalidDataException("report-delivery-record-byte-limit");
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path)) await WriteNewAsync(path, data).ConfigureAwait(false);
        else if (!(await ReadBoundedAsync(path, _limits.MaxRecordBytes, default).ConfigureAwait(false)).AsSpan().SequenceEqual(data))
            throw new InvalidDataException("report-delivery-version-collision");
        // Immutable version first, atomic latest pointer second. Unpublished orphan versions are not commits.
        await AtomicWriteAsync(Path.Combine(Job(row.DeliveryId), "head.json"), Encoding.UTF8.GetBytes(Serialize(new Head(StoreSchema, row.Revision, fileName, hash)))).ConfigureAwait(false);
        return row;
    }

    private static string Fingerprint(ReportDeliveryRecord row, IEnumerable<ReportSourceSnapshot> sources) => Hash(Encoding.UTF8.GetBytes(
        Serialize(new { row.GeneratorSha256, row.Request.Inputs, row.Request.Strict, row.SourceLabel, Sources = sources })));

    private async Task<ReportDeliveryRecord> BindSourceAsync(ReportDeliveryRecord row, string sourceDirectory, string sourceLabel)
    {
        sourceDirectory = Path.GetFullPath(sourceDirectory);
        if (string.IsNullOrWhiteSpace(sourceLabel)) throw new ArgumentException("report-source-label-required", nameof(sourceLabel));
        if (row.SourceDirectory is not null)
        {
            if (row.SourceDirectory != sourceDirectory || row.SourceLabel != sourceLabel)
                throw new InvalidOperationException("report-source-binding-conflict");
            return row;
        }
        return await NextAsync(row with { SourceDirectory = sourceDirectory, SourceLabel = sourceLabel }).ConfigureAwait(false);
    }

    private static void ValidateRequest(ReportDeliveryRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.OwnerRef) || request.OwnerRef.Length > 1024
            || string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Length > 256
            || string.IsNullOrWhiteSpace(request.ReportKind) || request.ReportKind.Length > 128
            || string.IsNullOrWhiteSpace(request.GeneratorPath) || request.GeneratorPath.Length > 4096
            || request.Inputs is null || request.Inputs.Schema is not ("uniclaw.run-report.inputs.v1" or "uniclaw.coverage-report.inputs.v1")
            || string.IsNullOrWhiteSpace(request.Inputs.GeneratorName) || request.Inputs.GeneratorName.Length > 128
            || request.Inputs.GeneratorVersion < 1 || request.Inputs.Files is null || request.Inputs.Files.Count is < 1 or > 64
            || request.Inputs.OutputFiles is null || request.Inputs.OutputFiles.Count is < 1 or > 8)
            throw new InvalidDataException("report-requirement-invalid");
        if (request.Inputs.Files.Any(f => f is null || string.IsNullOrWhiteSpace(f.Name) || f.Name.Length > 128 || !Leaf(f.FileName))
            || request.Inputs.Files.Select(f => f.Name).Distinct(StringComparer.Ordinal).Count() != request.Inputs.Files.Count
            || request.Inputs.Files.Select(f => f.FileName).Distinct(StringComparer.Ordinal).Count() != request.Inputs.Files.Count
            || request.Inputs.OutputFiles.Any(f => !Leaf(f))
            || request.Inputs.OutputFiles.Distinct(StringComparer.Ordinal).Count() != request.Inputs.OutputFiles.Count)
            throw new InvalidDataException("report-requirement-catalog-invalid");
        if (request.ProtectedSources is { Count: > 64 }
            || request.ProtectedSources?.Any(source => source is null
                || string.IsNullOrWhiteSpace(source.Purpose) || source.Purpose.Length > 256
                || !ValidProtectedPath(source.RelativePath)) == true)
            throw new InvalidDataException("report-protected-source-invalid");
        if (request.Inputs.Attachments is { Count: > 64 }
            || request.Inputs.Attachments?.Any(attachment => attachment is null
                || string.IsNullOrWhiteSpace(attachment.Purpose) || attachment.Purpose.Length > 256
                || !ValidProtectedPath(attachment.RelativePath)
                || attachment.ExpectedSha256 is { } expected && (!ValidSha256(expected) || attachment.Directory)) == true
            || request.Inputs.Attachments?.Select(attachment => attachment.RelativePath)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Inputs.Attachments?.Count)
            throw new InvalidDataException("report-attachment-requirement-invalid");
    }

    private static bool ValidProtectedPath(string? value)
    {
        try { RequiredReportDeliveryPaths.ValidateRelativePath(value!); return true; }
        catch (ArgumentException) { return false; }
    }

    private static bool ValidSha256(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);

    private string Job(string id)
    {
        if (id is null || id.Length != 71 || !id.StartsWith("report-", StringComparison.Ordinal)
            || id[7..].Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("invalid-report-delivery-id", nameof(id));
        return Path.Combine(_root, id);
    }
    private static bool Leaf(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128
        && value is not ("." or "..") && !value.StartsWith(".delivery-", StringComparison.Ordinal)
        && value.IndexOfAny(['/', '\\']) < 0 && Path.GetFileName(value) == value;
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static T Deserialize<T>(string text) => JsonSerializer.Deserialize<T>(text, Json) ?? throw new InvalidDataException("report-delivery-json");
    private static string Reason(Exception error) { var reason = error.GetType().Name + ":" + error.Message; return reason.Length > 1024 ? reason[..1024] : reason; }
    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)); return options;
    }
    private async Task<string> ReadTextAsync(string path, CancellationToken cancellation) => Encoding.UTF8.GetString(
        await ReadBoundedAsync(path, _limits.MaxRecordBytes, cancellation).ConfigureAwait(false));

    private static async Task<byte[]> ReadBoundedAsync(string path, long limit, CancellationToken cancellation)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        if (input.Length > limit) throw new InvalidDataException("report-byte-limit:" + Path.GetFileName(path));
        using var output = new MemoryStream(); var buffer = new byte[65536];
        while (true)
        {
            var count = await input.ReadAsync(buffer, cancellation).ConfigureAwait(false); if (count == 0) break;
            if (output.Length + count > limit) throw new InvalidDataException("report-byte-limit:" + Path.GetFileName(path));
            await output.WriteAsync(buffer.AsMemory(0, count), cancellation).ConfigureAwait(false);
        }
        return output.ToArray();
    }

    private static async Task<ReportOutputSnapshot> CaptureFileAsync(string source, string target, long limit, CancellationToken cancellation)
    {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("report-input-link");
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        if (input.Length > limit) throw new InvalidDataException("report-source-byte-limit");
        await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 65536, FileOptions.Asynchronous);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer = new byte[65536]; long total = 0;
        while (true)
        {
            var count = await input.ReadAsync(buffer, cancellation).ConfigureAwait(false); if (count == 0) break;
            if (count > limit - total) throw new InvalidDataException("report-source-byte-limit");
            hash.AppendData(buffer, 0, count); await output.WriteAsync(buffer.AsMemory(0, count), cancellation).ConfigureAwait(false); total += count;
        }
        await output.FlushAsync().ConfigureAwait(false);
        return new(Path.GetFileName(target), total, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }
    private static async Task<ReportOutputSnapshot> MeasureAsync(string path, long limit, CancellationToken cancellation)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        if (file.Length > limit) throw new InvalidDataException("report-byte-limit");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer = new byte[65536]; long total = 0;
        while (true)
        {
            var count = await file.ReadAsync(buffer, cancellation).ConfigureAwait(false); if (count == 0) break;
            if (count > limit - total) throw new InvalidDataException("report-byte-limit"); hash.AppendData(buffer, 0, count); total += count;
        }
        return new(Path.GetFileName(path), total, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }
    private static async Task WriteNewAsync(string path, byte[] bytes)
    {
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await file.WriteAsync(bytes).ConfigureAwait(false); await file.FlushAsync().ConfigureAwait(false);
    }
    private static async Task AtomicWriteAsync(string path, byte[] bytes)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await WriteNewAsync(temporary, bytes).ConfigureAwait(false); File.Move(temporary, path, overwrite: true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
