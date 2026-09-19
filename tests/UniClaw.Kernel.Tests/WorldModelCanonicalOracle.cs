using System.Collections.Frozen;
using System.Text;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit.Sdk;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// WMP-002 test-only canonical oracle. Expected values are computed by naive
/// full-table scans over the SAME immutable revision's public canonical
/// collections; actual values come from the production optimized paths behind
/// the public WorldModel interface. This file never enters src/: production
/// keeps exactly one behavioral implementation and the default runtime path
/// performs no extra canonical scan.
/// </summary>
public static class CanonicalOracle
{
    public const string Schema = "wmp-canonical-oracle/1";

    private const int MaxValueLength = 96;

    // ---- First-Divergence report -------------------------------------

    /// <summary>One side of a first-divergence report (identity + value).</summary>
    public readonly record struct DivergenceFact(string? Identity, string? Value);

    /// <summary>
    /// Stable, machine-parseable first-divergence report. One canonical line
    /// (grep-able, key=value) plus human context lines. Values carry only
    /// stable identities / short descriptors (length-capped) — never secrets,
    /// raw bulk artifacts, or object addresses. The owner field localizes the
    /// minimal owning seam per uniclaw-debug-evidence semantics.
    /// </summary>
    public static string Divergence(
        string operation,
        string revisionId,
        string lookupKey,
        long expectedCount,
        long actualCount,
        long firstDivergentPosition,
        DivergenceFact? expected,
        DivergenceFact? actual,
        string? context = null) =>
        "WMP-DIVERGENCE schema=" + Schema
        + " op=" + operation
        + " owner=" + Quote(OwnerFor(operation))
        + " rev=" + Quote(revisionId)
        + " key=" + Quote(lookupKey)
        + " expectedCount=" + expectedCount
        + " actualCount=" + actualCount
        + " firstDivergentPosition=" + firstDivergentPosition
        + " expected=" + Quote(Render(expected))
        + " actual=" + Quote(Render(actual))
        + "\n  expected: " + Render(expected)
        + "\n  actual:   " + Render(actual)
        + "\n  gap:      first divergence at position " + firstDivergentPosition
        + " of the canonical sequence for " + operation
        + (string.IsNullOrEmpty(context) ? "" : "\n  context:  " + context);

    /// <summary>Minimal owning seam for an operation family (E-level owner
    /// localization; production realization, not a second truth).</summary>
    public static string OwnerFor(string operation) =>
        operation switch
        {
            "ResolveCurrent" or "DeriveSlice" or "BindingView" or "OutcomeView"
                or "ActionAssuranceView" or "ResolveCurrent.Result" or "Slice"
                or "StaleSlice" => "WorldModel consumer-view derivation via WorldRevisionIndex",
            "DemandRegistry" or "IsHotItem" => "WorldModel demand registry indexes",
            "PublicWorldState" or "PublicEvidenceBasis" or "PublicWorldGraph"
                or "GoldenCanonicalHash" or "HistoryInvariance" or "HistoryReplay"
                => "WorldModel reconciliation + PersistentRevisionCollections publication",
            "RevisedClaim" => "WorldModel claim evolution (reconcile)",
            _ => "WorldModel sole authority",
        };

    private static string Render(DivergenceFact? fact) =>
        fact is null
            ? "<none>"
            : Sanitize(fact.Value.Identity) + (fact.Value.Value is null ? "" : "|" + Sanitize(fact.Value.Value));

    private static string Quote(string? value) =>
        "\"" + (value ?? "<null>").Replace("\"", "'").Replace("\n", "\\n").Replace("\r", "") + "\"";

    internal static string Sanitize(string? value)
    {
        var text = (value ?? "<null>").Replace("\n", "\\n").Replace("\r", "");
        return text.Length <= MaxValueLength ? text : text[..MaxValueLength] + "…";
    }

    // ---- Comparison engine -------------------------------------------

