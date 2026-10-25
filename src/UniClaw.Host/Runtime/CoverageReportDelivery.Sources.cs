using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace UniClaw.Host.Runtime;

public sealed partial class CoverageReportDelivery
{
    private sealed record SourceAttachment(string RelativePath, string Purpose, bool RequiredForDelivery,
        bool RetainAfterDelivery, string? ExpectedSha256, bool WasPresent);
    private sealed record SourceDocument(string SchemaVersion, string OwnerRef, CoverageReportMaterial Material,
        IReadOnlyList<SourceAttachment> Attachments, string? DiscoveryProblem);
    private sealed record CapturedAttachment(string RelativePath, string Purpose, bool RequiredForDelivery,
        bool Present, long Bytes, string? Sha256);
    private sealed record AttachmentManifest(string SchemaVersion, string OwnerRef, string SourceSha256,
        IReadOnlyList<CapturedAttachment> Files);

    private static FileStream SourceLease(ReportDeliveryRecord row)
    {
        if (row.SourceDirectory is null || !Directory.Exists(row.SourceDirectory)) throw new IOException("coverage-source-directory-missing");
        return new(RequiredReportDeliveryPaths.ResolveSourcePath(row.SourceDirectory, ".coverage-source.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.Asynchronous);
    }

    private async Task SaveMaterialAsync(ReportDeliveryRecord row, CoverageReportMaterial material, CancellationToken token)
    {
        using var report = JsonDocument.Parse(material.ReportJson);
        using var steps = JsonDocument.Parse(material.StepsJson);
        if (!ValidRunId(material.Identity.RunId)) throw new InvalidDataException("coverage-source-run-id-invalid");
        if (report.RootElement.ValueKind != JsonValueKind.Object || steps.RootElement.ValueKind != JsonValueKind.Array
            || new[] { material.Identity.RunRecordingId, material.Identity.EvidenceRecordingId, material.Identity.WorldRecordingId }
                .Any(id => !Guid.TryParseExact(id, "N", out _)))
            throw new InvalidDataException("coverage-source-identity-or-shape");
        var path = RequiredReportDeliveryPaths.ResolveSourcePath(row.SourceDirectory!, SourceFileName);
        if (File.Exists(path))
        {
            var old = JsonSerializer.Deserialize<SourceDocument>(await ReadSourceBytesAsync(path, token).ConfigureAwait(false), Json)
                ?? throw new InvalidDataException("coverage-source-empty");
            if (old.OwnerRef != row.Request.OwnerRef || old.Material != material) throw new InvalidOperationException("coverage-source-material-conflict");
            return;
        }
        var attachments = new Dictionary<string, SourceAttachment>(StringComparer.Ordinal);
        string? problem = null;
        foreach (var request in row.Request.Inputs.Attachments ?? [])
        {
            try
            {
                var original = RequiredReportDeliveryPaths.ResolveSourcePath(row.SourceDirectory!, request.RelativePath);
                if (request.Directory)
                {
                    if (!Directory.Exists(original))
                    {
                        if (request.RequiredForDelivery) problem ??= "coverage-required-source-directory-missing";
                        continue;
                    }
                    foreach (var item in Directory.EnumerateFiles(original, "*", SearchOption.AllDirectories))
                    {
                        var relative = Path.GetRelativePath(row.SourceDirectory!, item).Replace(Path.DirectorySeparatorChar, '/');
                        _ = RequiredReportDeliveryPaths.ResolveSourcePath(row.SourceDirectory!, relative);
                        Add(new(relative, request.Purpose, request.RequiredForDelivery, request.RetainAfterDelivery, null, true));
                    }
                }
                else Add(new(request.RelativePath, request.Purpose, request.RequiredForDelivery, request.RetainAfterDelivery,
                    request.ExpectedSha256?.ToLowerInvariant(), File.Exists(original)));
            }
            catch (Exception error) when (SourceError(error)) { problem ??= "coverage-attachment-discovery-failed:" + SourceReason(error); }
        }
        void Add(SourceAttachment value)
        {
            var caseVariant = attachments.Keys.FirstOrDefault(path => string.Equals(path, value.RelativePath, StringComparison.OrdinalIgnoreCase));
            if (caseVariant is not null && !string.Equals(caseVariant, value.RelativePath, StringComparison.Ordinal))
                throw new InvalidOperationException("coverage-attachment-case-conflict:" + caseVariant + ":" + value.RelativePath);
            if (attachments.TryGetValue(value.RelativePath, out var old))
            {
                if (old.ExpectedSha256 is not null && value.ExpectedSha256 is not null && old.ExpectedSha256 != value.ExpectedSha256)
                    throw new InvalidOperationException("coverage-source-expectation-conflict");
                attachments[value.RelativePath] = value with { Purpose = old.Purpose + ";" + value.Purpose,
                    RequiredForDelivery = old.RequiredForDelivery || value.RequiredForDelivery,
                    RetainAfterDelivery = old.RetainAfterDelivery || value.RetainAfterDelivery,
                    ExpectedSha256 = old.ExpectedSha256 ?? value.ExpectedSha256, WasPresent = old.WasPresent || value.WasPresent };
            }
            else attachments.Add(value.RelativePath, value);
        }
        var document = new SourceDocument("uniclaw.coverage-report.source.v1", row.Request.OwnerRef, material,
            attachments.Values.OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray(), problem);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, Json);
        if (bytes.LongLength > _options.Limits.MaxSourceFileBytes) throw new InvalidDataException("coverage-source-byte-limit");
        // Preserve the unique Ledger-produced report even if attachment acquisition later fails.
        await WriteOriginalAsync(path, bytes, token).ConfigureAwait(false);
    }

    private async Task EnsureBundleAsync(ReportDeliveryRecord row, CancellationToken token)
    {
        var sourcePath = RequiredReportDeliveryPaths.ResolveSourcePath(row.SourceDirectory!, SourceFileName);
        if (!File.Exists(sourcePath)) throw new IOException("coverage-source-missing:" + SourceFileName);
        var bytes = await ReadSourceBytesAsync(sourcePath, token).ConfigureAwait(false);
        var source = JsonSerializer.Deserialize<SourceDocument>(bytes, Json) ?? throw new InvalidDataException("coverage-source-empty");
        if (source.SchemaVersion != "uniclaw.coverage-report.source.v1" || source.OwnerRef != row.Request.OwnerRef)
            throw new InvalidDataException("coverage-source-owner-mismatch");
        if (!ValidRunId(source.Material.Identity.RunId)) throw new InvalidDataException("coverage-source-run-id-invalid");
        if (source.DiscoveryProblem is not null) throw new InvalidDataException(source.DiscoveryProblem);
        var destination = RequiredReportDeliveryPaths.ResolveSourcePath(row.SourceDirectory!, BundleFileName);
        if (File.Exists(destination)) return; // Existing bytes are verified by the pinned generator, never overwritten.
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var captured = new List<CapturedAttachment>(); long total = bytes.LongLength;
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 65536, FileOptions.Asynchronous))
            {
                using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var item in source.Attachments)
                    {
                        var original = RequiredReportDeliveryPaths.ResolveSourcePath(row.SourceDirectory!, item.RelativePath);
                        if (!File.Exists(original))
                        {
                            if (item.RequiredForDelivery || item.WasPresent) throw new IOException("coverage-attachment-missing:" + item.RelativePath);
                            captured.Add(new(item.RelativePath, item.Purpose, false, false, 0, null)); continue;
                        }
                        var limit = Math.Min(_options.Limits.MaxSourceFileBytes, _options.Limits.MaxSourceBytes - total);
                        await using var input = new FileStream(original, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
                        if (input.Length > limit) throw new InvalidDataException("coverage-attachment-byte-limit");
                        var entry = archive.CreateEntry("files/" + item.RelativePath, CompressionLevel.Optimal);
                        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); long size = 0;
                        await using (var sink = entry.Open())
                        {
                            var buffer = new byte[65536]; int count;
                            while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                            {
                                if (count > limit - size) throw new InvalidDataException("coverage-attachment-byte-limit");
                                hash.AppendData(buffer, 0, count); await sink.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false); size += count;
                            }
                        }
                        var sha = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
                        if (item.ExpectedSha256 is not null && sha != item.ExpectedSha256) throw new InvalidDataException("coverage-attachment-hash-mismatch");
                        if (await HashFileAsync(original, token).ConfigureAwait(false) != sha) throw new IOException("coverage-attachment-drift");
                        total += size; captured.Add(new(item.RelativePath, item.Purpose, item.RequiredForDelivery, true, size, sha));
                    }
                    var manifest = new AttachmentManifest("uniclaw.coverage-report.attachments.v1", row.Request.OwnerRef,
                        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), captured);
                    await using var manifestStream = archive.CreateEntry("manifest.json").Open();
                    await JsonSerializer.SerializeAsync(manifestStream, manifest, Json, token).ConfigureAwait(false);
                }
                await output.FlushAsync(token).ConfigureAwait(false);
                if (output.Length > _options.Limits.MaxSourceFileBytes) throw new InvalidDataException("coverage-attachment-bundle-byte-limit");
            }
            File.Move(temporary, destination);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private async Task<byte[]> ReadSourceBytesAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        if (stream.Length > _options.Limits.MaxSourceFileBytes) throw new InvalidDataException("coverage-source-byte-limit");
        using var data = new MemoryStream(); var buffer = new byte[65536]; int count;
        while ((count = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
        {
            if (count > _options.Limits.MaxSourceFileBytes - data.Length) throw new InvalidDataException("coverage-source-byte-limit");
            await data.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
        }
        return data.ToArray();
    }

    private static async Task WriteOriginalAsync(string path, byte[] bytes, CancellationToken token)
    {
        if (File.Exists(path))
        {
            await using var original = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
            if (original.Length != bytes.LongLength) throw new IOException("coverage-original-path-conflict:" + Path.GetFileName(path));
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); long total = 0;
            var buffer = new byte[65536]; int count;
            while ((count = await original.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
            {
                if (count > bytes.LongLength - total) throw new IOException("coverage-original-path-conflict:" + Path.GetFileName(path));
                hash.AppendData(buffer, 0, count); total += count;
            }
            if (total != bytes.LongLength || !hash.GetHashAndReset().AsSpan().SequenceEqual(SHA256.HashData(bytes)))
                throw new IOException("coverage-original-path-conflict:" + Path.GetFileName(path));
            return;
        }
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 65536, FileOptions.Asynchronous))
            { await stream.WriteAsync(bytes, token).ConfigureAwait(false); await stream.FlushAsync(token).ConfigureAwait(false); }
            File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static bool ValidRunId(string? value) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 1024 && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
