# RuntimeAgent Decision Adaptation — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_RUNTIME_AGENT_DECISION_ADAPTATION` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-runtime-agent-decision-adaptation/`
> Authority: Runtime Architecture Contract I-1..I-14 (specifically I-2/I-3/I-5/I-12/I-13) and Architecture v1 (invariants 2-4) remain the governing baselines; this decision adds no architecture authority.

## 1. Buyer and exact claim boundary

**Buyer:** RuntimeAgent decision-to-hypothesis loop closure — applying a bounded RuntimeDecision to update the run-local execution hypothesis without gaining planning, execution, authorization, recovery, or traversal authority (per proposal.md "Why").

This receipt claims only that:

1. a **NEW** immutable `HypothesisAdaptation` record (`Model/`) exists as a passive record of one bounded modification of the execution hypothesis — RunId, AdaptationType (Keep/Replace/Escalate), DecisionReference, PreviousHypothesisReference, AdaptedHypothesis, AdaptationReason — carrying NO Plan, DeviceAction, Tap instruction, UI element selection, Goal modification, Traversal control, or execution authority (proposal.md "What Changes"; spec Requirement "Hypothesis adaptation representation");
2. a **NEW** `HypothesisAdaptationType` enum exists with Keep=1, Replace=2, Escalate=3 (proposal.md "What Changes");
3. a **NEW** stateless static pure `HypothesisAdapter.Adapt(RuntimeDecision, ExecutionHypothesis) → HypothesisAdaptation` (`Planning/`) exists mirroring the `HypothesisReconciler.Reconcile` discipline — Keep (Continue → confirm), Replace (Revise → new boundary-aware hypothesis, NO SystemBack), Escalate (Escalate → record inability, NO recovery); generic reasons only, NO scenario strings (proposal.md "What Changes"; design.md Decisions 2/3/5);
4. the `ExecutionHypothesisLedger` is extended **additively** with `Adapt()` (reads `LatestDecision`, delegates to the adapter, applies the `AdaptedHypothesis` to `_current` appending to `_history`, stores in `_latestAdaptation`) and a `LatestAdaptation` property; the ledger remains method-local, not Runtime state (proposal.md; design.md Decision 4);
5. `DirectiveExecution.RunDirectiveAsync` is extended **additively** with one call `ledger.Adapt()` inside the existing ContinueWith after `Reconcile` when the ledger is non-null, with **no signature change**; a null ledger preserves Phase 1-3 behavior unchanged (proposal.md; design.md Decision 4);
6. the integration is additive-only: the DFS engine, FSM, ExternalBoundary capability, `Agent.OpenWorld.cs`, `Agent.cs`, `Agent.Recovery.cs`, `Container/`, `Traversal/`, `Recovery/`, `World/`, `IntentExecution.cs`, `HypothesisReconciler.cs`, all contracts, and all frozen invariants remain unchanged (proposal.md "What Changes"/"Impact");
7. there is **no authority movement**: HypothesisAdaptation is passive, HypothesisAdapter is stateless, Replace does NOT execute SystemBack, Escalate does NOT recover; verified against v1 invariants 2-4 and Contract I-2/I-3/I-5/I-12/I-13 (proposal.md "Impact — Authority");
8. run-local isolation and history preservation hold: `LatestAdaptation` is per-run with two separate runs independent, the ledger is not retained in any Agent/Container/Traversal/Environment field, and `Adapt()` appends to the immutable history without rewriting prior entries (tasks.md tasks 5.3/5.4; spec Requirements "No authority over execution" / "Immutable history preservation").

No claim is made for: executing the adaptation's consequences (SystemBack, recovery, retry) — Replace only records a boundary-aware objective and the boundary was already handled by the ExternalBoundary capability inside the DFS loop, and Escalate only records inability with no recovery (design.md Non-Goals); real-time mid-loop adaptation, which would require modifying the DFS loop (out of scope; post-run decision-driven adaptation satisfies the proof goal) (design.md Non-Goals); agent-observable adaptation state (an Agent field) — the adaptation is observed via the ledger's `LatestAdaptation` (design.md Non-Goals); an autonomous planner, recovery executor, action selector, authorization layer, or global memory — forbidden by the mission and frozen invariants (design.md Non-Goals); any change to Architecture v1, Protocol v1, Contract I-1..I-14, the charter, `RunStartRequest`, the `Agent.RunOpenWorldAsync` signature, or any frozen decision (proposal.md "NOT changed").

