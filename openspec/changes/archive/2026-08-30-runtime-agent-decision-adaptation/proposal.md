# Proposal: runtime-agent-decision-adaptation

> Change ID: `runtime-agent-decision-adaptation`
> Status: Proposed
> Type: Capability extension (additive, no contract/invariant change, no DFS-loop modification)
> Baseline verified: 2026-08-22, branch `uni-agent`, Phases 1-3 verified clean
> Authority decision: Leader review passed (all 5 stop-conditions) — NONE authority impact.
> HypothesisAdaptation is a passive record; HypothesisAdapter is a stateless pure function; the Agent
> keeps sole authority; the DFS engine and FSM are unchanged.

## Why

After Phases 1-3, the RuntimeAgent can accept a directive, maintain a run-local execution hypothesis,
reconcile it against the observed world, and produce a bounded RuntimeDecision (Continue/Revise/
Escalate). But the loop is **open**: the Decision is produced but never applied back to the hypothesis.
There is no explicit, decision-driven adaptation step that updates the execution hypothesis with a
recorded adaptation reason. The mission's proof goal: "RuntimeAgent can apply a bounded RuntimeDecision
to update its run-local execution hypothesis without gaining planning or execution authority."

## What Changes

- **NEW** immutable `HypothesisAdaptation` record (`Model/`) — a passive record of one bounded
  modification of the execution hypothesis: RunId, AdaptationType (Keep/Replace/Escalate),
  DecisionReference, PreviousHypothesisReference, AdaptedHypothesis, AdaptationReason. Carries NO Plan,
  DeviceAction, Tap instruction, UI element selection, Goal modification, Traversal control, or
  execution authority.
- **NEW** `HypothesisAdaptationType` enum (`Model/`) — Keep=1, Replace=2, Escalate=3.
- **NEW** `HypothesisAdapter` (`Planning/`) — a stateless static pure function
  `Adapt(RuntimeDecision, ExecutionHypothesis) → HypothesisAdaptation`. Mirrors the
  `HypothesisReconciler.Reconcile` discipline (stateless, no decision authority). Maps a decision to a
  bounded hypothesis adaptation: Keep (Continue → confirm), Replace (Revise → new boundary-aware
  hypothesis, NO SystemBack), Escalate (Escalate → record inability, NO recovery). Generic reasons —
  NO scenario strings.
- **MODIFIED** `ExecutionHypothesisLedger` (additive) — gains `Adapt() → HypothesisAdaptation`
  (delegates to `HypothesisAdapter.Adapt(LatestDecision, Current)`, applies the adapted hypothesis to
  `_current`, stores in `LatestAdaptation`); gains `LatestAdaptation` property. The ledger remains
  method-local (not Runtime state).
- **MODIFIED** `DirectiveExecution.RunDirectiveAsync` (additive) — inside the existing ContinueWith
  (when ledger is non-null), after `Reconcile` (Phase 3), calls `ledger.Adapt()`. **No signature
  change** — the caller reads `ledger.LatestAdaptation` after awaiting.
- **UNCHANGED**: `Agent.OpenWorld.cs`, `Agent.cs`, `Agent.Recovery.cs`, `Container/`, `Traversal/`,
  `Recovery/`, `World/`, `IntentExecution.cs`, `HypothesisReconciler.cs`, all contracts, all frozen
  invariants. The DFS engine, FSM, and ExternalBoundary capability are not modified.
- **NEW** deterministic tests: unit (Keep/Replace/Escalate, isolation, history), authority (cannot
  authorize/execute/bypass/modify-Goal/create-Traversal-authority; Replace does not execute SystemBack;
  Escalate does not recover), scenario (3 scenarios).
- **NOT changed**: Architecture v1, Protocol v1, Contract I-1..I-14, charter, `RunStartRequest`,
  `Agent.RunOpenWorldAsync` signature, any frozen decision.

## Capabilities

### New Capabilities
- `runtime-agent-decision-adaptation`: run-local `HypothesisAdaptation` model (Keep/Replace/Escalate) +
  stateless `HypothesisAdapter` that applies a `RuntimeDecision` to produce a bounded hypothesis
  adaptation, integrated additively into the existing `ExecutionHypothesisLedger` and
  `DirectiveExecution` entry. Closes the decision-to-hypothesis loop. Owns no authority; the DFS engine,
  FSM, and Agent authority are unchanged.

### Modified Capabilities
<!-- The Phase 3 runtime-agent-reconciliation-decision capability is extended additively (ledger gains
Adapt + LatestAdaptation), not spec-level modified. The downstream execution capabilities are unchanged. -->

## Impact

- **Code**: NEW `src/UniClaw.Runtime/Model/HypothesisAdaptation.cs`; NEW
  `src/UniClaw.Runtime/Planning/HypothesisAdapter.cs`; MODIFIED
  `src/UniClaw.Runtime/Planning/ExecutionHypothesisLedger.cs` (additive); MODIFIED
  `src/UniClaw.Runtime/Planning/DirectiveExecution.cs` (additive, no signature change).
  `Agent.OpenWorld.cs`, `Agent.cs`, `Container/`, `Traversal/`, `Recovery/`, `World/`,
  `IntentExecution.cs`, `HypothesisReconciler.cs` — **unchanged**.
- **APIs**: additive only. No existing public signature is broken. The ledger gains `Adapt()` +
  `LatestAdaptation` (additive). `DirectiveExecution.RunDirectiveAsync` signature is unchanged.
- **Dependencies**: none new. Stays inside `UniClaw.Runtime` (ArchitectureGuardTests Guard 1/2). The
  adapter consumes only Model/ types (RuntimeDecision, ExecutionHypothesis).
- **Authority**: NONE. HypothesisAdaptation is passive; HypothesisAdapter is stateless; Replace does
  not execute SystemBack; Escalate does not recover. Verified against v1 invariants 2-4, Contract
  I-2/I-3/I-5/I-12/I-13.
- **Tests**: NEW deterministic tests; Phases 1-3 + existing suites must remain green.
- **Risk**: Low — additive model + stateless function + ledger method + one line in ContinueWith.
- **Concurrent work**: the `Capabilities/Perception/Semantic/` tree now compiles; its pre-existing
  scroll-guard failure is outside Phase 4 scope and will be isolated during verification if needed.
