# RuntimeAgent Reconciliation Decision — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_RUNTIME_AGENT_RECONCILIATION_DECISION` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-runtime-agent-reconciliation-decision/`
> Authority: Runtime Architecture Contract I-1..I-14 (change verified against I-2/I-3/I-5/I-12/I-13 per proposal.md) and Architecture v1 (invariants 2-4 per proposal.md) remain the governing baselines; this decision adds no architecture authority.

## 1. Buyer and exact claim boundary

**Buyer:** Per proposal.md's Why section, the mission's proof goal: "RuntimeAgent can reconcile ExecutionHypothesis against WorldBelief and produce bounded RuntimeDecision without gaining execution authority."

This receipt claims only that:

1. an immutable `RuntimeDecision` record exists in `Model/` (RunId, State, HypothesisReference, EvidenceReference, DecisionReason) with construction-time validation and no Action, authorization, UI element selection, Goal modification, Traversal control, scenario strings, or execution authority (proposal.md "What Changes"; specs/runtime-agent-reconciliation-decision/spec.md "Runtime decision representation");
2. a `RuntimeDecisionState` enum exists with exactly Continue=1, Revise=2, Escalate=3 (proposal.md; spec.md "Decision states");
3. a stateless static pure `HypothesisReconciler.Reconcile(ExecutionHypothesis, WorldBelief?, IReadOnlyList<TraceEvent>) → RuntimeDecision` classifies evidence into Continue/Revise/Escalate using generic trace reasons + belief state only, with no scenario strings (proposal.md; spec.md "Stateless hypothesis reconciliation" and the three classification requirements);
4. `ExecutionHypothesisLedger` is extended additively — stores the trace reference, gains `Reconcile(WorldBelief?)` and `LatestDecision` — and remains method-local, not Runtime state (proposal.md; spec.md "Additive integration without DFS modification");
5. `DirectiveExecution.RunDirectiveAsync` calls `ledger.Reconcile(agent.Belief)` inside the existing ContinueWith with no signature change, and a null ledger preserves Phase 1-2 behavior with zero regression (proposal.md; spec.md "Additive integration without DFS modification");
6. the DFS engine and `Agent.OpenWorld.cs`, `Agent.cs`, `Agent.Recovery.cs`, `Container/`, `Traversal/`, `Recovery/`, `World/`, `IntentExecution.cs` remain unchanged, and the RuntimeAgent remains the sole run-level semantic and execution authority (proposal.md "UNCHANGED"; spec.md "No authority over execution").

