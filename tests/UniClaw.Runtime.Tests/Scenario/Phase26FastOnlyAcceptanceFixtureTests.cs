using System.Collections.Immutable;
using System.Reflection;
using UniClaw.Runtime.Agent;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Recovery;
using UniClaw.Runtime.Startup;
using UniClaw.Runtime.Traversal;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// PHASE 2.6 FAST-ONLY ACCEPTANCE FIXTURE MATRIX — WI-CRV2-P26-A / Task 10.1a.
///
/// Declared acceptance oracle for the upcoming fresh Phase 2.6 campaign
/// (P26-F1..P26-F12). Every fixture drives the PRODUCTION Agent reconciliation
/// path: the private <c>TryPrepareContainerReconciliation</c> →
/// <c>CommitContainerReconciliation</c> seam of <see cref="RuntimeAgent"/> (the
/// same construction used by ContainerReconciliationTests / Stage C replay),
/// and asserts only on the public read surface (<see cref="RuntimeAgent.Belief"/>,
/// <see cref="RuntimeAgent.ContainerContext"/>, Trace / ContainerTransitions /
/// BranchProgress) plus existing fail-closed behavior.
///
/// It does NOT re-prove R8 reducer unit semantics: no test here instantiates
/// <see cref="ContainerRuntimeV2Reducer"/> directly. Slow stays Disabled; no
/// advisor / Shadow / provider path is enabled. NET_NEW_MUTABLE_TRUTH = 0
/// (test-only assertions, zero production change).
///
/// Each test name / assertion carries its P26-Fn identifier so that a failure
/// maps to exactly one acceptance point.
/// </summary>
public sealed class Phase26FastOnlyAcceptanceFixtureTests
{
    // ── P26-F1 ──────────────────────────────────────────────────────────────
    // r5: fresh observed SettingsRoot while execution obligation Display is
    // unresolved. Verify: CurrentContainer=SettingsRoot, Active execution=Display,
    // no forced reconciliation, no fabricated recovery/action/completion.
    [Fact]
    public void P26_F1_R5_FreshObservedRootWithUnresolvedExecutionObligation()
    {
        var agent = NewAgent();
        var display = NewContainer("Display");
        SetActiveExecution(agent, display);
        InitializeV2(agent, "Display", 2);

        var fresh = Observation(3);
        var context = GetActiveContext(agent);
        var state = PrepareAndCommit(
            agent,
            fresh,
            Belief("SettingsRoot", 3),
            context,
            Classification("Display", "SettingsRoot", 3, "Display") with
            {
                EvidenceRef = "observation:3:r5-settings-root",
            },
            context);

        // Current physical location = SettingsRoot (V2 current = belief).
        Assert.Equal("SettingsRoot", agent.Belief!.SemanticPage);
        Assert.Equal("SettingsRoot", agent.ContainerContext.CurrentObservedLocation);
        Assert.NotNull(agent.ContainerContext.CurrentNodeRef);
        Assert.Equal(
            state.Graph.Nodes.Single(n => n.NodeRef == state.CurrentContainer!.NodeRef).SemanticIdentityCandidate,
            agent.Belief.SemanticPage);

        // Execution obligation stays Display — Current != ActiveExecution is legal (r5).
        Assert.Equal("Display", agent.ContainerContext.ActiveExecutionContainer);
        Assert.Equal("SettingsRoot", agent.ContainerContext.CurrentObservedLocation);
        Assert.NotEqual(agent.ContainerContext.ActiveExecutionContainer, agent.ContainerContext.CurrentObservedLocation);

        // No forced reconciliation: the transition is a preserved non-parent observation.
        Assert.Equal(ContainerTransitionKind.KNOWN_NON_PARENT_TRANSITION, agent.LatestContainerTransition!.Kind);
        Assert.Equal(ContainerTransitionDisposition.OBSERVED_EXECUTION_PRESERVED, agent.LatestContainerTransition.Disposition);

        // No fabricated recovery / action / completion: every trace entry is evidence-only.
        Assert.All(agent.Trace, entry =>
        {
            Assert.Null(entry.Action);
            Assert.Null(entry.ActionId);
            Assert.Null(entry.RecoveryId);
        });
        Assert.Null(agent.LastTrap);
        Assert.False(state.Graph.Relations.Any());
    }

