using UniClaw.Runtime.Planning;
using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Container;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Recovery;
using UniClaw.Runtime.Startup;
using UniClaw.Runtime.Traversal;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.World;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using Xunit;

namespace UniClaw.Runtime.Tests.Strategy;

public sealed class StrategyExplorationRulePathTests
{
    [Fact]
    public async Task StrategyDepthZero_RecordsBoundaryIdentityAsRecordOnlyFrontier()
    {
        var graph = CreateGraphWithClassifiableLeaf();
        var strategy = StrategyTestSupport.Explore(maximumDepth: 0);
        var intent = Assert.IsType<StrategyCompilationResult.Accepted>(
            StrategyTestSupport.ExploreCompiler().Compile(strategy)).Intent;

        await StrategyExecution.RunAsync(graph.Agent, intent, "strategy-rule-depth-zero");

        Assert.True(graph.Agent.RecordOnlySatisfied.TryGetValue(StrategyTestSupport.Root, out var satisfied));
        Assert.Contains(StrategyTestSupport.Branch, satisfied!.Keys);
        Assert.True(graph.Agent.UnknownFrontierIdentities.TryGetValue(StrategyTestSupport.Root, out var frontier));
        Assert.Contains(StrategyTestSupport.Branch, frontier!);
        Assert.Equal(1, graph.Agent.UnknownFrontierBeyondDepth[StrategyTestSupport.Root]);
        var environment = Assert.IsType<SemanticCapabilityTestEnvironment>(graph.Environment);
        Assert.DoesNotContain(environment.ActionHistory,
            action => action is DeviceAction.Tap { TargetBounds.IsValid: true } tap
                && Math.Abs(tap.TargetBounds.Y1 - .1f) < .01f
                && Math.Abs(tap.TargetBounds.X2 - 1f) < .01f);
    }

    [Fact]
    public async Task LegacyDepthZeroDoesNotCreateStrategyRuleEvidence()
    {
        var graph = StrategyTestSupport.CreateGraph();
        var strategy = StrategyTestSupport.Explore(maximumDepth: 0);
        var intent = Assert.IsType<StrategyCompilationResult.Accepted>(
            StrategyTestSupport.ExploreCompiler().Compile(strategy)).Intent;
        var envelope = IntentSemanticEnvelope.Project(
            "legacy-open-world",
            intent.Goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(intent.Specification));

        await IntentExecution.RunOpenWorldAsync(graph.Agent, envelope, "legacy-rule-depth-zero", default);

        Assert.Empty(graph.Agent.RecordOnlySatisfied);
        Assert.Empty(graph.Agent.UnknownFrontierIdentities);
    }

    [Fact]
    public async Task StrategyDepthOne_ClassifiesDirectChildLeafRecordOnlyBeforeAuthorization()
    {
        var graph = CreateGraphWithClassifiableLeaf();
        var binding = new RuleBinding(classifyLeaf: true);
        var strategy = StrategyTestSupport.Explore(maximumDepth: 1);
        var intent = Assert.IsType<StrategyCompilationResult.Accepted>(
            new StrategyContractCompiler([binding]).Compile(strategy)).Intent;

        await StrategyExecution.RunAsync(graph.Agent, intent, "strategy-rule-depth-one");

        Assert.Contains(StrategyTestSupport.Branch, binding.AuthorizationCandidates);
        Assert.Contains(StrategyTestSupport.Branch, binding.AuthorizedCandidates);
        Assert.DoesNotContain(StrategyTestSupport.Leaf, binding.AuthorizationCandidates);
        Assert.DoesNotContain(StrategyTestSupport.Leaf, binding.AuthorizedCandidates);
        Assert.True(graph.Agent.RecordOnlySatisfied.TryGetValue(StrategyTestSupport.Child, out var satisfied),
            string.Join(" | ", graph.Agent.Trace.Select(trace => trace.Reason)));
        Assert.Contains(StrategyTestSupport.Leaf, satisfied!.Keys);
        var environment = Assert.IsType<SemanticCapabilityTestEnvironment>(graph.Environment);
        Assert.Contains(environment.ActionHistory,
            action => action is DeviceAction.Tap { TargetBounds.IsValid: true } tap
                && Math.Abs(tap.TargetBounds.Y1 - .1f) < .01f
                && Math.Abs(tap.TargetBounds.X2 - 1f) < .01f);
        Assert.DoesNotContain(environment.ActionHistory,
            action => action is DeviceAction.Tap { TargetBounds.IsValid: true } tap
                && Math.Abs(tap.TargetBounds.Y1 - .2f) < .01f
                && Math.Abs(tap.TargetBounds.X2 - .4f) < .01f);
        Assert.DoesNotContain(environment.ActionHistory, action => action is DeviceAction.SetSwitch);
        Assert.DoesNotContain(StrategyTestSupport.Leaf,
            graph.Agent.BranchProgress[StrategyTestSupport.Child].AuthorizedSiblingEvidence.Keys);
        Assert.Contains(StrategyTestSupport.Branch,
            graph.Agent.BranchProgress[StrategyTestSupport.Root].CompletedSiblingEvidence.Keys);
        Assert.Contains(StrategyTestSupport.Branch, binding.AuthorizedCandidates);
        Assert.True(graph.Agent.Trace.Any(trace => trace.Reason?.StartsWith(
            "verified parent return", StringComparison.Ordinal) is true));
    }

