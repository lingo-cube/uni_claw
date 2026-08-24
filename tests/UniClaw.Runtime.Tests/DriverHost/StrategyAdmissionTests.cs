using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Tests.Strategy;
using Xunit;

namespace UniClaw.Runtime.Tests.DriverHost;

public sealed class StrategyAdmissionTests
{
    [Fact]
    public void UnsupportedCriterion_IsRejectedBeforeGraphResolutionOrRunCreation()
    {
        var graphCalls = 0;
        var observability = new DriverHostObservability();
        var coordinator = new RunExecutionCoordinator(
            observability,
            _ =>
            {
                graphCalls++;
                throw new InvalidOperationException("Graph resolution must not run for rejected strategy semantics.");
            },
            strategyCompiler: StrategyTestSupport.InspectCompiler());

        var admission = coordinator.StartStrategyRun(
            StrategyTestSupport.Request(
                StrategyTestSupport.Inspect(criterionId: "unsupported-criterion")));

        Assert.False(admission.Accepted);
        Assert.Equal(StrategyRejectionCode.UnsupportedCriterion, admission.RejectionCode);
        Assert.Null(admission.RunId);
        Assert.Null(admission.RunState);
        Assert.Equal(0, graphCalls);
        Assert.Empty(observability.RegisteredRunIds);
    }

    [Fact]
    public void OneStrategyIdentity_CreatesAtMostOneRun()
    {
        var observability = new DriverHostObservability();
        var coordinator = new RunExecutionCoordinator(
            observability,
            _ => StrategyTestSupport.CreateGraph(),
            strategyCompiler: StrategyTestSupport.ExploreCompiler());
        var request = StrategyTestSupport.Request(StrategyTestSupport.Explore("strategy-idempotent"));

        var first = coordinator.StartStrategyRun(request);
        var second = coordinator.StartStrategyRun(request);

        Assert.True(first.Accepted);
        Assert.False(string.IsNullOrWhiteSpace(first.RunId));
        Assert.False(second.Accepted);
        Assert.Equal(StrategyRejectionCode.DuplicateStrategy, second.RejectionCode);
        Assert.Single(observability.RegisteredRunIds);
        Assert.Equal(first.RunId, observability.RegisteredRunIds[0]);
    }
}
