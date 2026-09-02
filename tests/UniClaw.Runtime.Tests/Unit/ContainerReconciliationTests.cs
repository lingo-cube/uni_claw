using System.Collections.Immutable;
using System.Reflection;
using UniClaw.Runtime.Agent;
using UniClaw.Runtime.Container;
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

namespace UniClaw.Runtime.Tests.Unit;

public sealed class ContainerReconciliationTests
{
    [Fact]
    public void PreparedAcceptanceUpdatesCurrentObservationAndOptionalViewportHistory()
    {
        var first = Observation(1);
        var second = Observation(2);
        var container = NewContainer();
        container.Bind(first);

        container.AcceptPreparedObservation(second, recordViewportHistory: true);

        Assert.Same(second, container.CurrentObservation);
        Assert.Equal([1, 2], container.ViewportExplorationObservations.Select(item => item.SequenceNumber));
    }

    [Fact]
    public void PreparedAcceptanceCanPreserveExistingViewportHistory()
    {
        var first = Observation(1);
        var second = Observation(2);
        var container = NewContainer();
        container.Bind(first);

        container.AcceptPreparedObservation(second, recordViewportHistory: false);

        Assert.Same(second, container.CurrentObservation);
        Assert.Equal([1], container.ViewportExplorationObservations.Select(item => item.SequenceNumber));
    }

    [Fact]
    public void ForgedContextPreparationRejectsWithoutChangingLiveState()
    {
        var agent = NewAgent();
        var currentContainer = NewContainer();
        var forgedContainer = NewContainer();
        var current = Observation(2);
        var fresh = Observation(3);
        currentContainer.Bind(current);
        var context = ActiveContainerContext.Create(currentContainer);
        var forgedContext = ActiveContainerContext.Create(forgedContainer);

        var accepted = InvokePrepare(
            agent,
            fresh,
            context,
            Classification("Root", "Root", fresh.SequenceNumber),
            forgedContext,
            candidateProgress: null);

        Assert.False(accepted);
        Assert.Null(agent.Belief);
        Assert.Empty(agent.Trace);
        Assert.Same(current, currentContainer.CurrentObservation);
    }

    [Fact]
    public void ForgedPathPreparationRejectsWithoutChangingLiveState()
    {
        var agent = NewAgent();
        var parent = NewContainer("Parent");
        var current = NewContainer("Root");
        var currentObservation = Observation(2);
        var fresh = Observation(3);
        current.Bind(currentObservation);
        var context = ActiveContainerContext.Create(parent).EnterChild(current, "authorized-child");
        var forgedPath = ActiveContainerContext.Create(current);

        var accepted = InvokePrepare(
            agent,
            fresh,
            context,
            Classification("Root", "Root", fresh.SequenceNumber, "Root", "Parent"),
            forgedPath,
            candidateProgress: null);

        Assert.False(accepted);
        Assert.Null(agent.Belief);
        Assert.Empty(agent.Trace);
        Assert.Same(currentObservation, current.CurrentObservation);
        Assert.Equal(1, context.ActiveAncestorPath.Length);
    }

    [Fact]
    public void ForgedProgressPreparationRejectsWithoutPartialCommit()
    {
        var agent = NewAgent();
        var container = NewContainer();
        var current = Observation(2);
        var fresh = Observation(3);
        container.Bind(current);
        var context = ActiveContainerContext.Create(container);
        var forgedProgress = ImmutableDictionary<string, BranchProgressEvidence>.Empty;

        var accepted = InvokePrepare(
            agent,
            fresh,
            context,
            Classification("Root", "Root", fresh.SequenceNumber),
            context,
            forgedProgress);

        Assert.False(accepted);
        Assert.Null(agent.Belief);
        Assert.Empty(agent.Trace);
        Assert.Same(current, container.CurrentObservation);
    }

