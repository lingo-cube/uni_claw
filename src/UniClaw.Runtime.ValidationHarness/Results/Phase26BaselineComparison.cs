using System.Collections.Immutable;
using UniClaw.Runtime.ValidationHarness.Classification;

namespace UniClaw.Runtime.ValidationHarness.Results;

/// <summary>
/// Frozen historical facts of the Phase-2.6 old baseline campaign
/// (openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/
/// PHASE-2.6-FINAL-REPORT.md): 19 fresh real runs, 0 Completed, and the
/// documented blocker distribution. These facts are historical truth — this
/// type READS them, never rewrites the evidence file (WI-CRV2-P26-B: "reference
/// it; never rewrite history").
/// NEW_SYMBOL_JUSTIFICATION: the old-baseline facts needed a stable typed home
/// that separates them from the new metrics and makes the historical citation
/// path explicit.
/// </summary>
public sealed record Phase26BaselineFacts
{
    /// <summary>Relative path of the historical evidence file this baseline cites.</summary>
    public const string EvidencePath =
        "openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/PHASE-2.6-FINAL-REPORT.md";

    private Phase26BaselineFacts(
        int freshRuns,
        int completedRuns,
        ImmutableSortedDictionary<Phase26BlockerCategory, int> blockerDistribution)
    {
        FreshRuns = freshRuns;
        CompletedRuns = completedRuns;
        BlockerDistribution = blockerDistribution;
    }

    /// <summary>Gets the number of fresh real runs in the old campaign (19).</summary>
    public int FreshRuns { get; }

    /// <summary>Gets the old campaign's Completed-run count (0 — no run reached
    /// terminal=Completed).</summary>
    public int CompletedRuns { get; }

    /// <summary>Gets the documented old blocker distribution over the 13-value
    /// taxonomy (counts cited from the final report; categories not documented
    /// are absent, never zero-fabricated).</summary>
    public ImmutableSortedDictionary<Phase26BlockerCategory, int> BlockerDistribution { get; }

    /// <summary>
    /// The canonical frozen old Phase-2.6 baseline: 19 fresh real runs, 0
    /// Completed, blocker distribution cited from the final report. The cited
    /// evidence file must exist (validated against the repository root).
    /// </summary>
    public static Phase26BaselineFacts FrozenPhase26()
    {
        ResolvedEvidencePath(); // validate the cited evidence file exists
        return new Phase26BaselineFacts(
            freshRuns: 19,
            completedRuns: 0,
            new Dictionary<Phase26BlockerCategory, int>
            {
                [Phase26BlockerCategory.PERCEPTION] = 10, // 5 root-Unknown + 5 deep-Unknown/garble-blocks (I,M,O,P,S; N,R,T,V,X)
                [Phase26BlockerCategory.CAPTURE] = 3,     // root Normalize Unresolved (L,U,W) + settle-budget rhythm (G,K role)
                [Phase26BlockerCategory.ENVIRONMENT] = 1, // empty run (Q)
                [Phase26BlockerCategory.UNKNOWN] = 1,     // un-attributed sporadic exit event (r5)
            }.ToImmutableSortedDictionary());
    }

    /// <summary>Resolves the cited historical evidence path against the repo root
    /// (the guard-pattern upward search) and returns it, throwing if absent.</summary>
    public static string ResolvedEvidencePath()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, EvidencePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Phase-2.6 frozen baseline cites evidence '{EvidencePath}' which no longer exists — history must not be rewritten.",
                path);
        }

        return path;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(dir.FullName, "src", "UniClaw.Runtime.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Cannot locate the repository root (no directory with AGENTS.md and src/UniClaw.Runtime.sln was found).");
    }
}

/// <summary>
/// The direct answer to the frozen acceptance question "old baseline 0 Completed
/// vs new CompletedRuns" (WI-CRV2-P26-B). It states the old frozen count, the
/// new counted value (classified), and an explicit human-readable answer.
/// NEW_SYMBOL_JUSTIFICATION: this is the one field the acceptance explicitly
/// requires — a single typed answer object keeps the comparison self-evident.
/// </summary>
public sealed record Phase26CompletedRunsAnswer
{
    /// <summary>Gets the frozen old-baseline Completed count (0).</summary>
    public int BaselineCompletedRuns { get; }

