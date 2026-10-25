using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Kernel.Evidence;

namespace UniClaw.Host.Runtime;

/// <summary>Exclusive original Evidence writer or strict historical reader. No append-on-restart.</summary>
public sealed class FileEvidenceJournal : IAsyncEvidenceJournal, IDisposable, IAsyncDisposable
{
    /// <summary>Actual managed file write/flush calls, not physical disk syscalls or authority.</summary>
    public sealed record WriteCounts(long FrameWrites, long FrameFlushes, long HeadWrites, long HeadFlushes,
        long FrameBytesSubmitted, int CommittedBatches, int PeakBatchRecords, int PeakBatchBytes);
    private const string Schema = "uniclaw.evidence.history.v1";
    private sealed record Frame(string Schema, string RecordingId, int Sequence, string Previous, string? Payload, string Hash);
    private sealed record Head(string Schema, string RecordingId, int Sequence, string Hash);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly FileStream _stream;
    private readonly string _headPath;
    private readonly List<string> _payloads = [];
    private string _hash = "";
    private bool _faulted;
    private bool _disposed;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly int _maxBatchRecords;
    private readonly int _maxBatchBytes;
    private long _frameWrites, _frameFlushes, _headWrites, _headFlushes, _frameBytes;
    private int _batches, _peakRecords, _peakBytes;
    public WriteCounts WriteStatistics => new(_frameWrites, _frameFlushes, _headWrites, _headFlushes,
        _frameBytes, _batches, _peakRecords, _peakBytes);
    public bool CanInitialize { get; }
    public string RecordingId { get; private set; } = "";

    public FileEvidenceJournal(string path, bool readOnly = false, int maxBatchRecords = 128, int maxBatchBytes = 1024 * 1024)
        : this(path, readOnly, maxBatchRecords, maxBatchBytes, false) { }

    public static async ValueTask<FileEvidenceJournal> CreateAsync(string path, int maxBatchRecords = 128, int maxBatchBytes = 1024 * 1024, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = new FileEvidenceJournal(path, false, maxBatchRecords, maxBatchBytes, true);
        try { await source.InitializeFrameAsync().ConfigureAwait(false); return source; }
        catch { await source.DisposeAsync().ConfigureAwait(false); throw; }
    }

