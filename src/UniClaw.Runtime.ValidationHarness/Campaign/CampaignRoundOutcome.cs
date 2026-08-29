using System.Collections.Immutable;
using UniClaw.Runtime.ValidationHarness.Emulator;
using UniClaw.Runtime.ValidationHarness.Reporting;
using UniClaw.Runtime.ValidationHarness.Results;
using UniClaw.Runtime.ValidationHarness.Scenarios;

namespace UniClaw.Runtime.ValidationHarness.Campaign;

/// <summary>
/// One campaign round's immutable outcome (Phase 2.6): the round index, the
/// authored directive, the strategy identity, the DriverHost-owned run identity,
/// the dispatch outcome, the graduated single-run outcome (which carries the
/// ValidationResult, the boundary verification and the G1–G4 gates), the round's
/// OWN call-log slice, the per-round autonomy assertion, and the four frozen
/// invariant assertions each with evidence refs drawn from THIS round's outputs.
/// </summary>
/// <param name="RoundIndex">Zero-based round index in campaign order.</param>
/// <param name="Directive">The round's authored directive input.</param>
/// <param name="StrategyId">The round's strategy identity (the translated
/// directive's identity; runtime-attested identity lives in
/// <see cref="Result"/>.Admission).</param>
/// <param name="RunId">DriverHost-owned run identity; null when nothing was
/// admitted.</param>
/// <param name="DispatchResult">Driver dispatch outcome (admission or
/// deterministic refusal).</param>
/// <param name="Run">The graduated single-run outcome (result, boundary, gates,
/// report — design D2/D4/D5/D7 composition).</param>
/// <param name="RoundCallLog">This round's OWN call-log slice (exactly the
/// round's dispatches — the slice the runner re-asserts autonomy from).</param>
/// <param name="Autonomy">Per-round autonomy assertion: exactly one accepted
/// <c>run.strategy.start</c>, zero driver/wire control calls after admission.</param>
/// <param name="InvariantAssertions">The four frozen invariants re-asserted
/// from this round's own result and call log.</param>
/// <param name="AllInvariantsPass">True when every invariant assertion passes
/// this round.</param>
public sealed record CampaignRoundOutcome(
    int RoundIndex,
    CampaignRoundDirective Directive,
    string StrategyId,
    string? RunId,
    DriverDispatchResult DispatchResult,
    ScenarioRunOutcome Run,
    EmulatorCallLog RoundCallLog,
    CampaignAutonomyAssertion Autonomy,
    ImmutableArray<InvariantAssertion> InvariantAssertions,
    bool AllInvariantsPass)
{
    /// <summary>This round admitted exactly one directive into exactly one run.</summary>
    public bool AdmittedRun => RunId is not null && Run.AdmittedRun;

    /// <summary>The round's aggregated validation result (design D4).</summary>
    public ValidationResult Result => Run.Result;

    /// <summary>The round's derived boundary proof (design D5).</summary>
    public BoundaryVerification Boundary => Run.Boundary;

    /// <summary>The round's G1–G4 gate outcomes (design D7).</summary>
    public ValidationGates Gates => Run.Gates;
}