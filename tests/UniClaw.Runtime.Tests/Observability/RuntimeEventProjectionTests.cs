using System.Collections.Immutable;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Harness;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Observability;

/// <summary>
/// OBS-F2/F3/F4/F9 projection truthfulness + 18-family classification coverage
/// (dsh-kernel-read-only-observability tasks 1.1/1.2).
/// </summary>
public sealed class RuntimeEventProjectionTests
{
    private static readonly RuntimeEventKind[] CClassKinds =
    [
        RuntimeEventKind.DecisionProposed,
        RuntimeEventKind.DecisionAccepted,
        RuntimeEventKind.ActionAuthorized,
        RuntimeEventKind.RecoveryVerified,
    ];

    // ── OBS-F2/F3: truthful absence — C-class never inferred ─────────────

    [Fact]
    public void CClassEvents_NeverEmitted_EvenWhenDispatchAndCompletionExist()
    {
        var projection = RuntimeEventProjector.Project(
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());

        // The truthful sources DO exist: a dispatch and a completion.
        Assert.Contains(projection.Events, e => e.Kind == RuntimeEventKind.ActionDispatched);
        Assert.Contains(projection.Events, e => e.Kind == RuntimeEventKind.RunCompleted);

        // Yet no C-class kind is ever present — never inferred from dispatch/success/ordering.
        Assert.DoesNotContain(projection.Events, e => CClassKinds.Contains(e.Kind));

        // RecoveryStarted exists in the failed fixture, but RecoveryVerified never follows it.
        var failed = RuntimeEventProjector.Project(
            ReadOnlyObservabilityFixtures.EmptyTrace(),
            ReadOnlyObservabilityFixtures.FailedRunWithTrapAndRecovery());
        Assert.Contains(failed.Events, e => e.Kind == RuntimeEventKind.RecoveryStarted);
        Assert.DoesNotContain(failed.Events, e => e.Kind == RuntimeEventKind.RecoveryVerified);
        Assert.DoesNotContain(failed.Events, e => e.Kind == RuntimeEventKind.DecisionAccepted);
    }

    [Fact]
    public void ClassificationCoverage_RecordsAll18Kinds_WithAuditedTable()
    {
        var projection = RuntimeEventProjector.Project(
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());

        var coverage = projection.ClassificationCoverage;
        Assert.Equal(18, coverage.Length);

        // C-class: RequiresNewRuntimeSemanticEmission, never emittable, reason present.
        foreach (var kind in CClassKinds)
        {
            var metadata = RuntimeEventKindTable.For(kind);
            Assert.Equal(RuntimeEventSourceClassification.RequiresNewRuntimeSemanticEmission, metadata.Classification);
            Assert.False(metadata.EmittableInSlice);
            Assert.False(string.IsNullOrWhiteSpace(metadata.NotEmittedReason));
        }

        // B-class with unreachable source: classified B, explicitly NOT emitted this slice, with reason.
        foreach (var kind in new[]
                 {
                     RuntimeEventKind.BindingUpdated,
                     RuntimeEventKind.StateBeliefUpdated,
                     RuntimeEventKind.PostActionObserved,
                     RuntimeEventKind.VerificationCompleted,
                 })
        {
            var metadata = RuntimeEventKindTable.For(kind);
            Assert.Equal(RuntimeEventSourceClassification.DerivableFromExistingPublicReadModel, metadata.Classification);
            Assert.False(metadata.EmittableInSlice);
            Assert.False(string.IsNullOrWhiteSpace(metadata.NotEmittedReason));
        }

        // A=1 (ContainerReconciled), A+B=1 (ActionDispatched), B=12, C=4.
        Assert.Equal(RuntimeEventSourceClassification.DerivableFromExistingSpan,
            RuntimeEventKindTable.For(RuntimeEventKind.ContainerReconciled).Classification);
        Assert.Equal(RuntimeEventSourceClassification.DerivableFromExistingSpanAndPublicReadModel,
            RuntimeEventKindTable.For(RuntimeEventKind.ActionDispatched).Classification);

        var bCount = RuntimeEventKindTable.All.Count(m =>
            m.Classification is RuntimeEventSourceClassification.DerivableFromExistingPublicReadModel
                or RuntimeEventSourceClassification.DerivableFromExistingSpanAndPublicReadModel);
        var cCount = RuntimeEventKindTable.All.Count(m =>
            m.Classification == RuntimeEventSourceClassification.RequiresNewRuntimeSemanticEmission);
        Assert.Equal(13, bCount); // B=12 + A+B=1
        Assert.Equal(4, cCount);
    }

