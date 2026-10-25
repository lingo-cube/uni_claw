using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UniClaw.Host.Runtime;

public sealed partial class RuntimeRunStore
{
    private const string ProjectionHistorySchema = "uniclaw.workspace.runtime-run-projection-history.v1";
    private const string ProjectionSnapshotSchema = "uniclaw.workspace.runtime-run-projection-snapshot.v2";

    /// <summary>Reads a retained Host projection without granting any execution or recovery authority.</summary>
    public RuntimeRunProjectionVersion ReadProjectionVersion(
        string runId,
        long? revision = null,
        HistoryVersionDirection direction = HistoryVersionDirection.Forward)
    {
        lock (_gate)
        {
            if (!Enum.IsDefined(direction))
                throw new ArgumentOutOfRangeException(nameof(direction));
            var state = ReadProjectionStateUnsafe(runId)
                ?? throw new KeyNotFoundException($"Runtime run '{runId}' was not found");
            if (state.Envelope is null)
            {
                if (revision is not null && revision != state.Projection.Revision)
                    throw new InvalidOperationException("Runtime run does not retain the requested legacy projection version");
                return new RuntimeRunProjectionVersion(state.Projection, state.Projection.Revision, state.Projection.Revision,
                    "legacy-v1", 0, false);
            }

            var target = revision ?? state.Envelope.Revision;
            if (target < state.Envelope.EarliestRevision)
                throw new InvalidOperationException($"Runtime run history begins at revision {state.Envelope.EarliestRevision}");
            if (target > state.Envelope.Revision)
                throw new InvalidOperationException($"Runtime run history ends at revision {state.Envelope.Revision}");

            if (target == state.Envelope.Revision)
                return new RuntimeRunProjectionVersion(state.Projection, target, state.Envelope.EarliestRevision,
                    state.Envelope.RecordingId, 1, state.Envelope.CompleteHistory);
            return direction == HistoryVersionDirection.Forward
                ? ReadForwardUnsafe(state, target)
                : ReadReverseUnsafe(state, target);
        }
    }

    private RuntimeRunProjectionVersion ReadForwardUnsafe(ProjectionState state, long target)
    {
        var envelope = state.Envelope!;
        RuntimeRunProjection? working = null;
        RuntimeRunProjection? value = null;
        string previous = "";
        var cost = 0;
        foreach (var revision in Revisions(envelope.EarliestRevision, envelope.Revision))
        {
            var frame = ReadFrameUnsafe(state.Projection.RunId, revision);
            cost++;
            ValidateFrameUnsafe(frame, state.Projection.RunId, revision, envelope.RecordingId, previous);
            if (frame.EarliestRevision != envelope.EarliestRevision)
                throw new InvalidDataException("runtime-run-history-earliest-mismatch");
            working = frame.Baseline is not null
                ? frame.Baseline
                : DeserializeProjection((frame.Delta ?? throw new InvalidDataException("runtime-run-history-delta-missing"))
                    .Apply(ProjectionElement(working!)));
            if (working.Revision != revision)
                throw new InvalidDataException("runtime-run-history-revision-mismatch");
            if (ProjectionHash(working) != frame.AfterProjectionHash)
                throw new InvalidDataException("runtime-run-history-after-projection-mismatch");
            if (revision == target)
                value = working;
            previous = frame.Hash;
        }
        ValidateDeclaredSuffixUnsafe(state, previous);
        return new RuntimeRunProjectionVersion(value!, target, envelope.EarliestRevision, envelope.RecordingId, cost, envelope.CompleteHistory);
    }

    private RuntimeRunProjectionVersion ReadReverseUnsafe(ProjectionState state, long target)
    {
        var envelope = state.Envelope!;
        var value = state.Projection;
        var cost = 0;
        var frames = new Dictionary<long, ProjectionRevisionFrame>();
        string previous = "";
        foreach (var revision in Revisions(envelope.EarliestRevision, envelope.Revision))
        {
            var frame = ReadFrameUnsafe(value.RunId, revision);
            cost++;
            ValidateFrameUnsafe(frame, value.RunId, revision, envelope.RecordingId, previous);
            if (frame.EarliestRevision != envelope.EarliestRevision)
                throw new InvalidDataException("runtime-run-history-earliest-mismatch");
            previous = frame.Hash;
            frames.Add(revision, frame);
        }
        ValidateDeclaredSuffixUnsafe(state, previous);
        for (var revision = envelope.Revision; revision > target; revision--)
        {
            var frame = frames[revision];
            if (ProjectionHash(value) != frame.AfterProjectionHash)
                throw new InvalidDataException("runtime-run-history-after-projection-mismatch");
            value = DeserializeProjection((frame.Delta ?? throw new InvalidDataException("runtime-run-history-delta-missing"))
                .Apply(ProjectionElement(value), reverse: true));
            if (ProjectionHash(value) != frame.BeforeProjectionHash)
                throw new InvalidDataException("runtime-run-history-before-projection-mismatch");
        }
        return new RuntimeRunProjectionVersion(value, target, envelope.EarliestRevision, envelope.RecordingId, cost, envelope.CompleteHistory);
    }

