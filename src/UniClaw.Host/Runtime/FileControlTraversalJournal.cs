using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Kernel.Control;

namespace UniClaw.Host.Runtime;

/// <summary>Original domain adapter; physical frame/head commitment is shared by the two actual buyers.</summary>
public sealed class FileControlTraversalJournal : IAsyncControlTraversalJournal, IDisposable, IAsyncDisposable
{
    private readonly string _path;
    private readonly FileCommittedHistory<ControlTraversalHistoryRecord>? _file;
    private readonly VersionedCommittedHistory<ControlTraversalHistoryRecord>? _versions;
    private static readonly JsonSerializerOptions ReadJson = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private sealed class Codec : IHistoryVersionCodec<ControlTraversalHistoryRecord>
    {
        public string VersionSchema => "uniclaw.control.traversal.history.v2";
        public string HeadSchema => "uniclaw.control.traversal.history-head.v2";
        public int Sequence(ControlTraversalHistoryRecord row) => row.Sequence;
        public JsonElement Mutable(ControlTraversalHistoryRecord row) => row.State.Clone();
        public ControlTraversalHistoryRecord WithoutMutable(ControlTraversalHistoryRecord row) => row with { State = JsonSerializer.SerializeToElement<object?>(null) };
        public ControlTraversalHistoryRecord WithMutable(ControlTraversalHistoryRecord row, JsonElement value) => row with { State = value.Clone() };
    }
    public bool CanInitialize => _file?.CanInitialize ?? _versions!.CanInitialize;
    public string RecordingId => _file?.RecordingId ?? _versions!.RecordingId;
    public bool CanAppendBatch => true;
    public HistoryWriteCounts WriteStatistics => _file?.WriteStatistics ?? _versions!.WriteStatistics;
    public HistorySnapshotWriteCounts VersionWriteStatistics => _versions?.VersionWriteStatistics ?? new(0, 0, 0);
    public FileControlTraversalJournal(string path, bool readOnly = false, int maxRecordBytes = 16 * 1024 * 1024) : this(path, new HistoryVersionOptions(), readOnly, maxRecordBytes) { }
    public FileControlTraversalJournal(string path, HistoryVersionOptions options, bool readOnly = false, int maxRecordBytes = 16 * 1024 * 1024)
    {
        _path = path;
        if (readOnly && VersionedCommittedHistory<ControlTraversalHistoryRecord>.IsLegacy(path, "uniclaw.control.traversal.history.v1", maxRecordBytes))
            _file = new(path, true, "uniclaw.control.traversal.history.v1", "uniclaw.control.traversal.history-head.v1", "control-traversal-history", r => r.Sequence, ReadJson, maxRecordBytes);
        else
            _versions = new(path, readOnly, "control-traversal-history", ReadJson, maxRecordBytes, options, new Codec());
    }
    public IReadOnlyList<ControlTraversalHistoryRecord> ReadRecords() => _file?.ReadRecords() ?? _versions!.ReadRecords();
    public ControlTraversalHistoryVersion ReadLatestVersion() => ReadVersionCore(null, HistoryVersionDirection.Forward);
    public ControlTraversalHistoryVersion ReadVersion(int sequence, HistoryVersionDirection direction = HistoryVersionDirection.Forward) => ReadVersionCore(sequence, direction);
    private ControlTraversalHistoryVersion ReadVersionCore(int? sequence, HistoryVersionDirection direction)
    {
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction));
        if (_file is not null)
        {
            var rows = _file.ReadRecords();
            var index = sequence ?? rows.Count;
            if (index < 1 || index > rows.Count)
                throw new ArgumentOutOfRangeException(nameof(sequence));
            return new(RecordingId, index, rows[index - 1], new(rows.Count, 0, new FileInfo(_path).Length + new FileInfo(_path + ".head").Length));
        }
        var value = _versions!.ReadVersion(sequence, direction);
        return new(RecordingId, value.Sequence, value.Row, value.Cost);
    }
    public void Append(ControlTraversalHistoryRecord record) => AppendAsync(record).AsTask().GetAwaiter().GetResult();
    public ValueTask AppendAsync(ControlTraversalHistoryRecord record, CancellationToken cancellationToken = default) => _file?.AppendAsync(record, cancellationToken) ?? _versions!.AppendBatchAsync([record], cancellationToken);
    public ValueTask AppendBatchAsync(IReadOnlyList<ControlTraversalHistoryRecord> records, CancellationToken cancellationToken = default) => _file?.AppendBatchAsync(records, cancellationToken) ?? _versions!.AppendBatchAsync(records, cancellationToken);
    public void Dispose()
    {
        if (_file is not null)
            _file.Dispose();
        else
            _versions!.Dispose();
    }
    public ValueTask DisposeAsync() => _file?.DisposeAsync() ?? _versions!.DisposeAsync();
}

public sealed record ControlTraversalHistoryVersion(string RecordingId, int Sequence, ControlTraversalHistoryRecord Record, HistoryVersionReadCounts ReadCost);