    // ── P26-F2 ──────────────────────────────────────────────────────────────
    // multi-entry: Desktop→Settings and Search→Settings. Verify: same logical
    // destination is legal, distinct EntryContext/relation, no canonical parent.
    [Fact]
    public void P26_F2_MultiEntrySameDestinationKeepsDistinctEntryAndNoCanonicalParent()
    {
        var agent = NewAgent();
        var home = NewContainer("Home");
        var desktop = NewContainer("Desktop");
        var settings = NewContainer("Settings");
        var search = NewContainer("Search");
        SetActiveExecution(agent, home);
        InitializeV2(agent, "Home", 1);

        // Home → Desktop (authorized child entry).
        var desktopContext = GetActiveContext(agent).EnterChild(desktop, "Desktop");
        var desktopState = PrepareAndCommit(
            agent,
            Observation(2),
            Belief("Desktop", 2),
            GetActiveContext(agent),
            Classification("Home", "Desktop", 2, "Home", "Home") with
            {
                IsAuthorizedChildEntry = true,
                EvidenceRef = "entry:home-desktop",
            },
            desktopContext,
            expectedEnteredChildObligationIdentity: "Desktop");
        var desktopEntryContext = Assert.IsType<ContainerEntryContext>(desktopState.CurrentContainer!.EntryContext);

        // Desktop → Settings (authorized child entry).
        var settingsFromDesktopContext = GetActiveContext(agent).EnterChild(settings, "Settings", desktopEntryContext);
        var settingsFromDesktop = PrepareAndCommit(
            agent,
            Observation(3),
            Belief("Settings", 3),
            GetActiveContext(agent),
            Classification("Desktop", "Settings", 3, "Desktop", "Desktop") with
            {
                IsAuthorizedChildEntry = true,
                EvidenceRef = "entry:desktop-settings",
            },
            settingsFromDesktopContext,
            expectedEnteredChildObligationIdentity: "Settings");
        var settingsNode = settingsFromDesktop.CurrentContainer!.NodeRef;
        var settingsDesktopEntry = Assert.IsType<ContainerEntryContext>(settingsFromDesktop.CurrentContainer.EntryContext);

        // Return to Desktop, then enter Search.
        var childContext = GetActiveContext(agent);
        Assert.True(childContext.TryReturnToParent(out var resumedDesktop, out _));
        PrepareAndCommit(
            agent,
            Observation(4),
            Belief("Desktop", 4),
            childContext,
            Classification("Settings", "Desktop", 4, "Settings", "Desktop") with
            {
                IsVerifiedReturn = true,
                EvidenceRef = "return:settings-desktop",
            },
            resumedDesktop!);

        var searchContext = GetActiveContext(agent).EnterChild(search, "Search", desktopEntryContext);
        var searchState = PrepareAndCommit(
            agent,
            Observation(5),
            Belief("Search", 5),
            GetActiveContext(agent),
            Classification("Desktop", "Search", 5, "Desktop", "Desktop") with
            {
                IsAuthorizedChildEntry = true,
                EvidenceRef = "entry:desktop-search",
            },
            searchContext,
            expectedEnteredChildObligationIdentity: "Search");
        var searchEntryContext = Assert.IsType<ContainerEntryContext>(searchState.CurrentContainer!.EntryContext);

        // Search → Settings (second path to the SAME logical destination).
        var settingsFromSearchContext = GetActiveContext(agent).EnterChild(settings, "Settings", searchEntryContext);
        var settingsFromSearch = PrepareAndCommit(
            agent,
            Observation(6),
            Belief("Settings", 6),
            GetActiveContext(agent),
            Classification("Search", "Settings", 6, "Search", "Search") with
            {
                IsAuthorizedChildEntry = true,
                EvidenceRef = "entry:search-settings",
            },
            settingsFromSearchContext,
            expectedEnteredChildObligationIdentity: "Settings");

        // P26-F2 Verify: same logical destination node is legal and shared.
        Assert.Equal(settingsNode, settingsFromSearch.CurrentContainer!.NodeRef);
        Assert.Equal(settingsNode, agent.ContainerContext.CurrentNodeRef);

        // Distinct EntryContext (different entry source evidence).
        Assert.NotNull(settingsFromDesktop.CurrentContainer.EntryContext);
        Assert.NotNull(settingsFromSearch.CurrentContainer.EntryContext);
        Assert.NotEqual(
            settingsDesktopEntry.SourceNodeRef,
            settingsFromSearch.CurrentContainer.EntryContext!.SourceNodeRef);
        Assert.Equal(settingsDesktopEntry, settingsFromDesktop.CurrentContainer.EntryContext);
        Assert.NotEqual(settingsDesktopEntry, settingsFromSearch.CurrentContainer.EntryContext);

        // Two distinct normal relations into Settings — no canonical single parent.
        var settingsRelations = settingsFromSearch.Graph.Relations
            .Where(r => r.DestinationNodeRef == settingsNode)
            .ToArray();
        Assert.Equal(2, settingsRelations.Length);
        Assert.NotEqual(settingsRelations[0].RelationRef, settingsRelations[1].RelationRef);

        // P26-F2: no canonical parent — the read model exposes entry source, not a single parent authority.
        Assert.DoesNotContain(
            typeof(ContainerTransitionReadModel).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            p => p.Name.Contains("CanonicalParent", StringComparison.OrdinalIgnoreCase));
    }

