using System.Collections.Immutable;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Harness;
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
/// BOUNDED REMEDIATION (WI-QA-4) — five authorized fixes to the EXISTING
/// post-scroll stability gate, verified through the PUBLIC read surface only:
/// (1) freshness sequence-number check; (2) factual failure classification
/// (re-observe failed / left container — no ReorderOrSignatureMismatch
/// masquerade); (3) backward revisit terminal parity; (4) Surface B real
/// projection verification via RuntimeEventProjector; (5) lifecycle sync.
///
/// Zero new RuntimeEventKind/wire DTO/DriverHost method/callback/mid-Run
/// transport. Same owner seam; fail-closed direction unchanged.
/// </summary>
public sealed class QuiescenceBoundedRemediationTests
{
    private const string App = "qa.app";
    private const string Root = "Root";
    private const string Target = "Target";

    // ── Scripted frame building blocks ──────────────────────────────────────

    private sealed record RowSpec(string Text, float Y1, string PerceptionType = "row");
    private sealed record FrameSpec(params RowSpec[] Rows);

    private static RowSpec Row(string text, float y1) => new(text, y1);

    private static FrameSpec CleanS => new(Row("S", 0.5f));

    private static FrameSpec WithTarget(params RowSpec[] rows)
    {
        var all = new RowSpec[rows.Length + 1];
        all[0] = Row(Target, 0.4f);
        Array.Copy(rows, 0, all, 1, rows.Length);
        return new FrameSpec(all);
    }

    private static FrameSpec LeftContainer => new(Row("Child:Target", 0.4f));

    // ── Standard fake open-world environment (mirrors RedTests/HandoffTests) ─

    private sealed class QuiescenceWorld : IEnvironment
    {
        private readonly FrameSpec[] _postScroll;
        private readonly List<DeviceAction> _actions = [];
        private int _obsSinceScroll = int.MaxValue;
        private string _screen = "Launcher";
        private long _seq;

        public QuiescenceWorld(FrameSpec[] postScroll) => _postScroll = postScroll;
        public IReadOnlyList<DeviceAction> ActionHistory => _actions;

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
            var elements = ImmutableArray.CreateBuilder<ObservedElement>();
            elements.Add(new ObservedElement(Root, null, 0, new ElementBounds(0, 0.95f, 1, 1f), "title"));
            elements.Add(new ObservedElement("Fill 01", null, 1, new ElementBounds(0, 0.8f, 1, 0.9f), "row"));
            if (_obsSinceScroll != int.MaxValue)
            {
                var idx = _obsSinceScroll;
                _obsSinceScroll++;
                var frame = idx < _postScroll.Length ? _postScroll[idx] : _postScroll[^1];
                var ei = 2;
                foreach (var row in frame.Rows)
                {
                    elements.Add(new ObservedElement(row.Text, null, ei,
                        new ElementBounds(0, row.Y1, 1, row.Y1 + 0.1f), row.PerceptionType));
                    ei++;
                }
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
                        _obsSinceScroll = 0;
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "ok"));
                case DeviceAction.Tap:
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

    // ── Stale-frame world: returns the SAME seq repeatedly after scroll ──────

    /// <summary>
    /// After scroll, every observation returns the SAME (cached/stale) sequence
    /// number — even though the frame content is identical and would otherwise
    /// confirm stability. Principle 1 (Fresh Observation) MUST reject these as
    /// not fresh: no confirmation, budget exhausted, fail closed.
    /// </summary>
    private sealed class StaleFrameWorld : IEnvironment
    {
        private readonly FrameSpec _postScrollFrame;
        private readonly List<DeviceAction> _actions = [];
        private int _obsSinceScroll = int.MaxValue;
        private string _screen = "Launcher";
        private long _seq;
        private long _staleSeq;
        private bool _staleFrozen;

        public StaleFrameWorld(FrameSpec postScrollFrame) => _postScrollFrame = postScrollFrame;
        public IReadOnlyList<DeviceAction> ActionHistory => _actions;

