using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Kernel;

public sealed partial class UniKernel
{
    private OverlayDismissalEvidence BuildOverlayDismissalEvidence(WorldBeliefRevision? before,
        WorldBeliefRevision? after, EffectReceipt? receipt)
    {
        var bindings = Effects.BindingLog.Select(item => item.Canonical).OfType<CanonicalBinding>()
            .Where(item => receipt is not null && item.BindingId == receipt.BindingId).ToArray();
        var binding = bindings.Length == 1 ? bindings[0] : null;
        var target = before?.Occurrences?.SingleOrDefault(item => item.OccurrenceId == binding?.TargetOccurrenceId);
        EvidenceRecord? Overlay(WorldBeliefRevision? revision)
        {
            if (revision?.WorldState.TryGetValue(OverlayObservationCodec.Subject, out var claim) != true
                || !_ledger.CanonicalRecords.TryGetValue(claim.EvidenceId, out var record)
                || !OverlayObservationCodec.TryRead(record.Claim.Value, out var observed)
                || observed is null
                || revision.WorldState.GetValueOrDefault(ProductAssociationStrategy.PopupSubject)?.Value != observed.State)
                return null;
            return record;
        }
        return new(before?.CurrentPageObservation, after?.CurrentPageObservation, Overlay(before), Overlay(after),
            receipt, binding, target, (target?.EvidenceBasis ?? []).Select(id => _ledger.CanonicalRecords.GetValueOrDefault(id))
                .OfType<EvidenceRecord>().ToArray(), before?.ObservationNeed, after?.ObservationNeed,
            (before?.Conflicts ?? []).Concat(after?.Conflicts ?? []).ToArray());
    }
}
