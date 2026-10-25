using UniClaw.Kernel.Runtime;

namespace UniClaw.Kernel.Run;

public enum RunTraversalTransition { Initialized, Reset, Handler, Operation, Gap, Completed, View }

/// <summary>One original Run recording operation, not a Control cursor or execution permit.</summary>
public sealed record RunTraversalEvent(string Schema, RunTraversalTransition Transition,
    PlanHandlerRecord? Handler = null, TraversalOperationRecord? Operation = null, TraversalGap? Gap = null,
    string? OperationId = null, string? Work = null, TraversalExecutionView? Control = null);

public sealed partial class RunModel
{
    private const string TraversalSchema = "uniclaw.run.traversal.v1";
    /// <summary>False for old histories without an original execution-fact source.</summary>
    public bool TraversalHistoryAvailable { get; private set; }
    public IReadOnlyList<PlanHandlerRecord> TraversalHandlers => _traversalHandlers.AsReadOnly();
    public IReadOnlyList<TraversalOperationRecord> TraversalOperations => _traversalOperations.AsReadOnly();
    public IReadOnlyList<TraversalGap> TraversalGaps => _traversalGaps.AsReadOnly();
    public IReadOnlyList<string> TraversalCompletedWork => _traversalCompletedWork.AsReadOnly();

