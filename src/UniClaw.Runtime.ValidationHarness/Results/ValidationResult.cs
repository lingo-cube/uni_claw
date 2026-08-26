using System.Collections.Immutable;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.ValidationHarness.Results;

/// <summary>
/// Truth-source classification carried by every <see cref="ValidationResult"/>
/// field (design D4 — the Result schema is an aggregation, not a new contract;
/// mirroring <c>RunSnapshot</c> field semantics: direct projection / derived
/// read model / unavailable). <see cref="Unavailable"/> is recorded EXPLICITLY
/// with its truth-source statement whenever a requested fact has no truthful
/// source on the current surface — never guessed, never invented.
/// </summary>
public enum ResultFieldClassification
{
    /// <summary>Copied verbatim from an existing Runtime public surface
    /// (admission response, frozen read-only wire surface, or Tier-A
    /// in-process Agent read model).</summary>
    DirectProjection,

    /// <summary>Derived read-model fact (ledger projection, event-stream
    /// ordering, terminal reason) — visibly identified as derived, never
    /// presented as canonical Kernel state.</summary>
    DerivedReadModel,

    /// <summary>Absent — no truthful source on the current surface; recorded
    /// explicitly with its classification and truth-source statement.</summary>
    Unavailable,
}

/// <summary>Non-generic view over one classified result field (walking helper
/// for traceability assertions and report rendering).</summary>
public interface IClassifiedField
{
    /// <summary>Truth-source classification of the field.</summary>
    ResultFieldClassification Classification { get; }

    /// <summary>Boxed raw value (null when unavailable).</summary>
    object? RawValue { get; }

    /// <summary>Human-auditable source statement.</summary>
    string TruthSource { get; }

    /// <summary>True for partial evidence (e.g. goal evidence without an
    /// observation anchor) — the field is honestly partial, not invented.</summary>
    bool IsPartial { get; }
}

/// <summary>
/// One classified result field. Value is null when the field is
/// <see cref="ResultFieldClassification.Unavailable"/> (explicit, never
/// invented). <see cref="IsPartial"/> marks partial evidence.
/// </summary>
public sealed record ResultField<T> : IClassifiedField
{
    /// <summary>Field value; null when unavailable.</summary>
    public T? Value { get; init; }

    /// <inheritdoc />
    public ResultFieldClassification Classification { get; init; }

    /// <inheritdoc />
    public string TruthSource { get; init; } = string.Empty;

    /// <inheritdoc />
    public bool IsPartial { get; init; }

    /// <inheritdoc />
    /// <remarks>Truthfulness of the non-null contract: a default
    /// <c>ImmutableArray&lt;T&gt;</c> (value-type Unavailable field) boxes to a
    /// non-null object; it is reported as null so the "populated ⇒ classified"
    /// walk invariant holds for value-type fields.</remarks>
    object? IClassifiedField.RawValue
        => Classification == ResultFieldClassification.Unavailable && IsDefaultValueTypeValue(Value)
            ? null
            : Value;

    private static bool IsDefaultValueTypeValue(T? value)
        => typeof(T).IsValueType
           && value is not null
           && value.GetType().IsGenericType
           && value.GetType().GetGenericTypeDefinition() == typeof(System.Collections.Immutable.ImmutableArray<>);


    /// <summary>Creates a direct-projection field.</summary>
    public static ResultField<T> Direct(T? value, string truthSource)
        => new() { Value = value, Classification = ResultFieldClassification.DirectProjection, TruthSource = truthSource };

    /// <summary>Creates a derived read-model field.</summary>
    public static ResultField<T> Derived(T? value, string truthSource)
        => new() { Value = value, Classification = ResultFieldClassification.DerivedReadModel, TruthSource = truthSource };

    /// <summary>Creates an unavailable field (value null; classification + source recorded).</summary>
    public static ResultField<T> Unavailable(string truthSource)
        => new() { Classification = ResultFieldClassification.Unavailable, TruthSource = truthSource };

