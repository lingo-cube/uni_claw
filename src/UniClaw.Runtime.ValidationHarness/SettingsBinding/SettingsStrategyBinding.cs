using System.Collections.Immutable;
using System.Globalization;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.World;

namespace UniClaw.Runtime.ValidationHarness.SettingsBinding;

/// <summary>
/// Harness-local semantic capability binding that adapts the production
/// <c>UniClaw.Semantic.Settings.SettingsSemanticCapability</c> output to the
/// graduated <c>IStrategySemanticCapabilityBinding</c> surface — spec
/// "SettingsStrategyBinding adapts without inventing" + design D6.
///
/// This binding is a PURE ADAPTER: every evaluator consumes ONLY the admitted
/// primary semantic evidence the production capability emitted for a real
/// observation (Observation.AdmittedSemanticEvidence). It invents no new
/// meaning, reads no knowledge/fixture content, and contains no page paths,
/// coordinates, selectors, or click sequences. Page identity resolution reuses
/// the graduated resolver semantics (resource anchors + the accessibility
/// action label) plus, failing closed, a structural child-title fallback that
/// reads only geometry and clickable structure — never text semantics or
/// page-name literals.
///
/// Capability identity and version come from the production manifest
/// (SettingsSemanticCapability.Manifest: "uni-claw.settings.semantic", "1").
/// </summary>
public sealed class SettingsStrategyBinding : IStrategySemanticCapabilityBinding
{
    /// <summary>
    /// Settings root semantic identity ('Settings', the production capability's
    /// settings.container title value). Declared as the directive semanticRoot.
    /// </summary>
    public const string RootIdentity = "Settings";

    /// <summary>Settings application identity (scope identity, never a path).</summary>
    public const string ApplicationIdentity = "com.android.settings";

    /// <summary>
    /// Root marker resource anchor (the graduated root resolution precedent:
    /// search_action_bar present → Settings root).
    /// </summary>
    private const string SearchActionBarResourceLeaf = "search_action_bar";

    /// <summary>
    /// Sub-page title-role resource anchor (the graduated toolbar title node).
    /// </summary>
    private const string ToolbarTitleRoleResourceLeaf = "collapsing_toolbar";

    /// <summary>
    /// Accessibility action-label anchor of the toolbar back control (the
    /// graduated parent-return signal; never a destination identity).
    /// </summary>
    private const string ParentReturnAccessibilityLabel = "Navigate up";

    /// <summary>Sub-page identity prefix: "SettingsSubpage(&lt;title&gt;)".</summary>
    private const string SubpagePrefix = "SettingsSubpage(";

    /// <summary>
    /// Normalized column tolerance for the structural child-title fallback: a
    /// vision text block belongs to the page's leftmost text margin column when
    /// its left edge is within this tolerance of the page-wide minimum text left
    /// edge (training evidence: a 1080-wide frame where 0.02 covers the 0.003
    /// gap between the caption's margin and the title's margin).
    /// </summary>
    private const float TitleFallbackColumnTolerance = 0.02f;

    /// <inheritdoc />
    public string CapabilityId => "uni-claw.settings.semantic";

    /// <inheritdoc />
    /// <remarks>Hard-constant 1 = the production manifest version "1"
    /// (SettingsSemanticCapability.Manifest.Version).</remarks>
    public int Version => 1;

    /// <inheritdoc />
    public ExplorationIntent Exploration => ExplorationIntent.ExhaustiveWithinScope;

    /// <inheritdoc />
    public bool SupportsUnqualifiedObjective => true;

    /// <inheritdoc />
    public bool SupportsCriterion(string criterionId) => false;

    /// <inheritdoc />
    public bool SupportsCompletion(StrategyCompletionKind completion)
        => completion == StrategyCompletionKind.ExhaustiveCoverageWithinScope;