    [Fact]
    public async Task StrategyDepthOne_UnclassifiableDirectChildRemainsUnresolvedBeforeAuthorization()
    {
        var graph = CreateGraphWithClassifiableLeaf();
        var binding = new RuleBinding(classifyLeaf: false);
        var intent = Assert.IsType<StrategyCompilationResult.Accepted>(
            new StrategyContractCompiler([binding]).Compile(StrategyTestSupport.Explore(maximumDepth: 1))).Intent;

        await StrategyExecution.RunAsync(graph.Agent, intent, "strategy-rule-unresolved");

        Assert.DoesNotContain(StrategyTestSupport.Leaf, binding.AuthorizationCandidates);
        Assert.True(graph.Agent.UnresolvedNodes.TryGetValue(StrategyTestSupport.Child, out var unresolved),
            string.Join(" | ", graph.Agent.Trace.Select(trace => trace.Reason)));
        Assert.Equal(1, unresolved);
        Assert.DoesNotContain(StrategyTestSupport.Leaf,
            graph.Agent.BranchProgress[StrategyTestSupport.Child].AuthorizedSiblingEvidence.Keys);
        Assert.Single(graph.Agent.BranchProgress[StrategyTestSupport.Child].ApprovedSiblingEvidence);
        var environment = Assert.IsType<SemanticCapabilityTestEnvironment>(graph.Environment);
        Assert.DoesNotContain(environment.ActionHistory,
            action => action is DeviceAction.Tap { TargetBounds.IsValid: true } tap
                && Math.Abs(tap.TargetBounds.Y1 - .2f) < .01f
                && Math.Abs(tap.TargetBounds.X2 - .4f) < .01f);
        Assert.DoesNotContain(environment.ActionHistory, action => action is DeviceAction.SetSwitch);
    }

    [Fact]
    public async Task StrategyDepthTwoExhaustive_UsesTheExistingCutoffReason()
    {
        var graph = CreateGraphWithClassifiableLeaf();
        var binding = new RuleBinding(classifyLeaf: true, leafContainer: true);
        var intent = Assert.IsType<StrategyCompilationResult.Accepted>(
            new StrategyContractCompiler([binding]).Compile(StrategyTestSupport.Explore(maximumDepth: 2))).Intent;

        await StrategyExecution.RunAsync(graph.Agent, intent, "strategy-rule-cutoff");

        Assert.Equal(
            "In-scope inventory requires traversal beyond declared depth=2; bounded cutoff is not exhaustion.",
            graph.Agent.Reason);
    }

