using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UniClaw.Host.Runtime;

/// <summary>v2 storage for the two core histories. It deliberately keeps the v1 reader separate.</summary>
internal sealed partial class VersionedCommittedHistory<T> : IDisposable, IAsyncDisposable where T : class
{
    private sealed record Frame(string Schema, string RecordingId, T Row, ReversibleJsonDelta? Delta, string Before, string After,
        string Previous, long PreviousOffset, string Hash);
    private sealed record Baseline(string FileName, string Hash, int Sequence, long LastOffset, long NextOffset, int Bytes);
    private sealed record Head(string Schema, string RecordingId, int Sequence, string Hash, long Length, long LastOffset, IReadOnlyList<Baseline> Baselines);
    private sealed record State(string RecordingId, int Sequence, string FrameHash, long LastOffset, long NextOffset, T Row, JsonElement Mutable);
    private readonly string _path, _headPath, _error; private readonly int _max; private readonly JsonSerializerOptions _json;
    private readonly IHistoryVersionCodec<T> _codec; private readonly HistoryVersionOptions _options; private readonly FileStream _stream;
    private readonly SemaphoreSlim _gate = new(1, 1); private bool _disposed, _faulted; private Head? _head; private JsonElement _mutable;
    private readonly HistoryWriteMeter _writeMeter = new();
    private long _baselineWrites, _baselineFlushes, _baselineBytes;
    public string RecordingId { get; private set; } = "";
    public bool CanInitialize
    {
        get;
    }
    public HistorySnapshotWriteCounts VersionWriteStatistics => new(_baselineWrites, _baselineFlushes, _baselineBytes);
    public HistoryWriteCounts WriteStatistics => _writeMeter.Snapshot;
    public VersionedCommittedHistory(string path, bool readOnly, string error, JsonSerializerOptions json, int max, HistoryVersionOptions options, IHistoryVersionCodec<T> codec)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (max <= 0 || options.MaxTailRecords <= 0 || options.MinDeltaBytesForSnapshot < 1 || options.SnapshotByteRatio <= 0)
            throw new ArgumentOutOfRangeException(nameof(options));
        _path = path;
        _headPath = path + ".head";
        _error = error;
        _json = new(json)
        {
            RespectRequiredConstructorParameters = true
        };
        _max = max;
        _options = options;
        _codec = codec;
        CanInitialize = !readOnly;
        _stream = new(path, readOnly ? FileMode.Open : FileMode.CreateNew, readOnly ? FileAccess.Read : FileAccess.ReadWrite,
            readOnly ? FileShare.ReadWrite : FileShare.Read, 4096, readOnly ? FileOptions.Asynchronous : FileOptions.Asynchronous | FileOptions.WriteThrough);
        if (!readOnly)
            RecordingId = Guid.NewGuid().ToString("N");
        else
        {
            try
            {
                var head = ReadHead(new Meter());
                RecordingId = head.RecordingId;
                _head = head;
            }
            catch { _stream.Dispose(); throw; }
        }
    }
    public async ValueTask AppendBatchAsync(IReadOnlyList<T> records, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var input = records.ToArray();
        if (input.Length is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(records));
        if (!_gate.Wait(0))
            throw new InvalidOperationException(_error + "-write-in-progress");
        try
        {
            if (!CanInitialize || _faulted || _disposed)
                throw new IOException(_error + "-not-writable");
            var expected = _head?.Sequence ?? 0;
            if (input.Select(_codec.Sequence).Where((s, i) => s != expected + i + 1).Any())
                throw new InvalidDataException(_error + "-sequence");
            try
            {
                var workingHead = _head;
                var workingMutable = _mutable;
                var baselines = workingHead?.Baselines.ToList() ?? [];
                var pendingBaselines = new List<(string Name, byte[] Bytes)>();
                using var encoded = new MemoryStream();
                foreach (var row in input)
                {
                    var offset = _stream.Length + encoded.Length;
                    var mutable = _codec.Mutable(row);
                    var first = workingHead is null;
                    var stored = first ? row : _codec.WithoutMutable(row);
                    var frame = new Frame(_codec.VersionSchema, RecordingId, stored,
                        first ? null : ReversibleJsonDelta.Between(workingMutable, mutable),
                        first ? "" : Hash(workingMutable), Hash(mutable), workingHead?.Hash ?? "", workingHead?.LastOffset ?? -1, "");
                    frame = frame with
                    {
                        Hash = Hash(JsonSerializer.Serialize(frame with
                        {
                            Hash = ""
                        }, _json))
                    };
                    var data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(frame, _json) + "\n");
                    if (data.Length > _max - encoded.Length)
                        throw new InvalidDataException(_error + "-record-exceeds-byte-limit");
                    encoded.Write(data);
                    var next = _stream.Length + encoded.Length;
                    if (baselines.Count == 0 || _codec.Sequence(row) - baselines[^1].Sequence >= _options.MaxTailRecords
                        || next - baselines[^1].NextOffset >= Math.Max(_options.MinDeltaBytesForSnapshot, (long)baselines[^1].Bytes * _options.SnapshotByteRatio))
                    {
                        var state = new State(RecordingId, _codec.Sequence(row), frame.Hash, offset, next, _codec.WithoutMutable(row), mutable);
                        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state, _json));
                        if (bytes.Length > _max)
                            throw new InvalidDataException(_error + "-snapshot-exceeds-byte-limit");
                        var hash = Hash(Encoding.UTF8.GetString(bytes));
                        var name = _codec.Sequence(row).ToString("D10") + "-" + hash + ".json";
                        var baseline = new Baseline(name, hash, _codec.Sequence(row), offset, next, bytes.Length);
                        baselines.Add(baseline);
                        pendingBaselines.Add((name, bytes));
                    }
                    workingHead = new Head(_codec.HeadSchema, RecordingId, _codec.Sequence(row), frame.Hash, next, offset, baselines.ToArray());
                    workingMutable = mutable;
                }
                var headBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(workingHead!, _json) + "\n");
                if (headBytes.Length > _max)
                    throw new InvalidDataException(_error + "-metadata-byte-limit");
                var payload = encoded.ToArray();
                _writeMeter.FrameWrite(input.Length, payload.Length);
                await _stream.WriteAsync(payload).ConfigureAwait(false);
                _writeMeter.FrameFlush();
                await _stream.FlushAsync().ConfigureAwait(false);
                if (pendingBaselines.Count > 0)
                    Directory.CreateDirectory(_path + ".versions");
                foreach (var baseline in pendingBaselines)
                    await WriteAtomicAsync(Path.Combine(_path + ".versions", baseline.Name), baseline.Bytes, false).ConfigureAwait(false);
                await WriteAtomicAsync(_headPath, headBytes, true).ConfigureAwait(false);
                _writeMeter.Committed();
                _head = workingHead;
                _mutable = workingMutable;
            }
            catch { _faulted = true; throw; }
        }
        finally { _gate.Release(); }
    }
    private async ValueTask WriteAtomicAsync(string path, byte[] bytes, bool head)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                if (!head)
                {
                    _baselineWrites++;
                    _baselineBytes += bytes.Length;
                }
                else
                    _writeMeter.HeadWrite();
                await file.WriteAsync(bytes).ConfigureAwait(false);
                if (!head)
                    _baselineFlushes++;
                else
                    _writeMeter.HeadFlush();
                await file.FlushAsync().ConfigureAwait(false);
            }
            File.Move(temp, path, head);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult(); public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
                return;
            _disposed = true;
            await _stream.DisposeAsync().ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
    private static string Hash(JsonElement value) => Hash(value.GetRawText()); private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
