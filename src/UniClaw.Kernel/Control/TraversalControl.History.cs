using System.Text.Json;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Kernel.Control;

internal sealed partial class TraversalControl
{
    private sealed record FrameState(string ContainerId, int Depth, string[] Completed, string[] ObservedCandidates,
        string[] AssignedCandidates, bool DiscoveryGapRecorded, bool? DiscoveryComplete);
    private sealed record GapKey(string Container, string? Candidate, string Reason);
    private sealed record ProgressState(string Kind, TraversalGap? Gap = null, string? OperationId = null, string? Work = null);
    private sealed record Snapshot(FrameState[] Path, KeyValuePair<string, int>[] ExpandedNavigationDepth, string[] ActivePath,
        ProgressState[] ProgressEvents, GapKey[] ReportedGaps, KeyValuePair<string, bool>[] ScopeDiscovery,
        TraversalOperation? ReturnOperation, string? ExpectedReturnContainerId, string? CurrentContainerId,
        bool Returning, bool Finished, bool DiscoveryComplete, int InvocationOrdinal, string? Pause, string? End,
        PlanExecutionStatus Status, string PlanId, int Version, TraversalRule[]? Rules);

    private readonly IControlTraversalJournal? _journal;
    private readonly List<ControlTraversalHistoryRecord> _history = [];
    private string? _failure;
    private int _operationActive;
    internal TraversalControl(IControlTraversalJournal? journal = null)
    {
        if (journal is not null && (!journal.CanInitialize || journal.ReadRecords().Count > 0))
            throw new InvalidOperationException("existing-control-traversal-source-is-read-only");
        _journal = journal;
    }
    internal string? ExecutionBlockReason => _failure ?? (Volatile.Read(ref _operationActive) != 0 ? "control-traversal-operation-in-progress" : null) ?? (_journal is not null && _history.Count == 0 ? "control-traversal-uninitialized" : null);
    internal ControlTraversalHistoryPosition? Position => _journal is null || _history.Count == 0 ? null : new(_journal.RecordingId, _history.Count);
    internal ControlTraversalHistoryRead? History => _journal is null || _history.Count == 0 ? null
        : ControlTraversalHistoryRead.Read(_history, _journal.RecordingId, _journal.RecordingId);
    internal void EnsureWritable()
    {
        if (Volatile.Read(ref _operationActive) != 0) throw new InvalidOperationException("control-traversal-operation-in-progress");
        if (ExecutionBlockReason is { } reason) throw new ControlTraversalPersistenceException(reason);
    }

    internal void Initialize(RunIdentityRecord run, string? runRecording) =>
        InitializeAsync(run, runRecording).AsTask().GetAwaiter().GetResult();

    internal async ValueTask InitializeAsync(RunIdentityRecord run, string? runRecording)
    {
        if (Interlocked.CompareExchange(ref _operationActive, 1, 0) != 0)
            throw new InvalidOperationException("control-traversal-operation-in-progress");
        try { await InitializeCoreAsync(run, runRecording).ConfigureAwait(false); }
        finally { Volatile.Write(ref _operationActive, 0); }
    }

    private async ValueTask InitializeCoreAsync(RunIdentityRecord run, string? runRecording)
    {
        if (_journal is null) return;
        if (_failure is not null) throw new ControlTraversalPersistenceException(_failure);
        if (_history.Count > 0)
        {
            if (_history[0].RunRecordingId != runRecording || JsonSerializer.Serialize(_history[0].Run) != JsonSerializer.Serialize(run))
                throw new InvalidOperationException("control-traversal-cannot-rebind");
            return;
        }
        if (!Guid.TryParseExact(runRecording, "N", out _) || !Guid.TryParseExact(_journal.RecordingId, "N", out _))
        {
            _failure = "control-traversal-run-source-missing";
            throw new ControlTraversalPersistenceException(_failure);
        }
        var empty = JsonSerializer.SerializeToElement<object?>(null, ControlTraversalHistoryRead.Json);
        await AppendAsync(new(ControlTraversalHistoryRead.Schema, 1, "initialize", empty, Capture(), empty, run, runRecording)).ConfigureAwait(false);
    }

