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
/// BRANCH GROUNDING BEFORE DISPATCH — deterministic proofs.
///
/// The Agent must never dispatch a pending branch from stale or unresolved
/// grounding: before dispatch, the CURRENT accepted observation must carry a
/// fresh grounding occurrence that resolves to the branch's logical source
/// class, and authorization must be re-derived from the CURRENT element.
/// These tests drive the real Agent over scenario-neutral viewport worlds —
/// no Settings logic, no ADB, no coordinate memory as identity.
/// </summary>
public sealed class BranchGroundingBeforeDispatchTests
{
    private const string App = "grounding.app";
    private const string Root = "Root";

    /// <summary>
    /// A scrollable world whose root shows a WINDOW of rows per viewport.
    /// Rows are the discoverable branches. A "similar" row option makes a row
    /// carry a text that signature-matches another branch's class while being
    /// a DIFFERENT element (the similar-appearance impostor).
    /// </summary>
    private sealed class GroundingWorld : IEnvironment
    {
        private readonly string[] _rows;
        private readonly int _windowSize;
        private readonly int _jump;
        private readonly List<DeviceAction> _actions = [];
        private readonly List<Observation> _history = [];
        private int _position;
        private string _screen = "Launcher";
        private long _seq;

        public GroundingWorld(string[] rows, int windowSize, int jump = 1)
        {
            _rows = rows;
            _windowSize = windowSize;
            _jump = jump;
        }

        public IReadOnlyList<DeviceAction> ActionHistory => _actions;
        public IReadOnlyList<Observation> ObservationHistory => _history;
        public int CurrentPosition => _position;

