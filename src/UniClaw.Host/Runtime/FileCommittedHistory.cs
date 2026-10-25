using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UniClaw.Host.Runtime;

/// <summary>Managed frame/head calls only; not physical disk syscalls or recording authority.</summary>
public sealed record HistoryWriteCounts(long FrameWrites, long FrameFlushes, long HeadWrites, long HeadFlushes,
    long FrameBytesSubmitted, int CommittedBatches, int PeakBatchRecords, int PeakBatchBytes);

/// <summary>Exclusive original writer or strict read-only history. No append-on-restart.</summary>
internal sealed class FileCommittedHistory<TRecord> : IDisposable, IAsyncDisposable where TRecord : class
{
    private readonly string _schema;
    private readonly string _headSchema;
    private readonly string _error;
    private readonly Func<TRecord, int> _sequence;
    private readonly JsonSerializerOptions _readJson;
    private readonly int _maxRecordBytes;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private bool _disposed;
    private long _frameWrites, _frameFlushes, _headWrites, _headFlushes, _frameBytes;
    private int _batches, _peakRecords, _peakBytes;
    public HistoryWriteCounts WriteStatistics => new(_frameWrites, _frameFlushes, _headWrites, _headFlushes,
        _frameBytes, _batches, _peakRecords, _peakBytes);
    private sealed record Frame(string Schema, string RecordingId, string Previous, string Payload, string Hash);
    private sealed record Head(string Schema, string RecordingId, int Sequence, string Hash);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly FileStream _stream;
    private readonly string _headPath;
    private readonly List<string> _payloads = [];
    private int _written;
    private string _hash = "";
    private bool _faulted;
    public bool CanInitialize { get; }
    public string RecordingId { get; private set; } = "";

    internal FileCommittedHistory(string path, bool readOnly, string schema, string headSchema, string error,
        Func<TRecord, int> sequence, JsonSerializerOptions readJson, int maxRecordBytes)
    {
        if (maxRecordBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxRecordBytes));
        _schema = schema; _headSchema = headSchema; _error = error; _sequence = sequence; _readJson = readJson;
        _maxRecordBytes = maxRecordBytes;
        CanInitialize = !readOnly;
        _headPath = path + ".head";
        _stream = new(path, readOnly ? FileMode.Open : FileMode.CreateNew,
            readOnly ? FileAccess.Read : FileAccess.ReadWrite, readOnly ? FileShare.ReadWrite : FileShare.Read,
            4096, readOnly ? FileOptions.Asynchronous : FileOptions.Asynchronous | FileOptions.WriteThrough);
        try
        {
            if (!readOnly) { RecordingId = Guid.NewGuid().ToString("N"); return; }
            using var reader = new StreamReader(_stream, new UTF8Encoding(false, true), leaveOpen: true);
            var text = reader.ReadToEnd();
            if (!text.EndsWith('\n')) throw new InvalidDataException(_error + "-empty-or-torn");
            foreach (var line in text[..^1].Split('\n'))
            {
                var frame = JsonSerializer.Deserialize<Frame>(line, Json) ?? throw new InvalidDataException(_error + "-frame");
                if (_payloads.Count == 0) RecordingId = frame.RecordingId;
                if (frame.Schema != _schema
                    || !Guid.TryParseExact(frame.RecordingId, "N", out _)
                    || frame.RecordingId != RecordingId || frame.Previous != _hash
                    || frame.Hash != Hash(_hash + "\n" + RecordingId + "\n" + frame.Payload))
                    throw new InvalidDataException(_error + "-integrity");
                var row = JsonSerializer.Deserialize<TRecord>(frame.Payload, _readJson);
                if (row is null || _sequence(row) != _payloads.Count + 1) throw new InvalidDataException(_error + "-sequence");
                _payloads.Add(frame.Payload); _hash = frame.Hash;
            }
            using var headStream = new FileStream(_headPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var headReader = new StreamReader(headStream, new UTF8Encoding(false, true));
            var headText = headReader.ReadToEnd();
            if (!headText.EndsWith('\n') || headText.Count(c => c == '\n') != 1)
                throw new InvalidDataException(_error + "-head-incomplete");
            var head = JsonSerializer.Deserialize<Head>(headText, Json);

            if (head is null || head.Schema != _headSchema || head.RecordingId != RecordingId
                || head.Sequence != _payloads.Count || head.Hash != _hash)
                throw new InvalidDataException(_error + "-head-mismatch");
        }
        catch { _stream.Dispose(); throw; }
    }

