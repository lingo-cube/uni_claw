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

            // ROOT PAGE identity: the "Settings" title (primary or corroborated).
            if (text.Equals("Settings", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(Container(fact, context.Observation, fact.OccurrenceId, "settings.container"));
                continue;
            }

            // SEARCH role: primary "Search" label or corroborating search-action-bar token.
            if (text.Equals("Search", StringComparison.OrdinalIgnoreCase)
                || corroboration.Any(c => HasSearchActionBarToken(c)))
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

        return ValueTask.FromResult(results.ToImmutable());
    }

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
                        string.Equals(f.RawProviderType, "menuItem", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(f.RawProviderType, "text", StringComparison.OrdinalIgnoreCase)) &&
         facts.Any(f => !string.IsNullOrWhiteSpace(f.RawContentDescription) || f.PrimitiveState is not null || f.Bounds is not null));

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

    private SemanticSymbolReference Symbol(string id) => new(Manifest.ManifestId, Manifest.Version, id);
    private static SemanticScopeReference Scope(SemanticObservationFact fact) =>
        new($"occurrence:{fact.OccurrenceId}", SemanticEvidenceScope.Observation);
}
