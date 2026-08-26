using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using System.Reflection;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using Xunit;

namespace UniClaw.Runtime.Tests.Strategy;

public sealed class StrategyExplorationRunBindingTests
{
    [Fact]
    public async Task StrategyExecution_BindsTheAcceptedSemanticsInstanceToTheRun()
    {
        var graph = StrategyTestSupport.CreateGraph();
        var strategy = StrategyTestSupport.Explore(maximumDepth: 1);
        var result = Assert.IsType<StrategyCompilationResult.Accepted>(
            StrategyTestSupport.ExploreCompiler().Compile(strategy));
        var intent = result.Intent;

        await StrategyExecution.RunAsync(graph.Agent, intent, "strategy-binding-run");

        var context = graph.Agent.AcceptedExplorationContext;
        Assert.NotNull(context);
        Assert.Equal("strategy-binding-run", context.RunId);
        Assert.Same(intent.ExplorationSemantics, context.Semantics);
        Assert.Equal(intent.StrategyId, context.Semantics.StrategyId);
        Assert.Equal(intent.StrategyId, context.Semantics.RuntimeExecutionIntentReference);
        Assert.Equal(intent.Specification.MaximumDepth, context.Semantics.DeclaredMaximumDepth);
    }

    [Fact]
    public async Task LegacyOpenWorldExecutionHasNoAcceptedStrategyContext()
    {
        var graph = StrategyTestSupport.CreateGraph();
        var strategy = StrategyTestSupport.Explore();
        var intent = Assert.IsType<StrategyCompilationResult.Accepted>(
            StrategyTestSupport.ExploreCompiler().Compile(strategy)).Intent;
        var envelope = IntentSemanticEnvelope.Project(
            "legacy-open-world",
            intent.Goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(intent.Specification));

        await IntentExecution.RunOpenWorldAsync(graph.Agent, envelope, "legacy-binding-run", default);

        Assert.Null(graph.Agent.AcceptedExplorationContext);
        Assert.Null(graph.Agent.LatestAcceptedStrategyExecutionEvidenceView);
    }

    [Fact]
    public async Task MismatchedDeclaredDepthFailsBeforeRunStateTransition()
    {
        var graph = StrategyTestSupport.CreateGraph();
        var strategy = StrategyTestSupport.Explore(maximumDepth: 1);
        var intent = Assert.IsType<StrategyCompilationResult.Accepted>(
            StrategyTestSupport.ExploreCompiler().Compile(strategy)).Intent;
        var envelope = IntentSemanticEnvelope.Project(
            "strategy-open-world",
            intent.Goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(intent.Specification));
        var mismatched = new ExplorationExecutionSemantics(
            intent.ExplorationSemantics.StrategyId,
            intent.ExplorationSemantics.RuntimeExecutionIntentReference,
            intent.ExplorationSemantics.ContainerRule,
            intent.ExplorationSemantics.LeafRule,
            intent.ExplorationSemantics.DepthSemantics,
            intent.ExplorationSemantics.BoundaryDisposition,
            declaredMaximumDepth: 2);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            IntentExecution.RunStrategyOpenWorldAsync(graph.Agent, envelope, "mismatch-run", mismatched, default));
        Assert.Equal(RunState.Idle, graph.Agent.State);
        Assert.Null(graph.Agent.AcceptedExplorationContext);
    }

    [Fact]
    public async Task StrategyLedgerProjectionUsesOnlyTheAcceptedContext()
    {
        var graph = StrategyTestSupport.CreateGraph();
        var intent = Assert.IsType<StrategyCompilationResult.Accepted>(
            StrategyTestSupport.ExploreCompiler().Compile(StrategyTestSupport.Explore(maximumDepth: 1))).Intent;

        await StrategyExecution.RunAsync(graph.Agent, intent, "strategy-ledger-binding");

        var projection = graph.Agent.CompileExplorationLedgerView();
        var context = Assert.IsType<AcceptedExplorationRunContext>(graph.Agent.AcceptedExplorationContext);
        Assert.Equal(context.RunId, projection.RunId);
        Assert.Equal(context.Semantics.RuntimeExecutionIntentReference, projection.RuntimeExecutionIntentReference);
        Assert.Equal(context.Semantics.ContainerRule, projection.ContainerRule);
        Assert.Equal(context.Semantics.LeafRule, projection.LeafRule);
        Assert.Equal(context.Semantics.DepthSemantics, projection.DepthSemantics);
        Assert.Equal(context.Semantics.DeclaredMaximumDepth, projection.DeclaredMaximumDepth);
        var acceptedView = graph.Agent.LatestAcceptedStrategyExecutionEvidenceView;
        Assert.NotNull(acceptedView);
        Assert.Equal(context.RunId, acceptedView.RunId);
        Assert.Equal(context.Semantics.RuntimeExecutionIntentReference, acceptedView.RuntimeExecutionIntentReference);
        Assert.NotEmpty(projection.LedgerDigest);
        Assert.Contains(acceptedView.EvidenceViewDigest, projection.StructuralCorrelationMaterial, StringComparison.Ordinal);
        var method = typeof(RuntimeAgent).GetMethod(nameof(RuntimeAgent.CompileExplorationLedgerView));
        Assert.NotNull(method);
        Assert.Empty(method!.GetParameters());
    }

    [Fact]
    public async Task UnsatisfiedGoalEvidenceDoesNotBecomeCompletedFromStructuralFacts()
    {
        var graph = StrategyTestSupport.CreateGraph();
        var intent = Assert.IsType<StrategyCompilationResult.Accepted>(
            StrategyTestSupport.ExploreCompiler(evidenceSatisfied: false).Compile(StrategyTestSupport.Explore(maximumDepth: 1))).Intent;

        await StrategyExecution.RunAsync(graph.Agent, intent, "strategy-unsatisfied-structural");

        Assert.Equal(RunState.Failed, graph.Agent.State);
        Assert.Contains("Generic bounded strategy evidence satisfied by the Fake World.", graph.Agent.Reason, StringComparison.Ordinal);
        Assert.Contains("fresh GoalEvidence remains unsatisfied", graph.Agent.Reason, StringComparison.Ordinal);
        var view = Assert.IsType<StrategyExecutionEvidenceView>(graph.Agent.LatestAcceptedStrategyExecutionEvidenceView);
        Assert.NotEmpty(view.StructuralProgressFacts);
        Assert.NotEmpty(graph.Agent.CompileExplorationLedgerView().LedgerDigest);
        Assert.Equal(RunState.Failed, graph.Agent.State);
    }
}
