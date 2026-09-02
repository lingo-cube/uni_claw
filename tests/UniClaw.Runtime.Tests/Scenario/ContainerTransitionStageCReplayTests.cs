using System.Collections.Immutable;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.World;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>Stage C contract evidence for honest observed-location reconciliation.</summary>
public sealed class ContainerTransitionStageCReplayTests
{
    [Fact]
    public void PrematureParentObservationPreservesExecutionObligation()
    {
        var preparation = ContainerTransitionClassifier.Prepare(new ContainerTransitionClassificationInput
        {
            RunId = "r5",
            FromObservedLocation = "Display",
            ToObservedLocation = "SettingsRoot",
            ActiveExecutionContainer = "Display",
            ActiveParentAtObservation = "SettingsRoot",
            FreshObservationRef = "observation:28",
            CompletenessRef = "container:Display:local-completeness",
            EvidenceRef = "observation:28",
        });

        Assert.True(preparation.CanCommit);
        Assert.Equal(ContainerTransitionKind.PREMATURE_RETURN_TO_ACTIVE_PARENT, preparation.Transition.Kind);
        Assert.Equal(ContainerTransitionDisposition.OBSERVED_EXECUTION_PRESERVED, preparation.Transition.Disposition);
        Assert.Equal("SettingsRoot", preparation.Transition.ToObservedLocation);
        Assert.Equal("Display", preparation.Transition.ActiveExecutionContainer);
    }

    [Fact]
    public void AcceptedUnknownRemainsUnknownAndDoesNotAuthorizeAction()
    {
        var preparation = ContainerTransitionClassifier.Prepare(new ContainerTransitionClassificationInput
        {
            RunId = "unknown-run",
            FromObservedLocation = "Display",
            ToObservedLocation = null,
            ActiveExecutionContainer = "Display",
            ActiveParentAtObservation = "SettingsRoot",
            FreshObservationRef = "observation:29",
        });

        Assert.True(preparation.CanCommit);
        Assert.Equal(ContainerTransitionKind.UNKNOWN_TRANSITION, preparation.Transition.Kind);
        Assert.Equal(ContainerTransitionDisposition.OBSERVED_EXECUTION_PRESERVED, preparation.Transition.Disposition);
        Assert.Null(preparation.Transition.ToObservedLocation);
    }

    [Fact]
    public async Task KnownNonParentOpenWorldDepartureAcceptsFreshBeliefAndPreservesRootExecution()
    {
        var world = new ScriptedEnvironment(
            "Launcher", "Root",
            [
                new ScreenConfig(
                    "Launcher", "test.app",
                    [new ElementConfig("Launcher", null, null, Bounds(0), "text")]),
                new ScreenConfig(
                    "Root", "test.app",
                    [new ElementConfig("Root", null, null, Bounds(0), "text")],
                    new ViewportTransitionConfig("Other")),
                new ScreenConfig(
                    "Other", "test.app",
                    [new ElementConfig("Other", null, null, Bounds(0), "text")]),
            ]);
        var environment = new SemanticCapabilityTestEnvironment(
            world, (_, _, _) => FixtureSemanticRole.NonInteractive);
        var agent = BuildAgent(environment, ResolveRootOrOther);
        var goal = new Goal(
            _ => new GoalEvidence(false, "known non-parent departure remains incomplete", 0),
            CandidateAuthorizationEvaluator: (_, _) => new(false, "no branch authorization"),
            ViewportExplorationEvaluator: observations =>
                new ViewportExplorationEvidence(true, "departure fixture requires one bounded viewport action"),
            BranchInventoryEvaluator: (_, _) => new BranchInventoryEvidence(
                ImmutableDictionary<string, long>.Empty, "bounded root inventory"));
        var envelope = IntentSemanticEnvelope.Project(
            "known non-parent departure replay", goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(
                new TypeLevelTraversalSpecification(
                    new TypeLevelTaskScope("test.app", "Root"),
                    ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
                    1,
                    new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
                    TypeLevelCompletionRequirement.ExhaustiveWithinScope,
                    new TypeLevelEntryBoundary("test.app", "Root"))));

        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "stage-c-known-non-parent", CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.Equal("Other", agent.Belief?.SemanticPage);
        Assert.True(agent.Belief?.SourceObservationSequence > 2);
        Assert.Equal("Root", agent.ContainerContext.ActiveExecutionContainer);
        Assert.Empty(agent.ContainerContext.ActiveAncestorPath);
        var transition = Assert.Single(agent.ContainerTransitions.Where(item =>
            item.Kind == ContainerTransitionKind.KNOWN_NON_PARENT_TRANSITION));
        Assert.Equal("Other", transition.ToObservedLocation);
        Assert.Equal(ContainerTransitionDisposition.OBSERVED_EXECUTION_PRESERVED, transition.Disposition);
        Assert.Null(agent.LastTrap);
        Assert.Single(world.ActionHistory.OfType<DeviceAction.LaunchApp>());
        Assert.Single(world.ActionHistory.OfType<DeviceAction.ScrollForward>());
    }