    // ── P26-F3 ──────────────────────────────────────────────────────────────
    // path-relative return: from B enter D, Back expectation=B only from the
    // current EntryContext; a fresh world observation verifies the actual return.
    // RETURN_EXPECTATION != RETURN_TRUTH.
    [Fact]
    public void P26_F3_PathRelativeReturnExpectationDerivesFromEntryContextAndFreshWorldVerifies()
    {
        var agent = NewAgent();
        var home = NewContainer("Home");
        var parent = NewContainer("B");
        var child = NewContainer("D");
        SetActiveExecution(agent, home);
        InitializeV2(agent, "Home", 1);

        // Home → B (authorized child entry): B acquires a real EntryContext from Home.
        var enteredParent = GetActiveContext(agent).EnterChild(parent, "B");
        var parentState = PrepareAndCommit(
            agent,
            Observation(2),
            Belief("B", 2),
            GetActiveContext(agent),
            Classification("Home", "B", 2, "Home", "Home") with
            {
                IsAuthorizedChildEntry = true,
                EvidenceRef = "entry:home-b",
            },
            enteredParent,
            expectedEnteredChildObligationIdentity: "B");
        var parentEntryContext = Assert.IsType<ContainerEntryContext>(parentState.CurrentContainer!.EntryContext);

        // B → D (authorized child entry), carrying B's own EntryContext evidence so
        // the eventual verified return can restore it (path-relative, no reverse edge).
        var enteredChild = GetActiveContext(agent).EnterChild(child, "D", parentEntryContext);
        var enteredState = PrepareAndCommit(
            agent,
            Observation(3),
            Belief("D", 3),
            GetActiveContext(agent),
            Classification("B", "D", 3, "B", "B") with
            {
                IsAuthorizedChildEntry = true,
                EvidenceRef = "entry:b-d",
            },
            enteredChild,
            expectedEnteredChildObligationIdentity: "D");

        // P26-F3 part (a): RETURN_EXPECTATION derives ONLY from the current
        // EntryContext (source = B), never from a reverse/topology edge.
        var entry = Assert.IsType<ContainerEntryContext>(enteredState.CurrentContainer!.EntryContext);
        Assert.Equal(new ContainerNodeRef(entry.SourceNodeRef.Value), enteredState.Graph.Nodes
            .Single(n => n.NodeRef == entry.SourceNodeRef).NodeRef);
        Assert.Equal("B", enteredState.Graph.Nodes
            .Single(n => n.NodeRef == entry.SourceNodeRef).SemanticIdentityCandidate);
        Assert.NotNull(agent.ContainerContext.EntrySourceNodeRef);
        Assert.Equal(entry.SourceNodeRef, agent.ContainerContext.EntrySourceNodeRef);

        // P26-F3 part (b): fresh world verification completes the actual return and
        // restores B's real EntryContext — RETURN_TRUTH comes from the fresh world,
        // not from the current EntryContext expectation alone.
        var childCtx = GetActiveContext(agent);
        Assert.True(childCtx.TryReturnToParent(out var resumedParent, out _));
        var returnedState = PrepareAndCommit(
            agent,
            Observation(4),
            Belief("B", 4),
            childCtx,
            Classification("D", "B", 4, "D", "B") with
            {
                IsVerifiedReturn = true,
                EvidenceRef = "return:d-b",
            },
            resumedParent!);

        Assert.Equal(ContainerTransitionKind.VERIFIED_RETURN_TO_ACTIVE_PARENT, agent.LatestContainerTransition!.Kind);
        Assert.Equal(ContainerTransitionDisposition.OBSERVED_AND_EXECUTION_RESUMED, agent.LatestContainerTransition.Disposition);
        Assert.Equal("B", agent.Belief!.SemanticPage);
        Assert.Equal("B", agent.ContainerContext.ActiveExecutionContainer);
        Assert.Equal("B", agent.ContainerContext.CurrentObservedLocation);
        // The actual return restored B's real entry context (path-relative).
        Assert.Equal(parentEntryContext, returnedState.CurrentContainer!.EntryContext);
    }