    /// <summary>
    /// Entry-by-entry comparison of an expected (naive canonical scan) and an
    /// actual (optimized production) ordered sequence. Fails at the FIRST
    /// divergence with the structured report — never collapses to counts or
    /// hashes alone.
    /// </summary>
    public static void EqualSequence<T>(
        string operation,
        WorldBeliefRevision revision,
        string lookupKey,
        IReadOnlyList<T> expected,
        IReadOnlyList<T> actual,
        Func<T, string> identity,
        Func<T, string> value,
        string? context = null)
    {
        var shared = Math.Min(expected.Count, actual.Count);
        for (var i = 0; i < shared; i++)
        {
            if (!string.Equals(identity(expected[i]), identity(actual[i]), StringComparison.Ordinal)
                || !string.Equals(value(expected[i]), value(actual[i]), StringComparison.Ordinal))
                throw new XunitException(Divergence(
                    operation, revision.RevisionId, lookupKey,
                    expected.Count, actual.Count, i,
                    new DivergenceFact(identity(expected[i]), value(expected[i])),
                    new DivergenceFact(identity(actual[i]), value(actual[i])), context));
        }
        if (expected.Count != actual.Count)
            throw new XunitException(Divergence(
                operation, revision.RevisionId, lookupKey,
                expected.Count, actual.Count, shared,
                shared < expected.Count
                    ? new DivergenceFact(identity(expected[shared]), value(expected[shared]))
                    : null,
                shared < actual.Count
                    ? new DivergenceFact(identity(actual[shared]), value(actual[shared]))
                    : null,
                context));
    }

