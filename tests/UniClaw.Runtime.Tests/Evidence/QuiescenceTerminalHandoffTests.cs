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
/// QUIESCENCE TERMINAL HANDOFF (S9-S12) — Principle 8 terminal supervisory
/// reporting proofs for the repaired post-scroll stability gate. The Run's
/// terminal failure reason (existing RunFailed reason / trace surface — NO new
/// RuntimeEventKind/wire DTO) SHALL name quiescence-admission budget exhaustion
/// with the last Observation sequence, attempt count, and final failure
/// classification; exactly one RunFailed terminal; no RunCompleted; no action
/// re-dispatch; no provisional Observation entering inventory. UniAgent can read
/// but cannot intervene; the terminal fact is idempotent.
///
/// These tests drive the REAL open-world scroll/stability loop over a
/// deterministic scripted world (same infrastructure as
/// QuiescenceAdmissionRedTests). Assertions use the PUBLIC read surface
/// (agent.State / agent.Reason / agent.Trace / world.ActionHistory) — never
/// private state, never reflection.
/// </summary>
public sealed class QuiescenceTerminalHandoffTests
{
    private const string App = "qa.app";
    private const string Root = "Root";
    private const string Target = "Target";

    // ── Scripted frame building blocks (mirrors QuiescenceAdmissionRedTests) ─

    private sealed record RowSpec(string Text, float Y1, string PerceptionType = "row");
    private sealed record FrameSpec(params RowSpec[] Rows);

    private static RowSpec Row(string text, float y1) => new(text, y1);

    /// <summary>The two-row "Item×2" duplicate-signature pair — persistent
    /// in-frame ambiguity is gate-level non-confirmable (budget exhausted).</summary>
    private static FrameSpec DupS => new(Row("S", 0.5f), Row("S", 0.6f));

    private static FrameSpec WithTarget(params RowSpec[] rows)
    {
        var all = new RowSpec[rows.Length + 1];
        all[0] = Row(Target, 0.4f);
        Array.Copy(rows, 0, all, 1, rows.Length);
        return new FrameSpec(all);
    }

    // ── Fake open-world environment (identical contract to RedTests) ─────────

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

    private static (RuntimeAgent Agent, QuiescenceWorld World, IntentSemanticEnvelope.Resolved Envelope)
        Build(params FrameSpec[] postScroll)
    {
        var world = new QuiescenceWorld(postScroll);
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
        return (agent, world, envelope);
    }

    // ════════════════════════════════════════════════════════════════════════
    // S9 — Budget exhaustion produces a terminal report
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task S9_PersistentAmbiguityBudgetExhausted_TerminalReportWithDetail()
    {
        // Every post-scroll frame carries the duplicate-signature pair until the
        // budget is exhausted. The Run fails closed; the RunFailed reason names
        // quiescence-admission exhaustion with last seq, attempts, and
        // classification. Exactly ONE RunFailed terminal; no RunCompleted; no
        // redispatch; nothing entered inventory (no Tap).
        var (agent, world, envelope) = Build(WithTarget(DupS.Rows));
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "qa-s9", CancellationToken.None);

        // Run is Failed (fail closed).
        Assert.Equal(RunState.Failed, state);

        // Exactly ONE RunFailed terminal projection; no RunCompleted.
        Assert.Single(agent.Trace.Where(t => t.RunState == RunState.Failed));
        Assert.DoesNotContain(agent.Trace, t => t.RunState == RunState.Completed);

        // The terminal reason (read via the existing public Reason surface)
        // names quiescence-admission budget exhaustion with last seq, attempts,
        // and a failure classification. No unstable frame admitted / no
        // re-dispatch is recorded in the reason.
        var reason = agent.Reason;
        Assert.NotNull(reason);
        Assert.Contains("quiescence admission budget exhausted", reason, StringComparison.Ordinal);
        Assert.Contains("last seq=", reason, StringComparison.Ordinal);
        Assert.Contains("attempts=", reason, StringComparison.Ordinal);
        Assert.Contains("classification=", reason, StringComparison.Ordinal);
        Assert.Contains("no unstable frame admitted", reason, StringComparison.Ordinal);
        Assert.Contains("no action re-dispatched", reason, StringComparison.Ordinal);
        // Persistent duplicate ⇒ duplicate ambiguity classification.
        Assert.Contains("duplicate ambiguity", reason, StringComparison.Ordinal);

        // Trace carries per-attempt entries (each with seq, occurrences, dup,
        // drift, reason). At least one "scroll stability pending" entry exists.
        Assert.Contains(agent.Trace, t => t.Reason is not null
            && t.Reason.Contains("scroll stability pending", StringComparison.Ordinal)
            && t.Reason.Contains("occurrences=", StringComparison.Ordinal)
            && t.Reason.Contains("dup=", StringComparison.Ordinal)
            && t.Reason.Contains("drift=", StringComparison.Ordinal)
            && t.Reason.Contains("reason=", StringComparison.Ordinal));
        // Budget exhausted trace entry.
        Assert.Contains(agent.Trace, t => t.Reason is not null
            && t.Reason.Contains("scroll stability budget exhausted", StringComparison.Ordinal));

