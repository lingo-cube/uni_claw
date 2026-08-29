using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.World;

/// <summary>
/// Agent-run-local deterministic source-occurrence normalizer for accepted
/// same-Container viewport Observations.
///
/// It uses:
/// - InteractionAffordanceAnalyzer to identify NAVIGATION_CANDIDATE occurrences
/// - exact structured signatures
/// - unique ordered overlap between adjacent viewports
///
/// It never uses bounds, node path, or destination as logical identity.
/// </summary>
public sealed record SourceNormalizationResult(
    ImmutableArray<string> UniqueSourceSignatures,
    ImmutableArray<SourceEquivalenceEvidence> EquivalenceEvidence,
    int UnresolvedCount,
    bool IsResolved,
    ImmutableArray<BoundaryTruncationRecord> BoundaryTruncations,
    ImmutableArray<AnchorMergeRecord> AnchorMerges)
{
    /// <summary>Creates an unresolved normalization result with the supplied reason.</summary>
    /// <param name="reason">Diagnostic reason for the unresolved result.</param>
    public static SourceNormalizationResult Unresolved(string reason)
        => new([], [], 1, false, [], []);
}

/// <summary>Normalizes accepted viewport observations into source-equivalence evidence.</summary>
public static class SourceEquivalenceNormalizer
{
    /// <summary>Produces deterministic source normalization for accepted observations.</summary>
    /// <param name="acceptedObservations">Accepted observations in run order.</param>
    public static SourceNormalizationResult Normalize(ImmutableArray<Observation> acceptedObservations)
    {
        if (acceptedObservations.IsDefaultOrEmpty)
            return SourceNormalizationResult.Unresolved("No accepted viewport observations.");

        // Convert each Observation to an ordered list of occurrence signatures.
        var sequences = ImmutableArray.CreateBuilder<ImmutableArray<string>>();
        foreach (var observation in acceptedObservations)
        {
            var signatures = ExtractNavigationSignatures(observation);
            if (signatures.IsDefaultOrEmpty)
                return SourceNormalizationResult.Unresolved(
                    $"Observation {observation.SequenceNumber} has no structured navigation candidates.");
            if (signatures.Distinct(StringComparer.Ordinal).Count() != signatures.Length)
                return SourceNormalizationResult.Unresolved(
                    $"Observation {observation.SequenceNumber} contains duplicate structured navigation signatures; equivalence is ambiguous.");
            sequences.Add(signatures);
        }

        var current = sequences[0];
        var evidence = ImmutableArray.CreateBuilder<SourceEquivalenceEvidence>();
        var boundaryTruncations = ImmutableArray.CreateBuilder<BoundaryTruncationRecord>();
        var anchorMerges = ImmutableArray.CreateBuilder<AnchorMergeRecord>();
        bool priorWindowResolvedByAnchor = false;
        for (int i = 1; i < sequences.Count; i++)
        {
            var next = sequences[i];
            var overlapLength = FindUniqueSuffixPrefixOverlap(current, next);
            // effectiveNext is the window actually compared/merged; it equals
            // `next` for strict matches, or a boundary-trimmed slice when strict
            // matching failed and relaxation succeeded.
            var effectiveNext = next;
            bool skipFirst = false;
            bool skipLast = false;

            if (overlapLength is null)
            {
                // An anchor merge preserves the accumulated union, including
                // earlier role/signature variants between the latest window's
                // anchors. Consequently, that latest accepted window need not
                // be a suffix of the union. Allow exactly one immediately
                // adjacent, exact-Ordinal repetition to confirm that window
                // without adding a source. This is deliberately narrower than
                // permitting general zero-insertion anchor revisits.
                if (priorWindowResolvedByAnchor
                    && sequences[i - 1].SequenceEqual(next, StringComparer.Ordinal))
                {
                    for (int k = 0; k < next.Length; k++)
                    {
                        evidence.Add(new SourceEquivalenceEvidence(
                            $"{acceptedObservations[i - 1].SequenceNumber}:{k}",
                            $"{acceptedObservations[i].SequenceNumber}:{k}",
                            SourceEquivalenceKind.SameSource,
                            "Exact adjacent accepted-window confirmation after anchor merge."));
                    }
                    priorWindowResolvedByAnchor = false;
                    continue;
                }

                // Strict suffix-prefix overlap failed. Progressively relax by
                // skipping the top and/or bottom (viewport-truncated) row of THIS
                // window. Skipped rows are never added to the union and never
                // participate in signature comparison; each skip is recorded
                // explicitly as boundary-truncated (never silent). Strict match is
                // always attempted first, so strict-match behavior is unchanged.
                var relaxation = TryBoundaryRelaxation(current, next);
                if (relaxation is not null)
                {
                    var r = relaxation.Value;
                    overlapLength = r.OverlapLength;
                    effectiveNext = r.TrimmedNext;
                    skipFirst = r.SkipFirst;
                    skipLast = r.SkipLast;

                    var windowSeq = acceptedObservations[i].SequenceNumber;
                    if (skipFirst)
                        boundaryTruncations.Add(new BoundaryTruncationRecord(
                            windowSeq, 0, next[0],
                            BoundaryTruncationRecord.BoundaryTruncatedReason));
                    if (skipLast)
                    {
                        var lastIdx = next.Length - 1;
                        boundaryTruncations.Add(new BoundaryTruncationRecord(
                            windowSeq, lastIdx, next[lastIdx],
                            BoundaryTruncationRecord.BoundaryTruncatedReason));
                    }
                }
                else
                {
                    // Strict AND boundary tolerance both failed. Third-tier
                    // fallback: neighbor-anchored merge. Find window rows that
                    // exactly (Ordinal) match rows already in the union and use
                    // them as anchors to insert the non-matching rows in place.
                    // At least one anchor is required; zero anchors keep the
                    // result fail-closed (Unresolved), identical to the prior
                    // behavior. Existing union elements are never deleted or
                    // reordered.
                    var anchorResult = TryAnchorBasedMerge(current, next);
                    if (anchorResult is null)
                    {
                        return SourceNormalizationResult.Unresolved(
                            $"Adjacent viewport overlap is ambiguous or absent between sequence {i - 1} and {i}.");
                    }
                    var ar = anchorResult.Value;
                    current = ar.MergedSequence;
                    anchorMerges.Add(new AnchorMergeRecord(
                        acceptedObservations[i].SequenceNumber,
                        ar.AnchorCount,
                        ar.InsertedRows,
                        AnchorMergeRecord.AnchorMergeReason));
                    // Record SAME_SOURCE evidence for each anchor: the window row
                    // at WindowIdx is the same logical source as the union row at
                    // UnionIdx. Inserted rows produce no equivalence evidence
                    // (they are new sources, not equivalences).
                    foreach (var am in ar.AnchorMappings)
                        evidence.Add(new SourceEquivalenceEvidence(
                            $"{acceptedObservations[i - 1].SequenceNumber}:{am.UnionIdx}",
                            $"{acceptedObservations[i].SequenceNumber}:{am.WindowIdx}",
                            SourceEquivalenceKind.SameSource,
                            "Anchor: exact-Ordinal match within neighbor-anchored merge."));
                    priorWindowResolvedByAnchor = true;
                    continue;
                }
            }

            // Record SAME_SOURCE evidence for each overlapped occurrence, computed
            // against the effective (possibly trimmed) window. The newId references
            // the ORIGINAL index in `next`, so skipped rows receive no evidence
            // entries.
            var firstNewIndex = skipFirst ? 1 : 0;
            for (int k = 0; k < overlapLength.Value; k++)
            {
                var oldId = $"{acceptedObservations[i - 1].SequenceNumber}:{current.Length - overlapLength.Value + k}";
                var newId = $"{acceptedObservations[i].SequenceNumber}:{firstNewIndex + k}";
                evidence.Add(new SourceEquivalenceEvidence(
                    oldId,
                    newId,
                    SourceEquivalenceKind.SameSource,
                    "Unique ordered overlap of exact structured signatures."));
            }

            // Append only newly appearing (non-skipped) sources.
            var combined = ImmutableArray.CreateBuilder<string>();
            combined.AddRange(current);
            for (int k = overlapLength.Value; k < effectiveNext.Length; k++)
                combined.Add(effectiveNext[k]);
            current = combined.ToImmutable();
            priorWindowResolvedByAnchor = false;
        }

        return new SourceNormalizationResult(
            current, evidence.ToImmutable(), 0, true,
            boundaryTruncations.ToImmutable(), anchorMerges.ToImmutable());
    }