    public void ConfigureRules(IReadOnlyList<TraversalRule> rules) => ConfigureRulesAsync(rules).AsTask().GetAwaiter().GetResult();
    public ValueTask ConfigureRulesAsync(IReadOnlyList<TraversalRule> rules) => ChangeAsync("configure-rules", new { rules }, c => c.ConfigureRulesCore(rules));
    public void Start(string planId, int version, string root) => StartAsync(planId, version, root).AsTask().GetAwaiter().GetResult();
    public ValueTask StartAsync(string planId, int version, string root) => ChangeAsync("start", new { planId, version, root }, c => c.StartCore(planId, version, root));
    public Work? Select(TraversalObservation observation, int maxDepth) => SelectAsync(observation, maxDepth).AsTask().GetAwaiter().GetResult();
    public ValueTask<Work?> SelectAsync(TraversalObservation observation, int maxDepth) => ChangeAsync("select", new { observation, maxDepth }, c => c.SelectCore(observation, maxDepth));
    public void Navigated(Work work, string targetContainerId, TraversalOperation returnOperation) => NavigatedAsync(work, targetContainerId, returnOperation).AsTask().GetAwaiter().GetResult();
    public ValueTask NavigatedAsync(Work work, string targetContainerId, TraversalOperation returnOperation) =>
        ChangeAsync("navigated", new { work, targetContainerId, returnOperation }, c => c.NavigatedCore(work, targetContainerId, returnOperation));
    public bool Returned(Work work, string? currentContainerId) => ReturnedAsync(work, currentContainerId).AsTask().GetAwaiter().GetResult();
    public ValueTask<bool> ReturnedAsync(Work work, string? currentContainerId) => ChangeAsync("returned", new { work, currentContainerId }, c => c.ReturnedCore(work, currentContainerId));
    public void ReadOnlyCompleted(Work work) => ReadOnlyCompletedAsync(work).AsTask().GetAwaiter().GetResult();
    public ValueTask ReadOnlyCompletedAsync(Work work) => ChangeAsync("read-only-completed", new { work }, c => c.ReadOnlyCompletedCore(work));
    public void Pause(string reason) => PauseAsync(reason).AsTask().GetAwaiter().GetResult();
    public ValueTask PauseAsync(string reason) => ChangeAsync("pause", new { reason }, c => c.PauseCore(reason));
    public void Resume() => ResumeAsync().AsTask().GetAwaiter().GetResult();
    public ValueTask ResumeAsync() => ChangeAsync("resume", new { }, c => c.ResumeCore());
    public void Abandon(string reason) => AbandonAsync(reason).AsTask().GetAwaiter().GetResult();
    public ValueTask AbandonAsync(string reason) => ChangeAsync("abandon", new { reason }, c => c.AbandonCore(reason));
    public void Fail(string reason) => FailAsync(reason).AsTask().GetAwaiter().GetResult();
    public ValueTask FailAsync(string reason) => ChangeAsync("fail", new { reason }, c => c.FailCore(reason));
    public void Gap(string container, string? candidate, string reason, IReadOnlyList<string> evidence) => GapAsync(container, candidate, reason, evidence).AsTask().GetAwaiter().GetResult();
    public ValueTask GapAsync(string container, string? candidate, string reason, IReadOnlyList<string> evidence) =>
        ChangeAsync("gap", new { container, candidate, reason, evidence }, c => c.GapCore(container, candidate, reason, evidence));
    public IReadOnlyList<TraversalProgressEvent> DrainProgressEvents() => DrainProgressEventsAsync().AsTask().GetAwaiter().GetResult();
    public ValueTask<IReadOnlyList<TraversalProgressEvent>> DrainProgressEventsAsync() => ChangeAsync("drain", new { }, c => c.DrainProgressEventsCore());

    private async ValueTask ChangeAsync(string operation, object input, Action<TraversalControl> apply) =>
        _ = await ChangeAsync(operation, input, c => { apply(c); return true; }).ConfigureAwait(false);

