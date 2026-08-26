using System.Collections.Immutable;
using UniClaw.Runtime.Container;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Recovery;
using UniClaw.Runtime.Startup;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.Traversal;
using UniClaw.Runtime.World;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using Xunit;

namespace UniClaw.Runtime.Tests.Strategy;

public sealed class StrategyExplorationLedgerRealPathTests
{
    [Fact]
    public async Task RealStrategyPath_ProducesExactIdentityPartitionAndUnresolvedEvidence()
    {
        var graph = CreateGraph();
        var binding = new TwoIdentityBinding();
        var intent = Assert.IsType<StrategyCompilationResult.Accepted>(
            new StrategyContractCompiler([binding]).Compile(StrategyTestSupport.Explore(maximumDepth: 1))).Intent;

        await StrategyExecution.RunAsync(graph.Agent, intent, "real-ledger-partition");
        var root = graph.Agent.CompileExplorationLedgerView().Scopes.Single(scope => scope.ScopeIdentity == StrategyTestSupport.Root);

        Assert.True((2, 1, 0, 1, 0) == (root.Discovered, root.Visited, root.Pending, root.Unresolved, root.UnknownFrontier), string.Join(" | ", graph.Agent.Trace.Select(t => t.Reason)));
        Assert.Contains("container", graph.Agent.BranchProgress[StrategyTestSupport.Root].CompletedSiblingEvidence.Keys);
        Assert.DoesNotContain("unknown", graph.Agent.BranchProgress[StrategyTestSupport.Root].AuthorizedSiblingEvidence.Keys);
        Assert.Equal(1, graph.Agent.UnresolvedNodes[StrategyTestSupport.Root]);
        Assert.Contains("container", binding.AuthorizationCandidates);
        Assert.DoesNotContain("unknown", binding.AuthorizationCandidates);
        var environment = Assert.IsType<SemanticCapabilityTestEnvironment>(graph.Environment);
        Assert.Contains(environment.ActionHistory, action => action is DeviceAction.Tap { TargetBounds.IsValid: true } tap
            && Math.Abs(tap.TargetBounds.Y1 - .1f) < .01f && Math.Abs(tap.TargetBounds.X2 - .3f) < .01f);
        Assert.DoesNotContain(environment.ActionHistory, action => action is DeviceAction.Tap { TargetBounds.IsValid: true } tap
            && Math.Abs(tap.TargetBounds.X1 - .6f) < .01f && Math.Abs(tap.TargetBounds.X2 - .9f) < .01f);
    }

    [Fact]
    public async Task RealDepthZeroBoundary_ProducesRecordOnlyUnknownFrontierWithoutDispatch()
    {
        var graph = StrategyTestSupport.CreateGraph();
        var intent = Assert.IsType<StrategyCompilationResult.Accepted>(
            StrategyTestSupport.ExploreCompiler().Compile(StrategyTestSupport.Explore(maximumDepth: 0))).Intent;

        await StrategyExecution.RunAsync(graph.Agent, intent, "real-ledger-frontier");
        var ledger = graph.Agent.CompileExplorationLedgerView().Scopes.Single();
        Assert.Equal((1, 1, 0, 0, 1), (ledger.Discovered, ledger.Visited, ledger.Pending, ledger.Unresolved, ledger.UnknownFrontier));
        Assert.Contains(StrategyTestSupport.Branch, graph.Agent.RecordOnlySatisfied[StrategyTestSupport.Root].Keys);
        Assert.Contains(StrategyTestSupport.Branch, graph.Agent.UnknownFrontierIdentities[StrategyTestSupport.Root]);
        var environment = Assert.IsType<SemanticCapabilityTestEnvironment>(graph.Environment);
        Assert.DoesNotContain(environment.ActionHistory, action => action is DeviceAction.Tap { TargetBounds.IsValid: true } tap
            && Math.Abs(tap.TargetBounds.Y1 - .1f) < .01f && Math.Abs(tap.TargetBounds.X2 - 1f) < .01f);
    }

    [Fact]
    public async Task RealClassifiedUnsatisfiedPathRemainsPendingWithoutDispatch()
    {
        var graph = CreateGraph();
        var binding = new TwoIdentityBinding(includeUnknown: false, authorizeContainer: false);
        var intent = Assert.IsType<StrategyCompilationResult.Accepted>(
            new StrategyContractCompiler([binding]).Compile(StrategyTestSupport.Explore(maximumDepth: 1))).Intent;

        await StrategyExecution.RunAsync(graph.Agent, intent, "real-ledger-pending");
        var root = graph.Agent.CompileExplorationLedgerView().Scopes.Single(scope => scope.ScopeIdentity == StrategyTestSupport.Root);
        Assert.Equal((1, 0, 1, 0, 0), (root.Discovered, root.Visited, root.Pending, root.Unresolved, root.UnknownFrontier));
        Assert.DoesNotContain("container", graph.Agent.BranchProgress[StrategyTestSupport.Root].CompletedSiblingEvidence.Keys);
        Assert.DoesNotContain("container", graph.Agent.RecordOnlySatisfied.GetValueOrDefault(StrategyTestSupport.Root)?.Keys ?? []);
        Assert.Equal(0, graph.Agent.UnresolvedNodes.GetValueOrDefault(StrategyTestSupport.Root));
        var environment = Assert.IsType<SemanticCapabilityTestEnvironment>(graph.Environment);
        Assert.DoesNotContain(environment.ActionHistory, action => action is DeviceAction.Tap { TargetBounds.IsValid: true } tap
            && Math.Abs(tap.TargetBounds.Y1 - .1f) < .01f && Math.Abs(tap.TargetBounds.X2 - .3f) < .01f);
    }