    // ── P26-F4 ──────────────────────────────────────────────────────────────
    // working unknown: MAY_ENTER_CONTAINER → fresh independent unknown page.
    // Verify: INITIALIZED working node can first become current
    // (NODE_EXISTS != IDENTITY_PROVEN).
    [Fact]
    public void P26_F4_WorkingUnknownNodeBecomesCurrentBeforeIdentityProven()
    {
        var agent = NewAgent();
        var home = NewContainer("Home");
        SetActiveExecution(agent, home);
        InitializeV2(agent, "Home", 1);

        // Same-container working continuity into a fresh independent Unknown page
        // (no authorized child, no identity): a NEW_CONTAINER working node.
        var state = PrepareAndCommit(
            agent,
            Observation(2),
            UnknownBelief(2),
            GetActiveContext(agent),
            Classification("Home", null, 2, "Home") with
            {
                EvidenceRef = "observation:2:working-unknown",
            },
            GetActiveContext(agent));

        // P26-F4 Verify: the INITIALIZED working node first becomes physical current.
        Assert.NotNull(state.CurrentContainer);
        var currentNode = state.Graph.Nodes.Single(n => n.NodeRef == state.CurrentContainer!.NodeRef);
        Assert.Equal(ContainerNodeLifecycleStage.INITIALIZED, currentNode.LifecycleStage);
        Assert.Equal(state.CurrentContainer.NodeRef, agent.ContainerContext.CurrentNodeRef);

        // NODE_EXISTS == true but IDENTITY_PROVEN == false: no identity fabricated.
        Assert.Null(currentNode.SemanticIdentityCandidate);
        Assert.Null(agent.Belief!.SemanticPage);
        Assert.Equal(0f, agent.Belief!.Confidence);

        // UNKNOWN_TRANSITION retained evidence, no completion/action authority.
        Assert.Equal(ContainerTransitionKind.UNKNOWN_TRANSITION, agent.LatestContainerTransition!.Kind);
        Assert.All(agent.Trace, entry => Assert.Null(entry.Action));
    }

    // ── P26-F5 ──────────────────────────────────────────────────────────────
    // off-path: expected child C but fresh world = unrelated D. Verify: physical
    // CurrentContainer accepts D, TransitionOccurrence retained, no normal Graph
    // edge fabricated, obligation C independent.
    [Fact]
    public void P26_F5_OffPathObservedChildDoesNotFabricateNormalEdgeAndObligationStaysIndependent()
    {
        var agent = NewAgent();
        var parent = NewContainer("Parent");
        SetActiveExecution(agent, parent);
        InitializeV2(agent, "Parent", 1);

        // Intended child "C" is the authorized obligation under Parent; observe
        // unrelated "D" instead (not marked authorized, not on path → off-path).
        var state = PrepareAndCommit(
            agent,
            Observation(2),
            Belief("D", 2),
            GetActiveContext(agent),
            Classification("Parent", "D", 2, "Parent") with
            {
                EvidenceRef = "observation:2:off-path-d",
            },
            GetActiveContext(agent));

        // P26-F5 Verify: physical CurrentContainer accepts the observed D.
        var currentSemantic = state.Graph.Nodes
            .Single(n => n.NodeRef == state.CurrentContainer!.NodeRef).SemanticIdentityCandidate;
        Assert.Equal("D", currentSemantic);
        Assert.Equal("D", agent.Belief!.SemanticPage);
        Assert.Equal("D", agent.ContainerContext.CurrentObservedLocation);

        // Occurrence retained (off-path evidence is append-only readable).
        Assert.Single(state.TransitionOccurrences, o => o.DestinationNodeRef == state.CurrentContainer!.NodeRef);
        Assert.Equal(ContainerTransitionKind.KNOWN_NON_PARENT_TRANSITION, agent.LatestContainerTransition!.Kind);

        // No normal Graph edge fabricated for the off-path D.
        Assert.Empty(state.Graph.Relations);

        // Intended obligation C stays independent (execution obligation remains Parent,
        // not advanced to D, and no completion was recorded).
        Assert.Equal("Parent", agent.ContainerContext.ActiveExecutionContainer);
        Assert.Equal(ContainerTransitionDisposition.OBSERVED_EXECUTION_PRESERVED, agent.LatestContainerTransition!.Disposition);
        Assert.All(agent.Trace, entry => Assert.Null(entry.Action));
    }

    // ── P26-F6 ──────────────────────────────────────────────────────────────
    // Fast resolution: trigger semantic + fresh destination semantic + Graph prior.
    // Verify: Fast produces a working interpretation with NO
    // action/completion/obligation authority (assessment available, authority absent).
    [Fact]
    public void P26_F6_FastProducesWorkingInterpretationWithoutActionOrCompletionAuthority()
    {
        var agent = NewAgent();
        var display = NewContainer("Display");
        SetActiveExecution(agent, display);
        InitializeV2(agent, "Display", 2);

        var fresh = Observation(3);
        var context = GetActiveContext(agent);
        var state = PrepareAndCommit(
            agent,
            fresh,
            Belief("SettingsRoot", 3),
            context,
            Classification("Display", "SettingsRoot", 3, "Display") with
            {
                EvidenceRef = "observation:3:fast-working",
            },
            context);

        // P26-F6 Verify part (a): Fast assessment is AVAILABLE for this committed
        // V2 state (V2 exists → NotRetained; not Unavailable = no state).
        Assert.True(agent.ContainerContext.IsV2StateAvailable);
        Assert.Equal(
            ContainerFastAssessmentAvailability.NotRetained,
            agent.ContainerContext.FastAssessmentAvailability);

        // Fast produced a working interpretation: the fresh destination became current.
        Assert.Equal("SettingsRoot", state.Graph.Nodes
            .Single(n => n.NodeRef == state.CurrentContainer!.NodeRef).SemanticIdentityCandidate);
        Assert.Equal("SettingsRoot", agent.Belief!.SemanticPage);

        // P26-F6 Verify part (b): Fast grants NO action/completion/obligation authority.
        // Execution obligation unchanged (not advanced to the Fast destination).
        Assert.Equal("Display", agent.ContainerContext.ActiveExecutionContainer);
        Assert.Equal(ContainerTransitionDisposition.OBSERVED_EXECUTION_PRESERVED, agent.LatestContainerTransition!.Disposition);
        Assert.Null(agent.LastTrap);
        Assert.All(agent.Trace, entry =>
        {
            Assert.Null(entry.Action);
            Assert.Null(entry.ActionId);
            Assert.Null(entry.RecoveryId);
        });
        // No latest mutable Fast slot is retained (authority-free projection).
        Assert.DoesNotContain(typeof(RuntimeAgent).GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            f => f.Name.Contains("latestFast", StringComparison.OrdinalIgnoreCase));
    }