    /// <summary>Creates an unavailable field retaining partial evidence.</summary>
    public static ResultField<T> UnavailablePartial(T? value, string truthSource)
        => new() { Value = value, Classification = ResultFieldClassification.Unavailable, TruthSource = truthSource, IsPartial = true };
}

/// <summary>Admission facts aggregated from the <c>run.strategy.start</c>
/// admission exchange (design D4 — Admission section).</summary>
/// <param name="RunId">DriverHost-owned run identity from the admission receipt.</param>
/// <param name="StrategyId">Strategy identity of the transported, admitted directive.</param>
/// <param name="Accepted">Admission outcome flag.</param>
/// <param name="RejectionCode">Deterministic admission rejection code; null when accepted.</param>
/// <param name="RejectionReason">Rejection reason; null when accepted.</param>
/// <param name="DeclaredMaximumDepth">Declared exploration depth of the admitted directive.</param>
public sealed record AdmissionSection(
    ResultField<string> RunId,
    ResultField<string> StrategyId,
    ResultField<bool> Accepted,
    ResultField<string?> RejectionCode,
    ResultField<string?> RejectionReason,
    ResultField<int> DeclaredMaximumDepth);

/// <summary>Lifecycle section (design D4): the projected event stream read
/// through the frozen read surface (each event carries the audited A/B source
/// classification).</summary>
public sealed record LifecycleSection(ResultField<ImmutableArray<SurfaceRuntimeEvent>> Events);

/// <summary>
/// Snapshot section (design D4): a harness-local mirror of the classified
/// <c>RunSnapshot</c> fields, preserving the frozen per-field semantics
/// (direct public projection / derived read model / unavailable).
/// </summary>
public sealed record SnapshotSection(
    ResultField<string> RunId,
    ResultField<RunState> RunState,
    ResultField<string?> CurrentSemanticPage,
    ResultField<Trap?> ActiveTrap,
    ResultField<GoalSummary?> CurrentGoal,
    ResultField<DecisionSummary?> LastDecision,
    ResultField<ActionSummary?> LastAction,
    ResultField<RecoverySummary?> RecoveryState,
    ResultField<GoalEvidenceSummary?> LatestGoalEvidence,
    ResultField<long?> CurrentObservationSequence,
    ResultField<string?> CurrentContainerSummary,
    ResultField<string?> BindingsSummary,
    ResultField<string?> StateBeliefsSummary,
    ResultField<ImmutableArray<string>> Diagnostics);

/// <summary>Trap section (design D4): the classified <c>run.trap.get</c> read.</summary>
public sealed record TrapSection(
    ResultField<bool> Found,
    ResultField<Trap?> Trap,
    ResultField<string?> Diagnostic);

/// <summary>One <c>evidence.get</c> resolution outcome kept in the Result.
/// Unresolvable refs are recorded (never dropped, never fabricated).</summary>
public sealed record ValidationEvidenceEntry(
    EvidenceRef RequestedRef,
    ResultField<bool> Resolved,
    ResultField<EvidenceRef?> CanonicalRef,
    ResultField<string?> Diagnostic);

/// <summary>Evidence section: every requested ref resolved through
/// <c>evidence.get</c>.</summary>
public sealed record EvidenceSection(ResultField<ImmutableArray<ValidationEvidenceEntry>> Entries);

/// <summary>One per-scope ledger accounting row (five counts, direct copy of
/// the read model's per-scope row — no sums invented).</summary>
public sealed record CoverageScopeCounts(
    string ScopeIdentity,
    int Discovered,
    int Visited,
    int Pending,
    int Unresolved,
    int UnknownFrontier);

/// <summary>
/// Tier-scoped coverage section (design D4 / D3): Tier A MAY attest the full
/// <c>ExplorationLedgerView</c> through the in-process Agent public read model
/// (five per-scope counts + stable digest); wire tiers record the ledger as
/// explicitly <see cref="ResultFieldClassification.Unavailable"/> — never
/// guessed, never approximated.
/// </summary>
public sealed record CoverageSection(
    ResultField<string> Availability,
    ResultField<ExplorationLedgerView?> Ledger,
    ResultField<ImmutableArray<CoverageScopeCounts>> Scopes,
    ResultField<string?> LedgerDigest);

