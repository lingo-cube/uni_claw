using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Kernel.Run;

namespace UniClaw.Kernel.Control;

/// <summary>Trusted original Control source. Append success acknowledges one complete operation.</summary>
public interface IControlTraversalJournal
{
    bool CanInitialize { get; }
    string RecordingId { get; }
    IReadOnlyList<ControlTraversalHistoryRecord> ReadRecords();
    void Append(ControlTraversalHistoryRecord record);
}

/// <summary>Success confirms the complete original operation, never just queued acceptance.</summary>
public interface IAsyncControlTraversalJournal : IControlTraversalJournal
{
    ValueTask AppendAsync(ControlTraversalHistoryRecord record, CancellationToken cancellationToken = default);
    /// <summary>Optional joint acknowledgement of the two original configure/start rows.</summary>
    bool CanAppendBatch => false;
    ValueTask AppendBatchAsync(IReadOnlyList<ControlTraversalHistoryRecord> records, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("control-traversal-batch-not-supported");
}

public sealed record ControlTraversalHistoryPosition(string RecordingId, int Sequence);

/// <summary>Frozen native input/result/state. This is never a replay command or issued intent.</summary>
public sealed record ControlTraversalHistoryRecord(string Schema, int Sequence, string Operation,
    JsonElement Input, JsonElement State, JsonElement Result, RunIdentityRecord? Run = null, string? RunRecordingId = null);

/// <summary>Historical original Control traversal only. No ControlLoop/Task is reconstructed.</summary>
public sealed record ControlTraversalHistoryRead(ControlTraversalHistoryPosition Position, string RunRecordingId,
    IReadOnlyList<ControlTraversalHistoryRecord> Records, JsonElement State)
{
    internal const string Schema = "uniclaw.control.traversal.v1";
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public bool ContinuationSupported => false;

    public static ControlTraversalHistoryRead ReadOriginal(IControlTraversalJournal journal, string expectedRecordingId, RunModel originalRun)
    {
        if (!originalRun.IsReadOnly || originalRun.HistoryPosition is not { } runPosition)
            throw new InvalidDataException("control-traversal-original-run-required");
        var result = Read(journal.ReadRecords(), journal.RecordingId, expectedRecordingId);
        if (result.RunRecordingId != runPosition.RecordingId
            || JsonSerializer.Serialize(result.Records[0].Run, Json) != JsonSerializer.Serialize(originalRun.CaptureIdentity(), Json))
            throw new InvalidDataException("control-traversal-run-source-mismatch");
        return result;
    }

    internal static ControlTraversalHistoryRead Read(IReadOnlyList<ControlTraversalHistoryRecord> rows, string actualRecording, string expectedRecording)
    {
        if (actualRecording != expectedRecording || !Guid.TryParseExact(expectedRecording, "N", out _) || rows.Count == 0)
            throw new InvalidDataException("control-traversal-source-missing");
        var first = rows[0];
        if (first.Operation != "initialize" || first.Run is null || !Guid.TryParseExact(first.RunRecordingId, "N", out _))
            throw new InvalidDataException("control-traversal-initial-identity");
        _ = RunModel.ReadArchivedIdentity(first.Run, first.Run.RunId);
        var frozen = new List<ControlTraversalHistoryRecord>();
        var selected = new Dictionary<string, string>(StringComparer.Ordinal);
        var priorOrdinal = 0;
        foreach (var row in rows)
        {
            if (row.Schema != Schema || row.Sequence != frozen.Count + 1
                || row.Sequence > 1 && (row.Run is not null || row.RunRecordingId is not null || row.Operation == "initialize"))
                throw new InvalidDataException("control-traversal-sequence-or-identity");
            TraversalControl.ValidateRecordedOperation(row);
            var ordinal = row.State.GetProperty("invocationOrdinal").GetInt32();
            if (row.Operation == "select" && row.Result.ValueKind == JsonValueKind.Object)
            {
                var work = row.Result.Deserialize<TraversalControl.Work>(Json)!;
                if (ordinal != priorOrdinal + 1 || work.InvocationId != $"{row.State.GetProperty("planId").GetString()}:{row.State.GetProperty("version").GetInt32()}:traversal-{ordinal}"
                    || !selected.TryAdd(work.InvocationId, JsonSerializer.Serialize(work, Json)))
                    throw new InvalidDataException("control-traversal-selection-identity");
            }
            else if (ordinal != priorOrdinal) throw new InvalidDataException("control-traversal-ordinal-transition");
            if (row.Operation is "navigated" or "returned" or "read-only-completed")
            {
                var work = row.Input.GetProperty("work").Deserialize<TraversalControl.Work>(Json)!;
                if (!selected.TryGetValue(work.InvocationId, out var original) || original != JsonSerializer.Serialize(work, Json))
                    throw new InvalidDataException("control-traversal-work-source-missing");
            }
            priorOrdinal = ordinal;
            frozen.Add(row with { Input = row.Input.Clone(), Result = row.Result.Clone(), State = row.State.Clone(),
                Run = row.Run is null ? null : JsonSerializer.Deserialize<RunIdentityRecord>(JsonSerializer.Serialize(row.Run, Json), Json) });
        }
        return new(new(actualRecording, rows.Count), first.RunRecordingId!, frozen.AsReadOnly(), frozen[^1].State);
    }
}

internal sealed class ControlTraversalPersistenceException(string message, Exception? inner = null) : IOException(message, inner);