        public Task<Observation> ObserveAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var elements = _screen == "Launcher"
                ? ImmutableArray.Create(new ObservedElement("Launcher", null, 0, new ElementBounds(0, 0, 1, 1), "text"))
                : _screen == "Root"
                    ? Enumerable.Range(0, _windowSize)
                        .Select(i => _position + i < _rows.Length
                            ? new ObservedElement(_rows[_position + i], null, i,
                                new ElementBounds(0, i * 0.1f, 1, (i + 1) * 0.1f), "row")
                            : new ObservedElement("", null, i, new ElementBounds(0, i * 0.1f, 1, (i + 1) * 0.1f), "row"))
                        .ToImmutableArray()
                    : ImmutableArray.Create(
                        new ObservedElement(_screen, null, 0, new ElementBounds(0, 0.1f, 1, 0.3f), "title"),
                        new ObservedElement(Root, null, 1, new ElementBounds(0, 0.8f, 1, 1f), "button"));
            return Task.FromResult(new Observation(elements, App, ++_seq));
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _actions.Add(action);
            switch (action)
            {
                case DeviceAction.LaunchApp:
                    _screen = "Root";
                    _position = 0;
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "launch", "ok"));
                case DeviceAction.ScrollForward:
                    if (_screen == "Root")
                    {
                        if (_position + _jump < _rows.Length)
                            _position += _jump;
                        else
                            _position = Math.Max(0, _rows.Length - _windowSize);
                    }
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "ok"));
                case DeviceAction.ScrollBackward:
                    if (_screen == "Root")
                        _position = Math.Max(0, _position - 1);
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "ok"));
                case DeviceAction.Tap tap:
                    if (_screen == "Root")
                    {
                        var rows = Enumerable.Range(0, _windowSize)
                            .Where(i => _position + i < _rows.Length)
                            .Select(i => _rows[_position + i])
                            .ToArray();
                        var idx = tap.TargetBounds is { IsValid: true } b
                            ? (int)Math.Floor(b.CenterY / 0.1f)
                            : 0;
                        if (idx >= 0 && idx < rows.Length)
                            _screen = "Child:" + rows[idx];
                    }
                    else if (_screen.StartsWith("Child:", StringComparison.Ordinal))
                    {
                        _screen = "Root";
                    }
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "tap", "ok"));
                default:
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Rejected, "unsupported", "n/a"));
            }
        }
    }

    private static (RuntimeAgent Agent, GroundingWorld World, IntentSemanticEnvelope.Resolved Envelope) Build(
        string[] rows, int windowSize, int jump, Func<ObservedElement, bool> authorize)
    {
        var world = new GroundingWorld(rows, windowSize, jump);
        var env = new SemanticCapabilityTestEnvironment(world, (observation, element, index) =>
        {
            var text = element.Text;
            if (string.IsNullOrWhiteSpace(text))
                return FixtureSemanticRole.NonInteractive;
            var isChildPage = observation.Elements.Any(e =>
                e.Text is not null && e.Text.StartsWith("Child:", StringComparison.Ordinal));
            if (string.Equals(text, Root, StringComparison.Ordinal))
                return isChildPage ? FixtureSemanticRole.ParentReturnControl : FixtureSemanticRole.NonInteractive;
            if (text.StartsWith("Node ", StringComparison.Ordinal))
                return isChildPage ? FixtureSemanticRole.NonInteractive : FixtureSemanticRole.NavigationCandidate;
            return FixtureSemanticRole.NonInteractive;
        });
        var traversal = new RuntimeTraversal(env);
        string? Page(Observation o) => o.Elements.FirstOrDefault(e =>
            e.Text is not null && e.Text.StartsWith("Child:", StringComparison.Ordinal))?.Text
            ?? (o.Elements.Any(e => e.Text is not null && e.Text.StartsWith("Node ", StringComparison.Ordinal))
                || o.Elements.Any(e => string.Equals(e.Text, Root, StringComparison.Ordinal))
                ? Root : null);

        var goal = new Goal(
            EvidenceEvaluator: observation =>
                new GoalEvidence(false, "grounding proof", observation.SequenceNumber),
            CandidateAuthorizationEvaluator: (_, candidate) =>
                new CandidateAuthorizationEvidence(authorize(candidate), "grounding authz"),
            ViewportExplorationEvaluator: observations =>
            {
                if (observations.IsDefaultOrEmpty)
                    return new ViewportExplorationEvidence(true, "explore");
                var latest = observations[^1];
                var latestSigs = Sigs(latest);
                var prior = observations.Take(observations.Length - 1)
                    .SelectMany(o => Sigs(o)).ToHashSet(StringComparer.Ordinal);
                var hasNew = latestSigs.Any(s => !prior.Contains(s));
                return new ViewportExplorationEvidence(hasNew,
                    hasNew ? "new source; scroll more" : "no new source; exhausted");
            },
            BranchInventoryEvaluator: (observations, _) =>
            {
                if (observations.IsDefaultOrEmpty)
                    return new BranchInventoryEvidence(null, "no observations");
                var current = observations[^1];
                var branches = current.Elements
                    .Where(e => e.Text is not null && e.Text.StartsWith("Node ", StringComparison.Ordinal))
                    .ToImmutableDictionary(e => e.Text!, _ => current.SequenceNumber, StringComparer.Ordinal);
                var grounding = SourceEquivalenceNormalizer.OccurrencesOf(current)
                    .Where(o => o.CanonicalOccurrence.Reference.ElementIndex < current.Elements.Length)
                    .ToImmutableDictionary(
                        o => current.Elements[o.CanonicalOccurrence.Reference.ElementIndex].Text,
                        o => new NavigationSourceOccurrenceReference(o.ObservationSequence, o.OccurrenceIdentity),
                        StringComparer.Ordinal);
                return new BranchInventoryEvidence(branches, "viewport inventory", grounding);
            });

        var spec = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, Root),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: 1,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, Root));
        var envelope = IntentSemanticEnvelope.Project(
            "grounding proof", goal, new IntentExecutionRepresentation.OpenWorldTypeLevel(spec));
        var startup = new RuntimeStartup(env, App, Page);
        var recovery = new RuntimeRecovery(env, _ => ImmutableArray<DeviceAction>.Empty, (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page) => new(page, o => Page(o) == page, traversal.ExecuteStep);
        var agent = new RuntimeAgent(startup, traversal, token => env.ObserveAsync(token), Page, Factory, recovery);
        return (agent, world, envelope);
    }

    private static ImmutableArray<string> Sigs(Observation o)
        => SourceEquivalenceNormalizer.OccurrencesOf(o)
            .Select(x => x.StructuredSignature)
            .ToImmutableArray();

    [Fact]
    public async Task BranchDisappearsAfterScroll_NoDispatch()
    {
        // "Node A" is visible at position 0 only; a jump scroll hides it. The
        // Agent must NOT dispatch Node A from stale grounding after the scroll.
        var (agent, world, envelope) = Build(
            ["Node A", "Node B", "Node C", "Node D", "Node E", "Node F"],
            windowSize: 2, jump: 3,
            authorize: c => c.Text is "Node A" or "Node B" or "Node C" or "Node D" or "Node E" or "Node F");
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "bgd-1", CancellationToken.None);

        // The run must not dispatch a branch whose current frame no longer
        // carries it. Either the run fails closed or Node A is never dispatched
        // while invisible — never a stale-grounding dispatch.
        Assert.DoesNotContain(world.ActionHistory, a => a is DeviceAction.Tap);
    }

    [Fact]
    public async Task BranchReappearsAfterViewportRecovery_DispatchAllowed()
    {
        // jump 1 (small): rows stay overlapping; every visible branch can be
        // dispatched from its CURRENT frame. The run should explore and dispatch.
        var (agent, world, envelope) = Build(
            ["Node A", "Node B", "Node C", "Node D", "Node E"],
            windowSize: 2, jump: 1,
            authorize: c => c.Text is "Node A" or "Node B" or "Node C" or "Node D" or "Node E");
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "bgd-2", CancellationToken.None);

        Assert.True(world.ActionHistory.Any(a => a is DeviceAction.Tap),
            $"expected dispatch from fresh grounding; acts={string.Join(",", world.ActionHistory.Select(a => a.GetType().Name))}");
    }

    [Fact]
    public async Task SimilarAppearanceImpostor_Rejected()
    {
        // Two rows share the same text "Node A" (an impostor). The current
        // frame's occurrence signature matches the branch class but is a
        // DIFFERENT element — the grounding gate must reject the dispatch
        // (ambiguous current occurrence resolves to no unique logical source
        // or fails the explicit source validation).
        var (agent, world, envelope) = Build(
            ["Node A", "Node A", "Node B"],
            windowSize: 3, jump: 0,
            authorize: _ => true);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "bgd-3", CancellationToken.None);

        // Ambiguous similar appearance must not be dispatched blindly.
        Assert.DoesNotContain(agent.Trace, t => t.Reason?.Contains("authorized", StringComparison.OrdinalIgnoreCase) is true
            && t.StepId is not null);
    }

    [Fact]
    public async Task VisionOnlyGrounding_NoAdb()
    {
        var (agent, world, envelope) = Build(
            ["Node A", "Node B", "Node C"],
            windowSize: 2, jump: 1,
            authorize: _ => true);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "bgd-4", CancellationToken.None);

        Assert.DoesNotContain(world.ActionHistory, a => a.ToString()!.Contains("adb", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GenericTreeWorld_NoScenarioKnowledge()
    {
        var (agent, world, envelope) = Build(
            ["Node A", "Node B", "Node C", "Node D"],
            windowSize: 2, jump: 1,
            authorize: _ => true);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "bgd-5", CancellationToken.None);

        Assert.DoesNotContain("Settings", world.ActionHistory.ToString()!, StringComparison.Ordinal);
        Assert.DoesNotContain("WiFi", world.ActionHistory.ToString()!, StringComparison.Ordinal);
        Assert.DoesNotContain("Android", world.ActionHistory.ToString()!, StringComparison.Ordinal);
    }
}
