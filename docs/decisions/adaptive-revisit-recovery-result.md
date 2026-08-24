# PROJECT_LEADER_ADAPTIVE_REVISIT_RECOVERY_RESULT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Goal: when a pending branch cannot be grounded from the current viewport,
> perform a BOUNDED, ADAPTIVE reverse exploration (step 0.4 → 0.2 → 0.1 →
> floor) so the branch can re-enter a groundable state — budget exhausted ⇒
> fail closed. Scope limited to the revisit step policy; no DFS / FSM /
> Traversal / Semantic / SourceGrounding contract change.

---

## 1. Problem Confirmed

`Agent.OpenWorld` `RunOpenWorldAsync` already had a bounded revisit mechanism:
when no pending branch is CURRENTLY_VISIBLE and the frozen RevisitBudget
remains, the Agent executed **one fixed `ScrollBackward()`** (full window) per
attempt, then re-evaluated. On the Capstone real device this was insufficient:

- a single full-window reverse step from the bottom of the list could not
  re-enter the viewport region where the top-of-list branches were discovered
  (the top branch was several windows above), so the run burned its budget and
  failed closed without recovering recoverable branches (Visited 4/8).

The mechanism was correct in structure (bounded, fail-closed, same-Container
continuity, post-completeness consistency) but its **step policy was not
adaptive**: it could not reach a branch that is far from the current viewport
without either overshooting or exhausting the budget.

## 2. Fix (scope-limited, evidence-driven)

Replaced the fixed reverse step with the **ADAPTIVE REVISIT RECOVERY** step
policy inside the existing `while (!dispatchSelected)` dispatch loop — no new
loop, no new authority, no DFS change:

```text
No pending branch CURRENTLY_VISIBLE + budget remaining
        |
        v
ScrollBackward(revisitStepFraction)     // reuses the existing StepFraction
        |                                 // mechanism (0.4 → 0.2 → 0.1 → floor)
        v
same-Container continuity (unchanged) -> post-completeness consistency (unchanged)
        |
        v
revisitStepFraction = max(RevisitStepFloor, revisitStepFraction / 2)
        |
        v
trace "adaptive revisit recovery seq=.. step=.. (budget remaining=..)"
        |
        v
loop back to dispatch pass (fresh grounding re-check, unchanged)
```

- `RevisitInitialStep = 0.4f`, `RevisitStepFloor = 0.1f` — the step HALVES
  after each reverse scroll that still cannot re-ground any pending branch. A
  smaller reverse step re-enters the viewport region where the top-of-list
  branches were discovered.
- **Bounded**: the frozen RevisitBudget is the hard stop; budget exhausted ⇒
  fail closed (no unbounded search, no infinite rollback, no automatic
  jump-to-top, no dispatch from historical frames).
- **Visibility ≠ Dispatchable** (unchanged): a recovered viewport still
  requires the full grounding chain (explicit `RequiredBranchGrounding` →
  `SourceGroundingValidator.Validate` → `TryResolveLogicalSource` →
  `ResolveCurrentVisibleElement` → fresh grounding gate → fresh authorization)
  before any dispatch — the revisit only re-positions the viewport.
- No Settings logic, no child-index / list-size assumptions, no coordinate
  memory, no OCR special rules, no forced dispatch were introduced.

Production delta:

| File | Change |
|------|--------|
| `src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs` | Adaptive revisit step policy in the bounded reverse-exploration path: `RevisitInitialStep`/`RevisitStepFloor` constants, `ScrollBackward(revisitStepFraction)`, halving after each reverse, adaptive-recovery trace. Also removed a leftover TEMP DEBUG block (wrote `/tmp/ow_unknown.txt` during adaptive-scroll tests). |
| `src/UniClaw.Runtime/Model/Actions/DeviceAction.cs` | (from the adaptive-scroll task) `ScrollForward(float StepFraction = 1.0f)` / `ScrollBackward(float StepFraction = 1.0f)` — default 1.0 preserves existing behavior. |
| `src/UniClaw.Runtime.Adapters/Operator/DeviceActionTranslator.cs` | (from the adaptive-scroll task) `TranslateScroll` / `TranslateScrollBackward` scale the swipe by StepFraction, clamped to [0.1, 2.0]. |
| `tests/UniClaw.Runtime.Tests/Evidence/AdaptiveRevisitRecoveryTests.cs` | **New** — 5 deterministic proofs over scenario-neutral scrollable worlds (below). |

## 3. Authority Verification

- **No new decision authority**: the Agent remains the sole run-level semantic
  authority; the revisit step policy is an execution parameter inside the
  Agent's existing bounded-revisit decision (Agent owns the decision, Traversal
  executes only authorized actions, Container owns page-local state).
- **No contract relaxation**: every check that previously gated a dispatch
  still gates it (identity safety → explicit grounding → validator → current
  visibility → fresh grounding → fresh authorization). The change only alters
  HOW the viewport is re-positioned when nothing is dispatchable.