    private FileEvidenceJournal(string path, bool readOnly, int maxBatchRecords, int maxBatchBytes, bool deferInitialization)
    {
        if (maxBatchRecords < 1 || maxBatchBytes < 512) throw new ArgumentOutOfRangeException(nameof(maxBatchRecords));
        _maxBatchRecords = maxBatchRecords; _maxBatchBytes = maxBatchBytes;
        CanInitialize = !readOnly; _headPath = path + ".head";
        _stream = new(path, readOnly ? FileMode.Open : FileMode.CreateNew,
            readOnly ? FileAccess.Read : FileAccess.ReadWrite, readOnly ? FileShare.ReadWrite : FileShare.Read,
            4096, FileOptions.Asynchronous);
        try
        {
            if (!readOnly) { RecordingId = Guid.NewGuid().ToString("N"); if (!deferInitialization) InitializeFrameAsync().AsTask().GetAwaiter().GetResult(); return; }
            using var reader = new StreamReader(_stream, new UTF8Encoding(false, true), leaveOpen: true);
            var text = reader.ReadToEnd();
            if (!text.EndsWith('\n')) throw new InvalidDataException("evidence-history-empty-or-torn");
            var index = 0;
            foreach (var line in text[..^1].Split('\n'))
            {
                var frame = JsonSerializer.Deserialize<Frame>(line, Json) ?? throw new InvalidDataException("evidence-history-frame");
                if (index == 0) RecordingId = frame.RecordingId;
                if (frame.Schema != Schema || !Guid.TryParseExact(frame.RecordingId, "N", out _)
                    || frame.RecordingId != RecordingId || frame.Sequence != index || frame.Previous != _hash
                    || frame.Hash != Hash(_hash, index, frame.Payload)) throw new InvalidDataException("evidence-history-integrity");
                if (index == 0)
                { if (frame.Payload is not null) throw new InvalidDataException("evidence-history-initialization"); }
                else
                {
                    var row = JsonSerializer.Deserialize<EvidenceHistoryRecord>(frame.Payload ?? "null", Json);
                    if (row is null || row.Sequence != index) throw new InvalidDataException("evidence-history-sequence");
                    _payloads.Add(frame.Payload!);
                }
                _hash = frame.Hash; index++;
            }
            using var headStream = new FileStream(_headPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var headReader = new StreamReader(headStream, new UTF8Encoding(false, true));
            var headText = headReader.ReadToEnd();
            if (!headText.EndsWith('\n') || headText.Count(c => c == '\n') != 1) throw new InvalidDataException("evidence-head-incomplete");
            var head = JsonSerializer.Deserialize<Head>(headText, Json);
            if (head is null || head.Schema != "uniclaw.evidence.history-head.v1" || head.RecordingId != RecordingId
                || head.Sequence != _payloads.Count || head.Hash != _hash) throw new InvalidDataException("evidence-head-mismatch");
        }
        catch { _stream.Dispose(); throw; }
    }

    public IReadOnlyList<EvidenceHistoryRecord> ReadRecords()
    {
        if (_faulted) throw new IOException("evidence-journal-faulted");
        return _payloads.Select(p => JsonSerializer.Deserialize<EvidenceHistoryRecord>(p, Json)!).ToArray();
    }
    public void Append(EvidenceHistoryRecord record)
    {
        // Explicit synchronous compatibility; native async callers never use this bridge.
        AppendBatchAsync([record]).AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask AppendBatchAsync(IReadOnlyList<EvidenceHistoryRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        cancellationToken.ThrowIfCancellationRequested();
        // Exclusive single-Owner writer: no collection delay and no admission queue.
        // Both waiting windows are zero; illegal overlap is rejected before acceptance.
        // Accepted I/O is still awaited to its actual commit boundary.
        if (!_writeGate.Wait(0)) throw new InvalidOperationException("evidence-journal-write-in-progress");
        try
        {
            if (!CanInitialize || _faulted || _disposed) throw new IOException("evidence-journal-not-writable");
            var input = records.ToArray();
            for (var i = 0; i < input.Length; i++)
                if (input[i].Sequence != _payloads.Count + i + 1) throw new InvalidDataException("evidence-history-sequence");
            if (input.Length == 0) return;
            // After acceptance do not cancel an in-flight write and pretend nothing was stored.
            // Each bounded chunk retains every original frame; head acknowledges the exact endpoint.
            try
            {
                var index = 0;
                while (index < input.Length)
                {
                    using var buffer = new MemoryStream(); var payloads = new List<string>(); var hash = _hash;
                    while (index < input.Length && payloads.Count < _maxBatchRecords)
                    {
                        var row = input[index]; var payload = JsonSerializer.Serialize(row, Json);
                        var next = Hash(hash, row.Sequence, payload);
                        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Frame(Schema, RecordingId, row.Sequence, hash, payload, next), Json) + "\n");
                        if (bytes.Length > _maxBatchBytes) throw new InvalidDataException("evidence-record-exceeds-batch-byte-limit");
                        if (buffer.Length + bytes.Length > _maxBatchBytes) break;
                        buffer.Write(bytes); payloads.Add(payload); hash = next; index++;
                    }
                    var data = buffer.ToArray();
                    _frameWrites++; _frameBytes += data.Length;
                    await _stream.WriteAsync(data).ConfigureAwait(false);
                    _frameFlushes++; await _stream.FlushAsync().ConfigureAwait(false);
                    await CommitHeadAsync(input[index - 1].Sequence, hash).ConfigureAwait(false);
                    _payloads.AddRange(payloads); _hash = hash; _batches++;
                    _peakRecords = Math.Max(_peakRecords, payloads.Count); _peakBytes = Math.Max(_peakBytes, data.Length);
                }
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
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous))
            {
                _headWrites++;
                await stream.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                    new Head("uniclaw.evidence.history-head.v1", RecordingId, sequence, hash), Json) + "\n")).ConfigureAwait(false);
                _headFlushes++; await stream.FlushAsync().ConfigureAwait(false);
            }
            File.Move(temporary, _headPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private async ValueTask InitializeFrameAsync()
    {
        var hash = Hash(_hash, 0, null);
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Frame(Schema, RecordingId, 0, _hash, null, hash), Json) + "\n");
        _frameWrites++; _frameBytes += bytes.Length;
        await _stream.WriteAsync(bytes).ConfigureAwait(false);
        _frameFlushes++;
        await _stream.FlushAsync().ConfigureAwait(false);
        await CommitHeadAsync(0, hash).ConfigureAwait(false);
        _hash = hash;
    }
    private string Hash(string previous, int sequence, string? payload) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(previous + "\n" + RecordingId + "\n" + sequence.ToString(CultureInfo.InvariantCulture) + "\n" + (payload ?? ""))));
    public void Dispose()
    {
        _writeGate.Wait();
        try { _disposed = true; _stream.Dispose(); }
        finally { _writeGate.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        await _writeGate.WaitAsync().ConfigureAwait(false);
        try { _disposed = true; await _stream.DisposeAsync().ConfigureAwait(false); }
        finally { _writeGate.Release(); }
    }
}