    // ── OBS-F4: partial goal evidence, never fabricated full record ──────

    [Fact]
    public void GoalEvidenceProduced_IsPartial_WithNoSourceObservationSequence()
    {
        var projection = RuntimeEventProjector.Project(
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());

        var goalEvidence = Assert.Single(
            projection.Events.Where(e => e.Kind == RuntimeEventKind.GoalEvidenceProduced));

        var payload = Assert.IsType<GoalEvidenceProducedPayload>(goalEvidence.Payload);
        Assert.True(payload.IsPartial);
        Assert.False(string.IsNullOrWhiteSpace(payload.Reason));
        Assert.Null(goalEvidence.ObservationSequence); // full record not on public surface
    }

    // ── OBS-F9: Sequence is projection ordering, never observation identity ─
    //
    // FROZEN SEMANTICS (OBS-F9A/B/C/D):
    //   RuntimeEvent.Sequence        — projected event ordering metadata (monotonic per run).
    //   ObservationSequence          — Kernel-produced observation evidence anchor.
    //   The two are INDEPENDENT semantic domains. Numeric equality is ALLOWED
    //   (coincidence); inequality is NOT an invariant and proves nothing.
    //   No semantic meaning follows from equality or inequality.

    [Fact]
    public void Sequence_IsProjectionOrdering_NotObservationIdentity()
    {
        var store = new RuntimeEventStore();
        var projection = RuntimeEventProjector.Project(
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());
        var page = store.Append(ReadOnlyObservabilityFixtures.RunId, projection.Events);

        var events = page.Events;
        Assert.NotEmpty(events);

        // Strictly monotonic projected ordering (ordering metadata ONLY).
        for (var i = 1; i < events.Length; i++)
        {
            Assert.True(events[i].Sequence > events[i - 1].Sequence, "projected Sequence must be strictly monotonic");
        }

        // Unique stable EventIds — identity lives in EventId, never in Sequence or ObservationSequence.
        Assert.Equal(events.Length, events.Select(e => e.EventId).Distinct().Count());

        // Observation events carry the Kernel observation sequence — provenance only.
        foreach (var observationEvent in events.Where(e => e.Kind == RuntimeEventKind.ObservationProduced))
        {
            Assert.NotNull(observationEvent.ObservationSequence);
            Assert.Contains(observationEvent.ObservationSequence!.Value, new long[] { 1, 7 });
        }

        // Numeric coincidence is permitted and occurs naturally in this fixture:
        // the ViewportExplorationDecision lands at projected Sequence=7 while its
        // Kernel source-seq is also 7. Equality coexists; no semantics follow.
        var viewport = Assert.Single(events.Where(e => e.Kind == RuntimeEventKind.ViewportExplorationDecision));
        Assert.Equal(7, viewport.Sequence);
        Assert.Equal(7, viewport.ObservationSequence);
        Assert.Equal(viewport.Sequence, viewport.ObservationSequence!.Value); // coincidence, NOT identity

        // Events without an observation anchor exist and carry null ObservationSequence.
        Assert.Contains(events, e => e.Kind == RuntimeEventKind.RunCompleted && e.ObservationSequence is null);
    }

    // ── OBS-F9A: numeric collision adversarial fixture ────────────────────

