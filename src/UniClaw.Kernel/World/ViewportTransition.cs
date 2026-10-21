using UniClaw.Kernel.Perception;

namespace UniClaw.Kernel.World;

/// <summary>Evidence-backed correspondence used by one viewport registration;
/// this is not a LogicalItem identity or an executable target.</summary>
public sealed record ViewportCorrespondence(
    string SourceOccurrenceId, string TargetOccurrenceId, bool Stationary);

/// <summary>A verified operation connects two observations of the same page.
/// Offset maps target viewport coordinates into the source viewport's content
/// frame. A null offset is explicitly unknown, even when the operation verified.</summary>
public sealed record ViewportTransition(
    string ContainerId,
    string SourceRevisionId,
    string TargetRevisionId,
    string OperationReceiptId,
    string OperationBindingId,
    string SourceTargetOccurrenceId,
    string EffectClass,
    DateTimeOffset DispatchedAt,
    string Scope,
    string VerificationSourceRef,
    IReadOnlyList<string> SourceEvidenceIds,
    IReadOnlyList<string> TargetEvidenceIds,
    string AlignmentStatus,
    double? OffsetX,
    double? OffsetY,
    IReadOnlyList<ViewportCorrespondence> Correspondences,
    CoordinateSpace? Space,
    SpatialLocator? SourceRegion,
    SpatialLocator? TargetRegion);

// Kernel-only ingress: actual dispatch/verification references, never a plan or
// a proposed displacement. World computes displacement from its own revisions.
internal sealed record ObservedViewportTransition(
    string SourceRevisionId, string TargetRevisionId, string ContainerId,
    string OperationReceiptId, string OperationBindingId,
    string SourceTargetOccurrenceId, string EffectClass,
    DateTimeOffset DispatchedAt, string Scope, string VerificationSourceRef);