    private void CommitProjectionUnsafe(RuntimeRunProjection? before, RuntimeRunProjection next, RuntimeRunEvent @event)
    {
        try
        {
            ThrowIfFaultedUnsafe();
            var state = before is null ? null : ReadProjectionStateUnsafe(next.RunId);
            if (before is not null && state is null)
                throw new InvalidDataException("runtime-run-projection-missing-before-commit");
            if (state is not null && next.Revision != checked(state.Projection.Revision + 1))
                throw new InvalidDataException("runtime-run-projection-revision-gap");
            if (state is { Envelope: null })
                state = AnchorLegacyUnsafe(state);

            var recordingId = state?.Envelope?.RecordingId ?? Guid.NewGuid().ToString("N");
            var earliest = state?.Envelope?.EarliestRevision ?? next.Revision;
            var previousHash = state?.Envelope?.FrameHash ?? "";
            var beforeElement = before is null ? ProjectionElement(next) : ProjectionElement(before);
            var afterElement = ProjectionElement(next);
            var delta = before is null ? null : ReversibleJsonDelta.Between(beforeElement, afterElement);
            var frame = new ProjectionRevisionFrame(ProjectionHistorySchema, recordingId, next.Revision, earliest,
                before is null ? next : null, @event, delta, ProjectionHash(beforeElement), ProjectionHash(afterElement), previousHash, "");
            frame = frame with { Hash = HashFrame(frame) };
            WriteNewFrameUnsafe(next.RunId, frame);
            WriteEnvelopeUnsafe(new ProjectionSnapshotEnvelope(ProjectionSnapshotSchema, recordingId, next.Revision, earliest, frame.Hash,
                state?.Envelope?.CompleteHistory ?? true, next));
        }
        catch (OverflowException ex)
        {
            throw new InvalidOperationException("runtime-run-projection-revision-overflow", ex);
        }
        catch
        {
            _faulted = true;
            throw;
        }
    }

    private ProjectionState AnchorLegacyUnsafe(ProjectionState legacy)
    {
        var prior = ReadEventBySequenceUnsafe(legacy.Projection.RunId, legacy.Projection.LastEventSequence)
            ?? throw new InvalidDataException("runtime-run-legacy-anchor-event-missing");
        var recordingId = Guid.NewGuid().ToString("N");
        var frame = new ProjectionRevisionFrame(ProjectionHistorySchema, recordingId, legacy.Projection.Revision, legacy.Projection.Revision,
            legacy.Projection, prior, null, ProjectionHash(legacy.Projection), ProjectionHash(legacy.Projection), "", "");
        frame = frame with { Hash = HashFrame(frame) };
        WriteNewFrameUnsafe(legacy.Projection.RunId, frame);
        return legacy with { Envelope = new ProjectionSnapshotEnvelope(ProjectionSnapshotSchema, recordingId, legacy.Projection.Revision,
            legacy.Projection.Revision, frame.Hash, false, legacy.Projection) };
    }

    private ProjectionState? ReadProjectionStateUnsafe(string runId)
    {
        if (!IsSafeId(runId)) return null;
        var path = ProjectionPath(runId);
        if (!File.Exists(path))
        {
            if (Directory.Exists(RevisionsPath(runId)))
                throw new InvalidDataException("runtime-run-orphan-revisions");
            return null;
        }
        return ReadProjectionStateFromPathUnsafe(path);
    }

    private RuntimeRunProjection ReadProjectionFromPathUnsafe(string path) => ReadProjectionStateFromPathUnsafe(path).Projection;

