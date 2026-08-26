using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.World;

namespace UniClaw.Runtime.ValidationHarness.Fixtures;

/// <summary>
/// Harness-local semantic capability binding for the REAL Android reality
/// fixture (com.uniclaw.fixture, CAPSTONE scenario) — Tier B composition.
/// Mirrors the Capstone real-emulator test's vision-only semantic rules:
///
///   root page  = "Fixture Root" title + "Visited X/8" state line + multiple
///                "Child NN" rows (OCR);
///   child page = exactly ONE distinct "Child NN" title, no state line, the
///                "Fixture Root" Button is the parent-return control.
///
/// It never authors a strategy, never generates coordinates/paths/actions,
/// and never fabricates completion — it only interprets observations the
/// production pipeline (screenshot → UDS perception) truthfully produces.
/// </summary>
public sealed class RealityFixtureStrategyBinding : IStrategySemanticCapabilityBinding
{
    public const string Application = "com.uniclaw.fixture";
    public const string RootPage = "Fixture Root";

    public string CapabilityId => "fixture.semantic.reality-capstone";
    public int Version => 1;
    public ExplorationIntent Exploration => ExplorationIntent.ExhaustiveWithinScope;
    public bool SupportsUnqualifiedObjective => true;
    public bool SupportsCriterion(string criterionId) => false;
    public bool SupportsCompletion(StrategyCompletionKind completion)
        => completion == StrategyCompletionKind.ExhaustiveCoverageWithinScope;

    public Goal CreateGoal(StrategyDirective strategy) => new(
        // SCENARIO-ALIGNED COMPLETION (goal-evaluation alignment, 2026-08-26
        // Human decision): the fixture's own external completion state is the
        // "Visited 8/8 [CAPSTONE COMPLETE]" OCR line (SharedState.java —
        // incremented only when a child is genuinely entered AND successfully
        // returns). The EvidenceEvaluator must require that exact state line,
        // never a merely-resolvable page: the scenario claims to validate
        // full-scope coverage, so the GoalEvaluator must demand exactly that.
        // OCR whitespace may merge tokens, so the predicate matches the
        // CAPSTONE/COMPLETE tokens on the state line (mirrors the Capstone
        // real-emulator test's own matcher).
        EvidenceEvaluator: observation => new GoalEvidence(
            Satisfied: IsCapstoneComplete(observation),
            Reason: IsCapstoneComplete(observation)
                ? "Fixture external state shows Visited 8/8 CAPSTONE COMPLETE (full required coverage)."
                : "Fixture external state has NOT reached Visited 8/8 CAPSTONE COMPLETE yet.",
            SourceObservationSequence: observation.SequenceNumber),
        CandidateAuthorizationEvaluator: EvaluateAuthorization,
        // Viewport exploration (scroll-until-exhausted, the graduated contract):
        // keep scrolling while NEW navigation signatures appear; stop when a
        // frame shows nothing prior frames lacked. This is what surfaces the
        // off-screen Child rows of the capstone ScrollView root.
        ViewportExplorationEvaluator: EvaluateViewportExploration,
        BranchInventoryEvaluator: EvaluateInventory,
        DiscoveredBranchEffectCriterion: null,
        CategoryClassifier: element =>
            IsChildTitle(element.Text) || IsParentReturn(element.Text)
                ? TypeLevelElementCategory.NavigableContainer
                : null);

    public TypeLevelDispatchPolicy? CreateDispatchPolicy(StrategyDirective strategy) => new(
        ImmutableDictionary<TypeLevelElementCategory, TypeLevelHandling>.Empty
            .Add(TypeLevelElementCategory.NavigableContainer, TypeLevelHandling.EnterAndTraverse));

