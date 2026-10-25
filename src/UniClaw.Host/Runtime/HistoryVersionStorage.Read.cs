using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UniClaw.Host.Runtime;

internal sealed partial class VersionedCommittedHistory<T> where T : class
{
    private void CheckWriterEndpoint(Head head)
    {
        if (CanInitialize && (_head is null || head.RecordingId != RecordingId || head.Sequence != _head.Sequence
            || head.Hash != _head.Hash || head.Length != _head.Length))
            throw new InvalidDataException(_error + "-writer-endpoint-mismatch");
    }

    internal static bool IsLegacy(string path, string legacySchema, int maxRecordBytes)
    {
        if (maxRecordBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxRecordBytes));
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var line = new MemoryStream();
        int value;
        while ((value = stream.ReadByte()) >= 0 && value != '\n')
        {
            if (line.Length >= maxRecordBytes)
                throw new InvalidDataException("history-format-byte-limit");
            line.WriteByte((byte)value);
        }
        if (value < 0 || line.Length == 0)
            throw new InvalidDataException("history-format-incomplete");
        using var document = JsonDocument.Parse(line.ToArray());
        return document.RootElement.TryGetProperty("schema", out var schema) && schema.GetString() == legacySchema;
    }

    private sealed class Meter
    {
        public int Frames, Snapshots; public long Bytes;
    }
    private byte[] ReadBounded(string path, Meter meter)
    {
        var length = new FileInfo(path).Length;
        if (length > _max)
            throw new InvalidDataException(_error + "-metadata-byte-limit");
        var bytes = File.ReadAllBytes(path);
        meter.Bytes += bytes.Length;
        return bytes;
    }
    private Head ReadHead(Meter meter)
    {
        var bytes = ReadBounded(_headPath, meter);
        if (bytes.Length == 0 || bytes[^1] != (byte)'\n' || bytes.Count(b => b == (byte)'\n') != 1)
            throw new InvalidDataException(_error + "-head-incomplete");
        var head = JsonSerializer.Deserialize<Head>(bytes, _json) ?? throw new InvalidDataException(_error + "-head-invalid");
        if (head.Schema != _codec.HeadSchema || !Guid.TryParseExact(head.RecordingId, "N", out _) || (!CanInitialize && RecordingId != "" && RecordingId != head.RecordingId) || head.Sequence < 1 || head.Length != new FileInfo(_path).Length || head.LastOffset < 0 || head.Baselines is null || head.Baselines.Count == 0 || head.Baselines[0].Sequence != 1)
            throw new InvalidDataException(_error + "-head-mismatch");
        var previous = 0;
        foreach (var baseline in head.Baselines)
        {
            if (baseline.Sequence <= previous || baseline.Sequence > head.Sequence || baseline.LastOffset < 0 || baseline.NextOffset <= baseline.LastOffset || baseline.NextOffset > head.Length || baseline.Bytes < 1 || baseline.Bytes > _max || Path.GetFileName(baseline.FileName) != baseline.FileName || baseline.FileName.Contains("..", StringComparison.Ordinal) || baseline.FileName.Contains('\\'))
                throw new InvalidDataException(_error + "-baseline-index-invalid");
            previous = baseline.Sequence;
        }
        var last = ReadFrame(head.LastOffset, meter);
        if (last.Next != head.Length || last.Value.Hash != head.Hash || _codec.Sequence(last.Value.Row) != head.Sequence || last.Value.RecordingId != head.RecordingId)
            throw new InvalidDataException(_error + "-head-mismatch");
        return head;
    }
    private (Frame Value, long Next) ReadFrame(long offset, Meter meter)
    {
        using var file = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (offset < 0 || offset >= file.Length)
            throw new InvalidDataException(_error + "-frame-offset");
        file.Position = offset;
        using var line = new MemoryStream();
        int b;
        while ((b = file.ReadByte()) >= 0 && b != '\n')
        {
            if (line.Length >= _max)
                throw new InvalidDataException(_error + "-record-exceeds-byte-limit");
            line.WriteByte((byte)b);
        }
        if (b < 0)
            throw new InvalidDataException(_error + "-torn-tail");
        meter.Bytes += line.Length + 1;
        meter.Frames++;
        var frame = JsonSerializer.Deserialize<Frame>(line.ToArray(), _json) ?? throw new InvalidDataException(_error + "-frame-invalid");
        if (frame.Schema != _codec.VersionSchema || frame.Row is null || !Guid.TryParseExact(frame.RecordingId, "N", out _) || frame.Hash != Hash(JsonSerializer.Serialize(frame with
        {
            Hash = ""
        }, _json)))
            throw new InvalidDataException(_error + "-frame-integrity");
        return (frame, offset + line.Length + 1);
    }
    private State ReadBaseline(Baseline baseline, Head head, Meter meter)
    {
        if (Path.GetFileName(baseline.FileName) != baseline.FileName || baseline.FileName.Contains("..", StringComparison.Ordinal))
            throw new InvalidDataException(_error + "-baseline-index-invalid");
        var bytes = ReadBounded(Path.Combine(_path + ".versions", baseline.FileName), meter);
        meter.Snapshots++;
        if (bytes.Length != baseline.Bytes || Hash(Encoding.UTF8.GetString(bytes)) != baseline.Hash)
            throw new InvalidDataException(_error + "-baseline-integrity");
        var state = JsonSerializer.Deserialize<State>(bytes, _json) ?? throw new InvalidDataException(_error + "-baseline-invalid");
        var frame = ReadFrame(baseline.LastOffset, meter);
        if (state.RecordingId != head.RecordingId || state.Sequence != baseline.Sequence || _codec.Sequence(state.Row) != state.Sequence || state.LastOffset != baseline.LastOffset || state.FrameHash != frame.Value.Hash || frame.Value.RecordingId != head.RecordingId || _codec.Sequence(frame.Value.Row) != state.Sequence || frame.Next != baseline.NextOffset || state.NextOffset != baseline.NextOffset || Hash(state.Mutable) != frame.Value.After || JsonSerializer.Serialize(state.Row, _json) != JsonSerializer.Serialize(_codec.WithoutMutable(frame.Value.Row), _json))
            throw new InvalidDataException(_error + "-baseline-binding");
        return state with
        {
            Row = _codec.WithMutable(state.Row, state.Mutable)
        };
    }
    private State Forward(State state, Frame frame, long offset, long next)
    {
        if (_codec.Sequence(frame.Row) != state.Sequence + 1 || frame.RecordingId != state.RecordingId || frame.Previous != state.FrameHash || frame.PreviousOffset != state.LastOffset || frame.Before != Hash(state.Mutable) || frame.Delta is null)
            throw new InvalidDataException(_error + "-delta-base");
        var mutable = frame.Delta.Apply(state.Mutable);
        if (Hash(mutable) != frame.After)
            throw new InvalidDataException(_error + "-delta-result");
        return new(state.RecordingId, _codec.Sequence(frame.Row), frame.Hash, offset, next, _codec.WithMutable(frame.Row, mutable), mutable);
    }
    public (T Row, int Sequence, HistoryVersionReadCounts Cost) ReadVersion(int? requested, HistoryVersionDirection direction)
    {
        if (CanInitialize && !_gate.Wait(0))
            throw new InvalidOperationException(_error + "-write-in-progress");
        try
        {
            if (CanInitialize && _faulted)
                throw new IOException(_error + "-faulted");
            if (CanInitialize && _head is null)
                throw new InvalidDataException(_error + "-source-missing");
            var meter = new Meter();
            var head = ReadHead(meter);
            CheckWriterEndpoint(head);
            var target = requested ?? head.Sequence;
            if (target < 1 || target > head.Sequence)
                throw new ArgumentOutOfRangeException(nameof(requested));
            var baseline = direction == HistoryVersionDirection.Forward ? head.Baselines.Last(b => b.Sequence <= target) : head.Baselines.FirstOrDefault(b => b.Sequence >= target) ?? head.Baselines[^1];
            var state = ReadBaseline(baseline, head, meter);
            while (state.Sequence < target)
            {
                var f = ReadFrame(state.NextOffset, meter);
                state = Forward(state, f.Value, state.NextOffset, f.Next);
            }
            while (state.Sequence > target)
            {
                var current = ReadFrame(state.LastOffset, meter);
                var previous = ReadFrame(current.Value.PreviousOffset, meter);
                if (current.Value.Hash != state.FrameHash || current.Value.Previous != previous.Value.Hash || current.Value.RecordingId != previous.Value.RecordingId
                    || previous.Next != state.LastOffset || previous.Value.After != current.Value.Before || _codec.Sequence(previous.Value.Row) != state.Sequence - 1 || current.Value.Delta is null || current.Value.After != Hash(state.Mutable))
                    throw new InvalidDataException(_error + "-reverse-delta-base");
                var mutable = current.Value.Delta.Apply(state.Mutable, reverse: true);
                if (Hash(mutable) != current.Value.Before)
                    throw new InvalidDataException(_error + "-reverse-delta-result");
                state = new(state.RecordingId, state.Sequence - 1, previous.Value.Hash, current.Value.PreviousOffset, previous.Next,
                    _codec.WithMutable(previous.Value.Row, mutable), mutable);
            }
            if (target == head.Sequence && (state.FrameHash != head.Hash || state.NextOffset != head.Length))
                throw new InvalidDataException(_error + "-latest-endpoint-mismatch");
            return (state.Row, target, new(meter.Frames, meter.Snapshots, meter.Bytes));
        }
        finally { if (CanInitialize) _gate.Release(); }
    }
    public IReadOnlyList<T> ReadRecords()
    {
        if (CanInitialize && !_gate.Wait(0))
            throw new InvalidOperationException(_error + "-write-in-progress");
        try
        {
            if (CanInitialize && _faulted)
                throw new IOException(_error + "-faulted");
            if (CanInitialize && _head is null)
                return [];
            var meter = new Meter();
            var head = ReadHead(meter);
            CheckWriterEndpoint(head);
            var first = ReadFrame(0, meter);
            var firstMutable = _codec.Mutable(first.Value.Row);
            if (_codec.Sequence(first.Value.Row) != 1 || first.Value.RecordingId != head.RecordingId || first.Value.Previous != "" || first.Value.PreviousOffset != -1 || first.Value.Delta is not null || first.Value.Before != "" || first.Value.After != Hash(firstMutable))
                throw new InvalidDataException(_error + "-initial-version-invalid");
            var state = new State(first.Value.RecordingId, 1, first.Value.Hash, 0, first.Next, first.Value.Row, firstMutable);
            var rows = new List<T> { state.Row };
            while (state.NextOffset < head.Length)
            {
                var f = ReadFrame(state.NextOffset, meter);
                state = Forward(state, f.Value, state.NextOffset, f.Next);
                rows.Add(state.Row);
            }
            if (state.Sequence != head.Sequence || state.FrameHash != head.Hash || state.NextOffset != head.Length)
                throw new InvalidDataException(_error + "-head-mismatch");
            return rows;
        }
        finally { if (CanInitialize) _gate.Release(); }
    }
}
