using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Simulation.Tests;

public sealed class AsyncHandlerTraversalScenarioTests
{
    [Fact]
    public async Task SlowBeforeHandlersRemainQueryableAndResumeInOrderWithoutEffectsOrRepeatedConsultation()
    {
        var first = TraversalScenarioFixture.EnglishHandler("slow-before-1");
        var second = TraversalScenarioFixture.EnglishHandler("slow-before-2");
        var operation = TraversalScenarioFixture.ReadOnlyOperation("ordered-read",
            before: new[] { first, second });
        var firstCompletion = new TaskCompletionSource<Finding?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondCompletion = new TaskCompletionSource<Finding?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStarted = NewSignal();
        var secondStarted = NewSignal();
        PlanHandlerInput? firstInput = null;
        PlanHandlerInput? secondInput = null;
        var invocationOrder = new List<string>();
        var scenario = TraversalScenarioFixture.Create(
            new[] { new TraversalScenarioFixture.Page("root", new[]
            {
                TraversalScenarioFixture.ReadOnlyTarget("menuItem", "Settings"),
            }) },
            new[] { new TraversalRule("ordered-read-rule", "readOnly", operation) },
            inspectOverride: (_, input) => Finding(input, FindingDisposition.Violation, CapabilityStatus.Completed),
            expectedOverride: new ScenarioExpectation("TerminalNotProven", null, 0, 2, 0, null));
        var options = scenario.Options with
        {
            Seams = scenario.Options.Seams! with
            {
                InspectPlanLanguage = null,
                InspectPlanLanguageAsync = (handler, input, _) =>
                {
                    invocationOrder.Add(handler.HandlerId);
                    if (handler.HandlerId == first.HandlerId)
                    {
                        firstInput = input;
                        firstStarted.TrySetResult();
                        return new ValueTask<Finding?>(firstCompletion.Task);
                    }
                    secondInput = input;
                    secondStarted.TrySetResult();
                    return new ValueTask<Finding?>(secondCompletion.Task);
                },
            },
        };
        var host = ComposeAndActivate(scenario.Bundle, options);
        var drive = host.DriveOnceAsync();
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var running = Assert.IsType<TraversalExecutionView>(host.KernelCore.TraversalExecution);
        var runningRecord = Assert.Single(running.Handlers);
        Assert.Equal(PlanHandlerCallStatus.Running, runningRecord.Status);
        Assert.False(string.IsNullOrWhiteSpace(runningRecord.CallId));
        var consultations = host.Consultations.Calls.Count;
        Assert.Empty(host.Facts.EffectReceipts);
        Assert.Empty(scenario.Effects.Effects);
        Assert.Equal(new[] { first.HandlerId }, invocationOrder);
        Assert.Equal(0, host.KernelCore.TraversalExecution is TraversalExecutionView view
            ? view.OperationsCompleted : -1);

        firstCompletion.SetResult(Finding(firstInput!, FindingDisposition.Violation, CapabilityStatus.Completed));
        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { first.HandlerId, second.HandlerId }, invocationOrder);
        Assert.Equal(consultations, host.Consultations.Calls.Count);
        Assert.Empty(host.Facts.EffectReceipts);
        Assert.Empty(scenario.Effects.Effects);
        var secondRunning = Assert.IsType<TraversalExecutionView>(host.KernelCore.TraversalExecution);
        Assert.Equal(PlanHandlerCallStatus.Running, Assert.Single(secondRunning.Handlers.Where(
            record => record.HandlerId == second.HandlerId)).Status);