## 2. Validation evidence

- proposal.md records the change's type and baseline: "Capability extension (additive, no contract/invariant change, no DFS-loop modification)"; "Baseline verified: 2026-08-22, branch `uni-agent`, Phases 1-3 verified clean"; "Authority decision: Leader review passed (all 5 stop-conditions) — NONE authority impact" (proposal.md header).
- tasks.md marks all task checkboxes complete — 1.1..9.2 with every `[x]` checked (tasks.md tasks 1-9).
- Build record (as a completed task): tasks.md task 8.1 `[x]` — `dotnet build src/UniClaw.Runtime.sln` — 0 errors, 0 warnings (quarantine-verify-restore isolation permitted for a concurrent broken `Capabilities/Perception/Semantic/` tree if needed).
- Test record (as a completed task): tasks.md task 8.2 `[x]` — `dotnet test src/UniClaw.Runtime.sln` — all deterministic suites green (1596+), including SETTINGS-TREE-01 capstone (TREE-1..TREE-20), U2OpenWorld, OpenWorldTypeDirected, Phase 1 Directive tests, Phase 2 ExecutionHypothesis tests, Phase 3 RuntimeDecision tests, ArchitectureGuardTests; only pre-existing env-gated RealDevice/RealEmulator and the concurrent scroll-guard may fail.
- Consistency record (as a completed task): tasks.md task 8.3 `[x]` — `scripts/check-consistency.sh` ALL PASS and `git diff --check` clean.
- OpenSpec validation record (as a completed task): tasks.md task 9.1 `[x]` — `openspec validate runtime-agent-decision-adaptation --strict` — passes.
- Deterministic test coverage is documented as completed tasks (not run logs): tasks.md tasks 5.1-5.4 (`HypothesisAdaptationTests`, `HypothesisAdapterTests`, `HypothesisAdaptationRunLocalIsolationTests`, `HypothesisAdaptationHistoryTests`), 6.1-6.6 (`HypothesisAdaptationAuthorityTests`; Replace no SystemBack/DeviceAction; Escalate no recovery/retry; RunState produced by the DFS engine not the adaptation; GoalEvidence evaluated by the existing evaluator; authorization path does not reference the adaptation), and 7.1-7.3 (`AdaptationScenario1KeepTests`, `AdaptationScenario2ReplaceTests`, `AdaptationScenario3EscalateTests` — Fake World) — all `[x]`.
- design.md documents the five design decisions (immutable record in `Model/`; stateless pure adapter in `Planning/`; Replace does NOT execute SystemBack and Escalate does NOT recover; ledger `Adapt()` + `LatestAdaptation` with a one-line ContinueWith integration; generic adapted-hypothesis objective derived from the decision's evidence reference) and the Migration Plan: additive only, deploy = build `src/UniClaw.Runtime.sln` + `dotnet test`, rollback = delete the two new files and revert the two additive modifications, no shared mutable state, no contract change (design.md Decisions 1-5 / Migration Plan).
- spec.md encodes the regression gate: "Existing capability regression" requires the SETTINGS-TREE-01 capstone proofs (TREE-1..TREE-20) and all Phase 1-3 tests to pass unchanged (specs/runtime-agent-decision-adaptation/spec.md, Requirement "Existing capability regression").
- The change's files record no standalone evidence/ directory and no independent build/test run logs; the above is the verification evidence the change's own files record.

## 3. Scenario receipts and falsifiers

The change files record no explicit falsifier section (no evidence/ directory; design.md's Risks/Trade-offs documents mitigations for five risks but no falsification results); rejection/negative requirements are defined in specs/runtime-agent-decision-adaptation/spec.md:

- **"Hypothesis adaptation representation"** — the record MUST NOT carry a Plan, a DeviceAction, a Tap instruction, a UI element selection, a Goal modification, a Traversal control, scenario strings, or any execution authority; invalid construction (blank RunId / blank AdaptationReason) must fail with an explicit validation error and create no instance.
- **"Adaptation types"** — the enum contains exactly Keep, Replace, and Escalate, and no other types exist.
- **"Stateless hypothesis adaptation"** — the adapter MUST NOT observe the world, authorize an action, execute anything, recover, modify the Goal or completion, or contain scenario-specific knowledge.
- **"Keep adaptation"** — MUST NOT create a new assumption, execute an action, or modify the Goal.
- **"Replace adaptation"** — MUST NOT execute a SystemBack, a DeviceAction, a Tap, or any traversal action; the existing ExternalBoundary capability inside the DFS loop remains solely responsible for boundary handling.
- **"Escalate adaptation"** — MUST NOT recover, retry, dispatch an action, or automatically continue; it records the authority boundary being exceeded.
- **"No authority over execution"** — the model and adapter MUST NOT acquire any decision, authorization, completion, execution, recovery, or traversal authority; the adaptation MUST NOT be consulted by the Agent; the adapter MUST NOT call any Agent method, dispatch a DeviceAction, create a container, or initiate a sub-run; the RuntimeAgent remains the sole run-level semantic authority, the Agent the sole execution authority, the FSM the sole lifecycle owner, and the Traversal the sole action performer; the DFS engine MUST be unchanged.
- **"Additive integration without DFS or FSM modification"** — the DFS engine, the FSM (RunState), the `IntentExecution` seam, `Agent.OpenWorld.cs`, `Agent.cs`, `Container/`, `Traversal/`, `Recovery/`, `World/`, and `HypothesisReconciler.cs` MUST remain unchanged; a null ledger preserves the existing Phase 1-3 behavior with zero regression.
- **"Immutable history preservation"** — `Adapt()` MUST append to the immutable history without rewriting or deleting prior entries.
- **"Existing capability regression"** — the capability MUST NOT change the behavior of open-world execution, bounded candidate safety, cross-page discovery, the SETTINGS-TREE-01 capstone, or the Phase 1-3 capabilities.

## 4. Deferred scope

The following remain outside this graduation and require separate authorization:

- Executing the adaptation's consequences — SystemBack, recovery, retry (Replace records a boundary-aware objective; the ExternalBoundary capability already handled the boundary inside the DFS loop; Escalate records inability with no recovery) (design.md Non-Goals).
- Real-time mid-loop adaptation, which would require modifying the DFS loop (out of scope; post-run decision-driven adaptation satisfies the proof goal) (design.md Non-Goals).
- Agent-observable adaptation state (adding a field to Agent); the adaptation is observed via the ledger's `LatestAdaptation`, not an Agent property (design.md Non-Goals).
- An autonomous planner, recovery executor, action selector, authorization layer, and global memory — forbidden by the mission and the frozen invariants (design.md Non-Goals).
- The pre-existing `Capabilities/Perception/Semantic/` scroll-guard failure, a concurrent-work item outside Phase 4 scope to be isolated during verification (proposal.md "Concurrent work"; design.md Risk).

## 5. Final conclusion

**GRADUATED.** The bounded decision-to-hypothesis adaptation loop — immutable `HypothesisAdaptation`, stateless `HypothesisAdapter`, additive ledger `Adapt()`/`LatestAdaptation`, and the one-line additive `DirectiveExecution` integration — is human-authorized with no authority movement, grounded in the change's own records: tasks.md marks all tasks complete (build 0 errors/0 warnings, deterministic suites green 1596+ including SETTINGS-TREE-01, consistency ALL PASS, strict OpenSpec validate PASS) and design.md documents the no-authority decisions and additive-only migration; archival is performed on 2026-08-30 as a separate lifecycle operation in this batch.