using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Simulation.Tests;

public sealed class HandlerTraversalScenarioTests
{
    [Fact]
    public void RegisteredInspectorViolationIsRecordedAndReadOnlyBeforeAfterOrderIsPreserved()
    {
        var before = TraversalScenarioFixture.EnglishHandler("before");
        var after = TraversalScenarioFixture.EnglishHandler("after");
        var operation = TraversalScenarioFixture.ReadOnlyOperation("inspect-menu",
            new[] { before }, new[] { after });
        var scenario = TraversalScenarioFixture.Create(
            new[] { new TraversalScenarioFixture.Page("root", new[]
            {
                TraversalScenarioFixture.ReadOnlyTarget("menuItem", "设置"),
            }) },
            new[] { new TraversalRule("inspect-readonly", "readOnly", operation) },
            expectedOverride: new ScenarioExpectation("TerminalNotProven", null, 0, 2, 0, null));

        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);

        Assert.True(execution.Report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report) + "; traversal="
            + System.Text.Json.JsonSerializer.Serialize(execution.Host.KernelCore.TraversalExecution));
        Assert.Empty(scenario.Effects.Effects);
        Assert.Empty(execution.Host.Facts.EffectReceipts);
        var traversal = Assert.IsType<TraversalExecutionView>(execution.Host.KernelCore.TraversalExecution);
        Assert.Collection(traversal.Handlers,
            record => Assert.Equal("Before", record.Phase),
            record => Assert.Equal("AfterVerified", record.Phase));
        Assert.Equal(new[] { "before", "after" }, traversal.Handlers.Select(record => record.HandlerId));
        Assert.All(traversal.Handlers, record =>
        {
            Assert.Equal(PlanHandlerCallStatus.Completed, record.Status);
            Assert.Equal(FindingDisposition.Violation, record.Result!.Disposition);
            Assert.Equal(CapabilityStatus.Completed, record.Result.Status);
            Assert.Equal(record.OperationId, record.Result.Correlation.OperationId);
            Assert.Equal(execution.Host.Facts.Revisions[^1].RevisionId, record.SourceRevisionId);
            Assert.Equal(record.SourceRevisionId, record.Result.Correlation.DomainCorrelation);
        });
        Assert.Equal(traversal.Handlers[0].OperationId, traversal.Handlers[1].OperationId);
        Assert.Contains(traversal.Operations!, operationRecord =>
            operationRecord.TemplateId == "inspect-menu" && operationRecord.Completed
            && operationRecord.ObservedState == "unchecked");
        Assert.Equal(0, traversal.KnownRemainingWork);
    }

    [Fact]
    public void RequirePassUnknownAfterNavigationPausesAndExplicitContinueDoesNotRepeatEffect()
    {
        var after = TraversalScenarioFixture.EnglishHandler("after-required", requirePass: true);
        var operation = TraversalScenarioFixture.NavigationOperation("open-settings",
            after: new[] { after });
        var agentCalls = 0;
        var recovery = new List<UniClaw.Kernel.Runtime.TraversalRecoveryFacts>();
        var scenario = TraversalScenarioFixture.Create(
            new[]
            {
                new TraversalScenarioFixture.Page("root", new[] { TraversalScenarioFixture.Link("设置") }),
                new TraversalScenarioFixture.Page("settings", new[] { TraversalScenarioFixture.Back() }),
            },
            TraversalScenarioFixture.NavigationRules(operation),
            agentOverride: context =>
            {
                agentCalls++;
                if (context.Phase == AgentDecisionPhase.InitialPlanning)
                    return TraversalScenarioFixture.Decide(context,
                        TraversalScenarioFixture.NavigationRules(operation));
                if (agentCalls == 2 || agentCalls == 4) return null;
                return new AgentDecision.ContinuePlan(context.DecisionId, context.PlanExecution!.Position);
            },
            recoveryObserver: recovery.Add,
            expectedOverride: new ScenarioExpectation("WaitingForInput", null, 1, 4, 0, null));
        var phased = scenario.Options with { Phased = true };

        var pending = ScenarioRunner.Run(scenario.Bundle, phased);

        Assert.False(pending.Report.AcceptancePassed);
        Assert.StartsWith("PhasedPending:", pending.Report.Reason, StringComparison.Ordinal);
        Assert.Equal(1, pending.Host.Facts.EffectReceipts.Count);
        var paused = Assert.IsType<TraversalExecutionView>(pending.Host.KernelCore.TraversalExecution);
        Assert.Equal(PlanExecutionStatus.Paused, paused.Status);
        Assert.Contains(paused.Handlers, record => record.Phase == "AfterVerified"
            && record.Status == PlanHandlerCallStatus.Completed
            && record.Result?.Disposition == FindingDisposition.Unknown);
        Assert.Contains(recovery, facts => facts.Traversal.PauseReason == "traversal:handler-require-pass"
            && facts.Attempts.Count > 0);

        var continued = pending.Host.DriveOnce();
        Assert.Equal(RunDriveStatus.WaitingForInput, continued.Status);
        Assert.Equal(1, pending.Host.Facts.EffectReceipts.Count);
        Assert.Single(scenario.Effects.Effects);
        Assert.DoesNotContain(scenario.Effects.Effects.Skip(1), effect => effect.Descriptor == "设置");
        var finalReport = ScenarioRunner.FinalizePhased(scenario.Bundle, pending.Host);
        Assert.True(finalReport.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, finalReport) + "; traversal="
            + System.Text.Json.JsonSerializer.Serialize(pending.Host.KernelCore.TraversalExecution));
        Assert.Equal(4, finalReport.AgentConsultations);
        Assert.Equal(1, finalReport.EffectDeliveries);
    }

    [Theory]
    [InlineData("unknown", PlanHandlerCallStatus.Completed, "TerminalNotProven")]
    [InlineData("unavailable", PlanHandlerCallStatus.Unavailable, "AgentDecisionFailed")]
    [InlineData("null", PlanHandlerCallStatus.Failed, "AgentDecisionFailed")]
    [InlineData("exception", PlanHandlerCallStatus.Failed, "AgentDecisionFailed")]
    [InlineData("timeout", PlanHandlerCallStatus.TimedOut, "AgentDecisionFailed")]
    public void TypedReturnUnknownAndInvocationFailuresKeepDistinctRuntimeSemantics(
        string resultKind, PlanHandlerCallStatus expectedCallStatus, string expectedRunStatus)
    {
        var handler = TraversalScenarioFixture.EnglishHandler("runtime-result");
        var operation = TraversalScenarioFixture.ReadOnlyOperation("inspect-result",
            before: new[] { handler });
        var expectedConsultations = 2;
        var scenario = TraversalScenarioFixture.Create(
            new[] { new TraversalScenarioFixture.Page("root", new[]
            {
                TraversalScenarioFixture.ReadOnlyTarget("menuItem", "Settings"),
            }) },
            new[] { new TraversalRule("inspect-result-rule", "readOnly", operation) },
            handlerTimeout: resultKind == "timeout" ? TimeSpan.FromMilliseconds(1) : null,
            inspectOverride: (_, input) =>
            {
                if (resultKind == "null") return null;
                if (resultKind == "exception") throw new InvalidOperationException("simulated-inspector-failure");
                if (resultKind == "timeout")
                {
                    Thread.Sleep(100);
                    return Finding(input, FindingDisposition.Pass, CapabilityStatus.Completed);
                }
                if (resultKind == "unknown")
                    return Finding(input, FindingDisposition.Unknown, CapabilityStatus.Unknown);
                return Finding(input, FindingDisposition.Unknown, CapabilityStatus.Unavailable);
            },
            canBindOverride: _ => true,
            agentOverride: resultKind == "unknown" ? null : context =>
                context.Phase == AgentDecisionPhase.InitialPlanning
                    ? TraversalScenarioFixture.Decide(context, new[]
                        { new TraversalRule("inspect-result-rule", "readOnly", operation) })
                    : null,
            expectedOverride: new ScenarioExpectation(expectedRunStatus, null, 0,
                expectedConsultations, 0, null));

        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);

        Assert.True(execution.Report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report) + "; traversal="
            + System.Text.Json.JsonSerializer.Serialize(execution.Host.KernelCore.TraversalExecution));
        Assert.Empty(scenario.Effects.Effects);
        Assert.Empty(execution.Host.Facts.EffectReceipts);
        var traversal = Assert.IsType<TraversalExecutionView>(execution.Host.KernelCore.TraversalExecution);
        var record = Assert.Single(traversal.Handlers);
        Assert.Equal(expectedCallStatus, record.Status);
        if (resultKind == "unknown")
        {
            Assert.Equal(FindingDisposition.Unknown, record.Result!.Disposition);
            Assert.Equal(CapabilityStatus.Unknown, record.Result.Status);
            Assert.Equal(PlanExecutionStatus.Completed, traversal.Status);
        }
        else
        {
            Assert.Equal(PlanExecutionStatus.Paused, traversal.Status);
            Assert.Equal("traversal:handler-" + expectedCallStatus.ToString().ToLowerInvariant(),
                traversal.PauseReason);
        }
        if (resultKind == "timeout")
            Assert.Equal("handler-timeout", record.Reason);
        if (resultKind == "null")
            Assert.Equal("handler-returned-no-finding", record.Reason);
        if (resultKind == "exception")
            Assert.Equal("InvalidOperationException", record.Reason);
    }

    private static Finding Finding(PlanHandlerInput input, FindingDisposition disposition, CapabilityStatus status) =>
        new("sim-handler-finding", new CapabilityCorrelation(input.RunId,
            operationId: input.OperationId, domainCorrelation: input.SourceRevisionId),
            "1.0", "runtime.language-inspector", disposition, status, "scenario finding");
}
