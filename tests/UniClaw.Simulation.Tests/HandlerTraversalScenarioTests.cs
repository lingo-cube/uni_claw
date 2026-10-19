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
        var consultationsBeforeRepeat = execution.Host.Consultations.Calls.Count;
        var effectsBeforeRepeat = execution.Host.EffectDeliveryCount;
        var terminalRepeat = execution.Host.DriveOnce();
        Assert.Equal(RunDriveStatus.TerminalNotProven, terminalRepeat.Status);
        Assert.Equal(consultationsBeforeRepeat, execution.Host.Consultations.Calls.Count);
        Assert.Equal(effectsBeforeRepeat, execution.Host.EffectDeliveryCount);
    }

    [Fact]
    public void RequirePassPauseCanResumeAfterInspectionPassWithoutRepeatingNavigation()
    {
        var after = TraversalScenarioFixture.EnglishHandler("after-required", requirePass: true);
        var operation = TraversalScenarioFixture.NavigationOperation("open-settings",
            after: new[] { after });
        var agentCalls = 0;
        var inspectorCalls = 0;
        var recovery = new List<UniClaw.Kernel.Runtime.TraversalRecoveryFacts>();
        var scenario = TraversalScenarioFixture.Create(
            new[]
            {
                new TraversalScenarioFixture.Page("root", new[] { TraversalScenarioFixture.Link("设置") }),
                new TraversalScenarioFixture.Page("settings", new[] { TraversalScenarioFixture.Back() }),
                new TraversalScenarioFixture.Page("root", new[] { TraversalScenarioFixture.Link("设置") }),
            },
            TraversalScenarioFixture.NavigationRules(operation),
            agentOverride: context =>
            {
                agentCalls++;
                if (context.Phase == AgentDecisionPhase.InitialPlanning)
                    return TraversalScenarioFixture.Decide(context,
                        TraversalScenarioFixture.NavigationRules(operation));
                if (agentCalls == 2) return null;
                if (agentCalls == 3)
                    return new AgentDecision.ContinuePlan(context.DecisionId, context.PlanExecution!.Position);
                return new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId,
                    "traversal complete"));
            },
            inspectOverride: (_, input) =>
            {
                inspectorCalls++;
                // This tests a later typed retry result in the simulation seam;
                // the real adapter correctly reports Unknown when the original
                // target is absent from the post-navigation container.
                return inspectorCalls == 1
                    ? Finding(input, FindingDisposition.Unknown, CapabilityStatus.Unknown)
                    : Finding(input, FindingDisposition.Pass, CapabilityStatus.Completed);
            },
            recoveryObserver: recovery.Add,
            expectedOverride: new ScenarioExpectation("TerminalNotProven", null, 2, 4, 0, null));
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
        var consultsBeforeQuery = pending.Host.Consultations.Calls.Count;
        var effectsBeforeQuery = pending.Host.EffectDeliveryCount;
        var query1 = pending.Host.Driver.TraversalExecution;
        var query2 = pending.Host.Driver.TraversalExecution;
        Assert.Same(query1, query2);
        Assert.Equal(consultsBeforeQuery, pending.Host.Consultations.Calls.Count);
        Assert.Equal(effectsBeforeQuery, pending.Host.EffectDeliveryCount);

        var continued = pending.Host.DriveOnce();
        Assert.Equal(RunDriveStatus.TerminalNotProven, continued.Status);
        Assert.Equal(2, pending.Host.Facts.EffectReceipts.Count);
        Assert.Equal(new[] { "设置", "Back" }, scenario.Effects.Effects.Select(effect => effect.Descriptor));
        var finalReport = ScenarioRunner.FinalizePhased(scenario.Bundle, pending.Host);
        Assert.True(finalReport.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, finalReport) + "; traversal="
            + System.Text.Json.JsonSerializer.Serialize(pending.Host.KernelCore.TraversalExecution));
        Assert.Equal(4, finalReport.AgentConsultations);
        Assert.Equal(2, finalReport.EffectDeliveries);
        var resumed = Assert.IsType<TraversalExecutionView>(pending.Host.KernelCore.TraversalExecution);
        Assert.Equal(PlanExecutionStatus.Completed, resumed.Status);
        var afterRecords = resumed.Handlers.Where(record => record.Phase == "AfterVerified").ToArray();
        Assert.Equal(2, afterRecords.Length);
        Assert.Equal(afterRecords[0].OperationId, afterRecords[1].OperationId);
        Assert.Equal(FindingDisposition.Unknown, afterRecords[0].Result!.Disposition);
        Assert.Equal(FindingDisposition.Pass, afterRecords[1].Result!.Disposition);
        Assert.True(resumed.WorkUsed > paused.WorkUsed);
    }

    [Theory]
    [InlineData("unknown", PlanHandlerCallStatus.Completed, "TerminalNotProven")]
    [InlineData("unavailable", PlanHandlerCallStatus.Unavailable, "WaitingForInput")]
    [InlineData("null", PlanHandlerCallStatus.Failed, "WaitingForInput")]
    [InlineData("exception", PlanHandlerCallStatus.Failed, "WaitingForInput")]
    [InlineData("timeout", PlanHandlerCallStatus.TimedOut, "WaitingForInput")]
    public void TypedReturnUnknownAndInvocationFailuresKeepDistinctRuntimeSemantics(
        string resultKind, PlanHandlerCallStatus expectedCallStatus, string expectedRunStatus)
    {
        var handler = TraversalScenarioFixture.EnglishHandler("runtime-result");
        var operation = TraversalScenarioFixture.ReadOnlyOperation("inspect-result",
            before: new[] { handler });
        var expectedConsultations = 2;
        var agentCalls = 0;
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
                ++agentCalls == 1
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
            Assert.Null(traversal.EndReason);
            Assert.Equal("traversal:awaiting-decision", execution.Report.Reason);
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

    [Fact]
    public void MissingRequiredBindingFailsBeforeReadOnlyHandlerOrEffect()
    {
        var handler = TraversalScenarioFixture.EnglishHandler("required-before");
        var operation = TraversalScenarioFixture.NavigationOperation("open-required",
            before: new[] { handler });
        var scenario = TraversalScenarioFixture.Create(
            new[] { new TraversalScenarioFixture.Page("root", new[]
            {
                TraversalScenarioFixture.Link("Settings"),
            }) },
            TraversalScenarioFixture.NavigationRules(operation),
            canBindOverride: _ => false,
            expectedOverride: new ScenarioExpectation("AgentDecisionFailed", null, 0, 1, 0, null));

        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);

        Assert.True(execution.Report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report));
        Assert.Empty(scenario.Effects.Effects);
        Assert.Empty(execution.Host.Facts.EffectReceipts);
        Assert.Null(execution.Host.KernelCore.TraversalExecution);
    }

    [Fact]
    public void RequiredAfterHandlerWithoutTypedAdapterIsRejectedBeforeNavigation()
    {
        var after = TraversalScenarioFixture.EnglishHandler("required-after");
        var operation = TraversalScenarioFixture.NavigationOperation("open-before-null-adapter",
            after: new[] { after });
        var scenario = TraversalScenarioFixture.Create(
            new[] { new TraversalScenarioFixture.Page("root", new[]
            {
                TraversalScenarioFixture.Link("Settings"),
            }) },
            TraversalScenarioFixture.NavigationRules(operation),
            canBindOverride: _ => true,
            expectedOverride: new ScenarioExpectation("AgentDecisionFailed", null, 0, 1, 0, null));
        var options = scenario.Options with
        {
            Seams = scenario.Options.Seams! with { InspectPlanLanguage = null },
        };

        var execution = ScenarioRunner.Run(scenario.Bundle, options);

        Assert.True(execution.Report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report));
        Assert.Empty(scenario.Effects.Effects);
        Assert.Empty(execution.Host.Facts.EffectReceipts);
        Assert.Null(execution.Host.KernelCore.TraversalExecution);
    }

    [Fact]
    public void RequiredBindingThatDisappearsAtInvocationPausesAfterTheSingleNavigation()
    {
        var after = TraversalScenarioFixture.EnglishHandler("required-after-runtime");
        var operation = TraversalScenarioFixture.NavigationOperation("navigate-required-after",
            after: new[] { after });
        var bindCalls = 0;
        var agentCalls = 0;
        var recovery = new List<UniClaw.Kernel.Runtime.TraversalRecoveryFacts>();
        var scenario = TraversalScenarioFixture.Create(
            new[]
            {
                new TraversalScenarioFixture.Page("root", new[] { TraversalScenarioFixture.Link("Settings") }),
                new TraversalScenarioFixture.Page("settings", new[] { TraversalScenarioFixture.Back() }),
            },
            TraversalScenarioFixture.NavigationRules(operation),
            canBindOverride: _ => ++bindCalls == 1,
            agentOverride: context => ++agentCalls == 1
                ? TraversalScenarioFixture.Decide(context, TraversalScenarioFixture.NavigationRules(operation))
                : null,
            recoveryObserver: recovery.Add,
            expectedOverride: new ScenarioExpectation("WaitingForInput", null, 1, 2, 0, null));

        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);

        Assert.True(execution.Report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report) + "; traversal="
            + System.Text.Json.JsonSerializer.Serialize(execution.Host.KernelCore.TraversalExecution));
        Assert.Equal(new[] { "Settings" }, scenario.Effects.Effects.Select(effect => effect.Descriptor));
        Assert.Single(execution.Host.Facts.EffectReceipts);
        var traversal = Assert.IsType<TraversalExecutionView>(execution.Host.KernelCore.TraversalExecution);
        var record = Assert.Single(traversal.Handlers);
        Assert.Equal(PlanHandlerCallStatus.Unavailable, record.Status);
        Assert.Equal("AfterVerified", record.Phase);
        Assert.Equal("traversal:handler-unavailable", traversal.PauseReason);
        Assert.Contains(recovery, facts => facts.Traversal.PauseReason == "traversal:handler-unavailable");
        Assert.Contains(traversal.Operations!, operationRecord => !operationRecord.Completed);
    }

    [Fact]
    public void OptionalBindingUnavailableIsRecordedAndReadOnlyWorkContinues()
    {
        var before = TraversalScenarioFixture.EnglishHandler("optional-before", required: false);
        var operation = TraversalScenarioFixture.ReadOnlyOperation("optional-inspection",
            before: new[] { before });
        var scenario = TraversalScenarioFixture.Create(
            new[] { new TraversalScenarioFixture.Page("root", new[]
            {
                TraversalScenarioFixture.ReadOnlyTarget("menuItem", "Settings"),
            }) },
            new[] { new TraversalRule("optional-rule", "readOnly", operation) },
            canBindOverride: _ => false,
            expectedOverride: new ScenarioExpectation("TerminalNotProven", null, 0, 2, 0, null));

        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);

        Assert.True(execution.Report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report));
        Assert.Empty(scenario.Effects.Effects);
        var traversal = Assert.IsType<TraversalExecutionView>(execution.Host.KernelCore.TraversalExecution);
        var handlerRecord = Assert.Single(traversal.Handlers);
        Assert.Equal(PlanHandlerCallStatus.Unavailable, handlerRecord.Status);
        Assert.Equal("binding-unavailable", handlerRecord.Reason);
        Assert.Contains(traversal.Operations!, operationRecord => operationRecord.Completed);
    }

    [Fact]
    public void NavigationAndReadOnlyOperationsKeepDistinctInvocationAndRevisionBindings()
    {
        var navBefore = TraversalScenarioFixture.EnglishHandler("nav-before");
        var navAfter = TraversalScenarioFixture.EnglishHandler("nav-after");
        var readBefore = TraversalScenarioFixture.EnglishHandler("read-before");
        var readAfter = TraversalScenarioFixture.EnglishHandler("read-after");
        var navigation = TraversalScenarioFixture.NavigationOperation("navigate-settings",
            new[] { navBefore }, new[] { navAfter });
        var readOnly = TraversalScenarioFixture.ReadOnlyOperation("inspect-setting",
            new[] { readBefore }, new[] { readAfter });
        var rules = new[]
        {
            new TraversalRule("navigate-rule", "navigation", navigation),
            new TraversalRule("read-rule", "readOnly", readOnly),
        };
        var scenario = TraversalScenarioFixture.Create(
            new[]
            {
                new TraversalScenarioFixture.Page("root", new[]
                {
                    TraversalScenarioFixture.Link("Settings"),
                    TraversalScenarioFixture.ReadOnlyTarget("menuItem", "Root label"),
                }),
                new TraversalScenarioFixture.Page("settings", new[] { TraversalScenarioFixture.Back() }),
                new TraversalScenarioFixture.Page("root", new[]
                {
                    TraversalScenarioFixture.Link("Settings"),
                    TraversalScenarioFixture.ReadOnlyTarget("menuItem", "Root label"),
                }),
            }, rules,
            expectedOverride: new ScenarioExpectation("TerminalNotProven", null, 2, 2, 0, null));

        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);

        Assert.True(execution.Report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report) + "; traversal="
            + System.Text.Json.JsonSerializer.Serialize(execution.Host.KernelCore.TraversalExecution));
        Assert.Equal(new[] { "Settings", "Back" }, scenario.Effects.Effects.Select(effect => effect.Descriptor));
        var traversal = Assert.IsType<TraversalExecutionView>(execution.Host.KernelCore.TraversalExecution);
        var navRecords = traversal.Handlers.Where(record => record.HandlerId is "nav-before" or "nav-after").ToArray();
        Assert.Collection(navRecords,
            record => Assert.Equal("Before", record.Phase),
            record => Assert.Equal("AfterVerified", record.Phase));
        Assert.Equal(navRecords[0].OperationId, navRecords[1].OperationId);
        Assert.NotEqual(navRecords[0].SourceRevisionId, navRecords[1].SourceRevisionId);
        Assert.Equal("navigate-settings", Assert.Single(traversal.Operations!.Where(operation =>
            operation.TemplateId == "navigate-settings")).TemplateId);
        Assert.Contains(traversal.Handlers, record => record.HandlerId == "read-before");
        Assert.Contains(traversal.Handlers, record => record.HandlerId == "read-after");
        var readOperationIds = traversal.Handlers.Where(record => record.HandlerId.StartsWith("read-", StringComparison.Ordinal))
            .Select(record => record.OperationId).Distinct().ToArray();
        Assert.Single(readOperationIds);
        Assert.NotEqual(navRecords[0].OperationId, readOperationIds[0]);
        Assert.Equal(2, traversal.Handlers.Select(record => record.OperationId).Distinct().Count());
    }

    [Fact]
    public void LocalWorkBudgetStopsBeforeInvokingARequiredHandler()
    {
        var before = TraversalScenarioFixture.EnglishHandler("budget-before");
        var operation = TraversalScenarioFixture.ReadOnlyOperation("budgeted-read", before: new[] { before });
        var rule = new TraversalRule("budget-rule", "readOnly", operation);
        var agentCalls = 0;
        var recovery = new List<UniClaw.Kernel.Runtime.TraversalRecoveryFacts>();
        var scenario = TraversalScenarioFixture.Create(
            new[] { new TraversalScenarioFixture.Page("root", new[]
            {
                TraversalScenarioFixture.ReadOnlyTarget("menuItem", "Settings"),
            }) },
            new[] { rule }, maxOperations: 1,
            agentOverride: context => ++agentCalls == 1
                ? TraversalScenarioFixture.Decide(context, new[] { rule }, maxOperations: 1)
                : new AgentDecision.ContinuePlan(context.DecisionId, context.PlanExecution!.Position),
            recoveryObserver: recovery.Add,
            expectedOverride: new ScenarioExpectation("AgentDecisionFailed", null, 0, 2, 0, null));

        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);

        Assert.True(execution.Report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report) + "; traversal="
            + System.Text.Json.JsonSerializer.Serialize(execution.Host.KernelCore.TraversalExecution));
        Assert.Empty(scenario.Effects.Effects);
        var traversal = Assert.IsType<TraversalExecutionView>(execution.Host.KernelCore.TraversalExecution);
        Assert.Equal("traversal:local-work-exhausted", traversal.PauseReason);
        Assert.Empty(traversal.Handlers);
        Assert.Contains(traversal.Operations!, operationRecord => !operationRecord.Completed);
        Assert.True(traversal.KnownRemainingWork >= 1);
        Assert.Contains("plan:local-work-exhausted-requires-revision", execution.Report.Reason,
            StringComparison.Ordinal);
        Assert.True(traversal.WorkUsed > 0);
        Assert.Equal(Assert.Single(recovery).Traversal.WorkUsed, traversal.WorkUsed);
    }

    [Fact]
    public void LifecycleCancellationDuringMissingPostEvidencePreservesEffectWithoutClaimingVerification()
    {
        var handler = TraversalScenarioFixture.EnglishHandler("after-cancel");
        var operation = TraversalScenarioFixture.NavigationOperation("open-before-cancel",
            after: new[] { handler });
        var scenario = TraversalScenarioFixture.Create(
            new[] { new TraversalScenarioFixture.Page("root", new[]
            {
                TraversalScenarioFixture.Link("Settings"),
            }) },
            TraversalScenarioFixture.NavigationRules(operation),
            expectedOverride: new ScenarioExpectation("CancelledWithoutSafeStopPath", null, 1, 1, 0, null));
        var phased = scenario.Options with { Phased = true };

        var pending = ScenarioRunner.Run(scenario.Bundle, phased);

        Assert.False(pending.Report.AcceptancePassed);
        Assert.StartsWith("PhasedPending:", pending.Report.Reason, StringComparison.Ordinal);
        Assert.Equal("post-action-evidence", pending.Host.RecordedDriveResults[^1].Reason);
        Assert.Single(pending.Host.Facts.EffectReceipts);
        var cancel = new ScenarioStimulus.CancelRequest("cancel-after-delivery")
        {
            StimulusId = "cancel-after-delivery",
            VirtualTime = new DateTimeOffset(2026, 10, 19, 2, 1, 0, TimeSpan.Zero),
        };
        Assert.True(pending.Host.SubmitStimulus(cancel));

        var cancelled = pending.Host.DriveOnce();
        Assert.Equal(RunDriveStatus.CancelledWithoutSafeStopPath, cancelled.Status);
        var report = ScenarioRunner.FinalizePhased(scenario.Bundle, pending.Host);

        Assert.True(report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, report) + "; traversal="
            + System.Text.Json.JsonSerializer.Serialize(pending.Host.KernelCore.TraversalExecution));
        Assert.Single(scenario.Effects.Effects);
        Assert.Single(pending.Host.Facts.EffectReceipts);
        Assert.Contains(cancel.StimulusId, report.ConsumedStimulusIds);
        var traversal = Assert.IsType<TraversalExecutionView>(pending.Host.KernelCore.TraversalExecution);
        Assert.Equal(PlanExecutionStatus.Abandoned, traversal.Status);
        Assert.Contains("cancel-after-delivery", traversal.EndReason, StringComparison.Ordinal);
        Assert.Contains(traversal.Gaps, gap => gap.Reason == "verification-interrupted-by-cancel");
        Assert.Contains(traversal.Operations!, record => !record.Completed);
        Assert.DoesNotContain(traversal.Handlers, record => record.Phase == "AfterVerified");
    }

    private static Finding Finding(PlanHandlerInput input, FindingDisposition disposition, CapabilityStatus status) =>
        new("sim-handler-finding", new CapabilityCorrelation(input.RunId,
            operationId: input.OperationId, domainCorrelation: input.SourceRevisionId),
            "1.0", "runtime.language-inspector", disposition, status, "scenario finding");
}
