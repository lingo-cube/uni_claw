using System.Collections.Immutable;

namespace UniClaw.Runtime.Model;

/// <summary>
/// Three-way equivalence between two navigation source occurrences within the
/// same Container exploration.
/// </summary>
public enum SourceEquivalenceKind
{
    /// <summary>Two occurrences were proven to represent the same source.</summary>
    SameSource,
    /// <summary>Two occurrences were proven to represent different sources.</summary>
    DifferentSource,
    /// <summary>Available evidence could not determine equivalence.</summary>
    Unknown,
}

/// <summary>
/// Agent-run-local immutable evidence that two occurrences are proven same,
/// different, or unresolved as one logical navigation source.
/// </summary>
/// <param name="FirstOccurrenceIdentity">First occurrence identity.</param>
/// <param name="SecondOccurrenceIdentity">Second occurrence identity.</param>
/// <param name="Kind">Equivalence result.</param>
/// <param name="Reason">Deterministic reason.</param>
public sealed record SourceEquivalenceEvidence(
    string FirstOccurrenceIdentity,
    string SecondOccurrenceIdentity,
    SourceEquivalenceKind Kind,
    string Reason);

/// <summary>
/// Boundary-row tolerance record: a viewport-truncated row (top or bottom of a
/// scrolling window) that was skipped to admit a unique suffix-prefix overlap
/// after strict matching failed. Skipped rows are never added to the union and
/// never participate in signature comparison; they are captured fully in a later
/// scroll frame. This record makes the skip explicit (never silent).
/// </summary>
/// <param name="WindowSequence">Sequence number of the window whose row was skipped.</param>
/// <param name="SkippedIndex">Original (pre-trim) index of the skipped row within that window.</param>
/// <param name="SkippedSignature">Exact structured signature of the skipped row.</param>
/// <param name="Reason">Deterministic skip reason; always "boundary-truncated".</param>
public sealed record BoundaryTruncationRecord(
    long WindowSequence,
    int SkippedIndex,
    string SkippedSignature,
    string Reason)
{
    /// <summary>Canonical reason for a boundary-truncation skip.</summary>
    public const string BoundaryTruncatedReason = "boundary-truncated";
}

/// <summary>
/// Anchor-merge record (third-tier fallback): evidence that a window was merged
/// into the union via NEIGHBOR-ANCHORED insertion rather than suffix-prefix
/// overlap. Anchors are window rows that exactly (Ordinal) match rows already in
/// the union; non-anchor rows are inserted between their nearest surrounding
/// anchors. Existing union elements are never deleted or reordered. At least one
/// anchor is required; zero anchors keep the result fail-closed (Unresolved).
/// </summary>
/// <param name="WindowSequence">Sequence number of the merged window.</param>
/// <param name="AnchorCount">Number of anchor rows found in this window.</param>
/// <param name="InsertedSignatures">Exact signatures of the rows inserted by this merge, in insertion order.</param>
/// <param name="Reason">Deterministic merge reason; always "anchor-merge".</param>
public sealed record AnchorMergeRecord(
    long WindowSequence,
    int AnchorCount,
    ImmutableArray<string> InsertedSignatures,
    string Reason)
{
    /// <summary>Canonical reason for an anchor-merge fallback.</summary>
    public const string AnchorMergeReason = "anchor-merge";
}