    /// <summary>Vision-only semantic page resolution (mirrors the Capstone test).</summary>
    public static string? ResolvePage(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var childTitles = observation.Elements
            .Where(e => e.Text is not null && e.Text.StartsWith("Child ", StringComparison.Ordinal))
            .Select(e => e.Text!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var hasVisited = observation.Elements.Any(e =>
            e.Text is not null && e.Text.Contains("Visited", StringComparison.Ordinal));
        var hasRootTitle = observation.Elements.Any(e =>
            string.Equals(e.Text, RootPage, StringComparison.Ordinal));
        if (hasVisited || childTitles.Length > 1 || (hasRootTitle && childTitles.Length == 0))
            return RootPage;
        if (childTitles.Length == 1)
            return childTitles[0];
        return hasRootTitle ? RootPage : null;
    }

    private static ViewportExplorationEvidence EvaluateViewportExploration(
        ImmutableArray<Observation> observations)
    {
        if (observations.IsDefaultOrEmpty)
            return new ViewportExplorationEvidence(true, "explore");
        var latest = observations[^1];
        var latestSigs = NavSignatures(latest);
        var prior = observations.Take(observations.Length - 1)
            .SelectMany(o => NavSignatures(o)).ToHashSet(StringComparer.Ordinal);
        var hasNew = latestSigs.Any(s => !prior.Contains(s));
        return new ViewportExplorationEvidence(
            hasNew,
            hasNew ? "new source appeared; scroll more" : "no new source; exhausted");
    }

    private static ImmutableArray<string> NavSignatures(Observation observation)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var occurrence in SourceEquivalenceNormalizer.OccurrencesOf(observation))
            builder.Add(occurrence.StructuredSignature);
        return builder.ToImmutable();
    }

    /// <summary>
    /// Fixture external completion matcher: the "Visited 8/8" state line must
    /// carry the CAPSTONE COMPLETE token (whitespace-merge tolerant, mirrors
    /// the Capstone real-emulator test). This is the SCENARIO truth source for
    /// full required coverage — 3/8 or any partial state never satisfies.
    /// </summary>
    public static bool IsCapstoneComplete(Observation observation)
        => observation.Elements.Any(e =>
            e.Text is not null
            && e.Text.Contains("Visited", StringComparison.Ordinal)
            && e.Text.Contains("CAPSTONE", StringComparison.Ordinal)
            && e.Text.Contains("COMPLETE", StringComparison.Ordinal));

    private static bool IsChildTitle(string? text)
        => text is not null && text.StartsWith("Child ", StringComparison.Ordinal);

    private static bool IsParentReturn(string? text)
        => string.Equals(text, RootPage, StringComparison.Ordinal);

    private static CandidateAuthorizationEvidence EvaluateAuthorization(
        Observation observation,
        ObservedElement candidate)
        => new(
            IsChildTitle(candidate.Text) || IsParentReturn(candidate.Text),
            IsChildTitle(candidate.Text) || IsParentReturn(candidate.Text)
                ? "Reality fixture candidate is inside the strategy boundary."
                : "Reality fixture candidate is outside the strategy boundary.");

    private static BranchInventoryEvidence EvaluateInventory(
        ImmutableArray<Observation> observations,
        int semanticDepth)
    {
        if (observations.IsDefaultOrEmpty)
            return new BranchInventoryEvidence(
                ImmutableDictionary<string, long>.Empty,
                "no observations yet",
                requiredBranchGrounding: null);

        var latest = observations[^1];
        var page = ResolvePage(latest);
        if (page is null)
            return new BranchInventoryEvidence(
                ImmutableDictionary<string, long>.Empty,
                "page unresolved on the latest observation",
                requiredBranchGrounding: null);

        if (string.Equals(page, RootPage, StringComparison.Ordinal))
        {
            // Root inventory — VIEWPORT UNION (scroll-aware): the capstone root
            // is a ScrollView with 8 rows (~4-5 visible per viewport), so a
            // single latest-frame inventory would under-authorize to the
            // visible subset and the run could never reach the scenario's 8/8.
            // The evaluator receives the accumulated viewport observations;
            // the required set is the UNION of Child rows across ROOT frames
            // (grounding still per-occurrence through the production
            // normalizer; a branch is claimable from any frame that showed it,
            // and the CURRENTLY_VISIBLE + grounding checks in the Agent decide
            // dispatchability — an off-screen row simply stays pending until a
            // scroll brings it back, which is exactly the viewport-exploration
            // contract this fixture was built to exercise).
            var rootFrames = observations
                .Where(o => string.Equals(ResolvePage(o), RootPage, StringComparison.Ordinal))
                .ToArray();
            var mapBuilder = ImmutableDictionary.CreateBuilder<string, long>(StringComparer.Ordinal);
            var groundingBuilder = ImmutableDictionary.CreateBuilder<string, NavigationSourceOccurrenceReference>(StringComparer.Ordinal);
            foreach (var frame in rootFrames)
            {
                foreach (var occurrence in SourceEquivalenceNormalizer.OccurrencesOf(frame))
                {
                    var index = occurrence.CanonicalOccurrence.Reference.ElementIndex;
                    if (index >= frame.Elements.Length)
                        continue;
                    var text = frame.Elements[index].Text;
                    if (!IsChildTitle(text))
                        continue;
                    var identity = text!;
                    mapBuilder[identity] = frame.SequenceNumber;
                    groundingBuilder[identity] = new NavigationSourceOccurrenceReference(
                        frame.SequenceNumber, occurrence.OccurrenceIdentity);
                }
            }
            var map = mapBuilder.ToImmutable();
            if (map.Count == 0)
                return new BranchInventoryEvidence(
                    ImmutableDictionary<string, long>.Empty,
                    "reality root has no child rows",
                    requiredBranchGrounding: null);
            return new BranchInventoryEvidence(
                map,
                $"reality root inventory (viewport union over {rootFrames.Length} root frames): {map.Count} child rows, occurrence-grounded",
                groundingBuilder.ToImmutable());
        }

        // Child page: record-only leaf — the parent return is handled by the
        // Runtime's VERIFIED PARENT RETURN mechanism, never by re-dispatching
        // the root identity as a branch (that would trip the ancestry-cycle
        // safety guard, correctly). No required branch expansion on children.
        return new BranchInventoryEvidence(
            ImmutableDictionary<string, long>.Empty,
            "reality child page: record-only leaf; parent return via verified-return, not branch dispatch",
            requiredBranchGrounding: null);
    }
}
