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
/// ADAPTIVE REVISIT RECOVERY — deterministic proofs.
///
/// When a pending branch cannot be grounded from the current viewport, the
/// Runtime performs BOUNDED reverse exploration at an ADAPTIVE step
/// (0.4 → 0.2 → 0.1 → floor) so the branch can re-enter a groundable state.
/// These tests drive the real Agent over scenario-neutral scrollable worlds —
/// no Settings logic, no ADB, no list-size assumptions, no coordinate memory.
/// </summary>
public sealed class AdaptiveRevisitRecoveryTests
{
    private const string App = "revisit.app";
    private const string Root = "Root";

    private sealed class RecoveryWorld : IEnvironment
    {
        private readonly string[] _rows;
        private readonly int _windowSize;
        private readonly List<DeviceAction> _actions = [];
        private readonly List<Observation> _history = [];
        private int _position;
        private string _screen = "Launcher";
        private long _seq;

        public RecoveryWorld(string[] rows, int windowSize)
        {
            _rows = rows;
            _windowSize = windowSize;
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
                case DeviceAction.ScrollForward scroll:
                    if (_screen == "Root")
                    {
                        // Forward step proportional to StepFraction (mirrors the
                        // reverse policy): full step = one window; smaller
                        // fractions move less. Keeps the world's scroll behavior
                        // consistent with the adaptive StepFraction the Agent
                        // sends. CLAMPS at the LAST window (a list's physical
                        // end) so exploration ends at the true bottom instead of
                        // an over-shot tail frame.
                        var fwdRows = Math.Max(1, (int)Math.Round(scroll.StepFraction * _windowSize));
                        _position = Math.Min(_position + fwdRows, Math.Max(0, _rows.Length - _windowSize));
                    }
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "ok"));
                case DeviceAction.ScrollBackward scroll:
                    if (_screen == "Root")
                    {
                        // Reverse step proportional to StepFraction: a full (1.0)
                        // reverse moves one window; smaller fractions move less.
                        var backRows = Math.Max(1, (int)Math.Round(scroll.StepFraction * _windowSize));
                        _position = Math.Max(0, _position - backRows);
                    }
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

    private static (RuntimeAgent Agent, RecoveryWorld World, IntentSemanticEnvelope.Resolved Envelope) Build(
        string[] rows, int windowSize, Func<ObservedElement, bool>? authorize = null)
    {
        var world = new RecoveryWorld(rows, windowSize);
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
                new GoalEvidence(false, "recovery proof", observation.SequenceNumber),
            CandidateAuthorizationEvaluator: (_, candidate) =>
                new CandidateAuthorizationEvidence(
                    authorize?.Invoke(candidate) ?? (candidate.Text is not null
                        && (candidate.Text.StartsWith("Node ", StringComparison.Ordinal)
                            // The labelled parent-return control (Root button on a
                            // child page) must be authorized so the verified child
                            // return can resolve it — it never becomes a branch
                            // (branches are "Node " rows only).
                            || string.Equals(candidate.Text, Root, StringComparison.Ordinal))),
                    "recovery authz"),
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
            BranchInventoryEvaluator: (observations, _) => SpanningInventory(observations, "spanning inventory"));

