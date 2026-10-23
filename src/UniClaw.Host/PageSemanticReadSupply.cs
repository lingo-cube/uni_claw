using UniClaw.Kernel;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World;

namespace UniClaw.Host;

/// <summary>Host scheduling seam: reads only World-confirmed current meaning;
/// historical references remain readable but never suppress a semantic request.</summary>
internal static class PageSemanticReadSupply
{
    internal static async ValueTask<PageSemanticReadResult> ReadAsync(
        UniKernel? kernel,
        ISemanticPerception perception,
        SemanticPerceptionRequest request,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(perception);
        ArgumentNullException.ThrowIfNull(request);

        var revision = kernel?.CurrentBelief;
        var page = revision?.CurrentPageObservation;
        if (revision is not null && page is { ContainerId: not null, IdentityEvidenceId: not null }
            && request.Current.IsFresh
            && request.Current.RevisionId == revision.RevisionId
            && request.Current.CaptureId == page.Capture.CaptureId
            && request.Current.ObservationCycleId == page.Capture.ObservationCycleId
            && request.Current.SessionCorrelation == page.Capture.SessionCorrelation
            && request.Current.CaptureTime == page.Capture.CaptureTime
            && request.Current.Space == page.Capture.Space
            && request.Current.EvidenceIds.Contains(page.EvidenceId, StringComparer.Ordinal))
        {
            var matches = revision.SemanticKnowledge.Where(meaning =>
                meaning.ApplicableRevisionId == revision.RevisionId
                && meaning.ContainerId == page.ContainerId
                && meaning.CurrentCaptureEvidenceId == page.EvidenceId
                && meaning.CaptureId == page.Capture.CaptureId
                && meaning.ObservationCycleId == page.Capture.ObservationCycleId
                && meaning.SessionCorrelation == page.Capture.SessionCorrelation
                && meaning.CorrespondenceEvidenceIds.Contains(page.EvidenceId, StringComparer.Ordinal)
                && meaning.CorrespondenceEvidenceIds.Contains(page.IdentityEvidenceId, StringComparer.Ordinal))
                .Take(2).ToArray();
            if (matches.Length == 1)
                return new PageSemanticReadResult(matches[0], null);
        }

        var observation = await perception.ObserveSemanticsAsync(request, token).ConfigureAwait(false);
        return new PageSemanticReadResult(null, observation);
    }
}

internal sealed record PageSemanticReadResult(
    WorldSemanticKnowledge? Current,
    SemanticPerceptionResult? Observation);
