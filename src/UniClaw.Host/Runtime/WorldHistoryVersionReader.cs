using System.Text.Json;
using UniClaw.Kernel.World;

namespace UniClaw.Host.Runtime;

/// <summary>Frame validation is performed at reader opening; only selected revisions are decoded per query.
/// SourceBytes is the logical file+head size, not a filesystem IO counter.</summary>
public sealed record WorldHistoryVersionReadCost(int FramesValidated, int RevisionsDecoded, long SourceBytes);

/// <summary>Frozen data projection, not complete World semantic certification or a live WorldModel.</summary>
public sealed record WorldHistoryVersionRead(WorldHistoryPosition SourcePosition, int RevisionNumber, int OperationSequence,
    JsonElement Revision, int BaselineRevisionNumber, WorldHistoryVersionReadCost ReadCost)
{
    public bool ContinuationSupported => false;
    public bool FullWorldHistoryValidated => false;
}

/// <summary>Checks the entire physical chain/head and indexes committed revision envelopes.
/// v3 reconstruction uses an independent baseline and a bounded tail; v2 retains its original prefix requirement.</summary>
public sealed class WorldHistoryVersionReader
{
    private readonly List<(int Operation, JsonElement Packet)> _revisions = [];
    private readonly string _schema;
    private readonly int _frames;
    private readonly long _sourceBytes;
    private readonly int _interval;
    public WorldHistoryPosition SourcePosition { get; }
    public int RevisionCount => _revisions.Count;
    public int? BaselineInterval => _schema == "uniclaw.world.history.v3" && RevisionCount != 0 ? _interval : null;

    public WorldHistoryVersionReader(string path, string expectedWorldRecordingId)
    {
        try
        {
            var source = FileWorldJournal.ReadEncoded(path);
            if (!Guid.TryParseExact(expectedWorldRecordingId, "N", out _) || source.RecordingId != expectedWorldRecordingId)
                throw new InvalidDataException("world-version-recording-mismatch");
            _schema = source.Schema; _frames = source.Rows.Count + 1; _sourceBytes = source.SourceBytes;
            SourcePosition = new(source.RecordingId, source.Rows.Count);
            var interval = 0;
            foreach (var row in source.Rows)
            {
                if (!row.Data.TryGetProperty("revisions", out var values)) continue;
                foreach (var packet in values.EnumerateArray())
                {
                    if (_schema != "uniclaw.world.history.v1")
                        interval = WorldRevisionDeltaReader.ValidateEnvelope(packet, _revisions.Count,
                            _schema == "uniclaw.world.history.v3", interval).Interval;
                    _revisions.Add((row.Sequence, packet));
                }
            }
            _interval = interval;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or ArgumentException or OverflowException)
        { throw new InvalidDataException("world-version-invalid-shape", error); }
    }

    public WorldHistoryVersionRead? ReadLatest() => RevisionCount == 0 ? null : ReadRevision(RevisionCount);

    public WorldHistoryVersionRead ReadRevision(int revisionNumber)
    {
        if (revisionNumber < 1 || revisionNumber > RevisionCount) throw new ArgumentOutOfRangeException(nameof(revisionNumber));
        try
        {
            var index = revisionNumber - 1;
            var start = _schema == "uniclaw.world.history.v1" ? index
                : _schema == "uniclaw.world.history.v3" ? index / _interval * _interval : 0;
            JsonElement revision;
            var decoded = 0;
            if (_schema == "uniclaw.world.history.v1") { revision = _revisions[index].Packet.Clone(); decoded = 1; }
            else
            {
                var decoder = new WorldRevisionDeltaReader(start);
                revision = default;
                for (var i = start; i <= index; i++)
                { revision = decoder.Decode(_revisions[i].Packet, _schema == "uniclaw.world.history.v3"); decoded++; }
            }
            return new(SourcePosition, revisionNumber, _revisions[index].Operation, revision, start + 1,
                new(_frames, decoded, _sourceBytes));
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or ArgumentException or OverflowException)
        { throw new InvalidDataException("world-version-invalid-data", error); }
    }
}
