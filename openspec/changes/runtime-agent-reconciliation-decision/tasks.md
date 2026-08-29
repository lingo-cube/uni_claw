# Tasks: runtime-agent-reconciliation-decision

> Implementation checklist. Each task is verifiable against
> `specs/runtime-agent-reconciliation-decision/spec.md`. Order respects dependencies: model → reconciler →
> ledger extension → integration → tests → regression → validate.

## 1. Runtime decision model

- [x] 1.1 Create `src/UniClaw.Runtime/Model/RuntimeDecision.cs`: `RuntimeDecisionState` enum
      (Continue=1, Revise=2, Escalate=3) + sealed record `RuntimeDecision` (RunId, State,
      HypothesisReference, EvidenceReference, DecisionReason).
- [x] 1.2 Add construction-time validation: non-blank RunId, HypothesisReference, EvidenceReference,
      DecisionReason; State defined. Reject invalid with ArgumentException.
- [x] 1.3 Assert the record carries NO Action, authorization, UI element, Goal modification, Traversal
      control, or scenario strings (model-level test in task 5).

## 2. Stateless hypothesis reconciler

- [x] 2.1 Create `src/UniClaw.Runtime/Planning/HypothesisReconciler.cs`: `static` class with
      `Reconcile(ExecutionHypothesis hypothesis, WorldBelief? belief, IReadOnlyList<TraceEvent> trace)
      → RuntimeDecision`. Pure function, no state, no authority.
- [x] 2.2 Implement Continue classification: hypothesis Status is Confirmed or Active (not
      Replaced/Revised), belief SemanticPage is non-null, trace shows in-scope progress (inventory
      complete / verified return) without EXTERNAL_BOUNDARY_OBSERVED. Generic reason from evidence.
- [x] 2.3 Implement Revise classification: trace shows EXTERNAL_BOUNDARY_OBSERVED, OR hypothesis
      Status is Revised, OR belief SemanticPage is null (unknown) but not a terminal
      authority-boundary failure. Generic reason from the contradicting evidence.
- [x] 2.4 Implement Escalate classification: detect authority-boundary failure indicators in trace
      reasons (identity safety, depth cutoff, boundary not handled) with a failed outcome, OR
      hypothesis Status is Revised AND the run outcome is Failed. Escalate is a RECORD, not an action.
- [x] 2.5 Ensure NO scenario strings in any decision reason — derive from generic trace event reasons
      + belief state only.

## 3. ExecutionHypothesisLedger extension (additive)

- [x] 3.1 Modify `src/UniClaw.Runtime/Planning/ExecutionHypothesisLedger.cs`: store the trace
      reference (IReadOnlyList<TraceEvent>) as a private field when `ReviseFromEvidence` is called.
- [x] 3.2 Add `Reconcile(WorldBelief? belief) → RuntimeDecision` method: delegates to
      `HypothesisReconciler.Reconcile(Current, belief, <stored trace>)`, stores the result in a new
      `LatestDecision` property, and returns it.
- [x] 3.3 Add `LatestDecision` property (RuntimeDecision? — null until Reconcile is called).
- [x] 3.4 Confirm the ledger remains method-local (not assigned to any Agent/Container/Traversal/
      Environment field) — enforced by the authority test in task 6.

## 4. DirectiveExecution integration (additive, no signature change)

- [x] 4.1 Modify `src/UniClaw.Runtime/Planning/DirectiveExecution.cs`: inside the existing ContinueWith
      (when hypothesisLedger is non-null), after `ReviseFromEvidence`, call
      `hypothesisLedger.Reconcile(agent.Belief)`. No signature change to RunDirectiveAsync.
- [x] 4.2 Confirm `Agent.OpenWorld.cs`, `Agent.cs`, `Agent.Recovery.cs`, `Container/`, `Traversal/`,
      `Recovery/`, `World/`, `IntentExecution.cs` are byte-unchanged (diff review).

## 5. Unit tests

- [x] 5.1 `RuntimeDecisionTests`: construction exposes only decision fields; rejects blank
      RunId/DecisionReason; carries no Action/authorization/UI/Goal-mod/Traversal-control/scenario
      (surface assertion).
