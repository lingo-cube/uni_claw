using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>Scenario coverage proving V2 current and execution context stay distinct.</summary>
public sealed class ContainerRuntimeV2LiveStateReplacementScenarioTests
{
    /// <summary>Normal navigation advances the V2 current projection without changing Agent authority.</summary>
    [Fact]
    public async Task NormalNavigationRetainsV2CurrentAndExecutionReadPaths()
    {
        var harness = ScenarioHarness.Create("happy");

        var state = await harness.RunAsync();

        Assert.Equal(RunState.Completed, state);
        Assert.Equal(harness.Agent.Belief?.SemanticPage, harness.Agent.ContainerContext.CurrentObservedLocation);
        Assert.Equal("WiFiSettings", harness.Agent.ContainerContext.ActiveExecutionContainer);
        Assert.NotNull(harness.Agent.ContainerContext.LatestTransition);
        Assert.All(harness.Agent.ContainerTransitions, transition =>
            Assert.NotEqual(ContainerTransitionDisposition.NO_COMMIT_FAIL_CLOSED, transition.Disposition));
    }

    /// <summary>Recovery keeps the compatibility projection readable while preserving execution semantics.</summary>
    [Fact]
    public async Task RecoveryScenarioDoesNotCreateASecondCurrentAuthority()
    {
        var harness = ScenarioHarness.Create("launcher-drift");

        await harness.RunAsync();

        Assert.NotNull(harness.Agent.Belief);
        Assert.NotNull(harness.Agent.ContainerContext.CurrentObservedLocation);
        Assert.NotNull(harness.Agent.ContainerContext.ActiveExecutionContainer);
        Assert.DoesNotContain(
            harness.Agent.ContainerTransitions,
            transition => transition.Disposition == ContainerTransitionDisposition.NO_COMMIT_FAIL_CLOSED);
    }
}