    [Fact]
    public void SequenceAndObservationSequence_MayCoincideNumerically_WithoutSemanticEquivalence()
    {
        // Deterministic adversarial fixture: exactly enough preceding events so an
        // Observation-bearing event receives RuntimeEvent.Sequence = N while its truthful
        // Kernel ObservationSequence is also N.
        //
        // 1 refresh span  → ContainerReconciled (Sequence=1)
        // 1 observation with Kernel SequenceNumber=3:
        //     ObservationProduced (Sequence=2, ObservationSequence=3)
        //     NavigationDecision  (Sequence=3, ObservationSequence=3)  ← NUMERIC COLLISION
        var trace = new TraceRun
        {
            TraceRunId = "collision-trace",
            TraceId = "collision-trace-id",
            RunId = "collision-run",
            Spans =
            [
                new TraceSpan
                {
                    SpanId = "refresh-1",
                    Name = "RefreshSnapshot",
                    Layer = "CONTAINER",
                    Component = "container.refresh",
                    StartOffsetNs = 0,
                    DurationNs = 5,
                    Outcome = "SUCCEEDED",
                },
            ],
        };
        var agent = new AgentStateSnapshot
        {
            RunId = "collision-run",
            State = RunState.Running,
            NavigationEvidence =
            [
                new Observation([new ObservedElement("Wi-Fi", true, 1)], "Settings", 3),
            ],
        };

        var projection = RuntimeEventProjector.Project(trace, agent);
        Assert.Empty(projection.Diagnostics); // projection succeeds

        var store = new RuntimeEventStore();
        var page = store.Append("collision-run", projection.Events);
        var events = page.Events;

        var collision = Assert.Single(events.Where(e => e.Kind == RuntimeEventKind.NavigationDecision));
        Assert.Equal(3, collision.Sequence);               // projected ordering remains N
        Assert.Equal(3, collision.ObservationSequence);    // kernel anchor remains N
        Assert.Equal(collision.Sequence, collision.ObservationSequence!.Value); // both coexist, equality allowed

        // No production logic treats equality as identity: the event is still a truthful
        // NavigationDecision whose provenance points at the Kernel observation evidence.
        var payload = Assert.IsType<NavigationDecisionPayload>(collision.Payload);
        Assert.Equal(3, payload.SequenceNumber);
        Assert.Equal(3, agent.NavigationEvidence[0].SequenceNumber); // provenance anchor source

        // The SAME kernel observation also produced an ObservationProduced event with a
        // DIFFERENT projected Sequence (2) — same anchor, different ordering slots.
        var produced = Assert.Single(events.Where(e => e.Kind == RuntimeEventKind.ObservationProduced));
        Assert.Equal(2, produced.Sequence);
        Assert.Equal(3, produced.ObservationSequence);
        Assert.NotEqual(produced.Sequence, produced.ObservationSequence); // coincidence-dependent, not invariant

        // Monotonicity and unique EventIds survive the collision.
        Assert.Equal(events.Length, events.Select(e => e.EventId).Distinct().Count());
        for (var i = 1; i < events.Length; i++)
        {
            Assert.True(events[i].Sequence > events[i - 1].Sequence);
        }
    }

    // ── OBS-F9B: ObservationSequence provenance — kernel anchors only ─────

    [Fact]
    public void ObservationSequence_Provenance_OnlyFromKernelObservationEvidence()
    {
        // Completed run: the only Kernel observation-bearing sources are
        // NavigationEvidence sequences {1, 7} (and the trace Reason's source-seq=7,
        // which is Kernel-written text referencing the same evidence domain).
        var store = new RuntimeEventStore();
        var page = store.Append(
            ReadOnlyObservabilityFixtures.RunId,
            RuntimeEventProjector.Project(
                ReadOnlyObservabilityFixtures.CompletedTrace(),
                ReadOnlyObservabilityFixtures.CompletedRun()).Events);

        var expectedAnchors = new long[] { 1, 7 };
        var observationSequences = page.Events
            .Where(e => e.ObservationSequence is not null)
            .Select(e => e.ObservationSequence!.Value)
            .ToArray();
        Assert.NotEmpty(observationSequences);

        // EVERY emitted ObservationSequence ∈ the Kernel anchor set — and nothing else.
        Assert.All(observationSequences, seq => Assert.Contains(seq, expectedAnchors));

        // NO mapping ObservationSequence = RuntimeEvent.Sequence: projected Sequences
        // range beyond the anchor set (e.g. 6, 8, 9 exist), yet no event ever carries
        // an ObservationSequence outside {1, 7}.
        Assert.Contains(page.Events, e => e.Sequence == 6 && e.Kind == RuntimeEventKind.ActionDispatched);
        Assert.Contains(page.Events, e => e.Sequence == 9 && e.Kind == RuntimeEventKind.RunCompleted);
        Assert.DoesNotContain(page.Events, e => e.ObservationSequence is { } seq && !expectedAnchors.Contains(seq));

        // Failed+trap run: anchors = NavigationEvidence {7} ∪ LastTrap {expected=3, observed=7}.
        var failedStore = new RuntimeEventStore();
        var failedPage = failedStore.Append(
            "run-failed",
            RuntimeEventProjector.Project(
                ReadOnlyObservabilityFixtures.EmptyTrace(),
                ReadOnlyObservabilityFixtures.FailedRunWithTrapAndRecovery()).Events);

        var failedAnchors = new long[] { 3, 7 };
        var trap = Assert.Single(failedPage.Events.Where(e => e.Kind == RuntimeEventKind.TrapRaised));
        Assert.Equal(7, trap.ObservationSequence); // observed ?? expected — from Agent.LastTrap
        Assert.Contains(trap.ObservationSequence!.Value, failedAnchors);
        Assert.All(
            failedPage.Events.Where(e => e.ObservationSequence is not null),
            e => Assert.Contains(e.ObservationSequence!.Value, failedAnchors));
    }

