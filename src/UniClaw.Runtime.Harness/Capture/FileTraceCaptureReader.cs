using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace UniClaw.Runtime.Harness.Capture;

/// <summary>Fail-closed reader for one explicitly named, published capture.</summary>
public sealed class FileTraceCaptureReader : ITraceCaptureReader
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly string _rootDir;

    /// <summary>Creates a reader rooted at the supplied capture directory.</summary>
    public FileTraceCaptureReader(string rootDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDir);
        _rootDir = Path.GetFullPath(rootDir);
    }

    /// <inheritdoc />
    public async ValueTask<TraceCaptureReadResult> ReadAsync(string captureSessionId, string? requiredTraceRunId = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSafeId(captureSessionId))
            return Invalid(TraceCaptureReadIssueCode.UnsafeCaptureSessionId, "CaptureSessionId must be one safe path segment.");
        if (requiredTraceRunId is not null && string.IsNullOrWhiteSpace(requiredTraceRunId))
            return Invalid(TraceCaptureReadIssueCode.TraceIdentityMismatch, "Required TraceRunId cannot be blank.");

        try
        {
            var root = new DirectoryInfo(_rootDir);
            var dir = new DirectoryInfo(Path.Combine(_rootDir, captureSessionId));
            if (!Directory.Exists(dir.FullName)) return TraceCaptureReadResult.Of(TraceCaptureReadStatus.CaptureNotFound);
            if (captureSessionId.StartsWith(".staging-", StringComparison.OrdinalIgnoreCase) || !Contained(root.FullName, dir.FullName) || HasReparsePoint(dir))
                return Invalid(TraceCaptureReadIssueCode.UnsafePath, "Capture path is unsafe.");
            var manifestPath = RequiredFilePath(dir, "capture-manifest.json", TraceCaptureReadIssueCode.ManifestMissing, out var manifestIssue);
            var recordsPath = RequiredFilePath(dir, "records.json", TraceCaptureReadIssueCode.RecordsMissing, out var recordsIssue);
            var checksumsPath = RequiredFilePath(dir, "checksums.sha256", TraceCaptureReadIssueCode.ChecksumManifestInvalid, out var checksumIssue);
            if (manifestIssue is not null || recordsIssue is not null || checksumIssue is not null)
                return TraceCaptureReadResult.Of(TraceCaptureReadStatus.ValidationFailed, null, [.. new[] { manifestIssue, recordsIssue, checksumIssue }.OfType<TraceCaptureReadIssue>()]);
            var manifest = await DeserializeAsync<TraceCaptureBundle>(manifestPath, cancellationToken);
            var records = await DeserializeAsync<ImmutableArray<CaptureRecord>>(recordsPath, cancellationToken);
            var issues = new List<TraceCaptureReadIssue>();
            if (manifest.SchemaVersion != 1) issues.Add(Issue(TraceCaptureReadIssueCode.UnsupportedCaptureSchema, $"Unsupported capture schema {manifest.SchemaVersion}."));
            if (manifest.CaptureSessionId != captureSessionId) issues.Add(Issue(TraceCaptureReadIssueCode.CaptureSessionIdMismatch, "Manifest capture identity differs from requested identity."));
            if (manifest.FinalState != CaptureState.Persisted) issues.Add(Issue(TraceCaptureReadIssueCode.NotPublished, "Capture is not published."));
            if (!records.Select(x => x.Order).SequenceEqual(Enumerable.Range(1, records.Length)) || !JsonEquivalent(manifest.Records, records))
                issues.Add(Issue(TraceCaptureReadIssueCode.RecordOrderInvalid, "Manifest and records are not contiguous and identical."));
            var artifacts = await ReadArtifactsAsync(dir, manifest.Artifacts, issues, cancellationToken);
            ValidateChecksums(dir, manifest.Artifacts, checksumsPath, issues);

            var tracePath = Path.GetFullPath(Path.Combine(dir.FullName, "observability-trace.json"));
            if (!Contained(dir.FullName, tracePath)) return Invalid(TraceCaptureReadIssueCode.UnsafePath, "Trace path escaped capture.");
            var hasTraceFile = File.Exists(tracePath);
            if ((manifest.ObservabilityTrace is null) != !hasTraceFile)
                issues.Add(Issue(hasTraceFile ? TraceCaptureReadIssueCode.TraceFileUnexpected : TraceCaptureReadIssueCode.TraceFileMissing, "Manifest and trace attachment disagree."));
            TraceRun? trace = null;
            if (hasTraceFile)
            {
                EnsureRegularFile(tracePath);
                trace = await DeserializeAsync<TraceRun>(tracePath, cancellationToken);
                ValidateTrace(trace, issues);
                if (!TraceEquals(manifest.ObservabilityTrace, trace)) issues.Add(Issue(TraceCaptureReadIssueCode.TraceIdentityMismatch, "Manifest and trace attachment differ."));
            }
            if (issues.Count > 0)
            {
                var status = issues.Any(x => x.Code is TraceCaptureReadIssueCode.UnsupportedCaptureSchema or TraceCaptureReadIssueCode.UnsupportedTraceSchema)
                    ? TraceCaptureReadStatus.UnsupportedSchema : TraceCaptureReadStatus.ValidationFailed;
                return TraceCaptureReadResult.Of(status, null, [.. issues]);
            }
            if (requiredTraceRunId is not null && trace is null)
                return TraceCaptureReadResult.Of(TraceCaptureReadStatus.TraceAbsent, manifest with { Records = records, Artifacts = artifacts });
            if (requiredTraceRunId is not null && trace!.TraceRunId != requiredTraceRunId)
                return TraceCaptureReadResult.Of(TraceCaptureReadStatus.IdentityMismatch, null, Issue(TraceCaptureReadIssueCode.TraceIdentityMismatch, "Required TraceRunId does not match."));
            return TraceCaptureReadResult.Of(trace is null ? TraceCaptureReadStatus.TraceAbsent : TraceCaptureReadStatus.Found,
                manifest with { Records = records, Artifacts = artifacts, ObservabilityTrace = trace });
        }
        catch (OperationCanceledException) { throw; }
        catch (JsonException ex) { return Invalid(TraceCaptureReadIssueCode.MalformedJson, ex.Message); }
        catch (IOException ex) { return Invalid(TraceCaptureReadIssueCode.ReadFailure, ex.Message); }
        catch (UnauthorizedAccessException ex) { return Invalid(TraceCaptureReadIssueCode.UnsafePath, ex.Message); }
        catch (Exception ex) { return Invalid(TraceCaptureReadIssueCode.ReadFailure, ex.Message); }
    }

    private static async Task<T> DeserializeAsync<T>(string path, CancellationToken ct)
    {
        EnsureRegularFile(path);
        await using var stream = File.OpenRead(path);
        return (await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct)) ?? throw new JsonException("JSON value is null.");
    }

    private static async Task<ImmutableArray<CaptureArtifact>> ReadArtifactsAsync(DirectoryInfo dir, ImmutableArray<CaptureArtifact> declared, List<TraceCaptureReadIssue> issues, CancellationToken ct)
    {
        var result = ImmutableArray.CreateBuilder<CaptureArtifact>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var artifactDir = Path.GetFullPath(Path.Combine(dir.FullName, "artifacts"));
        if (!Contained(dir.FullName, artifactDir)) { issues.Add(Issue(TraceCaptureReadIssueCode.UnsafePath, "Artifact directory escaped capture.")); return result.ToImmutable(); }
        if (Directory.Exists(artifactDir) && HasReparsePoint(new DirectoryInfo(artifactDir))) { issues.Add(Issue(TraceCaptureReadIssueCode.UnsafePath, "Artifact directory is a reparse point.")); return result.ToImmutable(); }
        foreach (var artifact in declared)
        {
            if (!IsSafeId(artifact.ArtifactId) || !ids.Add(artifact.ArtifactId)) { issues.Add(Issue(TraceCaptureReadIssueCode.ArtifactIdInvalid, artifact.ArtifactId)); continue; }
            if (artifact.DerivedFromArtifactId is not null && (!IsSafeId(artifact.DerivedFromArtifactId) || artifact.DerivedFromArtifactId == artifact.ArtifactId || !declared.Any(x => x.ArtifactId == artifact.DerivedFromArtifactId))) { issues.Add(Issue(TraceCaptureReadIssueCode.ArtifactIdInvalid, artifact.ArtifactId)); continue; }
            var path = Path.GetFullPath(Path.Combine(artifactDir, artifact.ArtifactId + ".bin"));
            if (!Contained(dir.FullName, path)) { issues.Add(Issue(TraceCaptureReadIssueCode.UnsafePath, artifact.ArtifactId)); continue; }
            if (!File.Exists(path)) { issues.Add(Issue(TraceCaptureReadIssueCode.ArtifactMissing, artifact.ArtifactId)); continue; }
            try
            {
                EnsureRegularFile(path);
                var bytes = await File.ReadAllBytesAsync(path, ct);
                if (bytes.Length != artifact.ByteCount) issues.Add(Issue(TraceCaptureReadIssueCode.ArtifactByteCountMismatch, artifact.ArtifactId));
                var hash = TraceCaptureSession.ComputeHash(bytes);
                if (!string.Equals(hash, artifact.ContentHash, StringComparison.Ordinal)) issues.Add(Issue(TraceCaptureReadIssueCode.ArtifactHashMismatch, artifact.ArtifactId));
                result.Add(artifact with { Content = bytes.ToImmutableArray() });
            }
            catch (IOException ex) { issues.Add(Issue(TraceCaptureReadIssueCode.UnsafePath, ex.Message)); }
        }
        if (Directory.Exists(artifactDir))
            foreach (var file in Directory.EnumerateFiles(artifactDir, "*.bin"))
                if (!declared.Any(a => Path.GetFileName(file).Equals(a.ArtifactId + ".bin", StringComparison.Ordinal))) issues.Add(Issue(TraceCaptureReadIssueCode.ChecksumManifestInvalid, "Unknown artifact file."));
        return result.ToImmutable();
    }

    private static void ValidateChecksums(DirectoryInfo dir, ImmutableArray<CaptureArtifact> declared, string path, List<TraceCaptureReadIssue> issues)
    {
        if (!File.Exists(path)) { issues.Add(Issue(TraceCaptureReadIssueCode.ChecksumManifestInvalid, "Checksum manifest missing.")); return; }
        EnsureRegularFile(path);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(path, Encoding.UTF8).Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var parts = line.Split("  ", 2, StringSplitOptions.None);
            var expected = parts.Length == 2 && declared.Any(a => IsSafeId(a.ArtifactId) && parts[1] == $"artifacts/{a.ArtifactId}.bin");
            if (!expected || !seen.Add(parts[1])) { issues.Add(Issue(TraceCaptureReadIssueCode.ChecksumManifestInvalid, "Invalid checksum entry.")); continue; }
            var id = Path.GetFileNameWithoutExtension(parts[1]);
            var artifact = declared.FirstOrDefault(x => x.ArtifactId == id);
            if (artifact is null || !string.Equals(parts[0], artifact.ContentHash, StringComparison.Ordinal)) issues.Add(Issue(TraceCaptureReadIssueCode.ChecksumManifestInvalid, parts[1]));
        }
        if (seen.Count != declared.Length) issues.Add(Issue(TraceCaptureReadIssueCode.ChecksumManifestInvalid, "Checksum coverage incomplete."));
    }

    private static void ValidateTrace(TraceRun trace, List<TraceCaptureReadIssue> issues)
    {
        if (trace.SchemaVersion != 1) { issues.Add(Issue(TraceCaptureReadIssueCode.UnsupportedTraceSchema, "Unsupported trace schema.")); return; }
        if (string.IsNullOrWhiteSpace(trace.TraceRunId)) issues.Add(Issue(TraceCaptureReadIssueCode.TraceStructureInvalid, "TraceRunId is empty."));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var span in trace.Spans)
        {
            if (string.IsNullOrWhiteSpace(span.SpanId) || !ids.Add(span.SpanId) || span.SchemaVersion != 1 || span.StartOffsetNs < 0 || span.DurationNs < 0 || string.IsNullOrWhiteSpace(span.Name) || string.IsNullOrWhiteSpace(span.Layer) || string.IsNullOrWhiteSpace(span.Component) || span.Outcome is not ("SUCCEEDED" or "FAILED" or "CANCELLED" or "UNKNOWN") || (span.ParentSpanId is not null && string.IsNullOrWhiteSpace(span.ParentSpanId))) { issues.Add(Issue(TraceCaptureReadIssueCode.TraceStructureInvalid, span.SpanId)); continue; }
            if (span.ParentSpanId == span.SpanId) issues.Add(Issue(TraceCaptureReadIssueCode.TraceStructureInvalid, "Self parent."));
            long spanEnd; try { spanEnd = checked(span.StartOffsetNs + span.DurationNs); } catch (OverflowException) { issues.Add(Issue(TraceCaptureReadIssueCode.TraceStructureInvalid, "Span interval overflow.")); continue; }
            foreach (var attr in span.Attributes) if (string.IsNullOrWhiteSpace(attr.Key)) issues.Add(Issue(TraceCaptureReadIssueCode.TraceStructureInvalid, "Empty attribute key."));
            foreach (var evt in span.Events) if (evt.SchemaVersion != 1 || string.IsNullOrWhiteSpace(evt.EventId) || evt.SpanId != span.SpanId || evt.TimestampOffsetNs < span.StartOffsetNs || evt.TimestampOffsetNs > spanEnd || evt.Attributes.Any(a => string.IsNullOrWhiteSpace(a.Key))) issues.Add(Issue(TraceCaptureReadIssueCode.TraceStructureInvalid, "Invalid trace event."));
        }
        foreach (var span in trace.Spans.Where(x => x.ParentSpanId is not null))
        {
            var parent = trace.Spans.FirstOrDefault(x => x.SpanId == span.ParentSpanId);
            if (parent is null) continue;
            long childEnd, parentEnd; try { childEnd = checked(span.StartOffsetNs + span.DurationNs); parentEnd = checked(parent.StartOffsetNs + parent.DurationNs); } catch (OverflowException) { issues.Add(Issue(TraceCaptureReadIssueCode.TraceStructureInvalid, "Interval overflow.")); continue; }
            if (span.StartOffsetNs < parent.StartOffsetNs || childEnd > parentEnd) issues.Add(Issue(TraceCaptureReadIssueCode.TraceStructureInvalid, "Child interval outside parent."));
            var seen = new HashSet<string>(StringComparer.Ordinal) { span.SpanId };
            for (var p = parent; p.ParentSpanId is not null;) { if (!seen.Add(p.SpanId)) { issues.Add(Issue(TraceCaptureReadIssueCode.TraceStructureInvalid, "Span cycle.")); break; } p = trace.Spans.FirstOrDefault(x => x.SpanId == p.ParentSpanId)!; if (p is null) break; }
        }
    }

    private static bool TraceEquals(TraceRun? a, TraceRun? b) => a is not null && b is not null && JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions);
    private static string RequiredFilePath(DirectoryInfo dir, string name, TraceCaptureReadIssueCode code, out TraceCaptureReadIssue? issue) { var p = Path.GetFullPath(Path.Combine(dir.FullName, name)); issue = null; if (!Contained(dir.FullName, p)) { issue = Issue(TraceCaptureReadIssueCode.UnsafePath, name); return p; } if (!File.Exists(p)) { issue = Issue(code, name); return p; } try { EnsureRegularFile(p); } catch (IOException ex) { issue = Issue(TraceCaptureReadIssueCode.UnsafePath, ex.Message); } return p; }
    private static void EnsureRegularFile(string path) { var fi = new FileInfo(path); if (fi.LinkTarget is not null || (fi.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse point is not allowed."); }
    private static bool HasReparsePoint(DirectoryInfo dir) => (dir.Attributes & FileAttributes.ReparsePoint) != 0;
    private static bool Contained(string root, string path) => Path.GetRelativePath(root, path) is var rel && rel != ".." && !rel.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    private static bool IsSafeId(string value) => !string.IsNullOrWhiteSpace(value) && value is not "." and not ".." && !Path.IsPathRooted(value) && value.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) < 0 && !value.StartsWith(".staging-", StringComparison.OrdinalIgnoreCase);
    private static TraceCaptureReadIssue Issue(TraceCaptureReadIssueCode code, string message) => new(code, message);
    private static TraceCaptureReadResult Invalid(TraceCaptureReadIssueCode code, string message) => TraceCaptureReadResult.Of(TraceCaptureReadStatus.ValidationFailed, null, Issue(code, message));
    private static bool JsonEquivalent<T>(T left, T right) { using var a = JsonDocument.Parse(JsonSerializer.Serialize(left, JsonOptions)); using var b = JsonDocument.Parse(JsonSerializer.Serialize(right, JsonOptions)); return JsonElement.DeepEquals(a.RootElement, b.RootElement); }
}