    /// <summary>Gets the new campaign's CompletedRuns metric (classified; value
    /// null when honestly Unavailable).</summary>
    public ResultField<int> NewCompletedRuns { get; }

    /// <summary>Gets the explicit comparison answer text (e.g. "old 0 Completed
    /// vs new N Completed").</summary>
    public string Answer { get; }

    /// <summary>Creates the Completed-runs answer from the frozen baseline and
    /// the new metric.</summary>
    public Phase26CompletedRunsAnswer(int baselineCompletedRuns, ResultField<int> newCompletedRuns)
    {
        if (baselineCompletedRuns < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baselineCompletedRuns));
        }

        ArgumentNullException.ThrowIfNull(newCompletedRuns);
        BaselineCompletedRuns = baselineCompletedRuns;
        NewCompletedRuns = newCompletedRuns;
        // A value-type metric's Value is the numeric default (0) even when
        // Unavailable — truthfulness is carried by the classification, not by a
        // null value (mirrors ResultField<T> semantics for value types).
        Answer = newCompletedRuns.Classification != ResultFieldClassification.Unavailable
            ? $"old baseline {baselineCompletedRuns} Completed vs new {newCompletedRuns.Value} Completed"
            : $"old baseline {baselineCompletedRuns} Completed vs new CompletedRuns unavailable (honest Unavailable)";
    }
}

/// <summary>
/// One item-by-item comparison row between the frozen old baseline and a new
/// metric value (WI-CRV2-P26-B). New and baseline quantities are each classified
/// so the comparison never fabricates a delta for an Unavailable quantity.
/// NEW_SYMBOL_JUSTIFICATION: a typed row makes the逐项 comparison mechanically
/// walkable and auditable.
/// </summary>
/// <param name="MetricName">Stable metric identifier.</param>
/// <param name="BaselineValue">Frozen old-baseline value (classified).</param>
/// <param name="NewValue">New campaign value (classified).</param>
public sealed record Phase26MetricComparison(
    string MetricName,
    ResultField<int> BaselineValue,
    ResultField<int> NewValue);

/// <summary>
/// Immutable comparison of the frozen old Phase-2.6 baseline (19 runs /
/// 0 Completed, blocker distribution cited by evidence path) against new
/// <see cref="Phase26FastOnlyRunMetrics"/>. It produces an explicit
/// <see cref="Phase26CompletedRunsAnswer"/> plus item-by-item metric rows, and
/// the old-baseline blocker distribution vs the new one. It never edits the
/// historical evidence file.
/// NEW_SYMBOL_JUSTIFICATION: required to deliver the "old baseline vs new
/// metrics per-item comparison with a Completed-runs answer" acceptance; no
/// existing comparison type owns this cross-campaign view.
/// </summary>
public sealed record Phase26BaselineComparison
{
    private Phase26BaselineComparison(
        Phase26BaselineFacts baseline,
        Phase26FastOnlyRunMetrics newMetrics,
        Phase26CompletedRunsAnswer completedRunsAnswer,
        ImmutableArray<Phase26MetricComparison> metricComparisons)
    {
        Baseline = baseline;
        NewMetrics = newMetrics;
        CompletedRunsAnswer = completedRunsAnswer;
        MetricComparisons = metricComparisons;
    }

    /// <summary>Gets the frozen old-baseline facts.</summary>
    public Phase26BaselineFacts Baseline { get; }

    /// <summary>Gets the new campaign metrics being compared.</summary>
    public Phase26FastOnlyRunMetrics NewMetrics { get; }

    /// <summary>Gets the direct answer to the "old 0 Completed vs new
    /// CompletedRuns" acceptance question.</summary>
    public Phase26CompletedRunsAnswer CompletedRunsAnswer { get; }

