using UniClaw.Runtime.ValidationHarness.Emulator;
using UniClaw.Runtime.ValidationHarness.Scenarios;

namespace UniClaw.Runtime.ValidationHarness.Campaign;

/// <summary>
/// One planner decision (Phase 2.6): either author the NEXT round's directive
/// or terminate the campaign with an explicit <see cref="CampaignTermination"/>.
/// The closed union makes an implicit fall-through impossible — the runner
/// stops only on <see cref="Stop"/> or the runner-owned hard MaxRounds bound.
/// </summary>
public abstract record CampaignPlannerDecision
{
    private CampaignPlannerDecision()
    {
    }

    /// <summary>Author the next round's directive. The runner REQUIRES a new
    /// StrategyId per round (a repeated identity is rejected — idempotency is
    /// UniAgent-owned, outside this change).</summary>
    public sealed record Continue(CampaignRoundDirective Directive) : CampaignPlannerDecision;

    /// <summary>Terminate now with an explicit, recorded termination.</summary>
    public sealed record Stop(CampaignTermination Termination) : CampaignPlannerDecision;
}

/// <summary>
/// The planner seam (Phase 2.6): given the prior round outcomes — immutable,
/// already per-round-asserted evidence — decide the next round directive or
/// terminate. The upper-agent loop (later WorkItem) supplies this delegate; the
/// <see cref="IterativeCampaignRunner"/> enforces round independence and the
/// loop closure AROUND it (a planner that never stops cannot loop forever: the
/// hard MaxRounds bound records a bounded stop). The planner is a pure reader
/// of prior outcomes — it never touches the Runtime between runs.
/// </summary>
public delegate CampaignPlannerDecision CampaignRoundPlanner(
    IReadOnlyList<CampaignRoundOutcome> priorRounds,
    CancellationToken cancellationToken);

/// <summary>
/// The run-execution seam (Phase 2.6): exactly ONE round's single-run
/// composition through the graduated Phase 2.5 chain (<see cref="ScenarioRunner"/>
/// on Tier A, or a Tier-B real-emulator composition plugged in later). Receives
/// the round directive and the campaign's immutable whole-campaign driver call
/// log (grows across rounds via ScenarioRunner's priorCallLog chaining — S3's
/// cross-run boundary proof surface). The returned <see cref="ScenarioRunOutcome"/>
/// MUST carry the round's OWN call-log slice in <c>RunCallLog</c> (the runner
/// re-asserts autonomy and the four invariants from that slice alone).
/// </summary>
public delegate Task<ScenarioRunOutcome> CampaignRunExecutor(
    CampaignRoundDirective directive,
    EmulatorCallLog priorCallLog,
    CancellationToken cancellationToken);