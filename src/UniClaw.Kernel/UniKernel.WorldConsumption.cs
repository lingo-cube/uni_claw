using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Runtime;
using System.Collections.Immutable;

namespace UniClaw.Kernel;

public sealed partial class UniKernel
{
    internal int RelevantWorldExecutionRecords => Control.RelevantWorldExecutionRecords;
    internal bool HasWorldReview => Control.HasWorldReview;
    internal bool RecordWorldSupplementation(SupplementationExecution execution, UniClaw.Kernel.World.WorldKnowledgeView knowledge) =>
        Control.RecordWorldSupplementation(execution, knowledge);
    internal bool RequestWorldReview(UniClaw.Kernel.World.WorldKnowledgeView knowledge, string reason) =>
        Control.RequestWorldReview(knowledge, reason);
    internal WorldRecoveryView ProjectWorldRecovery(UniClaw.Kernel.World.WorldKnowledgeView knowledge) =>
        Control.ProjectWorldRecovery(knowledge);
    internal void MarkWorldReviewRead() => Control.MarkWorldReviewRead();
    internal void RecordWorldConsultation(AgentDecisionContext context)
    {
        var source = _world.DeriveConsumptionBasis(context.WorldKnowledge?.SourceRevisionId);
        var snapshot = _world.RevisionHistory.FirstOrDefault(r => r.RevisionId == source.SourceRevisionId);
        var supplied = new HashSet<string>(StringComparer.Ordinal);
        if (context.Screen?.ContainerId == source.ContainerId || context.Elements?.Count > 0)
        {
            if (source.CaptureEvidenceId is not null) supplied.Add(source.CaptureEvidenceId);
            if (source.IdentityEvidenceId is not null) supplied.Add(source.IdentityEvidenceId);
            foreach (var occurrence in snapshot?.Occurrences ?? [])
                if ((occurrence.OwningContainerId == context.Screen?.ContainerId
                    || occurrence.OwningContainerId is null && snapshot?.Containers?.Count == 1)
                    && context.Elements?.Any(e => e.Role == occurrence.Role && e.Text == occurrence.SemanticDescriptor) == true)
                    foreach (var id in occurrence.EvidenceBasis) supplied.Add(id);
        }
        if (context.WorldKnowledge is { } knowledge)
        {
            foreach (var gap in knowledge.Gaps) foreach (var id in gap.EvidenceIds) supplied.Add(id);
            foreach (var reference in knowledge.SemanticReferences)
            { supplied.Add(reference.EvidenceId); if (reference.VerificationEvidenceId is { } id) supplied.Add(id); }
            foreach (var meaning in knowledge.CurrentSemantics)
            {
                supplied.Add(meaning.OriginalSemanticEvidenceId); supplied.Add(meaning.VerificationEvidenceId);
                foreach (var id in meaning.CorrespondenceEvidenceIds) supplied.Add(id);
            }
        }
        Control.RecordWorldDecisionUse(context.DecisionId, source with
        { EvidenceIds = supplied.Where(id => snapshot?.EvidenceBasis.Contains(id) == true).Order(StringComparer.Ordinal).ToImmutableArray(),
          Coverage = "actually-supplied-page-elements-and-meaning;other-claims-and-model-reasoning-unproven" });
    }
    internal bool HasCorrectedPlanningBasis(IReadOnlyList<string> decisionIds) =>
        Control.HasCorrectedPlanningBasis(decisionIds, _world.PageCorrectionLog);
    public IReadOnlyList<WorldDecisionUse> WorldDecisionUses => _control?.WorldDecisionUses ?? [];
    public IReadOnlyList<EffectWorldUse> EffectWorldUses => _assurance?.EffectWorldUses ?? [];
    public IReadOnlyList<EffectCorrectionReview> EffectCorrectionReviews => _assurance?.EffectCorrectionReviews ?? [];
    internal void RecordEffectVerificationSources(string receiptId, PostActionEffectVerification result) =>
        Assurance.RecordEffectVerificationUse(receiptId, _world.DeriveConsumptionBasis(result.RevisionId), result);
}