    // No external call or work handoff exists between these two original operations.
    // Legacy journals retain their per-row publication and failure behavior.
    internal async ValueTask ConfigureAndStartAsync(IReadOnlyList<TraversalRule> rules, string planId, int version, string root)
    {
        if (_journal is not IAsyncControlTraversalJournal { CanAppendBatch: true })
        {
            await ConfigureRulesAsync(rules).ConfigureAwait(false);
            await StartAsync(planId, version, root).ConfigureAwait(false);
            return;
        }
        await ChangeCandidateAsync((candidate, rows) =>
        {
            var configured = candidate.Prepare("configure-rules", new { rules }, c => { c.ConfigureRulesCore(rules); return true; }, _history.Count + 1);
            rows.Add(configured.Row);
            var started = candidate.Prepare("start", new { planId, version, root }, c => { c.StartCore(planId, version, root); return true; }, _history.Count + 2);
            rows.Add(started.Row);
            return true;
        }).ConfigureAwait(false);
    }

    private ValueTask<T> ChangeAsync<T>(string operation, object input, Func<TraversalControl, T> apply)
    {
        if (_journal is null) return ValueTask.FromResult(apply(this));
        return ChangeCandidateAsync((candidate, rows) =>
        {
            var prepared = candidate.Prepare(operation, input, apply, _history.Count + 1);
            rows.Add(prepared.Row);
            return prepared.Result;
        });
    }

    private (ControlTraversalHistoryRecord Row, T Result) Prepare<T>(string operation, object input, Func<TraversalControl, T> apply, int sequence)
    {
        var inputJson = JsonSerializer.SerializeToElement(input, ControlTraversalHistoryRead.Json);
        var result = apply(this); // Candidate-local nested Gap/Pause remain in the outer record.
        var resultJson = result is IReadOnlyList<TraversalProgressEvent> events
            ? JsonSerializer.SerializeToElement(events.Select(ToProgress).ToArray(), ControlTraversalHistoryRead.Json)
            : JsonSerializer.SerializeToElement(result, ControlTraversalHistoryRead.Json);
        var row = new ControlTraversalHistoryRecord(ControlTraversalHistoryRead.Schema, sequence, operation, inputJson, Capture(), resultJson);
        ValidateRecordedOperation(row);
        return (row, result is Work ? (T)(object)resultJson.Deserialize<Work>(ControlTraversalHistoryRead.Json)! : result);
    }

    private async ValueTask<T> ChangeCandidateAsync<T>(Func<TraversalControl, List<ControlTraversalHistoryRecord>, T> prepare)
    {
        EnsureWritable();
        if (Interlocked.CompareExchange(ref _operationActive, 1, 0) != 0)
            throw new InvalidOperationException("control-traversal-operation-in-progress");
        try
        {
            var candidate = new TraversalControl(); candidate.Load(Capture());
            var rows = new List<ControlTraversalHistoryRecord>();
            var result = prepare(candidate, rows);
            await AppendRecordsAsync(rows).ConfigureAwait(false);
            Load(rows[^1].State);
            return result;
        }
        finally { Volatile.Write(ref _operationActive, 0); }
    }

    private ValueTask AppendAsync(ControlTraversalHistoryRecord row) => AppendRecordsAsync([row]);
    private async ValueTask AppendRecordsAsync(IReadOnlyList<ControlTraversalHistoryRecord> rows)
    {
        try
        {
            if (_journal is IAsyncControlTraversalJournal asyncJournal)
            {
                if (rows.Count > 1) await asyncJournal.AppendBatchAsync(rows).ConfigureAwait(false);
                else await asyncJournal.AppendAsync(rows[0]).ConfigureAwait(false);
            }
            else foreach (var row in rows) _journal!.Append(row);
        }
        catch (Exception exception)
        {
            _failure = "control-traversal-persistence-failed:" + exception.GetType().Name;
            throw new ControlTraversalPersistenceException(_failure, exception);
        }
        _history.AddRange(rows);
    }

