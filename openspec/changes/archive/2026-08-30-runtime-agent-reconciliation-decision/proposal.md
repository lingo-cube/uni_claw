# Proposal: runtime-agent-reconciliation-decision

> Change ID: `runtime-agent-reconciliation-decision`
> Status: Proposed
> Type: Capability extension (additive, no contract/invariant change, no DFS-loop modification)
> Baseline verified: 2026-08-21, branch `uni-agent`, Phase 1 + Phase 2 verified clean
> Authority decision: Leader review passed — NONE authority impact. RuntimeDecision is a passive record;
> HypothesisReconciler is a stateless pure function; the Agent keeps sole authority; the DFS engine is
> unchanged.

## Why

After Phases 1-2, the RuntimeAgent can accept a bounded directive, maintain a run-local execution
hypothesis, observe the outcome, and revise the hypothesis record from trace evidence. But it cannot
**explicitly reconcile** the hypothesis against the observed world and produce a bounded
**RuntimeDecision** answering: "Is my current execution hypothesis still consistent with the observed
world?" and "What bounded execution direction should continue?"

The Phase 2 ledger's `ReviseFromEvidence` maps trace → hypothesis lifecycle, but the decision
(Continue / Revise / Escalate) is implicit in the hypothesis status; it is not a first-class, observable,
lifecycle-tracked model. The mission's proof goal: "RuntimeAgent can reconcile ExecutionHypothesis
against WorldBelief and produce bounded RuntimeDecision without gaining execution authority."

## What Changes

- **NEW** immutable `RuntimeDecision` record (`Model/`) — a passive record of one runtime-level
  decision after reconciliation: RunId, State (Continue/Revise/Escalate), HypothesisReference,
  EvidenceReference, DecisionReason. Carries NO Action, authorization, UI element selection, Goal
  modification, Traversal control, or execution authority.
- **NEW** `RuntimeDecisionState` enum (`Model/`) — Continue=1, Revise=2, Escalate=3.
- **NEW** `HypothesisReconciler` (`Planning/`) — a stateless static pure function
  `Reconcile(ExecutionHypothesis, WorldBelief?, IReadOnlyList<TraceEvent>) → RuntimeDecision`.
  Mirrors the `Reconcile.FromObservation` discipline (stateless, no decision authority). Classifies
  evidence into Continue/Revise/Escalate using generic trace reasons + belief state — NO scenario
  strings.
- **MODIFIED** `ExecutionHypothesisLedger` (additive) — stores the trace reference when
  `ReviseFromEvidence` is called; gains `Reconcile(WorldBelief?) → RuntimeDecision` delegating to
  `HypothesisReconciler`; gains `LatestDecision` property. The ledger remains method-local (not
  Runtime state).
- **MODIFIED** `DirectiveExecution.RunDirectiveAsync` (additive) — inside the existing ContinueWith
  (when ledger is non-null), calls `ledger.Reconcile(agent.Belief)` after `ReviseFromEvidence`.
  **No signature change** — the caller reads `ledger.LatestDecision` after the run.
- **UNCHANGED**: `Agent.OpenWorld.cs`, `Agent.cs`, `Agent.Recovery.cs`, `Container/`, `Traversal/`,
  `Recovery/`, `World/`, `IntentExecution.cs`, all contracts, all frozen invariants. The DFS engine
  is not modified.
- **NEW** deterministic tests: unit (creation, Continue/Revise/Escalate classification, run-local
  isolation), authority (cannot authorize/execute/bypass Agent/alter completion/create recursive
  authority), scenario (3 scenarios: expected child reached → Continue; external boundary → Revise;
  authority boundary exceeded → Escalate).
- **NOT changed**: Architecture v1, Protocol v1, Contract I-1..I-14, charter, `RunStartRequest`,
  `Agent.RunOpenWorldAsync` signature, any frozen decision.

## Capabilities

### New Capabilities
- `runtime-agent-reconciliation-decision`: run-local `RuntimeDecision` model (Continue/Revise/Escalate) +
  stateless `HypothesisReconciler` that reconciles an `ExecutionHypothesis` against `WorldBelief` +
  trace evidence, integrated additively into the existing `ExecutionHypothesisLedger` and
  `DirectiveExecution` entry. Owns no authority; the DFS engine and Agent authority are unchanged.

### Modified Capabilities
<!-- The Phase 2 runtime-agent-plan-hypothesis capability is extended additively (ledger gains a
Reconcile method + LatestDecision property), not spec-level modified. The downstream open-world
execution capabilities are unchanged. -->

## Impact

- **Code**: NEW `src/UniClaw.Runtime/Model/RuntimeDecision.cs`; NEW
  `src/UniClaw.Runtime/Planning/HypothesisReconciler.cs`; MODIFIED
  `src/UniClaw.Runtime/Planning/ExecutionHypothesisLedger.cs` (additive); MODIFIED
  `src/UniClaw.Runtime/Planning/DirectiveExecution.cs` (additive, no signature change).
  `Agent.OpenWorld.cs`, `Agent.cs`, `Container/`, `Traversal/`, `Recovery/`, `World/` — **unchanged**.
- **APIs**: additive only. No existing public signature is broken. `DirectiveExecution.RunDirectiveAsync`
  signature is unchanged (the Reconcile call is inside the existing ContinueWith). The ledger gains
  `Reconcile(WorldBelief?)` + `LatestDecision` (additive).
- **Dependencies**: none new. Stays inside `UniClaw.Runtime` (ArchitectureGuardTests Guard 1/2). The
  reconciler consumes only Model/ types (ExecutionHypothesis, WorldBelief, TraceEvent, RunState).
- **Authority**: NONE. RuntimeDecision is passive; HypothesisReconciler is stateless; Escalate is a
  record not an action. Verified against v1 invariants 2-4, Contract I-2/I-3/I-5/I-12/I-13.
- **Tests**: NEW deterministic tests; Phase 1-2 + existing suites must remain green.
- **Risk**: Low — additive model + stateless function + ledger method; DFS loop untouched.
- **Concurrent work**: the broken `Capabilities/Perception/Semantic/` files (untracked, CS0411) are
  outside Phase 3 scope and will be isolated during verification.
