using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Host.Runtime;

/// <summary>Single-writer, native asynchronous file adapter. Fault scope: one local process;
/// not power loss, OS crash, adversarial file replacement or full Run recovery.</summary>
public sealed partial class FileRunResourceJournal : IAsyncRunResourceJournal, IDisposable, IAsyncDisposable
{
    private readonly HistoryWriteMeter _writeMeter = new();
    public HistoryWriteCounts WriteStatistics => _writeMeter.Snapshot;

    private const string Schema = "uniclaw.runtime.resource.v3";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record SourceStamp(long Length, string Hash);
    private sealed record SourcePrefixes(SourceStamp Consultation, SourceStamp Execution);
    private sealed record Frame(string Schema, string Previous, string Payload, SourcePrefixes? Sources, string Hash, string? RecordingId = null);
    private sealed record Head(string Schema, string RecordingId, int Sequence, string Hash);
    private readonly string _headPath;
    private readonly string? _consultationPath;
    private readonly string? _executionPath;
    private readonly FileStream _stream;
    private readonly List<string> _payloads;
    private string _hash;
    private bool _faulted, _disposed;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly int _maxRecordBytes;
    private readonly string _path;
    private readonly ResourceVersionOptions _versionOptions;
    private readonly bool _versioned;
    public bool CanInitialize { get; }
    public string? RecordingId { get; private set; }

    public FileRunResourceJournal(string path, bool readOnly = false,
        string? consultationPath = null, string? executionPath = null, int maxRecordBytes = 16 * 1024 * 1024)
        : this(path, readOnly, consultationPath, executionPath, maxRecordBytes, null) { }

    public FileRunResourceJournal(string path, ResourceVersionOptions versionOptions, bool readOnly = false,
        string? consultationPath = null, string? executionPath = null, int maxRecordBytes = 16 * 1024 * 1024)
        : this(path, readOnly, consultationPath, executionPath, maxRecordBytes, versionOptions) { }

