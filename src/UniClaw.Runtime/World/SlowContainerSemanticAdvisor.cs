using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.World;

/// <summary>Controls how a Slow semantic assessment is consumed.</summary>
public enum SlowContainerSemanticMode
{
    /// <summary>Do not invoke a Slow advisor.</summary>
    Disabled,
    /// <summary>Invoke the advisor and retain its result for evaluation only.</summary>
    Shadow,
    /// <summary>Invoke the advisor and expose its current advisory availability.</summary>
    AsyncAdvisory,
}

/// <summary>Bounded interpretation produced by a Slow semantic assessment.</summary>
public enum SlowContainerSemanticAssessmentKind
{
    /// <summary>The Slow interpretation supports the supplied working meaning.</summary>
    Confirm,
    /// <summary>The Slow interpretation disputes the supplied working meaning.</summary>
    Challenge,
    /// <summary>The Slow interpretation supplies a corrected working meaning.</summary>
    Correct,
    /// <summary>The Slow interpretation is insufficient for a stronger meaning.</summary>
    Insufficient,
}

/// <summary>Scene interpretation supplied as evidence by a Slow advisor.</summary>
public enum SlowContainerSceneKind
{
    /// <summary>No scene classification is available.</summary>
    Unknown,
    /// <summary>The scene is consistent with an ordinary Container observation.</summary>
    Normal,
    /// <summary>The scene is likely an advertisement.</summary>
    Advertisement,
    /// <summary>The scene is transient and not settled.</summary>
    Transient,
    /// <summary>The scene is a loading view.</summary>
    Loading,
    /// <summary>The scene is an overlay over another view.</summary>
    Overlay,
    /// <summary>The scene is unrelated to the working interpretation.</summary>
    Unrelated,
    /// <summary>The scene is outside the expected execution path.</summary>
    OffPath,
    /// <summary>The scene is a different child than the expected child.</summary>
    WrongChild,
}

/// <summary>Bounded non-authoritative disposition suggested by an assessment.</summary>
public enum SlowContainerSemanticDisposition
{
    /// <summary>No follow-up disposition is suggested.</summary>
    None,
    /// <summary>Retain the assessment as evidence for evaluation.</summary>
    RetainEvidence,
    /// <summary>Ask the owning layer to obtain or assess fresh evidence.</summary>
    ReassessFreshEvidence,
}

/// <summary>Slow assessment of whether the fresh evidence is useful.</summary>
public enum SlowContainerEvidenceUsefulness
{
    /// <summary>Usefulness was not determined.</summary>
    Unknown,
    /// <summary>The evidence is useful for bounded semantic evaluation.</summary>
    Useful,
    /// <summary>The evidence is not useful for the proposed interpretation.</summary>
    NotUseful,
}

/// <summary>Derived availability of a Slow assessment consumption.</summary>
public enum SlowContainerSemanticAvailability
{
    /// <summary>Slow consumption was disabled.</summary>
    Disabled,
    /// <summary>A Slow result is not available because the advisor is absent.</summary>
    Unavailable,
    /// <summary>A matching Slow result is available for the current revision.</summary>
    Available,
    /// <summary>A matching Slow result is retained but belongs to an older revision.</summary>
    Stale,
    /// <summary>The returned result did not match the requested evidence binding.</summary>
    Rejected,
}

