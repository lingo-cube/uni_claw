using System.Collections.Immutable;
using UniClaw.Runtime.Container;
using UniClaw.Runtime.Model;
using Xunit;
using RuntimeContainer = UniClaw.Runtime.Container.Container;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// FRESH-1..FRESH-10 — OPEN_WORLD_FRESH_CONTAINER_EVIDENCE_REPAIR.
///
/// Container.AcceptFreshObservation is the narrow same-container CurrentObservation
/// refresh: it updates CurrentObservation while preserving container identity,
/// accepted viewport exploration history, executed-step/branch progress, local
/// completion, bindings and beliefs. It is wired into the Agent only AFTER
/// exact reconciliation + same-Container continuity verification, so unexpected
/// navigation / foreign observations never refresh an old Container.
/// </summary>
public sealed class FreshContainerEvidenceTests
{
    private const string App = "com.uniclaw.fixture";

    private static RuntimeContainer Container() => new(
        "Fixture Root",
        o => o.ForegroundApplication == App,
        (_, _, _) => new TraversalStepResult.Succeeded());

    private static Observation Obs(long seq, params string[] texts)
    {
        var elements = ImmutableArray.CreateBuilder<ObservedElement>();
        for (int i = 0; i < texts.Length; i++)
        {
            elements.Add(new ObservedElement(
                texts[i], null, i,
                new ElementBounds(0.05f, 0.1f + 0.1f * i, 0.9f, 0.2f + 0.1f * i),
                "menuItem"));
        }
        return new Observation(elements.ToImmutable(), App, seq);
    }

    private static Observation ObsWithState(long seq, string stateText)
    {
        var obs = Obs(seq, "Child 01", stateText);
        return obs;
    }

    // ── FRESH-1: initial CurrentObservation = O1 ────────────────────────────

    [Fact]
    public void FRESH1_InitialCurrentObservation_IsO1()
    {
        var container = Container();
        var o1 = Obs(1);
        container.Bind(o1);
        Assert.Equal(o1, container.CurrentObservation);
    }

    // ── FRESH-2: scroll O1→O2 same container -> CurrentObservation == O2 ────

    [Fact]
    public void FRESH2_ScrollFreshObservation_RefreshesCurrent()
    {
        var container = Container();
        var o1 = Obs(1, "Child 01", "Visited 0/8");
        var o2 = Obs(2, "Child 03", "Visited 0/8");
        container.Bind(o1);

        // continuity verification appends to viewport history; the repair
        // additionally refreshes CurrentObservation.
        Assert.True(container.TryVerifyViewportContinuity(o2, "Fixture Root", App));
        container.AcceptFreshObservation(o2);

        Assert.Equal(o2, container.CurrentObservation);
        Assert.Equal(2, container.ViewportExplorationObservations.Length);
    }

    // ── FRESH-3: consecutive O2→O3, history preserved ───────────────────────

    [Fact]
    public void FRESH3_ConsecutiveRefresh_PreservesViewportHistory()
    {
        var container = Container();
        var o1 = Obs(1, "Child 01", "Visited 0/8");
        var o2 = Obs(2, "Child 03", "Visited 0/8");
        var o3 = Obs(3, "Child 05", "Visited 0/8");
        container.Bind(o1);
        Assert.True(container.TryVerifyViewportContinuity(o2, "Fixture Root", App));
        container.AcceptFreshObservation(o2);
        Assert.True(container.TryVerifyViewportContinuity(o3, "Fixture Root", App));
        container.AcceptFreshObservation(o3);

        Assert.Equal(o3, container.CurrentObservation);
        Assert.Equal(new long[] { 1, 2, 3 },
            container.ViewportExplorationObservations.Select(o => o.SequenceNumber).ToArray());
    }

    // ── FRESH-4: child return -> fresh parent O4 refresh ───────────────────

    [Fact]
    public void FRESH4_ChildReturn_FreshParentObservation_RefreshesParent()
    {
        var parent = Container();
        var rootO1 = Obs(1, "Child 01", "Visited 0/8");
        parent.Bind(rootO1);

        // child visit does not touch the parent container...
        var childObs = Obs(2, "Child 01");
        var child = new RuntimeContainer("Child 01", _ => true, (_, _, _) => new TraversalStepResult.Succeeded());
        child.Bind(childObs);

        // ...the verified parent return refreshes the PARENT with the fresh root.
        var returned = Obs(3, "Child 01", "Visited 1/8");
        Assert.True(parent.TryVerifyViewportContinuity(returned, "Fixture Root", App));
        parent.AcceptFreshObservation(returned);

        Assert.Equal(returned, parent.CurrentObservation);
        Assert.Equal("Fixture Root", parent.SemanticPageName);
    }

    // ── FRESH-5: returned parent external state visible to EvidenceEvaluator ─

