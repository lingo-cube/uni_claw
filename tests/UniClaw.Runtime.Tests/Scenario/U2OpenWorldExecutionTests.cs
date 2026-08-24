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

/// <summary>Task 2.1 production-shaped tests for SC-U2-MUS-001 only.</summary>
public sealed class U2OpenWorldExecutionTests
{
    [Fact]
    public async Task Positive_UsesDynamicSiblingsVerifiedReturnsAndFreshGoalEvidence()
    {
        var run = Create(U2OpenWorldSettingsFixture.Positive());

        var state = await IntentExecution.RunOpenWorldAsync(
            run.Agent, run.Envelope, run.Fixture.RunId, CancellationToken.None);

        Assert.Equal(RunState.Completed, state);
        Assert.Equal(4, run.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.True(run.Traversal.Journal.Count == 4);
        // POST-ACTION SETTLE: final confirmed observation sequence (was 6 pre-settle).
        Assert.Equal(10, Assert.Single(run.GoalEvidenceSequences));
        var progress = run.Agent.BranchProgress[U2OpenWorldSettingsFixture.RootPage];
        Assert.True(progress.IsSubtreeComplete);
        Assert.Contains(U2OpenWorldSettingsFixture.BranchA, progress.CompletedSiblingEvidence.Keys);
        Assert.Contains(U2OpenWorldSettingsFixture.BranchB, progress.CompletedSiblingEvidence.Keys);
        Assert.DoesNotContain(run.Environment.ActionHistory.OfType<DeviceAction.Tap>(), tap => tap.TargetElementIndex == 2);
        Assert.Contains(run.Agent.Trace, entry => entry.Reason?.Contains("verified parent return", StringComparison.Ordinal) is true);
    }

    [Fact]
    public async Task UnresolvedInventory_PerformsNoTapAndNeverEvaluatesGoal()
    {
        var run = Create(U2OpenWorldSettingsFixture.UnresolvedRoot());

        var state = await IntentExecution.RunOpenWorldAsync(
            run.Agent, run.Envelope, run.Fixture.RunId, CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.Empty(run.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Empty(run.GoalEvidenceSequences);
    }

    [Theory]
    [InlineData("ambiguous")]
    [InlineData("wrong")]
    [InlineData("stale")]
    public async Task ReturnOrFreshnessFailure_NeverFabricatesBranchOrGoalCompletion(string variant)
    {
        var fixture = variant switch
        {
            "ambiguous" => U2OpenWorldSettingsFixture.AmbiguousParentReturn(),
            "wrong" => U2OpenWorldSettingsFixture.WrongParentReturn(),
            "stale" => U2OpenWorldSettingsFixture.StaleChildObservation(),
            _ => throw new ArgumentOutOfRangeException(nameof(variant)),
        };
        var run = Create(fixture);

        var state = await IntentExecution.RunOpenWorldAsync(
            run.Agent, run.Envelope, run.Fixture.RunId, CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.Empty(run.GoalEvidenceSequences);
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RunState == RunState.Completed);
        Assert.False(run.Agent.BranchProgress[U2OpenWorldSettingsFixture.RootPage].IsSubtreeComplete);
    }

    [Fact]
    public async Task ClosedWorldEnvelope_IsRejectedBeforeRuntimeActivity()
    {
        var run = Create(U2OpenWorldSettingsFixture.Positive());
        var closed = IntentSemanticEnvelope.Project(
            "closed",
            run.Envelope.Goal,
            new IntentExecutionRepresentation.ClosedWorldConcrete(new Plan([])));

        await Assert.ThrowsAsync<ArgumentException>(() => IntentExecution.RunOpenWorldAsync(
            run.Agent, closed, run.Fixture.RunId, CancellationToken.None));

        Assert.Empty(run.Environment.ActionHistory);
        Assert.Equal(RunState.Idle, run.Agent.State);
    }

    private static U2Run Create(U2OpenWorldSettingsFixture fixture)
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
        var goalSequences = new List<long>();
        var goal = new Goal(
            observation =>
            {
                goalSequences.Add(observation.SequenceNumber);
                return new GoalEvidence(
                    string.Equals(U2OpenWorldSettingsFixture.ResolveSemanticPage(observation), U2OpenWorldSettingsFixture.RootPage, StringComparison.Ordinal),
                    "Fresh root GoalEvidence is satisfied only after Agent derives bounded traversal completion.",
                    observation.SequenceNumber);
            },
            U2OpenWorldSettingsFixture.EvaluateAuthorization,
            BranchInventoryEvaluator: U2OpenWorldSettingsFixture.EvaluateInventory);
        var envelope = IntentSemanticEnvelope.Project(
            "Traverse Settings safe configuration items within depth <= 1.",
            goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(U2OpenWorldSettingsFixture.Specification()));
        return new U2Run(fixture, environment, traversal, agent, envelope, goalSequences);
    }

    private sealed record U2Run(
        U2OpenWorldSettingsFixture Fixture,
        SemanticCapabilityTestEnvironment Environment,
        RuntimeTraversal Traversal,
        RuntimeAgent Agent,
        IntentSemanticEnvelope.Resolved Envelope,
        List<long> GoalEvidenceSequences);
}