/// <summary>
/// Immutable, exact evidence binding for one Slow semantic assessment request.
/// References are correlation data, not identity or decision authority.
/// </summary>
public sealed record SlowContainerSemanticRequest
{
    /// <summary>Creates an exact revision-bound Slow request.</summary>
    public SlowContainerSemanticRequest(
        string observationRef,
        SemanticEvidenceRevision evidenceRevision,
        ContainerNodeRef? nodeRef = null,
        ContainerNodeRef? sourceNodeRef = null,
        string? triggerOccurrenceRef = null,
        TransitionOccurrenceRef? transitionOccurrenceRef = null,
        FastContainerAssessment? fastAssessment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(observationRef);
        if (triggerOccurrenceRef is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(triggerOccurrenceRef);

        ObservationRef = observationRef;
        EvidenceRevision = evidenceRevision;
        NodeRef = nodeRef;
        SourceNodeRef = sourceNodeRef;
        TriggerOccurrenceRef = string.IsNullOrWhiteSpace(triggerOccurrenceRef)
            ? null
            : triggerOccurrenceRef;
        TransitionOccurrenceRef = transitionOccurrenceRef;
        if (fastAssessment is { } fast
            && (fast.EvidenceRevision != evidenceRevision
                || fast.CandidateNodeRef != nodeRef
                || fast.CurrentNodeRef != sourceNodeRef))
        {
            throw new ArgumentException(
                "Fast assessment is not bound to the request evidence and nodes.",
                nameof(fastAssessment));
        }

        FastAssessment = fastAssessment;
    }

    /// <summary>Gets the exact fresh observation reference.</summary>
    public string ObservationRef { get; }
    /// <summary>Gets the exact accepted semantic evidence revision.</summary>
    public SemanticEvidenceRevision EvidenceRevision { get; }
    /// <summary>Gets the working destination node reference, when applicable.</summary>
    public ContainerNodeRef? NodeRef { get; }
    /// <summary>Gets the source node reference, when applicable.</summary>
    public ContainerNodeRef? SourceNodeRef { get; }
    /// <summary>Gets the per-transition trigger occurrence reference, when applicable.</summary>
    public string? TriggerOccurrenceRef { get; }
    /// <summary>Gets the transition occurrence reference, when applicable.</summary>
    public TransitionOccurrenceRef? TransitionOccurrenceRef { get; }
    /// <summary>Gets the Fast assessment used only to derive conflict visibility.</summary>
    public FastContainerAssessment? FastAssessment { get; }
}

/// <summary>
/// Immutable Slow semantic evidence. It is retained as an assessment and never
/// represents a world fact or a control decision.
/// </summary>
public sealed record SlowContainerSemanticAssessment
{
    /// <summary>Creates one exact evidence-bound Slow assessment.</summary>
    public SlowContainerSemanticAssessment(
        string observationRef,
        SemanticEvidenceRevision evidenceRevision,
        SlowContainerSemanticAssessmentKind kind,
        SlowContainerSceneKind scene,
        ContainerNodeRef? nodeRef = null,
        ContainerNodeRef? sourceNodeRef = null,
        string? triggerOccurrenceRef = null,
        TransitionOccurrenceRef? transitionOccurrenceRef = null,
        string? correctedIdentityCandidate = null,
        SlowContainerSemanticDisposition suggestedDisposition = SlowContainerSemanticDisposition.None,
        string? details = null,
        string? containerSemantic = null,
        string? triggerSemantic = null,
        string? relationSemantic = null,
        SlowContainerEvidenceUsefulness evidenceUsefulness = SlowContainerEvidenceUsefulness.Unknown,
        bool hasMismatch = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(observationRef);
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (!Enum.IsDefined(scene))
            throw new ArgumentOutOfRangeException(nameof(scene));
        if (!Enum.IsDefined(suggestedDisposition))
            throw new ArgumentOutOfRangeException(nameof(suggestedDisposition));
        if (!Enum.IsDefined(evidenceUsefulness))
            throw new ArgumentOutOfRangeException(nameof(evidenceUsefulness));
        if (triggerOccurrenceRef is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(triggerOccurrenceRef);

        ObservationRef = observationRef;
        EvidenceRevision = evidenceRevision;
        Kind = kind;
        Scene = scene;
        NodeRef = nodeRef;
        SourceNodeRef = sourceNodeRef;
        TriggerOccurrenceRef = string.IsNullOrWhiteSpace(triggerOccurrenceRef)
            ? null
            : triggerOccurrenceRef;
        TransitionOccurrenceRef = transitionOccurrenceRef;
        CorrectedIdentityCandidate = string.IsNullOrWhiteSpace(correctedIdentityCandidate)
            ? null
            : correctedIdentityCandidate;
        SuggestedDisposition = suggestedDisposition;
        Details = string.IsNullOrWhiteSpace(details) ? null : details;
        ContainerSemantic = string.IsNullOrWhiteSpace(containerSemantic) ? null : containerSemantic;
        TriggerSemantic = string.IsNullOrWhiteSpace(triggerSemantic) ? null : triggerSemantic;
        RelationSemantic = string.IsNullOrWhiteSpace(relationSemantic) ? null : relationSemantic;
        EvidenceUsefulness = evidenceUsefulness;
        HasMismatch = hasMismatch || kind is SlowContainerSemanticAssessmentKind.Challenge
            or SlowContainerSemanticAssessmentKind.Correct;
    }

    /// <summary>Gets the exact fresh observation reference.</summary>
    public string ObservationRef { get; }
    /// <summary>Gets the exact accepted semantic evidence revision.</summary>
    public SemanticEvidenceRevision EvidenceRevision { get; }
    /// <summary>Gets the Slow interpretation kind.</summary>
    public SlowContainerSemanticAssessmentKind Kind { get; }
    /// <summary>Gets the Slow scene interpretation.</summary>
    public SlowContainerSceneKind Scene { get; }
    /// <summary>Gets the working destination node reference, when applicable.</summary>
    public ContainerNodeRef? NodeRef { get; }
    /// <summary>Gets the source node reference, when applicable.</summary>
    public ContainerNodeRef? SourceNodeRef { get; }
    /// <summary>Gets the per-transition trigger occurrence reference, when applicable.</summary>
    public string? TriggerOccurrenceRef { get; }
    /// <summary>Gets the transition occurrence reference, when applicable.</summary>
    public TransitionOccurrenceRef? TransitionOccurrenceRef { get; }
    /// <summary>Gets a corrected identity candidate, when Slow supplied one.</summary>
    public string? CorrectedIdentityCandidate { get; }
    /// <summary>Gets the bounded suggested disposition.</summary>
    public SlowContainerSemanticDisposition SuggestedDisposition { get; }
    /// <summary>Gets optional human-readable assessment context.</summary>
    public string? Details { get; }
    /// <summary>Gets optional Slow interpretation of the Container semantics.</summary>
    public string? ContainerSemantic { get; }
    /// <summary>Gets optional Slow interpretation of the trigger semantics.</summary>
    public string? TriggerSemantic { get; }
    /// <summary>Gets optional Slow interpretation of the relation semantics.</summary>
    public string? RelationSemantic { get; }
    /// <summary>Gets the derived evidence usefulness assessment.</summary>
    public SlowContainerEvidenceUsefulness EvidenceUsefulness { get; }
    /// <summary>Gets whether the assessment observes a semantic mismatch.</summary>
    public bool HasMismatch { get; }
}

/// <summary>
/// Slow semantic advisor port. Implementations provide assessment evidence only;
/// this contract contains no execution or state ownership surface.
/// NEW_SYMBOL_JUSTIFICATION: ISemanticProvider is a Fast candidate port whose
/// frozen result and latency boundary do not express Slow scene/correction,
/// exact V2 correlation, or Disabled/Shadow/AsyncAdvisory consumption. Extending
/// it would mix provider lifecycle and result responsibilities. This separate
/// port has an independent Shadow fake buyer and keeps the existing port intact.
/// </summary>
public interface ISlowContainerSemanticAdvisor
{
    /// <summary>Asynchronously assesses one exact evidence binding.</summary>
    /// <param name="request">Immutable observation and transition correlation.</param>
    /// <param name="cancellationToken">Cancellation requested by the caller.</param>
    /// <returns>Immutable Slow assessment evidence for the same binding.</returns>
    Task<SlowContainerSemanticAssessment> AssessAsync(
        SlowContainerSemanticRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Immutable result of asynchronous Slow acquisition. The raw assessment is
/// retained even when a later projection rejects its binding or freshness.
/// </summary>
public sealed record SlowContainerSemanticInvocation
{
    /// <summary>Creates one immutable acquisition result.</summary>
    public SlowContainerSemanticInvocation(
        SlowContainerSemanticMode mode,
        SlowContainerSemanticRequest request,
        SlowContainerSemanticAssessment? rawAssessment,
        bool advisorInvoked,
        string? acquisitionIssue = null)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        ArgumentNullException.ThrowIfNull(request);
        Mode = mode;
        Request = request;
        RawAssessment = rawAssessment;
        AdvisorInvoked = advisorInvoked;
        AcquisitionIssue = string.IsNullOrWhiteSpace(acquisitionIssue) ? null : acquisitionIssue;
    }

    /// <summary>Gets the mode used for acquisition.</summary>
    public SlowContainerSemanticMode Mode { get; }
    /// <summary>Gets the exact request used for acquisition.</summary>
    public SlowContainerSemanticRequest Request { get; }
    /// <summary>Gets the raw advisor result, including a rejected result.</summary>
    public SlowContainerSemanticAssessment? RawAssessment { get; }
    /// <summary>Gets whether the advisor port was invoked.</summary>
    public bool AdvisorInvoked { get; }
    /// <summary>Gets an acquisition failure reason, when applicable.</summary>
    public string? AcquisitionIssue { get; }
}

/// <summary>
/// Immutable projection of one Slow consumption. It records no mutable current
/// result and exposes no execution effect; stale matching evidence remains
/// readable for evaluation while not being current.
/// </summary>
public sealed record SlowContainerSemanticConsumption
{
    /// <summary>Creates one immutable consumption projection.</summary>
    public SlowContainerSemanticConsumption(
        SlowContainerSemanticMode mode,
        SlowContainerSemanticAvailability availability,
        SlowContainerSemanticAssessment? assessment,
        bool isCurrent,
        bool isStale,
        bool conflictsWithFast,
        string? rejectionReason = null)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (!Enum.IsDefined(availability))
            throw new ArgumentOutOfRangeException(nameof(availability));

