using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Kernel.World;

namespace UniClaw.Host.Runtime;

/// <summary>Exclusive original World writer or strict historical reader. No append-on-restart.</summary>
public sealed partial class FileWorldJournal : IAsyncWorldJournal, IDisposable, IAsyncDisposable
{
    private readonly HistoryWriteMeter _writeMeter = new();
    public HistoryWriteCounts WriteStatistics => _writeMeter.Snapshot;

    private const string Schema = "uniclaw.world.history.v3";
    private const string DeltaSchema = "uniclaw.world.history.v2";
    private const string LegacySchema = "uniclaw.world.history.v1";
    private sealed record Frame(string Schema, string RecordingId, int Sequence, string Previous, string? Payload, string Hash);
    private sealed record Head(string Schema, string RecordingId, int Sequence, string Hash);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly FileStream _stream;
    private readonly string _headPath;
    private readonly List<WorldHistoryRecord> _records = [];
    private string _hash = "";
    private bool _faulted;
    private bool _disposed;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly int _maxOperationBytes;
    private int _written;
    private readonly WorldRevisionDeltaWriter _deltaWriter;
    private string _sourceSchema = Schema;
    public bool CanInitialize { get; }
    public string RecordingId { get; private set; } = "";

    public FileWorldJournal(string path, bool readOnly = false, int maxOperationBytes = 16 * 1024 * 1024)
        : this(path, readOnly, maxOperationBytes, false, new()) { }

    public FileWorldJournal(string path, WorldHistoryVersionOptions options, bool readOnly = false, int maxOperationBytes = 16 * 1024 * 1024)
        : this(path, readOnly, maxOperationBytes, false, options) { }

    public static async ValueTask<FileWorldJournal> CreateAsync(string path, int maxOperationBytes = 16 * 1024 * 1024, CancellationToken cancellationToken = default)
        => await CreateAsync(path, new WorldHistoryVersionOptions(), maxOperationBytes, cancellationToken).ConfigureAwait(false);

    public static async ValueTask<FileWorldJournal> CreateAsync(string path, WorldHistoryVersionOptions options,
        int maxOperationBytes = 16 * 1024 * 1024, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = new FileWorldJournal(path, false, maxOperationBytes, true, options);
        try { await source.InitializeFrameAsync().ConfigureAwait(false); return source; }
        catch { await source.DisposeAsync().ConfigureAwait(false); throw; }
    }

    private FileWorldJournal(string path, bool readOnly, int maxOperationBytes, bool deferInitialization,
        WorldHistoryVersionOptions options, bool encodedOnly = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.BaselineInterval < 1) throw new ArgumentOutOfRangeException(nameof(options));
        _deltaWriter = new(options.BaselineInterval);
        if (maxOperationBytes < 512) throw new ArgumentOutOfRangeException(nameof(maxOperationBytes));
        _maxOperationBytes = maxOperationBytes;
        CanInitialize = !readOnly; _headPath = path + ".head";
        _stream = new(path, readOnly ? FileMode.Open : FileMode.CreateNew,
            readOnly ? FileAccess.Read : FileAccess.ReadWrite, readOnly ? FileShare.ReadWrite : FileShare.Read,
            4096, FileOptions.Asynchronous);
        try
        {
            if (!readOnly) { RecordingId = Guid.NewGuid().ToString("N"); if (!deferInitialization) InitializeFrameAsync().AsTask().GetAwaiter().GetResult(); return; }
            EnsureCompleteFile();
            using var reader = new StreamReader(_stream, new UTF8Encoding(false, true), leaveOpen: true);
            var index = 0;
            string? originalSchema = null;
            var decoder = new WorldRevisionDeltaReader();
            while (reader.ReadLine() is { } line)
            {
                var frame = JsonSerializer.Deserialize<Frame>(line, Json) ?? throw new InvalidDataException("world-history-frame");
                if (index == 0) { RecordingId = frame.RecordingId; originalSchema = frame.Schema; _sourceSchema = frame.Schema; }
                if ((frame.Schema != Schema && frame.Schema != DeltaSchema && frame.Schema != LegacySchema) || frame.Schema != originalSchema || !Guid.TryParseExact(frame.RecordingId, "N", out _)
                    || frame.RecordingId != RecordingId || frame.Sequence != index || frame.Previous != _hash
                    || frame.Hash != Hash(_hash, index, frame.Payload)) throw new InvalidDataException("world-history-integrity");
                if (index == 0)
                { if (frame.Payload is not null) throw new InvalidDataException("world-history-initialization"); }
                else
                {
                    var row = JsonSerializer.Deserialize<WorldHistoryRecord>(frame.Payload ?? "null", Json);
                    if (row is null || row.Sequence != index) throw new InvalidDataException("world-history-sequence");
                    if (!encodedOnly && frame.Schema != LegacySchema)
                        row = TransformRevisions(row, revision => decoder.Decode(revision, frame.Schema == Schema));
                    _records.Add(row);
                }
                _hash = frame.Hash; index++;
            }
            EnsureCompleteFile();
            using var headStream = new FileStream(_headPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var headReader = new StreamReader(headStream, new UTF8Encoding(false, true));
            var headText = headReader.ReadToEnd();
            if (!headText.EndsWith('\n') || headText.Count(c => c == '\n') != 1) throw new InvalidDataException("world-head-incomplete");
            var head = JsonSerializer.Deserialize<Head>(headText, Json);
            if (head is null || head.Schema != "uniclaw.world.history-head.v1" || head.RecordingId != RecordingId
                || head.Sequence != _records.Count || head.Hash != _hash) throw new InvalidDataException("world-head-mismatch");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or ArgumentException or OverflowException)
        { _stream.Dispose(); throw new InvalidDataException("world-history-invalid-shape", ex); }
        catch { _stream.Dispose(); throw; }
    }