        secondCompletion.SetResult(Finding(secondInput!, FindingDisposition.Violation, CapabilityStatus.Completed));
        var result = await drive.WaitAsync(TimeSpan.FromSeconds(5));
        var report = ScenarioRunner.FinalizePhased(scenario.Bundle, host);

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.True(report.AcceptancePassed, ScenarioReport.DescribeAcceptance(scenario.Bundle, report));
        Assert.Empty(host.Facts.EffectReceipts);
        Assert.Empty(scenario.Effects.Effects);
        var completed = Assert.IsType<TraversalExecutionView>(host.KernelCore.TraversalExecution);
        Assert.Equal(new[] { first.HandlerId, second.HandlerId }, completed.Handlers.Select(r => r.HandlerId));
        Assert.All(completed.Handlers, record => Assert.Equal(PlanHandlerCallStatus.Completed, record.Status));
    }

    [Fact]
    public async Task AfterVerifiedHandlerStartsOnlyAfterNavigationVerification()
    {
        var after = TraversalScenarioFixture.EnglishHandler("after-verified-slow");
        var operation = TraversalScenarioFixture.NavigationOperation("navigate-before-after",
            after: new[] { after });
        var completion = new TaskCompletionSource<Finding?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = NewSignal();
        PlanHandlerInput? handlerInput = null;
        int? receiptsAtStart = null;
        int? verificationsAtStart = null;
        SimulationHost? hostRef = null;
        var scenario = TraversalScenarioFixture.Create(
            new[]
            {
                new TraversalScenarioFixture.Page("root", new[] { TraversalScenarioFixture.Link("Settings") }),
                new TraversalScenarioFixture.Page("settings", new[] { TraversalScenarioFixture.Back() }),
                new TraversalScenarioFixture.Page("root", new[] { TraversalScenarioFixture.Link("Settings") }),
            },
            TraversalScenarioFixture.NavigationRules(operation),
            expectedOverride: new ScenarioExpectation("TerminalNotProven", null, 2, 2, 0, null));
        var options = scenario.Options with
        {
            Seams = scenario.Options.Seams! with
            {
                InspectPlanLanguage = null,
                InspectPlanLanguageAsync = (handler, input, _) =>
                {
                    Assert.Equal(after.HandlerId, handler.HandlerId);
                    handlerInput = input;
                    receiptsAtStart = scenario.Effects.Effects.Count;
                    verificationsAtStart = hostRef?.Facts.PostActionVerifications.Count;
                    started.TrySetResult();
                    return new ValueTask<Finding?>(completion.Task);
                },
            },
        };
        var host = ComposeAndActivate(scenario.Bundle, options);
        hostRef = host;
        var drive = host.DriveOnceAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, receiptsAtStart);
        Assert.Equal(1, verificationsAtStart);
        Assert.Equal("settings", host.Facts.CurrentWorldClaims.GetValueOrDefault("ui.screen"));
        var handler = Assert.Single(Assert.IsType<TraversalExecutionView>(host.KernelCore.TraversalExecution).Handlers);
        Assert.Equal("AfterVerified", handler.Phase);
        Assert.Equal(PlanHandlerCallStatus.Running, handler.Status);
        Assert.Single(host.Facts.EffectReceipts);

        completion.SetResult(Finding(handlerInput!, FindingDisposition.Violation, CapabilityStatus.Completed));
        var result = await drive.WaitAsync(TimeSpan.FromSeconds(5));
        var report = ScenarioRunner.FinalizePhased(scenario.Bundle, host);
        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.True(report.AcceptancePassed, ScenarioReport.DescribeAcceptance(scenario.Bundle, report));
        Assert.Equal(2, host.Facts.EffectReceipts.Count);
        Assert.Equal(2, scenario.Effects.Effects.Count);
    }

    [Fact]
    public async Task TimedOutHandlerLateResultCannotChangeRunRecord()
    {
        var handler = TraversalScenarioFixture.EnglishHandler("timeout-late");
        var operation = TraversalScenarioFixture.ReadOnlyOperation("timeout-late-read", new[] { handler });
        var completion = new TaskCompletionSource<Finding?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = NewSignal();
        PlanHandlerInput? handlerInput = null;
        var scenario = TraversalScenarioFixture.Create(
            new[] { new TraversalScenarioFixture.Page("root", new[]
            {
                TraversalScenarioFixture.ReadOnlyTarget("menuItem", "Settings"),
            }) },
            new[] { new TraversalRule("timeout-late-rule", "readOnly", operation) },
            handlerTimeout: TimeSpan.FromMilliseconds(80),
            agentOverride: FirstPlanOnly(new[] { new TraversalRule("timeout-late-rule", "readOnly", operation) }),
            expectedOverride: new ScenarioExpectation("WaitingForInput", null, 0, 2, 0, null));
        var options = WithAsyncInspector(scenario, (h, input, _) =>
        {
            Assert.Equal(handler.HandlerId, h.HandlerId);
            handlerInput = input;
            started.TrySetResult();
            return new ValueTask<Finding?>(completion.Task);
        });
        var host = ComposeAndActivate(scenario.Bundle, options);
        var drive = host.DriveOnceAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var timedOut = await drive.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(RunDriveStatus.WaitingForInput, timedOut.Status);
        var record = Assert.Single(Assert.IsType<TraversalExecutionView>(host.KernelCore.TraversalExecution).Handlers);
        Assert.Equal(PlanHandlerCallStatus.TimedOut, record.Status);
        Assert.Equal("handler-timeout", record.Reason);

        completion.SetResult(Finding(handlerInput!, FindingDisposition.Pass, CapabilityStatus.Completed));
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var afterLateResult = Assert.Single(Assert.IsType<TraversalExecutionView>(host.KernelCore.TraversalExecution).Handlers);
        Assert.Equal(record, afterLateResult);
        Assert.Empty(host.Facts.EffectReceipts);
        Assert.Empty(scenario.Effects.Effects);
        var report = ScenarioRunner.FinalizePhased(scenario.Bundle, host);
        Assert.True(report.AcceptancePassed, ScenarioReport.DescribeAcceptance(scenario.Bundle, report));
    }

    [Fact]
    public async Task CancelledHandlerLateResultCannotChangeRunRecord()
    {
        var handler = TraversalScenarioFixture.EnglishHandler("cancel-late");
        var operation = TraversalScenarioFixture.ReadOnlyOperation("cancel-late-read", new[] { handler });
        var completion = new TaskCompletionSource<Finding?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = NewSignal();
        PlanHandlerInput? handlerInput = null;
        var scenario = TraversalScenarioFixture.Create(
            new[] { new TraversalScenarioFixture.Page("root", new[]
            {
                TraversalScenarioFixture.ReadOnlyTarget("menuItem", "Settings"),
            }) },
            new[] { new TraversalRule("cancel-late-rule", "readOnly", operation) },
            agentOverride: FirstPlanOnly(new[] { new TraversalRule("cancel-late-rule", "readOnly", operation) }),
            expectedOverride: new ScenarioExpectation("CancelledWithoutSafeStopPath", null, 0, 1, 0, null));
        var options = WithAsyncInspector(scenario, (h, input, _) =>
        {
            Assert.Equal(handler.HandlerId, h.HandlerId);
            handlerInput = input;
            started.TrySetResult();
            return new ValueTask<Finding?>(completion.Task);
        });
        var host = ComposeAndActivate(scenario.Bundle, options);
        var drive = host.DriveOnceAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var cancelRequest = new ScenarioStimulus.CancelRequest("cancel-during-handler")
        {
            StimulusId = "cancel-during-handler",
            VirtualTime = new DateTimeOffset(2026, 10, 19, 2, 1, 0, TimeSpan.Zero),
        };
        Assert.True(host.SubmitStimulus(cancelRequest));
        var cancelled = await drive.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(RunDriveStatus.CancelledWithoutSafeStopPath, cancelled.Status);
        var record = Assert.Single(Assert.IsType<TraversalExecutionView>(host.KernelCore.TraversalExecution).Handlers);
        Assert.Equal(PlanHandlerCallStatus.Cancelled, record.Status);

        completion.SetResult(Finding(handlerInput!, FindingDisposition.Pass, CapabilityStatus.Completed));
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(record, Assert.Single(Assert.IsType<TraversalExecutionView>(host.KernelCore.TraversalExecution).Handlers));
        Assert.Empty(host.Facts.EffectReceipts);
        Assert.Empty(scenario.Effects.Effects);
        var report = ScenarioRunner.FinalizePhased(scenario.Bundle, host);
        Assert.True(report.AcceptancePassed, ScenarioReport.DescribeAcceptance(scenario.Bundle, report));
    }

    private static SimulationHost ComposeAndActivate(MinimalScenarioBundle bundle, RunOptions options)
    {
        var host = SimulationHost.Compose(bundle, options);
        var admission = host.KernelCore.AdmitContract(host.Contract);
        Assert.True(admission.Accepted, string.Join(", ", admission.Checks.Where(check => !check.Passed).Select(check => check.Name)));
        var activation = host.Driver.Activate();
        Assert.True(activation.Accepted, activation.Reason);
        host.FirstActivation = activation;
        return host;
    }

    private static RunOptions WithAsyncInspector(TraversalScenarioFixture.Scenario scenario,
        Func<PlanLanguageHandler, PlanHandlerInput, CancellationToken, ValueTask<Finding?>> inspect) =>
        scenario.Options with
        {
            Seams = scenario.Options.Seams! with
            {
                InspectPlanLanguage = null,
                InspectPlanLanguageAsync = inspect,
            },
        };

    private static Func<AgentDecisionContext, AgentDecision?> FirstPlanOnly(IReadOnlyList<TraversalRule> rules)
    {
        var emitted = false;
        return context =>
        {
            if (!emitted && context.Phase == AgentDecisionPhase.InitialPlanning)
            {
                emitted = true;
                return TraversalScenarioFixture.Decide(context, rules);
            }
            return null;
        };
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static Finding Finding(PlanHandlerInput input, FindingDisposition disposition, CapabilityStatus status) =>
        new("async-simulation-finding", new CapabilityCorrelation(input.RunId,
            operationId: input.OperationId, domainCorrelation: input.SourceRevisionId),
            "1.0", "runtime.language-inspector", disposition, status, "controlled async result");
}