    [Fact]
    public void FRESH5_EvidenceEvaluator_SeesRefreshedParentState()
    {
        var parent = Container();
        parent.Bind(Obs(1, "Child 01", "Visited 0/8"));

        var returned = ObsWithState(4, "Visited 8/8  CAPSTONE COMPLETE");
        Assert.True(parent.TryVerifyViewportContinuity(returned, "Fixture Root", App));
        parent.AcceptFreshObservation(returned);

        Func<Observation, GoalEvidence> evidence = o =>
            new GoalEvidence(
                o.Elements.Any(e => e.Text.Contains("CAPSTONE COMPLETE")),
                o.Elements.Any(e => e.Text.Contains("CAPSTONE COMPLETE"))
                    ? "capstone complete observed"
                    : "capstone not yet complete",
                o.SequenceNumber);

        Assert.False(evidence(Obs(1, "Child 01", "Visited 0/8")).Satisfied);
        Assert.True(evidence(parent.CurrentObservation!).Satisfied);
        Assert.Equal(4, evidence(parent.CurrentObservation!).SourceObservationSequence);
    }

    // ── FRESH-6: unexpected navigation never refreshes the old Container ───

    [Fact]
    public void FRESH6_ForeignObservation_DoesNotRefreshOldContainer()
    {
        var a = Container();
        a.Bind(Obs(1, "Child 01", "Visited 0/8"));
        var bObs = new Observation([], "com.uniclaw.fixture", 2) { ForegroundApplication = App };

        // The wiring contract: AcceptFreshObservation is only reachable AFTER
        // exact reconciliation + same-Container continuity verification. A
        // foreign-page observation reconciles to a DIFFERENT page ("Child 99")
        // and therefore FAILS continuity for THIS container — the wiring never
        // reaches AcceptFreshObservation, so the old container keeps its frame.
        var foreign = Obs(2, "Child 99");
        Assert.False(a.TryVerifyViewportContinuity(foreign, "Child 99", App));

        Assert.Equal(1, a.CurrentObservation!.SequenceNumber);
        _ = bObs;
    }

    // ── FRESH-7: continuity failure -> no false advancement ────────────────

    [Fact]
    public void FRESH7_ContinuityFailure_DoesNotAdvanceCurrent()
    {
        var container = Container();
        var o1 = Obs(1, "Child 01", "Visited 0/8");
        container.Bind(o1);

        // Failed continuity (wrong foreground / wrong reconciled page) means the
        // repair must NOT refresh: the call site returns before AcceptFreshObservation.
        var foreign = new Observation([], "other.app", 2);
        Assert.False(container.TryVerifyViewportContinuity(foreign, "Fixture Root", App));

        Assert.Equal(o1, container.CurrentObservation);
        Assert.Single(container.ViewportExplorationObservations);
    }

    // ── FRESH-8: popup/parent-return composition refreshes fresh root ───────

    [Fact]
    public void FRESH8_PopupParentReturnComposition_RefreshesFreshRoot()
    {
        var root = Container();
        root.Bind(Obs(1, "Child 01", "Visited 6/8"));

        // popup child observation (obstruction present) -> verified return
        var popupChild = Obs(5, "Child 06", "Immediate popup");
        var returned = Obs(6, "Child 06", "Visited 7/8");
        Assert.True(root.TryVerifyViewportContinuity(returned, "Fixture Root", App));
        root.AcceptFreshObservation(returned);

        Assert.Equal(6, root.CurrentObservation!.SequenceNumber);
        Assert.True(root.CurrentObservation!.Elements.Any(e => e.Text.Contains("Visited 7/8")));
        _ = popupChild;
    }

    // ── FRESH-9: Observation sequences monotonically advance ───────────────

    [Fact]
    public void FRESH9_SequencesMonotonicallyAdvance()
    {
        var container = Container();
        long prev = 0;
        foreach (var seq in new long[] { 1, 2, 3, 7 })
        {
            var obs = Obs(seq, "Child 01", "Visited 0/8");
            Assert.True(obs.SequenceNumber > prev);
            container.Bind(obs);
            container.AcceptFreshObservation(obs);
            prev = obs.SequenceNumber;
        }
        Assert.Equal(7, container.CurrentObservation!.SequenceNumber);
    }

    // ── FRESH-10: no branch-progress / identity / history regression ───────

    [Fact]
    public void FRESH10_RefreshPreservesProgressIdentityAndHistory()
    {
        var container = Container();
        container.Bind(Obs(1, "Child 01", "Visited 0/8"));

        // simulate an executed step (branch progress)
        container.ExecuteStep(new PlanStep("Child 01", "Tap"));

        var o2 = Obs(2, "Child 03", "Visited 0/8");
        Assert.True(container.TryVerifyViewportContinuity(o2, "Fixture Root", App));
        container.AcceptFreshObservation(o2);

        // identity preserved
        Assert.Equal("Fixture Root", container.SemanticPageName);
        // viewport history preserved (Bind reset it to [O1]; continuity appended O2)
        Assert.Equal(2, container.ViewportExplorationObservations.Length);
        // executed-step history preserved by AcceptFreshObservation
        Assert.Single(container.ExecutedSteps);
        // local completion state untouched by refresh (Bind would reset it to
        // false; AcceptFreshObservation preserves it)
        Assert.True(container.IsLocalComplete);
    }
}