    // ── P26-F7 ──────────────────────────────────────────────────────────────
    // deep Unknown: traversal to a semantically unresolved item/page. Verify:
    // Unknown does not pollute Graph/current and does not force false identity.
    [Fact]
    public void P26_F7_DeepUnknownDoesNotPolluteCurrentOrForceFalseIdentity()
    {
        var agent = NewAgent();
        var root = NewContainer("Root");
        SetActiveExecution(agent, root);
        InitializeV2(agent, "Root", 1);

        var state = PrepareAndCommit(
            agent,
            Observation(2),
            UnknownBelief(2),
            GetActiveContext(agent),
            Classification("Root", null, 2, "Root") with
            {
                EvidenceRef = "observation:2:deep-unknown",
            },
            GetActiveContext(agent));

        // P26-F7: Unknown current node exists but forces no false identity.
        var unknownNode = state.Graph.Nodes.Single(n => n.NodeRef == state.CurrentContainer!.NodeRef);
        Assert.Null(unknownNode.SemanticIdentityCandidate);
        Assert.Equal(ContainerNodeLifecycleStage.INITIALIZED, unknownNode.LifecycleStage);
        Assert.Null(agent.Belief!.SemanticPage);

        // Unknown does not invent a normal Graph relation or an execution obligation.
        Assert.Empty(state.Graph.Relations);
        Assert.Equal("Root", agent.ContainerContext.ActiveExecutionContainer);
        Assert.Equal(ContainerTransitionKind.UNKNOWN_TRANSITION, agent.LatestContainerTransition!.Kind);

        // Prior known Graph evidence (Root node) is preserved - no pollution.
        Assert.Contains(state.Graph.Nodes, n => n.SemanticIdentityCandidate == "Root");
    }

    // ── P26-F8 ──────────────────────────────────────────────────────────────
    // coverage + Unknown: COVERAGE_COMPLETE != SEMANTIC_RESOLVED — local inventory
    // (coverage/current existence) can be exhausted while unresolved semantic
    // evidence is retained.
    [Fact]
    public void P26_F8_CoverageCompleteDoesNotEqualSemanticResolved()
    {
        var agent = NewAgent();
        var root = NewContainer("Root");
        SetActiveExecution(agent, root);
        InitializeV2(agent, "Root", 1);

        // Fully consume the local physical inventory (fresh observation accepted,
        // current advances) with an unresolved semantic destination.
        var state = PrepareAndCommit(
            agent,
            Observation(2),
            UnknownBelief(2),
            GetActiveContext(agent),
            Classification("Root", null, 2, "Root") with
            {
                EvidenceRef = "observation:2:coverage-unknown",
            },
            GetActiveContext(agent));

        // The physical observation is covered/current (inventory consumed)...
        Assert.Equal(state.CurrentContainer!.NodeRef, agent.ContainerContext.CurrentNodeRef);
        Assert.True(state.TransitionOccurrences.Length >= 1);

        // ...but the semantic evidence remains UNRESOLVED (no identity candidate).
        var current = state.Graph.Nodes.Single(n => n.NodeRef == state.CurrentContainer!.NodeRef);
        Assert.Null(current.SemanticIdentityCandidate);
        Assert.Equal(ContainerNodeLifecycleStage.INITIALIZED, current.LifecycleStage);
        Assert.Null(agent.Belief!.SemanticPage);
        Assert.Equal(0f, agent.Belief!.Confidence);
    }