    private void EnsureCompleteFile()
    {
        if (_stream.Length == 0) throw new InvalidDataException("world-history-empty-or-torn");
        var position = _stream.Position;
        _stream.Position = _stream.Length - 1;
        var last = _stream.ReadByte();
        _stream.Position = position;
        if (last != '\n') throw new InvalidDataException("world-history-empty-or-torn");
    }

    public IReadOnlyList<WorldHistoryRecord> ReadRecords()
    {
        if (_faulted) throw new IOException("world-journal-faulted");
        if (CanInitialize && _written == 0) return [];
        if (CanInitialize)
        {
            using var original = new FileWorldJournal(_stream.Name, readOnly: true);
            return original.ReadRecords();
        }
        // Records/JsonElements are immutable. Return a separate collection while
        // keeping the already validated decode, rather than encoding and parsing it again.
        return _records.ToArray();
    }
    // Explicit synchronous compatibility entrances; native async callers do not use these bridges.
    public void Append(WorldHistoryRecord record) => CommitAsync(record.Sequence,
        () => TransformRevisions(record, revision => _deltaWriter.Encode(revision))).AsTask().GetAwaiter().GetResult();

    public void AppendOperation(WorldHistoryRecord operation, IReadOnlyList<WorldBeliefRevision> revisions) =>
        AppendOperationAsync(operation, revisions).AsTask().GetAwaiter().GetResult();

    public ValueTask InitializeAsync(WorldHistoryRecord record, CancellationToken cancellationToken = default)
    {
        if (record.Sequence != 1 || record.Operation != "initialize") throw new InvalidDataException("world-initialization-record");
        return CommitAsync(record.Sequence, () => record, cancellationToken);
    }

    public ValueTask AppendOperationAsync(WorldHistoryRecord operation, IReadOnlyList<WorldBeliefRevision> revisions,
        CancellationToken cancellationToken = default) => CommitAsync(operation.Sequence, () =>
        {
            var data = new Dictionary<string, object?>
            { ["input"] = operation.Data.GetProperty("input"), ["revisions"] = revisions.Select(r => _deltaWriter.Encode(r)).ToArray() };
            foreach (var field in operation.Data.EnumerateObject())
                if (field.Name != "input") data.Add(field.Name, field.Value);
            return operation with { Data = JsonSerializer.SerializeToElement(data, Json) };
        }, cancellationToken);

    private async ValueTask CommitAsync(int sequence, Func<WorldHistoryRecord> encode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // One outer operation, one in-flight writer. Collection/admission windows are zero.
        if (!_writeGate.Wait(0)) throw new InvalidOperationException("world-journal-write-in-progress");
        try
        {
            if (!CanInitialize || _faulted || _disposed) throw new IOException("world-journal-not-writable");
            if (sequence != _written + 1) throw new InvalidDataException("world-history-sequence");
            try
            {
                var payload = JsonSerializer.Serialize(encode(), Json);
                var hash = Hash(_hash, sequence, payload);
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Frame(Schema, RecordingId, sequence, _hash, payload, hash), Json) + "\n");
                if (bytes.Length > _maxOperationBytes) throw new InvalidDataException("world-operation-exceeds-byte-limit");
                // After acceptance, cancellation cannot report an already-written operation as absent.
                _writeMeter.FrameWrite(1, bytes.Length);
                await _stream.WriteAsync(bytes).ConfigureAwait(false);
                _writeMeter.FrameFlush();
                await _stream.FlushAsync().ConfigureAwait(false);
                await CommitHeadAsync(sequence, hash).ConfigureAwait(false);
                _hash = hash;
                _written++; _writeMeter.Committed();
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
                4096, FileOptions.Asynchronous))
            {
                _writeMeter.HeadWrite();
                await stream.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                    new Head("uniclaw.world.history-head.v1", RecordingId, sequence, hash), Json) + "\n")).ConfigureAwait(false);
                _writeMeter.HeadFlush();
                await stream.FlushAsync().ConfigureAwait(false);
            }
            File.Move(temporary, _headPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static WorldHistoryRecord TransformRevisions(WorldHistoryRecord row, Func<JsonElement, JsonElement> transform) =>
        row with { Data = WorldRevisionDeltaReader.Render(writer =>
        {
            writer.WriteStartObject();
            foreach (var field in row.Data.EnumerateObject())
            {
                writer.WritePropertyName(field.Name);
                if (field.Name != "revisions") { field.Value.WriteTo(writer); continue; }
                writer.WriteStartArray();
                foreach (var revision in field.Value.EnumerateArray()) transform(revision).WriteTo(writer);
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }) };
    private async ValueTask InitializeFrameAsync()
    {
        var hash = Hash(_hash, 0, null);
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Frame(Schema, RecordingId, 0, _hash, null, hash), Json) + "\n");
        _writeMeter.FrameWrite(0, bytes.Length);
        await _stream.WriteAsync(bytes).ConfigureAwait(false);
        _writeMeter.FrameFlush();
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
