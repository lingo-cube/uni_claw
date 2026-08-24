using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.World;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SC-3: authority boundary exceeded → RuntimeDecision Escalate. A bounded exploration
/// directive expects execution possible; the Fake world exposes an ancestry cycle, the
/// DFS fails closed on Open-world identity safety (depth-cutoff-style authority boundary),
/// and the reconciler classifies Escalate. Escalate is a RECORD — the RuntimeAgent does
/// not perform an escalation action; the caller observes the record.
/// </summary>
public sealed class ReconciliationScenario3EscalateTests
{
    private const string App = "settings";

    private static readonly ImmutableDictionary<TypeLevelElementCategory, TypeLevelHandling> NavigatePolicy =
        ImmutableDictionary.CreateRange(new Dictionary<TypeLevelElementCategory, TypeLevelHandling>
        {
            [TypeLevelElementCategory.NavigableContainer] = TypeLevelHandling.EnterAndTraverse,
        });

    [Fact]
    public async Task AuthorityBoundaryExceeded_ProducesEscalateRecord_WithNoEscalationAction()
    {
        // Ancestry cycle: A -> B -> A. The DFS detects the cycle and fails closed
        // with an Open-world identity safety reason (authority boundary, not exhaustion).
        var world = new ScriptedEnvironment(
            "A",
            "A",
            new[]
            {
                new ScreenConfig("A", App, ImmutableArray.Create(
                    Marker("@A"),
                    Nav("B", "B"))),
                new ScreenConfig("B", App, ImmutableArray.Create(
                    Marker("@B"),
                    Nav("A", "A"))),
            });
        var environment = new SemanticCapabilityTestEnvironment(world,
            element => element.Text is "A" or "B" ? FixtureSemanticRole.NavigationCandidate : null);
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(environment, App, Resolve);
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page)
            => new(page, o => Resolve(o) == page, traversal.ExecuteStep, forwardsAuthorizationReceipts: true);
        var agent = new RuntimeAgent(
            startup, traversal, _ => environment.ObserveAsync(default), Resolve, Factory, recovery);

        var directive = new Directive(
            new TypeLevelTaskScope(App, "A"),
            new TypeLevelEntryBoundary(App, "A"),
            maximumDepth: 5,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new DirectiveStrategyRules(
                observation => new GoalEvidence(false, "Cycle run never satisfies Goal.", observation.SequenceNumber),
                CandidateAuthorizationEvaluator: (_, _) =>
                    new CandidateAuthorizationEvidence(true, "safe navigation"),
                BranchInventoryEvaluator: (observations, _) =>
                {
                    var latest = observations[^1];
                    var page = Resolve(latest);
                    var branches = page switch
                    {
                        "A" => new[] { "B" },
                        "B" => new[] { "A" },
                        _ => Array.Empty<string>(),
                    };
                    var occurrences = SourceEquivalenceNormalizer.OccurrencesOf(latest);
                    var grounding = branches.ToImmutableDictionary(branch => branch,
                        _ => new NavigationSourceOccurrenceReference(latest.SequenceNumber, occurrences.Single().OccurrenceIdentity), StringComparer.Ordinal);
                    return new BranchInventoryEvidence(
                        branches.ToImmutableDictionary(b => b, _ => latest.SequenceNumber, StringComparer.Ordinal),
                        $"cycle inventory for {page ?? "unknown"} at seq={latest.SequenceNumber}", grounding);
                },
                CategoryClassifier: element => string.IsNullOrEmpty(element.Text)
                    ? null
                    : TypeLevelElementCategory.NavigableContainer),
            dispatchPolicy: new TypeLevelDispatchPolicy(NavigatePolicy));

        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(directive));

        var ledger = new ExecutionHypothesisLedger(resolved, "recon-s3-run");
        var state = await DirectiveExecution.RunDirectiveAsync(
            agent, resolved, "recon-s3-run", CancellationToken.None, ledger);

        // The DFS failed closed on the authority boundary: Failed RunState + an
        // Open-world identity safety reason in the trace.
        Assert.Equal(RunState.Failed, state);
        Assert.Contains(agent.Trace, t => t.RunState == RunState.Failed
            && t.Reason?.Contains("Open-world identity safety", StringComparison.Ordinal) is true);

        // The reconciler classifies Escalate — the authority boundary was exceeded.
        var decision = ledger.LatestDecision;
        Assert.NotNull(decision);
        Assert.Equal(RuntimeDecisionState.Escalate, decision!.State);
        Assert.Contains("authority", decision.DecisionReason, StringComparison.OrdinalIgnoreCase);

        // Escalate is a RECORD: the RuntimeAgent performed no escalation action — the
        // RunState is the Agent's own DFS fail-closed result, and the agent never
        // dispatched an escalation; only the bounded cycle dispatch occurred.
        Assert.Equal(agent.State, state);
        Assert.Equal(RunState.Failed, agent.State);
        Assert.Single(environment.ActionHistory.OfType<DeviceAction.Tap>());
    }

    private static string? Resolve(Observation observation)
    {
        if (observation.Elements.Any(e => e.Text == "@A")) return "A";
        if (observation.Elements.Any(e => e.Text == "@B")) return "B";
        return null;
    }

    private static ElementConfig Marker(string page)
        => new(page, null, null, null, "text");

    private static ElementConfig Nav(string text, string next)
        => new(text, null, new TransitionConfig(ScreenTransitionAction.Tap, next), new ElementBounds(0f, 0.1f, 1f, 0.3f), "menuItem");
}
