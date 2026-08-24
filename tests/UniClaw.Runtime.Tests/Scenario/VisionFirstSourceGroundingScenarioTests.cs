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

/// <summary>Source-grounding proofs: Vision is primary; structured evidence is optional corroboration.</summary>
public sealed class VisionFirstSourceGroundingScenarioTests
{
    private const string App = "fixture.app";

    [Fact]
    public async Task VisionOnly_DiscoversGroundsExecutesVerifiesAndCompletes()
    {
        var h = Build(withVision: true, withAuxiliary: false);
        var state = await IntentExecution.RunOpenWorldAsync(h.Agent, h.Envelope, "vision-only", CancellationToken.None);

        Assert.True(state == RunState.Completed, $"reason={h.Agent.Reason}; trace={string.Join(" | ", h.Agent.Trace.Select(t => t.Reason))}; actions={h.Environment.Inner.ActionHistory.Count}");
        Assert.Equal(2, h.Environment.Inner.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.Contains(h.Evidence, e => e.Satisfied);
        Assert.All(h.Environment.ObservationHistory, o => Assert.Contains(o.Sources, s => s.Tier == ObservationSourceTier.PrimaryVision));
    }

    [Fact]
    public async Task VisionAndAuxiliary_ProducesSameAuthoritativeResult()
    {
        var vision = Build(withVision: true, withAuxiliary: false);
        var mixed = Build(withVision: true, withAuxiliary: true);

        var visionState = await IntentExecution.RunOpenWorldAsync(vision.Agent, vision.Envelope, "vision", CancellationToken.None);
        var mixedState = await IntentExecution.RunOpenWorldAsync(mixed.Agent, mixed.Envelope, "mixed", CancellationToken.None);

        Assert.True(visionState == RunState.Completed, $"vision reason={vision.Agent.Reason}; trace={string.Join(" | ", vision.Agent.Trace.Select(t => t.Reason))}");
        Assert.True(mixedState == RunState.Completed, $"mixed reason={mixed.Agent.Reason}; trace={string.Join(" | ", mixed.Agent.Trace.Select(t => t.Reason))}");
        Assert.Equal(vision.Environment.Inner.ActionHistory.OfType<DeviceAction.Tap>().Count(), mixed.Environment.Inner.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.Contains(mixed.Environment.ObservationHistory, o => o.Sources.Any(s => s.Tier == ObservationSourceTier.AuxiliaryStructured));
        Assert.Contains(mixed.Environment.ObservationHistory, o => o.StructuredElements.Length > 0);
        var canonical = SourceGroundingNormalizer.Normalize(mixed.Environment.ObservationHistory[0]).Single(o => o.PrimarySupport);
        Assert.NotEmpty(canonical.AuxiliarySupports);
        Assert.Contains(mixed.Evidence, e => e.Satisfied);
    }

    [Fact]
    public async Task AuxiliaryOnly_CannotGroundAuthorizeOrComplete()
    {
        var h = Build(withVision: false, withAuxiliary: true);
        var state = await IntentExecution.RunOpenWorldAsync(h.Agent, h.Envelope, "adb-only", CancellationToken.None);

        Assert.NotEqual(RunState.Completed, state);
        Assert.Empty(h.Environment.Inner.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.DoesNotContain(h.Evidence, e => e.Satisfied);
        Assert.All(h.Environment.ObservationHistory, o => Assert.DoesNotContain(o.Sources, s => s.Tier == ObservationSourceTier.PrimaryVision && s.Available));
    }

    private static Harness Build(bool withVision, bool withAuxiliary)
    {
        var inner = new ScriptedEnvironment("A", "A", [
            new ScreenConfig("A", App, [
                new ElementConfig("B", null, new TransitionConfig(ScreenTransitionAction.Tap, "B"), new ElementBounds(0.1f, 0.1f, 0.3f, 0.3f), "menu_item")]),
            new ScreenConfig("B", App, [
                new ElementConfig("A", null, new TransitionConfig(ScreenTransitionAction.Tap, "A"), new ElementBounds(0.1f, 0.1f, 0.3f, 0.3f), "menu_item")]),
        ]);
        var env = new SourceAwareEnvironment(inner, withVision, withAuxiliary);
        var traversal = new RuntimeTraversal(env);
        var evidence = new List<GoalEvidence>();
        string? Page(Observation o) => o.Elements.Any(e => e.Text == "B") ? "A" : o.Elements.Any(e => e.Text == "A") ? "B" : null;
        var goal = new Goal(
            observation =>
            {
                var satisfied = observation.SequenceNumber > 4;
                var result = new GoalEvidence(satisfied, satisfied ? "fresh vision evidence" : "not satisfied", observation.SequenceNumber);
                evidence.Add(result);
                return result;
            },
            (_, _) => new CandidateAuthorizationEvidence(true, "safe"),
            ViewportExplorationEvaluator: _ => new ViewportExplorationEvidence(false, "bounded fixture"),
            BranchInventoryEvaluator: (observations, _) =>
            {
                var current = observations[^1];
                var branches = current.Elements.Any(e => e.Text == "B") && !current.Elements.Any(e => e.Text == "A")
                    ? ImmutableDictionary<string, long>.Empty.Add("B", current.SequenceNumber)
                    : ImmutableDictionary<string, long>.Empty;
                var grounding = SourceEquivalenceNormalizer.OccurrencesOf(current)
                    .Where(o => o.CanonicalOccurrence.Reference.ElementIndex < current.Elements.Length)
                    .ToImmutableDictionary(
                        o => current.Elements[o.CanonicalOccurrence.Reference.ElementIndex].Text,
                        o => new NavigationSourceOccurrenceReference(o.ObservationSequence, o.OccurrenceIdentity),
                        StringComparer.Ordinal);
                return new BranchInventoryEvidence(branches, "bounded fixture", grounding);
            },
            CategoryClassifier: e => e.Text is "A" or "B" ? TypeLevelElementCategory.NavigableContainer : null);
        var spec = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, "A"),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer), 3,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, "A"),
            new TypeLevelDispatchPolicy(ImmutableDictionary.CreateRange(new Dictionary<TypeLevelElementCategory, TypeLevelHandling>
            {
                [TypeLevelElementCategory.NavigableContainer] = TypeLevelHandling.EnterAndTraverse,
            })));
        var envelope = IntentSemanticEnvelope.Project("explore bounded fixture", goal, new IntentExecutionRepresentation.OpenWorldTypeLevel(spec));
        var startup = new RuntimeStartup(env, App, Page);
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page) => new(page, o => Page(o) == page, traversal.ExecuteStep, forwardsAuthorizationReceipts: true);
        var agent = new RuntimeAgent(startup, traversal, token => env.ObserveAsync(token), Page, Factory, recovery);
        return new Harness(agent, env, envelope, evidence);
    }

    private sealed record Harness(RuntimeAgent Agent, SourceAwareEnvironment Environment, IntentSemanticEnvelope.Resolved Envelope, List<GoalEvidence> Evidence);

    private sealed class SourceAwareEnvironment : IEnvironment
    {
        private readonly bool _vision;
        private readonly bool _auxiliary;
        private readonly SemanticCapabilityTestEnvironment? _capability;
        private readonly List<Observation> _observationHistory = [];
        public ScriptedEnvironment Inner { get; }
        public IReadOnlyList<Observation> ObservationHistory => _observationHistory;

        public SourceAwareEnvironment(ScriptedEnvironment inner, bool vision, bool auxiliary)
        {
            Inner = inner;
            _vision = vision;
            _auxiliary = auxiliary;
            if (vision)
                _capability = new SemanticCapabilityTestEnvironment(inner, element => element.Text switch
                {
                    "A" => FixtureSemanticRole.ParentReturnControl,
                    "B" => FixtureSemanticRole.NavigationCandidate,
                    _ => null,
                });
        }

        public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            var observation = _capability is not null
                ? await _capability.ObserveAsync(cancellationToken)
                : await Inner.ObserveAsync(cancellationToken);
            var sequence = observation.SequenceNumber;
            var sources = new List<ObservationSourceMetadata>();
            if (_vision)
            {
                sources.AddRange(observation.Sources);
                if (sources.Count == 0)
                    sources.Add(new(ObservationSourceTier.PrimaryVision, true, sequence, $"frame:{sequence}", 100, 100, "test-vision", "vision"));
            }
            if (_auxiliary) sources.Add(new(ObservationSourceTier.AuxiliaryStructured, true, sequence, $"fixture-frame-{sequence}", 100, 100, "test-aux", "aux"));
            var structured = _auxiliary
                ? ImmutableArray.Create(new StructuredElementEvidence("menu_item", null, true, false, null, true, false, new ElementBounds(0.1f, 0.1f, 0.3f, 0.3f), RawText: "B", SourceNodeIdentity: "aux-B"))
                : ImmutableArray<StructuredElementEvidence>.Empty;
            var enriched = observation with
            {
                Elements = _vision ? observation.Elements : ImmutableArray<ObservedElement>.Empty,
                Sources = sources.ToImmutableArray(),
                StructuredElements = structured,
            };
            _observationHistory.Add(enriched);
            return enriched;
        }
        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken) => Inner.ExecuteAsync(action, cancellationToken);
    }
}
