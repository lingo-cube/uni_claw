namespace UniClaw.Runtime.ValidationHarness.PlanDelta;

/// <summary>
/// Deterministic machine check of one planning round's PlanDelta (spec "PlanDelta
/// contract" scenarios "Deltas are evidenced and contract-legal"; design D5):
/// citations must resolve, each declared change must correspond to a REAL
/// directive freedom difference, every real difference must be covered by
/// exactly one declared change (no undeclared drift, no vacuous delta, no
/// duplicate delta), and NO-OP rounds must be honest (non-empty reason AND
/// NextStrategy equal on every compared lever). StrategyId / ContractVersion
/// differences are expected between rounds and excluded. Rejections name the
/// first violation; a rejection is a typed result, never an exception.
/// </summary>
public static class PlanDeltaValidator
{
    /// <summary>Validate one planning round; deterministic: same round ⇒ same outcome.</summary>
    public static PlanDeltaValidation Validate(PlanningRound round)
    {
        ArgumentNullException.ThrowIfNull(round);

        var previous = DirectiveFacts.Compute(round.PreviousPlan);
        var next = DirectiveFacts.Compute(round.NextStrategy);

        if (round.PlanDelta.IsNoOp)
            return ValidateNoOp(round, previous, next);

        if (ValidateCitations(round) is { } citationRejection)
            return citationRejection;

        var declaredCounts = round.PlanDelta.Changes
            .GroupBy(change => change.Freedom)
            .ToDictionary(group => group.Key, group => group.Count());

        // A declared DispatchPolicy change needs the round's summaries as its
        // evidence surface (the directive itself never carries dispatch policy).
        if (declaredCounts.ContainsKey(PlanDeltaFreedom.DispatchPolicy)
            && (round.PreviousDispatchPolicySummary is null || round.NextDispatchPolicySummary is null))
        {
            return new PlanDeltaValidation.Rejected(
                "dispatch policy delta requires both previous and next dispatch policy summaries");
        }

        foreach (var freedom in Enum.GetValues<PlanDeltaFreedom>())
        {
            var actual = freedom == PlanDeltaFreedom.DispatchPolicy
                ? !DirectiveFacts.SameDispatchSummaries(round.PreviousDispatchPolicySummary, round.NextDispatchPolicySummary)
                : DirectiveFacts.LeverDiffers(previous, next, freedom);
            var declared = declaredCounts.GetValueOrDefault(freedom);

            if (actual && declared == 0)
                return new PlanDeltaValidation.Rejected(
                    $"undeclared directive drift: {DirectiveFacts.FieldName(freedom)}");
            if (!actual && declared > 0)
                return new PlanDeltaValidation.Rejected($"vacuous delta: {freedom}");
            if (declared > 1)
                return new PlanDeltaValidation.Rejected(
                    $"duplicate delta: {freedom} (exactly one declared change per freedom difference)");
        }

        foreach (var driftedField in DirectiveFacts.DriftOnlyDiffs(previous, next))
            return new PlanDeltaValidation.Rejected($"undeclared directive drift: {driftedField}");

        return new PlanDeltaValidation.Accepted();
    }

    // ── NO-OP consistency (NO_OP_WITH_REASON) ────────────────────────────────

    private static PlanDeltaValidation ValidateNoOp(
        PlanningRound round,
        DirectiveFacts.Computed previous,
        DirectiveFacts.Computed next)
    {
        if (string.IsNullOrWhiteSpace(round.PlanDelta.NoOpReason))
        {
            return new PlanDeltaValidation.Rejected(
                "NO_OP_WITH_REASON requires a non-empty reason; an empty delta without a reason is not a legal round");
        }

        if (!DirectiveFacts.SameDirectiveLevers(previous, next))
        {
            return new PlanDeltaValidation.Rejected(
                "NO-OP delta records no change but NextStrategy differs from PreviousPlan on a compared lever");
        }

        if (!DirectiveFacts.SameDispatchSummaries(round.PreviousDispatchPolicySummary, round.NextDispatchPolicySummary))
        {
            return new PlanDeltaValidation.Rejected(
                "NO-OP delta records no change but the round's dispatch policy summaries differ");
        }

        return new PlanDeltaValidation.Accepted();
    }

    // ── Citation resolution (HARD contract, not a soft warning) ──────────────

    private static PlanDeltaValidation? ValidateCitations(PlanningRound round)
    {
        var resolvableKnowledge = round.LoadedKnowledge
            .Concat(round.NewKnowledge)
            .ToHashSet(StringComparer.Ordinal);
        var availableEvidence = round.ObservedResult.EvidenceRefs.ToHashSet(StringComparer.Ordinal);

        for (var index = 0; index < round.PlanDelta.Changes.Count; index++)
        {
            var change = round.PlanDelta.Changes[index];
            foreach (var knowledgeRef in change.KnowledgeRefs)
            {
                if (!resolvableKnowledge.Contains(knowledgeRef))
                {
                    return new PlanDeltaValidation.Rejected(
                        $"PlanDelta change #{index + 1} cites unresolvable knowledge ref '{knowledgeRef}' (must resolve within LoadedKnowledge ∪ NewKnowledge)");
                }
            }

            foreach (var evidenceRef in change.EvidenceRefs)
            {
                if (!availableEvidence.Contains(evidenceRef))
                {
                    return new PlanDeltaValidation.Rejected(
                        $"PlanDelta change #{index + 1} cites unresolvable evidence ref '{evidenceRef}' (must resolve within ObservedResult.EvidenceRefs)");
                }
            }
        }

        return null;
    }
}