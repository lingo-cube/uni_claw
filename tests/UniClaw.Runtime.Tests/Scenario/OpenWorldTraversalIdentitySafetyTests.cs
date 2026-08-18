using System.Collections.Immutable;
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

public sealed class OpenWorldTraversalIdentitySafetyTests
{
    private const string App = "settings";

    private static readonly ImmutableDictionary<TypeLevelElementCategory, TypeLevelHandling> NavigatePolicy =
        ImmutableDictionary.CreateRange(new Dictionary<TypeLevelElementCategory, TypeLevelHandling>
        {
            [TypeLevelElementCategory.NavigableContainer] = TypeLevelHandling.EnterAndTraverse,
        });

    private sealed record Harness(
        RuntimeAgent Agent,
        ScriptedEnvironment Environment,
        RuntimeTraversal Traversal,
        IntentSemanticEnvelope.Resolved Envelope,
        List<GoalEvidence> Evidence);

    private static Harness Build(
        string root,
        IReadOnlyDictionary<string, string[]> inventory,
        ScreenConfig[] screens,
        string? goalText = null,
        int maximumDepth = 5)
    {
        var env = new ScriptedEnvironment(root, root, screens);
        var traversal = new RuntimeTraversal(env);
        var startup = new RuntimeStartup(env, App, Resolve);
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page) => new(page, o => Resolve(o) == page, traversal.ExecuteStep, forwardsAuthorizationReceipts: true);
        var evidence = new List<GoalEvidence>();

        var goal = new Goal(
            EvidenceEvaluator: observation =>
            {
                var satisfied = goalText is not null
                    && observation.Elements.Any(e => e.Text == goalText && e.SwitchState is true);
                var item = new GoalEvidence(
                    satisfied,
                    satisfied ? "Goal satisfied from fresh evidence." : "Goal not satisfied.",
                    observation.SequenceNumber);
                evidence.Add(item);
                return item;
            },
            CandidateAuthorizationEvaluator: (_, _) =>
                new CandidateAuthorizationEvidence(true, "safe navigation"),
            BranchInventoryEvaluator: (observations, _) =>
            {
                var latest = observations[^1];
                var page = Resolve(latest);
                var branches = page is not null && inventory.TryGetValue(page, out var list)
                    ? list
                    : [];
                return new BranchInventoryEvidence(
                    branches.ToImmutableDictionary(b => b, _ => latest.SequenceNumber, StringComparer.Ordinal),
                    $"inventory for {page ?? "unknown"} at seq={latest.SequenceNumber}");
            },
            CategoryClassifier: element =>
                string.IsNullOrEmpty(element.Text)
                    ? null
                    : TypeLevelElementCategory.NavigableContainer);

