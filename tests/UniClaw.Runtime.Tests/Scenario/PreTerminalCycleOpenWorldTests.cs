using System.Collections.Immutable;
using UniClaw.Runtime.Container;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>Behavioral evidence for the Agent-owned pre-terminal seam.</summary>
public sealed class PreTerminalCycleOpenWorldTests
{
    [Fact]
    public async Task DisabledEvaluatorPreservesOpenWorldOutcomeTraceAndDispatch()
    {
        var baseline = Create(U2OpenWorldSettingsFixture.Positive("baseline"), omittedEvaluator: true);
        var disabled = Create(U2OpenWorldSettingsFixture.Positive("disabled"), explicitNullEvaluator: true);

        var baselineState = await IntentExecution.RunOpenWorldAsync(
            baseline.Agent, baseline.Envelope, "baseline", CancellationToken.None);
        var disabledState = await IntentExecution.RunOpenWorldAsync(
            disabled.Agent, disabled.Envelope, "disabled", CancellationToken.None);

        Assert.Equal(baselineState, disabledState);
        Assert.Equal(baseline.Environment.ActionHistory, disabled.Environment.ActionHistory);
        Assert.Equal(
            baseline.Traversal.Journal.Select(e => (e.StepId, e.SelectedElementIndex, e.DispatchedAction, e.Result, e.RetryCount)),
            disabled.Traversal.Journal.Select(e => (e.StepId, e.SelectedElementIndex, e.DispatchedAction, e.Result, e.RetryCount)));
        Assert.Equal(
            baseline.Agent.Trace.Select(e => (e.ContainerId, e.StepId, e.ActionId, e.Action, e.Reason, e.RunState)),
            disabled.Agent.Trace.Select(e => (e.ContainerId, e.StepId, e.ActionId, e.Action, e.Reason, e.RunState)));
    }