    [Fact]
    public void ProgressReplacementWithoutExactIntentRejectsBeforeCommit()
    {
        var agent = NewAgent();
        var container = NewContainer();
        var current = Observation(2);
        var fresh = Observation(3);
        container.Bind(current);
        var context = ActiveContainerContext.Create(container);
        var candidateProgress = ImmutableDictionary<string, BranchProgressEvidence>.Empty;

        var accepted = InvokePrepare(
            agent,
            fresh,
            context,
            Classification("Root", "Root", fresh.SequenceNumber),
            context,
            candidateProgress,
            progressReplacementIntent: ContainerProgressReplacementIntent.None);

        Assert.False(accepted);
        Assert.Null(agent.Belief);
        Assert.Empty(agent.Trace);
        Assert.Same(current, container.CurrentObservation);
    }

    [Fact]
    public void ProgressReplacementWithForgedBoundaryIntentRejectsBeforeCommit()
    {
        var agent = NewAgent();
        var container = NewContainer();
        var current = Observation(2);
        var fresh = Observation(3);
        container.Bind(current);
        var context = ActiveContainerContext.Create(container);
        var candidateProgress = ImmutableDictionary<string, BranchProgressEvidence>.Empty;

        var accepted = InvokePrepare(
            agent,
            fresh,
            context,
            Classification("Root", "Root", fresh.SequenceNumber) with
            {
                CompletenessRef = "branch-progress:Root:boundary",
                EvidenceRef = "observation:3",
            },
            context,
            candidateProgress,
            progressReplacementIntent: ContainerProgressReplacementIntent.ExternalBoundaryObserved);

        Assert.False(accepted);
        Assert.Null(agent.Belief);
        Assert.Empty(agent.Trace);
        Assert.Same(current, container.CurrentObservation);
    }

    [Fact]
    public void StalePreparationRejectsWithoutChangingContainerObservation()
    {
        var agent = NewAgent();
        var container = NewContainer();
        var current = Observation(2);
        var stale = Observation(1);
        container.Bind(current);
        var context = ActiveContainerContext.Create(container);

        var accepted = InvokePrepare(
            agent,
            stale,
            context,
            Classification("Root", "Root", stale.SequenceNumber),
            context,
            candidateProgress: null);

        Assert.False(accepted);
        Assert.Null(agent.Belief);
        Assert.Empty(agent.Trace);
        Assert.Same(current, container.CurrentObservation);
    }

    [Fact]
    public void ForgedBeliefPreparationRejectsWithoutChangingLiveState()
    {
        var agent = NewAgent();
        var container = NewContainer();
        var current = Observation(2);
        var fresh = Observation(3);
        container.Bind(current);
        var context = ActiveContainerContext.Create(container);
        var forgedBelief = new WorldBelief("Forged", 1f, "forged", fresh.SequenceNumber);

        var accepted = InvokePrepare(
            agent,
            fresh,
            context,
            Classification("Root", "Forged", fresh.SequenceNumber),
            context,
            candidateProgress: null,
            preparedBelief: forgedBelief);

        Assert.False(accepted);
        Assert.Null(agent.Belief);
        Assert.Empty(agent.Trace);
        Assert.Same(current, container.CurrentObservation);
    }

