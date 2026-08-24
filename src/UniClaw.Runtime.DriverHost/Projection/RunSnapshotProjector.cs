using System.Collections.Immutable;
using UniClaw.Runtime.Harness;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Deterministic RunSnapshot projection (design.md §4 / §6).
/// Every field carries its audited classification; nothing is invented —
/// fields whose truthful source is not on the Agent public surface stay
/// NotCurrentlyAvailable with a source explanation.
/// </summary>
public static class RunSnapshotProjector
{
    private const string GoalSpanName = "RunSemanticGoal";
    private const string GoalTagKey = "goal";

    /// <summary>Projects truthful snapshot fields from trace and agent state.</summary>
    public static RunSnapshot Project(string runId, TraceRun trace, AgentStateSnapshot agent)
    {
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(agent);

        var diagnostics = new List<string>();

        // DERIVED: current goal from the RunSemanticGoal span tag (structural span evidence).
        var goalSpan = trace.Spans
            .FirstOrDefault(s => s.Name == GoalSpanName
                                 && s.Attributes.Any(a => a.Key == GoalTagKey && !string.IsNullOrEmpty(a.Value)));
        GoalSummary? goal = goalSpan?.Attributes.FirstOrDefault(a => a.Key == GoalTagKey)?.Value is { } goalTag
            ? new GoalSummary(goalTag)
            : null;
        if (goal is null && agent.State != RunState.Idle && agent.State != RunState.Initializing)
        {
            diagnostics.Add(
                "CurrentGoal derived from the RunSemanticGoal span tag; no such span tag was recorded — " +
                "CurrentGoal stays null (DERIVED_READ_MODEL, never invented).");
        }

        return new RunSnapshot
        {
            RunId = runId,
            RunState = SnapshotField<RunState>.Direct(agent.State, "Agent.State (public read model)"),
            CurrentSemanticPage = SnapshotField<string?>.Direct(
                agent.Belief?.SemanticPage, "Agent.Belief.SemanticPage (public read model)"),
            ActiveTrap = SnapshotField<Trap?>.Direct(agent.LastTrap, "Agent.LastTrap (public read model)"),
            CurrentGoal = SnapshotField<GoalSummary?>.Derived(
                goal, "span:agent.execution:RunSemanticGoal tag[goal] (structural span evidence)"),
            LastDecision = SnapshotField<DecisionSummary?>.Derived(
                LatestDecision(agent.Trace), "latest TraceEvent with Reason or ActionId (public trace)"),
            LastAction = SnapshotField<ActionSummary?>.Derived(
                LatestAction(agent.Trace), "latest TraceEvent with ActionId+Action (public trace)"),
            RecoveryState = SnapshotField<RecoverySummary?>.Derived(
                LatestRecovery(agent.Trace), "latest TraceEvent with RecoveryId (public trace)"),
            LatestGoalEvidence = LatestGoalEvidenceField(agent),
            CurrentObservationSequence = SnapshotField<long?>.Unavailable(
                "active Container observation is private — not on Agent public surface"),
            CurrentContainerSummary = SnapshotField<string?>.Unavailable(
                "active Container is private — not on Agent public surface"),
            BindingsSummary = SnapshotField<string?>.Unavailable(
                "Container.ObjectBindings is private — not on Agent public surface"),
            StateBeliefsSummary = SnapshotField<string?>.Unavailable(
                "Container.ObjectStateBeliefs is private — not on Agent public surface"),
            Diagnostics = [.. diagnostics],
        };
    }

    /// <summary>Latest decision-shaped trace event (Reason or ActionId), in trace order.</summary>
    private static DecisionSummary? LatestDecision(ImmutableArray<TraceEvent> trace)
    {
        foreach (var traceEvent in trace.AsEnumerable().Reverse())
        {
            if (traceEvent.Reason is not null || traceEvent.ActionId is not null)
            {
                return new DecisionSummary(traceEvent.Reason, traceEvent.ActionId, traceEvent.StepId, traceEvent.ContainerId);
            }
        }

        return null;
    }

    /// <summary>Latest dispatched action trace event, in trace order.</summary>
    private static ActionSummary? LatestAction(ImmutableArray<TraceEvent> trace)
    {
        foreach (var traceEvent in trace.AsEnumerable().Reverse())
        {
            if (traceEvent.ActionId is not null && traceEvent.Action is not null)
            {
                return new ActionSummary(
                    traceEvent.ActionId,
                    traceEvent.StepId,
                    traceEvent.ContainerId,
                    DeviceActionText.Describe(traceEvent.Action));
            }
        }

        return null;
    }

    /// <summary>Latest recovery trace event, in trace order.</summary>
    private static RecoverySummary? LatestRecovery(ImmutableArray<TraceEvent> trace)
    {
        foreach (var traceEvent in trace.AsEnumerable().Reverse())
        {
            if (traceEvent.RecoveryId is not null)
            {
                return new RecoverySummary(traceEvent.RecoveryId, traceEvent.Reason, traceEvent.ContainerId, traceEvent.StepId);
            }
        }

        return null;
    }

    /// <summary>
    /// Partial goal evidence: State=Completed + Reason only. Full GoalEvidence
    /// (SourceObservationSequence) is NOT on the Agent public surface — the field
    /// is classified NotCurrentlyAvailable with IsPartial=true.
    /// </summary>
    private static SnapshotField<GoalEvidenceSummary?> LatestGoalEvidenceField(AgentStateSnapshot agent)
    {
        foreach (var traceEvent in agent.Trace.AsEnumerable().Reverse())
        {
            if (traceEvent.RunState == RunState.Completed)
            {
                return SnapshotField<GoalEvidenceSummary?>.UnavailablePartial(
                    new GoalEvidenceSummary(
                        Satisfied: true,
                        traceEvent.Reason,
                        SourceObservationSequence: null,
                        IsPartial: true),
                    "Agent trace State=Completed + Reason only (full GoalEvidence not on public surface)");
            }
        }

        return SnapshotField<GoalEvidenceSummary?>.Unavailable(
            "no completion evidence on Agent public surface");
    }
}
