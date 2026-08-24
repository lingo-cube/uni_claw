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
/// SCROLL STABILITY CONFIRMATION — deterministic proofs.
///
/// After a scroll the page may keep moving (inertia / bounce-back): frames
/// captured mid-settle must NEVER become the decision basis. The Runtime
/// re-observes (bounded) and accepts a frame only once two consecutive
/// observations show the SAME viewport (identical navigation-signature set and
/// stable row positions). These tests drive the real Agent over a settle-
/// physics world: after a scroll the target row is observed at position A,
/// then B, then C, then stays at C (stable). Assertions use the world's own
/// frame values (no fixed coordinates), no Settings/ADB vocabulary, no fixed
/// action counts.
/// </summary>
public sealed class ScrollStabilityConfirmationTests
{
    private const string App = "stability.app";
    private const string Root = "Root";
    private const string Target = "Target";

    /// <summary>
    /// A settle-physics world: after a ScrollForward the target row is
    /// observed at settling positions A → B → C, then stays at C (stable).
    /// The stable variant confirms; the never-stable variant keeps moving so
    /// the confirmation budget must fail closed.
    /// </summary>
    private sealed class SettleWorld : IEnvironment
    {
        private readonly bool _neverStable;
        private readonly List<DeviceAction> _actions = [];
        private int _obsSinceScroll = int.MaxValue; // int.MaxValue = not post-scroll
        private string _screen = "Launcher";
        private long _seq;

        public SettleWorld(bool neverStable) => _neverStable = neverStable;

        public IReadOnlyList<DeviceAction> ActionHistory => _actions;

        /// <summary>The target's STABLE (confirmed) position — world-provided, not hard-coded.</summary>
        public float StableRowY => 0.4f;

        /// <summary>The target's mid-settle positions (must never be dispatched).</summary>
        public float[] SettlingRowYs => [0.2f, 0.3f];