    [Fact]
    public void V2OccurrenceAndLegacyProjectionShareExactFreshEvidenceReferencesThroughCommit()
    {
        var agent = NewAgent();
        var container = NewContainer();
        var child = NewContainer("Child");
        var current = Observation(2);
        var fresh = Observation(3);
        container.Bind(current);
        var context = ActiveContainerContext.Create(container);
        var candidateContext = context.EnterChild(child, "Child");
        var candidateBelief = new WorldBelief(
            "Child",
            1f,
            "语义页面解析为「Child」（观测 seq=3）。",
            fresh.SequenceNumber);
        var initialize = typeof(RuntimeAgent).GetMethod(
            "TryInitializeV2Belief",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.True((bool)initialize.Invoke(
            agent,
            [new WorldBelief(
                "Root",
                1f,
                "语义页面解析为「Root」（观测 seq=2）。",
                current.SequenceNumber)])!);
        var method = typeof(RuntimeAgent).GetMethod(
            "TryPrepareContainerReconciliation",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var arguments = new object?[]
        {
            "test-run", fresh, candidateBelief, context,
            Classification("Root", "Child", fresh.SequenceNumber, "Root", "Root") with
            {
                IsAuthorizedChildEntry = true,
                EvidenceRef = "trigger:menu",
            },
            null, false, candidateContext, null, "Child",
            ContainerProgressReplacementIntent.None, null, null,
        };

        Assert.True((bool)method.Invoke(agent, arguments)!);
        var preparation = arguments[11] ?? throw new InvalidOperationException("missing preparation");
        var transition = preparation.GetType().GetProperty("Transition")?.GetValue(preparation) as ContainerTransition
            ?? throw new InvalidOperationException("missing legacy transition projection");
        var v2State = preparation.GetType().GetProperty("V2State")?.GetValue(preparation) as ContainerRuntimeV2State
            ?? throw new InvalidOperationException("missing V2 state");
        var occurrence = v2State.TransitionOccurrences[^1];

        var commit = typeof(RuntimeAgent).GetMethod(
            "CommitContainerReconciliation",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        commit.Invoke(agent, [preparation, true]);
        var acceptedState = GetV2State(agent);
        var acceptedOccurrence = acceptedState.TransitionOccurrences[^1];
        var traceTransition = Assert.Single(agent.Trace
            .Where(entry => entry.ContainerTransition is not null))
            .ContainerTransition!;

        Assert.Equal(occurrence.OccurrenceRef.Value, transition.TransitionRef);
        Assert.Equal(occurrence.FreshObservationRef, transition.FreshObservationRef);
        Assert.Equal(occurrence.EvidenceRevision.Value, fresh.SequenceNumber);
        Assert.Equal(occurrence.TriggerOccurrenceRef, transition.EvidenceRef);
        Assert.Equal(acceptedOccurrence.OccurrenceRef, new TransitionOccurrenceRef(traceTransition.TransitionRef));
        Assert.Equal(acceptedOccurrence.FreshObservationRef, traceTransition.FreshObservationRef);
        Assert.Equal(acceptedOccurrence.EvidenceRevision.Value, fresh.SequenceNumber);
        Assert.Equal(acceptedOccurrence.SourceNodeRef, v2State.CurrentContainer!.EntryContext!.SourceNodeRef);
        Assert.Equal(acceptedOccurrence.SourceNodeRef, FindNodeRef(v2State, traceTransition.FromObservedLocation));
        Assert.Equal(acceptedOccurrence.DestinationNodeRef, FindNodeRef(v2State, traceTransition.ToObservedLocation));
        Assert.Equal(acceptedOccurrence.TriggerOccurrenceRef, traceTransition.EvidenceRef);
        Assert.Same(acceptedState, GetV2State(agent));
        Assert.Null(typeof(RuntimeAgent).GetField("_belief", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
    }

    [Fact]
    public void UnexpectedObservedLocationReplacesV2CurrentWithoutChangingActiveExecution()
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
            Belief("SettingsRoot", fresh.SequenceNumber),
            context,
            Classification("Display", "SettingsRoot", fresh.SequenceNumber, "Display") with
            {
                EvidenceRef = "observation:3:unexpected-settings",
            },
            context);

        Assert.Equal("SettingsRoot", state.Graph.Nodes.Single(
            node => node.NodeRef == state.CurrentContainer!.NodeRef).SemanticIdentityCandidate);
        Assert.Equal("SettingsRoot", agent.Belief!.SemanticPage);
        Assert.Equal("Display", agent.ContainerContext.ActiveExecutionContainer);
        Assert.Empty(state.Graph.Relations);
        Assert.Equal(ContainerTransitionKind.KNOWN_NON_PARENT_TRANSITION, agent.LatestContainerTransition!.Kind);
    }

    [Fact]
    public void MultiEntrySameDestinationKeepsDistinctRelationsAndVerifiedReturnRestoresParentEntryContext()
    {
        var agent = NewAgent();
        var home = NewContainer("Home");
        var desktop = NewContainer("Desktop");
        var settings = NewContainer("Settings");
        var search = NewContainer("Search");
        SetActiveExecution(agent, home);
        InitializeV2(agent, "Home", 1);

        var desktopContext = GetActiveContext(agent).EnterChild(desktop, "Desktop");
        var desktopState = PrepareAndCommit(
            agent,
            Observation(2),
            Belief("Desktop", 2),
            GetActiveContext(agent),
            Classification("Home", "Desktop", 2, "Home", "Home") with
            {
                IsAuthorizedChildEntry = true,
                EvidenceRef = "entry:desktop",
            },
            desktopContext,
            expectedEnteredChildObligationIdentity: "Desktop");
        var desktopEntryContext = Assert.IsType<ContainerEntryContext>(desktopState.CurrentContainer!.EntryContext);

        var settingsFromDesktopContext = GetActiveContext(agent).EnterChild(
            settings,
            "Settings",
            desktopEntryContext);
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

        var childContext = GetActiveContext(agent);
        Assert.True(childContext.TryReturnToParent(out var resumedDesktopContext, out _));
        var returnedDesktop = PrepareAndCommit(
            agent,
            Observation(4),
            Belief("Desktop", 4),
            childContext,
            Classification("Settings", "Desktop", 4, "Settings", "Desktop") with
            {
                IsVerifiedReturn = true,
                EvidenceRef = "return:settings-desktop",
            },
            resumedDesktopContext!);
        Assert.Equal(desktopEntryContext, returnedDesktop.CurrentContainer!.EntryContext);
        Assert.NotNull(returnedDesktop.CurrentContainer.EntryContext);

        var searchContext = GetActiveContext(agent).EnterChild(
            search,
            "Search",
            desktopEntryContext);
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

        var settingsFromSearchContext = GetActiveContext(agent).EnterChild(
            settings,
            "Settings",
            searchEntryContext);
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

        Assert.Equal(settingsNode, settingsFromSearch.CurrentContainer!.NodeRef);
        Assert.NotEqual(
            settingsDesktopEntry.SourceNodeRef,
            settingsFromSearch.CurrentContainer.EntryContext!.SourceNodeRef);
        var settingsRelations = settingsFromSearch.Graph.Relations
            .Where(relation => relation.DestinationNodeRef == settingsNode)
            .ToArray();
        Assert.Equal(2, settingsRelations.Length);
        Assert.NotEqual(settingsRelations[0].RelationRef, settingsRelations[1].RelationRef);

        var settingsSearchContext = GetActiveContext(agent);
        Assert.True(settingsSearchContext.TryReturnToParent(out var resumedSearchContext, out _));
        var returnedSearch = PrepareAndCommit(
            agent,
            Observation(7),
            Belief("Search", 7),
            settingsSearchContext,
            Classification("Settings", "Search", 7, "Settings", "Search") with
            {
                IsVerifiedReturn = true,
                EvidenceRef = "return:settings-search",
            },
            resumedSearchContext!);
        Assert.Equal(searchEntryContext, returnedSearch.CurrentContainer!.EntryContext);
        Assert.NotNull(returnedSearch.CurrentContainer.EntryContext);
    }

    [Fact]
    public void V2ReplacementChangesOnlyPhysicalCurrentAndCompatibilityTransition()
    {
        var agent = NewAgent();
        var display = NewContainer("Display");
        SetActiveExecution(agent, display);
        InitializeV2(agent, "Display", 2);
        var progressBefore = agent.ProgressSnapshot;
        var traceBefore = agent.Trace.Count;
        var stateBefore = agent.State;
        var reasonBefore = agent.Reason;
        var trapBefore = agent.LastTrap;

        var stateAfter = PrepareAndCommit(
            agent,
            Observation(3),
            Belief("SettingsRoot", 3),
            GetActiveContext(agent),
            Classification("Display", "SettingsRoot", 3, "Display") with
            {
                EvidenceRef = "observation:3:replacement-authority",
            },
            GetActiveContext(agent));

        Assert.Equal(
            "SettingsRoot",
            stateAfter.Graph.Nodes.Single(node => node.NodeRef == stateAfter.CurrentContainer!.NodeRef)
                .SemanticIdentityCandidate);
        Assert.Same(progressBefore, agent.ProgressSnapshot);
        Assert.Equal(traceBefore + 1, agent.Trace.Count);
        Assert.Equal(stateBefore, agent.State);
        Assert.Equal(reasonBefore, agent.Reason);
        Assert.Same(trapBefore, agent.LastTrap);
        Assert.All(agent.Trace, entry =>
        {
            Assert.Null(entry.Action);
            Assert.Null(entry.ActionId);
            Assert.Null(entry.RecoveryId);
        });
        Assert.Equal(ContainerTransitionKind.KNOWN_NON_PARENT_TRANSITION, agent.LatestContainerTransition!.Kind);
    }

    private static ContainerRuntimeV2State GetV2State(RuntimeAgent agent)
        => (ContainerRuntimeV2State)(typeof(RuntimeAgent)
            .GetField("_containerRuntimeV2State", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(agent)
            ?? throw new InvalidOperationException("missing live V2 state"));

    private static ContainerNodeRef FindNodeRef(ContainerRuntimeV2State state, string? semantic)
        => state.Graph.Nodes.Single(node => node.SemanticIdentityCandidate == semantic).NodeRef;

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
        string to,
        long sequence,
        string activeExecution = "Root",
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

    private static bool InvokePrepare(
        RuntimeAgent agent,
        Observation fresh,
        ActiveContainerContext context,
        ContainerTransitionClassificationInput input,
        ActiveContainerContext candidateContext,
        ImmutableDictionary<string, BranchProgressEvidence>? candidateProgress,
        WorldBelief? preparedBelief = null,
        ContainerProgressReplacementIntent progressReplacementIntent = ContainerProgressReplacementIntent.None)
    {
        var method = typeof(RuntimeAgent).GetMethod(
            "TryPrepareContainerReconciliation",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var arguments = new object?[]
        {
            "test-run", fresh, preparedBelief, context, input, null, false, candidateContext,
            candidateProgress, null, progressReplacementIntent, null, null,
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
            "TryPrepareContainerReconciliation",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var arguments = new object?[]
        {
            "test-run", fresh, preparedBelief, currentContext, input, null, false,
            candidateContext, null, expectedEnteredChildObligationIdentity,
            ContainerProgressReplacementIntent.None, null, null,
        };
        Assert.True((bool)method.Invoke(agent, arguments)!, "production preparation rejected");
        var preparation = arguments[11] ?? throw new InvalidOperationException("missing production preparation");
        var commit = typeof(RuntimeAgent).GetMethod(
            "CommitContainerReconciliation",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        commit.Invoke(agent, [preparation, true]);
        return GetV2State(agent);
    }

    private static void SetActiveExecution(RuntimeAgent agent, RuntimeContainer container)
        => typeof(RuntimeAgent).GetMethod(
            "ReplaceActiveExecutionContainer",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(agent, [container]);

    private static void InitializeV2(RuntimeAgent agent, string semantic, long sequence)
    {
        var method = typeof(RuntimeAgent).GetMethod(
            "TryInitializeV2Belief",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.True((bool)method.Invoke(agent, [Belief(semantic, sequence)])!);
    }

    private static ActiveContainerContext GetActiveContext(RuntimeAgent agent)
        => (ActiveContainerContext)(typeof(RuntimeAgent)
            .GetField("_activeContainerContext", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(agent)
            ?? throw new InvalidOperationException("missing active execution context"));

    private static WorldBelief Belief(string semantic, long sequence)
        => new(
            semantic,
            1f,
            $"语义页面解析为「{semantic}」（观测 seq={sequence}）。",
            sequence);

    private sealed class NoopEnvironment : IEnvironment
    {
        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
            => Task.FromResult(Observation(1));

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
            => Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "noop", "noop"));
    }

    private static Observation Observation(long sequence)
        => new(
            ImmutableArray<ObservedElement>.Empty,
            "test.app",
            sequence);
}
