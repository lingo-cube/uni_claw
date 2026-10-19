using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Simulation.Tests;

public sealed class TraversalScenarioTests
{
    [Fact]
    public void DeepBranchDoesNotHideDWhenTheSamePageIsReachedShallower()
    {
        var scenario = TraversalScenarioFixture.DeepThenShallow();

        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);

        Assert.True(execution.Report.AcceptancePassed,
            $"runner={execution.Report.RunDriveStatus}:{execution.Report.Reason}; "
            + ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report) + "; contexts="
            + string.Join(" | ", execution.Host.Consultations.Calls.Select(context =>
                $"{context.Phase}:{context.FailureReason}:plan={context.PlanExecution?.Status}:"
                + $"pause={context.PlanExecution?.Traversal?.PauseReason}:"
                + $"interrupt={context.PlanExecution?.InterruptionReason}")) + "; traversal="
            + System.Text.Json.JsonSerializer.Serialize(execution.Host.KernelCore.TraversalExecution)
            + "; world=" + System.Text.Json.JsonSerializer.Serialize(execution.Host.Facts.CurrentWorldClaims));
        Assert.Equal(8, execution.Host.Facts.EffectReceipts.Count);
        Assert.All(execution.Host.Facts.EffectReceipts,
            receipt => Assert.Equal(DispatchOutcome.DeliveryCompleted, receipt.Outcome));
        Assert.Equal(
            new[] { "A", "C", "Back", "Back", "C", "D", "Back", "Back" },
            scenario.Effects.Effects.Select(effect => effect.Descriptor));
        Assert.Equal(
            new[] { "root", "A", "C", "A", "root", "C", "D", "C" },
            scenario.Effects.Effects.Select(effect => effect.Page));
        var navigationGraph = execution.Host.KernelCore.ProjectWorldGraph(
            $"{execution.Host.KernelCore.RunId}:{execution.Host.KernelCore.RunView?.Version}");
        Assert.NotNull(navigationGraph);
        Assert.Equal(4, navigationGraph.Nodes.Count);
        Assert.Equal(8, navigationGraph.Edges.Count);
        Assert.All(navigationGraph.Edges, edge => Assert.Contains(edge.SourceContainerId,
            navigationGraph.Nodes.Select(node => node.Identity.ContainerId)));
        Assert.All(navigationGraph.Edges, edge => Assert.Contains(edge.TargetContainerId,
            navigationGraph.Nodes.Select(node => node.Identity.ContainerId)));

        var traversal = Assert.IsType<TraversalExecutionView>(execution.Host.KernelCore.TraversalExecution);
        Assert.Contains(traversal.Gaps, gap => gap.CandidateKey?.EndsWith(":D", StringComparison.Ordinal) == true
            && gap.Reason == "max-depth");
        Assert.Equal(8, traversal.CompletedWork.Count);
        Assert.Equal(PlanExecutionStatus.Completed, traversal.Status);
        Assert.Equal("partial", traversal.EndReason);
    }

    [Fact]
    public void FirstUnknownRulePreventsTheLaterNavigationRule()
    {
        var scenario = TraversalScenarioFixture.Create(
            new[] { new TraversalScenarioFixture.Page("root", new[]
            {
                TraversalScenarioFixture.Link("A"),
                TraversalScenarioFixture.Back(),
            }) },
            new[]
            {
                new TraversalRule("unknown-first", "not-projected", TraversalScenarioFixture.NavigationOperation("unknown-op")),
                new TraversalRule("navigation-second", "navigation", TraversalScenarioFixture.NavigationOperation("navigation-op")),
            }, expectedOverride: new ScenarioExpectation("AgentDecisionFailed", null, 0, 16, 0, null));

        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);

        Assert.True(execution.Report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report) + "; effects="
            + System.Text.Json.JsonSerializer.Serialize(scenario.Effects.Effects) + "; traversal="
            + System.Text.Json.JsonSerializer.Serialize(execution.Host.KernelCore.TraversalExecution) + "; contexts="
            + string.Join(" | ", execution.Host.Consultations.Calls.Select(context =>
                $"{context.Phase}:{context.FailureReason}:pause={context.PlanExecution?.Traversal?.PauseReason}:"
                + $"interrupt={context.PlanExecution?.InterruptionReason}")));
        Assert.Empty(scenario.Effects.Effects);
        Assert.Equal(0, execution.Host.Facts.EffectReceipts.Count);
        var traversal = Assert.IsType<TraversalExecutionView>(execution.Host.KernelCore.TraversalExecution);
        Assert.DoesNotContain(traversal.Operations, operation => operation.TemplateId == "navigation-op");
        Assert.Contains(traversal.Gaps, gap => gap.Reason == "condition-unknown:not-projected");
    }

    [Fact]
    public void ReadOnlyTargetIsInspectedWithoutDispatchingAnEffect()
    {
        var scenario = TraversalScenarioFixture.Create(
            new[] { new TraversalScenarioFixture.Page("root", new[]
            {
                TraversalScenarioFixture.ReadOnlyTarget("switch", "Airplane mode"),
            }) },
            new[]
            {
                new TraversalRule("inspect-switch", "readOnly", TraversalScenarioFixture.ReadOnlyOperation("inspect-switch-op")),
            });

        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);

        Assert.True(execution.Report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report));
        Assert.Empty(scenario.Effects.Effects);
        Assert.Equal(0, execution.Host.Facts.EffectReceipts.Count);
        var traversal = Assert.IsType<TraversalExecutionView>(execution.Host.KernelCore.TraversalExecution);
        Assert.Equal(PlanExecutionStatus.Completed, traversal.Status);
        Assert.Contains(traversal.Operations, operation => operation.TemplateId == "inspect-switch-op" && operation.Completed);
    }

    [Fact]
    public void BlockedMenuCandidateIsRecordedWithoutAnEffect()
    {
        var scenario = TraversalScenarioFixture.Create(
            new[] { new TraversalScenarioFixture.Page("root", new[]
            {
                TraversalScenarioFixture.BlockedLink("Restricted page"),
            }) },
            TraversalScenarioFixture.NavigationRules(
                TraversalScenarioFixture.NavigationOperation("visit-restricted")));

        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);

        Assert.True(execution.Report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report));
        Assert.Empty(scenario.Effects.Effects);
        Assert.Empty(execution.Host.Facts.EffectReceipts);
        var traversal = Assert.IsType<TraversalExecutionView>(execution.Host.KernelCore.TraversalExecution);
        Assert.Contains(traversal.Gaps, gap => gap.CandidateKey?.EndsWith(":Restricted page", StringComparison.Ordinal) == true
            && gap.Reason == "simulated-policy-block");
    }

    [Fact]
    public void EffectOutsideTheAcceptedContractIsRejectedBeforeDelivery()
    {
        var unauthorized = new TraversalOperation("unauthorized-op", TraversalOperationKind.Navigate,
            new AgentActionStep("$candidate", "$candidate", "delete", null),
            Array.Empty<PlanLanguageHandler>(), Array.Empty<PlanLanguageHandler>());
        var scenario = TraversalScenarioFixture.Create(
            new[] { new TraversalScenarioFixture.Page("root", new[]
            {
                TraversalScenarioFixture.Link("A"),
            }) },
            TraversalScenarioFixture.NavigationRules(unauthorized),
            expectedOverride: new ScenarioExpectation("AgentDecisionFailed", null, 0, 1, 0, null));

        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);

        Assert.True(execution.Report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report));
        Assert.Empty(scenario.Effects.Effects);
        Assert.Empty(execution.Host.Facts.EffectReceipts);
        Assert.Contains("invalid-traversal-step", execution.Report.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AncestorCycleSynchronizesCurrentPageAndStopsTraversalEffects()
    {
        var scenario = TraversalScenarioFixture.Create(
            new[]
            {
                new TraversalScenarioFixture.Page("root", new[] { TraversalScenarioFixture.Link("A") }),
                new TraversalScenarioFixture.Page("A", new[] { TraversalScenarioFixture.Link("C") }),
                new TraversalScenarioFixture.Page("C", new[] { TraversalScenarioFixture.Link("root") }),
                new TraversalScenarioFixture.Page("root", new[] { TraversalScenarioFixture.Link("A") }),
            },
            TraversalScenarioFixture.NavigationRules(
                TraversalScenarioFixture.NavigationOperation("visit-link")),
            maxDepth: 3,
            expectedOverride: new ScenarioExpectation("TerminalNotProven", null, 3, 2, 0, null),
            continuePaused: context => context.PlanExecution?.Traversal?.PauseReason != "traversal:navigation-cycle");

        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);

        Assert.True(execution.Report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report) + "; effects="
            + System.Text.Json.JsonSerializer.Serialize(scenario.Effects.Effects) + "; traversal="
            + System.Text.Json.JsonSerializer.Serialize(execution.Host.KernelCore.TraversalExecution) + "; contexts="
            + string.Join(" | ", execution.Host.Consultations.Calls.Select(context =>
                $"{context.Phase}:{context.FailureReason}:pause={context.PlanExecution?.Traversal?.PauseReason}:"
                + $"interrupt={context.PlanExecution?.InterruptionReason}")));
        Assert.Equal(new[] { "A", "C", "root" }, scenario.Effects.Effects.Select(effect => effect.Descriptor));
        Assert.Equal(3, execution.Host.Facts.EffectReceipts.Count);
        var traversal = Assert.IsType<TraversalExecutionView>(execution.Host.KernelCore.TraversalExecution);
        Assert.Equal("traversal:navigation-cycle", traversal.PauseReason);
        Assert.Equal(0, traversal.Depth);
        Assert.Equal(execution.Host.Facts.CurrentWorldClaims[WorldModel.CurrentContainerSubject],
            traversal.CurrentContainerId);
        Assert.DoesNotContain(traversal.Operations, operation => operation.Completed == false);
    }

    [Fact]
    public void IncompleteDiscoveryLeavesAnExplicitCoverageGap()
    {
        var scenario = TraversalScenarioFixture.Create(
            new[] { new TraversalScenarioFixture.Page("root", Array.Empty<TraversalScenarioFixture.MenuEntry>(), Complete: false) },
            TraversalScenarioFixture.NavigationRules(
                TraversalScenarioFixture.NavigationOperation("visit-link")));

        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);

        Assert.True(execution.Report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report));
        Assert.Empty(scenario.Effects.Effects);
        Assert.Empty(execution.Host.Facts.EffectReceipts);
        var traversal = Assert.IsType<TraversalExecutionView>(execution.Host.KernelCore.TraversalExecution);
        Assert.Contains(traversal.Gaps, gap => gap.Reason == "discovery-incomplete");
        Assert.Equal("partial", traversal.EndReason);
    }

    [Fact]
    public void EqualDepthSharedPageRechecksItsUpdatedReadOnlyObligation()
    {
        var observedStates = new List<string?>();
        TraversalScenarioFixture.Scenario? scenario = null;
        var operation = TraversalScenarioFixture.ReadOnlyOperation("inspect-switch",
            before: new[] { TraversalScenarioFixture.EnglishHandler("switch-language") });
        scenario = TraversalScenarioFixture.Create(
            new[]
            {
                new TraversalScenarioFixture.Page("root", new[]
                {
                    TraversalScenarioFixture.Link("A"), TraversalScenarioFixture.Link("B"),
                }),
                new TraversalScenarioFixture.Page("A", new[]
                {
                    TraversalScenarioFixture.Link("C"), TraversalScenarioFixture.Back(),
                }),
                new TraversalScenarioFixture.Page("C", new[]
                {
                    TraversalScenarioFixture.ReadOnlyTarget("switch", "Airplane mode", "unchecked"),
                    TraversalScenarioFixture.Back(),
                }),
                new TraversalScenarioFixture.Page("A", new[]
                {
                    TraversalScenarioFixture.Link("C"), TraversalScenarioFixture.Back(),
                }),
                new TraversalScenarioFixture.Page("root", new[]
                {
                    TraversalScenarioFixture.Link("A"), TraversalScenarioFixture.Link("B"),
                }),
                new TraversalScenarioFixture.Page("B", new[]
                {
                    TraversalScenarioFixture.Link("C"), TraversalScenarioFixture.Back(),
                }),
                new TraversalScenarioFixture.Page("C", new[]
                {
                    TraversalScenarioFixture.ReadOnlyTarget("switch", "Airplane mode", "checked"),
                    TraversalScenarioFixture.Back(),
                }),
                new TraversalScenarioFixture.Page("B", new[]
                {
                    TraversalScenarioFixture.Link("C"), TraversalScenarioFixture.Back(),
                }),
                new TraversalScenarioFixture.Page("root", new[]
                {
                    TraversalScenarioFixture.Link("A"), TraversalScenarioFixture.Link("B"),
                }),
            },
            new[]
            {
                new TraversalRule("navigate-links", "navigation",
                    TraversalScenarioFixture.NavigationOperation("visit-link")),
                new TraversalRule("inspect-switch", "readOnly", operation),
            },
            expectedOverride: new ScenarioExpectation("TerminalNotProven", null, 8, 2, 0, null),
            inspectObserver: (_, input) =>
            {
                var belief = scenario?.Effects.CurrentKernel?.CurrentBelief;
                var targetContainerId = input.TargetContainerId ?? input.ContainerId;
                var state = belief?.Occurrences?.SingleOrDefault(item => item.OwningContainerId == targetContainerId
                    && item.Role == input.Target.TargetRole
                    && item.SemanticDescriptor == input.Target.TargetDescriptor)?.State;
                observedStates.Add(state);
            });

        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);

        Assert.True(execution.Report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report) + "; traversal="
            + System.Text.Json.JsonSerializer.Serialize(execution.Host.KernelCore.TraversalExecution));
        Assert.Equal(new[] { "A", "C", "Back", "Back", "B", "C", "Back", "Back" },
            scenario.Effects.Effects.Select(effect => effect.Descriptor));
        Assert.Equal(new[] { "unchecked", "checked" }, observedStates);
        var traversal = Assert.IsType<TraversalExecutionView>(execution.Host.KernelCore.TraversalExecution);
        Assert.Equal(2, traversal.Handlers.Count(handler => handler.HandlerId == "switch-language"));
        Assert.Equal("completed", traversal.EndReason);
    }
}
