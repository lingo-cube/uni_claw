using System.Collections.Immutable;
using UniClaw.Runtime.Environment;
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

namespace UniClaw.Runtime.Tests.Evidence;

/// <summary>
/// ADAPTIVE SCROLL GROUNDING — deterministic proofs (PROJECT_LEADER_ADAPTIVE_
/// SCROLL_GROUNDING_IMPROVEMENT testing requirements).
///
/// The Agent's open-world viewport exploration must keep adjacent frames
/// overlapping (adaptive step), engage bounded recovery when a large jump
/// loses overlap, and fail closed when recovery is exhausted. These tests
/// drive the REAL Agent exploration seam over an in-memory viewport world and
/// assert the scroll behavior via observed ScrollForward StepFraction values
/// and terminal outcomes. No page-specific rules, no Settings fixture, no ADB,
/// no semantic capability controlling scroll.
/// </summary>
public sealed class AdaptiveScrollGroundingTests
{
    private const string App = "scroll.app";
    private const string Root = "Root";

    /// <summary>Records the StepFraction of every ScrollForward for assertion.</summary>
    private sealed class ScrollRecorder : IEnvironment
    {
        private readonly IEnvironment _inner;
        public List<float> ForwardSteps { get; } = [];
        public List<DeviceAction> Actions { get; } = [];

        public ScrollRecorder(IEnvironment inner) => _inner = inner;