    private FileRunResourceJournal(string path, bool readOnly, string? consultationPath, string? executionPath,
        int maxRecordBytes, ResourceVersionOptions? versionOptions)
    {
        if (maxRecordBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxRecordBytes));
        _maxRecordBytes = maxRecordBytes;
        _path = Path.GetFullPath(path);
        _versionOptions = versionOptions ?? new();
        if (_versionOptions.MaxTailRecords < 1 || _versionOptions.MinDeltaBytesForSnapshot < 1 || _versionOptions.SnapshotByteRatio < 1)
            throw new ArgumentOutOfRangeException(nameof(versionOptions));
        _headPath = _path + ".head";
        _consultationPath = consultationPath;
        _executionPath = executionPath;
        if (!readOnly) Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        // Atomic creation, rather than platform-dependent sharing, selects the
        // sole writer. Existing histories have no append/restart path in this slice.
        CanInitialize = !readOnly;
        _stream = new FileStream(path, readOnly ? FileMode.Open : FileMode.CreateNew,
            readOnly ? FileAccess.Read : FileAccess.ReadWrite, readOnly ? FileShare.ReadWrite : FileShare.Read, 4096, FileOptions.Asynchronous);
        try
        {
            _versioned = !readOnly || IsVersioned(_stream);
            if (readOnly && !_versioned) { (_payloads, _hash, _, var recordingId) = Load(_stream); RecordingId = recordingId; }
            else
            {
                _payloads = []; _hash = "";
                RecordingId = readOnly ? ReadVersionHead(new()).RecordingId : Guid.NewGuid().ToString("N");
            }
            _stream.Position = _stream.Length;
        }
        catch { _stream.Dispose(); throw; }
    }

    public IReadOnlyList<RunResourceRecord> ReadRecords() => _versioned ? ReadVersionRecords() : _payloads
        .Select(payload => JsonSerializer.Deserialize<RunResourceRecord>(payload, Json)!).ToArray();

    public void Append(RunResourceRecord record) => AppendAsync(record).AsTask().GetAwaiter().GetResult();

    public async ValueTask AppendAsync(RunResourceRecord record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_writeGate.Wait(0)) throw new InvalidOperationException("resource-write-in-progress");
        try
        {
            if (!CanInitialize) throw new InvalidOperationException("resource-history-read-only");
            if (_faulted || _disposed) throw new IOException("resource-journal-faulted");
            if (record.Sequence != (_versioned ? _versionHead?.Sequence ?? 0 : _payloads.Count) + 1) throw new InvalidDataException("resource-sequence-conflict");
            try
            {
                // Freeze before the first asynchronous source read; encoding and
                // byte-limit failures must latch the same accepted-write failure.
                var payload = JsonSerializer.Serialize(record, Json);
                if (Encoding.UTF8.GetByteCount(payload) > _maxRecordBytes) throw new InvalidDataException("resource-record-exceeds-byte-limit");
                if (_versioned) record = JsonSerializer.Deserialize<RunResourceRecord>(payload, Json)!;
                var sources = await CaptureSourcesAsync().ConfigureAwait(false);
                if (record.Phase == RunResourcePhase.Initialize && sources is not null
                    && (sources.Consultation.Length != 0 || sources.Execution.Length != 0))
                    throw new InvalidDataException("resource-source-prefix-not-empty");
                if (_versioned) { await AppendVersionedAsync(record, sources).ConfigureAwait(false); return; }
                var hash = FrameHash(_hash, payload, sources, Schema, RecordingId);
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Frame(Schema, _hash, payload, sources, hash, RecordingId), Json) + "\n");
                if (bytes.Length > _maxRecordBytes) throw new InvalidDataException("resource-record-exceeds-byte-limit");
                _writeMeter.FrameWrite(1, bytes.Length);
                await _stream.WriteAsync(bytes).ConfigureAwait(false);
                _writeMeter.FrameFlush();
                await _stream.FlushAsync().ConfigureAwait(false);
                await CommitHeadAsync(record.Sequence, hash).ConfigureAwait(false);
                _payloads.Add(payload); _hash = hash; _writeMeter.Committed();
            }
            catch { _faulted = true; throw; }
        }
        finally { _writeGate.Release(); }
    }

    private async ValueTask<SourcePrefixes?> CaptureSourcesAsync()
    {
        if (_consultationPath is null || _executionPath is null) return null;
        if (Path.GetFullPath(_consultationPath) == Path.GetFullPath(_executionPath))
            throw new InvalidDataException("resource-source-roles-conflict");
        return new(await StampAsync(_consultationPath).ConfigureAwait(false), await StampAsync(_executionPath).ConfigureAwait(false));
    }

    private static async ValueTask<SourceStamp> StampAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous);
        var length = stream.Length;
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream).ConfigureAwait(false));
        if (length != stream.Length) throw new IOException("resource-source-changed-during-read");
        return new(length, hash);
    }

    public static RunResourceRead Inspect(string path, RunResourceBinding? binding = null,
        string? consultationPath = null, string? executionPath = null)
        => InspectCore(path, binding, consultationPath, executionPath, false);

    internal static RunResourceRead InspectRecovery(string path, RunResourceBinding binding,
        string consultationPath, string executionPath)
        => InspectCore(path, binding, consultationPath, executionPath, true);

    private static RunResourceRead InspectCore(string path, RunResourceBinding? binding,
        string? consultationPath, string? executionPath, bool requireEndpoint)
    {
        RunResourceRead? read = null;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (IsVersioned(stream))
            {
                using var journal = new FileRunResourceJournal(path, readOnly: true);
                var rows = journal.ReadRecords();
                var endpoint = journal.ReadVersionHead(new());
                var last = journal.ReadFrame(endpoint.LastOffset, new()).Frame;
                var exact = false;
                try { exact = last.Sources is not null && last.Sources == CaptureSources(consultationPath, executionPath); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
                return RunResourceAccounting.Inspect(rows, binding, exact, journal.RecordingId) with { CommittedEndpointVerified = true };
            }
            stream.Position = 0;
            var (payloads, hash, sources, recordingId) = Load(stream);
            // Compare bytes without replaying/writing owner journals (their
            // legacy loaders may tolerate torn/invalid tails). Every stamp was
            // captured synchronously after the original owners settled.
            var verified = false;
            try { verified = sources is not null && sources == CaptureSources(consultationPath, executionPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            read = RunResourceAccounting.Inspect(payloads.Select(p =>
                JsonSerializer.Deserialize<RunResourceRecord>(p, Json)!).ToArray(), binding, verified, recordingId);
            if (!requireEndpoint) return read;
            using var headStream = new FileStream(path + ".head", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var headReader = new StreamReader(headStream, new UTF8Encoding(false, true));
            var text = headReader.ReadToEnd();
            if (!text.EndsWith('\n') || text.Count(c => c == '\n') != 1)
                throw new InvalidDataException("resource-head-incomplete");
            var head = JsonSerializer.Deserialize<Head>(text, Json);
            if (head is null || head.Schema != "uniclaw.runtime.resource-head.v1" || head.RecordingId != recordingId
                || head.Sequence != payloads.Count || head.Hash != hash)
                throw new InvalidDataException("resource-head-mismatch");
            return read with { CommittedEndpointVerified = true };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or ArgumentException)
        { return (read ?? new(null, null, null, false, null)) with
            { AccountingComplete = false, Reason = "resource-history-unreadable:" + ex.GetType().Name }; }
    }

    private async ValueTask CommitHeadAsync(int sequence, string hash)
    {
        var temporary = _headPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous))
            {
                _writeMeter.HeadWrite();
                await stream.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                    new Head("uniclaw.runtime.resource-head.v1", RecordingId!, sequence, hash), Json) + "\n"));
                _writeMeter.HeadFlush();
                await stream.FlushAsync().ConfigureAwait(false);
            }
            File.Move(temporary, _headPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static (List<string> Payloads, string Hash, SourcePrefixes? Sources, string? RecordingId) Load(Stream stream)
    {
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), leaveOpen: true);
        var text = reader.ReadToEnd();
        if (text.Length > 0 && !text.EndsWith('\n')) throw new InvalidDataException("resource-torn-tail");
        var result = new List<string>();
        var previous = "";
        SourcePrefixes? sources = null;
        string? recordingId = null;
        string? schema = null;
        foreach (var line in text.Length == 0 ? [] : text[..^1].Split('\n'))
        {
            var frame = JsonSerializer.Deserialize<Frame>(line, Json) ?? throw new InvalidDataException("resource-frame");
            if ((frame.Schema != Schema && frame.Schema != "uniclaw.runtime.resource.v2")
                || (frame.Schema == Schema && !Guid.TryParseExact(frame.RecordingId, "N", out _))
                || (result.Count > 0 && (schema != frame.Schema || recordingId != frame.RecordingId))
                || frame.Previous != previous || frame.Hash != FrameHash(previous, frame.Payload, frame.Sources, frame.Schema, frame.RecordingId))
                throw new InvalidDataException("resource-integrity");
            var record = JsonSerializer.Deserialize<RunResourceRecord>(frame.Payload, Json);
            if (record is null || record.Sequence != result.Count + 1) throw new InvalidDataException("resource-sequence");
            if (record.Phase == RunResourcePhase.Initialize && frame.Sources is not null
                && (frame.Sources.Consultation.Length != 0 || frame.Sources.Execution.Length != 0))
                throw new InvalidDataException("resource-source-prefix-not-empty");
            result.Add(frame.Payload);
            previous = frame.Hash;
            sources = frame.Sources;
            recordingId = frame.RecordingId;
            schema = frame.Schema;
        }
        return (result, previous, sources, recordingId);
    }

    private static SourcePrefixes? CaptureSources(string? consultationPath, string? executionPath)
    {
        if (consultationPath is null || executionPath is null) return null;
        if (Path.GetFullPath(consultationPath) == Path.GetFullPath(executionPath))
            throw new InvalidDataException("resource-source-roles-conflict");
        return new(Stamp(consultationPath), Stamp(executionPath));
    }

    private static SourceStamp Stamp(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var length = stream.Length;
        var hash = Convert.ToHexString(SHA256.HashData(stream));
        if (length != stream.Length) throw new IOException("resource-source-changed-during-read");
        return new(length, hash);
    }

    private static string FrameHash(string previous, string payload, SourcePrefixes? sources, string schema, string? recordingId) =>
        Hash(previous + "\n" + payload + "\n" + JsonSerializer.Serialize(sources, Json)
            + (schema == Schema ? "\n" + recordingId : ""));

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    public async ValueTask DisposeAsync()
    {
        await _writeGate.WaitAsync().ConfigureAwait(false);
        try { if (!_disposed) { _disposed = true; await _stream.DisposeAsync().ConfigureAwait(false); } }
        finally { _writeGate.Release(); }
    }
}
