namespace UniClaw.Kernel.Perception.Fusion;

public enum EscalationAction { Continue, FocusedRescan, DeepPerception, Stop }

public sealed record EscalationBudget(int FocusedRescansRemaining = 1, int DeepPerceptionsRemaining = 1)
{
    public EscalationBudget Consume(EscalationAction action) => action switch
    {
        EscalationAction.FocusedRescan => this with { FocusedRescansRemaining = Math.Max(0, FocusedRescansRemaining - 1) },
        EscalationAction.DeepPerception => this with { DeepPerceptionsRemaining = Math.Max(0, DeepPerceptionsRemaining - 1) },
        _ => this,
    };
}

public sealed record EscalationRequest(
    FusionDisposition Disposition,
    CoverageDisposition Coverage,
    bool HasFocusedBuyer,
    bool HasDeepBuyer);

public sealed record EscalationDecision(EscalationAction Action, string Reason, EscalationBudget Remaining);

/// <summary>
/// PER-011 bounded recovery policy. It only spends an explicitly supplied budget;
/// it never retries a settled result or escalates when no buyer exists.
/// </summary>
public sealed class BoundedEscalationPolicy
{
    public EscalationDecision Decide(EscalationRequest request, EscalationBudget budget)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(budget);

        if (request.Disposition is FusionDisposition.Supported)
            return new(EscalationAction.Continue, "fusion is supported", budget);
        var needsEscalation = request.Disposition is FusionDisposition.Conflicted or FusionDisposition.Unknown
            || request.Coverage is CoverageDisposition.Partial or CoverageDisposition.Unknown or CoverageDisposition.SourceUnavailable;
        if (!needsEscalation)
            return new(EscalationAction.Stop, "fusion outcome is terminal without escalation trigger", budget);
        if (request.HasFocusedBuyer && budget.FocusedRescansRemaining > 0)
            return new(EscalationAction.FocusedRescan, "bounded focused rescan requested", budget.Consume(EscalationAction.FocusedRescan));
        if (request.HasDeepBuyer && budget.DeepPerceptionsRemaining > 0)
            return new(EscalationAction.DeepPerception, "bounded deep perception requested", budget.Consume(EscalationAction.DeepPerception));
        return new(EscalationAction.Stop, "escalation budget or buyer is exhausted", budget);
    }
}
