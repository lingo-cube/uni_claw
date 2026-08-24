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

/// <summary>
/// No-authority-escalation proof: the stateless decomposer participates in no
/// decision and holds no mutable state; a decomposed directive still routes
/// through <c>Agent.RunOpenWorldAsync</c>, so the RuntimeAgent keeps sole
/// run-level execution authority. Uses a Fake (scripted) environment consistent
/// with U2OpenWorldSettingsFixture and asserts the existing DFS path executes.
/// </summary>
public sealed class DirectiveDecomposerAuthorityTests
{
    [Fact]
    public async Task DecomposedDirective_RunsThroughTheExistingOpenWorldDfsSeam()
    {
        var fixture = U2OpenWorldSettingsFixture.Positive();
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
        var directive = new Directive(
            new TypeLevelTaskScope("Settings", U2OpenWorldSettingsFixture.RootPage),
            new TypeLevelEntryBoundary("Settings", U2OpenWorldSettingsFixture.RootPage),
            maximumDepth: 1,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new DirectiveStrategyRules(
                observation =>
                {
                    goalSequences.Add(observation.SequenceNumber);
                    return new GoalEvidence(
                        string.Equals(U2OpenWorldSettingsFixture.ResolveSemanticPage(observation), U2OpenWorldSettingsFixture.RootPage, StringComparison.Ordinal),
                        "Fresh root GoalEvidence is satisfied only after Agent derives bounded traversal completion.",
                        observation.SequenceNumber);
                },
                U2OpenWorldSettingsFixture.EvaluateAuthorization,
                BranchInventoryEvaluator: U2OpenWorldSettingsFixture.EvaluateInventory));

        var decomposition = DirectiveDecomposer.Decompose(directive);
        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(decomposition);

        var state = await DirectiveExecution.RunDirectiveAsync(
            agent, resolved, fixture.RunId, CancellationToken.None);

        // The DFS path executed to completion: both safe branches were traversed,
        // the dangerous candidate was never tapped, and the Agent derived terminal
        // completion from its OWN authority (not the decomposer).
        Assert.Equal(RunState.Completed, state);
        Assert.Equal(4, environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.DoesNotContain(environment.ActionHistory.OfType<DeviceAction.Tap>(),
            tap => tap.TargetElementIndex == 2);
        Assert.Contains(agent.Trace, entry => entry.Reason?.Contains("verified parent return", StringComparison.Ordinal) is true);
        Assert.NotEmpty(goalSequences);
    }
}
