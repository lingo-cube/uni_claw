using System.Collections.ObjectModel;
using System.Text.Json;
using UniClaw.Kernel.Run;

namespace UniClaw.Kernel.Runtime;

/// <summary>Trusted original Plan-adoption storage. Append success acknowledges one complete record.</summary>
public interface IRuntimePlanJournal
{
    bool CanInitialize { get; }
    string RecordingId { get; }
    IReadOnlyList<RuntimePlanHistoryRecord> ReadRecords();
    void Append(RuntimePlanHistoryRecord record);
}

/// <summary>Completes after the original commit, never after queue admission alone.</summary>
public interface IAsyncRuntimePlanJournal : IRuntimePlanJournal
{
    ValueTask AppendAsync(RuntimePlanHistoryRecord record, CancellationToken cancellationToken = default);
}

public sealed record RuntimePlanHistoryPosition(string RecordingId, int Sequence);

/// <summary>Original admission/commit data, not an execution cursor or a resumed Plan.</summary>
public sealed record RuntimePlanHistoryRecord(string Schema, int Sequence, string Operation,
    IReadOnlyDictionary<string, int> Versions, JsonElement? Graph = null, string? DecisionId = null,
    int? ReplacesAdoptionSequence = null, int? ReplacementSequence = null,
    RunIdentityRecord? Run = null, string? RunRecordingId = null);

/// <summary>Source commits can outlive a lost acknowledgement. No row proves that execution started.
/// Full running cursors, Control work receipts and mechanical waits are not reconstructed.</summary>
public sealed record RuntimePlanHistoryRead(RuntimePlanHistoryPosition Position, string RunRecordingId,
    IReadOnlyList<RuntimePlanHistoryRecord> Records, RuntimePlanHistoryRecord? LatestCommittedAdoption,
    RuntimePlanHistoryRecord? PendingReplacement)
{
    internal const string Schema = "uniclaw.runtime.plan.adoption.v1";
    public bool ContinuationSupported => false;

    public static RuntimePlanHistoryRead ReadOriginal(IRuntimePlanJournal source, string expectedRecordingId, RunModel originalRun)
    {
        if (!originalRun.IsReadOnly || originalRun.HistoryPosition is not { } position)
            throw new InvalidDataException("runtime-plan-original-run-required");
        var read = Read(source.ReadRecords(), source.RecordingId, expectedRecordingId);
        if (read.RunRecordingId != position.RecordingId
            || JsonSerializer.Serialize(read.Records[0].Run, RuntimePlanGraph.Json)
                != JsonSerializer.Serialize(originalRun.CaptureIdentity(), RuntimePlanGraph.Json))
            throw new InvalidDataException("runtime-plan-run-source-mismatch");
        return read;
    }

    internal static RuntimePlanHistoryRead Read(IReadOnlyList<RuntimePlanHistoryRecord> rows, string actualSource, string expectedSource)
    {
        if (actualSource != expectedSource || !Guid.TryParseExact(expectedSource, "N", out _) || rows.Count == 0)
            throw new InvalidDataException("runtime-plan-source-missing");
        var first = rows[0];
        if (first.Operation != "initialize" || first.Run is null || !Guid.TryParseExact(first.RunRecordingId, "N", out _))
            throw new InvalidDataException("runtime-plan-initial-identity");
        _ = RunModel.ReadArchivedIdentity(first.Run, first.Run.RunId);
        var versions = new Dictionary<string, int>(StringComparer.Ordinal);
        var frozen = new List<RuntimePlanHistoryRecord>();
        RuntimePlanHistoryRecord? latest = null, pending = null;
        foreach (var row in rows)
        {
            if (row.Schema != Schema || row.Sequence != frozen.Count + 1 || row.Versions is null
                || row.Sequence > 1 && (row.Run is not null || row.RunRecordingId is not null))
                throw new InvalidDataException("runtime-plan-sequence-or-identity");
            if (row.Sequence == 1)
            {
                if (row.Graph is not null || row.DecisionId is not null || row.Versions.Count != 0
                    || row.ReplacesAdoptionSequence is not null || row.ReplacementSequence is not null)
                    throw new InvalidDataException("runtime-plan-initial-state");
            }
            else
            {
                if (row.Graph is not { } graph || string.IsNullOrWhiteSpace(row.DecisionId))
                    throw new InvalidDataException("runtime-plan-adoption-input");
                var plan = RuntimePlanGraph.Validate(graph);
                if (row.Operation == "replacement-begin")
                {
                    if (latest is null || pending is not null || row.ReplacesAdoptionSequence != latest.Sequence || row.ReplacementSequence is not null)
                        throw new InvalidDataException("runtime-plan-replacement-reference");
                    pending = row;
                }
                else if (row.Operation == "adopt")
                {
                    if (pending is null && (row.ReplacementSequence is not null || row.ReplacesAdoptionSequence is not null)
                        || pending is not null && (row.ReplacementSequence != pending.Sequence
                            || row.ReplacesAdoptionSequence != pending.ReplacesAdoptionSequence
                            || row.DecisionId != pending.DecisionId || !JsonElement.DeepEquals(graph, pending.Graph!.Value)))
                        throw new InvalidDataException("runtime-plan-replacement-incomplete");
                    if (plan.PlanId is { } id)
                    {
                        if (versions.TryGetValue(id, out var previous) && plan.Version <= previous)
                            throw new InvalidDataException("runtime-plan-version-reused");
                        versions[id] = plan.Version;
                    }
                    pending = null; latest = row;
                }
                else throw new InvalidDataException("runtime-plan-operation");
            }
            if (versions.Count != row.Versions.Count || versions.Any(pair => !row.Versions.TryGetValue(pair.Key, out var version) || pair.Value != version))
                throw new InvalidDataException("runtime-plan-version-table");
            var copy = row with
            {
                Graph = row.Graph?.Clone(),
                Versions = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(row.Versions, StringComparer.Ordinal)),
                Run = row.Run is null ? null : JsonSerializer.Deserialize<RunIdentityRecord>(JsonSerializer.Serialize(row.Run, RuntimePlanGraph.Json), RuntimePlanGraph.Json),
            };
            frozen.Add(copy);
            if (latest?.Sequence == row.Sequence) latest = copy;
            if (pending?.Sequence == row.Sequence) pending = copy;
        }
        return new(new(actualSource, rows.Count), first.RunRecordingId!, frozen.AsReadOnly(), latest, pending);
    }
}

internal sealed class RuntimePlanPersistenceException(string message, Exception? inner = null) : IOException(message, inner);