    // ── P26-F9 ──────────────────────────────────────────────────────────────
    // stale bounds: LocalModel contains a historical occurrence. Verify:
    // historical bounds cannot dispatch — a fresh CurrentSlice (observation) is
    // required, and reconciliation of a stale frame fails closed.
    [Fact]
    public void P26_F9_StaleLocalModelOccurrenceCannotDispatchAndRequiresFreshSlice()
    {
        var agent = NewAgent();
        var container = NewContainer("Root");
        SetActiveExecution(agent, container);
        InitializeV2(agent, "Root", 2);

        // LocalModel (Container viewport) has already accepted a historical frame seq=2.
        var historical = Observation(2);
        container.Bind(historical);

        ActiveContainerContext preservedContext = GetActiveContext(agent);
        // Attempt to prepare reconciliation from the historical bounds (seq=2) again —
        // a stale frame. Production seam must fail closed (historical != fresh slice).
        var prepared = TryPrepareWithObservationContainer(
            agent,
            Observation(1), // stale, older than the LocalModel frame
            Belief("Root", 1),
            preservedContext,
            Classification("Root", "Root", 1, "Root"),
            agentContainer: container,
            recordViewportObservation: true);

        // P26-F9 Verify: historical bounds rejected; no dispatch / no V2 commit.
        Assert.False(prepared, "stale LocalModel occurrence must not prepare/dispatch (P26-F9)");
        var state = GetV2State(agent);
        Assert.Equal(new SemanticEvidenceRevision(2), state.EvidenceRevision);
        Assert.Single(state.TransitionOccurrences);
        Assert.Null(agent.LatestContainerTransition);
        Assert.Same(historical, container.CurrentObservation);
    }

    // ── P26-F10 ─────────────────────────────────────────────────────────────
    // wrong-child correction: intended C / observed D. Verify: C pending semantics
    // remain representable, OBSERVED != SATISFIED (Slow still Disabled).
    [Fact]
    public void P26_F10_WrongChildObservedNotSatisfiedWithCStillPending()
    {
        var agent = NewAgent();
        var parent = NewContainer("Parent");
        SetActiveExecution(agent, parent);
        InitializeV2(agent, "Parent", 1);

        // Parent ledger: intended child C is approved+authorized but NOT completed (pending).
        var parentProgress = new BranchProgressEvidence(
            "Parent",
            ImmutableDictionary<string, long>.Empty.Add("C", 10),
            ImmutableDictionary<string, long>.Empty,
            ImmutableDictionary<string, long>.Empty.Add("C", 11));
        SetProgress(agent, ImmutableDictionary<string, BranchProgressEvidence>.Empty.Add("Parent", parentProgress));

        // Fresh observed world = unrelated D (off-path), replacing physical current.
        PrepareAndCommit(
            agent,
            Observation(2),
            Belief("D", 2),
            GetActiveContext(agent),
            Classification("Parent", "D", 2, "Parent") with
            {
                EvidenceRef = "observation:2:wrong-child-d",
            },
            GetActiveContext(agent));

        // P26-F10 Verify: OBSERVED (D is physical current) != SATISFIED (C still pending).
        Assert.Equal("D", agent.ContainerContext.CurrentObservedLocation);
        Assert.Equal("D", agent.Belief!.SemanticPage);
        var progress = agent.BranchProgress["Parent"];
        Assert.True(progress.ApprovedSiblingEvidence.ContainsKey("C"), "intended C remains in the approved inventory (representable pending)");
        Assert.True(progress.AuthorizedSiblingEvidence.ContainsKey("C"), "intended C remains an authorized obligation (not withdrawn)");
        Assert.False(progress.CompletedSiblingEvidence.ContainsKey("C"), "intended C is NOT satisfied/completed");

        // Physical observation D is not marked complete either.
        Assert.Equal(ContainerTransitionDisposition.OBSERVED_EXECUTION_PRESERVED, agent.LatestContainerTransition!.Disposition);
        Assert.Equal(ContainerTransitionKind.KNOWN_NON_PARENT_TRANSITION, agent.LatestContainerTransition!.Kind);
        // No correction/completion authority emitted (Slow remains Disabled).
        Assert.All(agent.Trace, entry => Assert.Null(entry.Action));
    }