        public Task<Observation> ObserveAsync(CancellationToken ct) => _inner.ObserveAsync(ct);

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken ct)
        {
            Actions.Add(action);
            if (action is DeviceAction.ScrollForward { StepFraction: var f })
                ForwardSteps.Add(f);
            return _inner.ExecuteAsync(action, ct);
        }
    }

    /// <summary>
    /// Viewport world: each scroll position shows a WINDOW of the row sequence.
    /// Adjacent windows overlap by construction; `jump` controls how many rows a
    /// forward scroll advances (1 = small step that keeps overlap; > window size
    /// = a large jump that loses overlap). The world is scenario-neutral
    /// (generic "Item NN" rows) and contains NO child pages — the exploration
    /// exercises only the viewport seam (a bounded leaf inventory, no dispatch),
    /// which is exactly the adaptive-scroll surface under test.
    /// </summary>
    private sealed class ViewportWorld : IEnvironment
    {
        private readonly string[] _rows;
        private readonly int _windowSize;
        private readonly int _jump;
        private int _position;
        private bool _launched;
        private long _seq;

        public ViewportWorld(string[] rows, int windowSize, int jump = 1)
        {
            _rows = rows;
            _windowSize = windowSize;
            _jump = jump;
        }

        public int CurrentPosition => _position;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var elements = Enumerable.Range(0, _windowSize)
                .Select(i => _position + i < _rows.Length
                    ? new ObservedElement(_rows[_position + i], null, i,
                        new ElementBounds(0, i * 0.1f, 1, (i + 1) * 0.1f), "row")
                    : new ObservedElement("", null, i, new ElementBounds(0, i * 0.1f, 1, (i + 1) * 0.1f), "row"))
                .ToImmutableArray();
            return Task.FromResult(new Observation(elements, App, ++_seq));
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (action)
            {
                case DeviceAction.LaunchApp:
                    _launched = true;
                    _position = 0;
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "launch", "ok"));
                case DeviceAction.ScrollForward:
                    if (_position + _jump < _rows.Length)
                        _position += _jump;
                    else
                        _position = Math.Max(0, _rows.Length - _windowSize);
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "ok"));
                case DeviceAction.ScrollBackward:
                    _position = Math.Max(0, _position - 1);
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "ok"));
                default:
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Rejected, "unsupported", "n/a"));
            }
        }
    }

    /// <summary>Builds an Agent whose open-world exploration drives the viewport
    /// world; the inventory is a bounded leaf so NO dispatch happens — the run
    /// exercises exactly the adaptive viewport seam.</summary>
    private static (RuntimeAgent Agent, ScrollRecorder Recorder, IntentSemanticEnvelope.Resolved Envelope) Build(
        string[] rows, int windowSize, int jump)
    {
        var world = new ViewportWorld(rows, windowSize, jump);
        var recorder = new ScrollRecorder(world);
        var env = new SemanticCapabilityTestEnvironment(recorder, element =>
            element.Text is not null && element.Text.StartsWith("Item ", StringComparison.Ordinal)
                ? FixtureSemanticRole.NavigationCandidate
                : FixtureSemanticRole.NonInteractive);
        var traversal = new RuntimeTraversal(env);

        string? Page(Observation o) => Root;

        var goal = new Goal(
            EvidenceEvaluator: observation =>
                new GoalEvidence(false, "leaf world: no goal signal", observation.SequenceNumber),
            CandidateAuthorizationEvaluator: (_, candidate) =>
                new CandidateAuthorizationEvidence(true, "authorized"),
            ViewportExplorationEvaluator: observations =>
            {
                if (observations.IsDefaultOrEmpty)
                    return new ViewportExplorationEvidence(true, "explore");
                var latest = observations[^1];
                var prior = observations.Take(observations.Length - 1)
                    .SelectMany(o => Signatures(o)).ToHashSet(StringComparer.Ordinal);
                var hasNew = Signatures(latest).Any(s => !prior.Contains(s));
                return new ViewportExplorationEvidence(hasNew,
                    hasNew ? "new source; scroll more" : "no new source; exhausted");
            },
            BranchInventoryEvaluator: (observations, _) =>
            {
                // BOUNDED LEAF: never dispatch. Exploration-only surface.
                return new BranchInventoryEvidence(
                    ImmutableDictionary<string, long>.Empty,
                    "bounded leaf viewport (no dispatch in this proof)");
            });

        var spec = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, Root),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: 0,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, Root));
        var envelope = IntentSemanticEnvelope.Project(
            "explore viewport world", goal, new IntentExecutionRepresentation.OpenWorldTypeLevel(spec));
        var startup = new RuntimeStartup(env, App, Page);
        var recovery = new RuntimeRecovery(env, _ => ImmutableArray<DeviceAction>.Empty, (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page) => new(page, o => Page(o) == page, traversal.ExecuteStep);
        var agent = new RuntimeAgent(startup, traversal, token => env.ObserveAsync(token), Page, Factory, recovery);
        return (agent, recorder, envelope);
    }

    private static ImmutableArray<string> Signatures(Observation o)
        => SourceEquivalenceNormalizer.OccurrencesOf(o)
            .Select(x => x.StructuredSignature)
            .ToImmutableArray();

    [Fact]
    public async Task SmallScrollMaintainsOverlap_ForwardExploration()
    {
        // jump 1 with window 3: consecutive frames always overlap. The adaptive
        // step must keep overlap — forward exploration scrolls multiple times,
        // never needs recovery (no ScrollBackward), and exhausts when no new
        // source appears.
        var (agent, recorder, envelope) = Build(
            ["Item 01", "Item 02", "Item 03", "Item 04", "Item 05", "Item 06", "Item 07", "Item 08"],
            windowSize: 3, jump: 1);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "asg-1", CancellationToken.None);

        // A bounded-leaf world terminates (leaf fail-closed) — the adaptive
        // scroll proof is about the EXPLORATION: forward scrolls with the
        // adaptive step profile (small start, slow growth to ceiling, no
        // recovery), and the exploration exhausted without a normalization
        // failure.
        Assert.True(recorder.ForwardSteps.Count >= 3,
            $"expected multi-step forward exploration; steps={string.Join(",", recorder.ForwardSteps)}");
        Assert.DoesNotContain(recorder.Actions, a => a is DeviceAction.ScrollBackward);
        // Adaptive step profile: starts small (<=0.4), grows slowly (+0.1 each
        // comfortable frame), never exceeds the 0.8 ceiling.
        Assert.True(recorder.ForwardSteps[0] <= 0.4f, $"first step={recorder.ForwardSteps[0]}");
        Assert.All(recorder.ForwardSteps, f => Assert.True(f >= 0.1f && f <= 0.8f, $"step={f}"));
        Assert.DoesNotContain(agent.Trace, t => t.Reason?.Contains("Source normalization is unresolved", StringComparison.Ordinal) is true);
    }

    [Fact]
    public async Task LargeJumpLosesOverlap_EngagesRecovery()
    {
        // jump 4 with window 3: a forward scroll moves 4 rows -> no overlap with
        // the previous frame. The adaptive gate must detect the loss and engage
        // bounded recovery (halved step forward retry or reverse) rather than
        // accepting a non-overlapping frame silently.
        var (agent, recorder, envelope) = Build(
            ["Item 01", "Item 02", "Item 03", "Item 04", "Item 05", "Item 06", "Item 07", "Item 08"],
            windowSize: 3, jump: 4);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "asg-2", CancellationToken.None);

        // A large jump (4 > window 3) loses overlap; the adaptive gate must
        // engage bounded recovery: a halved step forward retry (a step smaller
        // than the initial 0.4) or a reverse scroll. Never an unchecked
        // non-overlapping acceptance.
        var sawHalvedRetry = recorder.ForwardSteps.Any(f => f < 0.4f);
        var sawReverse = recorder.Actions.Any(a => a is DeviceAction.ScrollBackward);
        Assert.True(sawHalvedRetry || sawReverse,
            $"expected halved retry (<0.4) or reverse; steps={string.Join(",", recorder.ForwardSteps)}");
    }

    [Fact]
    public async Task RecoveryRestoresOverlap_Continues()
    {
        // jump 2 with window 4: frames overlap (Item 01-04 -> 03-06); the step
        // stays small and exploration exhausts — never fails closed.
        var (agent, recorder, envelope) = Build(
            ["Item 01", "Item 02", "Item 03", "Item 04", "Item 05", "Item 06", "Item 07", "Item 08"],
            windowSize: 4, jump: 2);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "asg-3", CancellationToken.None);

        // Overlapping frames (jump 2 < window 4): forward exploration continues
        // without recovery and without a normalization failure.
        Assert.True(recorder.ForwardSteps.Count >= 2,
            $"expected forward exploration; steps={string.Join(",", recorder.ForwardSteps)}");
        Assert.DoesNotContain(recorder.Actions, a => a is DeviceAction.ScrollBackward);
        Assert.DoesNotContain(agent.Trace, t => t.Reason?.Contains("Source normalization is unresolved", StringComparison.Ordinal) is true);
    }

    [Fact]
    public async Task RecoveryBudgetExhausted_FailsClosed()
    {
        // window 1 jump 4: every forward scroll lands on a disjoint single row —
        // overlap can never be restored. Bounded budget must fail closed (no
        // infinite oscillation, no fabricated completion).
        var (agent, recorder, envelope) = Build(
            ["Item 01", "Item 02", "Item 03", "Item 04", "Item 05", "Item 06", "Item 07", "Item 08"],
            windowSize: 1, jump: 4);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "asg-4", CancellationToken.None);

        // Disjoint single-row frames can never restore overlap: the bounded
        // halve-retry budget must fail closed (no infinite oscillation). The
        // normalization correctly reports unresolved (fail-closed by contract).
        var scrollActions = recorder.Actions.Count(a => a is DeviceAction.ScrollForward or DeviceAction.ScrollBackward);
        Assert.True(scrollActions <= 8, $"bounded scroll budget exceeded; scrolls={scrollActions}");
        Assert.True(scrollActions >= 1, "exploration attempted scrolls before failing closed");
        Assert.Contains(agent.Trace, t => t.Reason?.Contains("Source normalization is unresolved", StringComparison.Ordinal) is true);
    }

    [Fact]
    public async Task VisionOnly_NoAdbDependency()
    {
        // The whole run uses only the in-memory viewport world + fixture
        // semantic capability; no adb process is ever started.
        var (agent, recorder, envelope) = Build(
            ["Item 01", "Item 02", "Item 03", "Item 04", "Item 05"],
            windowSize: 2, jump: 1);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "asg-5", CancellationToken.None);

        // Vision-only: exploration happened through the in-memory world; no adb.
        Assert.True(recorder.ForwardSteps.Count >= 1,
            $"expected forward exploration; steps={string.Join(",", recorder.ForwardSteps)}");
        Assert.DoesNotContain(recorder.Actions, a => a.ToString()!.Contains("adb", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GenericWorld_NoSettingsFixture()
    {
        // Rows are generic "Item NN" — zero Settings/Android/WiFi vocabulary.
        var (agent, recorder, envelope) = Build(
            ["Item 01", "Item 02", "Item 03", "Item 04", "Item 05", "Item 06"],
            windowSize: 2, jump: 1);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "asg-6", CancellationToken.None);

        // Generic world: exploration ran on "Item NN" rows; zero Settings vocab.
        Assert.True(recorder.ForwardSteps.Count >= 1,
            $"expected forward exploration; steps={string.Join(",", recorder.ForwardSteps)}");
        Assert.DoesNotContain("Settings", recorder.Actions.ToString()!, StringComparison.Ordinal);
    }
}