        public Task<Observation> ObserveAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (_screen == "Launcher")
            {
                return Task.FromResult(new Observation(
                    ImmutableArray.Create(new ObservedElement("Launcher", null, 0, new ElementBounds(0, 0, 1, 1), "text")),
                    App, ++_seq));
            }
            var elements = ImmutableArray.CreateBuilder<ObservedElement>();
            elements.Add(new ObservedElement(Root, null, 0, new ElementBounds(0, 0.95f, 1, 1f), "title"));
            elements.Add(new ObservedElement("Fill 01", null, 1, new ElementBounds(0, 0.8f, 1, 0.9f), "row"));
            if (_obsSinceScroll != int.MaxValue)
            {
                // Freeze the sequence at the first post-scroll frame; all
                // subsequent observations replay the SAME stale seq (cached).
                if (!_staleFrozen)
                {
                    _staleSeq = ++_seq;
                    _staleFrozen = true;
                }
                _obsSinceScroll++;
                var ei = 2;
                foreach (var row in _postScrollFrame.Rows)
                {
                    elements.Add(new ObservedElement(row.Text, null, ei,
                        new ElementBounds(0, row.Y1, 1, row.Y1 + 0.1f), row.PerceptionType));
                    ei++;
                }
                return Task.FromResult(new Observation(elements.ToImmutable(), App, _staleSeq));
            }
            return Task.FromResult(new Observation(elements.ToImmutable(), App, ++_seq));
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
                        _obsSinceScroll = 0;
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "ok"));
                case DeviceAction.Tap:
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

    // ── Throw-once world: the first re-observe inside stability throws ──────

    /// <summary>
    /// The first post-scroll observation (from the traversal) succeeds; the
    /// second observation (the first re-observe inside
    /// ConfirmScrollStabilityAsync) throws once. The gate MUST classify this as
    /// "re-observe failed" (NOT "reorder/signature mismatch") and fail closed.
    /// </summary>
    private sealed class ThrowOnceWorld : IEnvironment
    {
        private readonly FrameSpec _postScrollFrame;
        private readonly List<DeviceAction> _actions = [];
        private int _obsSinceScroll = int.MaxValue;
        private string _screen = "Launcher";
        private long _seq;
        private bool _thrown;

        public ThrowOnceWorld(FrameSpec postScrollFrame) => _postScrollFrame = postScrollFrame;
        public IReadOnlyList<DeviceAction> ActionHistory => _actions;

        public Task<Observation> ObserveAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (_screen == "Launcher")
            {
                return Task.FromResult(new Observation(
                    ImmutableArray.Create(new ObservedElement("Launcher", null, 0, new ElementBounds(0, 0, 1, 1), "text")),
                    App, ++_seq));
            }
            // The first re-observe after the initial post-scroll frame throws.
            if (_obsSinceScroll == 1 && !_thrown)
            {
                _thrown = true;
                throw new InvalidOperationException("fake re-observe failure");
            }
            var elements = ImmutableArray.CreateBuilder<ObservedElement>();
            elements.Add(new ObservedElement(Root, null, 0, new ElementBounds(0, 0.95f, 1, 1f), "title"));
            elements.Add(new ObservedElement("Fill 01", null, 1, new ElementBounds(0, 0.8f, 1, 0.9f), "row"));
            if (_obsSinceScroll != int.MaxValue)
            {
                _obsSinceScroll++;
                var ei = 2;
                foreach (var row in _postScrollFrame.Rows)
                {
                    elements.Add(new ObservedElement(row.Text, null, ei,
                        new ElementBounds(0, row.Y1, 1, row.Y1 + 0.1f), row.PerceptionType));
                    ei++;
                }
            }
            return Task.FromResult(new Observation(elements.ToImmutable(), App, ++_seq));
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
                        _obsSinceScroll = 0;
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "ok"));
                case DeviceAction.Tap:
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

    // ── Build helpers ────────────────────────────────────────────────────────

    private static (RuntimeAgent Agent, IntentSemanticEnvelope.Resolved Envelope)
        Build(IEnvironment world)
    {
        var env = new SemanticCapabilityTestEnvironment(world, (observation, element, index) =>
        {
            var text = element.Text;
            if (string.IsNullOrWhiteSpace(text))
                return FixtureSemanticRole.NonInteractive;
            var isChildPage = observation.Elements.Any(e =>
                e.Text is not null && e.Text.StartsWith("Child:", StringComparison.Ordinal));
            if (string.Equals(text, Root, StringComparison.Ordinal))
                return isChildPage ? FixtureSemanticRole.ParentReturnControl : FixtureSemanticRole.NonInteractive;
            if (string.Equals(text, Target, StringComparison.Ordinal)
                || text.StartsWith("Fill ", StringComparison.Ordinal)
                || string.Equals(text, "S", StringComparison.Ordinal)
                || string.Equals(text, "A", StringComparison.Ordinal)
                || string.Equals(text, "B", StringComparison.Ordinal))
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
                new GoalEvidence(false, "quiescence proof", observation.SequenceNumber),
            CandidateAuthorizationEvaluator: (_, candidate) =>
                new CandidateAuthorizationEvidence(
                    string.Equals(candidate.Text, Target, StringComparison.Ordinal)
                        || string.Equals(candidate.Text, Root, StringComparison.Ordinal),
                    "quiescence authz"),
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
            "quiescence proof", goal, new IntentExecutionRepresentation.OpenWorldTypeLevel(spec));
        var startup = new RuntimeStartup(env, App, Page);
        var recovery = new RuntimeRecovery(env, _ => ImmutableArray<DeviceAction>.Empty, (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page) => new(page, o => Page(o) == page, traversal.ExecuteStep);
        var agent = new RuntimeAgent(startup, traversal, token => env.ObserveAsync(token), Page, Factory, recovery);
        return (agent, envelope);
    }

    // ── Trace helpers ────────────────────────────────────────────────────────

    private static int? ConfirmedAttempt(IReadOnlyList<DecisionRecord> trace)
    {
        var ev = trace.FirstOrDefault(t =>
            t.Reason is not null && t.Reason.Contains("scroll stability CONFIRMED", StringComparison.Ordinal));
        if (ev is null) return null;
        var reason = ev.Reason!;
        var marker = "attempt ";
        var idx = reason.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return null;
        var start = idx + marker.Length;
        var end = start;
        while (end < reason.Length && char.IsDigit(reason[end])) end++;
        return int.TryParse(reason.AsSpan(start, end - start), out var n) ? n : null;
    }

    private static bool HasBudgetExhausted(IReadOnlyList<DecisionRecord> trace) =>
        trace.Any(t => t.Reason is not null
            && t.Reason.Contains("scroll stability budget exhausted", StringComparison.Ordinal));

    // ════════════════════════════════════════════════════════════════════════
    // Item 1 — FRESHNESS: stale/cached frames (same seq) must NOT confirm
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Freshness_StaleFrameSameSeq_NeverConfirms_BudgetExhaustedFailClosed()
    {
        // After scroll the environment returns the SAME clean frame with the
        // SAME sequence number repeatedly. The content is identical and would
        // normally confirm stability at attempt 1 — but the frame is NOT fresh
        // (seq ≤ prev). Principle 1 MUST reject it: no confirmation, budget
        // exhausted, RunFailed.
        var world = new StaleFrameWorld(WithTarget(CleanS.Rows));
        var (agent, envelope) = Build(world);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "qa-fresh", CancellationToken.None);

        // No confirmation ever (stale frames are not fresh).
        Assert.False(ConfirmedAttempt(agent.Trace).HasValue,
            "Freshness: a stale/cached frame (same seq) was confirmed as stable — Principle 1 requires strictly newer observations. Expected no confirmation.");
        // Budget exhausted (fail closed).
        Assert.True(HasBudgetExhausted(agent.Trace),
            "Freshness: expected 'scroll stability budget exhausted' trace when stale frames never confirm.");
        // Trace carries the factual stale-observation reason.
        Assert.Contains(agent.Trace, t => t.Reason is not null
            && t.Reason.Contains("stale observation", StringComparison.Ordinal)
            && t.Reason.Contains("not fresh", StringComparison.Ordinal));
        // Run failed closed; no Tap (nothing admitted).
        Assert.Equal(RunState.Failed, state);
        Assert.DoesNotContain(world.ActionHistory, a => a is DeviceAction.Tap);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Item 2 — FACTUAL CLASSIFICATION: left container (not reorder)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Classification_LeftContainer_RunFailedReasonContainsLeftContainerNotReorder()
    {
        // A confirmation frame resolves to a different page (Child:Target) — the
        // gate's existing page/foreground sanity fails closed. The RunFailed
        // reason MUST classify this factually as "left container", NOT as
        // "reorder/signature mismatch" (the previous masquerade).
        var world = new QuiescenceWorld([WithTarget(), LeftContainer]);
        var (agent, envelope) = Build(world);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "qa-left", CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        var reason = agent.Reason;
        Assert.NotNull(reason);
        Assert.Contains("left container", reason, StringComparison.Ordinal);
        Assert.DoesNotContain(reason, "reorder", StringComparison.Ordinal);
        Assert.DoesNotContain(world.ActionHistory, a => a is DeviceAction.Tap);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Item 2 — FACTUAL CLASSIFICATION: re-observe failed (not reorder)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Classification_ReobserveFailed_RunFailedReasonContainsReobserveFailedNotReorder()
    {
        // The first re-observe inside ConfirmScrollStabilityAsync throws once.
        // The gate MUST classify this factually as "re-observe failed", NOT as
        // "reorder/signature mismatch" (the previous masquerade). Fail closed.
        var world = new ThrowOnceWorld(WithTarget(CleanS.Rows));
        var (agent, envelope) = Build(world);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "qa-reobs", CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        var reason = agent.Reason;
        Assert.NotNull(reason);
        Assert.Contains("re-observe failed", reason, StringComparison.Ordinal);
        Assert.DoesNotContain(reason, "reorder", StringComparison.Ordinal);
        Assert.DoesNotContain(world.ActionHistory, a => a is DeviceAction.Tap);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Item 4 — SURFACE B real projection: RuntimeEventProjector.Project
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SurfaceB_PersistentAmbiguity_ExactlyOneRunFailed_WithDetail_NoRunCompleted_Idempotent()
    {
        // Persistent duplicate-signature frames exhaust the budget. Project the
        // terminal facts through the REAL RuntimeEventProjector (Surface B):
        // exactly ONE RuntimeEventKind.RunFailed; its payload Reason contains
        // the exhaustion detail (last seq, attempts, classification, no
        // admission, no re-dispatch); NO RuntimeEventKind.RunCompleted;
        // projecting TWICE yields identical idempotent results.
        //
        // This replaces the earlier Agent.Trace/Reason-only assertions with the
        // real projection path that UniAgent/Emulator consumes (design.md §3).
        var world = new QuiescenceWorld([WithTarget(new RowSpec("S", 0.5f), new RowSpec("S", 0.6f))]);
        var (agent, envelope) = Build(world);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "qa-surfaceb", CancellationToken.None);

        Assert.Equal(RunState.Failed, state);

        // Build the real Surface B projection: AgentStateSnapshot.From(agent)
        // + RuntimeEventProjector.Project(trace, snapshot).
        var snapshot = AgentStateSnapshot.From(agent);
        var trace = new TraceRun
        {
            RunId = snapshot.RunId,
            TraceRunId = "trace-" + snapshot.RunId,
            TraceId = "trace-" + snapshot.RunId,
        };

        var projection = RuntimeEventProjector.Project(trace, snapshot);

        // Exactly ONE RunFailed terminal projection.
        var runFailedEvents = projection.Events.Where(e => e.Kind == RuntimeEventKind.RunFailed).ToArray();
        Assert.Single(runFailedEvents);

        // Its payload Reason contains the exhaustion detail.
        var payload = Assert.IsType<RunFailedPayload>(runFailedEvents[0].Payload);
        Assert.Contains("quiescence admission budget exhausted", payload.Reason, StringComparison.Ordinal);
        Assert.Contains("last seq=", payload.Reason, StringComparison.Ordinal);
        Assert.Contains("attempts=", payload.Reason, StringComparison.Ordinal);
        Assert.Contains("classification=", payload.Reason, StringComparison.Ordinal);
        Assert.Contains("no unstable frame admitted", payload.Reason, StringComparison.Ordinal);
        Assert.Contains("no action re-dispatched", payload.Reason, StringComparison.Ordinal);
        // Persistent duplicate ⇒ duplicate ambiguity classification.
        Assert.Contains("duplicate ambiguity", payload.Reason, StringComparison.Ordinal);

        // NO RunCompleted.
        Assert.DoesNotContain(projection.Events, e => e.Kind == RuntimeEventKind.RunCompleted);

        // Idempotent: projecting TWICE yields identical results.
        var projection2 = RuntimeEventProjector.Project(trace, snapshot);
        Assert.Equal(projection.Events.Length, projection2.Events.Length);
        for (int i = 0; i < projection.Events.Length; i++)
        {
            Assert.Equal(projection.Events[i].Kind, projection2.Events[i].Kind);
            Assert.Equal(projection.Events[i].RunId, projection2.Events[i].RunId);
        }
        var payload2 = Assert.IsType<RunFailedPayload>(
            Assert.Single(projection2.Events.Where(e => e.Kind == RuntimeEventKind.RunFailed)).Payload);
        Assert.Equal(payload.Reason, payload2.Reason);

        // No action re-dispatched.
        Assert.DoesNotContain(world.ActionHistory, a => a is DeviceAction.Tap);
    }
}