    /// <inheritdoc />
    public Goal CreateGoal(StrategyDirective strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        return new Goal(
            // Evidence: satisfied iff the fresh observation resolves to the
            // strategy's declared semantic root through the graduated page
            // identity resolution. Honest root-identity evidence — the Reason
            // describes exactly that.
            EvidenceEvaluator: observation =>
            {
                var resolved = ResolveSemanticPage(observation);
                var satisfied = resolved is not null
                    && string.Equals(resolved, strategy.Scope.SemanticRoot, StringComparison.Ordinal);
                return new GoalEvidence(
                    satisfied,
                    satisfied
                        ? $"Observation resolves to the strategy's declared semantic root '{strategy.Scope.SemanticRoot}'."
                        : $"Observation resolves to '{resolved ?? "null"}' which is not the strategy's declared semantic root '{strategy.Scope.SemanticRoot}'.",
                    observation.SequenceNumber);
            },
            // Authorization: a candidate element is authorized ONLY when the
            // production capability emitted PRIMARY admitted evidence for that
            // element's occurrence as a NavigationCandidate (settings.preference-
            // row) or as the parent-return relation class (settings.navigate-up /
            // settings.parent-container). Everything else is positively rejected
            // with an audit reason naming the admitted evidence class.
            CandidateAuthorizationEvaluator: EvaluateAuthorization,
            // Viewport exploration: graduated ExploreWhileNew semantics, with the
            // navigation signature based on ADMITTED EVIDENCE navigation
            // occurrences (the production normalizer derives them from admitted
            // primary evidence), unioned across frames.
            ViewportExplorationEvaluator: EvaluateViewportExploration,
            // Branch inventory: graduated Inventory semantics over admitted
            // navigation occurrences — first-seen per anchor across the current
            // container's frames, occurrence-grounded through the normalizer.
            BranchInventoryEvaluator: EvaluateInventory,
            DiscoveredBranchEffectCriterion: null,
            // Category: an admitted NavigationCandidate occurrence is a
            // navigable container; the reserved anchors (container title,
            // parent-return control) and anchorless elements stay null.
            CategoryClassifier: ClassifyCategory);
    }

    /// <inheritdoc />
    public TypeLevelDispatchPolicy? CreateDispatchPolicy(StrategyDirective strategy) => new(
        ImmutableDictionary<TypeLevelElementCategory, TypeLevelHandling>.Empty
            .Add(TypeLevelElementCategory.NavigableContainer, TypeLevelHandling.EnterAndTraverse));

    /// <summary>
    /// GRADUATED PAGE-IDENTITY RESOLUTION (harness-local reimplementation of the
    /// graduated Settings resolver):
    ///  - ROOT: Settings foreground + the search_action_bar resource anchor →
    ///    <see cref="RootIdentity"/> ("Settings");
    ///  - SUB-PAGE: Settings foreground + root marker absent + the labelled back
    ///    control ("Navigate up" accessibility label) + exactly ONE distinct
    ///    collapsing_toolbar title → "SettingsSubpage(&lt;title&gt;)";
    ///  - STRUCTURAL SUB-PAGE (WI-P26-R1): the same preconditions but NO
    ///    collapsing_toolbar title role → structural fallback: the unique
    ///    leftmost-margin title band above the topmost clickable content row
    ///    (see <see cref="ResolveStructuralChildTitle"/>) →
    ///    "SettingsSubpage(&lt;title&gt;)";
    ///  - otherwise null (fail closed).
    /// Identity values come ONLY from resource anchors, the accessibility
    /// action label, and — for the structural fallback — pure geometry with
    /// clickable structure. Never from hardcoded page-name literals.
    /// </summary>
    public static string? ResolveSemanticPage(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (!string.Equals(observation.ForegroundApplication, ApplicationIdentity, StringComparison.Ordinal))
            return null;

        var hasSearchBar = observation.StructuredElements.Any(se =>
            ResourceLeafEquals(se.ResourceId, SearchActionBarResourceLeaf));
        if (hasSearchBar)
            return RootIdentity;

        var hasBackControl = observation.StructuredElements.Any(se =>
            string.Equals(se.ContentDescription, ParentReturnAccessibilityLabel, StringComparison.Ordinal)
            || string.Equals(se.RawText, RootIdentity, StringComparison.Ordinal));
        if (!hasBackControl)
        {
            // VISION BACK INDICATOR (runK transition-settle): the structured
            // tier is MOMENTARILY EMPTY in immediate post-tap frames (XML
            // capture gap) and the root fallback flipped the settled identity
            // back to 'Settings' — never two consecutive non-parent frames.
            // A unique top-LEFT back ICON plus a top-band title TEXT (pure
            // vision) is the same child-page signal: continue to the structural
            // child-title resolution instead of the root fallback (fail-closed
            // to null if no title resolves). ROOT pages never carry a top-left
            // back arrow, so the root fallback remains safe.
            if (!HasVisionBackIndicator(observation))
                return RootIdentity;
        }

        var titleRoles = observation.StructuredElements
            .Where(se => ResourceLeafEquals(se.ResourceId, ToolbarTitleRoleResourceLeaf))
            .Select(se => se.ContentDescription)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (titleRoles.Length > 1)
            return null;
        if (titleRoles.Length == 1)
            return string.Concat(SubpagePrefix, titleRoles[0], ")");
        return ResolveStructuralChildTitle(observation);
    }