No claim is made for: real-time mid-loop reconciliation (would require modifying the DFS loop); an agent-observable decision state field on Agent (the decision is observed via the ledger's `LatestDecision`); wiring the decision into the closed-world path or the `RunStartRequest` wire surface; performing the escalation (Escalate is a record, not an action; the RuntimeAgent does not escalate); or global decision store, persistent decisions, navigation knowledge, and scenario strings (all per design.md "Non-Goals").

## 2. Validation evidence

- tasks.md records all 29 implementation tasks complete (sections 1-9, every checkbox `[x]`).
- tasks.md §8.1 records `dotnet build src/UniClaw.Runtime.sln` (isolated from the concurrent broken `Capabilities/Perception/Semantic/` files) at 0 errors, 0 warnings.
- tasks.md §8.2 records `dotnet test src/UniClaw.Runtime.sln` (isolated) with all deterministic suites green (1537+), including SETTINGS-TREE-01 capstone (TREE-1..TREE-20), U2OpenWorld, OpenWorldTypeDirected, Phase 1 directive tests, Phase 2 hypothesis tests, and ArchitectureGuardTests; only pre-existing env-gated RealDevice/RealEmulator tests are permitted to fail.
- tasks.md §8.3 records `scripts/check-consistency.sh` ALL PASS and `git diff --check` clean.
- tasks.md §9.1 records `openspec validate runtime-agent-reconciliation-decision --strict` passing.
- tasks.md §4.2 records a diff review confirming `Agent.OpenWorld.cs`, `Agent.cs`, `Agent.Recovery.cs`, `Container/`, `Traversal/`, `Recovery/`, `World/`, `IntentExecution.cs` are byte-unchanged.
- tasks.md §5-7 record the new deterministic test coverage: `RuntimeDecisionTests` (fields-only surface, rejects blank RunId/DecisionReason, no authority/UI/Goal-mod/Traversal/scenario surface); `HypothesisReconcilerTests` (Continue/Revise/Escalate classification, determinism, world-free, no scenario strings); `RuntimeDecisionRunLocalIsolationTests` (per-run LatestDecision, no retention in Agent/Container/Traversal/Environment fields); `RuntimeDecisionAuthorityTests` (no method authorizes an action, RunState produced by the DFS engine, GoalEvidence evaluated by the existing evaluator, no recursive authority, Escalate is a record); and three Fake-World scenario suites `ReconciliationScenario1ContinueTests` / `ReconciliationScenario2ReviseTests` / `ReconciliationScenario3EscalateTests` (Continue on expected child reached, Revise on external boundary, Escalate on authority boundary exceeded).
- design.md records Decisions 1-5 (RuntimeDecision as immutable `Model/` record; HypothesisReconciler as stateless static pure function in `Planning/`; Escalate is a record, not an action; ledger stores trace reference and gains `Reconcile` + `LatestDecision`; integration inside the existing ContinueWith with no signature change) and a rollback plan (delete the two new files and revert the two additive modifications — design.md "Migration Plan").

The change directory contains no `evidence/` subdirectory; the build/test/guard records above are task-completion records in tasks.md, not standalone evidence artifacts (no captured build/test output is archived in the change itself).

## 3. Scenario receipts and falsifiers

The change files record no explicit falsifier section (no `evidence/` directory; design.md contains a "Risks / Trade-offs" section of risks with mitigations rather than falsifier outcomes). The rejection/negative requirements are defined in specs/runtime-agent-reconciliation-decision/spec.md:

- "No authority over execution": the RuntimeDecision and HypothesisReconciler MUST NOT acquire any decision, authorization, completion, or execution authority; the RuntimeDecision MUST NOT be consulted by the Agent for decisions, authorization, completion, or execution; the reconciler MUST NOT call any Agent method that mutates run state, authorizes an action, evaluates GoalEvidence, or dispatches a DeviceAction; the RuntimeAgent MUST remain the sole run-level semantic and execution authority; the DFS engine MUST be unchanged. Scenarios: "decision cannot authorize actions" (no method authorizes an action or produces authorization evidence; the Agent's authorization path does not reference the decision), "decision cannot bypass the Agent" (RunState is produced by the Agent's existing DFS engine, not by the decision or reconciler), "decision cannot alter completion" (GoalEvidence is evaluated by the existing evidence evaluator, not by the decision), "decision cannot create recursive authority" (no method dispatches an action, creates a container, or initiates a sub-run; Escalate is a record, not an escalation action).
- "Additive integration without DFS modification": when the ledger is absent (null), the existing Phase 1-2 behavior MUST be preserved with zero regression and no decision is created or recorded; `Agent.OpenWorld.cs`, `Agent.cs`, `Container/`, `Traversal/`, `Recovery/`, and `World/` MUST remain byte-unchanged; the DFS loop MUST not reference the decision or reconciler.
- "Stateless hypothesis reconciliation": the reconciler MUST NOT observe the world, authorize an action, execute anything, modify the Goal or completion, or contain scenario-specific knowledge (scenarios "reconciler is deterministic and world-free" and "reconciler uses no scenario strings").

design.md "Risks / Trade-offs" records the mitigations the change relies on: reconciler misclassification is observable, not destructive, and covered by dedicated classification unit tests; Escalate-as-record is enforced by authority tests asserting the decision model has no dispatch/execute method; the ledger/trace reference staying method-local is enforced by the run-local isolation test; the concurrent broken `Capabilities/Perception/Semantic/` files are isolated during verification (quarantine, rebuild, test, restore) without repairing unrelated work; no real-time reconciliation is accepted as a trade-off because post-run trace-derived reconciliation satisfies the proof goal.

## 4. Deferred scope

The following remain outside this graduation (design.md "Non-Goals"):

- Real-time mid-loop reconciliation (would require modifying the DFS loop).
- Agent-observable decision state (adding a decision field to Agent); the decision is observed via the ledger's `LatestDecision`.
- Wiring the decision into the closed-world path or the `RunStartRequest` wire surface.
- Performing the escalation (Escalate is a record, not an action; the RuntimeAgent records that the situation exceeds its bounded authority).
- Global decision store, persistent decision, navigation knowledge, scenario strings (forbidden).

## 5. Final conclusion

**GRADUATED.** The bounded claim — an immutable `RuntimeDecision` model and a stateless `HypothesisReconciler` integrated additively into the ledger and the directive entry, with zero authority movement and an unchanged DFS engine — is human-authorized and grounded in the change's recorded evidence (all 29 tasks complete per tasks.md, including the build/test/consistency/validate records); archival is performed on 2026-08-30 as a separate lifecycle operation in this batch.
