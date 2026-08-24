using System.Reflection;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Tests.Strategy;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

public sealed class StrategyContractAuthorityTests
{
    [Fact]
    public void StrategyModels_CarryNoDeviceActionOrGoalEvidenceOrLifecycleCommand()
    {
        var strategyTypes = new[]
        {
            typeof(StrategyDirective),
            typeof(StrategyObjective),
            typeof(StrategyScope),
            typeof(StrategyConstraintSet),
            typeof(StrategyCompletionCriteria),
            typeof(StrategyAdaptationBoundary),
            typeof(RuntimeExecutionIntent),
        };

        foreach (var type in strategyTypes)
        {
            Assert.DoesNotContain(type.GetProperties(BindingFlags.Public | BindingFlags.Instance), property =>
                typeof(DeviceAction).IsAssignableFrom(property.PropertyType)
                || typeof(GoalEvidence).IsAssignableFrom(property.PropertyType));
            Assert.DoesNotContain(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), method =>
                method.Name.Contains("Authorize", StringComparison.Ordinal)
                || method.Name.Contains("Complete", StringComparison.Ordinal)
                || method.Name.Contains("Transition", StringComparison.Ordinal)
                || method.Name.Contains("Dispatch", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void StrategyAdmission_ReportsButCannotTransitionRunState()
    {
        var stateProperty = typeof(StrategyRunAdmission).GetProperty(nameof(StrategyRunAdmission.RunState));

        Assert.NotNull(stateProperty);
        Assert.False(stateProperty!.CanWrite);
        Assert.DoesNotContain(
            typeof(StrategyRunAdmission).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name.Contains("Transition", StringComparison.Ordinal)
                || method.Name.Contains("Complete", StringComparison.Ordinal));
    }

    [Fact]
    public void RuntimeStrategySources_HaveNoTraversalFsmMultiRunOrScenarioKnowledge()
    {
        var files = new[]
        {
            TestRepositoryPaths.RepoPath("src", "UniClaw.Runtime", "Model", "StrategyDirective.cs"),
            TestRepositoryPaths.RepoPath("src", "UniClaw.Runtime", "Planning", "StrategyContract.cs"),
        };

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("UniClaw.Runtime.Traversal", source, StringComparison.Ordinal);
            Assert.DoesNotContain("StateMachine", source, StringComparison.Ordinal);
            Assert.DoesNotContain("StartRun", source, StringComparison.Ordinal);
            Assert.DoesNotContain("StartStrategyRun", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Settings", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("com.android", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("security", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("clickPlan", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task AcceptedIntent_UsesExistingAgentRunAndAgentOwnsTerminalState()
    {
        var graph = StrategyTestSupport.CreateGraph();
        var accepted = Assert.IsType<StrategyCompilationResult.Accepted>(
            StrategyTestSupport.ExploreCompiler().Compile(StrategyTestSupport.Explore()));

        await StrategyExecution.RunAsync(graph.Agent, accepted.Intent, "strategy-authority-run");

        Assert.True(
            graph.Agent.State == RunState.Completed,
            $"Expected Agent-owned completion, actual={graph.Agent.State}, reason={graph.Agent.Reason}; "
            + string.Join(" | ", graph.Agent.Trace.Select(entry => entry.Reason ?? entry.RunState?.ToString() ?? "event")));
        Assert.Contains(graph.Agent.Trace, entry => entry.RunState == RunState.Completed);
    }

    [Fact]
    public async Task TypedMatchIntent_UsesTheSameGenericFakeWorldExecutionSeam()
    {
        var graph = StrategyTestSupport.CreateGraph();
        var accepted = Assert.IsType<StrategyCompilationResult.Accepted>(
            StrategyTestSupport.InspectCompiler().Compile(StrategyTestSupport.Inspect()));

        await StrategyExecution.RunAsync(graph.Agent, accepted.Intent, "strategy-match-run");

        Assert.Equal(RunState.Completed, graph.Agent.State);
        Assert.All(graph.Agent.Trace, entry => Assert.Equal("strategy-match-run", entry.RunId));
    }

    [Fact]
    public async Task DeclaredCompletionCriterion_CannotCompleteWithoutAgentGoalEvidence()
    {
        var graph = StrategyTestSupport.CreateGraph();
        var accepted = Assert.IsType<StrategyCompilationResult.Accepted>(
            StrategyTestSupport.ExploreCompiler(evidenceSatisfied: false)
                .Compile(StrategyTestSupport.Explore()));

        await StrategyExecution.RunAsync(graph.Agent, accepted.Intent, "strategy-no-evidence-run");

        Assert.Equal(RunState.Failed, graph.Agent.State);
        Assert.DoesNotContain(graph.Agent.Trace, entry => entry.RunState == RunState.Completed);
    }
}