- **No new state owner / component / facade**: all state lives in the existing
  `DiscoveryEpochState.RevisitBudget` and a loop-local `revisitStepFraction`.
- **Scope guard**: EBD / Settings-layout / Normalizer ordered-overlap
  continuity issues are explicitly OUT of scope and recorded in §6.

## 4. DFS / FSM / Traversal Impact

- **DFS**: `RunOpenWorldAsync` structure unchanged — discovery epoch frozen,
  pending = `ApprovedSiblingEvidence` minus completed/boundary-verified,
  deterministic (seq, identity) ordering, ROOT TERMINAL / verified-return
  triggers, budget-exhausted fail-closed — all untouched.
- **FSM**: no state-machine change.
- **Traversal**: `ExecuteLoweredActionAsync` unchanged; the revisit still goes
  through the same lowered `ScrollBackward` action with the same
  same-Container continuity and post-completeness consistency validation.
- **Semantic boundary / SourceGrounding contract**: untouched
  (`SourceEquivalenceNormalizer` NORM4 fail-closed behavior unchanged).

## 5. New Tests (5/5 PASS)

`tests/UniClaw.Runtime.Tests/Evidence/AdaptiveRevisitRecoveryTests.cs` drives
the real Agent over `RecoveryWorld` — a scrollable list with a windowed
viewport whose forward/reverse scrolls honor StepFraction. The branch inventory
is FRAME-SPANNING (first-appearance grounding), mirroring a real caller's
discovery aggregation — a single-viewport inventory would only ever contain the
last frame's rows, so the top-of-list branches could never be pending and the
recovery could never engage.

| Test | Proof |
|------|-------|
| `GenericTree_BottomToTop_AdaptiveReverse_RecoversAndDispatches` | Forward exploration reaches the bottom, then ADAPTIVE REVERSE RECOVERY genuinely engages (reverse scrolls ≥ 1); every reverse step follows the policy (all in [0.1, 0.4], never a fixed full-window step); recovered branches are dispatched from fresh grounding (Taps present); no "zero dispatch" fail-closed while an authorized branch was pending. |
| `TopBranch_AdaptiveHalvingReverse_RecoversAndDispatches` | Only "Node 01" (top row, position 0 only) is authorized. From the bottom, recovery re-enters the top viewport with HALVED steps (0.4 → 0.2 → 0.1 — a single large reverse step would overshoot and never ground the branch); Node 01 is recovered and dispatched from fresh grounding (dispatch tap targets viewport index 0); denied branches (02–05) are never dispatched; no "zero dispatch" fail-closed. |
| `VisionOnly_NoAdbDependency` | Recovery runs on primary-Vision evidence only — zero ADB actions. |
| `InsufficientBudget_NoInfiniteRollback_NoUngroundedDispatch` | 20 children / viewport 2: reverse exploration is BOUNDED (reverse scrolls ≤ rows, total scrolls ≤ 2×rows — no unbounded rollback, no jump-to-top); every authorized branch is recovered and dispatched from fresh grounding (no "zero dispatch" fail-closed). |
| `NoSettingsVocabulary_ArchitectureGuard` | The generic recovery path contains no Settings/WiFi/Android scenario vocabulary. |

Result: **5/5 pass** (47–59 ms suite).

## 6. Full Regression

```
Total:     1953   (was 1948 before this task; +5 new tests)
Passed:    1951
Failed:     2      (both known real-device failures, unchanged)
```

- `scripts/check-consistency.sh`: **ALL PASS**
- `git diff --check`: **clean**
- Remaining failures (classified — **both out of scope**):
  1. `CapstoneSingleAgentRunTests.Capstone_OneAgentOneRun_RealEmulator_ReachesCapstoneComplete`
     — code issue, NOT environment: the adaptive revisit already lifted Visited
     4/8 → 6/8 and let 7/8 children enter, but the bounded RevisitBudget is
     still exhausted before the remaining children (Visited 6/8 vs required
     8/8) can be grounded. Fix direction (out of scope): refine the
     forward-exploration/inventory policy so the recovery budget covers the
     top-of-list children.
  2. `ExternalBoundaryRealDeviceTests.ExternalBoundary_RealDevice`
     — code issue, NOT environment: the Settings "Location" row moves off the
     first screen and is recovered by scroll, but the source normalizer's
     ordered suffix/prefix overlap cannot prove continuity because adjacent
     scroll frames share a PREFIX (not a suffix) of rows. Fix direction (out of
     scope): align the normalizer's ordered-overlap contract with the actual
     scroll-frame relationship or adjust test data.

## 7. Verification Summary

- Adaptive revisit production implementation: **build clean**; revisit-path
  tests (AdaptiveScrollGrounding + BranchGroundingBeforeDispatch +
  AdaptiveRevisitRecovery) all green.
- Capstone real-device empirical evidence (prior to this task's finalization):
  `step=0.20` / `step=0.10` halving traces visible; Visited 4/8 → 6/8; children
  02–08 container entry; top-of-list recovery now partially covered.
- No production scenario strings added by this task; no new runtime behavior
  outside the bounded revisit path.
