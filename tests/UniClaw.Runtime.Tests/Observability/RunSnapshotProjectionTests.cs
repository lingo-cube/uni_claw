using System.Reflection;
using UniClaw.Runtime.Agent;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Observability;

/// <summary>
/// OBS-F5 (read-only, no mutation) + audited field classification
/// (dsh-kernel-read-only-observability task 2.1) + OBS-F10
/// (no ContainerSnapshot, no Agent public-surface expansion — task 2.2).
/// </summary>
public sealed class RunSnapshotProjectionTests
{
    // ── Field classification truthfulness ────────────────────────────────

    [Fact]
    public void SnapshotFields_CarryAuditedClassifications()
    {
        var snapshot = RunSnapshotProjector.Project(
            ReadOnlyObservabilityFixtures.RunId,
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());

        // DIRECT_PUBLIC_PROJECTION
        Assert.Equal(SnapshotFieldClassification.DirectPublicProjection, snapshot.RunState.Classification);
        Assert.Equal(RunState.Completed, snapshot.RunState.Value);
        Assert.Equal(SnapshotFieldClassification.DirectPublicProjection, snapshot.CurrentSemanticPage.Classification);
        Assert.Equal("Settings", snapshot.CurrentSemanticPage.Value);
        Assert.Equal(SnapshotFieldClassification.DirectPublicProjection, snapshot.ActiveTrap.Classification);
        Assert.Null(snapshot.ActiveTrap.Value);

        // DERIVED_READ_MODEL — visibly flagged with truth source
        Assert.Equal(SnapshotFieldClassification.DerivedReadModel, snapshot.CurrentGoal.Classification);
        Assert.Contains("span", snapshot.CurrentGoal.TruthSource);
        Assert.Equal("WifiConnectivity.Enabled=true", snapshot.CurrentGoal.Value!.Goal);

        Assert.Equal(SnapshotFieldClassification.DerivedReadModel, snapshot.LastDecision.Classification);
        Assert.NotNull(snapshot.LastDecision.Value);
        Assert.Equal(SnapshotFieldClassification.DerivedReadModel, snapshot.LastAction.Classification);
        Assert.Equal("Action-1", snapshot.LastAction.Value!.ActionId);
        Assert.Equal(SnapshotFieldClassification.DerivedReadModel, snapshot.RecoveryState.Classification);
        Assert.Null(snapshot.RecoveryState.Value);

        // NOT_CURRENTLY_AVAILABLE — never invented
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, snapshot.CurrentObservationSequence.Classification);
        Assert.Null(snapshot.CurrentObservationSequence.Value);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, snapshot.CurrentContainerSummary.Classification);
        Assert.Null(snapshot.CurrentContainerSummary.Value);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, snapshot.BindingsSummary.Classification);
        Assert.Null(snapshot.BindingsSummary.Value);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, snapshot.StateBeliefsSummary.Classification);
        Assert.Null(snapshot.StateBeliefsSummary.Value);
    }

    [Fact]
    public void LatestGoalEvidence_IsPartial_NotCurrentlyAvailable_NoFabricatedSourceSequence()
    {
        var snapshot = RunSnapshotProjector.Project(
            ReadOnlyObservabilityFixtures.RunId,
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());

        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, snapshot.LatestGoalEvidence.Classification);
        Assert.True(snapshot.LatestGoalEvidence.IsPartial);

        var evidence = snapshot.LatestGoalEvidence.Value;
        Assert.NotNull(evidence);
        Assert.True(evidence.Satisfied);
        Assert.Contains("goal satisfied", evidence.Reason);
        Assert.Null(evidence.SourceObservationSequence); // full record not on public surface
    }

    [Fact]
    public void GoalSpanAbsent_RecordsDerivedDiagnostic_AndGoalStaysNull()
    {
        var snapshot = RunSnapshotProjector.Project(
            "run-no-goal-span",
            ReadOnlyObservabilityFixtures.EmptyTrace(),
            new AgentStateSnapshot
            {
                RunId = "run-no-goal-span",
                State = RunState.Running,
                Trace = [new DecisionRecord("run-no-goal-span") { RunState = RunState.Running }],
            });

        Assert.Equal(SnapshotFieldClassification.DerivedReadModel, snapshot.CurrentGoal.Classification);
        Assert.Null(snapshot.CurrentGoal.Value);
        Assert.Contains(snapshot.Diagnostics, d => d.Contains("CurrentGoal"));
    }

    // ── OBS-F5: read-only, no mutation ────────────────────────────────────

    [Fact]
    public void SnapshotProjection_IsDeterministic_AndDoesNotMutateInputs()
    {
        var agent = ReadOnlyObservabilityFixtures.CompletedRun();
        var trace = ReadOnlyObservabilityFixtures.CompletedTrace();

        var first = RunSnapshotProjector.Project(ReadOnlyObservabilityFixtures.RunId, trace, agent);
        var second = RunSnapshotProjector.Project(ReadOnlyObservabilityFixtures.RunId, trace, agent);

        // Deterministic: same inputs → identical snapshots.
        Assert.Equal(first, second);

        // Inputs unchanged by projection (records are immutable — verify canonical leaves).
        Assert.Equal(RunState.Completed, agent.State);
        Assert.Equal("goal satisfied: WifiConnectivity.Enabled=true", agent.Reason);
        Assert.Equal(7, agent.Trace.Length);
        Assert.Equal(2, agent.NavigationEvidence.Length);
        Assert.Equal("Action-1", agent.Trace[4].ActionId);
        Assert.Equal("step-2", agent.Trace[5].StepId);
        Assert.Equal("Settings", agent.Belief!.SemanticPage);

        // Snapshot never exposes a mutable container reference: fields are classified values only.
        Assert.All(first.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance),
            p => Assert.True(p.PropertyType.Name.Contains("SnapshotField")
                              || p.Name == "RunId"
                              || p.PropertyType.Name.Contains("ImmutableArray"),
                $"RunSnapshot member {p.Name} must be a classified value or immutable collection"));
    }

    // ── OBS-F10: no ContainerSnapshot, no Agent public-surface expansion ──

    [Fact]
    public void NoContainerSnapshotType_ExistsInDriverHostOrRuntime()
    {
        // No ContainerSnapshot type is introduced anywhere in the slice.
        var driverHostTypes = typeof(DriverHostObservability).Assembly.GetTypes();
        Assert.DoesNotContain(driverHostTypes, t => t.Name.Contains("ContainerSnapshot"));
    }

    [Fact]
    public void AgentPublicSurface_HasNoNewAccessors_ForPrivateContainerState()
    {
        var forbidden = new[]
        {
            "CurrentObservation",
            "CurrentObservationSequence",
            "ObjectBindings",
            "ObjectStateBeliefs",
            "ActiveContainer",
            "Container",
        };

        var publicProperties = typeof(UniClaw.Runtime.Agent.Agent).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet();

        foreach (var name in forbidden)
        {
            Assert.DoesNotContain(name, publicProperties);
        }

        // The buyer (this slice) works with the current public surface only.
        Assert.Contains("State", publicProperties);
        Assert.Contains("Belief", publicProperties);
        Assert.Contains("Trace", publicProperties);
        Assert.Contains("NavigationEvidence", publicProperties);
    }
}