    // ── P26-F11 ─────────────────────────────────────────────────────────────
    // stale semantic result: an assessment bound to an old revision cannot
    // overwrite a fresh world (stale revision fail-closed).
    [Fact]
    public void P26_F11_StaleRevisionAssessmentCannotOverwriteFreshWorld()
    {
        var agent = NewAgent();
        var home = NewContainer("Home");
        SetActiveExecution(agent, home);
        InitializeV2(agent, "Home", 1);

        // Drive the world to a fresh revision (O23/T23): Home→SettingsRoot, then
        // SettingsRoot same-container continuity (execution obligation stays Home, r5-style).
        var context = GetActiveContext(agent);
        PrepareAndCommit(
            agent,
            Observation(2),
            Belief("SettingsRoot", 2),
            context,
            Classification("Home", "SettingsRoot", 2, "Home") with
            {
                EvidenceRef = "observation:2:advance",
            },
            context);
        PrepareAndCommit(
            agent,
            Observation(3),
            Belief("SettingsRoot", 3),
            context,
            Classification("SettingsRoot", "SettingsRoot", 3, "Home") with
            {
                EvidenceRef = "observation:3:advance",
            },
            context);

        var accepted = GetV2State(agent);
        Assert.Equal(3, accepted.EvidenceRevision.Value);

        // Now apply an assessment/preparation bound to a STALE revision (17 < 23).
        var stale = Observation(17);
        var prepared = TryPrepare(
            agent,
            stale,
            Belief("Stale", 17),
            GetActiveContext(agent),
            Classification("Home", "Stale", 17, "Home") with
            {
                EvidenceRef = "observation:17:stale",
            },
            GetActiveContext(agent));

        // P26-F11 Verify: stale revision fails closed; fresh world is untouched.
        Assert.False(prepared, "stale revision preparation must fail closed (P26-F11)");
        var afterStale = GetV2State(agent);
        Assert.Same(accepted, afterStale);
        Assert.Equal(3, afterStale.EvidenceRevision.Value);
        Assert.Equal(accepted.CurrentContainer!.NodeRef.Value, afterStale.CurrentContainer!.NodeRef.Value);
        Assert.DoesNotContain(afterStale.Graph.Nodes, n => n.SemanticIdentityCandidate == "Stale");
        Assert.Equal("SettingsRoot", agent.Belief!.SemanticPage);
    }

