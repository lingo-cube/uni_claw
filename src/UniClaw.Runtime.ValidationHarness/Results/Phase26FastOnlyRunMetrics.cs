using System.Collections.Immutable;
using UniClaw.Runtime.ValidationHarness.Classification;

namespace UniClaw.Runtime.ValidationHarness.Results;

/// <summary>
/// Immutable Phase-2.6 Fast-only run metrics (WI-CRV2-P26-B). One deterministic
/// schema holding ALL of the campaign's collected quantities for a fresh
/// campaign, designed to be compared item-by-item against the frozen old
/// 19-run / 0-Completed baseline (see <see cref="Phase26BaselineComparison"/>).
///
/// The 19 named count metrics plus <c>BlockerCategoryCounts</c>,
/// <c>FirstDivergence</c> and <c>RunTerminalDisposition</c> are each a
/// classified <see cref="ResultField{T}"/> mirroring the existing
/// DirectProjection / DerivedReadModel / Unavailable truth-source discipline.
/// A deterministic harness cannot measure a device-only quantity (e.g. the
/// physical depth a flash driver actually reached, or an OCR short-read) — such
/// a metric is recorded EXPLICITLY Unavailable with its reason, never inferred
/// from Graph / Belief / reason (WI-CRV2-P26-B: "mark, never fabricate").
///
/// The record is fully immutable (init-only, copied collections); it carries no
/// mutable owner / cache / service and serializes stably (round-trip).
/// NET_NEW_MUTABLE_TRUTH = 0: it is a read projection, never a live owner.
/// NEW_SYMBOL_JUSTIFICATION: no existing metrics type carries this exact
/// 20-metric Phase-2.6 schema; the existing <see cref="ValidationResult"/>
/// aggregates one run's evidence, not a campaign's cross-run metric set.
/// </summary>
public sealed record Phase26FastOnlyRunMetrics
{
    /// <summary>Gets the count of runs that reached terminal=Completed.</summary>
    public ResultField<int> CompletedRuns { get; init; } = ResultField<int>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets the deepest container traversal depth reached (device-only
    /// on real hardware; Unavailable when a deterministic harness cannot measure it).</summary>
    public ResultField<int> DeepestTraversalDepth { get; init; } = ResultField<int>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets the number of containers entered across the campaign.</summary>
    public ResultField<int> ContainersEntered { get; init; } = ResultField<int>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets the number of branches attempted (entry into a child path).</summary>
    public ResultField<int> BranchesAttempted { get; init; } = ResultField<int>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets the number of branches completed (child epoch proven).</summary>
    public ResultField<int> BranchesCompleted { get; init; } = ResultField<int>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets the count of containers left unresolved (UNRESOLVED at terminal).</summary>
    public ResultField<int> UnresolvedContainers { get; init; } = ResultField<int>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets the count of deep-Unknown events (semantic signal vacuum
    /// producing a no-identity current node).</summary>
    public ResultField<int> DeepUnknownCount { get; init; } = ResultField<int>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets the count of wrong-branch observations (intended child C,
    /// observed unrelated D; OBSERVED != SATISFIED).</summary>
    public ResultField<int> WrongBranchCount { get; init; } = ResultField<int>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets the count of unexpected off-path transitions (fresh world
    /// outside the expected path, retained append-only without a normal edge).</summary>
    public ResultField<int> UnexpectedOffPathTransitions { get; init; } = ResultField<int>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets the count of repeated traversals over already-visited evidence.</summary>
    public ResultField<int> RepeatedTraversalCount { get; init; } = ResultField<int>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets the count of restart-full-reset events during the campaign.</summary>
    public ResultField<int> RestartFullResetCount { get; init; } = ResultField<int>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets the count of current-vs-execution mismatches (physical
    /// CurrentContainer != ActiveExecutionContainer, r5-legal).</summary>
    public ResultField<int> CurrentVsExecutionMismatches { get; init; } = ResultField<int>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets the count of Fast resolutions that were trusted (working
    /// interpretation accepted as current).</summary>
    public ResultField<int> FastTrustedCount { get; init; } = ResultField<int>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets the count of Fast abstentions (no working interpretation).</summary>
    public ResultField<int> FastAbstainedCount { get; init; } = ResultField<int>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets the count of Fast conflicts (working interpretation
    /// conflicts with another signal/revision).</summary>
    public ResultField<int> FastConflictCount { get; init; } = ResultField<int>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets the count of FALSE Fast trusts — a Fast interpretation that
    /// was trusted but later proven wrong (never inferred; recorded only from
    /// direct subsequent evidence).</summary>
    public ResultField<int> FalseFastTrustCount { get; init; } = ResultField<int>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets the count of transition-reconciliation failures (stale /
    /// invalid reconciliation attempts that failed closed).</summary>
    public ResultField<int> TransitionReconciliationFailures { get; init; } = ResultField<int>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets the count of coverage-exhaustion failures (local inventory
    /// exhausted while semantic evidence remained unresolved).</summary>
    public ResultField<int> CoverageExhaustionFailures { get; init; } = ResultField<int>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets the count of stale-occurrence-bounds rejections (a stale or
    /// historical LocalModel frame rejected fail-closed).</summary>
    public ResultField<int> StaleOccurrenceBoundsRejections { get; init; } = ResultField<int>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets per-category blocker counts over the exactly-13
    /// <see cref="Phase26BlockerCategory"/> taxonomy. When populated, the map is
    /// validated to contain exactly the 13 defined categories (never a partial
    /// / invented category set).</summary>
    public ResultField<ImmutableSortedDictionary<Phase26BlockerCategory, int>> BlockerCategoryCounts { get; init; } =
        ResultField<ImmutableSortedDictionary<Phase26BlockerCategory, int>>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets the earliest evidence-derived divergence across the
    /// campaign (see <see cref="Phase26BlockerRecord.FirstDivergence"/>).</summary>
    public ResultField<string> FirstDivergence { get; init; } = ResultField<string>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Gets the campaign-terminal disposition (e.g. Completed /
    /// Unresolved / Failed-with-blocker) as recorded evidence.</summary>
    public ResultField<string> RunTerminalDisposition { get; init; } = ResultField<string>.Unavailable("No campaign run metrics were measured.");

    /// <summary>Enumerates every named count metric field (the 19 campaign
    /// counts) for walking / traceability / immutability assertions.</summary>
    public IEnumerable<ResultField<int>> EnumerateCountMetrics()
    {
        yield return CompletedRuns;
        yield return DeepestTraversalDepth;
        yield return ContainersEntered;
        yield return BranchesAttempted;
        yield return BranchesCompleted;
        yield return UnresolvedContainers;
        yield return DeepUnknownCount;
        yield return WrongBranchCount;
        yield return UnexpectedOffPathTransitions;
        yield return RepeatedTraversalCount;
        yield return RestartFullResetCount;
        yield return CurrentVsExecutionMismatches;
        yield return FastTrustedCount;
        yield return FastAbstainedCount;
        yield return FastConflictCount;
        yield return FalseFastTrustCount;
        yield return TransitionReconciliationFailures;
        yield return CoverageExhaustionFailures;
        yield return StaleOccurrenceBoundsRejections;
    }

    /// <summary>Enumerates every classified field of the metric set (all counts
    /// plus the blocker counts, first divergence and terminal disposition).</summary>
    public IEnumerable<IClassifiedField> EnumerateClassifiedFields()
    {
        foreach (var count in EnumerateCountMetrics())
        {
            yield return count;
        }

        yield return BlockerCategoryCounts;
        yield return FirstDivergence;
        yield return RunTerminalDisposition;
    }

    /// <summary>An empty metric set: every metric honestly Unavailable — the
    /// deterministic default for a campaign with no measured data (never zeroed,
    /// which would fabricate measured values).</summary>
    public static Phase26FastOnlyRunMetrics Empty()
        => new();

    /// <summary>Convenience factory for an explicitly unavailable (device-only)
    /// count metric with a stated reason — the honest "mark, never infer" path
    /// for a quantity a deterministic harness cannot measure.</summary>
    /// <param name="reason">Readable reason why the quantity is not measurable
    /// on the current deterministic harness.</param>
    public static ResultField<int> DeviceUnavailableCount(string reason)
        => ResultField<int>.Unavailable(reason);
}