    private JsonElement Capture() => JsonSerializer.SerializeToElement(new Snapshot(
        _path.Select(f => new FrameState(f.ContainerId, f.Depth, f.Completed.ToArray(), f.ObservedCandidates.ToArray(),
            f.AssignedCandidates.ToArray(), f.DiscoveryGapRecorded, f.DiscoveryComplete)).ToArray(),
        _expandedNavigationDepth.ToArray(), _activePath.ToArray(), _progressEvents.Select(ToProgress).ToArray(),
        _reportedGaps.Select(g => new GapKey(g.Container, g.Candidate, g.Reason)).ToArray(), _scopeDiscovery.ToArray(),
        _returnOperation, _expectedReturnContainerId, _currentContainerId, _returning, _finished, _discoveryComplete,
        _invocationOrdinal, _pause, _end, _status, _planId, _version, _rules?.ToArray()), ControlTraversalHistoryRead.Json);

    private static ProgressState ToProgress(TraversalProgressEvent value) => value switch
    {
        TraversalProgressEvent.Gap gap => new("gap", gap.Value),
        TraversalProgressEvent.Completed completed => new("completed", OperationId: completed.OperationId, Work: completed.Work),
        _ => throw new InvalidDataException("control-traversal-progress-shape"),
    };

    private void Load(JsonElement state)
    {
        var value = ReadSnapshot(state);
        _path.Clear();
        foreach (var frame in value.Path.Reverse())
            _path.Push(new(frame.ContainerId, frame.Depth) { Completed = frame.Completed.ToHashSet(StringComparer.Ordinal),
                ObservedCandidates = frame.ObservedCandidates.ToHashSet(StringComparer.Ordinal), AssignedCandidates = frame.AssignedCandidates.ToHashSet(StringComparer.Ordinal),
                DiscoveryGapRecorded = frame.DiscoveryGapRecorded, DiscoveryComplete = frame.DiscoveryComplete });
        _expandedNavigationDepth.Clear(); foreach (var pair in value.ExpandedNavigationDepth) _expandedNavigationDepth.Add(pair.Key, pair.Value);
        _activePath.Clear(); foreach (var container in value.ActivePath) _activePath.Add(container);
        _progressEvents.Clear();
        foreach (var progress in value.ProgressEvents)
            _progressEvents.Enqueue(progress.Kind == "gap" ? new TraversalProgressEvent.Gap(progress.Gap!)
                : new TraversalProgressEvent.Completed(progress.OperationId!, progress.Work!));
        _reportedGaps.Clear(); foreach (var gap in value.ReportedGaps) _reportedGaps.Add((gap.Container, gap.Candidate, gap.Reason));
        _scopeDiscovery.Clear(); foreach (var pair in value.ScopeDiscovery) _scopeDiscovery.Add(pair.Key, pair.Value);
        _returnOperation = value.ReturnOperation; _expectedReturnContainerId = value.ExpectedReturnContainerId;
        _currentContainerId = value.CurrentContainerId; _returning = value.Returning; _finished = value.Finished;
        _discoveryComplete = value.DiscoveryComplete; _invocationOrdinal = value.InvocationOrdinal; _pause = value.Pause; _end = value.End;
        _status = value.Status; _planId = value.PlanId; _version = value.Version; _rules = value.Rules;
    }