        Mode = mode;
        Availability = availability;
        Assessment = assessment;
        IsCurrent = isCurrent;
        IsStale = isStale;
        ConflictsWithFast = conflictsWithFast;
        RejectionReason = string.IsNullOrWhiteSpace(rejectionReason) ? null : rejectionReason;
    }

    /// <summary>Gets the consumption mode.</summary>
    public SlowContainerSemanticMode Mode { get; }
    /// <summary>Gets the derived assessment availability.</summary>
    public SlowContainerSemanticAvailability Availability { get; }
    /// <summary>Gets the original assessment, including when it is stale.</summary>
    public SlowContainerSemanticAssessment? Assessment { get; }
    /// <summary>Gets whether the assessment matches the current revision.</summary>
    public bool IsCurrent { get; }
    /// <summary>Gets whether the matching assessment belongs to an older revision.</summary>
    public bool IsStale { get; }
    /// <summary>Gets whether the Slow interpretation challenges a Fast interpretation.</summary>
    public bool ConflictsWithFast { get; }
    /// <summary>Gets the explicit fail-closed reason, when unavailable or rejected.</summary>
    public string? RejectionReason { get; }
    /// <summary>Gets whether this projection is advisory-only and has no runtime effect.</summary>
    public bool IsAdvisoryOnly => true;
    /// <summary>Gets whether this projection has any runtime behavior effect.</summary>
    public bool HasRuntimeEffect => false;
}

/// <summary>Pure consumption functions for the Slow advisor contract.</summary>
public static class SlowContainerSemanticConsumer
{
    /// <summary>
    /// Acquires one raw advisor result. Disabled mode does not invoke an
    /// advisor. Freshness is intentionally not supplied here: the caller must
    /// provide the current revision later to <see cref="Project"/>.
    /// </summary>
    /// <param name="mode">Disabled, Shadow, or AsyncAdvisory.</param>
    /// <param name="advisor">Optional provider-neutral advisor port.</param>
    /// <param name="request">Exact evidence binding for this request.</param>
    /// <param name="cancellationToken">Cancellation requested by the caller.</param>
    /// <returns>An immutable raw acquisition value.</returns>
    public static async Task<SlowContainerSemanticInvocation> AcquireAsync(
        SlowContainerSemanticMode mode,
        ISlowContainerSemanticAdvisor? advisor,
        SlowContainerSemanticRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));