    // ── OBS-F9C: GoalEvidence freshness separation ────────────────────────

    [Fact]
    public void GoalEvidenceAndTerminalEvents_NeverCarryObservationSequence()
    {
        var store = new RuntimeEventStore();
        var page = store.Append(
            ReadOnlyObservabilityFixtures.RunId,
            RuntimeEventProjector.Project(
                ReadOnlyObservabilityFixtures.CompletedTrace(),
                ReadOnlyObservabilityFixtures.CompletedRun()).Events);

        // Partial GoalEvidence: ObservationSequence stays null (no freshness derivation).
        var goalEvidence = Assert.Single(page.Events.Where(e => e.Kind == RuntimeEventKind.GoalEvidenceProduced));
        Assert.Null(goalEvidence.ObservationSequence);
        Assert.True(goalEvidence.Sequence > 0); // it HAS a projected Sequence…

        // RunCompleted: ObservationSequence stays null — RuntimeEvent.Sequence is never
        // copied into it, whatever its numeric value.
        var completed = Assert.Single(page.Events.Where(e => e.Kind == RuntimeEventKind.RunCompleted));
        Assert.Null(completed.ObservationSequence);
        Assert.Equal(9, completed.Sequence);
        Assert.Null(completed.ObservationSequence); // Sequence=9 did NOT become ObservationSequence=9

        // RunSnapshot.LatestGoalEvidence.SourceObservationSequence stays null for the
        // partial projection (full record not on the Agent public surface).
        var snapshot = RunSnapshotProjector.Project(
            ReadOnlyObservabilityFixtures.RunId,
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());
        Assert.True(snapshot.LatestGoalEvidence.IsPartial);
        Assert.Null(snapshot.LatestGoalEvidence.Value!.SourceObservationSequence);
    }

    // ── OBS-F9D: EventId identifies the projected EVENT, not the Observation ─

    [Fact]
    public void EventId_IdentifiesProjectedEvent_NotUnderlyingObservation()
    {
        var store = new RuntimeEventStore();
        var page = store.Append(
            ReadOnlyObservabilityFixtures.RunId,
            RuntimeEventProjector.Project(
                ReadOnlyObservabilityFixtures.CompletedTrace(),
                ReadOnlyObservabilityFixtures.CompletedRun()).Events);

        var events = page.Events;

        // Two events reference the SAME Kernel observation (SequenceNumber=7):
        // ObservationProduced(7) and NavigationDecision(7). Their EventIds MUST differ —
        // identity belongs to the projected event, not the observation.
        var produced = Assert.Single(events.Where(e => e.Kind == RuntimeEventKind.ObservationProduced && e.ObservationSequence == 7));
        var navigated = Assert.Single(events.Where(e => e.Kind == RuntimeEventKind.NavigationDecision && e.ObservationSequence == 7));
        Assert.Equal(7, produced.ObservationSequence);
        Assert.Equal(7, navigated.ObservationSequence);
        Assert.NotEqual(produced.EventId, navigated.EventId);

        // EventId derives from runId + projected Sequence (store contract) — the two
        // semantic domains never collapse.
        Assert.Equal($"evt-{ReadOnlyObservabilityFixtures.RunId}-{produced.Sequence}", produced.EventId);
        Assert.Equal($"evt-{ReadOnlyObservabilityFixtures.RunId}-{navigated.Sequence}", navigated.EventId);
        Assert.Equal(4, produced.Sequence);
        Assert.Equal(5, navigated.Sequence);

        // No two events in the run share an EventId (identity is per-event, globally unique).
        Assert.Equal(events.Length, events.Select(e => e.EventId).Distinct().Count());

        // And the observation anchor repeats across events without collapsing into EventId.
        Assert.True(events.Count(e => e.ObservationSequence == 7) >= 2);
    }

