# UniAgent ↔ RuntimeAgent Strategy Contract — Graduation Decision

> Status: GRADUATED (human-authorized verification path — Option A, 2026-08-24) | Decision: `GRADUATE_UNIAGENT_RUNTIMEAGENT_STRATEGY_CONTRACT` | Date: 2026-08-24
> Change: `openspec/changes/uniagent-runtimeagent-strategy-contract/`
> Authority: Runtime Architecture Contract (I-1..I-14) and Architecture v1 remain the governing baselines; this decision adds no architecture authority.

## 1. Buyer and exact claim boundary

**Buyer:** UniAgent → RuntimeAgent bounded abstract-strategy execution (Runtime Exploration
Roadmap Phase 1 — Exploration Plan Contract).

This receipt claims only that:

- `StrategyDirective` (objective, scope, exploration intent, constraints, completion
  criteria, adaptation boundary) is a typed, immutable, UniAgent-authored start-time
  contract, admitted by deterministic pre-Run admission.
- `run.strategy.start` is an additive Goal-plane operation; the frozen `run.start`
  request and all eight read-only methods are unchanged.
- RuntimeAgent interprets an accepted strategy into a runtime-local
  `RuntimeExecutionIntent` and adapts only within the declared
  `StrategyAdaptationBoundary`; objective, scope, safety constraints, and completion
  criteria are immutable for the accepted Run.
- Unsupported semantics, unresolved user language, concrete action/route content,
  unbounded scope, contradictory constraints, and unverifiable completion are rejected
  deterministically — never guessed, never implemented with scenario knowledge.
- One accepted `StrategyDirective` correlates with at most one Runtime-owned Run
  identity.

It claims no UniAgent Planner implementation, no automatic strategy generation, no
scenario knowledge base, no mid-Run strategy replacement, and no Multi-Run continuation.

## 2. Validation evidence (2026-08-24)

- Build: 0 warnings, 0 errors (`dotnet build src/UniClaw.Runtime.sln`).
- Deterministic suites: 1971/1971 Runtime + 32/32 Semantic green, including
  `StrategyContractTests`, `StrategyRunWireTests`, `StrategyContractAuthorityTests`,
  `ArchitectureGuardTests`, and `ExternalSemanticCapabilityBoundaryGuardTests`.
- Architecture/consistency: `scripts/check-consistency.sh` C1–C12 ALL PASS;
  `git diff --check` PASS.
- Strict OpenSpec validation: 60/60 (`openspec validate --all --strict`).
- Device-dependent limitation: 7 RealDevice/RealEmulator tests fail-closed on absent
  ADB device (hardware availability, by design); recorded, not hidden.

## 3. Six forbidden-edge proofs (Sol independent verification)

Sol (fresh-context independent reviewer, repository-evidence-only) returned
**`SIX_FORBIDDEN_EDGES: VERIFIED`** (82/82 targeted guard tests green):

| Edge | Structural evidence | Named guard |
|---|---|---|
| User planning | Single `StrategyDirective` construction at wire deserialization (`StrategyRunStartWireContract.cs:31`); zero strategy-generation code under `src/UniClaw.Runtime` | `StrategyContractTests.MissingTypedCriterion_IsRejectedRatherThanInferred` |
| Action | Zero `DeviceAction` occurrences in intent type graph (`StrategyContract.cs:90-114`, `Planning/`) | `StrategyContractAuthorityTests.StrategyModels_CarryNoDeviceActionOrGoalEvidenceOrLifecycleCommand` |
| FSM | No `Traversal`/`StateMachine` references from strategy modules; `StrategyRunAdmission.RunState` get-only | `RuntimeStrategySources_HaveNoTraversalFsmMultiRunOrScenarioKnowledge`, `SessionHasNoExecutionOrLifecycleDependencies` |
| Completion | World match without Agent GoalEvidence ends `RunState.Failed`, no `Completed` trace entry | `DeclaredCompletionCriterion_CannotCompleteWithoutAgentGoalEvidence` |
| MultiRun | `StartStrategyRun` has own admission path, never calls `StartRun`; `_strategyRunIds` duplicate lockout permanent | Token bans in authority/loop/boundary guards + `StrategyAdmissionTests` duplicate rejection |
| Scenario knowledge | Zero `Settings`/`Wi-Fi`/`Wifi` literals under `src/UniClaw.Runtime` (independent grep + whole-tree scan) | `ExternalSemanticCapabilityBoundaryGuardTests.RuntimeProductionSource_IsScenarioNeutral` |

Reviewer-recorded non-fatal findings (no behavior impact; candidates for future guard
hardening, each requiring no more than a Small change if pursued): guards distributed
across test files rather than one ArchitectureGuard entry; reflection guard checks
direct property types only (mitigated by source-token scans); fixed strategy-file lists
do not auto-cover future files under `Planning/` for Traversal/FSM/`StartRun` bans;
authority-test token scans are Contains-based without comment stripping.

Scope caveats: verification applies to the current working-tree state (implementation
edits are uncommitted); device-dependent tests not exercised.

## 4. Deferred scope

The following remain outside this graduation and require separate authorization:
UniAgent Planner / automatic exploration-strategy generation; mid-Run strategy
replacement; Multi-Run continuation; exploration Memory (Phase 3) including safety and
known-environment knowledge; dynamic depth and unknown handling (Phase 4); any change to
the frozen `run.start` contract or the eight read-only methods.

## 5. Final lifecycle conclusion

The Strategy Contract change is **GRADUATED** on the evidence above. The change is
eligible for the normal archive step; **archive has not been performed** by this
decision and is a separate lifecycle operation. Graduation authorizes no deferred
scope and no new architecture authority.
