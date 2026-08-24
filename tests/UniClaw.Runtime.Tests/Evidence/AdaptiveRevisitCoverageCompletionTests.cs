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
/// ADAPTIVE REVISIT COVERAGE COMPLETION — deterministic proofs.
///
/// The bounded revisit must serve CONTAINER COVERAGE COMPLETION, not
/// single-branch recovery: every discovered pending branch must either be
/// dispatched (with a verified return) or be given a re-grounding opportunity
/// within the bounded budget. A pending branch that is NEVER freshly exposed
/// (never CURRENTLY_VISIBLE in any dispatch pass) is a COVERAGE GAP — the run
/// fails closed with the unresolved-branch evidence (discovered / resolved
/// counts + unresolved identities), never a premature "verified bounded
/// traversal completion" and never a blind dispatch. These tests drive the
/// real Agent over scenario-neutral scrollable worlds — no Settings logic, no
/// ADB, no list-size assumptions, no coordinate memory.
/// </summary>
public sealed class AdaptiveRevisitCoverageCompletionTests
{
    private const string App = "coverage.app";
    private const string Root = "Root";

    /// <summary>
    /// A scrollable world whose FORWARD scrolls honor the adaptive StepFraction
    /// (fast exploration) but whose REVERSE scrolls move exactly one row per
    /// swipe (a physical world where a reverse swipe recedes more slowly than a
    /// forward swipe). With the COVERAGE-DRIVEN budget
    /// (max(discovery observations − 1, discovered branch count)) a slow
    /// reverse is still sufficient to walk the whole list back; only a
    /// genuinely one-way list (reverse = no-op, <see cref="reverseDisabled"/>)
    /// makes top-of-list branches physically unreachable — the coverage-gap
    /// precondition.
    /// </summary>
    private sealed class CoverageWorld : IEnvironment
    {
        private readonly string[] _rows;
        private readonly int _windowSize;
        private readonly bool _reverseDisabled;
        private readonly List<DeviceAction> _actions = [];
        private int _position;
        private string _screen = "Launcher";
        private long _seq;

        public CoverageWorld(string[] rows, int windowSize, bool reverseDisabled = false)
        {
            _rows = rows;
            _windowSize = windowSize;
            _reverseDisabled = reverseDisabled;
        }