    // ── Per-kind derivation truthfulness ─────────────────────────────────

    [Fact]
    public void ActionDispatched_DerivesFromDispatchTraceEvents_AndNeverActionAuthorized()
    {
        var projection = RuntimeEventProjector.Project(
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());

        var dispatched = projection.Events.Where(e => e.Kind == RuntimeEventKind.ActionDispatched).ToArray();
        Assert.Single(dispatched);

        var payload = Assert.IsType<ActionDispatchedPayload>(dispatched[0].Payload);
        Assert.Equal("Action-1", payload.ActionId);
        Assert.Equal("step-1", payload.StepId);
        Assert.Equal("Settings", payload.ContainerId);
        Assert.Contains("SetSwitch", payload.ActionDescription);
        Assert.Contains("index=1", payload.ActionDescription);
        // Coordinate/bounds data must never leak into the description (coordinate-free summary).
        Assert.DoesNotContain("Bounds", payload.ActionDescription);
        Assert.DoesNotContain(projection.Events, e => e.Kind == RuntimeEventKind.ActionAuthorized);
    }

    [Fact]
    public void ViewportExplorationDecision_ParsedFromClassifiedReason()
    {
        var projection = RuntimeEventProjector.Project(
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());

        var viewport = Assert.Single(
            projection.Events.Where(e => e.Kind == RuntimeEventKind.ViewportExplorationDecision));

        var payload = Assert.IsType<ViewportExplorationDecisionPayload>(viewport.Payload);
        Assert.Equal("exhausted", payload.Outcome);
        Assert.Equal(7, payload.SourceObservationSequence);
        Assert.Equal(7, viewport.ObservationSequence);
        Assert.Equal("step-2", payload.StepId);
    }

    [Fact]
    public void UnparseableViewportReason_IsNotEmitted_AndRecordsDiagnostic()
    {
        var runId = "run-bad";
        var agent = new AgentStateSnapshot
        {
            RunId = runId,
            State = RunState.Running,
            Trace =
            [
                new TraceEvent(runId) { Reason = "viewport exploration incomplete-reason-without-source-seq" },
            ],
        };

        var projection = RuntimeEventProjector.Project(ReadOnlyObservabilityFixtures.EmptyTrace(), agent);

        Assert.DoesNotContain(projection.Events, e => e.Kind == RuntimeEventKind.ViewportExplorationDecision);
        Assert.Contains(projection.Diagnostics, d => d.Contains("ViewportExplorationDecision NOT emitted"));
    }

    [Fact]
    public void TrapRaised_FromTraceAndMatchingLastTrap()
    {
        var projection = RuntimeEventProjector.Project(
            ReadOnlyObservabilityFixtures.EmptyTrace(),
            ReadOnlyObservabilityFixtures.FailedRunWithTrapAndRecovery());

        var trap = Assert.Single(projection.Events.Where(e => e.Kind == RuntimeEventKind.TrapRaised));

        var payload = Assert.IsType<TrapRaisedPayload>(trap.Payload);
        Assert.Equal(TrapKind.StateMismatch, payload.TrapKind);
        Assert.Equal(TrapScope.Agent, payload.TrapScope);
        Assert.Equal(3, payload.ExpectedSequence);
        Assert.Equal(7, payload.ObservedSequence);
        Assert.Equal(7, trap.ObservationSequence);
    }

    [Fact]
    public void RecoveryStarted_FromRecoveryIdTraceEvent()
    {
        var projection = RuntimeEventProjector.Project(
            ReadOnlyObservabilityFixtures.EmptyTrace(),
            ReadOnlyObservabilityFixtures.FailedRunWithTrapAndRecovery());

        var recovery = Assert.Single(projection.Events.Where(e => e.Kind == RuntimeEventKind.RecoveryStarted));
        var payload = Assert.IsType<RecoveryStartedPayload>(recovery.Payload);
        Assert.Equal("recovery-1", payload.RecoveryId);
        Assert.Contains("recovery started", payload.Reason);
        Assert.Null(recovery.ObservationSequence); // no attributable kernel observation anchor
    }