    [Fact]
    public async Task StrategyDepthTwoInspect_BoundaryRecordsFrontierWithoutDispatch()
    {
        var graph = CreateGraphWithClassifiableLeaf();
        var binding = new RuleBinding(
            classifyLeaf: true,
            leafContainer: true,
            exploration: ExplorationIntent.InspectMatchesWithinScope,
            supportsCriterion: true);
        var strategy = StrategyTestSupport.Inspect(maximumDepth: 2);
        var intent = Assert.IsType<StrategyCompilationResult.Accepted>(
            new StrategyContractCompiler([binding]).Compile(strategy)).Intent;

        await StrategyExecution.RunAsync(graph.Agent, intent, "strategy-rule-inspect-boundary");

        Assert.True(graph.Agent.RecordOnlySatisfied.TryGetValue("SampleDeep", out var satisfied));
        Assert.Contains(StrategyTestSupport.Leaf, satisfied!.Keys);
        Assert.True(graph.Agent.UnknownFrontierIdentities.TryGetValue("SampleDeep", out var frontier));
        Assert.Contains(StrategyTestSupport.Leaf, frontier!);
        var environment = Assert.IsType<SemanticCapabilityTestEnvironment>(graph.Environment);
        Assert.DoesNotContain(environment.ActionHistory,
            action => action is DeviceAction.Tap { TargetBounds.IsValid: true } tap
                && Math.Abs(tap.TargetBounds.Y1 - .6f) < .01f
                && Math.Abs(tap.TargetBounds.X2 - .9f) < .01f);
        Assert.DoesNotContain(environment.ActionHistory, action => action is DeviceAction.SetSwitch);
    }

    private sealed class RuleBinding : IStrategySemanticCapabilityBinding
    {
        private readonly bool _classifyLeaf;
        private readonly bool _leafContainer;
        private readonly ExplorationIntent _exploration;
        private readonly bool _supportsCriterion;

        public RuleBinding(
            bool classifyLeaf,
            bool leafContainer = false,
            ExplorationIntent exploration = ExplorationIntent.ExhaustiveWithinScope,
            bool supportsCriterion = false)
        {
            _classifyLeaf = classifyLeaf;
            _leafContainer = leafContainer;
            _exploration = exploration;
            _supportsCriterion = supportsCriterion;
        }
        public string CapabilityId => StrategyTestSupport.Capability;
        public int Version => 1;
        public ExplorationIntent Exploration => _exploration;
        public bool SupportsUnqualifiedObjective => _exploration == ExplorationIntent.ExhaustiveWithinScope;
        public List<string> AuthorizationCandidates { get; } = [];
        public List<string> AuthorizedCandidates { get; } = [];
        public bool SupportsCriterion(string criterionId) => _supportsCriterion
            && string.Equals(criterionId, StrategyTestSupport.SupportedCriterion, StringComparison.Ordinal);
        public bool SupportsCompletion(StrategyCompletionKind completion)
            => _exploration == ExplorationIntent.ExhaustiveWithinScope
                ? completion == StrategyCompletionKind.ExhaustiveCoverageWithinScope
                : completion == StrategyCompletionKind.AllDiscoveredMatchesInspected;

        public Goal CreateGoal(StrategyDirective strategy)
            => new(
                observation => new GoalEvidence(
                    string.Equals(Page(observation), StrategyTestSupport.Root, StringComparison.Ordinal),
                    "generic test evidence",
                    observation.SequenceNumber),
                (observation, candidate) =>
                {
                    AuthorizationCandidates.Add(candidate.Text);
                    var authorized = candidate.Text is StrategyTestSupport.Branch or StrategyTestSupport.Root
                        || (_leafContainer && candidate.Text == StrategyTestSupport.Leaf);
                    if (authorized) AuthorizedCandidates.Add(candidate.Text);
                    return new CandidateAuthorizationEvidence(authorized, "test authorization");
                },
                BranchInventoryEvaluator: (observations, depth) => Inventory(observations, depth),
                CategoryClassifier: element => element.Text is StrategyTestSupport.Branch or StrategyTestSupport.Root
                    ? TypeLevelElementCategory.NavigableContainer
                    : _classifyLeaf && element.Text == StrategyTestSupport.Leaf
                        ? _leafContainer ? TypeLevelElementCategory.NavigableContainer : TypeLevelElementCategory.StateChangingControl
                        : null);

        public TypeLevelDispatchPolicy CreateDispatchPolicy(StrategyDirective strategy)
            => new(
                ImmutableDictionary<TypeLevelElementCategory, TypeLevelHandling>.Empty
                    .Add(TypeLevelElementCategory.NavigableContainer, TypeLevelHandling.EnterAndTraverse));