        if (mode == SlowContainerSemanticMode.Disabled)
        {
            return new SlowContainerSemanticInvocation(
                mode,
                request,
                null,
                false,
                "Slow consumption is disabled");
        }

        if (advisor is null)
        {
            return new SlowContainerSemanticInvocation(
                mode,
                request,
                null,
                false,
                "Slow advisor is unavailable");
        }

        var assessment = await advisor.AssessAsync(request, cancellationToken).ConfigureAwait(false);
        return new SlowContainerSemanticInvocation(
            mode,
            request,
            assessment,
            true);
    }

    /// <summary>Alias for raw asynchronous acquisition.</summary>
    public static Task<SlowContainerSemanticInvocation> AssessAsync(
        SlowContainerSemanticMode mode,
        ISlowContainerSemanticAdvisor? advisor,
        SlowContainerSemanticRequest request,
        CancellationToken cancellationToken = default)
        => AcquireAsync(mode, advisor, request, cancellationToken);

    /// <summary>
    /// Projects an acquired raw result against the current revision observed at
    /// consumption time. Mismatched and future results remain readable as raw
    /// evidence but are never current.
    /// </summary>
    /// <param name="invocation">Immutable raw acquisition result.</param>
    /// <param name="currentEvidenceRevision">Current accepted revision now.</param>
    /// <returns>An immutable advisory-only projection.</returns>
    public static SlowContainerSemanticConsumption Project(
        SlowContainerSemanticInvocation invocation,
        SemanticEvidenceRevision currentEvidenceRevision)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var mode = invocation.Mode;
        var request = invocation.Request;
        var assessment = invocation.RawAssessment;

