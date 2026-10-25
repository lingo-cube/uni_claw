using System.Text.Json;

namespace UniClaw.Host.Runtime;

public sealed record ConsultationArtifactLogStatus(int Produced, int Written, int Dropped, int Buffered, bool Closed);

/// <summary>Optional bounded diagnostic records. Drain uses the existing Driver observer;
/// this adapter creates no worker and never acknowledges a formal consultation.</summary>
public sealed class ConsultationArtifactLog
{
    public const string FileName = "consultations.ndjson";
    private readonly string _directory;
    private readonly AuxiliaryArtifacts _artifacts;
    private readonly int _capacity;
    private readonly object _sync = new();
    private readonly Queue<(int Sequence, object Record)> _pending = [];
    private int _produced, _written, _dropped, _draining;
    private bool _closed;
    private bool _snapshotAttempted;

    public ConsultationArtifactLog(string directory, AuxiliaryArtifactLimits? limits = null, int capacity = 32)
        : this(directory, new AuxiliaryArtifacts(directory, limits), capacity) { }

    internal ConsultationArtifactLog(string directory, AuxiliaryArtifacts artifacts, int capacity)
    {
        if (capacity is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(capacity));
        _directory = directory;
        _artifacts = artifacts;
        _capacity = capacity;
    }

    public ConsultationArtifactLogStatus Status
    {
        get { lock (_sync) return new(_produced, _written, _dropped, _pending.Count, _closed); }
    }

    public bool TryRecord(object record)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_sync)
        {
            var sequence = ++_produced;
            if (_closed || _pending.Count == _capacity) { _dropped++; return false; }
            _pending.Enqueue((sequence, record));
            return true;
        }
    }

    public async ValueTask DrainAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _draining, 1, 0) != 0) return;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                (int Sequence, object Record) next;
                lock (_sync) { if (!_pending.TryDequeue(out next)) break; }
                var written = await _artifacts.AppendJsonLineAsync(FileName, () => new
                {
                    schemaVersion = "uniclaw.consultation-diagnostic.v1",
                    sequence = next.Sequence,
                    record = next.Record,
                }, cancellationToken).ConfigureAwait(false);
                lock (_sync) { if (written) _written++; else _dropped++; }
            }
        }
        finally { Volatile.Write(ref _draining, 0); }
    }

    /// <summary>Call after Driver observation close. Pending/unsettled work stays
    /// visible; it is never treated as a completed flush or drained by a second worker.</summary>
    public async ValueTask CompleteAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync) _closed = true;
        if (Interlocked.CompareExchange(ref _draining, 1, 0) != 0) return;
        try
        {
            if (_snapshotAttempted || Status.Buffered != 0 || Status.Written == 0) return;
            _snapshotAttempted = true;
            await _artifacts.WriteAsync("consultations.json", () => ReadCompatibilityRecords(), cancellationToken).ConfigureAwait(false);
        }
        finally { Volatile.Write(ref _draining, 0); }
    }

    private IEnumerable<JsonElement> ReadCompatibilityRecords()
    {
        using var stream = new FileStream(Path.Combine(_directory, FileName), FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            using var document = JsonDocument.Parse(line);
            yield return document.RootElement.GetProperty("record").Clone();
        }
    }
}