    private static ImmutableArray<string> ExtractNavigationSignatures(Observation observation)
    {
        var affordances = InteractionAffordanceAnalyzer.Analyze(observation);
        var hasExplicitPrimary = observation.Sources.Any(source =>
            source.Tier == ObservationSourceTier.PrimaryVision
            && source.Available
            && source.ObservationSequence == observation.SequenceNumber);
        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var affordance in affordances)
        {
            if (affordance.Classification != InteractionAffordanceKind.NavigationCandidate)
                continue;
            var canonical = affordance.CanonicalOccurrence;
            if (canonical is null) continue;
            // With an explicitly correlated primary Vision source, auxiliary
            // structured rows may corroborate diagnostics but cannot define
            // the logical-source sequence used for completeness.  Preserve the
            // source-less legacy compatibility path where structured evidence
            // is the only channel declared by the caller.
            if (hasExplicitPrimary && !canonical.EligibleForAuthorization)
                continue;
            var signature = OccurrenceSignature(observation, canonical);
            if (signature is null) continue;
            builder.Add(signature);
        }
        return builder.ToImmutable();
    }

    /// <summary>
    /// Deterministic occurrence derivation for ONE accepted Observation.
    /// Returns the ordered NAVIGATION_CANDIDATE occurrences with
    /// observation-local identities ("nav:1".."nav:n") and exact structured
    /// signatures. Occurrence identity is observation-local only. Occurrences
    /// of both source tiers are enumerated; callers MUST filter
    /// <see cref="NavigationSourceOccurrence.EligibleForAuthorization"/> before
    /// any authorization-bearing use (auxiliary occurrences are never
    /// authorization-eligible).
    /// </summary>
    public static ImmutableArray<NavigationSourceOccurrence> OccurrencesOf(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var affordances = InteractionAffordanceAnalyzer.Analyze(observation);
        var builder = ImmutableArray.CreateBuilder<NavigationSourceOccurrence>();
        int ordinal = 0;
        foreach (var affordance in affordances)
        {
            if (affordance.Classification != InteractionAffordanceKind.NavigationCandidate)
                continue;
            var canonical = affordance.CanonicalOccurrence;
            if (canonical is null) continue;
            var signature = OccurrenceSignature(observation, canonical);
            if (signature is null) continue;
            ordinal++;
            builder.Add(new NavigationSourceOccurrence(
                observation.SequenceNumber,
                $"nav:{ordinal}",
                signature,
                ordinal,
                canonical));
        }
        return builder.ToImmutable();
    }

    /// <summary>Derives the equivalence signature for a canonical occurrence from
    /// its own source channel: Vision elements use Text|PerceptionType, auxiliary
    /// structured elements use RawText|Class|ResourceId|ContentDescription.</summary>
    private static string? OccurrenceSignature(Observation observation, CanonicalObservationOccurrence canonical)
    {
        if (canonical.Reference.SourceKind == ObservationSourceKind.PrimaryVision)
        {
            if (canonical.Reference.ElementIndex < observation.Elements.Length)
                return BuildSignature(observation.Elements[canonical.Reference.ElementIndex]);
            return null;
        }
        if (canonical.Reference.ElementIndex < observation.StructuredElements.Length)
            return BuildSignature(observation.StructuredElements[canonical.Reference.ElementIndex]);
        return null;
    }

    /// <summary>
    /// STABLE SOURCE EQUIVALENCE KEY (evidence-contract repair): the identity
    /// key for a primary Vision occurrence is
    ///   StableKey ?? Text | PerceptionType.
    /// When <see cref="ObservedElement.StableKey"/> is non-null (a perception-layer
    /// stable row id), it is used in place of Text to stabilize identity across
    /// text-recognition drift; when null the construction falls back to Text
    /// (legacy-compatible). Bounds / node path / viewport ordinal / destination
    /// are never identity.
    /// </summary>
    /// <summary>
    /// STABLE SOURCE EQUIVALENCE KEY (evidence-contract repair): the identity
    /// key for a primary Vision occurrence is
    ///   StableKey ?? Text | PerceptionType.
    /// When <see cref="ObservedElement.StableKey"/> is non-null (a perception-layer
    /// stable row id), it is used in place of Text to stabilize identity across
    /// text-recognition drift; when null the construction falls back to Text
    /// (legacy-compatible). Bounds / node path / viewport ordinal / destination
    /// are never identity.
    /// </summary>
    internal static string BuildSignature(ObservedElement raw) =>
        string.Join("|", raw.StableKey ?? raw.Text ?? "", raw.PerceptionType ?? "", "", "");

    /// <summary>
    /// STABLE SOURCE EQUIVALENCE KEY for an auxiliary structured occurrence:
    ///   RawText | Class | ResourceId | ContentDescription.
    /// Bounds / node path / viewport ordinal / destination are never identity.
    /// </summary>
    internal static string BuildSignature(StructuredElementEvidence raw) =>
        string.Join("|", raw.RawText ?? "", raw.Class ?? "", raw.ResourceId ?? "", raw.ContentDescription ?? "");

    /// <summary>
    /// Finds the unique maximal length L such that the suffix of current of
    /// length L exactly equals the prefix of next of length L.
    /// Returns null when zero or multiple overlaps are possible.
    /// </summary>
    private static int? FindUniqueSuffixPrefixOverlap(
        ImmutableArray<string> current,
        ImmutableArray<string> next)
    {
        int? best = null;
        int max = Math.Min(current.Length, next.Length);
        for (int length = max; length >= 1; length--)
        {
            bool match = true;
            for (int i = 0; i < length; i++)
            {
                if (!string.Equals(
                        current[current.Length - length + i],
                        next[i],
                        StringComparison.Ordinal))
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                if (best is not null)
                    return null; // ambiguous
                best = length;
            }
        }
        return best;
    }

    /// <summary>
    /// Deterministic contiguous slice of an immutable signature array.
    /// </summary>
    private static ImmutableArray<string> Slice(ImmutableArray<string> source, int start, int length)
    {
        if (length <= 0)
            return ImmutableArray<string>.Empty;
        var builder = ImmutableArray.CreateBuilder<string>(length);
        for (int i = 0; i < length; i++)
            builder.Add(source[start + i]);
        return builder.MoveToImmutable();
    }

    /// <summary>
    /// Boundary-row tolerance relaxation, attempted ONLY after strict
    /// <see cref="FindUniqueSuffixPrefixOverlap"/> fails. Progressive,
    /// deterministic order: skip the first row (top viewport-truncated row),
    /// then skip the last row (bottom viewport-truncated row), then skip both.
    /// Only the first/last row of THIS window may be skipped (never a middle
    /// row). Returns the first relaxation that yields a UNIQUE overlap, or null
    /// when none of the three succeed. Comparison remains exact Ordinal.
    /// </summary>
    private readonly record struct BoundaryRelaxation(
        ImmutableArray<string> TrimmedNext,
        bool SkipFirst,
        bool SkipLast,
        int OverlapLength);

    private static BoundaryRelaxation? TryBoundaryRelaxation(
        ImmutableArray<string> current,
        ImmutableArray<string> next)
    {
        // Skip the first (top boundary) row.
        if (next.Length >= 2)
        {
            var trimmed = Slice(next, 1, next.Length - 1);
            var overlap = FindUniqueSuffixPrefixOverlap(current, trimmed);
            if (overlap is not null)
                return new BoundaryRelaxation(trimmed, true, false, overlap.Value);
        }
        // Skip the last (bottom boundary) row.
        if (next.Length >= 2)
        {
            var trimmed = Slice(next, 0, next.Length - 1);
            var overlap = FindUniqueSuffixPrefixOverlap(current, trimmed);
            if (overlap is not null)
                return new BoundaryRelaxation(trimmed, false, true, overlap.Value);
        }
        // Skip both boundary rows.
        if (next.Length >= 3)
        {
            var trimmed = Slice(next, 1, next.Length - 2);
            var overlap = FindUniqueSuffixPrefixOverlap(current, trimmed);
            if (overlap is not null)
                return new BoundaryRelaxation(trimmed, true, true, overlap.Value);
        }
        return null;
    }

    /// <summary>
    /// Anchor mapping captured for evidence: a window row index and the union
    /// index it exactly (Ordinal) matched at merge time (post-insertion indices
    /// are NOT recorded; only the original mapping is).
    /// </summary>
    private readonly record struct AnchorMapping(int WindowIdx, int UnionIdx);

    /// <summary>
    /// Result of a neighbor-anchored merge. Existing union elements are never
    /// deleted or reordered; non-anchor window rows are inserted between their
    /// nearest surrounding anchors. <see cref="AnchorMappings"/> records the
    /// ORIGINAL (pre-insertion) window→union index mapping for each anchor so
    /// equivalence evidence can reference stable identities.
    /// </summary>
    private readonly record struct AnchorMergeResult(
        ImmutableArray<string> MergedSequence,
        int AnchorCount,
        ImmutableArray<string> InsertedRows,
        ImmutableArray<AnchorMapping> AnchorMappings);

    /// <summary>
    /// Third-tier fallback (attempted only after strict suffix-prefix overlap
    /// AND boundary-row relaxation both fail): neighbor-anchored merge.
    ///
    /// Anchors are window rows that exactly (Ordinal) match a row already in the
    /// union. Non-anchor window rows are inserted between their nearest
    /// surrounding anchors (above anchor first; else before the below anchor;
    /// else appended at the end). Existing union elements are never deleted or
    /// reordered; the result is deterministic.
    ///
    /// LEADER NARROWING — returns null (fail-closed) when ANY of these hold:
    ///   - zero anchors found;
    ///   - zero rows actually inserted (pure repeat / backward view with no new
    ///     information — preserves the REVISIT_COMPLETENESS_FRESHNESS_PRESSURE
    ///     contract that non-monotonic revisits stay Unresolved);
    ///   - anchors' union indices are NOT strictly increasing in window order
    ///     (a backward scroll reverses union order and carries no gap to fill).
    /// Comparison remains exact Ordinal — no fuzzy matching is introduced.
    /// </summary>
    private static AnchorMergeResult? TryAnchorBasedMerge(
        ImmutableArray<string> union,
        ImmutableArray<string> window)
    {
        // 1. Find anchors: window rows that exist in union. First (lowest-union-
        // index) match wins per window row, keeping the algorithm deterministic.
        var anchors = new List<(int WindowIdx, int UnionIdx)>();
        for (int w = 0; w < window.Length; w++)
        {
            for (int u = 0; u < union.Length; u++)
            {
                if (string.Equals(window[w], union[u], StringComparison.Ordinal))
                {
                    anchors.Add((w, u));
                    break; // first match (deterministic)
                }
            }
        }

        if (anchors.Count == 0)
            return null; // no anchors → fail-closed

        // LEADER NARROWING (2026-08-29): reject pure-repeat / backward views.
        // (a) At least 1 inserted row required — a window where ALL rows are
        //     anchors (0 new rows) is a revisit/repeat, which the
        //     REVISIT_COMPLETENESS_FRESHNESS_PRESSURE contract keeps Unresolved.
        // (b) Forward-ordering check — anchors' union indices must be strictly
        //     increasing when ordered by window index. Backward scroll produces
        //     decreasing union indices and is rejected.
        var orderedByWindow = anchors.OrderBy(a => a.WindowIdx).ToList();
        for (int i = 1; i < orderedByWindow.Count; i++)
        {
            if (orderedByWindow[i].UnionIdx <= orderedByWindow[i - 1].UnionIdx)
                return null; // backward/non-monotonic → fail-closed
        }

        // Check (a): count how many window rows would be inserted
        var anchorWindowSet = anchors.Select(a => a.WindowIdx).ToHashSet();
        var potentialInsertions = 0;
        for (int w = 0; w < window.Length; w++)
        {
            if (anchorWindowSet.Contains(w))
                continue;
            if (!union.Contains(window[w], StringComparer.Ordinal))
                potentialInsertions++;
        }
        if (potentialInsertions == 0)
            return null; // pure repeat (all anchors, no new rows) → fail-closed

        // Snapshot the ORIGINAL window→union anchor mapping for evidence.
        var originalMappings = anchors
            .Select(a => new AnchorMapping(a.WindowIdx, a.UnionIdx))
            .ToImmutableArray();

        // 2. Build the merged sequence, inserting non-anchor rows in place.
        var result = new List<string>(union);
        var anchorSet = anchors.Select(a => a.WindowIdx).ToHashSet();
        var insertedRows = new List<string>();

        // WI-FIX (insertion-order repair): when MULTIPLE new rows land between
        // the SAME pair of anchors, each row's anchor-based insertAt is computed
        // from the (already-shifted) anchor indices, so a later window row can
        // compute the SAME insertAt as an earlier one and land BEFORE it —
        // reversing window order (e.g. window [B,C,D] inserted as [D,C,B]).
        // Track the last actual insertion index and force every subsequent
        // insertion at or after it, so insertion order == window order.
        int lastInsertPos = -1;

        for (int w = 0; w < window.Length; w++)
        {
            if (anchorSet.Contains(w))
                continue; // already in union

            var text = window[w];
            if (result.Contains(text, StringComparer.Ordinal))
                continue; // already in union (duplicate in window, skip)

            // Find insertion position: between nearest anchors above and below.
            int? aboveAnchorUnionIdx = null;
            int? belowAnchorUnionIdx = null;

            foreach (var (aw, au) in anchors)
            {
                if (aw < w && (aboveAnchorUnionIdx is null || au > aboveAnchorUnionIdx))
                    aboveAnchorUnionIdx = au;
                if (aw > w && (belowAnchorUnionIdx is null || au < belowAnchorUnionIdx))
                    belowAnchorUnionIdx = au;
            }

            // Insert after the above anchor (or at the position before the below
            // anchor, or append at end when no anchors surround this row).
            int insertAt;
            if (aboveAnchorUnionIdx.HasValue)
                insertAt = aboveAnchorUnionIdx.Value + 1;
            else if (belowAnchorUnionIdx.HasValue)
                insertAt = belowAnchorUnionIdx.Value;
            else
                insertAt = result.Count; // append at end

            // Monotonic insertion guard: never insert before the previous
            // inserted row, so window order is preserved across same-anchor-pair
            // multi-insertions.
            insertAt = Math.Max(insertAt, lastInsertPos + 1);

            // Clamp to a valid insertion index (defensive; previous insertions
            // cannot push this beyond result.Count).
            insertAt = Math.Min(insertAt, result.Count);

            result.Insert(insertAt, text);
            insertedRows.Add(text);
            lastInsertPos = insertAt;

            // Update anchor union indices for subsequent insertions so later
            // rows keep landing between their (shifted) surrounding anchors.
            for (int i = 0; i < anchors.Count; i++)
            {
                var (aw, au) = anchors[i];
                if (au >= insertAt)
                    anchors[i] = (aw, au + 1);
            }
        }

        // LEADER NARROWING (condition 1, ground-truth guard): a merge that
        // produced zero insertions is a pure repeat / backward view with no new
        // information — the REVISIT_COMPLETENESS_FRESHNESS_PRESSURE contract
        // keeps it fail-closed. (The potentialInsertions pre-check above is an
        // early-exit optimization; this is the authoritative post-build guard.)
        if (insertedRows.Count == 0)
            return null;

        return new AnchorMergeResult(
            result.ToImmutableArray(),
            originalMappings.Length,
            insertedRows.ToImmutableArray(),
            originalMappings);
    }
}