        var spec = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, root),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: maximumDepth,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, root),
            new TypeLevelDispatchPolicy(NavigatePolicy));

        var envelope = IntentSemanticEnvelope.Project(
            "full tree traversal",
            goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(spec));

        var agent = new RuntimeAgent(startup, traversal, _ => env.ObserveAsync(default), Resolve, Factory, recovery);
        return new Harness(agent, env, traversal, envelope, evidence);
    }

    private static string? Resolve(Observation o)
    {
        if (o.Elements.Any(e => e.Text == "@A")) return "A";
        if (o.Elements.Any(e => e.Text == "@B")) return "B";
        if (o.Elements.Any(e => e.Text == "@C")) return "C";
        return null;
    }

    private static ElementConfig Marker(string page)
        => new(page, null, null, null, "text");

    private static ElementConfig Nav(string text, string next)
        => new(text, null, new TransitionConfig(ScreenTransitionAction.Tap, next), null, "menuItem");

    private static ElementConfig ReturnTo(string parent)
        => new(parent, null, new TransitionConfig(ScreenTransitionAction.Tap, parent), null, "menuItem");

    private static ElementConfig GoalTrue()
        => new("Goal", true, null, null, "toggle");

    [Fact]
    public async Task OWI1_AncestryCycle_Rejected_NoChildDispatch()
    {
        var h = Build(
            "A",
            new Dictionary<string, string[]>
            {
                ["A"] = ["B"],
                ["B"] = ["A"],
            },
            [
                new ScreenConfig("A", App, [Marker("@A"), Nav("B", "B")]),
                new ScreenConfig("B", App, [Marker("@B"), Nav("A", "A")]),
            ]);

        var state = await IntentExecution.RunOpenWorldAsync(h.Agent, h.Envelope, "owi-1", CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.Contains("identity safety", h.Agent.Reason, StringComparison.Ordinal);
        Assert.Contains("cycle", h.Agent.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.DoesNotContain(h.Evidence, e => e.Satisfied);
    }

    [Fact]
    public async Task OWI2_DuplicateSemanticPageAcrossBranches_FailsClosed()
    {
        var h = Build(
            "A",
            new Dictionary<string, string[]>
            {
                ["A"] = ["B1", "B2"],
                ["B"] = [],
            },
            [
                new ScreenConfig("A", App, [Marker("@A"), Nav("B1", "B"), Nav("B2", "B")]),
                new ScreenConfig("B", App, [Marker("@B"), ReturnTo("A")]),
            ]);

        var state = await IntentExecution.RunOpenWorldAsync(h.Agent, h.Envelope, "owi-2", CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.Contains("duplicate semantic page identity", h.Agent.Reason, StringComparison.Ordinal);
        // First branch entered B and returned; second branch dispatched but duplicate child entry was rejected.
        Assert.Equal(3, h.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.DoesNotContain(h.Evidence, e => e.Satisfied);
    }

    [Fact]
    public async Task OWI3_UniqueTree_TraversalCompletes()
    {
        var h = Build(
            "A",
            new Dictionary<string, string[]>
            {
                ["A"] = ["B"],
                ["B"] = ["C"],
                ["C"] = [],
            },
            [
                new ScreenConfig("A", App, [Marker("@A"), Nav("B", "B"), GoalTrue()]),
                new ScreenConfig("B", App, [Marker("@B"), Nav("C", "C"), ReturnTo("A")]),
                new ScreenConfig("C", App, [Marker("@C"), ReturnTo("B")]),
            ],
            goalText: "Goal");

        var state = await IntentExecution.RunOpenWorldAsync(h.Agent, h.Envelope, "owi-3", CancellationToken.None);

        Assert.Equal(RunState.Completed, state);
        Assert.Equal(4, h.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.Contains(h.Evidence, e => e.Satisfied);
        var root = h.Agent.BranchProgress["A"];
        Assert.True(root.CompletedSiblingEvidence.ContainsKey("B"));
    }

    [Fact]
    public async Task OWI4_ParentEvidencePreserved_AfterRejectedCycle()
    {
        var h = Build(
            "A",
            new Dictionary<string, string[]>
            {
                ["A"] = ["B", "C"],
                ["B"] = [],
                ["C"] = ["A"],
            },
            [
                new ScreenConfig("A", App, [Marker("@A"), Nav("B", "B"), Nav("C", "C")]),
                new ScreenConfig("B", App, [Marker("@B"), ReturnTo("A")]),
                new ScreenConfig("C", App, [Marker("@C"), Nav("A", "A")]),
            ]);

        var state = await IntentExecution.RunOpenWorldAsync(h.Agent, h.Envelope, "owi-4", CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.Contains("identity safety", h.Agent.Reason, StringComparison.Ordinal);
        var root = h.Agent.BranchProgress["A"];
        Assert.True(root.CompletedSiblingEvidence.ContainsKey("B"));
        Assert.False(root.CompletedSiblingEvidence.ContainsKey("C"));
        Assert.DoesNotContain(h.Evidence, e => e.Satisfied);
    }

    [Fact]
    public async Task OWI5_GoalEvidenceSourceRemainsFresh_NoIdentityLeakage()
    {
        var h = Build(
            "A",
            new Dictionary<string, string[]>
            {
                ["A"] = ["B"],
                ["B"] = ["C"],
                ["C"] = [],
            },
            [
                new ScreenConfig("A", App, [Marker("@A"), Nav("B", "B"), GoalTrue()]),
                new ScreenConfig("B", App, [Marker("@B"), Nav("C", "C"), ReturnTo("A")]),
                new ScreenConfig("C", App, [Marker("@C"), ReturnTo("B")]),
            ],
            goalText: "Goal");

        var state = await IntentExecution.RunOpenWorldAsync(h.Agent, h.Envelope, "owi-5", CancellationToken.None);

        Assert.Equal(RunState.Completed, state);
        var final = h.Environment.ObservationHistory[^1];
        var satisfied = Assert.Single(h.Evidence, e => e.Satisfied);
        Assert.Equal(final.SequenceNumber, satisfied.SourceObservationSequence);
        // Identity evidence is run-local and must not leak into GoalEvidence or BranchProgress keys.
        Assert.DoesNotContain(h.Agent.BranchProgress.Keys, k => k is "@A" or "@B" or "@C");
    }
}