    [Fact]
    public void RunFailed_FromState_AndRunCompleted_FromState()
    {
        var failed = RuntimeEventProjector.Project(
            ReadOnlyObservabilityFixtures.EmptyTrace(),
            ReadOnlyObservabilityFixtures.FailedRunWithTrapAndRecovery());
        var runFailed = Assert.Single(failed.Events.Where(e => e.Kind == RuntimeEventKind.RunFailed));
        Assert.IsType<RunFailedPayload>(runFailed.Payload);
        Assert.DoesNotContain(failed.Events, e => e.Kind == RuntimeEventKind.RunCompleted);

        var completed = RuntimeEventProjector.Project(
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());
        var runCompleted = Assert.Single(completed.Events.Where(e => e.Kind == RuntimeEventKind.RunCompleted));
        var payload = Assert.IsType<RunCompletedPayload>(runCompleted.Payload);
        Assert.Contains("goal satisfied", payload.Reason);
        Assert.DoesNotContain(completed.Events, e => e.Kind == RuntimeEventKind.RunFailed);
    }

    [Fact]
    public void ContainerReconciled_FromRefreshSnapshotSpan_WithOutcome()
    {
        var projection = RuntimeEventProjector.Project(
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());

        var reconciled = Assert.Single(projection.Events.Where(e => e.Kind == RuntimeEventKind.ContainerReconciled));
        var payload = Assert.IsType<ContainerReconciledPayload>(reconciled.Payload);
        Assert.Equal("s2", payload.SpanId);
        Assert.Equal("SUCCEEDED", payload.Outcome);

        // No refresh span → no ContainerReconciled (truthful absence).
        var empty = RuntimeEventProjector.Project(
            ReadOnlyObservabilityFixtures.EmptyTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());
        Assert.DoesNotContain(empty.Events, e => e.Kind == RuntimeEventKind.ContainerReconciled);
    }

    [Fact]
    public void ObservationAndNavigationDecision_DeriveFromNavigationEvidence()
    {
        var projection = RuntimeEventProjector.Project(
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());

        var produced = projection.Events.Where(e => e.Kind == RuntimeEventKind.ObservationProduced).ToArray();
        var navigated = projection.Events.Where(e => e.Kind == RuntimeEventKind.NavigationDecision).ToArray();
        Assert.Equal(2, produced.Length);
        Assert.Equal(2, navigated.Length);

        var first = Assert.IsType<ObservationProducedPayload>(produced[0].Payload);
        Assert.Equal(1, first.SequenceNumber);
        Assert.Equal("Settings", first.ForegroundApplication);
        Assert.Equal(1, first.ElementCount);
    }

    // ── Diagnostics: truthful gaps, never manufactured continuity ────────

    [Fact]
    public void MismatchedRunIds_RecordDiagnostic_AndAnchorOnAgentRunId()
    {
        var trace = new TraceRun { TraceRunId = "t", TraceId = ReadOnlyObservabilityFixtures.TraceId, RunId = "other-run" };
        var projection = RuntimeEventProjector.Project(
            trace,
            ReadOnlyObservabilityFixtures.CompletedRun());

        Assert.Equal(ReadOnlyObservabilityFixtures.RunId, projection.RunId);
        Assert.Contains(projection.Diagnostics, d => d.Contains("differs from AgentStateSnapshot.RunId"));
        Assert.All(projection.Events, e => Assert.Equal(ReadOnlyObservabilityFixtures.RunId, e.RunId));
    }

    [Fact]
    public void RunStateLifecycleTraceEntries_ProduceNoEvents()
    {
        // Idle/Initializing/Running trace entries are not part of the 18-family vocabulary —
        // they must not leak as events.
        var projection = RuntimeEventProjector.Project(
            ReadOnlyObservabilityFixtures.EmptyTrace(),
            new AgentStateSnapshot
            {
                RunId = "run-lifecycle",
                State = RunState.Running,
                Trace =
                [
                    new TraceEvent("run-lifecycle") { RunState = RunState.Idle },
                    new TraceEvent("run-lifecycle") { RunState = RunState.Initializing },
                    new TraceEvent("run-lifecycle") { RunState = RunState.Running },
                ],
            });

        Assert.Empty(projection.Events);
    }
}