    public IReadOnlyList<TRecord> ReadRecords()
    {
        if (!CanInitialize) return _payloads.Select(payload => JsonSerializer.Deserialize<TRecord>(payload,
            _readJson)!).ToArray();
        if (!_writeGate.Wait(0)) throw new InvalidOperationException(_error + "-write-in-progress");
        try
        {
            if (_faulted) throw new IOException(_error + "-faulted");
            if (_written == 0) return [];
            // The original writer retains only its acknowledged endpoint. Explicit
            // history reads validate the original file, never an unacknowledged suffix.
            using var reader = new FileCommittedHistory<TRecord>(_stream.Name, true, _schema, _headSchema,
                _error, _sequence, _readJson, _maxRecordBytes);
            if (reader.RecordingId != RecordingId || reader._payloads.Count != _written || reader._hash != _hash)
                throw new InvalidDataException(_error + "-writer-endpoint-mismatch");
            return reader.ReadRecords();
        }
        finally { _writeGate.Release(); }
    }

    public ValueTask AppendAsync(TRecord record, CancellationToken cancellationToken = default) => AppendBatchAsync([record], cancellationToken);

    public async ValueTask AppendBatchAsync(IReadOnlyList<TRecord> records, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (records.Count is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(records));
        var input = records.ToArray();
        if (input.Length is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(records));
        if (!_writeGate.Wait(0)) throw new InvalidOperationException(_error + "-write-in-progress");
        try
        {
            if (!CanInitialize || _faulted || _disposed) throw new IOException(_error + "-not-writable");
            for (var index = 0; index < input.Length; index++)
                if (_sequence(input[index]) != _written + index + 1) throw new InvalidDataException(_error + "-sequence");
            try
            {
                var hash = _hash;
                using var encoded = new MemoryStream();
                foreach (var record in input)
                {
                    var payload = JsonSerializer.Serialize(record, Json);
                    var previous = hash; hash = Hash(previous + "\n" + RecordingId + "\n" + payload);
                    var frame = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Frame(_schema, RecordingId, previous, payload, hash), Json) + "\n");
                    if (frame.Length > _maxRecordBytes - encoded.Length) throw new InvalidDataException(_error + "-record-exceeds-byte-limit");
                    encoded.Write(frame);
                }
                var data = encoded.ToArray();
                _peakRecords = Math.Max(_peakRecords, input.Length); _peakBytes = Math.Max(_peakBytes, data.Length);
                // After acceptance, settle the original WriteThrough/async commit even if the caller cancels.
                _frameWrites++; _frameBytes += data.Length;
                await _stream.WriteAsync(data).ConfigureAwait(false);
                _frameFlushes++;
                await _stream.FlushAsync().ConfigureAwait(false);
                await CommitHeadAsync(_sequence(input[^1]), hash).ConfigureAwait(false);
                _written += input.Length; _hash = hash; _batches++;
            }
            catch { _faulted = true; throw; }
        }
        finally { _writeGate.Release(); }
    }

    private async ValueTask CommitHeadAsync(int sequence, string hash)
    {
        var temporary = _headPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
                4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                _headWrites++;
                await stream.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                    new Head(_headSchema, RecordingId, sequence, hash), Json) + "\n")).ConfigureAwait(false);
                _headFlushes++;
                await stream.FlushAsync().ConfigureAwait(false);
            }
            File.Move(temporary, _headPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    public async ValueTask DisposeAsync()
    {
        await _writeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;
            await _stream.DisposeAsync().ConfigureAwait(false);
        }
        finally { _writeGate.Release(); }
    }
}

/// <summary>Counts native managed calls for the existing file adapters; never recording authority.</summary>
internal sealed class HistoryWriteMeter
{
    private long _writes, _flushes, _headWrites, _headFlushes, _bytes;
    private int _batches, _peakRecords, _peakBytes;
    public HistoryWriteCounts Snapshot => new(_writes, _flushes, _headWrites, _headFlushes, _bytes, _batches, _peakRecords, _peakBytes);
    public void FrameWrite(int records, int bytes) { _writes++; _bytes += bytes; _peakRecords = Math.Max(_peakRecords, records); _peakBytes = Math.Max(_peakBytes, bytes); }
    public void FrameFlush() => _flushes++;
    public void HeadWrite() => _headWrites++;
    public void HeadFlush() => _headFlushes++;
    public void Committed() => _batches++;
}