    /// <summary>
    /// VISION-ONLY child-page indicator (runK transition-settle fix): the
    /// structured tier is momentarily empty in immediate post-tap frames, so
    /// the back-control marker must be derivable from pure vision when the
    /// frame presents a child-page toolbar: a UNIQUE top-band (centerY ≤ 0.2)
    /// TEXT-LESS icon in the LEFT column (centerX ≤ 0.2) — the back arrow —
    /// together with a top-band non-empty TEXT block (the child title band).
    /// Root pages never show a top-left back arrow (their top icons, if any,
    /// are right-aligned avatars/menu), so the root fallback stays safe.
    /// Pure structure/position, no page-name literal, no identity semantics.
    /// </summary>
    private static bool HasVisionBackIndicator(Observation observation)
    {
        const double TopBand = 0.2;
        const double LeftColumn = 0.2;
        var hasLeftTopIcon = observation.Elements.Any(e =>
            e.Bounds is { IsValid: true } b
            && b.CenterY <= TopBand
            && b.CenterX <= LeftColumn
            && string.Equals(e.PerceptionType, "icon", StringComparison.OrdinalIgnoreCase));
        if (!hasLeftTopIcon)
            return false;
        var hasTopBandTitle = observation.Elements.Any(e =>
            e.Bounds is { IsValid: true } b
            && b.CenterY <= TopBand
            && !string.IsNullOrWhiteSpace(e.Text)
            && (string.IsNullOrWhiteSpace(e.PerceptionType)
                || string.Equals(e.PerceptionType, "text_block", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.PerceptionType, "menu_item", StringComparison.OrdinalIgnoreCase)));
        return hasTopBandTitle;
    }

    /// <summary>
    /// STRUCTURAL CHILD-TITLE FALLBACK (WI-P26-R1): a Settings sub-page with no
    /// collapsing_toolbar title role still presents its title as the content
    /// band in the page's leftmost text margin, above the topmost clickable
    /// content row (real re-entry frame evidence: 'Wallpaper &amp; style' child
    /// with only 'Navigate up' + clickable rows and left-margin OCR title).
    /// Pure structure only — no page-name literals, no text semantics, no fixed
    /// coordinates — and fully shared with the WI-P26-R2 page-identity
    /// exclusion (<see cref="ResolveStructuralTitleElement"/>); the text form
    /// simply wraps the element decision.
    /// </summary>
    private static string? ResolveStructuralChildTitle(Observation observation)
    {
        var titleElement = ResolveStructuralTitleElement(observation);
        if (titleElement is null)
            return null;
        return string.Concat(SubpagePrefix, titleElement.Text.Trim(), ")");
    }

