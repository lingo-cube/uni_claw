using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Assurance;

public sealed partial class RuntimeAssurance
{
    private static bool VerifyOverlayDismissal(PostActionEffectVerificationInput input)
    {
        if (input.Overlay is not { BeforePage: { ContainerId: not null } before, AfterPage: { ContainerId: not null } after,
                BeforeOverlay: { } beforeRecord, AfterOverlay: { Context: ObservationContext.PostActionEffectFlow } afterRecord,
                Receipt: { Outcome: DispatchOutcome.DeliveryCompleted } receipt, Binding: { } binding, BoundTarget: { } target } facts
            || facts.BeforeNeed is not null || facts.AfterNeed is not null || facts.Conflicts.Count > 0
            || input.Target.EffectClass is not ("tap" or "click") || input.Target.DesiredState is not null
            || binding.BindingId != receipt.BindingId || binding.IntentId != receipt.IntentId
            || binding.TargetSubject != receipt.TargetSubject || binding.RevisionId != receipt.RevisionId
            || binding.RevisionNumber != receipt.RevisionNumber
            || target.OwningContainerId != before.ContainerId
            || binding.TargetOccurrenceId != target.OccurrenceId || binding.EffectClass != input.Target.EffectClass
            || target.Role != input.Target.Role || target.SemanticDescriptor != input.Target.SemanticDescriptor
            || !OverlayObservationCodec.TryRead(beforeRecord.Claim.Value, out var prior)
            || !OverlayObservationCodec.TryRead(afterRecord.Claim.Value, out var current)
            || prior is not { State: "present", WindowEvidence: { } original }
            || current is not { State: "absent", WindowEvidence: { } restored }) return false;
        var pre = before.Capture;
        var post = after.Capture;
        bool Matches(OverlayObservation observation, PageCaptureObservation capture, EvidenceRecord record) =>
            capture.RequiresOverlayEvidence && observation.CaptureId == capture.CaptureId
                && observation.ObservationCycleId == capture.ObservationCycleId
                && observation.SessionCorrelation == capture.SessionCorrelation
                && observation.CaptureTime == capture.CaptureTime && record.Provenance.CaptureTime == capture.CaptureTime;
        if (!Matches(prior, pre, beforeRecord) || !Matches(current, post, afterRecord)
            || original.VisibleWindowCoverage.Completeness != CoverageCompleteness.CompleteWithinDeclaredSurface
            || restored.VisibleWindowCoverage.Completeness != CoverageCompleteness.CompleteWithinDeclaredSurface
            || string.IsNullOrWhiteSpace(pre.DeviceId) || pre.DeviceId != post.DeviceId
            || pre.SessionCorrelation != post.SessionCorrelation || pre.CaptureId == post.CaptureId
            || pre.CaptureTime > receipt.DispatchedAt
            || post.CaptureTime <= receipt.DispatchedAt || post.CaptureTime <= pre.CaptureTime
            || original.BaseWindowId != restored.BaseWindowId || original.ActivityId != restored.ActivityId
            || original.DisplayId != restored.DisplayId || restored.OverlayWindowId is not null) return false;
        // The bound node must be inside this capture's observed dialog subtree.
        // A same-label control in the base page is not evidence of a dismissal.
        var member = facts.TargetSources.Any(record => record.Provenance.Hierarchy is { } h
            && h.CaptureId == pre.CaptureId && h.ObservationCycleId == pre.ObservationCycleId
            && h.SessionCorrelation == pre.SessionCorrelation && h.DeviceId == pre.DeviceId
            && h.CaptureTimestamp == pre.CaptureTime && record.Provenance.CaptureTime == pre.CaptureTime
            && h.CoverageCompleteness == CoverageCompleteness.CompleteWithinDeclaredSurface
            && original.OverlayNodeIndices.Contains(h.NodeLocalIndex));
        bool AcceptedHere(string id) => input.ProcessedObservations.Any(result =>
            result.Admission.Decision == AdmissionDecision.Accepted && result.Admission.EvidenceId == id
                && result.ResultingRevision is not null);
        return member && AcceptedHere(after.EvidenceId) && AcceptedHere(afterRecord.EvidenceId);
    }
}
