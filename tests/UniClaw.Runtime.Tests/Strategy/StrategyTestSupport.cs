using System.Collections.Immutable;
using UniClaw.Runtime.Container;
using UniClaw.Runtime.DriverHost;
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

namespace UniClaw.Runtime.Tests.Strategy;

internal static class StrategyTestSupport
{
    internal const string Application = "SampleApplication";
    internal const string Root = "SampleRoot";
    internal const string Capability = "semantic.sample";
    internal const string SupportedCriterion = "matching-item";
    internal const string Branch = "SampleBranch";
    internal const string Child = "SampleChild";
    internal const string Leaf = "SampleLeaf";

    internal static StrategyDirective Explore(
        string strategyId = "strategy-explore-1",
        int maximumDepth = 1,
        ImmutableHashSet<StrategyAdaptationKind>? adaptations = null)
        => new(
            strategyId,
            contractVersion: 1,
            new StrategyObjective(StrategyObjectiveKind.ExploreScope),
            new StrategyScope(Application, Root, maximumDepth),
            ExplorationIntent.ExhaustiveWithinScope,
            new StrategyConstraintSet(
                ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
                ImmutableHashSet.Create(
                    StrategyProhibitedEffect.StateMutation,
                    StrategyProhibitedEffect.ExternalBoundaryCrossing)),
            new StrategyCompletionCriteria(StrategyCompletionKind.ExhaustiveCoverageWithinScope),
            new StrategyAdaptationBoundary(adaptations
                ?? ImmutableHashSet.Create(
                    StrategyAdaptationKind.ReconcileBelief,
                    StrategyAdaptationKind.ReviseExecutionHypothesis)));

    internal static StrategyDirective Inspect(
        string criterionId = SupportedCriterion,
        string strategyId = "strategy-inspect-1")
        => new(
            strategyId,
            contractVersion: 1,
            new StrategyObjective(
                StrategyObjectiveKind.InspectMatchesWithinScope,
                new SemanticCriterionRef(Capability, criterionId, version: 1)),
            new StrategyScope(Application, Root, maximumDepth: 1),
            ExplorationIntent.InspectMatchesWithinScope,
            new StrategyConstraintSet(
                ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
                ImmutableHashSet.Create(
                    StrategyProhibitedEffect.StateMutation,
                    StrategyProhibitedEffect.ExternalBoundaryCrossing)),
            new StrategyCompletionCriteria(StrategyCompletionKind.AllDiscoveredMatchesInspected),
            new StrategyAdaptationBoundary(
                ImmutableHashSet.Create(
                    StrategyAdaptationKind.ReconcileBelief,
                    StrategyAdaptationKind.ReviseExecutionHypothesis)));

    internal static StrategyContractCompiler ExploreCompiler(bool evidenceSatisfied = true)
        => new([new TestBinding(
            ExplorationIntent.ExhaustiveWithinScope,
            defaultBinding: true,
            evidenceSatisfied: evidenceSatisfied)]);

    internal static StrategyContractCompiler InspectCompiler(bool supportsCompletion = true)
        => new([new TestBinding(
            ExplorationIntent.InspectMatchesWithinScope,
            defaultBinding: false,
            supportsCompletion: supportsCompletion)]);

    internal static StrategyRunStartRequest Request(
        StrategyDirective strategy,
        string device = "serial:sample-device")
        => new(
            strategy,
            DeviceSelector.TryParse(device, out var selector)
                ? selector
                : throw new InvalidOperationException("Test device selector is invalid."));