- [x] 5.2 `HypothesisReconcilerTests`: Continue (hypothesis Confirmed/Active + belief non-null +
      in-scope trace, no boundary); Revise (boundary observed / hypothesis Revised / belief null
      non-terminal); Escalate (failed + authority-boundary reason / Revised + Failed). Deterministic
      (two reconciliations structurally equal). World-free (no observation dispatched). No scenario
      strings in reasons.
- [x] 5.3 `RuntimeDecisionRunLocalIsolationTests`: the ledger's LatestDecision is per-run; two
      separate runs produce independent decisions with no cross-contamination; the ledger (and its
      LatestDecision) is not retained in any Agent/Container/Traversal/Environment field after the run.

## 6. Authority tests

- [x] 6.1 `RuntimeDecisionAuthorityTests`: the decision model and reconciler expose NO method that
      authorizes an action or produces authorization evidence; the Agent's authorization path does not
      reference the decision (source assertion).
- [x] 6.2 The RunState is produced by the Agent's existing DFS engine, not by the decision or reconciler
      (Fake-env end-to-end: run with ledger → assert RunState equals the DFS engine's result; the
      decision only records, never decides).
- [x] 6.3 The GoalEvidence is evaluated by the existing evidence evaluator, not by the decision (assert
      the decision state reflects the outcome but does not determine it).
- [x] 6.4 The decision model and reconciler expose NO method that dispatches an action, creates a
      container, or initiates a sub-run (no recursive authority); Escalate is a record, not an action.

## 7. Scenario tests (Fake World)

- [x] 7.1 `ReconciliationScenario1ContinueTests`: hypothesis expects child transition; observation
      shows expected child reached (trace: in-scope inventory complete / verified return; belief
      SemanticPage non-null) → decision Continue. Assert: execution authority unchanged.
- [x] 7.2 `ReconciliationScenario2ReviseTests`: hypothesis expects recursive child; observation shows
      external boundary (trace: EXTERNAL_BOUNDARY_OBSERVED) → decision Revise. Assert: RuntimeAgent
      does not decide the boundary action; existing Agent authority remains responsible.
- [x] 7.3 `ReconciliationScenario3EscalateTests`: hypothesis expects execution possible; observation
      shows authority boundary exceeded (run Failed with identity-safety / depth-cutoff / boundary
      reason, or Revised + Failed) → decision Escalate. Assert: Escalate is a record; the RuntimeAgent
      does not perform an escalation action.

## 8. Regression guard

- [x] 8.1 Run `dotnet build src/UniClaw.Runtime.sln` (isolated from concurrent broken
      `Capabilities/Perception/Semantic/` files) — 0 errors, 0 warnings.
- [x] 8.2 Run `dotnet test src/UniClaw.Runtime.sln` (isolated) — all deterministic suites green
      (1537+), including SETTINGS-TREE-01 capstone (TREE-1..TREE-20), U2OpenWorld, OpenWorldTypeDirected,
      Phase 1 directive tests, Phase 2 hypothesis tests, ArchitectureGuardTests. Only pre-existing
      env-gated RealDevice/RealEmulator tests may fail.
- [x] 8.3 Confirm `scripts/check-consistency.sh` ALL PASS and `git diff --check` clean.

## 9. OpenSpec validate

- [x] 9.1 Run `openspec validate runtime-agent-reconciliation-decision --strict` — passes.
- [x] 9.2 Update this `tasks.md` checkbox state as each task completes.

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime/Planning/` | [docs/system/layers/planning.md](../../../docs/system/layers/planning.md) |
| `src/UniClaw.Runtime/Model/` (immutable models) | [docs/system/greenfield-runtime-charter.md](../../../docs/system/greenfield-runtime-charter.md) §40 + `src/UniClaw.Runtime/AGENTS.md` directory table |
| `src/UniClaw.Runtime/World/` (reconciliation pattern reference) | [docs/system/layers/](../../../docs/system/layers/) (Reconcile.FromObservation pattern) |
