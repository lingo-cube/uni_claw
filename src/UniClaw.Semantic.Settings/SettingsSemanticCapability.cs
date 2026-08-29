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

    private static bool IsDuplicatePrimaryRowRendering(
        IReadOnlyCollection<SemanticObservationFact> occurrenceFacts,
        IReadOnlyCollection<SemanticObservationFact> allPrimaryFacts)
    {
        if (occurrenceFacts.Any(f =>
                string.Equals(f.RawProviderType, "menu_item", StringComparison.OrdinalIgnoreCase)
                || string.Equals(f.RawProviderType, "menuItem", StringComparison.OrdinalIgnoreCase)))
            return false;
        var text = occurrenceFacts.Select(f => f.RawText).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
        var bounds = occurrenceFacts.Select(f => f.Bounds).FirstOrDefault(b => b is not null);
        if (string.IsNullOrWhiteSpace(text) || bounds is null)
            return false;
        var peers = allPrimaryFacts
            .Where(f => !string.Equals(f.OccurrenceId, occurrenceFacts.First().OccurrenceId, StringComparison.Ordinal)
                && string.Equals(f.RawText, text, StringComparison.Ordinal)
                && f.Bounds is { } peerBounds
                && Overlaps(peerBounds, bounds)
                && (string.Equals(f.RawProviderType, "menu_item", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(f.RawProviderType, "menuItem", StringComparison.OrdinalIgnoreCase)))
            .Select(f => f.OccurrenceId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return peers.Length == 1;
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
