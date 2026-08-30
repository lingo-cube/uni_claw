using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;

namespace UniClaw.Semantic.Settings;

/// <summary>
/// External scenario vocabulary and interpreter for Settings observations.
///
/// Classification is primary-Vision driven: only primary occurrences may
/// receive candidate evidence, and every emitted candidate references a
/// primary occurrence with primary provenance. Auxiliary structured facts are
/// used ONLY as deterministic current-frame corroboration (raw text equality
/// or bounds overlap) to decide interaction shape (clickable row / toggle /
/// labelled back control). Auxiliary-only occurrences are never promoted.
/// </summary>
public sealed class SettingsSemanticCapability : IExternalSemanticCapability
{
    private static readonly ImmutableHashSet<string> SupportedLocales =
        ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "en-US", "en-GB");

    public SemanticCapabilityManifest Manifest { get; } = new(
        "uni-claw.settings.semantic", "1",
        new[] { "settings.container", "settings.preference-row", "settings.search-role", "settings.navigate-up", "settings.parent-container" });

    private readonly string _locale;

    public SettingsSemanticCapability(string locale = "en-US")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        _locale = locale;
    }

    public ValueTask<ImmutableArray<SemanticEvidenceV2Envelope>> InterpretAsync(
        ExternalSemanticCapabilityContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (!SupportedLocales.Contains(_locale))
            return ValueTask.FromResult(ImmutableArray<SemanticEvidenceV2Envelope>.Empty);

        // Structured-only acquisition is deliberately insufficient for semantic authority.
        var primaryFacts = context.Facts.Where(f => f.SourceTier == SemanticSourceTier.Primary).ToArray();
        if (primaryFacts.Length == 0)
            return ValueTask.FromResult(ImmutableArray<SemanticEvidenceV2Envelope>.Empty);

        var auxiliary = context.Facts
            .Where(f => f.SourceTier == SemanticSourceTier.Auxiliary)
            .ToArray();

        var results = ImmutableArray.CreateBuilder<SemanticEvidenceV2Envelope>();
        foreach (var occurrenceFacts in primaryFacts.GroupBy(f => f.OccurrenceId, StringComparer.Ordinal))
        {
            var facts = occurrenceFacts.ToArray();
            var fact = facts.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f.RawText) ||
                                                  !string.IsNullOrWhiteSpace(f.RawContentDescription)) ?? facts[0];
            if (fact.ObservationSequence != context.Observation.Sequence ||
                !string.Equals(fact.FrameId, context.Observation.FrameId, StringComparison.Ordinal))
                continue;

            var text = string.Join(" ", facts.SelectMany(f => new[] { f.RawText, f.RawContentDescription }))
                .Trim();
            var corroboration = Correlate(auxiliary, fact);

            // Generic composition is resolved before semantic role admission:
            // preserve the primary visual role, record ChildOf(parent), then
            // admit an affordance only when independent child interaction is
            // evidenced. A clickable parent is never credited to its child.
            if (SemanticComposition.TryVerifyChild(facts, auxiliary, out var parent, out var independentlyInteractive))
            {
                // PARENT-ROLE INHERITANCE (runJ 'Parent-return candidate is
                // absent'): when an occurrence is verified as the child OF THE
                // BACK CONTROL itself (the auxiliary 'Navigate up' label), the
                // child (e.g. the text-less arrow icon inside the button) IS the
                // parent-return control — record ReturnToParent instead of a
                // consumptive ChildOf (which would `continue` and starve the
                // return classification). Control-role only; never identity.
                // NOTE: TryVerifyChild returns the parent's GEOMETRY fact, so
                // the back-control check is per-OCCURRENCE (all facts), not on
                // that single fact (FACT_FRAGMENTATION: the 'Navigate up'
                // content-description lives on the parent's other fact).
                var parentIsBackControl = auxiliary.Any(f =>
                    string.Equals(f.OccurrenceId, parent.OccurrenceId, StringComparison.Ordinal)
                    && IsBackControlLabel(f));
                if (parentIsBackControl)
                {
                    results.Add(Relation(fact, context.Observation, fact.OccurrenceId, "settings.navigate-up"));
                    continue;
                }
                var parentSymbol = IsSearchActionBar(parent, auxiliary)
                    ? "settings.search-role"
                    : "settings.parent-container";
                results.Add(ChildRelation(fact, context.Observation, fact.OccurrenceId, parentSymbol));
                if (!independentlyInteractive)
                    continue;

                results.Add(Affordance(
                    fact,
                    context.Observation,
                    fact.OccurrenceId,
                    parentSymbol,
                    ElementAffordanceKind.NavigationCandidate));
                continue;
            }

            // ROOT PAGE identity: the "Settings" title (primary or corroborated).
            if (text.Equals("Settings", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(Container(fact, context.Observation, fact.OccurrenceId, "settings.container"));
                results.Add(Affordance(
                    fact,
                    context.Observation,
                    fact.OccurrenceId,
                    "settings.container",
                    ElementAffordanceKind.NonInteractive));
                continue;
            }

            // SEARCH role: primary "Search" label or corroborating search-action-bar token.
            if (text.Equals("Search", StringComparison.OrdinalIgnoreCase)
                || IsVisualSearchHint(facts, text)
                || corroboration.Any(HasSearchActionBarToken))
            {
                results.Add(Affordance(fact, context.Observation, fact.OccurrenceId, "settings.search-role"));
                continue;
            }

            // PARENT-RETURN control: primary "Navigate up" label or corroborating
            // back-control label.
            if (text.Equals("Navigate up", StringComparison.OrdinalIgnoreCase)
                || text.Equals("NavigateUp", StringComparison.OrdinalIgnoreCase)
                || corroboration.Any(c => IsBackControlLabel(c)))
            {
                results.Add(Relation(fact, context.Observation, fact.OccurrenceId, "settings.navigate-up"));
                continue;
            }

            // PARENT-RETURN position/context fallback (runH 'Parent-return
            // candidate is absent'): a child-page back arrow is TEXT-LESS and
            // the campaign's structured tier carries NO bounds — the Correlate
            // (same-text / overlapping-bounds) bridge to the 'Navigate up'
            // auxiliary label is structurally broken. When the frame carries an
            // auxiliary back-control label AND exactly ONE top-band
            // (centerY ≤ 0.2) icon/image occurrence exists — the toolbar back
            // arrow — THAT occurrence is the parent-return control. Unique-icon
            // requirement keeps ambiguity fail-closed; never position/coordinate
            // identity for sources (control role only, bounded to the return
            // affordance).
            if (auxiliary.Any(c => IsBackControlLabel(c))
                && IsUniqueTopBandBackIcon(facts, primaryFacts))
            {
                results.Add(Relation(fact, context.Observation, fact.OccurrenceId, "settings.navigate-up"));
                continue;
            }

            // A provider may expose an alternate text box for the same visible
            // row in addition to the composed menu_item.  It is not a second
            // action source when one unique primary-Vision peer has identical
            // text and overlapping bounds.  This disposition is deliberately
            // non-interactive and never promotes an untyped text box.
            if (IsDuplicatePrimaryRowRendering(facts, primaryFacts))
            {
                results.Add(Affordance(
                    fact,
                    context.Observation,
                    fact.OccurrenceId,
                    "settings.preference-row",
                    ElementAffordanceKind.NonInteractive));
                continue;
            }

            // ROW-BAND SUB-ELEMENT (ROW_BAND_SUB_ELEMENT pattern — bounded
            // repair for the 'Not set'/'Will never' residuals; real child-frame
            // evidence r5 seq25): a text_block that is (a) fully contained
            // inside a composed menu_item row band, OR (b) that row's immediate
            // caption directly below it in the same left column — with DIFFERENT
            // text from the row, no interaction shape and no structural peer of
            // its own text — is a SUPPORTING sub-element of that EXACTLY-ONE
            // row, not an independent interactive obligation
            // (SECONDARY_REPRESENTATION != INDEPENDENT_INTERACTION_OBLIGATION).
            // Occurrence-level aggregation; any guard failure keeps the item
            // fail-closed (unchanged). Never proves support by row_id/StableKey/
            // same-text/bounds alone; Pattern-5/Pattern-7 semantics untouched.
            if (IsRowBandSubElement(facts, primaryFacts, auxiliary))
            {
                results.Add(Affordance(
                    fact,
                    context.Observation,
                    fact.OccurrenceId,
                    "settings.preference-row",
                    ElementAffordanceKind.NonInteractive));
                continue;
            }

            // LOCAL control: toggle/switch-shaped primary occurrence.
            if (facts.Any(IsLocalControl) || corroboration.Any(IsToggleShape))
            {
                results.Add(Affordance(fact, context.Observation, fact.OccurrenceId, "settings.preference-row", ElementAffordanceKind.LocalControl));
                continue;
            }

            // PREFERENCE ROW: primary text row corroborated as an interactive
            // Settings row (clickable LinearLayout / menu_item provider) or a
            // clearly interactive primary row.
            if (LooksLikePreferenceRow(facts) || corroboration.Any(IsNavigationRowShape))
            {
                results.Add(Affordance(fact, context.Observation, fact.OccurrenceId, "settings.preference-row"));
            }
        }

        // ── PATTERN 7: SUBTITLE / DESCRIPTION OF KNOWN ROW (E_SUBTITLE_ADMISSION_PATTERN) ──
        // A primary text occurrence that got NO evidence from Patterns 1–6,
        // and has a clear spatial subordination to a KNOWN preference row
        // (a row that DID get preference-row evidence in this same observation):
        // directly below that row's bottom edge (gap ≤ 60% of the row's own
        // height), in the same left column, with different text, and no
        // interactive shape evidence. This is DESCRIPTION_OF_KNOWN_ROW →
        // NonInteractive — NOT "any non-clickable text is NonInteractive".
        var classifiedAsPreferenceRow = results
            .Where(e => e.Candidate is ElementAffordanceCandidateEvidence a
                && a.AffordanceKind == ElementAffordanceKind.NavigationCandidate)
            .Select(e => ((ElementAffordanceCandidateEvidence)e.Candidate).OccurrenceId)
            .ToHashSet(StringComparer.Ordinal);
        var evidenceOccurrenceIds = results
            .Select(e => CandidateOccurrenceIdOf(e))
            .Where(id => id is not null)
            .ToHashSet(StringComparer.Ordinal!);
        foreach (var occurrenceFacts in primaryFacts.GroupBy(f => f.OccurrenceId, StringComparer.Ordinal))
        {
            var occurrenceId = occurrenceFacts.Key;
            if (evidenceOccurrenceIds.Contains(occurrenceId))
                continue; // already classified by Patterns 1–6

            var facts = occurrenceFacts.ToArray();
            var textFact = facts.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f.RawText));
            if (textFact is null) continue;

            // Find the geometry fact for this occurrence
            var geometry = facts.FirstOrDefault(f => f.Kind == SemanticObservationFactKind.Geometry && f.Bounds is not null);
            if (geometry?.Bounds is not { } subBounds) continue;

            // Find a known preference row that this text is directly below
            foreach (var rowOccurrenceId in classifiedAsPreferenceRow)
            {
                var rowFacts = primaryFacts.Where(f => f.OccurrenceId == rowOccurrenceId).ToArray();
                var rowGeometry = rowFacts.FirstOrDefault(f => f.Kind == SemanticObservationFactKind.Geometry && f.Bounds is not null);
                if (rowGeometry?.Bounds is not { } rowBounds) continue;
                var rowText = rowFacts.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f.RawText))?.RawText;

                // Different text (not the row's own duplicate — that's Pattern 5)
                if (string.Equals(textFact.RawText?.Trim(), rowText?.Trim(), StringComparison.Ordinal))
                    continue;

                // Same left column (X1 within tolerance)
                const double ColumnTolerance = 0.05;
                if (Math.Abs(subBounds.Left - rowBounds.Left) > ColumnTolerance)
                    continue;

                // Directly below: subtitle's Y1 in [row.Y2, row.Y2 + row_height × 0.6]
                var rowHeight = rowBounds.Height;
                var maxGap = rowHeight * 0.6;
                if (subBounds.Top < rowBounds.Top + rowBounds.Height || subBounds.Top > rowBounds.Top + rowBounds.Height + maxGap)
                    continue;

                // Not an interactive shape (no toggle/switch)
                if (facts.Any(IsLocalControl) || facts.Any(IsToggleShape))
                    continue;

                // DESCRIPTION_OF_KNOWN_ROW → NonInteractive
                results.Add(Affordance(
                    textFact,
                    context.Observation,
                    occurrenceId,
                    "settings.preference-row",
                    ElementAffordanceKind.NonInteractive));
                break; // one row is sufficient
            }
        }

        return ValueTask.FromResult(results.ToImmutable());
    }

    private static string? CandidateOccurrenceIdOf(SemanticEvidenceV2Envelope envelope) =>
        envelope.Candidate switch
        {
            ElementAffordanceCandidateEvidence a => a.OccurrenceId,
            ContainerRelationCandidateEvidence r => r.RelatedOccurrenceId,
            _ => null,
        };

    /// <summary>
    /// Deterministic current-frame auxiliary corroboration for a primary
    /// occurrence: same raw text, or overlapping normalized bounds.
    /// Ambiguous matches never attach; the primary classification then relies
    /// on its own evidence only.
    /// </summary>
    private static SemanticObservationFact[] Correlate(
        IReadOnlyCollection<SemanticObservationFact> auxiliary,
        SemanticObservationFact primary)
    {
        if (auxiliary.Count == 0) return [];
        SemanticObservationFact[] matches;
        if (!string.IsNullOrWhiteSpace(primary.RawText))
        {
            matches = auxiliary
                .Where(c => string.Equals(c.RawText, primary.RawText, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 1)
                return matches;
        }
        if (primary.Bounds is not null)
        {
            matches = auxiliary
                .Where(c => c.Bounds is { } cb && Overlaps(cb, primary.Bounds))
                .ToArray();
            if (matches.Length == 1)
                return matches;
        }
        return [];
    }

    private static bool Overlaps(SemanticNormalizedBounds a, SemanticNormalizedBounds b)
        => a.Left <= b.Left + b.Width && b.Left <= a.Left + a.Width
           && a.Top <= b.Top + b.Height && b.Top <= a.Top + a.Height;

    private static bool HasSearchActionBarToken(SemanticObservationFact fact) =>
        fact.RawResourceName is { } rid
        && ResourceLeaf(rid).Equals("search_action_bar", StringComparison.Ordinal);

    private static bool IsSearchActionBar(
        SemanticObservationFact parent,
        IReadOnlyCollection<SemanticObservationFact> auxiliary) =>
        auxiliary.Any(f => string.Equals(f.OccurrenceId, parent.OccurrenceId, StringComparison.Ordinal)
            && (HasSearchActionBarToken(f)
                || f.RawClassName?.Contains("SearchView", StringComparison.OrdinalIgnoreCase) == true
                || f.RawClassName?.Contains("SearchBar", StringComparison.OrdinalIgnoreCase) == true));

    private static bool IsVisualSearchHint(
        IReadOnlyCollection<SemanticObservationFact> facts,
        string text)
    {
        if (!facts.Any(f => string.Equals(f.RawProviderType, "input", StringComparison.OrdinalIgnoreCase)))
            return false;
        var normalized = new string(text.Where(char.IsLetterOrDigit).ToArray());
        return normalized.EndsWith("searchsettings", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBackControlLabel(SemanticObservationFact fact) =>
        string.Equals(fact.RawText, "Navigate up", StringComparison.OrdinalIgnoreCase)
        || string.Equals(fact.RawContentDescription, "Navigate up", StringComparison.OrdinalIgnoreCase);

    private static bool IsToggleShape(SemanticObservationFact fact) =>
        fact.Checkable == true
        || (fact.RawClassName?.Contains("Switch", StringComparison.OrdinalIgnoreCase) == true)
        || string.Equals(fact.RawProviderType, "toggle", StringComparison.OrdinalIgnoreCase);

    private static bool IsNavigationRowShape(SemanticObservationFact fact) =>
        fact.Clickable == true
        && (fact.RawClassName?.Contains("LinearLayout", StringComparison.OrdinalIgnoreCase) == true
            || string.Equals(fact.RawProviderType, "menu_item", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fact.RawProviderType, "menuItem", StringComparison.OrdinalIgnoreCase));

    private static string ResourceLeaf(string resourceId)
    {
        var colon = resourceId.LastIndexOf(':');
        var slash = resourceId.LastIndexOf('/');
        return resourceId[(Math.Max(colon, slash) + 1)..];
    }

    private static bool LooksLikePreferenceRow(IReadOnlyCollection<SemanticObservationFact> facts) =>
        facts.Any(f => !string.IsNullOrWhiteSpace(f.RawText)) &&
        (facts.Any(f => f.RawClassName?.Contains("LinearLayout", StringComparison.OrdinalIgnoreCase) == true ||
                        string.Equals(f.RawProviderType, "menu_item", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(f.RawProviderType, "menuItem", StringComparison.OrdinalIgnoreCase)) &&
         facts.Any(f => !string.IsNullOrWhiteSpace(f.RawContentDescription) || f.PrimitiveState is not null || f.Bounds is not null));

    /// <summary>
    /// OCCURRENCE-SCOPED semantic view (PATTERN_5_OCCURRENCE_GRANULARITY_REPAIR_GATE):
    /// the production projector emits ONE occurrence as MULTIPLE facts (Text fact
    /// = RawText+Provider; ClassName fact; Geometry fact = Bounds). Predicates
    /// that evaluate OCCURRENCE properties (text / bounds / provider) MUST
    /// aggregate by OccurrenceId BEFORE evaluating — a single-fact query can
    /// never colocate RawText+bounds+provider (FACT_FRAGMENTATION: FACT !=
    /// OCCURRENCE; PREDICATE_REQUIRING_OCCURRENCE_PROPERTIES must not assume
    /// single-fact colocation). This view is a capability-internal predicate
    /// projection only — it is NOT a Runtime authority object.
    /// </summary>
    private readonly record struct OccurrenceSemanticView(
        string OccurrenceId,
        string? RawText,
        SemanticNormalizedBounds? Bounds,
        ImmutableArray<string> Providers);

    private static OccurrenceSemanticView ViewOf(IReadOnlyCollection<SemanticObservationFact> occurrenceFacts)
    {
        var first = occurrenceFacts.First();
        return new OccurrenceSemanticView(
            first.OccurrenceId,
            occurrenceFacts.Select(f => f.RawText).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)),
            occurrenceFacts.Select(f => f.Bounds).FirstOrDefault(b => b is not null),
            occurrenceFacts
                .Select(f => f.RawProviderType)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray());
    }

    /// <summary>
    /// PATTERN-5 — OCCURRENCE-LEVEL duplicate/primary-row suppression predicate
    /// (semantic conditions UNCHANGED; only the input granularity is
    /// occurrence-scoped): the CURRENT secondary text representation is a
    /// duplicate when there exists EXACTLY ONE OTHER menu_item occurrence with
    /// the same RawText, mutually overlapping bounds (existing overlap
    /// predicate), and a menu_item provider. 0 peers → no suppression;
    /// exactly 1 peer → NonInteractive (duplicate suppression); >1 peers →
    /// ambiguous → fail closed (no suppression). Duplication is never proven
    /// by row_id / StableKey alone, same-text alone, same-bounds alone, or XML
    /// corroboration alone.
    /// </summary>
    private static bool IsDuplicatePrimaryRowRendering(
        IReadOnlyCollection<SemanticObservationFact> occurrenceFacts,
        IReadOnlyCollection<SemanticObservationFact> allPrimaryFacts)
    {
        // The current occurrence itself is a primary row (not a secondary
        // representation): it is never a duplicate.
        if (occurrenceFacts.Any(f =>
                string.Equals(f.RawProviderType, "menu_item", StringComparison.OrdinalIgnoreCase)
                || string.Equals(f.RawProviderType, "menuItem", StringComparison.OrdinalIgnoreCase)))
            return false;

        var current = ViewOf(occurrenceFacts);
        var text = current.RawText;
        var bounds = current.Bounds;
        if (string.IsNullOrWhiteSpace(text) || bounds is null)
            return false;

        // Occurrence-level peer evaluation: aggregate each OTHER occurrence's
        // fragmented facts (rawText / bounds / providers) before matching.
        var peers = allPrimaryFacts
            .GroupBy(f => f.OccurrenceId, StringComparer.Ordinal)
            .Select(group => ViewOf(group.ToArray()))
            .Where(peer => !string.Equals(peer.OccurrenceId, current.OccurrenceId, StringComparison.Ordinal)
                && string.Equals(peer.RawText, text, StringComparison.Ordinal)
                && peer.Bounds is { } peerBounds
                && Overlaps(peerBounds, bounds)
                && peer.Providers.Any(p =>
                    string.Equals(p, "menu_item", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p, "menuItem", StringComparison.OrdinalIgnoreCase)))
            .Select(peer => peer.OccurrenceId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return peers.Length == 1;
    }

    /// <summary>
    /// ROW-BAND SUB-ELEMENT predicate (ROW_BAND_SUB_ELEMENT pattern): a
    /// secondary text representation T is the supporting sub-element of a
    /// UNIQUE composed menu_item row R in the same frame when:
    ///   (a) T's center is CONTAINED inside R's row band, OR
    ///   (b) T is R's immediate caption: same left column (existing 0.05
    ///       tolerance) and 0 ≤ (T.top − R.bottom) ≤ 0.8 × R.height
    ///       (the 0.8 bound covers OCR/geometry quantization — real evidence:
    ///       'Will never' at 0.010625 vs 0.0105 = 0.6× pattern bound, a
    ///       sub-1% quantization flake), with DIFFERENT text from R.
    /// T must be text-bearing, geometry-bearing, NOT menu_item-shaped, NOT a
    /// toggle-shaped control, and WITHOUT a structural (XML) peer row of its
    /// own text. EXACTLY ONE candidate row required — zero or multiple rows →
    /// fail closed (unchanged behavior). Occurrence-level aggregation (Pattern-5
    /// gate convention); support is never proven by row_id / StableKey / same
    /// text / bounds alone.
    /// </summary>
    private static bool IsRowBandSubElement(
        IReadOnlyCollection<SemanticObservationFact> occurrenceFacts,
        IReadOnlyCollection<SemanticObservationFact> allPrimaryFacts,
        IReadOnlyCollection<SemanticObservationFact> auxiliary)
    {
        if (occurrenceFacts.Any(f =>
                string.Equals(f.RawProviderType, "menu_item", StringComparison.OrdinalIgnoreCase)
                || string.Equals(f.RawProviderType, "menuItem", StringComparison.OrdinalIgnoreCase)))
            return false;
        var current = ViewOf(occurrenceFacts);
        var text = current.RawText;
        var bounds = current.Bounds;
        if (string.IsNullOrWhiteSpace(text) || bounds is null)
            return false;
        if (occurrenceFacts.Any(IsToggleShape))
            return false;
        // Same-text rows are the Pattern-5 DUPLICATE domain (unique → suppressed
        // there; ambiguous → fail-closed there). The row-band sub-element rule
        // never redeems same-text pairs: sub-lines have DIFFERENT text.
        var hasMenuRowWithSameText = allPrimaryFacts
            .GroupBy(f => f.OccurrenceId, StringComparer.Ordinal)
            .Select(group => ViewOf(group.ToArray()))
            .Any(row => row.Providers.Any(p =>
                    string.Equals(p, "menu_item", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p, "menuItem", StringComparison.OrdinalIgnoreCase))
                && string.Equals(row.RawText, text, StringComparison.Ordinal));
        if (hasMenuRowWithSameText)
            return false;

        // Independent-interaction guard: a structural (XML) peer row bearing the
        // sub-line's OWN text suggests a real interactive row — fail closed.
        var primaryFact = occurrenceFacts.First(f =>
            !string.IsNullOrWhiteSpace(f.RawText) || !string.IsNullOrWhiteSpace(f.RawContentDescription))
            is { } tf ? tf : occurrenceFacts.First();
        if (Correlate(auxiliary, primaryFact) is { Length: > 0 } corroboration
            && corroboration.Any(IsNavigationRowShape))
            return false;

        const double ColumnTolerance = 0.05;
        const double BelowGapRatio = 0.8;
        double centerX = bounds.Left + bounds.Width / 2.0;
        double centerY = bounds.Top + bounds.Height / 2.0;
        var candidateRows = allPrimaryFacts
            .GroupBy(f => f.OccurrenceId, StringComparer.Ordinal)
            .Select(group => ViewOf(group.ToArray()))
            .Where(row => row.Bounds is { } rowBounds
                && row.Providers.Any(p =>
                    string.Equals(p, "menu_item", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p, "menuItem", StringComparison.OrdinalIgnoreCase))
                && ((ContainsCenter(rowBounds, centerX, centerY)
                        // containment arm requires the sub-line to be
                        // meaningfully SMALLER than the row (a value/caption
                        // text, not an equal-size overlapping box)
                        && bounds.Height <= rowBounds.Height * 0.8)
                    || (Math.Abs(rowBounds.Left - bounds.Left) <= ColumnTolerance
                        && bounds.Top >= rowBounds.Top + rowBounds.Height
                        && bounds.Top - (rowBounds.Top + rowBounds.Height) <= BelowGapRatio * rowBounds.Height)))
            .Select(row => row.OccurrenceId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return candidateRows.Length == 1;
    }

    private static bool ContainsCenter(SemanticNormalizedBounds container, double centerX, double centerY)
        => centerX >= container.Left && centerX <= container.Left + container.Width
           && centerY >= container.Top && centerY <= container.Top + container.Height;

    /// <summary>True when the CURRENT occurrence is the UNIQUE top-band
    /// (centerY ≤ 0.2) text-less icon/image occurrence of the frame — the
    /// toolbar back arrow — usable as the parent-return control only when the
    /// frame also carries a 'Navigate up'/back auxiliary label (checked by the
    /// caller). Exactly-one requirement: two top icons → ambiguous → fail
    /// closed (no classification).</summary>
    private static bool IsUniqueTopBandBackIcon(
        IReadOnlyCollection<SemanticObservationFact> occurrenceFacts,
        IReadOnlyCollection<SemanticObservationFact> allPrimaryFacts)
    {
        if (occurrenceFacts.Any(f => !string.IsNullOrWhiteSpace(f.RawText)))
            return false;
        if (!occurrenceFacts.Any(f =>
                string.Equals(f.RawProviderType, "icon", StringComparison.OrdinalIgnoreCase)
                || string.Equals(f.RawProviderType, "image", StringComparison.OrdinalIgnoreCase)))
            return false;
        var bounds = occurrenceFacts.Select(f => f.Bounds).FirstOrDefault(b => b is not null);
        if (bounds is null || bounds.Top + bounds.Height / 2.0 > 0.2)
            return false;
        const double TopBand = 0.2;
        var topIcons = allPrimaryFacts
            .GroupBy(f => f.OccurrenceId, StringComparer.Ordinal)
            .Select(group => ViewOf(group.ToArray()))
            .Where(view => view.Bounds is { } vp
                && vp.Top + vp.Height / 2.0 <= TopBand
                && view.Providers.Any(p =>
                    string.Equals(p, "icon", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p, "image", StringComparison.OrdinalIgnoreCase)))
            .Select(view => view.OccurrenceId)
            .ToArray();
        if (topIcons.Length != 1)
            return false;
        return string.Equals(topIcons[0], occurrenceFacts.First().OccurrenceId, StringComparison.Ordinal);
    }

    private static bool IsLocalControl(SemanticObservationFact fact) =>
        fact.PrimitiveState is not null ||
        string.Equals(fact.RawProviderType, "toggle", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fact.RawProviderType, "switch", StringComparison.OrdinalIgnoreCase);

    private SemanticProvenance Provenance(SemanticObservationFact fact) =>
        new(fact.SourceId, fact.SourceTier, fact.ProvenanceId, DateTimeOffset.UnixEpoch, fact.FrameId);

    private SemanticEvidenceV2Envelope Container(SemanticObservationFact fact, SemanticObservationReference observation, string occurrence, string symbol) =>
        new($"settings:{occurrence}:{symbol}", new ContainerIdentityCandidateEvidence(
            Symbol(symbol), observation, Scope(fact), Provenance(fact), 0.9,
            DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue));

    private SemanticEvidenceV2Envelope Affordance(SemanticObservationFact fact, SemanticObservationReference observation, string occurrence, string symbol) =>
        Affordance(fact, observation, occurrence, symbol,
            symbol == "settings.search-role" ? ElementAffordanceKind.LocalControl : ElementAffordanceKind.NavigationCandidate);

    private SemanticEvidenceV2Envelope Affordance(SemanticObservationFact fact, SemanticObservationReference observation, string occurrence, string symbol, ElementAffordanceKind kind) =>
        new($"settings:{fact.OccurrenceId}:{symbol}", new ElementAffordanceCandidateEvidence(
            occurrence, kind,
            Symbol(symbol), observation, Scope(fact), Provenance(fact), 0.9,
            DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue));

    private SemanticEvidenceV2Envelope Relation(SemanticObservationFact fact, SemanticObservationReference observation, string occurrence, string symbol) =>
        new($"settings:{fact.OccurrenceId}:{symbol}", new ContainerRelationCandidateEvidence(
            occurrence, ContainerRelationKind.ReturnToParent, Symbol("settings.parent-container"),
            Symbol(symbol), observation, Scope(fact), Provenance(fact), 0.9,
            DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue));

    private SemanticEvidenceV2Envelope ChildRelation(
        SemanticObservationFact fact,
        SemanticObservationReference observation,
        string occurrence,
        string parentSymbol) =>
        new($"settings:{fact.OccurrenceId}:child-of:{parentSymbol}", new ContainerRelationCandidateEvidence(
            occurrence,
            ContainerRelationKind.Child,
            Symbol(parentSymbol),
            Symbol(parentSymbol),
            observation,
            Scope(fact),
            Provenance(fact),
            0.9,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.MaxValue));

    private SemanticSymbolReference Symbol(string id) => new(Manifest.ManifestId, Manifest.Version, id);
    private static SemanticScopeReference Scope(SemanticObservationFact fact) =>
        new($"occurrence:{fact.OccurrenceId}", SemanticEvidenceScope.Observation);
}