    [Fact]
    public async Task ExternalBoundaryOpenWorldRunKeepsBoundaryOwnerAndAuthorizesOnlyExistingBack()
    {
        var world = new BoundaryWorld();
        var environment = new SemanticCapabilityTestEnvironment(
            world,
            (_, element, _) => element.Text is "Location" or "App location permissions"
                ? FixtureSemanticRole.NavigationCandidate
                : FixtureSemanticRole.NonInteractive);
        var agent = BuildAgent(environment, ResolveRootOrLocation);
        var goal = new Goal(
            _ => new GoalEvidence(false, "boundary replay is evidence-only", 0),
            CandidateAuthorizationEvaluator: (observation, candidate) =>
                candidate.Text == "Location"
                    ? new(true, "authorized child", AuthorizationKind.AuthorizedChild)
                    : candidate.Text == "App location permissions"
                        ? new(true, "authorized existing boundary", AuthorizationKind.AuthorizedBoundary)
                        : new(false, "not authorized"),
            ViewportExplorationEvaluator: observations =>
                new ViewportExplorationEvidence(
                    observations.Length == 1,
                    observations.Length == 1 ? "bounded first viewport" : "viewport exhausted"),
            BranchInventoryEvaluator: (observations, depth) => InventoryFor(
                observations, depth == 0 ? "Location" : "App location permissions"));
        var envelope = IntentSemanticEnvelope.Project(
            "external boundary replay", goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(
                new TypeLevelTraversalSpecification(
                    new TypeLevelTaskScope("test.app", "Root"),
                    ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
                    2,
                    new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
                    TypeLevelCompletionRequirement.ExhaustiveWithinScope,
                    new TypeLevelEntryBoundary("test.app", "Root"))));

        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "stage-c-external", CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        var externalTransitions = agent.ContainerTransitions.Where(item =>
            item.Kind == ContainerTransitionKind.EXTERNAL_EXIT).ToArray();
        Assert.True(externalTransitions.Length == 1,
            string.Join(" | ", agent.Trace.Select(item => item.Reason ?? item.ContainerId ?? "<lifecycle>")));
        var transition = externalTransitions[0];
        Assert.Equal("external.permission", transition.ToObservedLocation);
        Assert.Equal("Location", transition.ActiveExecutionContainer);
        Assert.Equal(ContainerTransitionDisposition.OBSERVED_EXECUTION_PRESERVED, transition.Disposition);
        Assert.StartsWith("observation:", transition.FreshObservationRef);
        Assert.Equal("Location", agent.Belief?.SemanticPage);
        Assert.Equal("Location", agent.ContainerContext.ActiveExecutionContainer);
        Assert.Equal(["Root"], agent.ContainerContext.ActiveAncestorPath);
        Assert.Single(world.ActionHistory.OfType<DeviceAction.SystemBack>());
        var traceEntries = agent.Trace.ToArray();
        var transitionIndex = Array.FindIndex(traceEntries, item => item.ContainerTransition == transition);
        var systemBackIndex = Array.FindIndex(traceEntries, item => item.Action is DeviceAction.SystemBack);
        Assert.True(transitionIndex >= 0 && systemBackIndex > transitionIndex,
            "External boundary transition must be committed before the existing SystemBack dispatch.");
        Assert.Contains(agent.Trace, item => item.Reason?.Contains("EXTERNAL_BOUNDARY_OBSERVED", StringComparison.Ordinal) is true);
        Assert.Contains(agent.Trace, item => item.Reason?.Contains("EXTERNAL_BOUNDARY_RETURNED_TO_PARENT", StringComparison.Ordinal) is true);
        Assert.Single(agent.ProgressSnapshot["Location"].VerifiedBoundaryDispositions);
        Assert.Null(agent.LastTrap);
    }

