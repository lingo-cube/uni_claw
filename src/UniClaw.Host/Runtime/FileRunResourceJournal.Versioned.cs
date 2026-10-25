using System.Text;
using System.Text.Json;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Host.Runtime;

public enum ResourceHistoryDirection { Forward, Reverse }
public sealed record ResourceHistoryReadCounts(int FramesRead, int SnapshotReads, long BytesRead);
/// <summary>Data at an original resource sequence; never continuation authority or full-history certification.</summary>
public sealed record RunResourceVersion(string RecordingId, int Sequence, RunResourceRecord Record,
    RunResourceIdentity? Identity, RunResourceSnapshot? Confirmed, ResourceHistoryReadCounts ReadCost);
public sealed record ResourceVersionOptions
{
    public int MaxTailRecords { get; init; } = 64;
    public long MinDeltaBytesForSnapshot { get; init; } = 65536;
    public int SnapshotByteRatio { get; init; } = 4;
}

public sealed partial class FileRunResourceJournal
{
    private const string VersionSchema = "uniclaw.runtime.resource.v4";
    private sealed record VersionFrame(string Schema, string RecordingId, RunResourceRecord Row, bool HasSnapshot,
        ResourceSnapshotDelta? Delta, string? BeforeSnapshot, string? AfterSnapshot, SourcePrefixes? Sources,
        string Previous, long PreviousOffset, string Hash);
    private sealed record Baseline(string FileName, string Hash, int Sequence, long LastOffset, long NextOffset, int Bytes);
    private sealed record VersionHead(string Schema, string RecordingId, int Sequence, string Hash,
        long Length, long LastOffset, IReadOnlyList<Baseline> Baselines);
    private sealed record VersionState(string RecordingId, int Sequence, string FrameHash, long LastOffset, long NextOffset,
        RunResourceRecord Row, bool HasSnapshot, RunResourceIdentity? Identity, RunResourceSnapshot? Snapshot);
    private sealed class ReadMeter
    {
        public int Frames, Snapshots;
        public long Bytes;
        public ResourceHistoryReadCounts Counts => new(Frames, Snapshots, Bytes);
    }
    private VersionHead? _versionHead;
    private RunResourceSnapshot? _versionSnapshot;
    private RunResourceIdentity? _versionIdentity;

