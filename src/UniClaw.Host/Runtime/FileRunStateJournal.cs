using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Kernel.Run;

namespace UniClaw.Host.Runtime;

/// <summary>Exclusive original writer or strict read-only history. No append-on-restart.</summary>
public sealed class FileRunStateJournal : IAsyncRunStateJournal, IDisposable, IAsyncDisposable
{
    private readonly HistoryWriteMeter _writeMeter = new();
    public HistoryWriteCounts WriteStatistics => _writeMeter.Snapshot;

    private const string Schema = "uniclaw.run.history.v2";
    private const string LegacySchema = "uniclaw.run.history.v1";
    private sealed record Frame(string Schema, string RecordingId, string Previous, string Payload, string Hash);
    private sealed record Head(string Schema, string RecordingId, int Sequence, string Hash);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions ExecutionJson = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private readonly FileStream _stream;
    private readonly string _headPath;
    private readonly List<string> _payloads = [];
    private int _written;
    private string _hash = "";
    private string _formatSchema = Schema;
    private bool _faulted;
    private bool _disposed;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly int _maxRecordBytes;
    public bool CanInitialize { get; }
    public string RecordingId { get; private set; } = "";

    public FileRunStateJournal(string path, bool readOnly = false, int maxRecordBytes = 16 * 1024 * 1024)
    {
        if (maxRecordBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxRecordBytes));
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
            if (!text.EndsWith('\n')) throw new InvalidDataException("run-history-empty-or-torn");
            foreach (var line in text[..^1].Split('\n'))
            {
                var frame = JsonSerializer.Deserialize<Frame>(line, Json) ?? throw new InvalidDataException("run-history-frame");
                if (_payloads.Count == 0) { RecordingId = frame.RecordingId; _formatSchema = frame.Schema; }
                if (frame.Schema != _formatSchema || frame.Schema is not (Schema or LegacySchema)
                    || !Guid.TryParseExact(frame.RecordingId, "N", out _)
                    || frame.RecordingId != RecordingId || frame.Previous != _hash
                    || frame.Hash != Hash(_hash + "\n" + RecordingId + "\n" + frame.Payload))
                    throw new InvalidDataException("run-history-integrity");
                var row = JsonSerializer.Deserialize<RunHistoryRecord>(frame.Payload, _formatSchema == Schema ? ExecutionJson : Json);
                if (row is null || row.Sequence != _payloads.Count + 1) throw new InvalidDataException("run-history-sequence");
                if (_formatSchema == LegacySchema && (row.Traversal is not null || row.Transition == RunHistoryTransition.Traversal)
                    || _formatSchema == Schema && _payloads.Count == 0 && row.Traversal is null)
                    throw new InvalidDataException("run-history-format-provenance");
                _payloads.Add(frame.Payload); _hash = frame.Hash;
            }
            using var headStream = new FileStream(_headPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var headReader = new StreamReader(headStream, new UTF8Encoding(false, true));
            var headText = headReader.ReadToEnd();
            if (!headText.EndsWith('\n') || headText.Count(c => c == '\n') != 1)
                throw new InvalidDataException("run-history-head-incomplete");
            var head = JsonSerializer.Deserialize<Head>(headText, Json);
            var headSchema = _formatSchema == Schema ? "uniclaw.run.history-head.v2" : "uniclaw.run.history-head.v1";
            if (head is null || head.Schema != headSchema || head.RecordingId != RecordingId
                || head.Sequence != _payloads.Count || head.Hash != _hash)
                throw new InvalidDataException("run-history-head-mismatch");
        }
        catch { _stream.Dispose(); throw; }
    }

    public IReadOnlyList<RunHistoryRecord> ReadRecords()
    {
        if (!CanInitialize) return _payloads.Select(payload => JsonSerializer.Deserialize<RunHistoryRecord>(payload,
            _formatSchema == Schema ? ExecutionJson : Json)!).ToArray();
        if (!_writeGate.Wait(0)) throw new InvalidOperationException("run-journal-write-in-progress");
        try
        {
            if (_faulted) throw new IOException("run-history-faulted");
            if (_written == 0) return [];
            // Reuse strict original-source validation only for an explicit history
            // read. Normal appends retain no second serialized history in memory.
            using var reader = new FileRunStateJournal(_stream.Name, readOnly: true, maxRecordBytes: _maxRecordBytes);
            if (reader.RecordingId != RecordingId || reader._payloads.Count != _written || reader._hash != _hash)
                throw new InvalidDataException("run-history-writer-endpoint-mismatch");
            return reader.ReadRecords();
        }
        finally { _writeGate.Release(); }
    }

    public void Append(RunHistoryRecord record) => AppendAsync(record).AsTask().GetAwaiter().GetResult();

    public async ValueTask AppendAsync(RunHistoryRecord record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_writeGate.Wait(0)) throw new InvalidOperationException("run-journal-write-in-progress");
        try
        {
            if (!CanInitialize || _faulted || _disposed) throw new IOException("run-history-not-writable");
            if (record.Sequence != _written + 1) throw new InvalidDataException("run-history-sequence");
            try
            {
                var payload = JsonSerializer.Serialize(record, Json);
                var hash = Hash(_hash + "\n" + RecordingId + "\n" + payload);
                var data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Frame(Schema, RecordingId, _hash, payload, hash), Json) + "\n");
                if (data.Length > _maxRecordBytes) throw new InvalidDataException("run-record-exceeds-byte-limit");
                // WriteThrough retains the original disk-flush request while payload I/O
                // uses the platform async API. An accepted write must settle despite cancellation.
                _writeMeter.FrameWrite(1, data.Length);
                await _stream.WriteAsync(data).ConfigureAwait(false);
                _writeMeter.FrameFlush();
                await _stream.FlushAsync().ConfigureAwait(false);
                await CommitHeadAsync(record.Sequence, hash).ConfigureAwait(false);
                _written++; _hash = hash; _writeMeter.Committed();
            }
            catch { _faulted = true; throw; }
        }
        finally { _writeGate.Release(); }
    }

    private async ValueTask CommitHeadAsync(int sequence, string hash)
    {
        // Publish the committed endpoint after the frame is flushed. A crash
        // between these two writes is rejected on read, never rolled back.
        var temporary = _headPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
                4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                _writeMeter.HeadWrite();
                await stream.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                    new Head("uniclaw.run.history-head.v2", RecordingId, sequence, hash), Json) + "\n")).ConfigureAwait(false);
                _writeMeter.HeadFlush();
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