    internal static RunExecutionGraph CreateGraph()
    {
        var environment = new ScriptedEnvironment(
            "Launcher",
            Root,
            [
                new ScreenConfig(
                    "Launcher",
                    "Launcher",
                    [new ElementConfig("Home", null, null)]),
                new ScreenConfig(
                    Root,
                    Application,
                    [new ElementConfig(Branch, null, new TransitionConfig(ScreenTransitionAction.Tap, Child), new ElementBounds(0, .1f, 1, .2f), "menu_item")]),
                new ScreenConfig(
                    Child,
                    Application,
                    [
                        new ElementConfig(Root, null, new TransitionConfig(ScreenTransitionAction.Tap, Root), new ElementBounds(0, 0, .2f, .1f), "menu_item"),
                        new ElementConfig(Leaf, null, null),
                    ]),
            ]);
        var decorated = new SemanticCapabilityTestEnvironment(environment, element =>
            element.Text == Branch ? FixtureSemanticRole.NavigationCandidate
            : element.Text == Root ? FixtureSemanticRole.ParentReturnControl : null);
        var traversal = new RuntimeTraversal(decorated);
        Func<Observation, string?> resolver = ResolvePage;
        var startup = new RuntimeStartup(decorated, Application, resolver);
        var recovery = new RuntimeRecovery(
            decorated,
            _ => ImmutableArray<DeviceAction>.Empty,
            (_, _) => null,
            (_, _) => true);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => decorated.ObserveAsync(cancellationToken),
            resolver,
            page => new RuntimeContainer(
                page,
                observation => string.Equals(resolver(observation), page, StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);
        return new RunExecutionGraph(agent, decorated);
    }

    internal sealed class TestBinding(
        ExplorationIntent exploration,
        bool defaultBinding,
        bool supportsCompletion = true,
        bool evidenceSatisfied = true) : IStrategySemanticCapabilityBinding
    {
        public string CapabilityId => Capability;
        public int Version => 1;
        public ExplorationIntent Exploration => exploration;
        public bool SupportsUnqualifiedObjective => defaultBinding;
        public bool SupportsCriterion(string criterionId)
            => string.Equals(criterionId, SupportedCriterion, StringComparison.Ordinal);
        public bool SupportsCompletion(StrategyCompletionKind completion) => supportsCompletion;

        public Goal CreateGoal(StrategyDirective strategy)
            => new(
                EvidenceEvaluator: observation => new GoalEvidence(
                    Satisfied: evidenceSatisfied
                        && string.Equals(ResolvePage(observation), Root, StringComparison.Ordinal),
                    Reason: "Generic bounded strategy evidence satisfied by the Fake World.",
                    SourceObservationSequence: observation.SequenceNumber),
                CandidateAuthorizationEvaluator: EvaluateAuthorization,
                ViewportExplorationEvaluator: null,
                BranchInventoryEvaluator: EvaluateInventory,
                DiscoveredBranchEffectCriterion: null,
                CategoryClassifier: element => element.Text is Branch or Root
                    ? TypeLevelElementCategory.NavigableContainer
                    : null);

        public TypeLevelDispatchPolicy? CreateDispatchPolicy(StrategyDirective strategy)
            => new(
                ImmutableDictionary<TypeLevelElementCategory, TypeLevelHandling>.Empty
                    .Add(TypeLevelElementCategory.NavigableContainer, TypeLevelHandling.EnterAndTraverse));
    }

    private static CandidateAuthorizationEvidence EvaluateAuthorization(
        Observation observation,
        ObservedElement candidate)
        => new(
            candidate.Text is Branch or Root,
            candidate.Text is Branch or Root
                ? "Generic candidate is inside the Fake World strategy boundary."
                : "Generic candidate is outside the Fake World strategy boundary.");

    private static BranchInventoryEvidence EvaluateInventory(
        ImmutableArray<Observation> observations,
        int semanticDepth)
    {
        if (observations.IsDefaultOrEmpty)
            return new BranchInventoryEvidence(null, "No accepted Fake World evidence is available.");
        var current = observations[^1];
        if (semanticDepth == 0 && current.Elements.Any(element => element.Text == Branch))
        {
            var occurrence = SourceEquivalenceNormalizer.OccurrencesOf(current)
                .FirstOrDefault(o => o.CanonicalOccurrence.Reference.ElementIndex < current.Elements.Length
                    && current.Elements[o.CanonicalOccurrence.Reference.ElementIndex].Text == Branch);
            if (occurrence is null)
                return new BranchInventoryEvidence(null, "Primary navigation occurrence grounding is unresolved.");
            return new BranchInventoryEvidence(
                ImmutableDictionary<string, long>.Empty.Add(Branch, current.SequenceNumber),
                "Generic root inventory contains one bounded branch.",
                ImmutableDictionary<string, NavigationSourceOccurrenceReference>.Empty.Add(
                    Branch, new NavigationSourceOccurrenceReference(occurrence.ObservationSequence, occurrence.OccurrenceIdentity)));
        }
        if (semanticDepth == 1 && current.Elements.Any(element => element.Text == Leaf))
        {
            return new BranchInventoryEvidence(
                ImmutableDictionary<string, long>.Empty,
                "Generic bounded child is a leaf.");
        }
        return new BranchInventoryEvidence(null, "Fake World inventory evidence is unresolved.");
    }

    private static string? ResolvePage(Observation observation)
    {
        if (!string.Equals(observation.ForegroundApplication, Application, StringComparison.Ordinal))
            return null;
        if (observation.Elements.Any(element => element.Text == Branch))
            return Root;
        if (observation.Elements.Any(element => element.Text == Leaf))
            return Child;
        return null;
    }
}
