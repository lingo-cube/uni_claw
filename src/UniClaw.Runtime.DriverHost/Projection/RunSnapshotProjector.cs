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
    public static RunSnapshot Project(string runId, TraceRun trace, AgentStateSnapshot agent, EvidenceCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(agent);

        var diagnostics = new List<string>();
        var context = agent.ContainerContext;
        var latestTransition = context.LatestTransition;
        string? evidenceRef = context.EvidenceRef;
        string? catalogEvidenceRef = null;
        string? assetRef = context.AssetRef;
        var assetRefs = ImmutableArray<string>.Empty;
        var contextUnavailable = context.ActiveAncestorPath.IsDefault;
        var v2Available = context.IsV2StateAvailable;
        if (latestTransition is not null
            && latestTransition.EvidenceRef is not null
            && !string.Equals(latestTransition.EvidenceRef, latestTransition.FreshObservationRef, StringComparison.Ordinal))
            diagnostics.Add($"MISSING_EVIDENCE: historical EvidenceRef '{latestTransition.EvidenceRef}' does not match FreshObservationRef '{latestTransition.FreshObservationRef}'; historical value preserved.");
        if (latestTransition is not null && catalog is not null)
        {
            var linked = catalog.ResolveTransition(latestTransition);
            catalogEvidenceRef = linked.EvidenceRef?.Locator;
            assetRefs = [.. linked.AssetRefs.Select(r => r.Locator)];
            diagnostics.AddRange(linked.Diagnostics);
        }
        else if (latestTransition is not null)
        {
            diagnostics.Add("MISSING_EVIDENCE: no EvidenceCatalog registered for transition observation.");
            diagnostics.Add("MISSING_ASSET: no EvidenceCatalog registered for transition asset.");
        }
        diagnostics.AddRange(context.Diagnostics);
        if (latestTransition is not null && string.IsNullOrWhiteSpace(assetRef) && assetRefs.IsDefaultOrEmpty)
            diagnostics.Add("MISSING_ASSET: no historical or catalog asset reference.");
        diagnostics = diagnostics.Distinct(StringComparer.Ordinal).ToList();

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
            CurrentObservedLocation = contextUnavailable
                ? SnapshotField<string?>.Unavailable("Agent.ContainerContext.CurrentObservedLocation unavailable")
                : SnapshotField<string?>.Direct(context.CurrentObservedLocation, "Agent.ContainerContext.CurrentObservedLocation (WorldBelief projection)"),
            ActiveExecutionContainer = contextUnavailable
                ? SnapshotField<string?>.Unavailable("Agent.ContainerContext.ActiveExecutionContainer unavailable")
                : SnapshotField<string?>.Direct(context.ActiveExecutionContainer, "Agent.ContainerContext.ActiveExecutionContainer (immutable context copy)"),
            ActiveAncestorPath = contextUnavailable || context.ActiveAncestorPath.IsDefault
                ? SnapshotField<ImmutableArray<string>>.Unavailable("Agent.ContainerContext.ActiveAncestorPath unavailable")
                : SnapshotField<ImmutableArray<string>>.Direct(context.ActiveAncestorPath, "Agent.ContainerContext.ActiveAncestorPath (immutable context copy)"),
            CurrentContainerNodeRef = !v2Available
                ? SnapshotField<ContainerNodeRef?>.Unavailable("no V2 aggregate state on the Agent public read model")
                : SnapshotField<ContainerNodeRef?>.Direct(context.CurrentNodeRef, "Agent.ContainerContext.CurrentNodeRef (immutable V2 CurrentContainer projection)"),
            CurrentSliceRef = !v2Available
                ? SnapshotField<ContainerSliceRef?>.Unavailable("no V2 aggregate state on the Agent public read model")
                : SnapshotField<ContainerSliceRef?>.Direct(context.CurrentSliceRef, "Agent.ContainerContext.CurrentSliceRef (immutable V2 CurrentContainer projection)"),
            EntrySourceNodeRef = !v2Available
                ? SnapshotField<ContainerNodeRef?>.Unavailable("no V2 aggregate state on the Agent public read model")
                : SnapshotField<ContainerNodeRef?>.Direct(context.EntrySourceNodeRef, "Agent.ContainerContext.EntrySourceNodeRef (path-relative entry source; null = root entry known-null direct value)"),
            EntryTransitionOccurrenceRef = !v2Available
                ? SnapshotField<TransitionOccurrenceRef?>.Unavailable("no V2 aggregate state on the Agent public read model")
                : SnapshotField<TransitionOccurrenceRef?>.Direct(context.EntryTransitionOccurrenceRef, "Agent.ContainerContext.EntryTransitionOccurrenceRef (path-relative entry occurrence; null = root entry known-null direct value)"),
            EntryRelationRef = !v2Available
                ? SnapshotField<ContainerRelationRef?>.Unavailable("no V2 aggregate state on the Agent public read model")
                : SnapshotField<ContainerRelationRef?>.Direct(context.EntryRelationRef, "Agent.ContainerContext.EntryRelationRef (optional path-relative entry relation; null = none)"),
            LatestTransitionOccurrence = !v2Available
                ? SnapshotField<ContainerTransitionOccurrence?>.Unavailable("no V2 aggregate state on the Agent public read model")
                : SnapshotField<ContainerTransitionOccurrence?>.Direct(context.LatestTransitionOccurrence, "Agent.ContainerContext.LatestTransitionOccurrence (latest immutable V2 occurrence)"),
            EvidenceRevision = !v2Available
                ? SnapshotField<SemanticEvidenceRevision?>.Unavailable("no V2 aggregate state on the Agent public read model")
                : SnapshotField<SemanticEvidenceRevision?>.Direct(context.EvidenceRevision, "Agent.ContainerContext.EvidenceRevision (accepted V2 evidence revision)"),
            FastAssessmentAvailability = SnapshotField<ContainerFastAssessmentAvailability?>.UnavailablePartial(
                context.FastAssessmentAvailability,
                v2Available
                    ? "production deliberately retains no mutable latest Fast assessment slot (D18) — Agent.ContainerContext.FastAssessmentAvailability=NotRetained; never inferred from Graph/Belief/legacy"
                    : "no V2 aggregate state on the Agent public read model — Agent.ContainerContext.FastAssessmentAvailability=Unavailable"),
            LatestObservedTransition = latestTransition is null
                ? SnapshotField<ContainerTransition?>.Unavailable("no structured transition event in immutable DecisionRecord history")
                : SnapshotField<ContainerTransition?>.Derived(
                    latestTransition, "Agent.ContainerContext.LatestTransition (immutable DecisionRecord history)"),
            ContainerCompletenessRef = latestTransition is null
                ? SnapshotField<string?>.Unavailable("no structured transition event")
                : context.CompletenessRef is null
                    ? SnapshotField<string?>.Unavailable("ContainerTransition.CompletenessRef unavailable")
                    : SnapshotField<string?>.Derived(context.CompletenessRef, "ContainerTransition.CompletenessRef (existing evidence reference)"),
            ContainerEvidenceRef = latestTransition is null
                ? SnapshotField<string?>.Unavailable("no structured transition event")
                : evidenceRef is null
                    ? SnapshotField<string?>.Unavailable("ContainerTransition.EvidenceRef unavailable")
                    : SnapshotField<string?>.Derived(evidenceRef, "ContainerTransition.EvidenceRef (committed historical fact)"),
            ContainerCatalogEvidenceRef = latestTransition is null || catalogEvidenceRef is null
                ? SnapshotField<string?>.Unavailable(latestTransition is null ? "no structured transition event" : "catalog observation record unavailable")
                : SnapshotField<string?>.Derived(catalogEvidenceRef, "ContainerTransition.FreshObservationRef -> EvidenceCatalog (logical reference)"),
            ContainerAssetRef = latestTransition is null || (assetRef is null && assetRefs.IsDefaultOrEmpty)
                ? SnapshotField<string?>.Unavailable(latestTransition is null ? "no structured transition event" : "historical asset reference unavailable")
                : SnapshotField<string?>.Derived(assetRef ?? assetRefs[0], assetRef is null ? "CaptureArtifact.FrameId -> EvidenceCatalog (logical reference only)" : "ContainerTransition.AssetRef (committed historical fact)"),
            ContainerAssetRefs = latestTransition is null
                ? SnapshotField<ImmutableArray<string>>.Unavailable("no structured transition event")
                : assetRefs.IsDefaultOrEmpty
                    ? SnapshotField<ImmutableArray<string>>.Unavailable("catalog asset references unavailable")
                    : SnapshotField<ImmutableArray<string>>.Derived(assetRefs, "CaptureArtifact.FrameId -> EvidenceCatalog (logical references only)"),
            ActiveTrap = SnapshotField<Trap?>.Direct(agent.LastTrap, "Agent.LastTrap (public read model)"),
            CurrentGoal = SnapshotField<GoalSummary?>.Derived(
                goal, "span:agent.execution:RunSemanticGoal tag[goal] (structural span evidence)"),
            LastDecision = SnapshotField<DecisionSummary?>.Derived(
                LatestDecision(agent.Trace), "latest DecisionRecord with Reason or ActionId (public trace)"),
            LastAction = SnapshotField<ActionSummary?>.Derived(
                LatestAction(agent.Trace), "latest DecisionRecord with ActionId+Action (public trace)"),
            RecoveryState = SnapshotField<RecoverySummary?>.Derived(
                LatestRecovery(agent.Trace), "latest DecisionRecord with RecoveryId (public trace)"),
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
    private static DecisionSummary? LatestDecision(ImmutableArray<DecisionRecord> trace)
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
    private static ActionSummary? LatestAction(ImmutableArray<DecisionRecord> trace)
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
    private static RecoverySummary? LatestRecovery(ImmutableArray<DecisionRecord> trace)
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
