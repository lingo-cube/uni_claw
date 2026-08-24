## 1. Human Apply Gate

- [x] 1.1 Obtain explicit human approval for the `run.strategy.start` Surface A expansion and freeze the approved design/spec revision before production edits.
- [x] 1.2 Record Luna as implementation owner for the approved mechanical scope and Sol as architecture review/final verification owner.

## 2. Contract Models

- [x] 2.1 Add immutable typed external models for `StrategyRunStartRequest`, `StrategyDirective`, objective, scope, exploration intent, constraints, completion criteria, adaptation boundary, semantic criterion reference, and admission result.
- [x] 2.2 Add closed validation rules that reject unbounded fields, concrete action/route content, unresolved user-language intent, contradictory constraints, and unsupported versions.
- [x] 2.3 Add internal `ValidatedStrategy`, non-action `RuntimeExecutionIntent`, and bounded boundary-violation result types without duplicating existing Phase 1–4 models.

## 3. Generic Strategy Admission

- [x] 3.1 Implement deterministic pre-Run admission with stable malformed, unsupported-capability, unsupported-criterion, unverifiable-completion, and boundary-conflict rejection codes.
- [x] 3.2 Implement composition-owned resolution of typed semantic criterion references through generic capability contracts, with no executable callbacks on the wire.
- [x] 3.3 Prove that rejection creates no Run, performs no fallback execution, and does not guess unsupported semantics.

## 4. Runtime-Local Interpretation

- [x] 4.1 Interpret each approved generic exploration intent into a bounded `RuntimeExecutionIntent` while preserving the immutable accepted strategy.
- [x] 4.2 Seed the existing `ExecutionHypothesis` from the runtime intent and current WorldBelief without creating a second Directive or a concrete action plan.
- [x] 4.3 Constrain existing reconciliation and hypothesis adaptation to the accepted `StrategyAdaptationBoundary`; emit a bounded revision/escalation reason on immutable-boundary pressure.
- [x] 4.4 Compile strategy completion criteria only into Agent-verifiable GoalEvidence requirements, never into a RuntimeAgent completion fact.

## 5. Additive Goal-Plane Transport

- [x] 5.1 Add `run.strategy.start` as a distinct DriverHost operation while leaving `run.start` and the frozen eight read-only methods unchanged.
- [x] 5.2 Advertise the new operation through explicit capability discovery without changing protocol semantics for existing clients.
- [x] 5.3 Correlate one accepted StrategyDirective with at most one Runtime-owned Run identity and prevent reuse for active-Run replacement or post-terminal continuation.

## 6. Agent Handoff and Authority Guards

- [x] 6.1 Hand the non-action runtime intent into the existing Agent authorization seam without adding Traversal, FSM, RunState, or terminal dependencies to strategy modules.
- [x] 6.2 Add a type/dependency guard proving `RuntimeExecutionIntent` cannot contain or inherit `DeviceAction` and strategy modules cannot reference Traversal or FSM.
- [x] 6.3 Add a dependency guard proving RuntimeAgent strategy components cannot invoke `run.start` or `run.strategy.start` and therefore cannot create an outer Multi-Run loop.
- [x] 6.4 Keep any future pre-terminal integration behind the separate Agent-owned cycle contract; do not implement or merge that cycle in this change.

## 7. Deterministic Scenario Proofs

- [x] 7.1 Test existing `run.start` wire compatibility and all eight frozen read-only methods.
- [x] 7.2 Test accepted exhaustive-scope exploration and accepted typed-match inspection using generic Fake World capability bindings.
- [x] 7.3 Test unsupported criterion, unresolved prose, concrete route/action input, unbounded scope, contradictory constraints, and unverifiable completion rejection.
- [x] 7.4 Test allowed re-ground/reorder/hypothesis revision and forbidden objective/scope/safety/completion mutation.
- [x] 7.5 Test that an apparent completion match cannot transition terminal state without Agent-owned GoalEvidence and FSM authorization.
- [x] 7.6 Test one StrategyDirective to one Run identity across observation, reconciliation, authorized execution, verification, and terminal state.
- [x] 7.7 Add a scenario-knowledge guard proving Runtime core and Strategy Contract contain no Android Settings labels, selectors, routes, or scenario-specific dependencies.