    /// <summary>
    /// The unique structural title ELEMENT of a child page (WI-P26-R1 / R3): the
    /// TOPMOST leftmost-margin text BAND above the topmost clickable content
    /// row, decided exactly when the R1 fallback would resolve identity:
    ///   1. content rows = structured clickable elements with non-empty text;
    ///      a row's y-center comes from its structured bounds, or — when the
    ///      structured tier carries none — from the vision text block whose
    ///      text corresponds to the row's text;
    ///   2. topClickableY = the smallest such y-center;
    ///   3. leftmost column = the smallest left edge among all non-empty vision
    ///      text blocks; a block belongs to it when within
    ///      <see cref="TitleFallbackColumnTolerance"/>;
    ///   4. title candidates = non-empty leftmost-column blocks whose y-center
    ///      lies above topClickableY, ordered by Y1 (reading order);
    ///   5. BAND CLUSTERING (R3): candidates group into vertical bands; two
    ///      candidates are in the same band iff their gap (next.Y1 − prev.Y2)
    ///      is ≤ <see cref="TitleFallbackColumnTolerance"/>; a larger gap
    ///      starts a new band;
    ///   6. the TOPMOST band (smallest Y1) is the ONLY title decision-maker.
    ///      Lower bands are section headers / content labels and do NOT veto
    ///      the top header (real-emulator evidence: a child page can present a
    ///      title band plus a separate section-header band, both above the first
    ///      clickable row — the section header must not null the top title);
    ///   7. MERGE: within the top band, candidates with the same normalized
    ///      text (duplicate OCR / different perception types of the same
    ///      logical text) merge into one, keeping the topmost (smallest Y1)
    ///      occurrence;
    ///   8. TOPMOST-WINS + NESTING SUBORDINATION: the merged element with the
    ///      smallest Y1 is the presumptive title. Any other unique text whose
    ///      Y1 falls within [title.Y1, title.Y2] is a NESTED SUBORDINATE
    ///      (caption / sub-line) and does NOT conflict. Any other unique text
    ///      whose Y1 falls below title.Y2 (a peer-level competitor still in the
    ///      band via gap ≤ tolerance) is an UNRESOLVABLE conflict → null;
    ///   9. no candidates / empty top band / unresolvable top-band conflict →
    ///      null (fail closed).
    /// Pure structure only — no page-name literals, no text semantics, no fixed
    /// coordinates.
    /// </summary>
    private static ObservedElement? ResolveStructuralTitleElement(Observation observation)
    {
        var rowYCenters = ClickableRowYCenters(observation);
        if (rowYCenters.Length == 0)
            return null;
        var topClickableY = rowYCenters.Min();

        var visionTexts = observation.Elements
            .Where(e => e.Bounds is not null && e.Bounds.IsValid && !string.IsNullOrWhiteSpace(e.Text))
            .Select(e => (Element: e, Bounds: e.Bounds!))
            .ToArray();
        if (visionTexts.Length == 0)
            return null;

        var leftmostX1 = visionTexts.Min(v => v.Bounds.X1);

        var candidates = visionTexts
            .Where(v => Math.Abs(v.Bounds.X1 - leftmostX1) <= TitleFallbackColumnTolerance
                && v.Bounds.CenterY < topClickableY)
            .OrderBy(v => v.Bounds.Y1)
            .ToArray();
        if (candidates.Length == 0)
            return null;

        // ── BACK-BUTTON PROXIMITY GUARD (audit follow-up): the structural title
        // must be NEAR the Navigate-up control. When the page is scrolled, the
        // real title band scrolls off-screen and a section header (e.g.
        // "Brightness") becomes the topmost candidate — resolving it as the
        // page title produces a WRONG identity that breaks the scroll-stability
        // container check. A candidate far below the back control indicates the
        // title has scrolled off → return null (identity temporarily
        // unresolvable; the quiescence gate's scrolled-title tolerance handles it).
        var backControl = observation.StructuredElements
            .Where(se => string.Equals(se.ContentDescription, ParentReturnAccessibilityLabel, StringComparison.Ordinal)
                && se.Bounds is { IsValid: true })
            .Select(se => se.Bounds!)
            .FirstOrDefault();
        if (backControl is not null)
        {
            // The title must start within TitleMaxDistanceFromBackControl of the
            // back control's bottom edge. Real Settings titles sit immediately
            // below the toolbar (~0.02-0.08); section headers after scrolling are
            // much lower (~0.15+). This is a STRUCTURAL relation (toolbar →
            // title), not a fixed coordinate.
            const float TitleMaxDistanceFromBackControl = 0.15f;
            if (candidates[0].Bounds.Y1 - backControl.Y2 > TitleMaxDistanceFromBackControl)
                return null; // title scrolled off → identity unresolvable
        }

        // ── R3: TOPMOST-WINS + NESTING SUBORDINATION (Human Gate
        // SETTINGS_STRUCTURAL_CHILD_TITLE_BAND_REPAIR, 2026-08-29) ──
        // Cluster candidates into vertical BANDS (gap > TitleFallbackColumnTolerance
        // separates bands). The TOPMOST band is the ONLY title decision-maker;
        // lower bands are section headers / content labels and do NOT veto the
        // top header. Within the top band:
        //   - Same-text duplicates merge (keep the topmost occurrence).
        //   - The element with the smallest Y1 is the presumptive title.
        //   - Another element whose Y1 falls within [title.Y1, title.Y2] is a
        //     NESTED SUBORDINATE (caption/sub-line) — resolvable.
        //   - Another element whose Y1 falls BELOW title.Y2 (but still in the
        //     band via gap ≤ tolerance) is a PEER COMPETITOR — null.
        var bands = new List<List<(ObservedElement Element, ElementBounds Bounds)>>();
        var currentBand = new List<(ObservedElement, ElementBounds)> { candidates[0] };
        for (int i = 1; i < candidates.Length; i++)
        {
            var prevBottom = currentBand[^1].Item2.Y2;
            var gap = candidates[i].Bounds.Y1 - prevBottom;
            if (gap <= TitleFallbackColumnTolerance)
                currentBand.Add(candidates[i]);
            else
            {
                bands.Add(currentBand);
                currentBand = [(candidates[i].Item1, candidates[i].Bounds)];
            }
        }
        bands.Add(currentBand);

        var topBand = bands[0]; // already Y1-ordered from the candidates sort

        // Merge same-text duplicates within the top band (keep topmost bounds).
        var merged = new Dictionary<string, (ObservedElement Element, ElementBounds Bounds)>(StringComparer.Ordinal);
        foreach (var (element, bounds) in topBand)
        {
            var norm = NormalizeTitleText(element.Text);
            if (!merged.TryGetValue(norm, out var existing) || bounds.Y1 < existing.Bounds.Y1)
                merged[norm] = (element, bounds);
        }

        if (merged.Count == 0)
            return null; // top band empty (fail-closed)

        // TOPMOST-WINS: the merged element with the smallest Y1 is the
        // presumptive title. (Merge already kept the topmost occurrence per
        // text, so this picks the unique text that opens the band.)
        var (titleElement, titleBounds) = merged.Values.OrderBy(v => v.Bounds.Y1).First();

        // NESTING SUBORDINATION (runs over MERGED unique texts, AFTER the
        // same-text merge — per the R3 ruling "duplicates merge first"): any
        // OTHER unique text whose Y1 falls within [title.Y1, title.Y2] is a
        // nested subordinate (caption / sub-line) — resolvable. Any other
        // unique text whose Y1 falls below title.Y2 (a peer-level competitor
        // still in the band via gap ≤ tolerance) is an UNRESOLVABLE conflict.
        foreach (var (element, bounds) in merged.Values)
        {
            if (element.Index == titleElement.Index)
                continue;
            // title has the smallest Y1, so every other element has Y1 >= title.Y1.
            if (bounds.Y1 <= titleBounds.Y2)
                continue; // nested subordinate — resolvable
            return null; // peer competitor — unresolvable conflict
        }

        return titleElement;
    }