    [Fact]
    public async Task ActualBoundaryEvidenceWithContradictoryUnresolvedIdentityFailsClosed()
    {
        var graph = StrategyTestSupport.CreateGraph();
        var intent = Assert.IsType<StrategyCompilationResult.Accepted>(
            StrategyTestSupport.ExploreCompiler().Compile(StrategyTestSupport.Explore(maximumDepth: 0))).Intent;
        await StrategyExecution.RunAsync(graph.Agent, intent, "real-ledger-contradiction");
        var progress = graph.Agent.BranchProgress[StrategyTestSupport.Root];
        var recordOnly = graph.Agent.RecordOnlySatisfied[StrategyTestSupport.Root];
        var contradictoryEvidence = new ExplorationScopeEvidence(
            progress,
            unresolvedIds: recordOnly.Keys,
            recordOnlyIds: recordOnly,
            unknownFrontierIds: graph.Agent.UnknownFrontierIdentities[StrategyTestSupport.Root]);
        Assert.Throws<InvalidOperationException>(() => ExplorationLedgerCompiler.CompileScope(contradictoryEvidence));
    }

    private sealed class TwoIdentityBinding : IStrategySemanticCapabilityBinding
    {
        public string CapabilityId => StrategyTestSupport.Capability;
        public int Version => 1;
        public ExplorationIntent Exploration => ExplorationIntent.ExhaustiveWithinScope;
        public bool SupportsUnqualifiedObjective => true;
        private readonly bool _includeUnknown;
        private readonly bool _authorizeContainer;
        public TwoIdentityBinding(bool includeUnknown = true, bool authorizeContainer = true)
        { _includeUnknown = includeUnknown; _authorizeContainer = authorizeContainer; }
        public List<string> AuthorizationCandidates { get; } = [];
        public bool SupportsCriterion(string criterionId) => false;
        public bool SupportsCompletion(StrategyCompletionKind completion) => true;
        public Goal CreateGoal(StrategyDirective strategy) => new(
            observation => new GoalEvidence(false, "generic unsatisfied", observation.SequenceNumber),
            (_, candidate) => { AuthorizationCandidates.Add(candidate.Text); return new CandidateAuthorizationEvidence((candidate.Text == "container" && _authorizeContainer) || candidate.Text == StrategyTestSupport.Root, "generic"); },
            BranchInventoryEvaluator: Inventory,
            CategoryClassifier: element => element.Text is "container" or StrategyTestSupport.Root ? TypeLevelElementCategory.NavigableContainer : null);
        public TypeLevelDispatchPolicy CreateDispatchPolicy(StrategyDirective strategy) => new(
            ImmutableDictionary<TypeLevelElementCategory, TypeLevelHandling>.Empty.Add(TypeLevelElementCategory.NavigableContainer, TypeLevelHandling.EnterAndTraverse));
        private BranchInventoryEvidence Inventory(ImmutableArray<Observation> observations, int depth)
        {
            if (observations.IsDefaultOrEmpty) return new BranchInventoryEvidence(null, "empty");
            var current = observations[^1];
            var ids = current.Elements.Where(e => e.Text == "container" || (_includeUnknown && e.Text == "unknown")).ToImmutableDictionary(e => e.Text, _ => current.SequenceNumber);
            var occurrences = SourceEquivalenceNormalizer.OccurrencesOf(current).Where(o => ids.ContainsKey(current.Elements[o.CanonicalOccurrence.Reference.ElementIndex].Text)).ToImmutableDictionary(o => current.Elements[o.CanonicalOccurrence.Reference.ElementIndex].Text, o => new NavigationSourceOccurrenceReference(current.SequenceNumber, o.OccurrenceIdentity));
            return new BranchInventoryEvidence(ids, "generic inventory", occurrences);
        }
    }

    private static RunExecutionGraph CreateGraph()
    {
        var environment = new ScriptedEnvironment("Launcher", StrategyTestSupport.Root, [
            new ScreenConfig("Launcher", "Launcher", [new ElementConfig("start", null, null)]),
            new ScreenConfig(StrategyTestSupport.Root, StrategyTestSupport.Application, [
                new ElementConfig("container", null, new TransitionConfig(ScreenTransitionAction.Tap, StrategyTestSupport.Child), new ElementBounds(0, .1f, .3f, .2f)),
                new ElementConfig("unknown", null, null, new ElementBounds(.6f, .1f, .9f, .2f))]),
            new ScreenConfig(StrategyTestSupport.Child, StrategyTestSupport.Application, [new ElementConfig(StrategyTestSupport.Root, null, new TransitionConfig(ScreenTransitionAction.Tap, StrategyTestSupport.Root), new ElementBounds(0, 0, .2f, .1f))])]);
        var decorated = new SemanticCapabilityTestEnvironment(environment, element =>
            element.Text is "container" or "unknown" ? FixtureSemanticRole.NavigationCandidate
            : element.Text == StrategyTestSupport.Root ? FixtureSemanticRole.ParentReturnControl : null);
        var traversal = new RuntimeTraversal(decorated);
        static string? Resolve(Observation observation) => observation.ForegroundApplication == StrategyTestSupport.Application ? (observation.Elements.Any(e => e.Text == "unknown" || e.Text == "container") ? StrategyTestSupport.Root : StrategyTestSupport.Child) : null;
        var startup = new RuntimeStartup(decorated, StrategyTestSupport.Application, Resolve);
        var recovery = new RuntimeRecovery(decorated, _ => ImmutableArray<DeviceAction>.Empty, (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(startup, traversal, ct => decorated.ObserveAsync(ct), Resolve, page => new RuntimeContainer(page, observation => Resolve(observation) == page, traversal.ExecuteStep), recovery);
        return new RunExecutionGraph(agent, decorated);
    }
}
