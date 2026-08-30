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
/// QUIESCENCE ADMISSION — RED-FIRST proofs for the EXISTING post-scroll stability
/// gate (Agent.OpenWorld.cs: ConfirmScrollStabilityAsync / IsViewportStable /
/// NavigationRowCenters). These tests are the deterministic RED basis the
/// `runtime-evidence-based-quiescence-admission` change requires.
///
/// The gate's comparison evidence (NavigationRowCenters) builds a
/// Dictionary&lt;string,float&gt; with TryAdd over OccurrencesOf — so two rows in the
/// SAME frame that share a structured signature (Text|PerceptionType) COLLAPSE to
/// ONE map entry, and IsViewportStable compares the resulting UNORDERED distinct-
/// signature maps. Consequently today the gate:
///   - confirms an in-frame-ambiguous (duplicate-signature) frame as stable;
///   - masks a same-signature occurrence-count change (S×2 vs S×1) as stable;
///   - masks an occurrence-order change ([A,B] vs [B,A]) as stable.
///
/// Each RED test scripts a deterministic post-scroll frame sequence and asserts the
/// NEW quiescence-admission semantics (in-frame ambiguity non-confirmable;
/// multiplicity-preserving; order-preserving). The assertion is on the gate's PUBLIC
/// trace outcome (the "scroll stability CONFIRMED (seq=…, attempt N)" / "scroll
/// stability budget exhausted" / "scroll stability frame left the container" trace
/// reasons) — never on private state, never via reflection. Synthetic signatures
/// (S / A / B / Target / Fill 01) are used; no Settings/ADB text.
///
/// Expected state TODAY (before the repair): the 5 RED tests FAIL (that IS the
/// required state — their failure points at the TryAdd-collapse / unordered-map
/// mechanism); the 3 control tests (S3 / S4 / S8) PASS, preserving existing
/// semantics. The existing ScrollStabilityConfirmationTests stay untouched.
/// </summary>
public sealed class QuiescenceAdmissionRedTests
{
    private const string App = "qa.app";
    private const string Root = "Root";
    private const string Target = "Target";

    // ── Scripted frame building blocks ──────────────────────────────────────

    private sealed record RowSpec(string Text, float Y1, string PerceptionType = "row");
    private sealed record FrameSpec(params RowSpec[] Rows);

    private static RowSpec Row(string text, float y1) => new(text, y1);

    /// <summary>The two-row "Item×2" duplicate-signature pair (S at two positions,
    /// same signature "S|row|"). TryAdd collapses them to one map entry.</summary>
    private static FrameSpec DupS => new(Row("S", 0.5f), Row("S", 0.6f));

    /// <summary>The single "Item×1" clean frame (S once).</summary>
    private static FrameSpec CleanS => new(Row("S", 0.5f));

    /// <summary>A stable Target row, present in every post-scroll frame so the
    /// open-world viewport-exploration decision terminates (Target visible ->
    /// exhausted) after the gate's single confirmation pass.</summary>
    private static FrameSpec WithTarget(params RowSpec[] rows)
    {
        var all = new RowSpec[rows.Length + 1];
        all[0] = Row(Target, 0.4f);
        Array.Copy(rows, 0, all, 1, rows.Length);
        return new FrameSpec(all);
    }

    /// <summary>A frame that resolves to a DIFFERENT semantic page (Child:Target),
    /// exercising the gate's existing same-Container page/foreground sanity check.</summary>
    private static FrameSpec LeftContainer => new(Row("Child:Target", 0.4f));

    // ── Fake open-world environment ─────────────────────────────────────────