/// <summary>Terminal section: the terminal reason and its backing ordering
/// fact (GoalEvidenceProduced before RunCompleted, S1).</summary>
public sealed record TerminalSection(
    ResultField<RunState> TerminalState,
    ResultField<string?> TerminalReason,
    ResultField<bool?> GoalEvidenceBacksCompletion);

/// <summary>
/// Boundary section — a typed EMPTY placeholder in this WorkItem: the four
/// boundary proofs (zero mutating calls / no injected actions / A/B event
/// vocabulary / evidence provenance) are derived and attached by the boundary
/// verifier increment. Kept as a distinct typed section so the Result schema
/// is stable; every scenario result carries this placeholder until the
/// verifier fills it.
/// </summary>
public sealed record BoundarySection
{
    private BoundarySection()
    {
    }

    /// <summary>The single typed-empty boundary placeholder.</summary>
    public static BoundarySection Placeholder { get; } = new();

    /// <summary>Status marker: boundary proof not yet attached.</summary>
    public string Status => "PENDING_BOUNDARY_VERIFIER";
}

/// <summary>
/// Harness-local aggregation, NOT a new runtime contract (design D4): eight
/// sections Admission / Lifecycle / Snapshot / Trap / Evidence / Coverage /
/// Terminal / Boundary. Every field carries its truth-source classification;
/// the collector copies runtime facts only.
/// </summary>
public sealed record ValidationResult(
    AdmissionSection Admission,
    LifecycleSection Lifecycle,
    SnapshotSection Snapshot,
    TrapSection Trap,
    EvidenceSection Evidence,
    CoverageSection Coverage,
    TerminalSection Terminal,
    BoundarySection Boundary)
{
    /// <summary>
    /// Enumerates every classified field of the result (walking helper for
    /// traceability assertions — G3 "every Result field traces to surfaces").
    /// </summary>
    public IEnumerable<IClassifiedField> EnumerateClassifiedFields()
    {
        // Admission
        yield return Admission.RunId;
        yield return Admission.StrategyId;
        yield return Admission.Accepted;
        yield return Admission.RejectionCode;
        yield return Admission.RejectionReason;
        yield return Admission.DeclaredMaximumDepth;

        // Lifecycle
        yield return Lifecycle.Events;

        // Snapshot
        yield return Snapshot.RunId;
        yield return Snapshot.RunState;
        yield return Snapshot.CurrentSemanticPage;
        yield return Snapshot.ActiveTrap;
        yield return Snapshot.CurrentGoal;
        yield return Snapshot.LastDecision;
        yield return Snapshot.LastAction;
        yield return Snapshot.RecoveryState;
        yield return Snapshot.LatestGoalEvidence;
        yield return Snapshot.CurrentObservationSequence;
        yield return Snapshot.CurrentContainerSummary;
        yield return Snapshot.BindingsSummary;
        yield return Snapshot.StateBeliefsSummary;
        yield return Snapshot.Diagnostics;

        // Trap
        yield return Trap.Found;
        yield return Trap.Trap;
        yield return Trap.Diagnostic;

        // Evidence
        yield return Evidence.Entries;
        if (Evidence.Entries.Value is { } evidenceEntries)
        {
            foreach (var entry in evidenceEntries)
            {
                yield return entry.Resolved;
                yield return entry.CanonicalRef;
                yield return entry.Diagnostic;
            }
        }

        // Coverage
        yield return Coverage.Availability;
        yield return Coverage.Ledger;
        yield return Coverage.Scopes;
        yield return Coverage.LedgerDigest;

        // Terminal
        yield return Terminal.TerminalState;
        yield return Terminal.TerminalReason;
        yield return Terminal.GoalEvidenceBacksCompletion;
    }
}