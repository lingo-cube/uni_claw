# B. Iterative Campaign Runner — Acceptance Evidence

## Leader's independent verification

- Build: `dotnet build src/UniClaw.Runtime.sln` → 0 errors.
- Tests (re-run by leader): `CampaignRunnerTests` → **8/8 passed**.
- Purity: only new files under `src/UniClaw.Runtime.ValidationHarness/Campaign/` +
  `tests/UniClaw.Runtime.Tests/ValidationHarness/CampaignRunnerTests.cs`; zero edits to
  Runtime production paths (baseline manifest unchanged for those paths; only the two
  documented pre-existing diffs remain).
- Worker-reported full-solution state: 2159/2161; the 2 failures are pre-existing
  `[Collection("RealDevice")]` physical-device tests requiring attached hardware —
  out of scope (PhysicalDevice: DEFERRED).

## Worker WorkResult (module-worker-b) — accepted design summary

- ScenarioRunner reused unchanged through a thin `CampaignRunExecutor` seam;
  `CampaignRunExecutors.TierA(host)` adapts `ScenarioRunner.RunTierAAsync` verbatim with
  `priorCallLog` chaining (one immutable whole-campaign call log) — Tier-B real-emulator
  compositions plug into the same seam (Stage G/H/I/K entry point).
- Planner seam: `CampaignRoundPlanner` receives immutable prior-round snapshots, returns
  closed `CampaignPlannerDecision` union; unreachable-default throw (no implicit fall-through).
- Termination: exactly spec's three kinds (BoundedScopeExhaustion / UnsafeRemainingFrontier
  with reason+evidence / EvidencedRuntimeContractGap with reason+evidence) + runner-owned
  `BoundedStop` (max rounds, checked BEFORE the round). Duplicate StrategyId rejected as
  an evidenced gap; round never executes; idempotency stays UniAgent-owned.
- Per-round re-assertion: autonomy from the round's OWN call-log slice (exactly one accepted
  `run.strategy.start`, zero post-admission/foreign calls); four invariants as explicit
  `InvariantAssertion(bool, id, EvidenceRefs, Reason)` derived from that round's own
  result/report — the poisoned-round test proves per-round (not cached) assertion.
- I3/I4 recorded truthfully (structural scans; no fabricated scenario verdicts).
- Noted follow-up (non-blocking): latent .NET 10 hazard enumerating
  `default(ImmutableArray<T>)` from `Unavailable` fields in the graduated G3 walk — the
  test fake avoided it truthfully; recorded here for the chain owner (NO Runtime change
  made or needed by this change).

DEVIATIONS: none. BLOCKED: none.

## Spec scenario coverage

| Spec scenario | Test evidence |
|---|---|
| Every run is autonomous and independent | per-round slice assertions; zero post-admission entries per round |
| Loop termination (three kinds) | each termination kind produced + recorded; hard-bound BoundedStop |
| Four frozen invariants asserted per run | InvariantAssertion per round; poisoned-round fails alone |
| distinct StrategyId/RunId per run | duplicate StrategyId rejected with evidence |