    private ProjectionState ReadProjectionStateFromPathUnsafe(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(ReadLimitedTextUnsafe(path));
            if (document.RootElement.TryGetProperty("projection", out _))
            {
                var envelope = JsonSerializer.Deserialize<ProjectionSnapshotEnvelope>(document.RootElement.GetRawText(), _json)
                    ?? throw new InvalidDataException("runtime-run-projection-envelope-empty");
                if (envelope.Schema != ProjectionSnapshotSchema || envelope.Projection.SchemaVersion != ProjectionSchema || envelope.Revision < 1 || envelope.EarliestRevision < 1 || envelope.Revision != envelope.Projection.Revision || envelope.EarliestRevision > envelope.Revision)
                    throw new InvalidDataException("runtime-run-projection-envelope-invalid");
                if (!string.Equals(Path.GetFullPath(path), ProjectionPath(envelope.Projection.RunId), StringComparison.Ordinal))
                    throw new InvalidDataException("runtime-run-projection-path-mismatch");
                var frame = ReadFrameUnsafe(envelope.Projection.RunId, envelope.Revision);
                ValidateFrameUnsafe(frame, envelope.Projection.RunId, envelope.Revision, envelope.RecordingId,
                    envelope.Revision == envelope.EarliestRevision ? "" : frame.PreviousHash);
                if (frame.Hash != envelope.FrameHash || frame.EarliestRevision != envelope.EarliestRevision || ProjectionHash(envelope.Projection) != frame.AfterProjectionHash)
                    throw new InvalidDataException("runtime-run-projection-head-mismatch");
                if (envelope.Revision < long.MaxValue && File.Exists(RevisionPath(envelope.Projection.RunId, envelope.Revision + 1)))
                    throw new InvalidDataException("runtime-run-projection-unconfirmed-suffix");
                return new ProjectionState(envelope.Projection, envelope);
            }
            var projection = JsonSerializer.Deserialize<RuntimeRunProjection>(document.RootElement.GetRawText(), _json)
                ?? throw new InvalidDataException("runtime-run-projection-empty");
            if (!string.Equals(Path.GetFullPath(path), ProjectionPath(projection.RunId), StringComparison.Ordinal))
                throw new InvalidDataException("runtime-run-projection-path-mismatch");
            if (projection.SchemaVersion != ProjectionSchema || projection.Revision < 1 || Directory.Exists(RevisionsPath(projection.RunId)))
                throw new InvalidDataException("runtime-run-legacy-orphan-revisions");
            return new ProjectionState(projection, null);
        }
        catch (JsonException ex) { throw new InvalidDataException("runtime-run-projection-invalid", ex); }
    }

    private void ValidateDeclaredSuffixUnsafe(ProjectionState state, string finalHash)
    {
        var envelope = state.Envelope!;
        if (finalHash != envelope.FrameHash)
            throw new InvalidDataException("runtime-run-history-head-mismatch");
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var revision in Revisions(envelope.EarliestRevision, envelope.Revision))
            expected.Add(RevisionPath(state.Projection.RunId, revision));
        var actual = Directory.EnumerateFiles(RevisionsPath(state.Projection.RunId)).ToHashSet(StringComparer.Ordinal);
        if (!actual.SetEquals(expected))
            throw new InvalidDataException("runtime-run-history-orphan-suffix");
    }

    private IReadOnlyList<RuntimeRunEvent> ReadEventsUnsafe(string runId, ProjectionState state)
    {
        if (state.Envelope is null) return ReadAllEventsUnsafe(runId).ToArray();
        var result = ReadAllEventsUnsafe(runId).Where(e => e.Sequence < state.Envelope.EarliestRevision).ToList();
        string previous = "";
        foreach (var revision in Revisions(state.Envelope.EarliestRevision, state.Envelope.Revision))
        {
            var frame = ReadFrameUnsafe(runId, revision);
            ValidateFrameUnsafe(frame, runId, revision, state.Envelope.RecordingId, previous);
            if (frame.EarliestRevision != state.Envelope.EarliestRevision)
                throw new InvalidDataException("runtime-run-event-history-earliest-mismatch");
            result.Add(frame.Event);
            previous = frame.Hash;
        }
        ValidateDeclaredSuffixUnsafe(state, previous);
        return result.OrderBy(e => e.Sequence).ToArray();
    }

    private IEnumerable<RuntimeRunEvent> ReadAllEventsUnsafe(string runId)
    {
        var path = EventsPath(runId);
        if (!File.Exists(path)) return Array.Empty<RuntimeRunEvent>();
        try
        {
            return ReadLimitedTextUnsafe(path).Split('\n').Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => JsonSerializer.Deserialize<RuntimeRunEvent>(line, _lineJson) ?? throw new InvalidDataException("runtime-run-event-empty"))
                .ToArray();
        }
        catch (JsonException ex) { throw new InvalidDataException("runtime-run-event-invalid", ex); }
    }

    private RuntimeRunEvent? ReadEventBySequenceUnsafe(string runId, long sequence) => ReadAllEventsUnsafe(runId).SingleOrDefault(e => e.Sequence == sequence);

    private ProjectionRevisionFrame ReadFrameUnsafe(string runId, long revision)
    {
        var path = RevisionPath(runId, revision);
        if (!File.Exists(path)) throw new InvalidDataException("runtime-run-history-frame-missing");
        try { return JsonSerializer.Deserialize<ProjectionRevisionFrame>(ReadLimitedTextUnsafe(path), _json) ?? throw new InvalidDataException("runtime-run-history-frame-empty"); }
        catch (JsonException ex) { throw new InvalidDataException("runtime-run-history-frame-invalid", ex); }
    }

    private static void ValidateFrameUnsafe(ProjectionRevisionFrame frame, string runId, long revision, string recordingId, string previous)
    {
        if (frame.Schema != ProjectionHistorySchema || frame.RecordingId != recordingId || frame.Revision != revision || frame.PreviousHash != previous || frame.Hash != HashFrame(frame))
            throw new InvalidDataException("runtime-run-history-frame-integrity");
        if (frame.Event.SchemaVersion != EventSchema || frame.Event.RunId != runId || frame.Event.Sequence != revision || frame.Event.PayloadAvailable)
            throw new InvalidDataException("runtime-run-history-event-integrity");
        if ((revision == frame.EarliestRevision) != (frame.Baseline is not null))
            throw new InvalidDataException("runtime-run-history-baseline-integrity");
        if (frame.Baseline is not null && (ProjectionHash(frame.Baseline) != frame.BeforeProjectionHash || frame.BeforeProjectionHash != frame.AfterProjectionHash))
            throw new InvalidDataException("runtime-run-history-baseline-hash");
    }

    private void WriteNewFrameUnsafe(string runId, ProjectionRevisionFrame frame)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(frame, _lineJson));
        if (bytes.Length > _maxRecordBytes) throw new InvalidDataException("runtime-run-history-record-exceeds-byte-limit");
        Directory.CreateDirectory(RevisionsPath(runId));
        WriteAtomicUnsafe(RevisionPath(runId, frame.Revision), bytes, overwrite: false);
    }

    private void WriteEnvelopeUnsafe(ProjectionSnapshotEnvelope envelope)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, _json));
        if (bytes.Length > _maxRecordBytes) throw new InvalidDataException("runtime-run-projection-record-exceeds-byte-limit");
        WriteAtomicUnsafe(ProjectionPath(envelope.Projection.RunId), bytes, overwrite: true);
    }

    private static void WriteAtomicUnsafe(string path, byte[] bytes, bool overwrite)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static string HashFrame(ProjectionRevisionFrame frame) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(frame with { Hash = "" }))));
    private JsonElement ProjectionElement(RuntimeRunProjection projection)
    {
        return CanonicalProjectionElement(JsonSerializer.Serialize(projection, _lineJson));
    }
    private static string ProjectionHash(RuntimeRunProjection projection)
    {
        return ProjectionHash(CanonicalProjectionElement(JsonSerializer.Serialize(projection, CreateJsonOptions(writeIndented: false))));
    }
    private static string ProjectionHash(JsonElement projection) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(projection.GetRawText())));
    private static JsonElement CanonicalProjectionElement(string json)
    {
        var node = JsonNode.Parse(json) ?? throw new InvalidDataException("runtime-run-projection-canonical-empty");
        return JsonSerializer.SerializeToElement(node);
    }
    private string ReadLimitedTextUnsafe(string path)
    {
        var length = new FileInfo(path).Length;
        if (length < 0 || length > _maxRecordBytes)
            throw new InvalidDataException("runtime-run-record-exceeds-byte-limit");
        return File.ReadAllText(path);
    }
    private static IEnumerable<long> Revisions(long first, long last)
    {
        for (var current = first; ; current++)
        {
            yield return current;
            if (current == last) yield break;
        }
    }
    private RuntimeRunProjection DeserializeProjection(JsonElement element) => JsonSerializer.Deserialize<RuntimeRunProjection>(element.GetRawText(), _json) ?? throw new InvalidDataException("runtime-run-history-projection-empty");
    private void ThrowIfFaultedUnsafe() { if (_faulted) throw new IOException("runtime-run-store-write-uncertain"); }
    private string RevisionsPath(string runId) => Path.Combine(_root, runId + ".revisions");
    private string RevisionPath(string runId, long revision) => Path.Combine(RevisionsPath(runId), revision.ToString("D20", System.Globalization.CultureInfo.InvariantCulture) + ".revision.json");

    public sealed record RuntimeRunProjectionVersion(RuntimeRunProjection Projection, long Revision, long EarliestRevision,
        string RecordingId, int ReadCost, bool CompleteHistory);

    private sealed record ProjectionState(RuntimeRunProjection Projection, ProjectionSnapshotEnvelope? Envelope);
    private sealed record ProjectionSnapshotEnvelope(string Schema, string RecordingId, long Revision, long EarliestRevision, string FrameHash,
        bool CompleteHistory, RuntimeRunProjection Projection);
    private sealed record ProjectionRevisionFrame(string Schema, string RecordingId, long Revision, long EarliestRevision,
        RuntimeRunProjection? Baseline, RuntimeRunEvent Event, ReversibleJsonDelta? Delta, string BeforeProjectionHash,
        string AfterProjectionHash, string PreviousHash, string Hash);
}