        // No action re-dispatched (no Tap — nothing entered inventory).
        Assert.DoesNotContain(world.ActionHistory, a => a is DeviceAction.Tap);
    }

    // ════════════════════════════════════════════════════════════════════════
    // S10 — UniAgent can read but cannot intervene
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task S10_TerminalReasonReadableViaExistingSurface_DistinguishableFromSuccess()
    {
        // The RunFailed payload is readable via the existing read surface
        // (agent.Reason + trace terminal event). The quiescence-exhaustion
        // reason is distinguishable from a success reason; no continuation
        // occurs (state stays Failed).
        var (agent, world, envelope) = Build(WithTarget(DupS.Rows));
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "qa-s10", CancellationToken.None);

        Assert.Equal(RunState.Failed, state);

        // Read the terminal reason via the existing public surface.
        var reason = agent.Reason;
        Assert.NotNull(reason);
        // Distinguishable from success: a success would carry GoalEvidence.Reason
        // ("quiescence proof"), never "quiescence admission budget exhausted".
        Assert.DoesNotContain(reason, "quiescence proof", StringComparison.Ordinal);
        Assert.Contains("quiescence admission budget exhausted", reason, StringComparison.Ordinal);

        // The terminal trace event carries RunState.Failed with this reason.
        var failedEvent = Assert.Single(agent.Trace.Where(t => t.RunState == RunState.Failed));
        Assert.Contains("quiescence admission budget exhausted", failedEvent.Reason, StringComparison.Ordinal);

        // No continuation: state is terminal Failed, not Completed/Running.
        Assert.NotEqual(RunState.Completed, agent.State);
        Assert.NotEqual(RunState.Running, agent.State);
        // No action re-dispatched.
        Assert.DoesNotContain(world.ActionHistory, a => a is DeviceAction.Tap);
    }

    // ════════════════════════════════════════════════════════════════════════
    // S11 — Normal stability produces no fallback report (control)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task S11_NormalStability_NoQuiescenceFallbackLoopContinues()
    {
        // Consecutive unambiguous stable frames confirm within budget: the
        // latest stable frame is admitted, NO quiescence-admission fallback
        // report is produced, and the existing Runtime loop continues past the
        // gate — the admitted frame becomes the decision basis and the Target is
        // dispatched (a Tap at the stable position). (A downstream post-action
        // transition settle is a separate concern outside the quiescence gate's
        // scope; this control asserts the GATE produced no fallback, not the
        // whole-run terminal state — mirroring the existing
        // ScrollStabilityConfirmationTests.StabilityConfirmed control.)
        var (agent, world, envelope) = Build(
            WithTarget(),   // frame 0 — Target only (first post-scroll)
            WithTarget());  // frame 1 — attempt 1 — identical (stable)
        await IntentExecution.RunOpenWorldAsync(agent, envelope, "qa-s11", CancellationToken.None);

        // No quiescence-admission fallback report from the GATE.
        Assert.DoesNotContain(agent.Trace, t => t.Reason is not null
            && t.Reason.Contains("quiescence admission budget exhausted", StringComparison.Ordinal));
        Assert.DoesNotContain(agent.Trace, t => t.Reason is not null
            && t.Reason.Contains("scroll stability budget exhausted", StringComparison.Ordinal));
        // Stability confirmed within budget.
        Assert.Contains(agent.Trace, t => t.Reason is not null
            && t.Reason.Contains("scroll stability CONFIRMED", StringComparison.Ordinal));
        // The loop continued past the gate: the Target was dispatched (a Tap at
        // the stable position 0.4f) — the admitted frame became the decision basis.
        Assert.Contains(world.ActionHistory,
            a => a is DeviceAction.Tap { TargetBounds.IsValid: true } t
                && Math.Abs(t.TargetBounds.Y1 - 0.4f) < 0.01f);
    }

    // ════════════════════════════════════════════════════════════════════════
    // S12 — Terminal fact idempotent (read twice yields same terminal state)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task S12_TerminalFactIdempotent_ReadingTwiceYieldsSameState()
    {
        // After budget exhaustion the RunFailed fact stands unchanged. Reading
        // the terminal state/reason twice via the existing surface yields the
        // same idempotent result — projection unavailability never implies
        // permission to continue.
        var (agent, world, envelope) = Build(WithTarget(DupS.Rows));
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "qa-s12", CancellationToken.None);

        Assert.Equal(RunState.Failed, state);

        // First read.
        var reason1 = agent.Reason;
        var state1 = agent.State;
        var failedEvents1 = agent.Trace.Where(t => t.RunState == RunState.Failed).ToList();

        // Second read — identical idempotent terminal result.
        var reason2 = agent.Reason;
        var state2 = agent.State;
        var failedEvents2 = agent.Trace.Where(t => t.RunState == RunState.Failed).ToList();

        Assert.Equal(state1, state2);
        Assert.Equal(reason1, reason2);
        Assert.Equal(failedEvents1.Count, failedEvents2.Count);
        Assert.Single(failedEvents1);
        // The reason is stable across reads (no mutation, no re-execution).
        Assert.NotNull(reason2);
        Assert.Contains("quiescence admission budget exhausted", reason2, StringComparison.Ordinal);
        // No action re-executed between reads (action history unchanged).
        Assert.DoesNotContain(world.ActionHistory, a => a is DeviceAction.Tap);
    }
}
