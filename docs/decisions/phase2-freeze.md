# Phase 2 Freeze — Trap & Recovery Architecture

> 状态: Frozen | 日期: 2026-08-08
> 依据: Phase 2E independent acceptance CONDITIONAL_PASS (implementation verified, docs reconciled)
> 前提: Phase 2A/B/C/D 全部完成 (158/158 tests, 8/8 guards, 3/3 formal scenarios proven)

## Frozen

### Trap Model (HG-2)
- **Exactly 7 fields**: Kind / Scope / Expected / Observed / Source / Evidence / LastAction
- Expected/Observed = `long?` (observation sequence references, NOT Observation snapshots — I-13)
- Explicitly excluded: Recoverability / Confidence / Severity / Timestamp / HistoricalMemoryFields
- Trap is an immutable sealed record — no behavior methods, no RunState, no recovery logic

### Recovery Ownership (HG-4)
- **Recovery component** owns: mechanism state (recipe action list, execution cursor, post-recovery observation, verification result)
- **Agent** owns: all decision authority (initiate recovery, verify, resume, terminate)
- Agent composes Recovery; Recovery never references Container/Traversal/Agent

### Recovery Boundary (HG-1, HG-5)
- Recovery → Container: FORBIDDEN (Guard 7)
- Recovery → Traversal: FORBIDDEN (Guard 7)
- RecoveryRequest / RecoveryPlanner / RecoveryRuntime: FORBIDDEN everywhere (Guard 5b, HG-5)
- Recovery scope: Anchor → Restore → Observe → Verify → RecoveryResult → Agent decision

### Agent Authority
- Final RunState owner (I-2)
- Recovery initiation / resume / failure termination authority (I-3, I-8)
- Goal completion authority (I-10)
- Drift detection authority (HG-3: no DriftStatus field)

### Traversal Retry Boundary
- Step-scope retry authority (I-8 对偶: handle locally, don't escalate)
- Bounded by `maxRetries` (default 0 = Phase 1 behavior preserved)
- Re-observe + re-resolve only — NO action dispatch during retry
- Exhaustion → Phase 1 `Failed` path — NO Trap, NO Recovery

### Environment Contract
- IEnvironment is the sole external world port
- ObserveAsync / ExecuteAsync unchanged from Phase 1
- Fake/ScriptedEnvironment exists only in tests/
- Dispatch result ≠ world success (裁决 10)

### Scenario Receipts
- All Phase 2 production deltas have Scenario Receipts (11 items audited)
- No unreceipted production complexity (I-12)
- Deferred items confirmed absent (RecoveryRequest, DriftStatus, coordinate, FSM, etc.)

## Phase 3 Cannot Modify Without

1. **Scenario** — a new scenario that proves the current model insufficient
2. **Semantic Gate** — scenario-architect confirms the semantic gap
3. **OpenSpec Reconciliation** — proposal/design/specs/scenarios/tasks updated

Specifically, the following Phase 2 decisions may NOT be reversed by Phase 3 without formal gate review:
- Recovery ownership split (HG-4 Option B)
- Trap 7-field shape (HG-2)
- Recovery → Container/Traversal ban (Guard 7)
- RecoveryRequest/Planner/Runtime exclusion (HG-5)
- No DriftStatus field (HG-3)

## Verification Baseline

```
dotnet build src/UniClaw.Runtime.sln:  0 warnings, 0 errors
dotnet test src/UniClaw.Runtime.sln:   158/158 PASS
Architecture Guards:                    8/8 PASS
scripts/check-consistency.sh:          ALL PASS
Deterministic replay:                  SC-P1-001 / SC-P2-001/002/003
```

## State

```
PHASE_2_FROZEN
```