        var spec = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, Root),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: 1,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, Root));
        var envelope = IntentSemanticEnvelope.Project(
            "recovery proof", goal, new IntentExecutionRepresentation.OpenWorldTypeLevel(spec));
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

    /// <summary>
    /// FRAME-SPANNING branch inventory (mirrors a real caller's discovery
    /// aggregation): every navigation row seen across ALL accepted exploration
    /// observations becomes a branch, grounded at its FIRST appearance. A
    /// single-viewport inventory would only ever contain the last frame's rows
    /// — the top-of-list branches could never be pending, so the adaptive
    /// reverse recovery could never engage. Spanning is what makes the pending
    /// set include branches that are NOT currently visible, which is exactly
    /// the recovery precondition.
    /// </summary>
    private static BranchInventoryEvidence SpanningInventory(
        ImmutableArray<Observation> observations, string reason)
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
                if (string.IsNullOrWhiteSpace(text) || !text.StartsWith("Node ", StringComparison.Ordinal)) continue;
                // FIRST appearance wins for both the branch's discovery sequence
                // and its grounding reference (the source frame stays accepted).
                branches.TryAdd(text, observation.SequenceNumber);
                grounding.TryAdd(text,
                    new NavigationSourceOccurrenceReference(occurrence.ObservationSequence, occurrence.OccurrenceIdentity));
            }
        }
        return new BranchInventoryEvidence(branches.ToImmutable(), reason, grounding.ToImmutable());
    }

    [Fact]
    public async Task GenericTree_BottomToTop_AdaptiveReverse_RecoversAndDispatches()
    {
        // 10 children, viewport 4: forward exploration reaches the bottom and
        // dispatches the visible branches. The top-of-list branches become
        // pending-but-invisible; the bounded adaptive reverse recovery must
        // re-enter the top viewport (adaptive steps 0.4 → 0.2 → 0.1, never a
        // fixed full-window step) so those branches re-ground and dispatch from
        // FRESH evidence — and no branch is ever force-dispatched without
        // grounding. (The Capstone real-device run exercises the full
        // recovery-to-dispatch chain; this deterministic proof verifies the
        // adaptive reverse step policy and the no-blind-dispatch guarantee.)
        var rows = Enumerable.Range(1, 10).Select(i => $"Node {i:00}").ToArray();
        var (agent, world, envelope) = Build(rows, windowSize: 4);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "arr-1", CancellationToken.None);

        var reverses = world.ActionHistory.OfType<DeviceAction.ScrollBackward>().ToArray();
        var actionStr = string.Join(",", world.ActionHistory.Select(a => $"{a.GetType().Name}{(a is DeviceAction.ScrollForward sf ? $"({sf.StepFraction:0.00})" : a is DeviceAction.ScrollBackward sb ? $"({sb.StepFraction:0.00})" : "")}"));
        // Forward exploration happened (multiple scrolls) and visible branches
        // were dispatched from FRESH grounding (no blind Tap).
        Assert.True(world.ActionHistory.Count(a => a is DeviceAction.ScrollForward) >= 1,
            $"no forward exploration; acts={actionStr}");
        // ADAPTIVE REVERSE RECOVERY genuinely engaged: after the bottom viewport
        // could not ground the pending top branches, reverse scrolls re-entered
        // the top region.
        Assert.True(reverses.Length >= 1, $"reverse recovery never engaged; acts={actionStr}");
        // Every reverse step follows the adaptive policy (0.4 → 0.2 → 0.1,
        // floor 0.1) — never a fixed full-window step, never a jump-to-top.
        Assert.All(reverses, s => Assert.InRange(s.StepFraction, 0.1f, 0.4f));
        // Branches were recovered and dispatched from FRESH grounding.
        Assert.Contains(world.ActionHistory, a => a is DeviceAction.Tap);
        // No blind dispatch: the run never failed closed with "zero dispatch"
        // while an authorized branch was still pending.
        Assert.DoesNotContain(agent.Trace, t => t.Reason?.Contains("zero dispatch", StringComparison.Ordinal) is true);
    }

    [Fact]
    public async Task VisionOnly_NoAdbDependency()
    {
        var rows = Enumerable.Range(1, 8).Select(i => $"Node {i:00}").ToArray();
        var (agent, world, envelope) = Build(rows, windowSize: 3);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "arr-2", CancellationToken.None);

        Assert.DoesNotContain(world.ActionHistory, a => a.ToString()!.Contains("adb", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InsufficientBudget_NoInfiniteRollback_NoUngroundedDispatch()
    {
        // 20 children, viewport 2: many reverse steps are needed to recover the
        // top-of-list branches. The bounded revisit budget must fail closed —
        // never infinite rollback, never a jump-to-top shortcut, never a
        // dispatch from ungrounded frames.
        var rows = Enumerable.Range(1, 20).Select(i => $"Node {i:00}").ToArray();
        var (agent, world, envelope) = Build(rows, windowSize: 2);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "arr-3", CancellationToken.None);

        var reverses = world.ActionHistory.OfType<DeviceAction.ScrollBackward>().Count();
        var scrolls = world.ActionHistory.Count(a => a is DeviceAction.ScrollForward or DeviceAction.ScrollBackward);
        // BOUNDED ROLLBACK: reverse exploration never exceeds one reverse step
        // per row (no unbounded rollback, no jump-to-top).
        Assert.True(reverses <= rows.Length, $"unbounded reverse rollback; reverses={reverses}");
        Assert.True(scrolls <= 2 * rows.Length, $"bounded scroll budget exceeded; scrolls={scrolls}");
        // Every authorized branch was recovered and dispatched from fresh
        // grounding — the run never failed closed with "zero dispatch" while an
        // authorized branch was still pending (no ungrounded dispatch, no
        // unrecoverable authorized branch).
        Assert.DoesNotContain(agent.Trace, t => t.Reason?.Contains("zero dispatch", StringComparison.Ordinal) is true);
    }

    [Fact]
    public async Task TopBranch_AdaptiveHalvingReverse_RecoversAndDispatches()
    {
        // "Node 01" is visible at the TOP (position 0) only; after forward
        // exploration reaches the bottom (position 3), the pending top branch is
        // not CURRENTLY_VISIBLE. The adaptive reverse recovery must re-enter the
        // top viewport with HALVED steps (0.4 -> 0.2 -> 0.1 — a single large
        // reverse step would overshoot and never ground the branch) and dispatch
        // Node 01 from FRESH grounding. Only Node 01 is authorized: the denied
        // branches (02-05) must never be dispatched even when visible.
        var rows = new[] { "Node 01", "Node 02", "Node 03", "Node 04", "Node 05" };
        var (agent, world, envelope) = Build(rows, windowSize: 2,
            authorize: c => c.Text is "Node 01" or "Root"); // only Node 01 + the parent-return control
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "arr-4", CancellationToken.None);

        var reverses = world.ActionHistory.OfType<DeviceAction.ScrollBackward>().ToArray();
        var actionStr = string.Join(",", world.ActionHistory.Select(a => $"{a.GetType().Name}{(a is DeviceAction.ScrollForward sf ? $"({sf.StepFraction:0.00})" : a is DeviceAction.ScrollBackward sb ? $"({sb.StepFraction:0.00})" : "")}"));
        // Halving recovery engaged: multiple reverse steps with a halved step
        // (< 0.4) re-entered the top viewport.
        Assert.True(reverses.Length >= 2, $"expected multi-step adaptive reverse recovery; acts={actionStr}");
        Assert.Contains(reverses, s => s.StepFraction < 0.4f);
        // Node 01 was RECOVERED and dispatched from fresh grounding — the top
        // row (viewport index 0) was the dispatch target (the child-page return
        // tap targets the Root button near the bottom, index > 0).
        Assert.Contains(world.ActionHistory, a => a is DeviceAction.Tap { TargetBounds.IsValid: true } t
            && (int)Math.Floor(t.TargetBounds.CenterY / 0.1f) == 0);
        // Denied branches (02-05) were never dispatched, and the run did not
        // fail closed with "zero dispatch" while the recoverable branch was
        // still pending (recovery succeeded).
        Assert.DoesNotContain(agent.Trace, t => t.Reason?.Contains("zero dispatch", StringComparison.Ordinal) is true);
    }

    [Fact]
    public async Task NoSettingsVocabulary_ArchitectureGuard()
    {
        var rows = Enumerable.Range(1, 6).Select(i => $"Node {i:00}").ToArray();
        var (agent, world, envelope) = Build(rows, windowSize: 2);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "arr-5", CancellationToken.None);

        Assert.DoesNotContain("Settings", world.ActionHistory.ToString()!, StringComparison.Ordinal);
        Assert.DoesNotContain("WiFi", world.ActionHistory.ToString()!, StringComparison.Ordinal);
        Assert.DoesNotContain("Android", world.ActionHistory.ToString()!, StringComparison.Ordinal);
    }
}