## 8. Validation and Graduation Review

- [x] 8.1 Run targeted strategy, transport, authority, and existing Phase 1–4 regression tests.
- [x] 8.2 Run the full Runtime solution tests, architecture guards, consistency checks, and strict OpenSpec validation.
  - Evidence (2026-08-24): `dotnet build` 0 errors; deterministic suite 1971/1971 Runtime + 32/32 Semantic green (includes `ArchitectureGuardTests`, `StrategyContractTests`, `StrategyRunWireTests`, `StrategyContractAuthorityTests`, `TraceSpanReadModelArchitectureGuardTests`); `scripts/check-consistency.sh` ALL PASS (C1–C12); `openspec validate --all --strict` 60/60 passed.
  - Known environment limitation: 7 RealDevice/RealEmulator scenario tests fail-closed as designed because no online ADB device is connected; this is a hardware availability dependency, not a regression.
- [x] 8.3 Have Sol independently verify the six forbidden-edge proofs: RuntimeAgent cannot own user planning, Action, FSM, Completion, MultiRun, or scenario knowledge.
  - Evidence (2026-08-24): Sol independent verification (fresh-context reviewer, repository-evidence-only) returned `SIX_FORBIDDEN_EDGES: VERIFIED`. Per-edge: (1) User planning — single `StrategyDirective` construction at wire deserialization (`StrategyRunStartWireContract.cs:31`), unresolved intent rejected (`StrategyContractTests.MissingTypedCriterion_IsRejectedRatherThanInferred`); (2) Action — `StrategyContractAuthorityTests.StrategyModels_CarryNoDeviceActionOrGoalEvidenceOrLifecycleCommand` + zero `DeviceAction` in intent type graph; (3) FSM — `RuntimeStrategySources_HaveNoTraversalFsmMultiRunOrScenarioKnowledge` + `SessionHasNoExecutionOrLifecycleDependencies`; (4) Completion — `DeclaredCompletionCriterion_CannotCompleteWithoutAgentGoalEvidence` (world match without GoalEvidence ends `RunState.Failed`); (5) MultiRun — `RunExecutionCoordinator.StartStrategyRun` has its own admission path, never calls `StartRun`, `_strategyRunIds` duplicate lockout; token bans across three guards; (6) Scenario knowledge — `RuntimeProductionSource_IsScenarioNeutral` whole-tree scan + zero independent grep matches. 82/82 targeted guard tests green.
  - Non-fatal reviewer findings (recorded, no behavior impact): guards distributed across Strategy/Scenario/Architecture test files rather than a single ArchitectureGuard entry; reflection guard checks direct property types only (mitigated by source-token scans); fixed strategy-file lists do not auto-cover future files under Planning/ for Traversal/FSM/StartRun bans; authority-test token scans are Contains-based without comment stripping.
  - Scope caveats: verification applies to current uncommitted working tree; 7 device-dependent tests not exercised (fail-closed, unrelated to the six edges).
- [x] 8.4 If and only if all evidence passes, prepare a separate graduation decision and lifecycle projection update; do not archive or claim graduation from task completion alone.
  - Evidence (2026-08-24): graduation decision prepared and recorded at `docs/decisions/uniagent-runtimeagent-strategy-contract-graduation-decision.md` (human-authorized via Option A; six forbidden-edge proofs VERIFIED by Sol independent review; 82/82 targeted guards + full deterministic suite + consistency + strict validation green). Lifecycle projections updated (`docs/work/active/current-gates.md` Gate Annotations, `docs/snapshots/latest.md`). Archive intentionally NOT performed — separate lifecycle operation.