    private static string NormalizeTitleText(string text) =>
        string.Join(" ", text.Trim().ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// DIAGNOSTIC ONLY (WI-P26-R3, read-only — NOT a Runtime Contract input):
    /// describes whether the structural child-title fallback is active for
    /// <paramref name="observation"/> and, when active, what title it resolves.
    /// Intended for evidence/debugging reports; the resolution path itself is
    /// unchanged from <see cref="ResolveStructuralChildTitle"/>.
    /// </summary>
    public static string? DescribeStructuralTitleResolution(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (!IsStructuralFallbackActive(observation))
            return "inactive (root page / toolbar title / no back control)";
        var title = ResolveStructuralChildTitle(observation);
        return title is null ? "null (top band conflict or empty)" : title;
    }

    /// <summary>
    /// Whether the structural child-title fallback is the ACTIVE child-identity
    /// path (WI-P26-R1): Settings foreground, no root marker, labelled back
    /// control present, and ZERO collapsing_toolbar title roles. Only in this
    /// configuration does the leftmost title band decide the page identity;
    /// everywhere else (root page, toolbar-role-titled child, unresolvable
    /// frame) the band is NOT the page title — fail closed, no structural
    /// title exclusion there.
    /// </summary>
    private static bool IsStructuralFallbackActive(Observation observation)
    {
        if (!string.Equals(observation.ForegroundApplication, ApplicationIdentity, StringComparison.Ordinal))
            return false;
        if (observation.StructuredElements.Any(se => ResourceLeafEquals(se.ResourceId, SearchActionBarResourceLeaf)))
            return false;
        if (!observation.StructuredElements.Any(se =>
                string.Equals(se.ContentDescription, ParentReturnAccessibilityLabel, StringComparison.Ordinal)
                || string.Equals(se.RawText, RootIdentity, StringComparison.Ordinal)))
            return false;
        var titleRoles = observation.StructuredElements
            .Where(se => ResourceLeafEquals(se.ResourceId, ToolbarTitleRoleResourceLeaf))
            .Select(se => se.ContentDescription)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return titleRoles.Length == 0;
    }

    /// <summary>
    /// WI-P26-R2 page-identity predicate: is <paramref name="element"/> the
    /// STRUCTURAL PAGE TITLE of a Settings child page? The decision reuses the
    /// R1 structural rule (<see cref="ResolveStructuralTitleElement"/> — the
    /// leftmost text margin, unique band above the topmost clickable content
    /// row) AND only fires where the structural fallback actually decides the
    /// page identity (<see cref="IsStructuralFallbackActive"/>): root pages and
    /// toolbar-role-titled children never consult the band, and an undecidable
    /// title (ambiguous band) never excludes — fail closed keeps the candidate
    /// honestly in the inventory.
    /// </summary>
    private static bool IsStructuralPageTitle(Observation observation, ObservedElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!IsStructuralFallbackActive(observation))
            return false;
        var titleElement = ResolveStructuralTitleElement(observation);
        return titleElement is not null && titleElement.Index == element.Index;
    }