        public IReadOnlyList<DeviceAction> ActionHistory => _actions;
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
                        var fwdRows = Math.Max(1, (int)Math.Round(scroll.StepFraction * _windowSize));
                        if (_position + fwdRows < _rows.Length)
                            _position += fwdRows;
                        else
                            _position = Math.Max(0, _rows.Length - _windowSize);
                    }
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "ok"));
                case DeviceAction.ScrollBackward:
                    if (_screen == "Root" && !_reverseDisabled)
                        _position = Math.Max(0, _position - 1); // fixed slow reverse
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

    private static (RuntimeAgent Agent, CoverageWorld World, IntentSemanticEnvelope.Resolved Envelope) Build(
        string[] rows, int windowSize, Func<ObservedElement, bool>? authorize = null, bool reverseDisabled = false)
    {
        var world = new CoverageWorld(rows, windowSize, reverseDisabled);
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
                new GoalEvidence(false, "coverage proof", observation.SequenceNumber),
            CandidateAuthorizationEvaluator: (_, candidate) =>
                new CandidateAuthorizationEvidence(
                    authorize?.Invoke(candidate) ?? (candidate.Text is not null
                        && (candidate.Text.StartsWith("Node ", StringComparison.Ordinal)
                            // The labelled parent-return control (Root button on a
                            // child page) must be authorized so the verified child
                            // return can resolve it — it never becomes a branch.
                            || string.Equals(candidate.Text, Root, StringComparison.Ordinal))),
                    "coverage authz"),
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
            "coverage proof", goal, new IntentExecutionRepresentation.OpenWorldTypeLevel(spec));
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
    /// observations becomes a branch, grounded at its FIRST appearance. This is
    /// what makes the pending set include branches that are NOT currently
    /// visible — the recovery precondition.
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
                branches.TryAdd(text, observation.SequenceNumber);
                grounding.TryAdd(text,
                    new NavigationSourceOccurrenceReference(occurrence.ObservationSequence, occurrence.OccurrenceIdentity));
            }
        }
        return new BranchInventoryEvidence(branches.ToImmutable(), reason, grounding.ToImmutable());
    }

    private static int RootRowTapIndex(DeviceAction action)
        => action is DeviceAction.Tap { TargetBounds.IsValid: true } tap
            ? (int)Math.Floor(tap.TargetBounds.CenterY / 0.1f)
            : -1;

    /// <summary>
    /// Bottom -> adaptive reverse -> TOP recovered -> every discovered branch
    /// dispatched: the revisit served full container coverage. The run fails
    /// only at the root terminal (GoalEvidence unsatisfied by the proof goal) —
    /// never a coverage gap, never a blind zero-dispatch.
    /// </summary>
    [Fact]
    public async Task SixBranches_BottomToTop_CoverageCompletes_AllDispatch()
    {
        // 6 children, viewport 2: exploration exhausts at the bottom with a
        // budget of 5; the fixed 1-row reverse walks back 4 rows to the top
        // within budget — every branch is recovered and dispatched.
        var rows = Enumerable.Range(1, 6).Select(i => $"Node {i:00}").ToArray();
        var (agent, world, envelope) = Build(rows, windowSize: 2);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "cvc-1", CancellationToken.None);

        var reverses = world.ActionHistory.OfType<DeviceAction.ScrollBackward>().ToArray();
        var actionStr = string.Join(",", world.ActionHistory.Select(a => $"{a.GetType().Name}{(a is DeviceAction.ScrollForward sf ? $"({sf.StepFraction:0.00})" : a is DeviceAction.ScrollBackward sb ? $"({sb.StepFraction:0.00})" : "")}"));
        // Forward exploration reached the bottom; adaptive reverse recovery
        // walked back to the top (reverses present).
        Assert.True(world.ActionHistory.Count(a => a is DeviceAction.ScrollForward) >= 1,
            $"no forward exploration; acts={actionStr}");
        Assert.True(reverses.Length >= 1, $"reverse recovery never engaged; acts={actionStr}");
        // ALL 6 branches dispatched from fresh grounding (6 root-row taps; the
        // child-page return taps target the Root button near the bottom).
        var rootTaps = world.ActionHistory.Count(a => RootRowTapIndex(a) >= 0 && RootRowTapIndex(a) < 2);
        Assert.Equal(6, rootTaps);
        // No coverage gap: the run never failed closed with "coverage INCOMPLETE"
        // or the blind "zero dispatch" while branches were still pending.
        Assert.DoesNotContain(agent.Trace, t => t.Reason?.Contains("coverage INCOMPLETE", StringComparison.Ordinal) is true);
        Assert.DoesNotContain(agent.Trace, t => t.Reason?.Contains("zero dispatch", StringComparison.Ordinal) is true);
        // The proof goal's EvidenceEvaluator is always false: only the root
        // terminal (verified bounded traversal completion) may end the run.
        Assert.Equal(RunState.Failed, state);
        Assert.Contains("Verified bounded traversal completion", agent.Reason ?? "");
    }

    /// <summary>
    /// COVERAGE-DRIVEN BUDGET (Option A) proof: the slow 1-row reverse needs 9
    /// steps to walk from the bottom (position 9) back to the top, but the old
    /// budget (discovery observations − 1 = 4) allowed only 4 — a coverage gap.
    /// The coverage-driven budget (max(observations − 1, discovered branches) =
    /// max(4, 10) = 10) suffices: EVERY branch is recovered bottom→top and
    /// dispatched from fresh grounding; the run fails only at the root terminal
    /// (proof GoalEvidence false) — never a coverage gap, never a blind
    /// zero-dispatch.
    /// </summary>
    [Fact]
    public async Task TenBranches_AsymmetricReverse_CoverageDrivenBudget_CompletesAll()
    {
        var rows = Enumerable.Range(1, 10).Select(i => $"Node {i:00}").ToArray();
        var (agent, world, envelope) = Build(rows, windowSize: 4);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "cvc-2", CancellationToken.None);

        var actionStr = string.Join(",", world.ActionHistory.Select(a => $"{a.GetType().Name}{(a is DeviceAction.ScrollBackward sb ? $"({sb.StepFraction:0.00})" : "")}"));
        // Reverse recovery genuinely engaged and walked the whole list back.
        Assert.True(world.ActionHistory.OfType<DeviceAction.ScrollBackward>().Any(),
            $"reverse recovery never engaged; acts={actionStr}");
        // ALL 10 branches dispatched from fresh grounding (10 root-row taps).
        var rootTaps = world.ActionHistory.Count(a => RootRowTapIndex(a) >= 0 && RootRowTapIndex(a) < 4);
        Assert.Equal(10, rootTaps);
        // No coverage gap / no blind zero-dispatch: the run failed ONLY at the
        // root terminal (the proof goal's EvidenceEvaluator is always false).
        Assert.DoesNotContain(agent.Trace, t => t.Reason?.Contains("coverage INCOMPLETE", StringComparison.Ordinal) is true);
        Assert.DoesNotContain(agent.Trace, t => t.Reason?.Contains("zero dispatch", StringComparison.Ordinal) is true);
        Assert.Equal(RunState.Failed, state);
        Assert.Contains("Verified bounded traversal completion", agent.Reason ?? "");
    }

    /// <summary>
    /// Branches that are permanently unavailable because the WORLD cannot
    /// re-expose them (a one-way list — reverse swipes are a physical no-op):
    /// every reachable branch dispatches, the reverse is BOUNDARY-CONFIRMED
    /// (no new viewport occurrences at the floor step), and the never-exposed
    /// branches remain an EVIDENCE FAILURE — fail closed with the unresolved-
    /// branch coverage evidence instead of a premature "verified bounded
    /// traversal completion", and never an infinite loop.
    /// </summary>
    [Fact]
    public async Task OneWayWorld_UnreachableBranches_CoverageGap_FailsClosedWithEvidence()
    {
        // 8 children, viewport 4, ONE-WAY world (reverse = no-op): exploration
        // ends at the bottom viewport which exposes 07-08 (dispatched);
        // 01-06 are physically unreachable by any reverse swipe. The run must
        // fail closed with their evidence — after a boundary-confirmed reverse.
        var rows = Enumerable.Range(1, 8).Select(i => $"Node {i:00}").ToArray();
        var (agent, world, envelope) = Build(rows, windowSize: 4, reverseDisabled: true);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "cvc-5", CancellationToken.None);

        // Every reachable branch (07-08) dispatched from fresh grounding.
        var rootTaps = world.ActionHistory.Count(a => RootRowTapIndex(a) >= 0 && RootRowTapIndex(a) < 4);
        Assert.True(rootTaps >= 2, $"expected >= 2 dispatched; taps={rootTaps} acts={string.Join(",", world.ActionHistory.Select(a => a.GetType().Name))}");
        // BOUNDARY-PROVEN reverse termination fired (Option B): the no-op
        // reverse produced no new viewport occurrences at the floor step.
        Assert.Contains(agent.Trace, t => t.Reason?.Contains("bounded revisit boundary CONFIRMED", StringComparison.Ordinal) is true);
        // COVERAGE GAP fail-closed WITH unresolved branch evidence: the
        // never-exposed branch identities are part of the failure reason.
        Assert.Equal(RunState.Failed, state);
        Assert.Contains("coverage INCOMPLETE", agent.Reason ?? "");
        Assert.Contains("unresolved=[", agent.Reason ?? "");
        Assert.Contains("Node 01", agent.Reason ?? "");
        Assert.Contains("Node 06", agent.Reason ?? "");
        // Not a premature "verified bounded traversal completion" (coverage is
        // NOT complete while branches were never given an opportunity).
        Assert.DoesNotContain("Verified bounded traversal completion", agent.Reason ?? "");
        // The coverage ledger evidences the gap: Node 01..Node 06 were never
        // freshly exposed.
        Assert.True(agent.RevisitCoverage.TryGetValue(Root, out var exposed)
            && Enumerable.Range(1, 6).All(i => !exposed.Contains($"Node {i:00}")),
            "Node 01..Node 06 must never be freshly exposed (coverage gap evidence).");
    }

    [Fact]
    public async Task VisionOnly_NoAdbDependency()
    {
        var rows = Enumerable.Range(1, 8).Select(i => $"Node {i:00}").ToArray();
        var (agent, world, envelope) = Build(rows, windowSize: 2);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "cvc-3", CancellationToken.None);

        Assert.DoesNotContain(world.ActionHistory, a => a.ToString()!.Contains("adb", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NoSettingsVocabulary_ArchitectureGuard()
    {
        var rows = Enumerable.Range(1, 8).Select(i => $"Node {i:00}").ToArray();
        var (agent, world, envelope) = Build(rows, windowSize: 2);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "cvc-4", CancellationToken.None);

        Assert.DoesNotContain("Settings", world.ActionHistory.ToString()!, StringComparison.Ordinal);
        Assert.DoesNotContain("WiFi", world.ActionHistory.ToString()!, StringComparison.Ordinal);
        Assert.DoesNotContain("Android", world.ActionHistory.ToString()!, StringComparison.Ordinal);
    }
}
