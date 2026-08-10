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

/// <summary>Formal L2 proof for SC-U2-MUS-001. It exercises the production Planning → Agent → Container → Traversal → Environment path.</summary>
public sealed class U2OpenWorldSettingsFormalScenarioTests
{
    [Fact]
    public async Task Positive_TraversesBothDynamicSiblingsReturnsAndCompletesFromFreshGoalEvidence()
    {
        var run = Create(U2OpenWorldSettingsFixture.Positive());

        var state = await RunAsync(run);

        Assert.Equal(RunState.Completed, state);
        Assert.Equal(
            new int?[] { 0, 0, 1, 0 },
            run.Environment.ActionHistory.OfType<DeviceAction.Tap>().Select(tap => tap.TargetElementIndex));
        Assert.Equal(4, run.Traversal.Journal.Count);
        Assert.All(run.Traversal.Journal, entry => Assert.NotNull(entry.PostActionObservation));
        Assert.Equal(6, Assert.Single(run.GoalEvidenceReceipts).SourceObservationSequence);
        var root = run.Agent.BranchProgress[U2OpenWorldSettingsFixture.RootPage];
        Assert.True(root.IsSubtreeComplete);
        Assert.Equal(2, root.CompletedSiblingEvidence.Count);
        Assert.Contains(run.Agent.Trace, entry => entry.Reason?.Contains("verified parent return", StringComparison.Ordinal) is true);
        Assert.DoesNotContain(run.Environment.ActionHistory.OfType<DeviceAction.Tap>(), tap => tap.TargetElementIndex == 2);
    }