        public Task<Observation> ObserveAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _seq++;
            if (_screen == "Launcher")
            {
                return Task.FromResult(new Observation(
                    ImmutableArray.Create(new ObservedElement("Launcher", null, 0, new ElementBounds(0, 0, 1, 1), "text")),
                    App, _seq));
            }
            var elements = ImmutableArray.CreateBuilder<ObservedElement>(3);
            // A non-interactive Root title gives the page a stable identity
            // (its bounds are fixed, so it never perturbs the settle signal).
            elements.Add(new ObservedElement(Root, null, 0, new ElementBounds(0, 0.95f, 1, 1f), "title"));
            elements.Add(new ObservedElement("Fill 01", null, 1, new ElementBounds(0, 0.8f, 1, 0.9f), "row"));
            if (_obsSinceScroll != int.MaxValue)
            {
                // Post-scroll: the target is observed at a settling position
                // that changes frame to frame until it stabilizes.
                var idx = _obsSinceScroll;
                _obsSinceScroll++;
                var targetY = _neverStable
                    ? 0.2f + idx * 0.05f
                    : idx switch { 0 => 0.2f, 1 => 0.3f, _ => StableRowY };
                elements.Add(new ObservedElement(Target, null, 2, new ElementBounds(0, targetY, 1, targetY + 0.1f), "row"));
            }
            return Task.FromResult(new Observation(elements.ToImmutable(), App, _seq));
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _actions.Add(action);
            switch (action)
            {
                case DeviceAction.LaunchApp:
                    _screen = "Root";
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "launch", "ok"));
                case DeviceAction.ScrollForward:
                    if (_screen == "Root")
                        _obsSinceScroll = 0; // the page starts settling
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "ok"));
                case DeviceAction.Tap tap:
                    if (_screen == "Root")
                        _screen = "Child:" + Target;
                    else if (_screen.StartsWith("Child:", StringComparison.Ordinal))
                        _screen = "Root";
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "tap", "ok"));
                default:
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Rejected, "unsupported", "n/a"));
            }
        }
    }

    private static (RuntimeAgent Agent, SettleWorld World, IntentSemanticEnvelope.Resolved Envelope) Build(bool neverStable)
    {
        var world = new SettleWorld(neverStable);
        var env = new SemanticCapabilityTestEnvironment(world, (observation, element, index) =>
        {
            var text = element.Text;
            if (string.IsNullOrWhiteSpace(text))
                return FixtureSemanticRole.NonInteractive;
            var isChildPage = observation.Elements.Any(e =>
                e.Text is not null && e.Text.StartsWith("Child:", StringComparison.Ordinal));
            if (string.Equals(text, Root, StringComparison.Ordinal))
                return isChildPage ? FixtureSemanticRole.ParentReturnControl : FixtureSemanticRole.NonInteractive;
            if (string.Equals(text, Target, StringComparison.Ordinal) || text.StartsWith("Fill ", StringComparison.Ordinal))
                return isChildPage ? FixtureSemanticRole.NonInteractive : FixtureSemanticRole.NavigationCandidate;
            return FixtureSemanticRole.NonInteractive;
        });
        var traversal = new RuntimeTraversal(env);
        string? Page(Observation o) => o.Elements.FirstOrDefault(e =>
            e.Text is not null && e.Text.StartsWith("Child:", StringComparison.Ordinal))?.Text
            ?? (o.Elements.Any(e => string.Equals(e.Text, Target, StringComparison.Ordinal))
                || o.Elements.Any(e => string.Equals(e.Text, Root, StringComparison.Ordinal))
                ? Root : null);

        var goal = new Goal(
            EvidenceEvaluator: observation =>
                new GoalEvidence(false, "stability proof", observation.SequenceNumber),
            CandidateAuthorizationEvaluator: (_, candidate) =>
                new CandidateAuthorizationEvidence(
                    string.Equals(candidate.Text, Target, StringComparison.Ordinal)
                        || string.Equals(candidate.Text, Root, StringComparison.Ordinal),
                    "stability authz"),
            ViewportExplorationEvaluator: observations =>
            {
                if (observations.IsDefaultOrEmpty)
                    return new ViewportExplorationEvidence(true, "explore");
                var latest = observations[^1];
                if (latest.Elements.Any(e => string.Equals(e.Text, Target, StringComparison.Ordinal)))
                    return new ViewportExplorationEvidence(false, "target visible; exhausted");
                var latestSigs = SourceEquivalenceNormalizer.OccurrencesOf(latest)
                    .Select(x => x.StructuredSignature).ToHashSet(StringComparer.Ordinal);
                var prior = observations.Take(observations.Length - 1)
                    .SelectMany(o => SourceEquivalenceNormalizer.OccurrencesOf(o))
                    .Select(x => x.StructuredSignature).ToHashSet(StringComparer.Ordinal);
                return new ViewportExplorationEvidence(
                    latestSigs.Any(s => !prior.Contains(s)),
                    "new source appeared; scroll more");
            },
            BranchInventoryEvaluator: (observations, _) =>
            {
                if (observations.IsDefaultOrEmpty)
                    return new BranchInventoryEvidence(null, "no observations");
                var branches = ImmutableDictionary.CreateBuilder<string, long>(StringComparer.Ordinal);
                var grounding = ImmutableDictionary.CreateBuilder<string, NavigationSourceOccurrenceReference>(StringComparer.Ordinal);
                foreach (var observation in observations)
                {
                    foreach (var occurrence in SourceEquivalenceNormalizer.OccurrencesOf(observation))
                    {
                        if (!occurrence.CanonicalOccurrence.EligibleForAuthorization) continue;
                        var index = occurrence.CanonicalOccurrence.Reference.ElementIndex;
                        if (index < 0 || index >= observation.Elements.Length) continue;
                        var text = observation.Elements[index].Text;
                        if (string.IsNullOrWhiteSpace(text)) continue;
                        branches.TryAdd(text, observation.SequenceNumber);
                        grounding.TryAdd(text,
                            new NavigationSourceOccurrenceReference(occurrence.ObservationSequence, occurrence.OccurrenceIdentity));
                    }
                }
                return new BranchInventoryEvidence(branches.ToImmutable(), "spanning inventory", grounding.ToImmutable());
            });

        var spec = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, Root),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: 1,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, Root));
        var envelope = IntentSemanticEnvelope.Project(
            "stability proof", goal, new IntentExecutionRepresentation.OpenWorldTypeLevel(spec));
        var startup = new RuntimeStartup(env, App, Page);
        var recovery = new RuntimeRecovery(env, _ => ImmutableArray<DeviceAction>.Empty, (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page) => new(page, o => Page(o) == page, traversal.ExecuteStep);
        var agent = new RuntimeAgent(startup, traversal, token => env.ObserveAsync(token), Page, Factory, recovery);
        return (agent, world, envelope);
    }

    [Fact]
    public async Task StabilityConfirmed_AcceptedFrameIsStable_NoSettlingFrameDispatched()
    {
        // The target settles A → B → C → C. The dispatch must use the STABLE
        // (confirmed) frame's position C — never the mid-settle A or B.
        var (agent, world, envelope) = Build(neverStable: false);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "ssc-1", CancellationToken.None);

        // A dispatch tap exists at the STABLE position (world-provided), and NO
        // tap at any mid-settle position — the settling frames were never used.
        Assert.Contains(world.ActionHistory,
            a => a is DeviceAction.Tap { TargetBounds.IsValid: true } t
                && Math.Abs(t.TargetBounds.Y1 - world.StableRowY) < 0.01f);
        foreach (var settlingY in world.SettlingRowYs)
        {
            Assert.DoesNotContain(world.ActionHistory,
                a => a is DeviceAction.Tap { TargetBounds.IsValid: true } t
                    && Math.Abs(t.TargetBounds.Y1 - settlingY) < 0.01f);
        }
        // The stability confirmation engaged (trace evidence).
        Assert.Contains(agent.Trace, t => t.Reason?.Contains("scroll stability CONFIRMED", StringComparison.Ordinal) is true);
        // Vision-only, no Settings vocabulary, no ADB.
        Assert.DoesNotContain(world.ActionHistory, a => a.ToString()!.Contains("adb", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Settings", world.ActionHistory.ToString()!, StringComparison.Ordinal);
        Assert.DoesNotContain("WiFi", world.ActionHistory.ToString()!, StringComparison.Ordinal);
        Assert.DoesNotContain("Android", world.ActionHistory.ToString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NeverStable_FailsClosed_NoDispatch()
    {
        // The page NEVER stops moving: the bounded confirmation budget must fail
        // closed — no unstable frame is ever accepted or dispatched.
        var (agent, world, envelope) = Build(neverStable: true);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "ssc-2", CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.DoesNotContain(world.ActionHistory, a => a is DeviceAction.Tap);
        Assert.Contains(agent.Trace, t => t.Reason?.Contains("scroll stability budget exhausted", StringComparison.Ordinal) is true);
    }
}