    private static bool IsVersioned(Stream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), leaveOpen: true);
        var first = reader.ReadLine(); stream.Position = 0;
        if (first is null) return false;
        using var document = JsonDocument.Parse(first);
        return document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("schema", out var schema)
            && schema.ValueKind == JsonValueKind.String && schema.GetString() == VersionSchema;
    }

    private static string SnapshotHash(RunResourceSnapshot? snapshot) => Hash(JsonSerializer.Serialize(snapshot, Json));
    private static string VersionHash(VersionFrame frame) => Hash(JsonSerializer.Serialize(frame with { Hash = "" }, Json));

    private async ValueTask AppendVersionedAsync(RunResourceRecord row, SourcePrefixes? sources)
    {
        var hasSnapshot = row.Snapshot is not null;
        var nextSnapshot = row.Snapshot ?? _versionSnapshot;
        if (row.Sequence == 1 && (row.Phase != RunResourcePhase.Initialize || nextSnapshot is null))
            throw new InvalidDataException("resource-initial-version-required");
        var delta = row.Sequence > 1 && hasSnapshot ? ResourceSnapshotDelta.Between(_versionSnapshot!, nextSnapshot!) : null;
        var offset = _stream.Length;
        var frame = new VersionFrame(VersionSchema, RecordingId!, row.Sequence == 1 ? row : row with { Snapshot = null },
            hasSnapshot, delta, row.Sequence == 1 ? null : SnapshotHash(_versionSnapshot), SnapshotHash(nextSnapshot), sources,
            _versionHead?.Hash ?? "", _versionHead?.LastOffset ?? -1, "");
        frame = frame with { Hash = VersionHash(frame) };
        var data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(frame, Json) + "\n");
        if (data.Length > _maxRecordBytes) throw new InvalidDataException("resource-record-exceeds-byte-limit");
        _writeMeter.FrameWrite(1, data.Length);
        await _stream.WriteAsync(data).ConfigureAwait(false);
        _writeMeter.FrameFlush(); await _stream.FlushAsync().ConfigureAwait(false);
        var baselines = _versionHead?.Baselines.ToList() ?? [];
        if (baselines.Count == 0 || row.Sequence - baselines[^1].Sequence >= _versionOptions.MaxTailRecords
            || _stream.Length - baselines[^1].NextOffset >= Math.Max(_versionOptions.MinDeltaBytesForSnapshot,
                (long)baselines[^1].Bytes * _versionOptions.SnapshotByteRatio))
        {
            var state = new VersionState(RecordingId!, row.Sequence, frame.Hash, offset, _stream.Length,
                frame.Row, hasSnapshot, row.Identity ?? _versionIdentity, nextSnapshot);
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state, Json));
            if (bytes.Length > _maxRecordBytes) throw new InvalidDataException("resource-snapshot-exceeds-byte-limit");
            var hash = Hash(Encoding.UTF8.GetString(bytes));
            var name = row.Sequence.ToString("D10") + "-" + hash + ".json";
            var directory = _path + ".snapshots"; Directory.CreateDirectory(directory);
            await WriteVersionFileAsync(Path.Combine(directory, name), bytes, false).ConfigureAwait(false);
            baselines.Add(new(name, hash, row.Sequence, offset, _stream.Length, bytes.Length));
        }
        var head = new VersionHead(VersionSchema, RecordingId!, row.Sequence, frame.Hash, _stream.Length, offset, baselines);
        await WriteVersionFileAsync(_headPath, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(head, Json) + "\n"), true).ConfigureAwait(false);
        _versionSnapshot = nextSnapshot; _versionIdentity = row.Identity ?? _versionIdentity; _versionHead = head;
        _writeMeter.Committed();
    }

    private async ValueTask WriteVersionFileAsync(string path, byte[] data, bool replace)
    {
        if (data.Length > _maxRecordBytes) throw new InvalidDataException("resource-version-metadata-byte-limit");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                if (replace) _writeMeter.HeadWrite();
                await file.WriteAsync(data).ConfigureAwait(false);
                if (replace) _writeMeter.HeadFlush();
                await file.FlushAsync().ConfigureAwait(false);
            }
            File.Move(temporary, path, overwrite: replace);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private byte[] ReadBounded(string path, ReadMeter meter)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (file.Length > _maxRecordBytes) throw new InvalidDataException("resource-version-metadata-byte-limit");
        using var output = new MemoryStream(); var buffer = new byte[65536]; int count;
        while ((count = file.Read(buffer)) > 0)
        {
            meter.Bytes += count;
            if (output.Length + count > _maxRecordBytes) throw new InvalidDataException("resource-version-metadata-byte-limit");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    private VersionHead ReadVersionHead(ReadMeter meter)
    {
        var bytes = ReadBounded(_headPath, meter);
        if (bytes.Length == 0 || bytes[^1] != (byte)'\n' || bytes.Count(x => x == (byte)'\n') != 1)
            throw new InvalidDataException("resource-head-incomplete");
        var head = JsonSerializer.Deserialize<VersionHead>(bytes, Json) ?? throw new InvalidDataException("resource-head-invalid");
        if (head.Schema != VersionSchema || !Guid.TryParseExact(head.RecordingId, "N", out _)
            || (RecordingId is not null && RecordingId != head.RecordingId) || head.Sequence < 1
            || head.Length != new FileInfo(_path).Length || head.LastOffset < 0 || head.LastOffset >= head.Length
            || head.Baselines is null || head.Baselines.Count == 0 || head.Baselines[0].Sequence != 1)
            throw new InvalidDataException("resource-head-mismatch");
        var previous = 0;
        foreach (var baseline in head.Baselines)
        {
            if (baseline is null || baseline.Sequence <= previous || baseline.Sequence > head.Sequence || baseline.LastOffset < 0
                || baseline.NextOffset <= baseline.LastOffset || baseline.NextOffset > head.Length
                || Path.GetFileName(baseline.FileName) != baseline.FileName || baseline.FileName.Contains("..", StringComparison.Ordinal)
                || baseline.FileName.Contains('\\') || baseline.Bytes < 1 || baseline.Bytes > _maxRecordBytes)
                throw new InvalidDataException("resource-baseline-index-invalid");
            previous = baseline.Sequence;
        }
        var last = ReadFrame(head.LastOffset, meter);
        if (last.Next != head.Length || last.Frame.Hash != head.Hash || last.Frame.Row.Sequence != head.Sequence
            || last.Frame.RecordingId != head.RecordingId) throw new InvalidDataException("resource-head-mismatch");
        return head;
    }

    private (VersionFrame Frame, long Next) ReadFrame(long offset, ReadMeter meter)
    {
        using var file = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (offset < 0 || offset >= file.Length) throw new InvalidDataException("resource-frame-offset");
        file.Position = offset;
        using var output = new MemoryStream(); var buffer = new byte[4096];
        while (true)
        {
            var count = file.Read(buffer); meter.Bytes += count;
            if (count == 0) throw new InvalidDataException("resource-torn-tail");
            var newline = Array.IndexOf(buffer, (byte)'\n', 0, count);
            var take = newline < 0 ? count : newline;
            if (output.Length + take > _maxRecordBytes) throw new InvalidDataException("resource-record-exceeds-byte-limit");
            output.Write(buffer, 0, take);
            if (newline < 0) continue;
            var frame = JsonSerializer.Deserialize<VersionFrame>(output.ToArray(), Json) ?? throw new InvalidDataException("resource-frame-invalid");
            meter.Frames++;
            if (frame.Row is null || frame.Schema != VersionSchema || !Guid.TryParseExact(frame.RecordingId, "N", out _)
                || frame.Hash != VersionHash(frame) || frame.Row.Sequence < 1)
                throw new InvalidDataException("resource-frame-integrity");
            return (frame, offset + output.Length + 1);
        }
    }

    private VersionState ReadBaseline(Baseline reference, VersionHead head, ReadMeter meter)
    {
        var bytes = ReadBounded(Path.Combine(_path + ".snapshots", reference.FileName), meter); meter.Snapshots++;
        if (bytes.Length != reference.Bytes || Hash(Encoding.UTF8.GetString(bytes)) != reference.Hash)
            throw new InvalidDataException("resource-baseline-integrity");
        var state = JsonSerializer.Deserialize<VersionState>(bytes, Json) ?? throw new InvalidDataException("resource-baseline-invalid");
        var frame = ReadFrame(reference.LastOffset, meter);
        if (state.Row is null || state.RecordingId != head.RecordingId || frame.Frame.RecordingId != head.RecordingId
            || state.Sequence != reference.Sequence || state.Row.Sequence != reference.Sequence
            || frame.Frame.Row.Sequence != state.Sequence || state.LastOffset != reference.LastOffset
            || state.NextOffset != reference.NextOffset || frame.Next != reference.NextOffset
            || state.FrameHash != frame.Frame.Hash || SnapshotHash(state.Snapshot) != frame.Frame.AfterSnapshot
            || state.HasSnapshot != frame.Frame.HasSnapshot || JsonSerializer.Serialize(state.Row, Json) != JsonSerializer.Serialize(frame.Frame.Row, Json))
            throw new InvalidDataException("resource-baseline-binding");
        return state;
    }

    private VersionState Forward(VersionState state, VersionFrame frame, long offset, long next)
    {
        if (frame.Row.Sequence != state.Sequence + 1 || frame.RecordingId != state.RecordingId
            || frame.Previous != state.FrameHash || frame.PreviousOffset != state.LastOffset
            || frame.BeforeSnapshot != SnapshotHash(state.Snapshot) || frame.HasSnapshot != (frame.Delta is not null)
            || frame.Row.Snapshot is not null || frame.Row.Identity is not null)
            throw new InvalidDataException("resource-delta-base");
        var snapshot = frame.HasSnapshot ? frame.Delta!.Apply(state.Snapshot!) : state.Snapshot;
        if (SnapshotHash(snapshot) != frame.AfterSnapshot) throw new InvalidDataException("resource-delta-result");
        return new(state.RecordingId, frame.Row.Sequence, frame.Hash, offset, next, frame.Row,
            frame.HasSnapshot, state.Identity, snapshot);
    }

    public RunResourceVersion ReadLatestVersion() => ReadVersionCore(null, ResourceHistoryDirection.Forward);

    public RunResourceVersion ReadVersion(int sequence, ResourceHistoryDirection direction = ResourceHistoryDirection.Forward) =>
        ReadVersionCore(sequence, direction);

    private RunResourceVersion ReadVersionCore(int? requested, ResourceHistoryDirection direction)
    {
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        var meter = new ReadMeter();
        if (!_versioned)
        {
            using var file = File.OpenRead(_path); var rows = Load(file).Payloads;
            var sequence = requested ?? rows.Count;
            if (sequence < 1 || sequence > rows.Count) throw new ArgumentOutOfRangeException(nameof(sequence));
            RunResourceIdentity? identity = null; RunResourceSnapshot? snapshot = null; RunResourceRecord? row = null;
            foreach (var payload in rows.Take(sequence))
            { row = JsonSerializer.Deserialize<RunResourceRecord>(payload, Json)!; identity = row.Identity ?? identity; snapshot = row.Snapshot ?? snapshot; }
            return new(RecordingId ?? "", sequence, row!, identity, snapshot, new(rows.Count, 0, file.Length));
        }
        var head = ReadVersionHead(meter);
        var target = requested ?? head.Sequence;
        if (target < 1 || target > head.Sequence) throw new ArgumentOutOfRangeException(nameof(requested));
        var reference = direction == ResourceHistoryDirection.Forward
            ? head.Baselines.Last(x => x.Sequence <= target)
            : head.Baselines.FirstOrDefault(x => x.Sequence >= target) ?? head.Baselines[^1];
        var state = ReadBaseline(reference, head, meter);
        while (state.Sequence < target || (direction == ResourceHistoryDirection.Reverse && state.Sequence < head.Sequence && reference.Sequence < target))
        {
            var (frame, next) = ReadFrame(state.NextOffset, meter);
            state = Forward(state, frame, state.NextOffset, next);
            if (state.Sequence == (direction == ResourceHistoryDirection.Reverse ? head.Sequence : target)) break;
        }
        while (state.Sequence > target)
        {
            var (current, _) = ReadFrame(state.LastOffset, meter);
            var (previous, next) = ReadFrame(current.PreviousOffset, meter);
            if (current.Hash != state.FrameHash || current.Row.Sequence != state.Sequence
                || previous.Hash != current.Previous || previous.Row.Sequence != state.Sequence - 1
                || previous.RecordingId != state.RecordingId || next != state.LastOffset
                || SnapshotHash(state.Snapshot) != current.AfterSnapshot)
                throw new InvalidDataException("resource-reverse-delta-base");
            var snapshot = current.HasSnapshot ? current.Delta!.Apply(state.Snapshot!, reverse: true) : state.Snapshot;
            if (SnapshotHash(snapshot) != current.BeforeSnapshot || SnapshotHash(snapshot) != previous.AfterSnapshot)
                throw new InvalidDataException("resource-reverse-delta-result");
            state = new(state.RecordingId, previous.Row.Sequence, previous.Hash, current.PreviousOffset, next,
                previous.Row, previous.HasSnapshot, state.Identity, snapshot);
        }
        if (target == head.Sequence && (state.FrameHash != head.Hash || state.LastOffset != head.LastOffset
            || state.NextOffset != head.Length))
            throw new InvalidDataException("resource-latest-endpoint-mismatch");
        return new(state.RecordingId, target, state.Row with { Snapshot = state.HasSnapshot ? state.Snapshot : null },
            state.Identity, state.Snapshot, meter.Counts);
    }

    private IReadOnlyList<RunResourceRecord> ReadVersionRecords()
    {
        if (new FileInfo(_path).Length == 0) return [];
        var meter = new ReadMeter(); var head = ReadVersionHead(meter);
        var (first, next) = ReadFrame(0, meter);
        if (first.Row.Sequence != 1 || first.Row.Phase != RunResourcePhase.Initialize || first.Row.Snapshot is null
            || first.RecordingId != head.RecordingId || first.Previous != "" || first.PreviousOffset != -1
            || first.Delta is not null || !first.HasSnapshot || SnapshotHash(first.Row.Snapshot) != first.AfterSnapshot)
            throw new InvalidDataException("resource-initial-version-invalid");
        if (first.Sources is not null && (first.Sources.Consultation.Length != 0 || first.Sources.Execution.Length != 0))
            throw new InvalidDataException("resource-source-prefix-not-empty");
        var state = new VersionState(first.RecordingId, 1, first.Hash, 0, next, first.Row, true, first.Row.Identity, first.Row.Snapshot);
        var rows = new List<RunResourceRecord> { first.Row };
        while (state.NextOffset < head.Length)
        {
            var loaded = ReadFrame(state.NextOffset, meter);
            state = Forward(state, loaded.Frame, state.NextOffset, loaded.Next);
            rows.Add(state.Row with { Snapshot = state.HasSnapshot ? state.Snapshot : null });
        }
        if (state.Sequence != head.Sequence || state.FrameHash != head.Hash || state.NextOffset != head.Length)
            throw new InvalidDataException("resource-head-mismatch");
        return rows;
    }
}
