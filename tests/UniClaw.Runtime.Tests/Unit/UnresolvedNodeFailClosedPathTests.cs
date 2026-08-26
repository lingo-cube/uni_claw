using System.Collections.Immutable;
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

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// Spec Req 2 "Unclassifiable node fails closed" on the REAL Agent open-world
/// run path: when a CONFIGURED CategoryClassifier returns null for a discovered
/// required branch, the node is recorded unresolved in the ledger and no rule
/// is inferred — never authorized, never dispatched (no Tap fallback), never
/// counted visited. The no-classifier legacy path is exercised by the other
/// suites and must remain unchanged.
/// </summary>
public sealed class UnresolvedNodeFailClosedPathTests
{
    private const string App = "com.example.unresolved";

    private static readonly ImmutableDictionary<TypeLevelElementCategory, TypeLevelHandling> SafeMenuPolicy =
        ImmutableDictionary.CreateRange(new Dictionary<TypeLevelElementCategory, TypeLevelHandling>
        {
            [TypeLevelElementCategory.NavigableContainer] = TypeLevelHandling.EnterAndTraverse,
        });

    // 'Mystery' is the UNCLASSIFIABLE element: the classifier returns null for it.
    private static (RuntimeAgent Agent, ScriptedEnvironment Env, IntentSemanticEnvelope.Resolved Envelope) Create()
    {
        var screens = new[]
        {
            new ScreenConfig("Launcher", null, []),
            new ScreenConfig("Root", App, [
                new ElementConfig("Network&internet", null, new TransitionConfig(ScreenTransitionAction.Tap, "NetworkPage")),
                new ElementConfig("Mystery", null, new TransitionConfig(ScreenTransitionAction.Tap, "MysteryPage")),
            ]),
            new ScreenConfig("NetworkPage", App, [
                new ElementConfig("Root", null, new TransitionConfig(ScreenTransitionAction.Tap, "Root")),
                new ElementConfig("Internet", null, null),
            ]),
            new ScreenConfig("MysteryPage", App, [
                new ElementConfig("Root", null, new TransitionConfig(ScreenTransitionAction.Tap, "Root")),
                new ElementConfig("Deep", null, null),
            ]),
        };
        var env = new ScriptedEnvironment("Launcher", "Root", WithPrimaryBounds(screens));
        var semanticEnv = new SemanticCapabilityTestEnvironment(env, element =>
            element.Text is "Root" or "NetworkPage"
                ? FixtureSemanticRole.ParentReturnControl
                : FixtureSemanticRole.NavigationCandidate);
        var traversal = new RuntimeTraversal(semanticEnv);

        static string? Page(Observation o) =>
            o.Elements.Any(e => e.Text == "Network&internet") && o.Elements.Any(e => e.Text == "Mystery") ? "Root"
            : o.Elements.Any(e => e.Text == "Root") && o.Elements.Any(e => e.Text == "Internet") ? "NetworkPage"
            : o.Elements.Any(e => e.Text == "Root") && o.Elements.Any(e => e.Text == "Deep") ? "MysteryPage"
            : null;

        var goal = new Goal(
            EvidenceEvaluator: observation =>
                new GoalEvidence(Page(observation) == "Root", "probe goal.", observation.SequenceNumber),
            CandidateAuthorizationEvaluator: (_, candidate) =>
                new CandidateAuthorizationEvidence(true, $"safe receipt: {candidate.Text}"),
            BranchInventoryEvaluator: (observations, _) =>
            {
                var latest = observations[^1];
                var required = Page(latest) switch
                {
                    "Root" => new[] { "Network&internet", "Mystery" },
                    _ => Array.Empty<string>(),
                };
                var branches = required
                    .ToImmutableDictionary(t => t, _ => latest.SequenceNumber, StringComparer.Ordinal);
                var occurrences = SourceEquivalenceNormalizer.OccurrencesOf(latest);
                var grounding = required.ToImmutableDictionary(
                    branch => branch,
                    branch =>
                    {
                        var occurrence = occurrences.FirstOrDefault(candidate =>
                            candidate.CanonicalOccurrence.Reference.ElementIndex < latest.Elements.Length
                            && string.Equals(latest.Elements[candidate.CanonicalOccurrence.Reference.ElementIndex].Text, branch, StringComparison.Ordinal));
                        return new NavigationSourceOccurrenceReference(
                            latest.SequenceNumber,
                            occurrence?.OccurrenceIdentity ?? $"missing:{branch}");
                    }, StringComparer.Ordinal);
                return branches.Count > 0
                    ? new BranchInventoryEvidence(branches, $"inventory: {branches.Count} branches", grounding)
                    : new BranchInventoryEvidence(ImmutableDictionary<string, long>.Empty, "bounded leaf");
            },
            // Classification unavailable for 'Mystery' — the fail-closed case.
            CategoryClassifier: element =>
                element.Text == "Mystery" ? (TypeLevelElementCategory?)null
                : TypeLevelElementCategory.NavigableContainer);

        var spec = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, "Root"),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            3,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, "Root"),
            new TypeLevelDispatchPolicy(SafeMenuPolicy));

        var envelope = IntentSemanticEnvelope.Project(
            "unclassifiable branch fails closed", goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(spec));

        RuntimeContainer Factory(string page) => new(page, o => Page(o) == page, traversal.ExecuteStep);
        var startup = new RuntimeStartup(semanticEnv, App, Page);
        var recovery = new RuntimeRecovery(semanticEnv, _ => ImmutableArray<DeviceAction>.Empty, (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(startup, traversal, token => semanticEnv.ObserveAsync(token), Page, Factory, recovery);
        return (agent, env, envelope);
    }

    private static IEnumerable<ScreenConfig> WithPrimaryBounds(IEnumerable<ScreenConfig> screens) =>
        screens.Select(screen => screen with
        {
            Elements = screen.Elements.Select((element, index) => element with
            {
                Bounds = element.Bounds ?? new ElementBounds(0, index * 0.08f, 1, (index + 1) * 0.08f)
            }).ToImmutableArray()
        });

    [Fact]
    public async Task UnclassifiableRequiredBranch_NeverDispatched_RecordedUnresolvedInLedger()
    {
        var (agent, env, envelope) = Create();
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "unresolved-fail-closed", CancellationToken.None);

        // The classifiable sibling still completes normally (the Run terminates
        // through the existing verified-return / terminal logic — no new
        // termination path); the unclassifiable node never blocks it.
        Assert.True(state == RunState.Completed,
            $"state={state} reason={agent.Reason}\ntrace={string.Join(" | ", agent.Trace.Select(t => $"[{t.ContainerId}] {t.Reason}"))}");

        // ZERO dispatch of the unclassifiable node: on the Root screen 'Mystery'
        // occupies the second row (center Y in [0.08, 0.16)); no Tap may target it.
        var mysteryRowTaps = env.ActionHistory
            .OfType<DeviceAction.Tap>()
            .Where(t => t.TargetBounds is { } b && b.CenterY >= 0.08f && b.CenterY < 0.16f)
            .ToList();
        Assert.Empty(mysteryRowTaps);

        // Never an authorized obligation, never completed, and its child page
        // never became a traversal scope.
        var rootProgress = agent.BranchProgress["Root"];
        Assert.DoesNotContain("Mystery", rootProgress.AuthorizedSiblingEvidence);
        Assert.DoesNotContain("Mystery", rootProgress.CompletedSiblingEvidence);
        Assert.DoesNotContain(agent.BranchProgress, kv => kv.Key == "MysteryPage");

        // A Trace event records the fail-closed unresolved disposition.
        Assert.Contains(agent.Trace, t =>
            t.ContainerId == "Root"
            && t.Reason?.Contains("unclassifiable", StringComparison.Ordinal) is true
            && t.Reason.Contains("recorded unresolved", StringComparison.Ordinal));

        // The Agent-owned unresolved evidence feeds the ledger projection:
        // Unresolved >= 1 for the scope, and Visited excludes the node (only
        // the classifiable sibling completed with evidence).
        Assert.True(agent.UnresolvedNodes.GetValueOrDefault("Root") >= 1,
            $"unresolvedNodes={string.Join(',', agent.UnresolvedNodes.Select(kv => $"{kv.Key}={kv.Value}"))}");
        Assert.Throws<InvalidOperationException>(() => agent.CompileExplorationLedgerView());
    }
}