        if (mode == SlowContainerSemanticMode.Disabled)
        {
            return new SlowContainerSemanticConsumption(
                mode,
                SlowContainerSemanticAvailability.Disabled,
                null,
                false,
                false,
                false);
        }

        if (!invocation.AdvisorInvoked && assessment is null)
        {
            return new SlowContainerSemanticConsumption(
                mode,
                SlowContainerSemanticAvailability.Unavailable,
                null,
                false,
                false,
                false,
                invocation.AcquisitionIssue);
        }

        if (assessment is null)
        {
            return new SlowContainerSemanticConsumption(
                mode,
                SlowContainerSemanticAvailability.Rejected,
                null,
                false,
                false,
                false,
                "Slow advisor returned no assessment");
        }

        if (!Matches(request, assessment))
        {
            return new SlowContainerSemanticConsumption(
                mode,
                SlowContainerSemanticAvailability.Rejected,
                assessment,
                false,
                false,
                false,
                "Slow result does not match the requested evidence binding");
        }

        if (assessment.EvidenceRevision.Value > currentEvidenceRevision.Value)
        {
            return new SlowContainerSemanticConsumption(
                mode,
                SlowContainerSemanticAvailability.Rejected,
                assessment,
                false,
                false,
                false,
                "Slow result is newer than the accepted consumption revision");
        }

        var stale = assessment.EvidenceRevision.Value < currentEvidenceRevision.Value;
        return new SlowContainerSemanticConsumption(
            mode,
            stale
                ? SlowContainerSemanticAvailability.Stale
                : SlowContainerSemanticAvailability.Available,
            assessment,
            !stale,
            stale,
            IsFastConflict(request, assessment));
    }

    private static bool Matches(
        SlowContainerSemanticRequest request,
        SlowContainerSemanticAssessment assessment)
        => string.Equals(request.ObservationRef, assessment.ObservationRef, StringComparison.Ordinal)
            && request.EvidenceRevision == assessment.EvidenceRevision
            && request.NodeRef == assessment.NodeRef
            && request.SourceNodeRef == assessment.SourceNodeRef
            && string.Equals(request.TriggerOccurrenceRef, assessment.TriggerOccurrenceRef, StringComparison.Ordinal)
            && request.TransitionOccurrenceRef == assessment.TransitionOccurrenceRef;

    private static bool IsFastConflict(
        SlowContainerSemanticRequest request,
        SlowContainerSemanticAssessment assessment)
        => request.FastAssessment is { } fast
            && fast.EvidenceRevision == assessment.EvidenceRevision
            && assessment.Kind is SlowContainerSemanticAssessmentKind.Challenge
                or SlowContainerSemanticAssessmentKind.Correct;
}
