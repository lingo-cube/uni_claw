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

/// <summary>Stage A replay evidence: typed transition is diagnostic only.</summary>
public sealed class ContainerTransitionStageAReplayTests
{
    private const string App = "r5.fixture";
    private const string Root = "SettingsRoot";
    private const string Child = "Display";
    private const string ChildEntry = "Open Display";

    private sealed class R5World : IEnvironment
    {
        private enum Screen { Launcher, Root, Child, ParentObserved }

        private readonly List<DeviceAction> _actions = [];
        private Screen _screen = Screen.Launcher;
        private long _sequence;

        public IReadOnlyList<DeviceAction> ActionHistory => _actions;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sequence = ++_sequence;
            return Task.FromResult(_screen switch
            {
                Screen.Launcher => Observation(sequence, ("Launcher", "text")),
                Screen.Root => Observation(sequence, (Root, "title"), (ChildEntry, "navigation")),
                Screen.Child => Observation(sequence, (Child, "title"), ("Incomplete child row", "navigation")),
                Screen.ParentObserved => Observation(sequence, (Root, "title"), ("Incomplete child row", "navigation")),
                _ => throw new InvalidOperationException("Unknown fixture screen."),
            });
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _actions.Add(action);
            switch (action)
            {
                case DeviceAction.LaunchApp:
                    _screen = Screen.Root;
                    break;
                case DeviceAction.Tap when _screen == Screen.Root:
                    _screen = Screen.Child;
                    break;
                case DeviceAction.ScrollForward when _screen == Screen.Child:
                    _screen = Screen.ParentObserved;
                    break;
                default:
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Rejected, "unsupported", "fixture"));
            }
            return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "fixture action", "ok"));
        }

        private static Observation Observation(long sequence, params (string Text, string PerceptionType)[] elements)
            => new(
                elements.Select((item, index) => new ObservedElement(
                    item.Text,
                    null,
                    index,
                    new ElementBounds(0f, index * 0.1f, 1f, index * 0.1f + 0.08f),
                    item.PerceptionType)).ToImmutableArray(),
                App,
                sequence);
    }

    private static (RuntimeAgent Agent, R5World World, IntentSemanticEnvelope.Resolved Envelope) BuildR5()
    {
        var world = new R5World();
        var environment = new SemanticCapabilityTestEnvironment(world, (observation, element, _) =>
        {
            if (string.Equals(element.Text, ChildEntry, StringComparison.Ordinal)
                && observation.Elements.Any(e => string.Equals(e.Text, Root, StringComparison.Ordinal)))
                return FixtureSemanticRole.NavigationCandidate;
            if (string.Equals(element.Text, "Incomplete child row", StringComparison.Ordinal))
                return FixtureSemanticRole.NavigationCandidate;
            return FixtureSemanticRole.NonInteractive;
        });
        var traversal = new RuntimeTraversal(environment);
        string? page(Observation observation)
            => observation.Elements.Any(e => string.Equals(e.Text, Child, StringComparison.Ordinal))
                ? Child
                : observation.Elements.Any(e => string.Equals(e.Text, Root, StringComparison.Ordinal))
                    ? Root
                    : null;

        var goal = new Goal(
            EvidenceEvaluator: observation => new GoalEvidence(false, "r5 remains incomplete", observation.SequenceNumber),
            CandidateAuthorizationEvaluator: (_, candidate) => new CandidateAuthorizationEvidence(
                string.Equals(candidate.Text, ChildEntry, StringComparison.Ordinal),
                "only the root child entry is authorized"),
            ViewportExplorationEvaluator: observations =>
            {
                if (observations.IsDefaultOrEmpty)
                    return new ViewportExplorationEvidence(false, "root inventory is already bounded");
                return observations[^1].Elements.Any(e => string.Equals(e.Text, "Incomplete child row", StringComparison.Ordinal))
                    ? new ViewportExplorationEvidence(true, "child inventory is incomplete")
                    : new ViewportExplorationEvidence(false, "root inventory is already bounded");
            },
            BranchInventoryEvaluator: (observations, _) =>
            {
                var source = observations
                    .SelectMany(observation => SourceEquivalenceNormalizer.OccurrencesOf(observation)
                        .Select(occurrence => (observation, occurrence)))
                    .FirstOrDefault(item =>
                    {
                        var index = item.occurrence.CanonicalOccurrence.Reference.ElementIndex;
                        return index >= 0
                            && index < item.observation.Elements.Length
                            && string.Equals(item.observation.Elements[index].Text, ChildEntry, StringComparison.Ordinal);
                    });
                return source.occurrence is null
                    ? new BranchInventoryEvidence(null, "root inventory unavailable")
                    : new BranchInventoryEvidence(
                        ImmutableDictionary<string, long>.Empty.Add(ChildEntry, source.observation.SequenceNumber),
                        "root inventory is bounded",
                        ImmutableDictionary<string, NavigationSourceOccurrenceReference>.Empty.Add(
                            ChildEntry,
                            new NavigationSourceOccurrenceReference(
                                source.occurrence.ObservationSequence,
                                source.occurrence.OccurrenceIdentity)));
            });
        var specification = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, Root),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: 1,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, Root));
        var envelope = IntentSemanticEnvelope.Project(
            "r5 incomplete child replay", goal, new IntentExecutionRepresentation.OpenWorldTypeLevel(specification));
        var startup = new RuntimeStartup(environment, App, page);
        var recovery = new RuntimeRecovery(environment, _ => ImmutableArray<DeviceAction>.Empty, (_, _) => null, (_, _) => true);
        RuntimeContainer factory(string pageName) => new(pageName, observation => page(observation) == pageName, traversal.ExecuteStep);
        var agent = new RuntimeAgent(startup, traversal, token => environment.ObserveAsync(token), page, factory, recovery);
        return (agent, world, envelope);
    }

    [Fact]
    public async Task R5AgentReplay_KeepsDisplayExecutionAndFailsClosedOnKnownParentDeparture()
    {
        var (agent, world, envelope) = BuildR5();

        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "r5-agent", CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.True(
            string.Equals(Root, agent.Belief?.SemanticPage, StringComparison.Ordinal),
            string.Join(" | ", agent.Trace.Select(entry => entry.Reason ?? entry.ContainerId ?? "<lifecycle>")));
        var transition = Assert.Single(agent.ContainerTransitions.Where(item => item.Kind == ContainerTransitionKind.PREMATURE_RETURN_TO_ACTIVE_PARENT));
        Assert.Equal(Root, transition.ToObservedLocation);
        Assert.Equal(Child, transition.ActiveExecutionContainer);
        Assert.Equal(Root, transition.ActiveParentAtObservation);
        Assert.Equal(ContainerTransitionDisposition.OBSERVED_EXECUTION_PRESERVED, transition.Disposition);
        Assert.Equal("container:Display:local-completeness", transition.CompletenessRef);
        Assert.True(transition.IsAssetMissing);
        Assert.Contains(agent.Trace, entry => entry.ContainerTransition == transition);
        Assert.Contains(agent.Trace, entry =>
            entry.ContainerId == Child
            && entry.Reason?.Contains("child inventory is incomplete", StringComparison.Ordinal) is true);
        Assert.DoesNotContain(agent.Trace, entry => entry.Reason?.Contains("verified parent return", StringComparison.Ordinal) is true);
        Assert.DoesNotContain(world.ActionHistory, action => action is DeviceAction.SystemBack);
        Assert.Equal(1, world.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.Equal(1, world.ActionHistory.OfType<DeviceAction.ScrollForward>().Count());
        Assert.Single(agent.ContainerTransitions.Where(item => item.Kind == ContainerTransitionKind.ENTER_CHILD));
    }

    [Fact]
    public void R5Replay_ObservedParentAndIncompleteDisplayRemainDistinct()
    {
        var transition = ContainerTransitionClassifier.Classify(new ContainerTransitionClassificationInput
        {
            RunId = "r5",
            FromObservedLocation = "Display",
            ToObservedLocation = "SettingsRoot",
            ActiveExecutionContainer = "Display",
            ActiveParentAtObservation = "SettingsRoot",
            CompletenessRef = "container:Display:incomplete",
            FreshObservationRef = "observation:28",
        });

        Assert.Equal(ContainerTransitionKind.PREMATURE_RETURN_TO_ACTIVE_PARENT, transition.Kind);
        Assert.Equal(ContainerTransitionDisposition.OBSERVED_EXECUTION_PRESERVED, transition.Disposition);
        Assert.Equal("SettingsRoot", transition.ToObservedLocation);
        Assert.Equal("Display", transition.ActiveExecutionContainer);
        Assert.Equal("container:Display:incomplete", transition.CompletenessRef);
        Assert.DoesNotContain("recover", transition.Kind.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("complete", transition.Kind.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OlderTraceWithoutStructuredEvent_IsUnavailable()
    {
        var record = new DecisionRecord("old-run") { Reason = "PREMATURE_RETURN_TO_ACTIVE_PARENT" };
        var projection = ContainerTransitionReadModel.From("SettingsRoot", "Display", [], [record]);

        Assert.Null(projection.LatestTransition);
        Assert.Contains("unavailable", projection.Diagnostics.Single(), StringComparison.OrdinalIgnoreCase);
    }
}