    // ── P26-F12 ─────────────────────────────────────────────────────────────
    // no duplicate authority: validation via reflection that authority counts
    // collapse to exactly one each and zero mutable latest slots.
    [Fact]
    public void P26_F12_NoDuplicateAuthoritySingleOwnersAndZeroMutableLatestSlots()
    {
        // Drive a representative set of scenarios through the production path first.
        var r5 = NewAgent();
        var display = NewContainer("Display");
        SetActiveExecution(r5, display);
        InitializeV2(r5, "Display", 2);
        PrepareAndCommit(
            r5, Observation(3), Belief("SettingsRoot", 3),
            GetActiveContext(r5),
            Classification("Display", "SettingsRoot", 3, "Display") with
            {
                EvidenceRef = "observation:3:p26f12-r5",
            },
            GetActiveContext(r5));

        var unknown = NewAgent();
        var root = NewContainer("Root");
        SetActiveExecution(unknown, root);
        InitializeV2(unknown, "Root", 1);
        PrepareAndCommit(
            unknown, Observation(2), UnknownBelief(2),
            GetActiveContext(unknown),
            Classification("Root", null, 2, "Root") with
            {
                EvidenceRef = "observation:2:p26f12-unknown",
            },
            GetActiveContext(unknown));

        var fields = typeof(RuntimeAgent).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

        // Exactly one physical-current owner (V2 aggregate).
        var v2Fields = fields.Where(f => f.FieldType == typeof(ContainerRuntimeV2State)).ToArray();
        Assert.True(v2Fields.Length == 1, "P26-F12: exactly one ContainerRuntimeV2State field");
        Assert.NotNull(v2Fields[0].GetValue(r5));

        // The superseded _belief owner must not exist.
        Assert.DoesNotContain(fields, f => string.Equals(f.Name, "_belief", StringComparison.Ordinal));

        // Exactly one execution-obligation/path owner.
        var contextFields = fields.Where(f => f.FieldType == typeof(ActiveContainerContext)).ToArray();
        Assert.True(contextFields.Length == 1, "P26-F12: exactly one ActiveContainerContext field");
        Assert.NotNull(contextFields[0].GetValue(r5));

        // Exactly one progress owner.
        var progressFields = fields.Where(f => string.Equals(f.Name, "_branchProgress", StringComparison.Ordinal)).ToArray();
        Assert.True(progressFields.Length == 1, "P26-F12: exactly one _branchProgress owner");

        // Zero mutable latest Fast/Slow/trust/correction/checkpoint slots.
        foreach (var fragment in new[] { "latestFast", "latestSlow", "latestTrust", "latestCorrection", "latestCheckpoint" })
        {
            Assert.False(
                fields.Any(f => f.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)),
                $"P26-F12: no mutable {fragment} slot");
        }
    }

    // ── helpers (production path drive) ─────────────────────────────────────

    private static RuntimeContainer NewContainer(string semanticPage = "Root")
        => new(semanticPage, _ => true, (_, _, _) => new TraversalStepResult.Failed("unused"));

    private static RuntimeAgent NewAgent()
    {
        var environment = new NoopEnvironment();
        Func<Observation, string?> resolver = observation =>
            observation.Elements.IsDefaultOrEmpty ? "Root" : null;
        return new RuntimeAgent(
            new RuntimeStartup(environment, "test.app", resolver),
            new RuntimeTraversal(environment),
            _ => Task.FromResult(Observation(1)),
            resolver,
            _ => NewContainer(),
            new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true));
    }

    private static ContainerTransitionClassificationInput Classification(
        string from,
        string? to,
        long sequence,
        string activeExecution,
        string? activeParent = null)
        => new()
        {
            RunId = "test-run",
            FromObservedLocation = from,
            ToObservedLocation = to,
            ActiveExecutionContainer = activeExecution,
            ActiveParentAtObservation = activeParent,
            FreshObservationRef = $"observation:{sequence}",
        };

    private static ContainerRuntimeV2State GetV2State(RuntimeAgent agent)
        => (ContainerRuntimeV2State)(typeof(RuntimeAgent)
            .GetField("_containerRuntimeV2State", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(agent)
            ?? throw new InvalidOperationException("missing live V2 state"));

    private static ActiveContainerContext GetActiveContext(RuntimeAgent agent)
        => (ActiveContainerContext)(typeof(RuntimeAgent)
            .GetField("_activeContainerContext", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(agent)
            ?? throw new InvalidOperationException("missing active execution context"));

    private static void SetActiveExecution(RuntimeAgent agent, RuntimeContainer container)
        => typeof(RuntimeAgent).GetMethod(
            "ReplaceActiveExecutionContainer", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(agent, [container]);

    private static void InitializeV2(RuntimeAgent agent, string semantic, long sequence)
    {
        var method = typeof(RuntimeAgent).GetMethod(
            "TryInitializeV2Belief", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.True((bool)method.Invoke(agent, [Belief(semantic, sequence)])!);
    }

    private static void SetProgress(RuntimeAgent agent, ImmutableDictionary<string, BranchProgressEvidence> progress)
        => typeof(RuntimeAgent).GetField("_branchProgress", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(agent, progress);

    private static bool TryPrepare(
        RuntimeAgent agent,
        Observation fresh,
        WorldBelief belief,
        ActiveContainerContext currentContext,
        ContainerTransitionClassificationInput input,
        ActiveContainerContext candidateContext)
    {
        var method = typeof(RuntimeAgent).GetMethod(
            "TryPrepareContainerReconciliation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var arguments = new object?[]
        {
            "test-run", fresh, belief, currentContext, input, null, false,
            candidateContext, null, null, ContainerProgressReplacementIntent.None, null, null,
        };
        return (bool)method.Invoke(agent, arguments)!;
    }

    private static bool TryPrepareWithObservationContainer(
        RuntimeAgent agent,
        Observation fresh,
        WorldBelief belief,
        ActiveContainerContext currentContext,
        ContainerTransitionClassificationInput input,
        RuntimeContainer agentContainer,
        bool recordViewportObservation)
    {
        var method = typeof(RuntimeAgent).GetMethod(
            "TryPrepareContainerReconciliation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var arguments = new object?[]
        {
            "test-run", fresh, belief, currentContext, input, agentContainer, recordViewportObservation,
            currentContext, null, null, ContainerProgressReplacementIntent.None, null, null,
        };
        return (bool)method.Invoke(agent, arguments)!;
    }

    private static ContainerRuntimeV2State PrepareAndCommit(
        RuntimeAgent agent,
        Observation fresh,
        WorldBelief preparedBelief,
        ActiveContainerContext currentContext,
        ContainerTransitionClassificationInput input,
        ActiveContainerContext candidateContext,
        string? expectedEnteredChildObligationIdentity = null)
    {
        var method = typeof(RuntimeAgent).GetMethod(
            "TryPrepareContainerReconciliation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var arguments = new object?[]
        {
            "test-run", fresh, preparedBelief, currentContext, input, null, false,
            candidateContext, null, expectedEnteredChildObligationIdentity,
            ContainerProgressReplacementIntent.None, null, null,
        };
        var prepared = (bool)method.Invoke(agent, arguments)!;
        Assert.True(prepared,
            $"production preparation rejected: {arguments[12] ?? "(no reason)"} (P26 prepare)");
        var preparation = arguments[11] ?? throw new InvalidOperationException("missing production preparation");
        var commit = typeof(RuntimeAgent).GetMethod(
            "CommitContainerReconciliation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        commit.Invoke(agent, [preparation, true]);
        return GetV2State(agent);
    }

    private static WorldBelief Belief(string semantic, long sequence)
        => new(
            semantic,
            1f,
            $"语义页面解析为「{semantic}」（观测 seq={sequence}）。",
            sequence);

    private static WorldBelief UnknownBelief(long sequence)
        => new(
            null,
            0f,
            $"语义页面 Unknown：观测（seq={sequence}）无匹配的语义解析规则（§10 证据不足不得假装确定）。",
            sequence);

    private static Observation Observation(long sequence)
        => new(
            ImmutableArray<ObservedElement>.Empty,
            "test.app",
            sequence);

    private sealed class NoopEnvironment : IEnvironment
    {
        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
            => Task.FromResult(Observation(1));

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
            => Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "noop", "noop"));
    }
}