    private async ValueTask RecordTraversalEventAsync(RunTraversalEvent value)
    {
        await RecordOperationAsync(async () =>
        {
            await RecordTraversalEventCoreAsync(value).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
    }

    private async ValueTask RecordTraversalEventCoreAsync(RunTraversalEvent value)
    {
        EnsureStateAvailable();
        if (IsTerminal) return;
        if (State is null) throw new InvalidOperationException("run-not-admitted");
        value = FreezeTraversalEvent(value);
        if (!ValidateTraversalEvent(value)) return;
        await AppendHistoryAsync(new(_committedHistorySequence + 1, RunHistoryTransition.Traversal,
            State.Progress.Cycles, State.Progress.Acts, Traversal: value)).ConfigureAwait(false);
        ApplyTraversalEvent(value);
    }

    // Freeze every caller-owned collection before append; publication only follows acknowledgement.
    private static RunTraversalEvent FreezeTraversalEvent(RunTraversalEvent value) => value with
    {
        Operation = value.Operation is { } operation ? FreezeOperation(operation) : null,
        Gap = value.Gap is { } gap ? FreezeGap(gap) : null,
        Control = value.Control is { } control ? control with
        {
            Handlers = Array.AsReadOnly(control.Handlers.ToArray()),
            Gaps = Array.AsReadOnly(control.Gaps.Select(FreezeGap).ToArray()),
            CompletedWork = Array.AsReadOnly(control.CompletedWork.ToArray()),
            Operations = control.Operations is { } operations ? Array.AsReadOnly(operations.Select(FreezeOperation).ToArray()) : null,
        } : null,
    };
    private static TraversalOperationRecord FreezeOperation(TraversalOperationRecord value) =>
        value with { EvidenceRefs = Array.AsReadOnly(value.EvidenceRefs.ToArray()) };
    private static TraversalGap FreezeGap(TraversalGap value) =>
        value with { EvidenceRefs = Array.AsReadOnly(value.EvidenceRefs.ToArray()) };

    // False denotes the same no-op as the original Run port. A recorded no-op is an illegal suffix.
    private bool ValidateTraversalEvent(RunTraversalEvent value)
    {
        if (value.Schema != TraversalSchema || !Enum.IsDefined(value.Transition))
            throw new InvalidDataException("run-traversal-schema");
        var fields = (value.Handler is null ? 0 : 1) + (value.Operation is null ? 0 : 1)
            + (value.Gap is null ? 0 : 1) + (value.Control is null ? 0 : 1)
            + (value.OperationId is null ? 0 : 1) + (value.Work is null ? 0 : 1);
        var shape = value.Transition switch
        {
            RunTraversalTransition.Initialized => !TraversalHistoryAvailable && fields == 0,
            RunTraversalTransition.Reset => fields == 0,
            RunTraversalTransition.Handler => value.Handler is not null && fields == 1,
            RunTraversalTransition.Operation => value.Operation is not null && fields == 1,
            RunTraversalTransition.Gap => value.Gap is not null && fields == 1,
            RunTraversalTransition.Completed => value.OperationId is not null && value.Work is not null && fields == 2,
            RunTraversalTransition.View => value.Control is not null && fields == 1,
            _ => false,
        };
        if (!shape || value.Transition != RunTraversalTransition.Initialized && !TraversalHistoryAvailable)
            throw new InvalidDataException("run-traversal-shape-or-source");
        if (value.Handler is { } handler)
        {
            if (!Enum.IsDefined(handler.Status)) throw new InvalidDataException("run-traversal-handler-status");
            if (!_traversalOperations.Any(operation => operation.OperationId == handler.OperationId))
                throw new InvalidDataException("run-traversal-handler-operation-missing");
            // Preserve the original Finding, including an association rejected by Runtime.
            // Recording its mechanical terminal status must never judge that result again.
            if (handler.CallId is { } id && _traversalHandlers.FirstOrDefault(h => h.CallId == id) is { } prior)
            {
                if (prior.Status != PlanHandlerCallStatus.Running || handler.Status == PlanHandlerCallStatus.Running) return false;
                if (prior.OperationId != handler.OperationId || prior.HandlerId != handler.HandlerId
                    || prior.Phase != handler.Phase || prior.SourceRevisionId != handler.SourceRevisionId)
                    throw new InvalidDataException("run-traversal-handler-identity");
            }
            else if (handler.CallId is not null && handler.Status != PlanHandlerCallStatus.Running)
                throw new InvalidDataException("run-traversal-handler-running-missing");
        }
        if (value.Operation is { } operation && _traversalOperations.Any(o => o.OperationId == operation.OperationId))
            throw new InvalidOperationException("duplicate-traversal-operation");
        if (value.Transition == RunTraversalTransition.Completed)
        {
            var completedOperation = _traversalOperations.FirstOrDefault(o => o.OperationId == value.OperationId)
                ?? throw new InvalidOperationException("unknown-traversal-operation");
            if (completedOperation.Completed) return false;
        }
        return true;
    }

    private void ApplyTraversalEvent(RunTraversalEvent original)
    {
        var value = FreezeTraversalEvent(original);
        try
        {
            if (!ValidateTraversalEvent(value)) throw new InvalidDataException("run-traversal-recorded-noop");
        }
        catch (InvalidOperationException exception) { throw new InvalidDataException("run-traversal-dependency", exception); }
        switch (value.Transition)
        {
            case RunTraversalTransition.Initialized:
                TraversalHistoryAvailable = true;
                break;
            case RunTraversalTransition.Reset:
                _traversalHandlers.Clear(); _traversalGaps.Clear(); _traversalOperations.Clear(); _traversalCompletedWork.Clear();
                break;
            case RunTraversalTransition.Handler:
                var index = value.Handler!.CallId is { } id ? _traversalHandlers.FindIndex(h => h.CallId == id) : -1;
                if (index < 0) _traversalHandlers.Add(value.Handler); else _traversalHandlers[index] = value.Handler;
                break;
            case RunTraversalTransition.Operation:
                _traversalOperations.Add(value.Operation!);
                break;
            case RunTraversalTransition.Gap:
                _traversalGaps.Add(value.Gap!);
                break;
            case RunTraversalTransition.Completed:
                var at = _traversalOperations.FindIndex(o => o.OperationId == value.OperationId);
                _traversalOperations[at] = _traversalOperations[at] with { Completed = true };
                _traversalCompletedWork.Add(value.Work!);
                break;
            case RunTraversalTransition.View:
                var control = value.Control!;
                TraversalExecution = control with
                {
                    OperationsCompleted = _traversalOperations.Count(o => o.Completed),
                    Handlers = Array.AsReadOnly(_traversalHandlers.ToArray()),
                    Gaps = Array.AsReadOnly(_traversalGaps.ToArray()),
                    CompletedWork = Array.AsReadOnly(_traversalCompletedWork.ToArray()),
                    Operations = Array.AsReadOnly(_traversalOperations.ToArray()),
                    DiscoveryComplete = control.DiscoveryComplete && !_traversalGaps.Any(g => g.Reason == "discovery-incomplete"),
                    EndReason = control.Status == PlanExecutionStatus.Completed && _traversalGaps.Count > 0 ? "partial" : control.EndReason,
                };
                _traversalHistory.Add(TraversalExecution);
                break;
        }
    }
}