    /// <summary>
    /// Deterministic scripted world mirroring ScrollStabilityConfirmationTests'
    /// SettleWorld: Launcher -> Root, then a scripted post-scroll frame sequence
    /// driven through the REAL open-world scroll/stability loop. A non-interactive
    /// Root title gives the page a stable identity; "Fill 01" is a stable navigation
    /// anchor present pre- and post-scroll so ordered-overlap normalization succeeds
    /// for the clean controls. All navigation rows carry valid bounds so the
    /// evidence-quality settle consumes zero extra observations and the stability
    /// gate receives the first post-scroll frame directly.
    /// </summary>
    private sealed class QuiescenceWorld : IEnvironment
    {
        private readonly FrameSpec[] _postScroll;
        private readonly List<DeviceAction> _actions = [];
        private int _obsSinceScroll = int.MaxValue; // int.MaxValue = not post-scroll
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
            // Non-interactive Root title — stable page identity (fixed bounds).
            elements.Add(new ObservedElement(Root, null, 0, new ElementBounds(0, 0.95f, 1, 1f), "title"));
            // Stable navigation anchor present in every Root frame.
            elements.Add(new ObservedElement("Fill 01", null, 1, new ElementBounds(0, 0.8f, 1, 0.9f), "row"));
            if (_obsSinceScroll != int.MaxValue)
            {
                var idx = _obsSinceScroll;
                _obsSinceScroll++;
                // Repeat the last scripted frame for any attempt beyond the script
                // (handles persistent-duplicate / persistent-reorder budgets).
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
                        _obsSinceScroll = 0; // the page starts settling
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
            // Synthetic navigation sources: Target, "Fill …", and the scenario
            // tokens S / A / B. On a child page none of these is a navigation
            // candidate (the gate's page sanity fails first for S8 anyway).
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

    // ── Trace helpers (public observable; no private-state access) ──────────

    /// <summary>The 1-based attempt at which the gate emitted "scroll stability
    /// CONFIRMED (seq=…, attempt N)", or null if it never confirmed.</summary>
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

    private static bool HasLeftContainer(IReadOnlyList<DecisionRecord> trace) =>
        trace.Any(t => t.Reason is not null
            && t.Reason.Contains("scroll stability frame left the container", StringComparison.Ordinal));

    // ════════════════════════════════════════════════════════════════════════
    // RED tests — must FAIL on today's implementation (the required RED state)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task S1_DupArtifactsThenClean_ConfirmsOnlyOnCleanPair_Red()
    {
        // Scenario 1: [dup(S,S), dup(S,S), clean, clean]. The duplicate-signature
        // frames are pending (in-frame ambiguity); only the clean pair confirms,
        // and ONLY the last clean frame is admitted — so the CONFIRMED attempt
        // must be 3 (clean2 vs clean3), never 1 (a dup frame).
        var (agent, _, envelope) = Build(
            WithTarget(DupS.Rows),      // frame 0 — first post-scroll (dup)
            WithTarget(DupS.Rows),      // frame 1 — attempt 1 (dup)
            WithTarget(CleanS.Rows),    // frame 2 — attempt 2 (clean)
            WithTarget(CleanS.Rows));   // frame 3 — attempt 3 (clean)
        await IntentExecution.RunOpenWorldAsync(agent, envelope, "qa-s1", CancellationToken.None);

        var attempt = ConfirmedAttempt(agent.Trace);
        Assert.True(attempt == 3,
            $"S1 RED: gate confirmed at attempt {attempt} on a duplicate-signature frame — NavigationRowCenters' TryAdd collapsed the in-frame duplicate pair (S,S) into one map entry, so the dup frames compared 'stable' and masked the ambiguity. Expected confirmation only on the clean pair at attempt 3 (in-frame ambiguity must be non-confirmable).");
    }

    [Fact]
    public async Task S2_PersistentDuplicateArtifacts_FailsClosedBudgetExhausted_Red()
    {
        // Scenario 2: every post-scroll frame carries the duplicate pair until the
        // budget is exhausted. Persistent in-frame ambiguity is non-confirmable ->
        // budget exhausted, nothing admitted, NO confirmation.
        var (agent, world, envelope) = Build(WithTarget(DupS.Rows));
        await IntentExecution.RunOpenWorldAsync(agent, envelope, "qa-s2", CancellationToken.None);

        Assert.False(ConfirmedAttempt(agent.Trace).HasValue,
            "S2 RED: gate CONFIRMED a persistent-duplicate frame — TryAdd collapsed the in-frame duplicate every attempt so the collapsed distinct-signature maps compared 'stable' (attempt 1). Expected fail-closed budget exhaustion with NO confirmation (persistent ambiguity is non-confirmable).");
        Assert.True(HasBudgetExhausted(agent.Trace),
            "S2 RED: the budget was NOT exhausted on persistent duplicates — the gate confirmed at attempt 1 via TryAdd collapse. Expected a 'scroll stability budget exhausted' trace.");
        Assert.DoesNotContain(world.ActionHistory, a => a is DeviceAction.Tap);
    }

    [Fact]
    public async Task S5_MultiplicityChange_Item2ThenItem1_NotStableAcrossCountChange_Red()
    {
        // Scenario 5: frame 1 shows Item×2, frame 2 shows Item×1, frame 3 Item×1.
        // The S×2 -> S×1 pair is NOT stable (multiplicity must be preserved); only
        // the equal-count pair (frame 2 -> frame 3) confirms, so the CONFIRMED
        // attempt must be 2, never 1.
        var (agent, _, envelope) = Build(
            WithTarget(DupS.Rows),      // frame 0 — Item×2 (first post-scroll)
            WithTarget(CleanS.Rows),    // frame 1 — attempt 1 — Item×1
            WithTarget(CleanS.Rows),    // frame 2 — attempt 2 — Item×1
            WithTarget(CleanS.Rows));   // frame 3 — repeat (equal-count pair)
        await IntentExecution.RunOpenWorldAsync(agent, envelope, "qa-s5", CancellationToken.None);

        var attempt = ConfirmedAttempt(agent.Trace);
        Assert.True(attempt == 2,
            $"S5 RED: gate confirmed at attempt {attempt} across an Item×2 -> Item×1 multiplicity change — TryAdd collapsed both frames to a single 'S' map entry, so set-equality masked the occurrence-count change. Expected confirmation only on the equal-count pair at attempt 2 (multiplicity must be preserved).");
    }

    [Fact]
    public async Task S6_PersistentRealDuplicateRows_GateLevelNonConfirmable_Red()
    {
        // Scenario 6: two REAL rows produce the same frozen signature in every
        // frame. The gate is non-confirmable (GATE_LEVEL_NON_CONFIRMABILITY):
        // budget exhausted, the Observation never becomes a decision frame, no
        // dispatch — the duplicate identity is NOT resolved or relaxed here.
        var (agent, world, envelope) = Build(WithTarget(DupS.Rows));
        await IntentExecution.RunOpenWorldAsync(agent, envelope, "qa-s6", CancellationToken.None);

        Assert.False(ConfirmedAttempt(agent.Trace).HasValue,
            "S6 RED: gate CONFIRMED a frame with persistent genuinely-duplicate visible rows — TryAdd collapsed the two identical real rows to one map entry, masking the in-frame identity ambiguity. Expected gate-level non-confirmability (budget exhausted, nothing admitted, no identity relaxation).");
        Assert.True(HasBudgetExhausted(agent.Trace),
            "S6 RED: persistent real duplicates did NOT exhaust the budget — the gate confirmed via TryAdd collapse. Expected a 'scroll stability budget exhausted' trace.");
        Assert.DoesNotContain(world.ActionHistory, a => a is DeviceAction.Tap);
    }

    [Fact]
    public async Task S7_CandidateOrderChange_ABCtoBAC_NotStableAcrossReorder_Red()
    {
        // Scenario 7: frame 1 carries [A,B], frame 2 carries [B,A] — same signature
        // multiset, order swapped, centers consistent per signature. A reorder is
        // instability; the [A,B]->[B,A] pair must NOT confirm. Confirmation may only
        // happen once the order stabilizes (frame 2 -> frame 3, both [B,A]), so the
        // CONFIRMED attempt must be 2, never 1.
        var (agent, _, envelope) = Build(
            WithTarget(Row("A", 0.5f), Row("B", 0.6f)),  // frame 0 — [A,B]
            WithTarget(Row("B", 0.6f), Row("A", 0.5f)),  // frame 1 — attempt 1 — [B,A] (reorder)
            WithTarget(Row("B", 0.6f), Row("A", 0.5f)),  // frame 2 — attempt 2 — [B,A] (stable)
            WithTarget(Row("B", 0.6f), Row("A", 0.5f))); // frame 3 — repeat
        await IntentExecution.RunOpenWorldAsync(agent, envelope, "qa-s7", CancellationToken.None);

        var attempt = ConfirmedAttempt(agent.Trace);
        Assert.True(attempt == 2,
            $"S7 RED: gate confirmed at attempt {attempt} across an [A,B] -> [B,A] reorder — IsViewportStable compared UNORDERED Dictionary maps, so the occurrence-order change was masked as 'stable'. Expected confirmation only after the order stabilizes at attempt 2 (ordered correspondence is a stability condition).");
    }

    // ════════════════════════════════════════════════════════════════════════
    // Control tests — must PASS on today's implementation (existing semantics)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task S3_PositionMovesThenStops_ConfirmsAtFinalC_Control()
    {
        // Scenario 3 (control): row center A -> B -> C -> C. The mid-settle
        // positions are pending (center drift > epsilon); the final stable C pair
        // confirms at attempt 3. The moving row IS the Target (a single source),
        // so it is NOT wrapped in WithTarget (which would add a second same-
        // signature Target and collapse via TryAdd). Existing semantics — GREEN.
        var (agent, world, envelope) = Build(
            new FrameSpec(Row(Target, 0.2f)),  // frame 0 — A (first post-scroll)
            new FrameSpec(Row(Target, 0.3f)),  // frame 1 — attempt 1 — B (drift)
            new FrameSpec(Row(Target, 0.4f)),  // frame 2 — attempt 2 — C (drift)
            new FrameSpec(Row(Target, 0.4f))); // frame 3 — attempt 3 — C (stable)
        await IntentExecution.RunOpenWorldAsync(agent, envelope, "qa-s3", CancellationToken.None);

        Assert.Equal(3, ConfirmedAttempt(agent.Trace));
        // The stable (final C) frame's target position was dispatched; no
        // mid-settle position was ever used as the decision basis.
        Assert.Contains(world.ActionHistory,
            a => a is DeviceAction.Tap { TargetBounds.IsValid: true } t
                && Math.Abs(t.TargetBounds.Y1 - 0.4f) < 0.01f);
    }

    [Fact]
    public async Task S4_IdenticalCleanPair_ConfirmsAtMinimumAttempts_Control()
    {
        // Scenario 4 (control): two consecutive identical unambiguous frames
        // confirm at the minimum attempt count (attempt 1). Existing semantics —
        // must stay GREEN today.
        var (agent, _, envelope) = Build(
            WithTarget(),   // frame 0 — Target only (first post-scroll)
            WithTarget());  // frame 1 — attempt 1 — identical
        await IntentExecution.RunOpenWorldAsync(agent, envelope, "qa-s4", CancellationToken.None);

        Assert.Equal(1, ConfirmedAttempt(agent.Trace));
    }

    [Fact]
    public async Task S8_LeftContainerDuringConfirmation_FailsClosedNewPageNotAdmitted_Control()
    {
        // Scenario 8 (control): a confirmation frame resolves to a different page
        // -> fail-closed (the gate's existing page/foreground sanity); the new page
        // is never admitted as the scroll's stable result. Existing semantics —
        // must stay GREEN today.
        var (agent, world, envelope) = Build(
            WithTarget(),     // frame 0 — valid Root page (first post-scroll)
            LeftContainer);   // frame 1 — attempt 1 — resolves to Child:Target page
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "qa-s8", CancellationToken.None);

        Assert.True(HasLeftContainer(agent.Trace),
            "S8 control: expected a 'scroll stability frame left the container' fail-closed trace when a confirmation frame resolves to a different page.");
        Assert.False(ConfirmedAttempt(agent.Trace).HasValue,
            "S8 control: a frame that left the container must never be confirmed as stable.");
        Assert.Equal(RunState.Failed, state);
        Assert.DoesNotContain(world.ActionHistory, a => a is DeviceAction.Tap);
    }
}