        private static BranchInventoryEvidence Inventory(ImmutableArray<Observation> observations, int depth)
        {
            if (observations.IsDefaultOrEmpty) return new BranchInventoryEvidence(null, "no observations");
            var current = observations[^1];
            var wanted = depth == 0 ? StrategyTestSupport.Branch : StrategyTestSupport.Leaf;
            var element = current.Elements.FirstOrDefault(candidate => candidate.Text == wanted);
            if (element is null) return new BranchInventoryEvidence(null, "no required element");
            var occurrence = SourceEquivalenceNormalizer.OccurrencesOf(current)
                .FirstOrDefault(item => item.CanonicalOccurrence.Reference.ElementIndex < current.Elements.Length
                    && current.Elements[item.CanonicalOccurrence.Reference.ElementIndex].Text == wanted);
            if (occurrence is null) return new BranchInventoryEvidence(null, "no source occurrence");
            return new BranchInventoryEvidence(
                ImmutableDictionary<string, long>.Empty.Add(wanted, current.SequenceNumber),
                "test inventory",
                ImmutableDictionary<string, NavigationSourceOccurrenceReference>.Empty.Add(
                    wanted,
                    new NavigationSourceOccurrenceReference(current.SequenceNumber, occurrence.OccurrenceIdentity)));
        }

        private static string? Page(Observation observation)
            => observation.Elements.Any(element => element.Text == StrategyTestSupport.Branch)
                ? StrategyTestSupport.Root
                : observation.Elements.Any(element => element.Text == StrategyTestSupport.Leaf)
                    ? StrategyTestSupport.Child
                    : null;
    }

    private static RunExecutionGraph CreateGraphWithClassifiableLeaf()
    {
        var environment = new ScriptedEnvironment(
            "Launcher", StrategyTestSupport.Root,
            [
                new ScreenConfig("Launcher", "Launcher", [new ElementConfig("Home", null, null)]),
                new ScreenConfig(StrategyTestSupport.Root, StrategyTestSupport.Application,
                    [new ElementConfig(StrategyTestSupport.Branch, null,
                        new TransitionConfig(ScreenTransitionAction.Tap, StrategyTestSupport.Child),
                        new ElementBounds(0, .1f, 1, .2f), "menu_item")]),
                new ScreenConfig(StrategyTestSupport.Child, StrategyTestSupport.Application,
                    [new ElementConfig(StrategyTestSupport.Root, null,
                        new TransitionConfig(ScreenTransitionAction.Tap, StrategyTestSupport.Root),
                        new ElementBounds(0, 0, .2f, .1f), "menu_item"),
                     new ElementConfig(StrategyTestSupport.Leaf, null,
                        new TransitionConfig(ScreenTransitionAction.Tap, "SampleDeep"),
                        new ElementBounds(0, .2f, .4f, .4f), "menu_item")])
                ,new ScreenConfig("SampleDeep", StrategyTestSupport.Application,
                    [new ElementConfig("DeepMarker", null, null,
                        new ElementBounds(0, 0, .1f, .1f), "marker"),
                     new ElementConfig(StrategyTestSupport.Leaf, null,
                        new TransitionConfig(ScreenTransitionAction.Tap, "SampleDeep"),
                        new ElementBounds(.6f, .6f, .9f, .9f), "menu_item")])
            ]);
        var decorated = new SemanticCapabilityTestEnvironment(environment, element =>
            element.Text == StrategyTestSupport.Branch ? FixtureSemanticRole.NavigationCandidate
            : element.Text == StrategyTestSupport.Leaf ? FixtureSemanticRole.NavigationCandidate
            : element.Text == StrategyTestSupport.Root ? FixtureSemanticRole.ParentReturnControl : null);
        var traversal = new RuntimeTraversal(decorated);
        static string? Resolve(Observation observation)
            => observation.ForegroundApplication == StrategyTestSupport.Application
                ? observation.Elements.Any(element => element.Text == "DeepMarker")
                    ? "SampleDeep"
                    : observation.Elements.Any(element => element.Text == StrategyTestSupport.Branch)
                    ? StrategyTestSupport.Root
                    : observation.Elements.Any(element => element.Text == StrategyTestSupport.Leaf)
                        ? StrategyTestSupport.Child
                        : null
                : null;
        var startup = new RuntimeStartup(decorated, StrategyTestSupport.Application, Resolve);
        var recovery = new RuntimeRecovery(decorated, _ => ImmutableArray<DeviceAction>.Empty,
            (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(startup, traversal,
            cancellationToken => decorated.ObserveAsync(cancellationToken), Resolve,
            page => new RuntimeContainer(page,
                observation => string.Equals(Resolve(observation), page, StringComparison.Ordinal),
                traversal.ExecuteStep), recovery);
        return new RunExecutionGraph(agent, decorated);
    }
}
