using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
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

    [Fact]
    public void ReturnToUnexpectedContainerPausesWithoutAnotherEffect()
    {
        var scenario = TraversalScenarioFixture.Create(
            new[]
            {
                new TraversalScenarioFixture.Page("root", new[] { TraversalScenarioFixture.Link("A") }),
                new TraversalScenarioFixture.Page("A", new[]
                {
                    TraversalScenarioFixture.Link("C"), TraversalScenarioFixture.Back(),
                }),
                new TraversalScenarioFixture.Page("C", new[] { TraversalScenarioFixture.Back() }),
                new TraversalScenarioFixture.Page("unexpected", new[] { TraversalScenarioFixture.Back() }),
            },
            TraversalScenarioFixture.NavigationRules(
                TraversalScenarioFixture.NavigationOperation("visit-link")),
            expectedOverride: new ScenarioExpectation("TerminalNotProven", null, 3, 2, 0, null),
            continuePaused: _ => false);

        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);

        Assert.True(execution.Report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report) + "; traversal="
            + System.Text.Json.JsonSerializer.Serialize(execution.Host.KernelCore.TraversalExecution));
        Assert.Equal(new[] { "A", "C", "Back" }, scenario.Effects.Effects.Select(effect => effect.Descriptor));
        Assert.Equal(3, execution.Host.Facts.EffectReceipts.Count);
        var traversal = Assert.IsType<TraversalExecutionView>(execution.Host.KernelCore.TraversalExecution);
        Assert.Equal("traversal:return-failed", traversal.PauseReason);
        Assert.Contains(traversal.Gaps, gap => gap.Reason == "return-target-not-current");
        Assert.Equal(execution.Host.Facts.CurrentWorldClaims[WorldModel.CurrentContainerSubject],
            traversal.CurrentContainerId);
    }

    [Fact]
    public void MissingAfterVerifiedTargetStopsLaterHandlerAndSiblingEffect()
    {
        var seenHandlers = new List<string>();
        var agentCalls = 0;
        var required = TraversalScenarioFixture.EnglishHandler("require-after-target", requirePass: true);
        var later = TraversalScenarioFixture.EnglishHandler("must-not-run-after-target");
        var operation = TraversalScenarioFixture.NavigationOperation("open-settings",
            after: new[] { required, later });
        var scenario = TraversalScenarioFixture.Create(
            new[]
            {
                new TraversalScenarioFixture.Page("root", new[]
                {
                    TraversalScenarioFixture.Link("Settings"),
                    TraversalScenarioFixture.Link("More"),
                }),
                new TraversalScenarioFixture.Page("settings", new[]
                {
                    TraversalScenarioFixture.Link("Details"),
                }),
            },
            TraversalScenarioFixture.NavigationRules(operation),
            agentOverride: context =>
            {
                agentCalls++;
                return context.Phase == AgentDecisionPhase.InitialPlanning
                    ? TraversalScenarioFixture.Decide(context,
                        TraversalScenarioFixture.NavigationRules(operation))
                    : null;
            },
            expectedOverride: new ScenarioExpectation("WaitingForInput", null, 1, 2, 0, null),
            inspectObserver: (handler, input) => seenHandlers.Add(handler.HandlerId));
        var phased = scenario.Options with { Phased = true };

        var pending = ScenarioRunner.Run(scenario.Bundle, phased);

        Assert.False(pending.Report.AcceptancePassed);
        Assert.StartsWith("PhasedPending:", pending.Report.Reason, StringComparison.Ordinal);
        Assert.Equal(2, agentCalls);
        Assert.Single(scenario.Effects.Effects);
        Assert.Single(pending.Host.Facts.EffectReceipts);
        Assert.Equal(new[] { "require-after-target" }, seenHandlers);
        var traversal = Assert.IsType<TraversalExecutionView>(pending.Host.KernelCore.TraversalExecution);
        Assert.Contains(traversal.Handlers, handler => handler.HandlerId == "require-after-target"
            && handler.Phase == "AfterVerified"
            && handler.Result?.Disposition == FindingDisposition.Unknown);
        Assert.DoesNotContain(traversal.Handlers, handler => handler.HandlerId == "must-not-run-after-target");
        Assert.Equal("traversal:handler-require-pass", traversal.PauseReason);
        var consultations = pending.Host.Consultations.Calls.Count;
        var deliveries = pending.Host.EffectDeliveryCount;
        var queried = pending.Host.Driver.TraversalExecution;
        Assert.Same(queried, pending.Host.Driver.TraversalExecution);
        Assert.Equal(consultations, pending.Host.Consultations.Calls.Count);
        Assert.Equal(deliveries, pending.Host.EffectDeliveryCount);
        Assert.Single(scenario.Effects.Effects);
    }

    [Fact]
    public void ContradictoryAfterVerifiedFindingStopsLaterHandler()
    {
        var inspected = new List<string>();
        var agentCalls = 0;
        var required = TraversalScenarioFixture.EnglishHandler("require-pass", requirePass: true);
        var later = TraversalScenarioFixture.EnglishHandler("later-after-handler");
        var operation = TraversalScenarioFixture.ReadOnlyOperation("inspect-current-target",
            after: new[] { required, later });
        var scenario = TraversalScenarioFixture.Create(
            new[] { new TraversalScenarioFixture.Page("root", new[]
            {
                TraversalScenarioFixture.ReadOnlyTarget("menuItem", "Settings"),
            }) },
            new[] { new TraversalRule("inspect", "readOnly", operation) },
            agentOverride: context =>
            {
                agentCalls++;
                return context.Phase == AgentDecisionPhase.InitialPlanning
                    ? TraversalScenarioFixture.Decide(context,
                        new[] { new TraversalRule("inspect", "readOnly", operation) })
                    : null;
            },
            inspectOverride: (handler, input) =>
            {
                inspected.Add(handler.HandlerId);
                var correlation = new CapabilityCorrelation(input.RunId, operationId: input.OperationId,
                    domainCorrelation: input.SourceRevisionId);
                return new Finding("simulated-contradictory-post", correlation, handler.ProtocolVersion,
                    handler.CapabilityId, FindingDisposition.Violation, CapabilityStatus.Completed,
                    "simulated verified result contradicts the required pass condition");
            },
            expectedOverride: new ScenarioExpectation("WaitingForInput", null, 0, 2, 0, null));
        var phased = scenario.Options with { Phased = true };

        var pending = ScenarioRunner.Run(scenario.Bundle, phased);

        Assert.False(pending.Report.AcceptancePassed);
        Assert.StartsWith("PhasedPending:", pending.Report.Reason, StringComparison.Ordinal);
        Assert.Equal(2, agentCalls);
        Assert.Empty(scenario.Effects.Effects);
        Assert.Empty(pending.Host.Facts.EffectReceipts);
        Assert.Equal(new[] { "require-pass" }, inspected);
        var traversal = Assert.IsType<TraversalExecutionView>(pending.Host.KernelCore.TraversalExecution);
        Assert.Single(traversal.Handlers);
        Assert.Equal(FindingDisposition.Violation, traversal.Handlers[0].Result!.Disposition);
        Assert.Equal("traversal:handler-require-pass", traversal.PauseReason);
    }

    [Fact]
    public void MissingPostActionObservationWaitsWithoutAfterHandlerOrSiblingEffect()
    {
        var inspectedHandlers = new List<string>();
        var after = TraversalScenarioFixture.EnglishHandler("after-verified");
        var operation = TraversalScenarioFixture.NavigationOperation("open-settings",
            after: new[] { after });
        var scenario = TraversalScenarioFixture.Create(
            new[] { new TraversalScenarioFixture.Page("root", new[]
            {
                TraversalScenarioFixture.Link("Settings"),
                TraversalScenarioFixture.Link("More"),
            }) },
            TraversalScenarioFixture.NavigationRules(operation),
            expectedOverride: new ScenarioExpectation("WaitingForInput", null, 1, 1, 0, null),
            inspectObserver: (handler, _) => inspectedHandlers.Add(handler.HandlerId));

        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);

        Assert.True(execution.Report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report));
        Assert.Single(scenario.Effects.Effects);
        Assert.Single(execution.Host.Facts.EffectReceipts);
        Assert.Empty(inspectedHandlers);
        var traversal = Assert.IsType<TraversalExecutionView>(execution.Host.KernelCore.TraversalExecution);
        Assert.Empty(traversal.Handlers);
        Assert.Equal("post-action-evidence", execution.Report.Reason);
        Assert.Empty(scenario.Effects.Effects.Skip(1));
    }

    [Fact]
    public void ContradictoryNavigationPostIsRejectedBeforeAfterHandlerOrNextEffect()
    {
        var inspectedHandlers = new List<string>();
        var after = TraversalScenarioFixture.EnglishHandler("must-not-inspect-unverified-target");
        var operation = TraversalScenarioFixture.NavigationOperation("open-A", after: new[] { after });
        var scenario = TraversalScenarioFixture.Create(
            new[]
            {
                new TraversalScenarioFixture.Page("root", new[]
                {
                    TraversalScenarioFixture.Link("A"),
                    TraversalScenarioFixture.Link("B"),
                }),
                new TraversalScenarioFixture.Page("root", new[]
                {
                    TraversalScenarioFixture.Link("B"),
                }),
            },
            TraversalScenarioFixture.NavigationRules(operation),
            expectedOverride: new ScenarioExpectation("TerminalNotProven", null, 1, 2, 0, null),
            inspectObserver: (handler, _) => inspectedHandlers.Add(handler.HandlerId));

        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);

        Assert.True(execution.Report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report) + "; traversal="
            + System.Text.Json.JsonSerializer.Serialize(execution.Host.KernelCore.TraversalExecution));
        Assert.Single(scenario.Effects.Effects);
        Assert.Single(execution.Host.Facts.EffectReceipts);
        Assert.Empty(inspectedHandlers);
        var verification = Assert.Single(execution.Host.Facts.PostActionVerifications);
        Assert.False(verification.IsVerified);
        Assert.Equal("post-action-target-unique", verification.RejectionReason);
        var attempt = Assert.Single(execution.Host.Consultations.Calls.Last().PlanExecution!.Attempts);
        Assert.Equal(PlanAttemptStatus.Rejected, attempt.Status);
        var traversal = Assert.IsType<TraversalExecutionView>(execution.Host.KernelCore.TraversalExecution);
        Assert.Equal(PlanExecutionStatus.Abandoned, traversal.Status);
        Assert.Equal("traversal:step-chain-failed", traversal.EndReason);
        Assert.Empty(traversal.Handlers);
    }

    [Fact]
    public void StaleTraversalProjectionCannotDispatchAnEffect()
    {
        var scenario = TraversalScenarioFixture.Create(
            new[] { new TraversalScenarioFixture.Page("root", new[]
            {
                TraversalScenarioFixture.Link("A"),
            }) },
            TraversalScenarioFixture.NavigationRules(
                TraversalScenarioFixture.NavigationOperation("visit-stale-projection")),
            expectedOverride: new ScenarioExpectation("TerminalNotProven", null, 0, 2, 0, null),
            continuePaused: _ => false,
            projectionOverride: (kernel, source) =>
            {
                var observation = new TraversalScenarioFixture.EffectCapture().Project(kernel, source);
                return observation is null ? null : observation with
                {
                    Graph = observation.Graph with { SourceRevisionId = "revision:not-current" },
                };
            });

        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);

        Assert.True(execution.Report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report) + "; traversal="
            + System.Text.Json.JsonSerializer.Serialize(execution.Host.KernelCore.TraversalExecution));
        Assert.Empty(scenario.Effects.Effects);
        Assert.Empty(execution.Host.Facts.EffectReceipts);
        Assert.Equal("evidence-insufficient", execution.Report.Reason);
        var traversal = Assert.IsType<TraversalExecutionView>(execution.Host.KernelCore.TraversalExecution);
        Assert.Equal("traversal:stale-or-unassociated-projection", traversal.PauseReason);
        Assert.Contains(traversal.Gaps, gap => gap.Reason == "traversal:stale-or-unassociated-projection");
    }

    [Fact]
    public void ValidWorldPageOutsideTraversalPathPausesBeforeConsumingItsCandidates()
    {
        var injected = false;
        var issuedPlan = false;
        string? traversalPage = null;
        string? worldPage = null;
        string? recoveryPause = null;
        PlanExecutionStatus? recoveryStatus = null;
        var projectedSurpriseCandidate = false;
        var rules = TraversalScenarioFixture.NavigationRules(
            TraversalScenarioFixture.NavigationOperation("visit-link"));
        TraversalScenarioFixture.Scenario? scenario = null;
        scenario = TraversalScenarioFixture.Create(
            new[]
            {
                new TraversalScenarioFixture.Page("root", new[]
                {
                    new TraversalScenarioFixture.MenuEntry("menuItem", "Ambiguous", Traverse: true,
                        Disposition: PlanCandidateDisposition.Unknown, Reason: "ambiguous-current-page-entry"),
                }),
            },
            rules,
            maxDepth: 2,
            expectedOverride: new ScenarioExpectation("TerminalNotProven", null, 0, 3, 0, null),
            agentOverride: context =>
            {
                if (!issuedPlan)
                {
                    issuedPlan = true;
                    return TraversalScenarioFixture.Decide(context, rules, maxDepth: 2);
                }

                if (context.PlanExecution?.Traversal is { Status: PlanExecutionStatus.Paused } traversal)
                {
                    if (!injected)
                    {
                        injected = true;
                        var kernel = scenario!.Effects.CurrentKernel!;
                        traversalPage = kernel.CurrentBelief!.WorldState[WorldModel.CurrentContainerSubject].Value;
                        kernel.Process(Observation("ui.screen", "unrelated", 30, kernel.CurrentBelief!.WorldState["ui.screen"].EstablishingProducer!));
                        worldPage = kernel.CurrentBelief!.WorldState[WorldModel.CurrentContainerSubject].Value;
                        kernel.Process(Observation("ui.screen.route", "unrelated", 31, kernel.CurrentBelief!.WorldState["ui.screen.route"].EstablishingProducer!));
                        kernel.Process(Observation("menu.ready", System.Text.Json.JsonSerializer.Serialize(new
                        {
                            Complete = true,
                            Items = new[]
                            {
                                new
                                {
                                    Role = "menuItem", Descriptor = "Surprise", Traverse = true,
                                    ReadOnly = false, State = (string?)null,
                                    Disposition = PlanCandidateDisposition.Ready, Reason = (string?)null,
                                },
                            },
                        }), 32, kernel.CurrentBelief!.WorldState["menu.ready"].EstablishingProducer!));
                        projectedSurpriseCandidate = scenario.Effects.Project(kernel,
                            TraversalScenarioFixture.Source)?.Candidates.Any(candidate =>
                                candidate.Target.TargetDescriptor == "Surprise") == true;
                        // The external observation is accepted while the traversal is paused;
                        // resume will request a fresh, pure projection of this new World.
                        return new AgentDecision.ContinuePlan(context.DecisionId, context.PlanExecution.Position);
                    }

                    recoveryPause = traversal.PauseReason;
                    recoveryStatus = traversal.Status;
                }

                return new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId,
                    "stop after the World page leaves the active traversal path"));
            });

        var activeScenario = scenario ?? throw new InvalidOperationException("scenario was not created");
        var execution = ScenarioRunner.Run(activeScenario.Bundle, activeScenario.Options);

        Assert.True(injected, "the paused recovery boundary should accept the later World observation");
        Assert.NotNull(traversalPage);
        Assert.NotNull(worldPage);
        Assert.NotEqual(traversalPage, worldPage);
        Assert.True(projectedSurpriseCandidate, "the new World revision must project an executable Surprise candidate");
        Assert.Empty(activeScenario.Effects.Effects);
        Assert.Empty(execution.Host.Facts.EffectReceipts);
        Assert.Equal("traversal:control-path-current-page-mismatch", recoveryPause);
        Assert.Equal(PlanExecutionStatus.Paused, recoveryStatus);
        Assert.True(execution.Report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(activeScenario.Bundle, execution.Report) + "; traversal="
            + System.Text.Json.JsonSerializer.Serialize(execution.Host.KernelCore.TraversalExecution));
        var traversal = Assert.IsType<TraversalExecutionView>(execution.Host.KernelCore.TraversalExecution);
        Assert.Equal(worldPage, traversal.CurrentContainerId);
        Assert.Contains(traversal.Gaps, gap => gap.Reason == "control-path-current-page-mismatch");
        Assert.DoesNotContain(traversal.Operations, operation => operation.TargetKey?.EndsWith(":Surprise", StringComparison.Ordinal) == true);
    }

    private static ObservationProposal Observation(string subject, string value, int second, string producer) => new(
        new ObservationClaim(subject, value), IngressKind.Observation, ObservationContext.External,
        new Provenance(producer, new DateTimeOffset(2026, 10, 20, 0, 0, second, TimeSpan.Zero),
            $"scope:{subject}", new[] { $"capture:{subject}:{second}" }));
}