    /// <summary>Scalar fact comparison (flags, kinds, echoes) with the same report shape.</summary>
    public static void EqualScalar(
        string operation,
        WorldBeliefRevision revision,
        string lookupKey,
        string expected,
        string actual,
        string? context = null)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            throw new XunitException(Divergence(
                operation, revision.RevisionId, lookupKey, 1, 1, 0,
                new DivergenceFact(expected, null),
                new DivergenceFact(actual, null), context));
    }

    // ---- Expected computations (naive scans over public collections) ----

    /// <summary>ResolveCurrent expectation: linear scan of the revision's Occurrences.</summary>
    public static IReadOnlyList<OccurrenceBelief> ExpectedResolveCurrent(
        WorldBeliefRevision revision, TargetDescriptor descriptor) =>
        (revision.Occurrences ?? Array.Empty<OccurrenceBelief>())
            .Where(o => o.Role == descriptor.Role
                && (descriptor.OwningContainerId is null || o.OwningContainerId == descriptor.OwningContainerId)
                && (descriptor.SemanticDescriptor is null || o.SemanticDescriptor == descriptor.SemanticDescriptor))
            .ToArray();

    /// <summary>DeriveSlice occurrence expectation: owner ∈ scope, global canonical order.</summary>
    public static IReadOnlyList<OccurrenceBelief> ExpectedSliceOccurrences(
        WorldBeliefRevision revision, IReadOnlyList<string> scope) =>
        (revision.Occurrences ?? Array.Empty<OccurrenceBelief>())
            .Where(o => o.OwningContainerId is not null && scope.Contains(o.OwningContainerId))
            .ToArray();

    /// <summary>DeriveSlice scoped-claim expectation: naive WorldState scan
    /// for `<containerId>.`-prefixed subjects, projected through the same BCL
    /// frozen construction the canonical (pre-WMP-001 baseline) publication
    /// used — the observable order is the frozen layout of the scoped subset,
    /// not the raw WorldState enumeration order.</summary>
    public static IReadOnlyList<(string Subject, string Value)> ExpectedScopedClaims(
        WorldBeliefRevision revision, IReadOnlyList<string> scope) =>
        revision.WorldState
            .Where(kv => kv.Key.Contains('.')
                && scope.Any(id => kv.Key.StartsWith(id + ".", StringComparison.Ordinal)))
            .ToFrozenDictionary(kv => kv.Key, kv => kv.Value.Value)
            .Select(kv => (kv.Key, kv.Value))
            .ToArray();

    /// <summary>Outcome view claim expectation: naive WorldState scan for
    /// in-scope subjects, projected through the same frozen construction the
    /// consumer view publishes (observable order = frozen layout).</summary>
    public static IReadOnlyList<(string Subject, string Value, string EvidenceId)> ExpectedOutcomeClaims(
        WorldBeliefRevision revision, IReadOnlySet<string> subjects) =>
        revision.WorldState
            .Where(kv => subjects.Contains(kv.Key))
            .ToFrozenDictionary(kv => kv.Key, kv => (kv.Value.Value, kv.Value.EvidenceId))
            .Select(kv => (kv.Key, kv.Value.Item1, kv.Value.Item2))
            .ToArray();

    /// <summary>Conflict expectation: linear scan of the revision's Conflicts.</summary>
    public static IReadOnlyList<Conflict> ExpectedConflicts(
        WorldBeliefRevision revision, IReadOnlySet<string> subjects) =>
        revision.Conflicts.Where(c => subjects.Contains(c.Subject)).ToArray();

    /// <summary>ActionAssurance expectation: any conflict on the subject.</summary>
    public static bool ExpectedHasConflict(WorldBeliefRevision revision, string subject) =>
        revision.Conflicts.Any(c => c.Subject == subject);

    /// <summary>BindingView occurrence expectation: linear occurrence-id scan.</summary>
    public static OccurrenceBelief? ExpectedOccurrence(
        WorldBeliefRevision revision, string occurrenceId) =>
        (revision.Occurrences ?? Array.Empty<OccurrenceBelief>())
            .FirstOrDefault(o => o.OccurrenceId == occurrenceId);

    /// <summary>IsHotItem expectation: linear scan of the public demand registry.</summary>
    public static bool ExpectedIsHotItem(WorldModel world, string logicalItemId) =>
        world.ContinuityDemands.Any(d => d.LogicalItemId == logicalItemId);

    // ---- Identity/value renderings (stable, sanitize d) ----------------

    public static string OccurrenceIdentity(OccurrenceBelief o) => o.OccurrenceId;

    public static string OccurrenceValue(OccurrenceBelief o) =>
        Sanitize($"{o.Role}|{o.SemanticDescriptor}|{o.OwningContainerId}|{o.State}|{Describe(o.Locator)}|{Describe(o.Native)}");

    public static string CandidateIdentity(CandidateOccurrenceFact c) => c.OccurrenceId;

    public static string CandidateValue(CandidateOccurrenceFact c) =>
        Sanitize($"{c.Role}|{c.SemanticDescriptor}|{c.OwningContainerId}|{c.SourceRevisionId}");

    public static string FactIdentity(OccurrenceFact o) => o.OccurrenceId;

    public static string FactValue(OccurrenceFact o) =>
        Sanitize($"{o.Role}|{o.SemanticDescriptor}|{o.OwningContainerId}|{o.State}|{Describe(o.Locator)}|{Describe(o.Native)}");

    public static string ClaimIdentity((string Subject, string Value) kv) => kv.Subject;

    public static string ClaimValue((string Subject, string Value) kv) => Sanitize(kv.Value);

    public static string OutcomeClaimIdentity((string Subject, string Value, string EvidenceId) kv) => kv.Subject;

    public static string OutcomeClaimValue((string Subject, string Value, string EvidenceId) kv) =>
        Sanitize(kv.Value + "|" + kv.EvidenceId);

    public static string ConflictIdentity(Conflict c) => $"{c.Subject}#{c.EstablishedEvidenceId}->{c.ChallengingEvidenceId}";

    public static string ConflictValue(Conflict c) =>
        Sanitize($"{c.EstablishedValue}|{c.ChallengingValue}");

    public static string Describe(SpatialLocator? locator) => locator is null ? "-" : Sanitize(locator.ToString());

    public static string Describe(NativeLocator? native) => native is null ? "-" : Sanitize(native.ToString());

    // ---- Full public-collection battery --------------------------------

    /// <summary>
    /// Deep content+order oracle over the public canonical collections.
    /// Content is verified item-by-item against the driver-tracked
    /// expectation (subject/value/evidence). Enumeration ORDER is verified
    /// against a test-side incremental frozen chain: the canonical order is
    /// frozenLayout(parent order + newly added keys) — it depends on the
    /// construction sequence (fresh rebuilds over a permutation may differ),
    /// so the oracle mirrors the publication algorithm with BCL frozen
    /// collections over driver-known deltas, independently of production's
    /// persistent structures.
    /// </summary>
    public static void EqualPublicState(
        WorldBeliefRevision revision,
        IReadOnlyList<(string Subject, string Value, string EvidenceId)> expectedState,
        IReadOnlyList<string> expectedStateOrder,
        IReadOnlyList<string> expectedBasisOrder,
        IReadOnlyList<string> expectedGraph,
        string context)
    {
        // content: every expected subject present with exact value/evidence
        var expectedMap = expectedState.ToDictionary(t => t.Subject, StringComparer.Ordinal);
        var position = 0;
        foreach (var kv in revision.WorldState)
        {
            if (!expectedMap.TryGetValue(kv.Key, out var expected))
                throw new XunitException(Divergence(
                    "PublicWorldState.Content", revision.RevisionId, "world-state",
                    expectedMap.Count, revision.WorldState.Count, position,
                    null,
                    new DivergenceFact(kv.Key, kv.Value.Value + "|" + kv.Value.EvidenceId),
                    $"{context}: unexpected subject in canonical state"));
            if (!string.Equals(expected.Value, kv.Value.Value, StringComparison.Ordinal)
                || !string.Equals(expected.EvidenceId, kv.Value.EvidenceId, StringComparison.Ordinal))
                throw new XunitException(Divergence(
                    "PublicWorldState.Content", revision.RevisionId, $"subject={kv.Key}",
                    1, 1, position,
                    new DivergenceFact(expected.Subject, expected.Value + "|" + expected.EvidenceId),
                    new DivergenceFact(kv.Key, kv.Value.Value + "|" + kv.Value.EvidenceId), context));
            position++;
        }
        if (expectedMap.Count != position)
        {
            var missing = expectedMap.Keys.Except(revision.WorldState.Keys).First();
            throw new XunitException(Divergence(
                "PublicWorldState.Content", revision.RevisionId, $"subject={missing}",
                expectedMap.Count, position, position,
                new DivergenceFact(missing, expectedMap[missing].Value),
                null,
                $"{context}: expected subject missing from canonical state"));
        }

        // order: production incremental publication == test-side frozen chain
        EqualSequence(
            "PublicWorldState.Order", revision, "world-state",
            expectedStateOrder.Select(key =>
                (Subject: key, expectedMap[key].Value, expectedMap[key].EvidenceId)).ToArray(),
            revision.WorldState.Select(kv =>
                (Subject: kv.Key, kv.Value.Value, kv.Value.EvidenceId)).ToArray(),
            kv => kv.Subject, kv => Sanitize(kv.Value + "|" + kv.EvidenceId), context);

        EqualSequence(
            "PublicEvidenceBasis.Order", revision, "evidence-basis",
            expectedBasisOrder.Select(id => new OracleEntry(id)).ToArray(),
            revision.EvidenceBasis.Select(id => new OracleEntry(id)).ToArray(),
            entry => entry.Id, _ => "-", context);

        EqualSequence(
            "PublicWorldGraph", revision, "world-graph",
            expectedGraph.Select(id => new OracleEntry(id)).ToArray(),
            revision.WorldGraph.Select(id => new OracleEntry(id)).ToArray(),
            entry => entry.Id, _ => "-", context);
    }

    /// <summary>Single-entry carrier for id-only sequence comparisons.</summary>
    public readonly record struct OracleEntry(string Id);

    /// <summary>Canonical rendering used for golden hashing and replay invariance.</summary>
    public static string RenderRevision(WorldBeliefRevision r) =>
        new StringBuilder()
            .Append(r.RevisionId).Append('|').Append(r.ParentRevisionId).Append('|').Append(r.RevisionNumber)
            .Append("|state=").Append(string.Join(";", r.WorldState.Select(kv =>
                $"{kv.Key}={kv.Value.Value}:{kv.Value.EvidenceId}:{kv.Value.EstablishingProducer}:{kv.Value.EstablishingScope}:"
                + string.Join(",", kv.Value.SupersededEvidenceIds ?? Array.Empty<string>()))))
            .Append("|graph=").Append(string.Join(";", r.WorldGraph))
            .Append("|basis=").Append(string.Join(";", r.EvidenceBasis))
            .Append("|conflicts=").Append(string.Join(";", r.Conflicts.Select(c =>
                $"{c.Subject}:{c.EstablishedValue}:{c.ChallengingValue}:{c.EstablishedEvidenceId}:{c.ChallengingEvidenceId}")))
            .Append("|containers=").Append(string.Join(";", (r.Containers ?? Array.Empty<ContainerBelief>()).Select(c =>
                c.Identity.ContainerId + ":" + string.Join(",", c.EvidenceBasis))))
            .Append("|relations=").Append(string.Join(";", (r.Relations ?? Array.Empty<ContainerRelation>()).Select(x =>
                $"{x.Kind}:{x.SourceContainerId}:{x.TargetContainerId}:" + string.Join(",", x.EvidenceBasis))))
            .Append("|occurrences=").Append(string.Join(";", (r.Occurrences ?? Array.Empty<OccurrenceBelief>()).Select(o =>
                $"{o.OccurrenceId}:{o.OwningContainerId}:{o.Role}:{o.SemanticDescriptor}:{o.State}")))
            .Append("|items=").Append(string.Join(";", (r.LogicalItems ?? Array.Empty<LogicalItemBelief>()).Select(i =>
                $"{i.LogicalItemId}:{i.OwningContainerId}:{i.Role}:{i.SemanticDescriptor}:{i.Lifecycle}:{i.EndedReason}:"
                + string.Join(",", i.EvidenceBasis))))
            .ToString();
}
