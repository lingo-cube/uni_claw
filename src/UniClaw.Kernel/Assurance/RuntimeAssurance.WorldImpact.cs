using System.Collections.Immutable;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Assurance;

public sealed record EffectWorldUse(string ReceiptId, string BindingId, WorldConsumptionBasis ActionBasis,
    IReadOnlyList<WorldDecisionUse> Decisions, WorldConsumptionBasis? VerificationBasis = null,
    bool? InitiallyVerified = null);

/// <summary>Append-only reassessment. It does not rewrite the original receipt,
/// verification, Run outcome, or request an inverse operation.</summary>
public sealed record EffectCorrectionReview(string ReviewId, string CorrectionEvidenceId,
    string ReceiptId, string BindingId, string SourceRevisionId, string DeliveryStatus,
    bool? InitiallyVerified, string? PreviousContainerId, string? CorrectedContainerId,
    IReadOnlyList<string> RevokedConsumedEvidenceIds, string DependencyCoverage,
    string Resolution, string Reason, bool TerminalAlreadyRecorded);

public sealed partial class RuntimeAssurance
{
    private readonly List<EffectWorldUse> _effectWorldUses = [];
    private readonly List<EffectCorrectionReview> _effectCorrectionReviews = [];
    public IReadOnlyList<EffectWorldUse> EffectWorldUses => _effectWorldUses.AsReadOnly();
    public IReadOnlyList<EffectCorrectionReview> EffectCorrectionReviews => _effectCorrectionReviews.AsReadOnly();

    internal void RecordEffectWorldUse(EffectReceipt receipt, WorldConsumptionBasis basis,
        IReadOnlyList<WorldDecisionUse> decisions)
    {
        if (_effectWorldUses.Any(u => u.ReceiptId == receipt.ReceiptId)) return;
        _effectWorldUses.Add(new(receipt.ReceiptId, receipt.BindingId, basis, decisions.ToImmutableArray()));
    }

    internal void RecordEffectVerificationUse(string receiptId, WorldConsumptionBasis basis,
        PostActionEffectVerification verification)
    {
        var prior = _effectWorldUses.LastOrDefault(u => u.ReceiptId == receiptId);
        if (prior is not null) _effectWorldUses.Add(prior with { VerificationBasis = basis, InitiallyVerified = verification.IsVerified });
    }

    internal void ReassessCorrection(PageCorrectionDecision correction, WorldConsumptionBasis current,
        IReadOnlyList<EffectReceipt> receipts, bool terminal,
        IReadOnlyDictionary<string, WorldConsumptionBasis> bindingSources)
    {
        if (!correction.Applied) return;
        foreach (var receipt in receipts)
        {
            if (_effectCorrectionReviews.Any(r => r.ReceiptId == receipt.ReceiptId
                && r.CorrectionEvidenceId == correction.CorrectionEvidenceId)) continue;
            var use = _effectWorldUses.LastOrDefault(u => u.ReceiptId == receipt.ReceiptId);
            var recovered = bindingSources.GetValueOrDefault(receipt.ReceiptId);
            if (use is null && recovered is { CaptureEvidenceId: not null, IdentityEvidenceId: not null }
                && !correction.RevokedInterpretationEvidenceIds.Any(recovered.EvidenceIds.Contains)) continue;
            var consumed = use is null ? [] : use.ActionBasis.EvidenceIds
                .Concat(use.Decisions.SelectMany(d => d.Basis.EvidenceIds))
                .Concat(use.VerificationBasis?.EvidenceIds ?? []).Distinct(StringComparer.Ordinal).ToArray();
            var revoked = correction.RevokedInterpretationEvidenceIds.Intersect(consumed, StringComparer.Ordinal).ToImmutableArray();
            var lineageKnown = use?.ActionBasis is { CaptureEvidenceId: not null, IdentityEvidenceId: not null };
            if (lineageKnown && revoked.IsEmpty) continue;
            var unknown = !lineageKnown && revoked.IsEmpty;
            var reason = unknown ? "original-consumption-dependencies-not-collected"
                : receipt.Outcome.IsUnconfirmedOutcome() ? "delivery-unconfirmed-reconcile-before-further-action"
                : "consumed-world-basis-revoked;effect-on-intended-object-unproven";
            _effectCorrectionReviews.Add(new("effect-review:" + correction.CorrectionEvidenceId + ":" + receipt.ReceiptId,
                correction.CorrectionEvidenceId, receipt.ReceiptId, receipt.BindingId, current.SourceRevisionId,
                receipt.Outcome.ToString(), use?.InitiallyVerified, correction.PreviousContainerId, correction.CorrectedContainerId,
                revoked, unknown ? "not_collected" : "recorded-page-target-and-verification;other-dependencies-unproven",
                unknown ? "DependenciesUnknown" : "Unresolved", reason, terminal));
        }
    }

    internal bool HasUnresolvedWorldEffectBasis => _effectCorrectionReviews.Any(r => r.Resolution is "Unresolved" or "DependenciesUnknown");

    private bool HasUnresolvedEffectInContext(string? containerId) => _effectCorrectionReviews.Any(r =>
        (r.Resolution is "DependenciesUnknown" or "Unresolved")
        && (containerId is null || containerId == r.PreviousContainerId || containerId == r.CorrectedContainerId));
}