    /// <summary>
    /// Occurrence-level page-identity check: resolves the occurrence back to
    /// its primary-Vision element (the occurrence ↔ element correlation the
    /// production projector derives — occurrence id = source id +
    /// element array position) and applies
    /// <see cref="IsStructuralPageTitle"/>. Auxiliary occurrences are never
    /// authorization-eligible and are already filtered upstream; defensive
    /// bounds checks throughout.
    /// </summary>
    private static bool IsStructuralPageTitleOccurrence(Observation observation, NavigationSourceOccurrence occurrence)
    {
        var reference = occurrence.CanonicalOccurrence.Reference;
        if (reference.SourceKind != ObservationSourceKind.PrimaryVision)
            return false;
        if (reference.ElementIndex < 0 || reference.ElementIndex >= observation.Elements.Length)
            return false;
        return IsStructuralPageTitle(observation, observation.Elements[reference.ElementIndex]);
    }

    /// <summary>Inventory reason fragment for excluded structural page titles:
    /// each anchor is recorded honestly as page identity, not a destination.</summary>
    private static string FormatTitleExclusions(IReadOnlyCollection<string> anchors)
        => string.Join("; ", anchors.Select(anchor => $"title-excluded: {anchor} (page identity, not a destination)"));

    /// <summary>
    /// y-centers of the structured clickable content rows (clickable elements
    /// carrying text). A row's bounds come from the structured tier when
    /// present; otherwise from the corresponding vision text block (the row's
    /// text matched to a vision element's text — the real re-entry structured
    /// tier carries no bounds). Rows with neither are dropped; if no row is
    /// resolvable there is no content anchor to order the title against.
    /// </summary>
    private static ImmutableArray<float> ClickableRowYCenters(Observation observation)
    {
        var yCenters = ImmutableArray.CreateBuilder<float>();
        foreach (var row in observation.StructuredElements)
        {
            if (row.Clickable != true)
                continue;
            if (string.IsNullOrWhiteSpace(row.RawText))
                continue;
            if (row.Bounds is { IsValid: true } bounds)
            {
                yCenters.Add(bounds.CenterY);
                continue;
            }
            var correspondent = observation.Elements
                .FirstOrDefault(e => e.Bounds is { IsValid: true }
                    && string.Equals(e.Text, row.RawText, StringComparison.Ordinal));
            if (correspondent is null)
                continue;
            yCenters.Add(correspondent.Bounds!.CenterY);
        }
        return yCenters.ToImmutable();
    }

    private static CandidateAuthorizationEvidence EvaluateAuthorization(
        Observation observation,
        ObservedElement candidate)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(candidate);

        var position = FindElementPosition(observation, candidate);
        if (position < 0)
            return Reject("authorization rejected: the candidate element has no position in the current observation.");
        var occurrenceId = OccurrenceIdAt(observation, position);
        if (occurrenceId is null)
            return Reject("authorization rejected: no correlated primary vision source for the candidate occurrence.");

        // WI-P26-R2 page-identity guard: the structural child title IS the page
        // identity, never a navigation destination. The binding refuses to
        // authorize dispatch even when the frozen capability admitted the title
        // occurrence as NavigationCandidate (completeness still sees the
        // occurrence — never an Unknown — but this binding never sends the page
        // identity anywhere).
        if (IsStructuralPageTitle(observation, candidate))
            return Reject("page title is the page identity, not a navigation destination");

        foreach (var envelope in observation.AdmittedSemanticEvidence.EligibleForAuthorizationInput)
        {
            switch (envelope.Candidate)
            {
                case ElementAffordanceCandidateEvidence { AffordanceKind: ElementAffordanceKind.NavigationCandidate } affordance
                    when string.Equals(affordance.OccurrenceId, occurrenceId, StringComparison.Ordinal):
                    return new CandidateAuthorizationEvidence(
                        true,
                        "authorized: admitted primary evidence classifies this occurrence as NavigationCandidate (settings.preference-row).");
                case ContainerRelationCandidateEvidence { RelationKind: ContainerRelationKind.ReturnToParent } relation
                    when string.Equals(relation.RelatedOccurrenceId, occurrenceId, StringComparison.Ordinal):
                    return new CandidateAuthorizationEvidence(
                        true,
                        "authorized: admitted primary evidence classifies this occurrence as the parent-return control (settings.navigate-up / settings.parent-container).");
            }
        }

        var covering = CoveringEvidenceClass(observation, occurrenceId);
        if (covering is not null)
        {
            return Reject(
                $"authorization rejected: admitted evidence class {covering.Value.Class} (symbol '{covering.Value.SymbolId}') is not a navigation affordance.");
        }