    [Fact]
    public async Task UnresolvedInventory_StopsBeforeAnyDiscoveredBranchDispatchOrGoalEvaluation()
    {
        var run = Create(U2OpenWorldSettingsFixture.UnresolvedRoot());

        var state = await RunAsync(run);

        Assert.Equal(RunState.Failed, state);
        Assert.Empty(run.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Empty(run.GoalEvidenceReceipts);
    }

    [Fact]
    public async Task ACompleteWhileBPending_DoesNotEvaluateGoalOrCompleteEarly()
    {
        var run = Create(
            U2OpenWorldSettingsFixture.Positive(),
            authorization: (observation, candidate) => candidate.Text == U2OpenWorldSettingsFixture.BranchB
                ? new CandidateAuthorizationEvidence(false, "B remains explicitly rejected.")
                : U2OpenWorldSettingsFixture.EvaluateAuthorization(observation, candidate));

        var state = await RunAsync(run);

        Assert.Equal(RunState.Failed, state);
        Assert.Equal(2, run.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.Empty(run.GoalEvidenceReceipts);
        var root = run.Agent.BranchProgress[U2OpenWorldSettingsFixture.RootPage];
        Assert.True(root.CompletedSiblingEvidence.ContainsKey(U2OpenWorldSettingsFixture.BranchA));
        Assert.False(root.CompletedSiblingEvidence.ContainsKey(U2OpenWorldSettingsFixture.BranchB));
        Assert.False(root.IsSubtreeComplete);
    }

    [Theory]
    [InlineData("ambiguous")]
    [InlineData("rejected")]
    public async Task AmbiguousOrRejectedParentReturn_DoesNotDispatchReturnOrRecordChildCompletion(string mode)
    {
        var run = mode == "ambiguous"
            ? Create(U2OpenWorldSettingsFixture.AmbiguousParentReturn())
            : Create(
                U2OpenWorldSettingsFixture.Positive(),
                authorization: (observation, candidate) => candidate.Text == U2OpenWorldSettingsFixture.RootPage
                    ? new CandidateAuthorizationEvidence(false, "Parent return is rejected.")
                    : U2OpenWorldSettingsFixture.EvaluateAuthorization(observation, candidate));

        var state = await RunAsync(run);

        Assert.Equal(RunState.Failed, state);
        Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Empty(run.GoalEvidenceReceipts);
        Assert.Empty(run.Agent.BranchProgress[U2OpenWorldSettingsFixture.RootPage].CompletedSiblingEvidence);
    }

    [Theory]
    [InlineData("wrong")]
    [InlineData("stale")]
    public async Task WrongParentOrStaleObservation_DoesNotFabricateProgressOrCompletion(string mode)
    {
        var fixture = mode == "wrong"
            ? U2OpenWorldSettingsFixture.WrongParentReturn()
            : U2OpenWorldSettingsFixture.StaleChildObservation();
        var run = Create(fixture);

        var state = await RunAsync(run);

        Assert.Equal(RunState.Failed, state);
        Assert.Empty(run.GoalEvidenceReceipts);
        Assert.Empty(run.Agent.BranchProgress[U2OpenWorldSettingsFixture.RootPage].CompletedSiblingEvidence);
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RunState == RunState.Completed);
        Assert.Equal(mode == "wrong" ? 2 : 1, run.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
    }

    [Fact]
    public async Task UnsatisfiedFreshGoalEvidence_AfterVerifiedTraversalFailsInsteadOfMechanicalSuccess()
    {
        var run = Create(
            U2OpenWorldSettingsFixture.Positive(),
            goalEvidence: observation => new GoalEvidence(false, "Final fresh GoalEvidence is intentionally unsatisfied.", observation.SequenceNumber));

        var state = await RunAsync(run);

        Assert.Equal(RunState.Failed, state);
        Assert.Equal(4, run.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.True(run.Agent.BranchProgress[U2OpenWorldSettingsFixture.RootPage].IsSubtreeComplete);
        Assert.Equal(6, Assert.Single(run.GoalEvidenceReceipts).SourceObservationSequence);
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RunState == RunState.Completed);
    }

    [Fact]
    public async Task EqualInputs_ReplayEqualActionsObservationsJournalTraceProgressGoalEvidenceAndFinalState()
    {
        var first = await SnapshotAsync();
        var second = await SnapshotAsync();

        Assert.Equal(first, second);
    }

    private static Task<RunState> RunAsync(U2Run run)
        => IntentSemanticEnvelopeExecution.RunOpenWorldAsync(
            run.Agent, run.Envelope, run.Fixture.RunId, CancellationToken.None);

    private static U2Run Create(
        U2OpenWorldSettingsFixture fixture,
        Func<Observation, ObservedElement, CandidateAuthorizationEvidence>? authorization = null,
        Func<Observation, GoalEvidence>? goalEvidence = null)
    {
        var environment = fixture.Environment;
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(environment, "Settings", U2OpenWorldSettingsFixture.ResolveSemanticPage);
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => environment.ObserveAsync(cancellationToken),
            U2OpenWorldSettingsFixture.ResolveSemanticPage,
            page => new RuntimeContainer(
                page,
                observation => string.Equals(U2OpenWorldSettingsFixture.ResolveSemanticPage(observation), page, StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);
        var receipts = new List<GoalEvidence>();
        var evaluator = goalEvidence ?? (observation => new GoalEvidence(
            string.Equals(U2OpenWorldSettingsFixture.ResolveSemanticPage(observation), U2OpenWorldSettingsFixture.RootPage, StringComparison.Ordinal),
            "Fresh root GoalEvidence is satisfied.",
            observation.SequenceNumber));
        var goal = new Goal(
            observation =>
            {
                var evidence = evaluator(observation);
                receipts.Add(evidence);
                return evidence;
            },
            authorization ?? U2OpenWorldSettingsFixture.EvaluateAuthorization,
            BranchInventoryEvaluator: U2OpenWorldSettingsFixture.EvaluateInventory);
        var envelope = IntentSemanticEnvelope.Project(
            "Traverse Settings safe configuration items within depth <= 1.",
            goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(U2OpenWorldSettingsFixture.Specification()));
        return new U2Run(fixture, environment, traversal, agent, envelope, receipts);
    }

    private static async Task<FormalSnapshot> SnapshotAsync()
    {
        var run = Create(U2OpenWorldSettingsFixture.Positive());
        var state = await RunAsync(run);
        return new FormalSnapshot(
            state,
            run.Agent.Reason,
            string.Join("\n", run.Environment.ActionHistory.Select(CanonicalAction)),
            string.Join("\n", run.Environment.ObservationHistory.Select(CanonicalObservation)),
            string.Join("\n", run.Traversal.Journal.Select(CanonicalJournal)),
            string.Join("\n", run.Agent.Trace.Select(CanonicalTrace)),
            string.Join("\n", run.Agent.BranchProgress.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(CanonicalProgress)),
            string.Join("\n", run.GoalEvidenceReceipts.Select(evidence => $"{evidence.Satisfied}|{evidence.Reason}|{evidence.SourceObservationSequence}")));
    }

    private static string CanonicalAction(DeviceAction action) => action switch
    {
        DeviceAction.LaunchApp launch => $"Launch:{launch.ApplicationId}",
        DeviceAction.Tap tap => $"Tap:{tap.TargetElementIndex}",
        DeviceAction.SetSwitch set => $"Set:{set.TargetElementIndex}:{set.TargetState}",
        DeviceAction.ScrollForward => "ScrollForward",
        _ => action.GetType().Name,
    };

    private static string CanonicalObservation(Observation observation)
        => $"{observation.SequenceNumber}|{observation.ForegroundApplication}|"
           + string.Join(",", observation.Elements.Select(element => $"{element.Index}:{element.Text}:{element.SwitchState}"));

    private static string CanonicalJournal(UniClaw.Runtime.Traversal.TraversalJournalEntry entry)
        => $"{entry.StepId}|{entry.SelectedElementIndex}|{entry.DispatchedAction?.GetType().Name}|{entry.PostActionObservation?.SequenceNumber}|{entry.Result.GetType().Name}";

    private static string CanonicalTrace(TraceEvent entry)
        => $"{entry.RunId}|{entry.ContainerId}|{entry.StepId}|{entry.ActionId}|{entry.Action?.GetType().Name}|{entry.Reason}|{entry.RunState}";

    private static string CanonicalProgress(KeyValuePair<string, BranchProgressEvidence> entry)
        => $"{entry.Key}|approved:{string.Join(",", entry.Value.ApprovedSiblingEvidence.OrderBy(item => item.Key))}"
           + $"|completed:{string.Join(",", entry.Value.CompletedSiblingEvidence.OrderBy(item => item.Key))}";

    private sealed record U2Run(
        U2OpenWorldSettingsFixture Fixture,
        ScriptedEnvironment Environment,
        RuntimeTraversal Traversal,
        RuntimeAgent Agent,
        IntentSemanticEnvelope.Resolved Envelope,
        List<GoalEvidence> GoalEvidenceReceipts);

    private sealed record FormalSnapshot(
        RunState State,
        string? Reason,
        string Actions,
        string Observations,
        string Journal,
        string Trace,
        string Progress,
        string GoalEvidence);
}