    [Fact]
    public async Task SupportedEvaluatorRunsOnceBeforeFirstCandidateAuthorizationAndCommits()
    {
        var run = Create(U2OpenWorldSettingsFixture.Positive());
        var order = new List<string>();
        var evaluations = 0;
        var authorizations = 0;
        var ledger = new PreTerminalReasoningLedger();
        var snapshots = new List<long>();
        var evaluator = new LedgerPreTerminalReasoningEvaluator((snapshot, _) =>
        {
            evaluations++;
            snapshots.Add(snapshot.AcceptedObservationSequence);
            order.Add($"evaluate:{run.Environment.ActionHistory.Count}");
            return ValueTask.FromResult(Proposal(snapshot,
                PreTerminalContinuationKind.ContinuationSupported, $"r{evaluations}"));
        }, ledger);
        run = Create(U2OpenWorldSettingsFixture.Positive(), evaluator, (observation, candidate) =>
        {
            authorizations++;
            order.Add("authorize");
            return U2OpenWorldSettingsFixture.EvaluateAuthorization(observation, candidate);
        });

        var state = await IntentExecution.RunOpenWorldAsync(
            run.Agent, run.Envelope, run.Fixture.RunId, CancellationToken.None);

        Assert.True(state == RunState.Completed, run.Agent.Reason);
        Assert.True(evaluations > 1);
        Assert.Equal(snapshots.Count, snapshots.Distinct().Count());
        Assert.Equal("evaluate:1", order[0]);
        Assert.Equal("authorize", order[1]);
        Assert.Equal(4, run.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.Equal($"r{evaluations}", evaluator.AcceptedReasoningRevisionReference);
        Assert.True(authorizations > 0);
    }

    [Fact]
    public async Task NotSupportedCommitsReasoningThenAgentFailsClosedWithoutSemanticDispatch()
    {
        var evaluator = new LedgerPreTerminalReasoningEvaluator((snapshot, _) =>
            ValueTask.FromResult(Proposal(snapshot,
                PreTerminalContinuationKind.ContinuationNotSupported, "r1")));
        var run = Create(U2OpenWorldSettingsFixture.Positive(), evaluator);

        var state = await IntentExecution.RunOpenWorldAsync(
            run.Agent, run.Envelope, run.Fixture.RunId, CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.Empty(run.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Equal("r1", evaluator.AcceptedReasoningRevisionReference);
    }

    [Theory]
    [InlineData(PreTerminalContinuationKind.ContinuationSupported)]
    [InlineData(PreTerminalContinuationKind.ContinuationSupportedAfterRevision)]
    public async Task SupportedProposalsCommitAndAgentContinuesExistingDfs(
        PreTerminalContinuationKind kind)
    {
        var evaluations = 0;
        var ledger = new PreTerminalReasoningLedger();
        var evaluator = new LedgerPreTerminalReasoningEvaluator((snapshot, _) =>
        {
            evaluations++;
            return ValueTask.FromResult(Proposal(snapshot, kind, $"r{evaluations}"));
        }, ledger);
        var run = Create(U2OpenWorldSettingsFixture.Positive(), evaluator);

        var state = await IntentExecution.RunOpenWorldAsync(
            run.Agent, run.Envelope, run.Fixture.RunId, CancellationToken.None);

        Assert.True(state == RunState.Completed, run.Agent.Reason);
        Assert.Equal(4, run.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.True(evaluations > 1);
        Assert.Equal($"r{evaluations}", evaluator.AcceptedReasoningRevisionReference);
        Assert.Equal(evaluations + 1, ledger.History.Count);
    }

    [Fact]
    public async Task CancelledEvaluationRejectsWithoutCommitOrDispatch()
    {
        var evaluator = new LedgerPreTerminalReasoningEvaluator((_, _) =>
            ValueTask.FromException<PreTerminalContinuationProposal>(
                new OperationCanceledException("deterministic cancellation")));
        var run = Create(U2OpenWorldSettingsFixture.Positive(), evaluator);

        var state = await IntentExecution.RunOpenWorldAsync(
            run.Agent, run.Envelope, run.Fixture.RunId, CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.Empty(run.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Equal("reasoning-0", evaluator.AcceptedReasoningRevisionReference);
    }

    [Fact]
    public async Task TokenIgnoringEvaluatorTimesOutAndLateCompletionCannotCommit()
    {
        var completion = new TaskCompletionSource<PreTerminalContinuationProposal>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        PreTerminalReasoningSnapshot? snapshot = null;
        var ledger = new PreTerminalReasoningLedger();
        var evaluator = new LedgerPreTerminalReasoningEvaluator((current, _) =>
        {
            snapshot = current;
            return new ValueTask<PreTerminalContinuationProposal>(completion.Task);
        }, ledger);
        var run = Create(U2OpenWorldSettingsFixture.Positive(), evaluator);

        var state = await IntentExecution.RunOpenWorldAsync(
            run.Agent, run.Envelope, run.Fixture.RunId, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(8));

        Assert.Equal(RunState.Failed, state);
        Assert.Empty(run.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Equal("reasoning-0", evaluator.AcceptedReasoningRevisionReference);

        Assert.NotNull(snapshot);
        completion.TrySetResult(Proposal(snapshot!, PreTerminalContinuationKind.ContinuationSupported));
        await Task.Yield();
        Assert.Equal("reasoning-0", evaluator.AcceptedReasoningRevisionReference);
    }

    [Fact]
    public async Task RejectedLiveRevisionCannotDispatchLateResult()
    {
        var evaluator = new LedgerPreTerminalReasoningEvaluator((snapshot, _) =>
            ValueTask.FromResult(new PreTerminalContinuationProposal(
                snapshot.RunId, snapshot.CycleSequence, snapshot.AcceptedObservationSequence,
                snapshot.BeliefRevision, snapshot.TraceDigest, "stale", "r1",
                PreTerminalContinuationKind.ContinuationSupported)));
        var run = Create(U2OpenWorldSettingsFixture.Positive(), evaluator);

        var state = await IntentExecution.RunOpenWorldAsync(
            run.Agent, run.Envelope, run.Fixture.RunId, CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.Empty(run.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Equal("reasoning-0", evaluator.AcceptedReasoningRevisionReference);
    }

    private static PreTerminalContinuationProposal Proposal(
        PreTerminalReasoningSnapshot snapshot,
        PreTerminalContinuationKind kind,
        string proposedRevision = "r1") =>
        new(snapshot.RunId, snapshot.CycleSequence, snapshot.AcceptedObservationSequence,
            snapshot.BeliefRevision, snapshot.TraceDigest,
            snapshot.AcceptedReasoningRevisionReference, proposedRevision, kind);

    private static TestRun Create(
        U2OpenWorldSettingsFixture fixture,
        IPreTerminalReasoningEvaluator? evaluator = null,
        Func<Observation, ObservedElement, CandidateAuthorizationEvidence>? authorizationOverride = null,
        bool omittedEvaluator = false,
        bool explicitNullEvaluator = false)
    {
        var environment = fixture.Environment;
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(environment, "Settings", U2OpenWorldSettingsFixture.ResolveSemanticPage);
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        RuntimeAgent agent;
        if (omittedEvaluator)
        {
            agent = new RuntimeAgent(
                startup, traversal, cancellationToken => environment.ObserveAsync(cancellationToken),
                U2OpenWorldSettingsFixture.ResolveSemanticPage,
                page => new RuntimeContainer(page,
                    observation => string.Equals(U2OpenWorldSettingsFixture.ResolveSemanticPage(observation), page, StringComparison.Ordinal),
                    traversal.ExecuteStep), recovery);
        }
        else
        {
            agent = new RuntimeAgent(
                startup, traversal, cancellationToken => environment.ObserveAsync(cancellationToken),
                U2OpenWorldSettingsFixture.ResolveSemanticPage,
                page => new RuntimeContainer(page,
                    observation => string.Equals(U2OpenWorldSettingsFixture.ResolveSemanticPage(observation), page, StringComparison.Ordinal),
                    traversal.ExecuteStep), recovery,
                preTerminalReasoningEvaluator: explicitNullEvaluator ? null : evaluator);
        }
        var goal = new Goal(
            observation => new GoalEvidence(
                string.Equals(U2OpenWorldSettingsFixture.ResolveSemanticPage(observation), U2OpenWorldSettingsFixture.RootPage, StringComparison.Ordinal),
                "bounded completion", observation.SequenceNumber),
            (observation, candidate) => authorizationOverride?.Invoke(observation, candidate)
                ?? U2OpenWorldSettingsFixture.EvaluateAuthorization(observation, candidate),
            BranchInventoryEvaluator: U2OpenWorldSettingsFixture.EvaluateInventory);
        var envelope = IntentSemanticEnvelope.Project(
            "bounded open-world traversal", goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(U2OpenWorldSettingsFixture.Specification()));
        return new TestRun(fixture, environment, traversal, agent, envelope);
    }

    private sealed record TestRun(
        U2OpenWorldSettingsFixture Fixture,
        SemanticCapabilityTestEnvironment Environment,
        RuntimeTraversal Traversal,
        RuntimeAgent Agent,
        IntentSemanticEnvelope.Resolved Envelope);
}