    /// <summary>Gets the item-by-item metric comparisons (new vs frozen baseline).</summary>
    public ImmutableArray<Phase26MetricComparison> MetricComparisons { get; }

    /// <summary>Builds a comparison of the frozen baseline against the supplied
    /// new metrics. The baseline is the canonical <see cref="Phase26BaselineFacts.FrozenPhase26"/>.</summary>
    public static Phase26BaselineComparison Compare(Phase26FastOnlyRunMetrics newMetrics)
    {
        ArgumentNullException.ThrowIfNull(newMetrics);
        var baseline = Phase26BaselineFacts.FrozenPhase26();
        var rows = ImmutableArray.CreateBuilder<Phase26MetricComparison>();
        AddRow(rows, "CompletedRuns",
            ResultField<int>.Direct(baseline.CompletedRuns, "Frozen Phase-2.6 baseline completed-run count (PHASE-2.6-FINAL-REPORT.md)"),
            newMetrics.CompletedRuns);
        AddRow(rows, "DeepestTraversalDepth", UnavailableBaseline(), newMetrics.DeepestTraversalDepth);
        AddRow(rows, "ContainersEntered", UnavailableBaseline(), newMetrics.ContainersEntered);
        AddRow(rows, "BranchesAttempted", UnavailableBaseline(), newMetrics.BranchesAttempted);
        AddRow(rows, "BranchesCompleted", UnavailableBaseline(), newMetrics.BranchesCompleted);
        AddRow(rows, "UnresolvedContainers", UnavailableBaseline(), newMetrics.UnresolvedContainers);
        AddRow(rows, "DeepUnknownCount", UnavailableBaseline(), newMetrics.DeepUnknownCount);
        AddRow(rows, "WrongBranchCount", UnavailableBaseline(), newMetrics.WrongBranchCount);
        AddRow(rows, "UnexpectedOffPathTransitions", UnavailableBaseline(), newMetrics.UnexpectedOffPathTransitions);
        AddRow(rows, "RepeatedTraversalCount", UnavailableBaseline(), newMetrics.RepeatedTraversalCount);
        AddRow(rows, "RestartFullResetCount", UnavailableBaseline(), newMetrics.RestartFullResetCount);
        AddRow(rows, "CurrentVsExecutionMismatches", UnavailableBaseline(), newMetrics.CurrentVsExecutionMismatches);
        AddRow(rows, "FastTrustedCount", UnavailableBaseline(), newMetrics.FastTrustedCount);
        AddRow(rows, "FastAbstainedCount", UnavailableBaseline(), newMetrics.FastAbstainedCount);
        AddRow(rows, "FastConflictCount", UnavailableBaseline(), newMetrics.FastConflictCount);
        AddRow(rows, "FalseFastTrustCount", UnavailableBaseline(), newMetrics.FalseFastTrustCount);
        AddRow(rows, "TransitionReconciliationFailures", UnavailableBaseline(), newMetrics.TransitionReconciliationFailures);
        AddRow(rows, "CoverageExhaustionFailures", UnavailableBaseline(), newMetrics.CoverageExhaustionFailures);
        AddRow(rows, "StaleOccurrenceBoundsRejections", UnavailableBaseline(), newMetrics.StaleOccurrenceBoundsRejections);

        return new Phase26BaselineComparison(
            baseline,
            newMetrics,
            new Phase26CompletedRunsAnswer(baseline.CompletedRuns, newMetrics.CompletedRuns),
            rows.ToImmutable());
    }

    private static void AddRow(
        ImmutableArray<Phase26MetricComparison>.Builder rows,
        string name,
        ResultField<int> baseline,
        ResultField<int> newValue)
        => rows.Add(new Phase26MetricComparison(name, baseline, newValue));

    private static ResultField<int> UnavailableBaseline()
        => ResultField<int>.Unavailable(
            "No per-metric old-baseline count is quantified by the frozen Phase-2.6 final report; it documents the blocker distribution and terminal-set, not this count.");
}
