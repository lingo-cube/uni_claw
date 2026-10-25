using System.Text.Json;

namespace UniClaw.Kernel.Runtime;

public sealed partial class KernelRunDriver
{
    private readonly List<string> _runtimePlanHistory = [];
    private string? _runtimePlanFailure;
    private int? _runtimePlanReplacement;
    private int? _latestRuntimePlanAdoptionSequence;
    private int? _runtimePlanReplacesAdoptionSequence;

    /// <summary>Only acknowledged original adoption commits; null means no source, not a known-empty history.</summary>
    public RuntimePlanHistoryRead? RuntimePlanHistory => _inputs.RuntimePlanJournal is not { } source || _runtimePlanHistory.Count == 0
        ? null : RuntimePlanHistoryRead.Read(_runtimePlanHistory.Select(row => JsonSerializer.Deserialize<RuntimePlanHistoryRecord>(row, RuntimePlanGraph.Json)!).ToArray(),
            source.RecordingId, source.RecordingId);

    private async ValueTask EnsureRuntimePlanHistoryAsync()
    {
        if (_inputs.RuntimePlanJournal is not { } source) return;
        if (_runtimePlanFailure is not null) throw new RuntimePlanPersistenceException(_runtimePlanFailure);
        if (_runtimePlanReplacement is not null) FailRuntimePlanHistory(new InvalidOperationException("runtime-plan-replacement-unsettled"));
        if (_runtimePlanHistory.Count > 0) return;
        try
        {
            if (!source.CanInitialize || source.ReadRecords().Count > 0 || !Guid.TryParseExact(source.RecordingId, "N", out _)
                || _kernel.RunHistoryPosition is not { } position || !Guid.TryParseExact(position.RecordingId, "N", out _))
                throw new InvalidOperationException("runtime-plan-original-empty-writer-and-run-required");
            await AppendRuntimePlanAsync(new(RuntimePlanHistoryRead.Schema, 1, "initialize", new Dictionary<string, int>(),
                Run: _kernel.CaptureRunIdentity(), RunRecordingId: position.RecordingId));
        }
        catch (Exception ex) { FailRuntimePlanHistory(ex); }
    }

    private async ValueTask BeginRuntimePlanReplacementAsync(AgentDecision.Plan decision)
    {
        if (_inputs.RuntimePlanJournal is null || !_planPaused && _activeTraversal is null) return;
        var previous = _latestRuntimePlanAdoptionSequence
            ?? throw new RuntimePlanPersistenceException("runtime-plan-replacement-original-missing");
        var row = new RuntimePlanHistoryRecord(RuntimePlanHistoryRead.Schema, _runtimePlanHistory.Count + 1,
            "replacement-begin", new Dictionary<string, int>(_latestPlanVersionById, StringComparer.Ordinal),
            RuntimePlanGraph.Capture(decision.Proposal), decision.DecisionId, ReplacesAdoptionSequence: previous);
        await AppendRuntimePlanAsync(row);
        _runtimePlanReplacement = row.Sequence;
        _runtimePlanReplacesAdoptionSequence = previous;
    }

    private async ValueTask CommitRuntimePlanAdoptionAsync(AgentDecision.Plan decision)
    {
        if (_inputs.RuntimePlanJournal is null) return;
        var versions = new Dictionary<string, int>(_latestPlanVersionById, StringComparer.Ordinal);
        if (decision.Proposal.PlanId is { } id) versions[id] = decision.Proposal.Version;
        int? previous = _runtimePlanReplacement is null ? null : _runtimePlanReplacesAdoptionSequence
            ?? throw new RuntimePlanPersistenceException("runtime-plan-replacement-original-missing");
        var sequence = _runtimePlanHistory.Count + 1;
        await AppendRuntimePlanAsync(new(RuntimePlanHistoryRead.Schema, sequence, "adopt", versions,
            RuntimePlanGraph.Capture(decision.Proposal), decision.DecisionId, previous, _runtimePlanReplacement));
        // These positions belong to this running Owner and advance only after
        // the original append acknowledgement. They are never a resume cursor.
        _latestRuntimePlanAdoptionSequence = sequence;
        _runtimePlanReplacement = null;
        _runtimePlanReplacesAdoptionSequence = null;
    }

    private async ValueTask AppendRuntimePlanAsync(RuntimePlanHistoryRecord row)
    {
        if (_runtimePlanFailure is not null) throw new RuntimePlanPersistenceException(_runtimePlanFailure);
        try
        {
            var payload = JsonSerializer.Serialize(row, RuntimePlanGraph.Json);
            var frozen = JsonSerializer.Deserialize<RuntimePlanHistoryRecord>(payload, RuntimePlanGraph.Json)!;
            if (_inputs.RuntimePlanJournal is IAsyncRuntimePlanJournal asyncJournal)
                await asyncJournal.AppendAsync(frozen).ConfigureAwait(false);
            else _inputs.RuntimePlanJournal!.Append(frozen);
            _runtimePlanHistory.Add(payload);
        }
        catch (Exception ex) { FailRuntimePlanHistory(ex); }
    }

    private void FailRuntimePlanHistory(Exception error)
    {
        _runtimePlanFailure ??= "runtime-plan-persistence-failed:" + error.GetType().Name;
        throw new RuntimePlanPersistenceException(_runtimePlanFailure, error);
    }
}
