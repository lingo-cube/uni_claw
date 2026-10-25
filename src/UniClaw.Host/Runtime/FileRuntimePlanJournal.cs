using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Host.Runtime;

/// <summary>Original domain adapter; physical frame/head commitment is shared by the two actual buyers.</summary>
public sealed class FileRuntimePlanJournal : IAsyncRuntimePlanJournal, IDisposable, IAsyncDisposable
{
    private readonly string _path;
    private readonly FileCommittedHistory<RuntimePlanHistoryRecord>? _file;
    private readonly VersionedCommittedHistory<RuntimePlanHistoryRecord>? _versions;
    private static readonly JsonSerializerOptions ReadJson = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };
    private sealed class Codec : IHistoryVersionCodec<RuntimePlanHistoryRecord>
    {
        public string VersionSchema => "uniclaw.runtime.plan.history.v2";
        public string HeadSchema => "uniclaw.runtime.plan.history-head.v2";
        public int Sequence(RuntimePlanHistoryRecord row) => row.Sequence;
        public JsonElement Mutable(RuntimePlanHistoryRecord row) => JsonSerializer.SerializeToElement(row.Versions.Select(pair => new KeyValuePair<string, int>(pair.Key, pair.Value)).ToArray());
        public RuntimePlanHistoryRecord WithoutMutable(RuntimePlanHistoryRecord row) => row with { Versions = new Dictionary<string, int>(StringComparer.Ordinal) };
        public RuntimePlanHistoryRecord WithMutable(RuntimePlanHistoryRecord row, JsonElement value) => row with { Versions = (value.Deserialize<KeyValuePair<string, int>[]>() ?? throw new InvalidDataException("runtime-plan-history-version-table")).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal) };
    }
    public bool CanInitialize => _file?.CanInitialize ?? _versions!.CanInitialize;
    public string RecordingId => _file?.RecordingId ?? _versions!.RecordingId;
    public HistoryWriteCounts WriteStatistics => _file?.WriteStatistics ?? _versions!.WriteStatistics;
    public HistorySnapshotWriteCounts VersionWriteStatistics => _versions?.VersionWriteStatistics ?? new(0, 0, 0);
    public FileRuntimePlanJournal(string path, bool readOnly = false, int maxRecordBytes = 16 * 1024 * 1024) : this(path, new HistoryVersionOptions(), readOnly, maxRecordBytes) { }
    public FileRuntimePlanJournal(string path, HistoryVersionOptions options, bool readOnly = false, int maxRecordBytes = 16 * 1024 * 1024)
    {
        _path = path;
        if (readOnly && VersionedCommittedHistory<RuntimePlanHistoryRecord>.IsLegacy(path, "uniclaw.runtime.plan.history.v1", maxRecordBytes))
            _file = new(path, true, "uniclaw.runtime.plan.history.v1", "uniclaw.runtime.plan.history-head.v1", "runtime-plan-history", r => r.Sequence, ReadJson, maxRecordBytes);
        else
            _versions = new(path, readOnly, "runtime-plan-history", ReadJson, maxRecordBytes, options, new Codec());
    }
    public IReadOnlyList<RuntimePlanHistoryRecord> ReadRecords() => _file?.ReadRecords() ?? _versions!.ReadRecords();
    public RuntimePlanHistoryVersion ReadLatestVersion() => ReadVersionCore(null, HistoryVersionDirection.Forward);
    public RuntimePlanHistoryVersion ReadVersion(int sequence, HistoryVersionDirection direction = HistoryVersionDirection.Forward) => ReadVersionCore(sequence, direction);
    private RuntimePlanHistoryVersion ReadVersionCore(int? sequence, HistoryVersionDirection direction)
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
    public void Append(RuntimePlanHistoryRecord record) => AppendAsync(record).AsTask().GetAwaiter().GetResult();
    public ValueTask AppendAsync(RuntimePlanHistoryRecord record, CancellationToken cancellationToken = default) => _file?.AppendAsync(record, cancellationToken) ?? _versions!.AppendBatchAsync([record], cancellationToken);
    public void Dispose()
    {
        if (_file is not null)
            _file.Dispose();
        else
            _versions!.Dispose();
    }
    public ValueTask DisposeAsync() => _file?.DisposeAsync() ?? _versions!.DisposeAsync();
}

public sealed record RuntimePlanHistoryVersion(string RecordingId, int Sequence, RuntimePlanHistoryRecord Record, HistoryVersionReadCounts ReadCost);