    private static Snapshot ReadSnapshot(JsonElement state)
    {
        var value = state.Deserialize<Snapshot>(ControlTraversalHistoryRead.Json) ?? throw new InvalidDataException("control-traversal-state-missing");
        var expected = JsonSerializer.SerializeToElement(value, ControlTraversalHistoryRead.Json);
        if (state.ValueKind != JsonValueKind.Object || state.EnumerateObject().Count() != expected.EnumerateObject().Count()
            || !state.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal).SetEquals(expected.EnumerateObject().Select(p => p.Name))
            || value.Path is null || value.ActivePath is null || value.ProgressEvents is null || value.ReportedGaps is null
            || value.ScopeDiscovery is null || value.ExpandedNavigationDepth is null || value.PlanId is null
            || value.Version < 0 || value.InvocationOrdinal < 0 || !Enum.IsDefined(value.Status))
            throw new InvalidDataException("control-traversal-state-shape");
        if (value.Path.Select(p => p.ContainerId).Distinct(StringComparer.Ordinal).Count() != value.Path.Length
            || !value.ActivePath.ToHashSet(StringComparer.Ordinal).SetEquals(value.Path.Select(p => p.ContainerId))
            || value.ActivePath.Distinct(StringComparer.Ordinal).Count() != value.ActivePath.Length
            || value.Path.Where((p, i) => p.Depth != value.Path.Length - 1 - i || p.Completed is null
                || p.ObservedCandidates is null || p.AssignedCandidates is null
                || !p.AssignedCandidates.All(p.ObservedCandidates.Contains)).Any()
            || value.Returning && (value.Path.Length < 2 || value.ReturnOperation is null || value.ExpectedReturnContainerId != value.Path[1].ContainerId)
            || value.ScopeDiscovery.Select(p => p.Key).Distinct(StringComparer.Ordinal).Count() != value.ScopeDiscovery.Length
            || value.ExpandedNavigationDepth.Select(p => p.Key).Distinct(StringComparer.Ordinal).Count() != value.ExpandedNavigationDepth.Length)
            throw new InvalidDataException("control-traversal-path-dependency");
        foreach (var progress in value.ProgressEvents)
            if (!(progress.Kind == "gap" && progress.Gap is not null && progress.OperationId is null && progress.Work is null
                || progress.Kind == "completed" && progress.Gap is null && progress.OperationId is not null && progress.Work is not null))
                throw new InvalidDataException("control-traversal-progress-shape");
        return value;
    }

    internal static void ValidateRecordedOperation(ControlTraversalHistoryRecord row)
    {
        var state = ReadSnapshot(row.State);
        string[] required = row.Operation switch
        {
            "initialize" => [], "configure-rules" => ["rules"], "start" => ["planId", "version", "root"],
            "select" => ["observation", "maxDepth"], "navigated" => ["work", "targetContainerId", "returnOperation"],
            "returned" => ["work", "currentContainerId"], "read-only-completed" => ["work"],
            "pause" or "abandon" or "fail" => ["reason"], "gap" => ["container", "candidate", "reason", "evidence"],
            "resume" or "drain" => [], _ => throw new InvalidDataException("control-traversal-operation-unknown"),
        };
        if (row.Operation == "initialize")
        {
            if (row.Input.ValueKind != JsonValueKind.Null || row.Result.ValueKind != JsonValueKind.Null)
                throw new InvalidDataException("control-traversal-initialize-shape");
            if (state.Path.Length != 0 || state.ActivePath.Length != 0 || state.ExpandedNavigationDepth.Length != 0
                || state.ProgressEvents.Length != 0 || state.ReportedGaps.Length != 0 || state.ScopeDiscovery.Length != 0
                || state.ReturnOperation is not null || state.ExpectedReturnContainerId is not null || state.CurrentContainerId is not null
                || state.Returning || state.Finished || state.DiscoveryComplete || state.InvocationOrdinal != 0 || state.Pause is not null
                || state.End is not null || state.Status != PlanExecutionStatus.Active || state.PlanId != "" || state.Version != 0 || state.Rules is not null)
                throw new InvalidDataException("control-traversal-initial-state-not-empty");
        }
        else if (row.Input.ValueKind != JsonValueKind.Object || row.Input.EnumerateObject().Count() != required.Length
            || !row.Input.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal).SetEquals(required)
            || row.Result.ValueKind is JsonValueKind.Undefined)
            throw new InvalidDataException("control-traversal-input-shape");
        var resultShape = row.Operation switch
        {
            "initialize" => row.Result.ValueKind == JsonValueKind.Null,
            "select" => row.Result.ValueKind is JsonValueKind.Null or JsonValueKind.Object,
            "returned" => row.Result.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "drain" => row.Result.ValueKind == JsonValueKind.Array,
            _ => row.Result.ValueKind == JsonValueKind.True,
        };
        if (!resultShape) throw new InvalidDataException("control-traversal-result-shape");
    }
}