    [Fact]
    public async Task PlanRunAcceptsUnknownFreshBeliefWithoutRecoveryOrAdditionalAction()
    {
        var world = new ScriptedEnvironment(
            "Launcher", "Root",
            [
                new ScreenConfig("Launcher", "test.app",
                    [new ElementConfig("Launcher", null, null, Bounds(0), "text")]),
                new ScreenConfig("Root", "test.app",
                    [new ElementConfig("Root", null,
                        new TransitionConfig(ScreenTransitionAction.Tap, "Unknown"), Bounds(0), "navigation")]),
                new ScreenConfig("Unknown", "test.app",
                    [new ElementConfig("unresolved", null, null, Bounds(0), "text")]),
            ]);
        var traversal = new RuntimeTraversal(world);
        var resolve = (Observation observation) =>
            observation.Elements.Any(item => item.Text == "Root") ? "Root" : null;
        var startup = new RuntimeStartup(world, "test.app", resolve);
        var recovery = new RuntimeRecovery(world, _ => [], (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(
            startup, traversal, world.ObserveAsync, resolve,
            page => new RuntimeContainer(page, _ => true, traversal.ExecuteStep), recovery);
        var goal = new Goal(_ => new GoalEvidence(false, "Unknown remains unsatisfied", 0));

        var state = await agent.RunAsync(
            goal,
            new Plan([new PlanStep("Root", "Tap")]),
            "stage-c-unknown",
            CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.Null(agent.Belief?.SemanticPage);
        Assert.Equal(3, agent.Belief?.SourceObservationSequence);
        Assert.Equal("Root", agent.ContainerContext.ActiveExecutionContainer);
        Assert.Empty(agent.ContainerContext.ActiveAncestorPath);
        Assert.Contains(agent.ContainerTransitions, item => item.Kind == ContainerTransitionKind.UNKNOWN_TRANSITION);
        Assert.Null(agent.LastTrap);
        Assert.Equal(2, world.ActionHistory.Count);
        Assert.Single(world.ActionHistory.OfType<DeviceAction.Tap>());
    }

    [Fact]
    public async Task PlanRunOrdinarySameContainerUsesOneCausalSameTransition()
    {
        var world = new ScriptedEnvironment(
            "Launcher", "Root",
            [
                new ScreenConfig("Launcher", "test.app",
                    [new ElementConfig("Launcher", null, null, Bounds(0), "text")]),
                new ScreenConfig("Root", "test.app",
                    [new ElementConfig(
                        "Root", null,
                        new TransitionConfig(ScreenTransitionAction.Tap, "Root"),
                        Bounds(0), "navigation")]),
            ]);
        var environment = new SemanticCapabilityTestEnvironment(
            world, (_, _, _) => FixtureSemanticRole.NonInteractive);
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(environment, "test.app", ResolveRootOrOther);
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        var containers = new List<RuntimeContainer>();
        RuntimeContainer CreateContainer(string page)
        {
            var container = new RuntimeContainer(page, observation => ResolveRootOrOther(observation) == page, traversal.ExecuteStep);
            containers.Add(container);
            return container;
        }
        var agent = new RuntimeAgent(
            startup, traversal, environment.ObserveAsync, ResolveRootOrOther, CreateContainer, recovery);
        var state = await agent.RunAsync(
            new Goal(_ => new GoalEvidence(false, "ordinary same remains incomplete", 0)),
            new Plan([new PlanStep("Root", "Tap")]),
            "stage-c-ordinary-same",
            CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        var step = Assert.Single(agent.Trace.Where(item => item.StepId == "Step-1"));
        Assert.Equal(ContainerTransitionKind.SAME_CONTAINER, step.ContainerTransition?.Kind);
        Assert.Single(agent.Trace.Where(item => item.ContainerTransition is not null));
        Assert.Equal(3, containers[0].CurrentObservation?.SequenceNumber);
        Assert.Single(world.ActionHistory.OfType<DeviceAction.Tap>());
    }

    [Fact]
    public async Task PlanRunIdentityConflictAcceptsBeliefAndTransitionButRebindsOldContainer()
    {
        var world = new ScriptedEnvironment(
            "Launcher", "Root",
            [
                new ScreenConfig("Launcher", "test.app",
                    [new ElementConfig("Launcher", null, null, Bounds(0), "text")]),
                new ScreenConfig("Root", "test.app",
                    [new ElementConfig(
                        "Root", null,
                        new TransitionConfig(ScreenTransitionAction.Tap, "Root"),
                        Bounds(0), "navigation")]),
            ]);
        var traversal = new RuntimeTraversal(world);
        var startup = new RuntimeStartup(world, "test.app", ResolveRootOrOther);
        var recovery = new RuntimeRecovery(world, _ => [], (_, _) => null, (_, _) => true);
        var containers = new List<RuntimeContainer>();
        RuntimeContainer CreateContainer(string page)
        {
            // Deliberately reject the post-action frame while retaining the
            // same semantic resolver result: this is an identity conflict,
            // not permission to mutate the old Container.
            var container = new RuntimeContainer(page, _ => false, traversal.ExecuteStep);
            containers.Add(container);
            return container;
        }
        var agent = new RuntimeAgent(
            startup, traversal, world.ObserveAsync, ResolveRootOrOther,
            CreateContainer, recovery);
        var state = await agent.RunAsync(
            new Goal(_ => new GoalEvidence(false, "identity conflict remains incomplete", 0)),
            new Plan([new PlanStep("Root", "Tap")]),
            "stage-c-identity-conflict",
            CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.Equal("Root", agent.Belief?.SemanticPage);
        var step = Assert.Single(agent.Trace.Where(item => item.StepId == "Step-1"));
        Assert.Equal(ContainerTransitionKind.SAME_CONTAINER, step.ContainerTransition?.Kind);
        Assert.Single(agent.Trace.Where(item => item.ContainerTransition is not null));
        Assert.Equal(2, containers.Count);
        Assert.Equal(2, containers[0].CurrentObservation?.SequenceNumber);
        Assert.Equal(3, containers[1].CurrentObservation?.SequenceNumber);
        Assert.Single(world.ActionHistory.OfType<DeviceAction.Tap>());
    }

    [Fact]
    public async Task PlanRunLocalHandlingSameContainerUsesOneCausalSameTransition()
    {
        var world = ScriptedEnvironmentVariants.PopupRuntimeContinuous();
        var traversal = new RuntimeTraversal(world);
        var startup = new RuntimeStartup(world, "Settings", ScenarioIdentity.ResolveSemanticPage);
        var recovery = new RuntimeRecovery(world, _ => [], (_, _) => null, (_, _) => true);
        var containers = new List<RuntimeContainer>();
        RuntimeContainer CreateContainer(string page)
        {
            var container = new RuntimeContainer(page, ScenarioIdentity.IdentityRule(page), traversal.ExecuteStep);
            containers.Add(container);
            return container;
        }
        var agent = new RuntimeAgent(
            startup, traversal, world.ObserveAsync, ScenarioIdentity.ResolveSemanticPage,
            CreateContainer, recovery);
        var state = await agent.RunAsync(
            new Goal(observation => new GoalEvidence(
                observation.SequenceNumber >= 4
                && ScenarioIdentity.ResolveSemanticPage(observation) == "NetworkSettings",
                "local handling result", observation.SequenceNumber)),
            new Plan([new PlanStep("WiFi", "Tap"), new PlanStep("Dismiss", "Tap")]),
            "stage-c-local-same",
            CancellationToken.None);

        Assert.Equal(RunState.Completed, state);
        var step = Assert.Single(agent.Trace.Where(item => item.StepId == "Step-2"));
        Assert.Equal(ContainerTransitionKind.SAME_CONTAINER, step.ContainerTransition?.Kind);
        Assert.Single(agent.Trace.Where(item => item.ContainerTransition?.Kind == ContainerTransitionKind.SAME_CONTAINER));
        Assert.Equal(4, containers[0].CurrentObservation?.SequenceNumber);
    }

    [Fact]
    public async Task PlanRunViewportSameContainerUsesOneCausalSameTransitionAndOneHistoryAppend()
    {
        var world = ScriptedEnvironmentVariants.ViewportContinuous();
        var traversal = new RuntimeTraversal(world);
        var resolve = (Observation observation) =>
            observation.Elements.Any(item => item.Text is "A" or "B" or "C" or "D" or "E" or "F")
                ? "ScrollableList"
                : observation.Elements.Any(item => item.Text == "Other semantic page")
                    ? "OtherPage"
                    : null;
        var startup = new RuntimeStartup(world, "Settings", resolve);
        var recovery = new RuntimeRecovery(world, _ => [], (_, _) => null, (_, _) => true);
        var containers = new List<RuntimeContainer>();
        RuntimeContainer CreateContainer(string page)
        {
            var container = new RuntimeContainer(page, observation => resolve(observation) == page, traversal.ExecuteStep);
            containers.Add(container);
            return container;
        }
        var agent = new RuntimeAgent(startup, traversal, world.ObserveAsync, resolve, CreateContainer, recovery);
        var state = await agent.RunAsync(
            new Goal(observation => new GoalEvidence(
                observation.SequenceNumber >= 4 && resolve(observation) == "ScrollableList"
                    && observation.Elements.Any(item => item.Text == "D"),
                "viewport result", observation.SequenceNumber)),
            new Plan([new PlanStep("A", "Tap"), new PlanStep("Viewport", "ScrollForward")]),
            "stage-c-viewport-same",
            CancellationToken.None);

        Assert.Equal(RunState.Completed, state);
        var step = Assert.Single(agent.Trace.Where(item => item.Action is DeviceAction.ScrollForward));
        Assert.Equal(ContainerTransitionKind.SAME_CONTAINER, step.ContainerTransition?.Kind);
        Assert.Equal(1, agent.Trace.Count(item => item.StepId == step.StepId));
        Assert.Equal(2, agent.Trace.Count(item => item.ContainerTransition?.Kind == ContainerTransitionKind.SAME_CONTAINER));
        Assert.Equal([2L, 4L], containers[0].ViewportExplorationObservations.Select(item => item.SequenceNumber));
    }

    private static RuntimeAgent BuildAgent(IEnvironment environment, Func<Observation, string?> resolve)
    {
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(environment, "test.app", resolve);
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        return new RuntimeAgent(
            startup, traversal, environment.ObserveAsync, resolve,
            page => new RuntimeContainer(page, observation => resolve(observation) == page, traversal.ExecuteStep),
            recovery);
    }

    private static BranchInventoryEvidence InventoryFor(ImmutableArray<Observation> observations, string text)
    {
        var occurrence = observations
            .SelectMany(observation => SourceEquivalenceNormalizer.OccurrencesOf(observation)
                .Select(item => (observation, item)))
            .First(item => item.observation.Elements.Any(element => element.Text == text));
        var source = occurrence.item;
        return new BranchInventoryEvidence(
            ImmutableDictionary<string, long>.Empty.Add(text, source.ObservationSequence),
            "deterministic bounded inventory",
            ImmutableDictionary<string, NavigationSourceOccurrenceReference>.Empty.Add(
                text,
                new NavigationSourceOccurrenceReference(source.ObservationSequence, source.OccurrenceIdentity)));
    }

    private static string? ResolveRootOrOther(Observation observation)
        => observation.Elements.Any(item => item.Text == "Root")
            ? "Root"
            : observation.Elements.Any(item => item.Text == "Other")
                ? "Other"
                : null;

    private static string? ResolveRootOrLocation(Observation observation)
        => observation.Elements.Any(item => item.Text == "Location")
            ? "Root"
            : observation.Elements.Any(item => item.Text == "ChildPage")
                ? "Location"
                : null;

    private static ElementBounds Bounds(int index)
        => new(0f, index * 0.1f, 1f, index * 0.1f + 0.08f);

    private sealed class BoundaryWorld : IEnvironment
    {
        private string _screen = "Launcher";
        private long _sequence;
        private readonly List<DeviceAction> _actions = [];

        public IReadOnlyList<DeviceAction> ActionHistory => _actions;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sequence = ++_sequence;
            var (foreground, elements) = _screen switch
            {
                "Root" => ("test.app", new[] { Element("Location", 0, "navigation") }),
                "Location" => ("test.app", new[]
                {
                    Element("ChildPage", 0, "text"),
                    Element("App location permissions", 1, "navigation"),
                }),
                "External" => ("external.permission", new[] { Element("Permission", 0, "text") }),
                _ => ("test.app", new[] { Element("Launcher", 0, "text") }),
            };
            return Task.FromResult(new Observation(elements.ToImmutableArray(), foreground, sequence));
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _actions.Add(action);
            switch (action)
            {
                case DeviceAction.LaunchApp:
                    _screen = "Root";
                    break;
                case DeviceAction.Tap when _screen == "Root":
                    _screen = "Location";
                    break;
                case DeviceAction.Tap when _screen == "Location":
                    _screen = "External";
                    break;
                case DeviceAction.SystemBack:
                    _screen = "Location";
                    break;
            }
            return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "stage-c boundary action", "accepted"));
        }

        private static ObservedElement Element(string text, int index, string perceptionType)
            => new(text, null, index, Bounds(index), perceptionType);
    }
}