        // The production capability's container identity is PAGE-SCOPED (the
        // envelope carries no element occurrence), so a non-row element on a
        // resolved page has no element-level evidence; the audit reason names
        // the frame-level ContainerIdentity class for the plain title case.
        var hasContainerIdentity = observation.AdmittedSemanticEvidence.EligibleForAuthorizationInput
            .Any(envelope => envelope.Candidate is ContainerIdentityCandidateEvidence);
        return hasContainerIdentity
            ? Reject("authorization rejected: no element-level admitted primary evidence; the frame's admitted evidence class is ContainerIdentity (container identity is page-scoped, never an element affordance).")
            : Reject("authorization rejected: no admitted primary evidence exists for this element occurrence.");
    }

    private static CandidateAuthorizationEvidence Reject(string reason) => new(false, reason);

    private static (string SymbolId, string Class)? CoveringEvidenceClass(Observation observation, string occurrenceId)
    {
        foreach (var envelope in observation.AdmittedSemanticEvidence.EligibleForAuthorizationInput)
        {
            string? candidateOccurrence = null;
            string? evidenceClass = null;
            switch (envelope.Candidate)
            {
                case ElementAffordanceCandidateEvidence affordance:
                    // Audit reason names the AFFORDANCE class (LocalControl /
                    // NonInteractive), the evidence class the evaluator rejects.
                    candidateOccurrence = affordance.OccurrenceId;
                    evidenceClass = affordance.AffordanceKind.ToString();
                    break;
                case ContainerRelationCandidateEvidence relation:
                    candidateOccurrence = relation.RelatedOccurrenceId;
                    evidenceClass = relation.EvidenceKind.ToString();
                    break;
                case ContainerIdentityCandidateEvidence identity:
                    candidateOccurrence = identity.OccurrenceId;
                    evidenceClass = identity.EvidenceKind.ToString();
                    break;
            }
            if (candidateOccurrence is not null
                && string.Equals(candidateOccurrence, occurrenceId, StringComparison.Ordinal))
            {
                return (envelope.Meaning.SymbolId, evidenceClass!);
            }
        }
        return null;
    }

    private static ViewportExplorationEvidence EvaluateViewportExploration(
        ImmutableArray<Observation> observations)
    {
        if (observations.IsDefaultOrEmpty)
            return new ViewportExplorationEvidence(true, "explore: no frames observed yet");
        var latest = observations[^1];
        var latestSignatures = NavigationSignatures(latest);
        var prior = observations.Take(observations.Length - 1)
            .SelectMany(o => NavigationSignatures(o))
            .ToHashSet(StringComparer.Ordinal);
        var hasNew = latestSignatures.Any(signature => !prior.Contains(signature));
        return new ViewportExplorationEvidence(
            hasNew,
            hasNew
                ? "new admitted navigation occurrence appeared in the latest frame; scroll more"
                : "no new admitted navigation occurrence; viewport exhausted");
    }

    /// <summary>
    /// Navigation signatures from ADMITTED EVIDENCE only: the production
    /// normalizer derives NavigationCandidate occurrences from admitted primary
    /// evidence (never raw text heuristics), so the viewport-union signature set
    /// is exactly the admitted navigation-occurrence set across frames.
    /// </summary>
    private static ImmutableArray<string> NavigationSignatures(Observation observation)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var occurrence in SourceEquivalenceNormalizer.OccurrencesOf(observation))
        {
            if (!occurrence.EligibleForAuthorization)
                continue;
            builder.Add(occurrence.StructuredSignature);
        }
        return builder.ToImmutable();
    }

    private static BranchInventoryEvidence EvaluateInventory(
        ImmutableArray<Observation> observations,
        int semanticDepth)
    {
        if (observations.IsDefaultOrEmpty)
            return new BranchInventoryEvidence(
                ImmutableDictionary<string, long>.Empty,
                "no accepted observations yet",
                ImmutableDictionary<string, NavigationSourceOccurrenceReference>.Empty);

        // Graduated Inventory semantics over the admitted-navigation-occurrence
        // set: first-seen wins per anchor across the current container's frames
        // (viewport union, mirroring the graduated Inventory evaluator). Child
        // pages inventory their own NavigationCandidate rows too; a page with
        // none is a bounded leaf (empty inventory with that reason). WI-P26-R2:
        // the STRUCTURAL PAGE TITLE — an occurrence the frozen capability
        // admitted as NavigationCandidate (it has no NonInteractive admission
        // path) but never a navigation destination (Gate #2) — never enters the
        // required map; the exclusion is recorded in the reason (page identity,
        // not a destination) and the occurrence itself stays admitted.
        var first = new Dictionary<string, NavigationSourceOccurrence>(StringComparer.Ordinal);
        var titleExcluded = new List<string>();
        foreach (var observation in observations)
        {
            foreach (var occurrence in SourceEquivalenceNormalizer.OccurrencesOf(observation))
            {
                // OccurrencesOf intentionally exposes both evidence tiers for
                // diagnostics.  A buyer that constructs DFS obligations must
                // consume primary-Vision-supported occurrences only; auxiliary
                // hierarchy rows are corroboration, never branch authority.
                if (!occurrence.EligibleForAuthorization)
                    continue;
                var anchor = TitleOf(occurrence.StructuredSignature);
                if (first.ContainsKey(anchor))
                    continue;
                if (IsStructuralPageTitleOccurrence(observation, occurrence))
                {
                    if (!titleExcluded.Contains(anchor))
                        titleExcluded.Add(anchor);
                    continue;
                }
                first[anchor] = occurrence;
            }
        }

        if (first.Count == 0)
        {
            var leafReason = titleExcluded.Count == 0
                ? "no admitted navigation-candidate occurrences on the current page (bounded leaf)"
                : $"no admitted navigation-candidate anchors remain after title exclusion (bounded leaf); {FormatTitleExclusions(titleExcluded)}";
            return new BranchInventoryEvidence(
                ImmutableDictionary<string, long>.Empty,
                leafReason,
                ImmutableDictionary<string, NavigationSourceOccurrenceReference>.Empty);
        }

        var required = ImmutableDictionary.CreateBuilder<string, long>(StringComparer.Ordinal);
        var grounding = ImmutableDictionary.CreateBuilder<string, NavigationSourceOccurrenceReference>(StringComparer.Ordinal);
        foreach (var (anchor, occurrence) in first)
        {
            required[anchor] = occurrence.ObservationSequence;
            grounding[anchor] = new NavigationSourceOccurrenceReference(
                occurrence.ObservationSequence, occurrence.OccurrenceIdentity);
        }
        var reason = $"inventory: {first.Count} admitted navigation-candidate anchors across the current container frames (viewport union, occurrence-grounded)";
        if (titleExcluded.Count > 0)
            reason += $"; {FormatTitleExclusions(titleExcluded)}";
        return new BranchInventoryEvidence(
            required.ToImmutable(),
            reason,
            grounding.ToImmutable());
    }

    private static TypeLevelElementCategory? ClassifyCategory(ObservedElement element)
    {
        // Fail closed: an element with no text anchor is unclassifiable. In the
        // dispatch path the classifier only ever sees pending-branch candidates
        // (admitted NavigationCandidate occurrences), which classify as
        // navigable containers; the reserved anchors — the Settings container
        // title (ContainerIdentity evidence) and the labelled parent-return
        // control (ParentReturnControl evidence) — are never navigation rows.
        if (string.IsNullOrWhiteSpace(element.Text))
            return null;
        if (string.Equals(element.Text, RootIdentity, StringComparison.Ordinal))
            return null;
        if (string.Equals(element.Text, ParentReturnAccessibilityLabel, StringComparison.Ordinal))
            return null;
        return TypeLevelElementCategory.NavigableContainer;
    }

    /// <summary>
    /// Anchor of an occurrence = the first segment of its normalizer signature
    /// (the graduated Inventory's TitleOf: element text for primary occurrences).
    /// </summary>
    private static string TitleOf(string signature)
    {
        var bar = signature.IndexOf('|');
        return bar < 0 ? signature : signature[..bar];
    }

    /// <summary>
    /// OCCURRENCE ↔ ELEMENT CORRELATION (projector-verified): the production
    /// projector builds primary-vision occurrence ids as
    /// CreateOccurrenceId(primarySourceId, elementArrayPosition), so a candidate
    /// element correlates to admitted evidence through its array position (the
    /// element's stable Index). Defensive bounds checks throughout.
    /// </summary>
    private static int FindElementPosition(Observation observation, ObservedElement candidate)
    {
        if (observation.Elements.IsDefaultOrEmpty)
            return -1;
        for (var i = 0; i < observation.Elements.Length; i++)
        {
            if (observation.Elements[i].Index == candidate.Index)
                return i;
        }
        return -1;
    }

    private static string? OccurrenceIdAt(Observation observation, int position)
    {
        var primary = observation.Sources.FirstOrDefault(source =>
            source.Tier == ObservationSourceTier.PrimaryVision
            && source.Available
            && source.ObservationSequence == observation.SequenceNumber);
        if (primary is null || position < 0 || position >= observation.Elements.Length)
            return null;
        return SemanticObservationFactProjector.CreateOccurrenceId(
            primary.SourceId, position.ToString(CultureInfo.InvariantCulture));
    }

    private static bool ResourceLeafEquals(string? resourceId, string expectedLeaf)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            return false;
        var cut = Math.Max(resourceId.LastIndexOf(':'), resourceId.LastIndexOf('/'));
        return string.Equals(resourceId[(cut + 1)..], expectedLeaf, StringComparison.Ordinal);
    }
}
