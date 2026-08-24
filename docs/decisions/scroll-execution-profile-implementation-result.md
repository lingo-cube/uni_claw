# PROJECT_LEADER_SCROLL_EXECUTION_PROFILE_IMPLEMENTATION_RESULT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_SCROLL_EXECUTION_PROFILE_IMPLEMENTATION — introduce a
> Scroll Execution Profile that controls real-device swipe velocity without
> changing DeviceAction semantics or Agent/Traversal ownership.
>
> **AuthorityDelta: NONE — ArchitectureDelta: ADDITIVE** (Adapter-layer
> execution-parameter derivation only; DeviceAction semantic model unchanged;
> Agent/Planner/GoalEvidence/Semantic/Vision/OCR untouched; no ADB-XML
> compensation; STOP conditions not triggered).

---

## 1. Implementation Summary

**Production** (Adapter layer only):

| file | change |
|------|--------|
| `DeviceActionTranslator.cs` | **Scroll Execution Profile**: `ComputeScrollDurationMs(distance)` — duration = `max(200ms, ⌈distance / 0.9px/ms⌉)` (velocity cap ~900 px/s, upward rounding keeps velocity ≤ cap; 200ms floor keeps tiny steps reliable). `ScrollProfile(stepFraction, height)` exposes (distance, duration, velocity) for observability/tests. `TranslateScroll`/`TranslateScrollBackward` pass the derived duration into `AdbOperation.Swipe`. |
| `AdbOperation.Swipe` | new optional `int? Duration = null` — null keeps the historical adb default (300ms), so non-profile paths are byte-compatible. |
| `AdbDispatchTarget.cs` | swipe command appends the explicit duration when present (`input swipe x1 y1 x2 y2 <duration>`); `Describe` shows the duration. |

- **StepFraction semantics unchanged** — it remains the semantic scroll amount;
  only the physical duration is derived from the actual distance.
- **Default compatibility** — non-Scroll actions (Tap/Back/Launch) untouched;
  Swipe with null Duration keeps the old behavior.
- **Observability** — `ScrollProfile` exposes distance/duration/velocity for
  diagnostics (observation only, never a decision input).

## 2. Boundary Verification

- DeviceAction public semantic model: **unchanged** (ScrollForward/Backward
  still only `StepFraction`; no physical fields leaked).
- Agent / Traversal: **unchanged** (no duration control, no ownership change).
- Non-scroll actions: **byte-compatible** (Duration null → adb default).
- No Settings vocabulary, no scenario knowledge, no ADB-XML compensation.

## 3. AuthorityDelta

**NONE**.

## 4. ArchitectureDelta

**ADDITIVE** — Adapter-internal execution-parameter derivation. No new state
owner, no cross-layer contract, no Agent/Planner/GoalEvidence/Semantic change;
fail-closed preserved.

## 5. Test Result

New `ScrollExecutionProfileTests` (Unit, 10/10 PASS):
- different StepFractions → different distances AND different durations
  (duration is NOT constant — the core fix);
- velocity never exceeds the cap (~900 px/s) across fractions 0.1-2.0, with the
  200ms floor;
- DeviceAction semantics unchanged (ScrollForward/Backward carry only
  StepFraction; Tap/Back translate to duration-free ops);
- the explicit duration reaches the adb command (`input swipe ... <duration>`);
- no Settings vocabulary; vision-only / ADB-independent (pure translation, no
  device, no adb process).

Deterministic regression (adapter, scroll, stability, revisit, environment
suites): **68/68 PASS**. Full regression: **1971 PASS / 1 FAIL (only
ExternalBoundary_RealDevice) / 1972 total** — Capstone real-device PASSES.

## 6. Real Device Comparison (EBD, 3 runs each)

| metric | BEFORE (fixed 300ms) | AFTER (velocity-capped profile) |
|--------|----------------------|--------------------------------|
| Root normalization failure | **3/3 runs** (42s, stuck at "Source normalization is unresolved") | **1/3 runs** (43s) — 2/3 passed normalization |
| Reached Location sub-page + boundary stage | 0/3 | **2/3** (1m19s, entered `Location:uselocation`, dispatched `applocationpermissions`) |
| External page actually opened | — | **yes** (permissioncontroller frames, XML 21-25) |
| OCR abnormal frames | frequent (normalization-blocking garbles) | reduced (normalization pass rate 0% → 67%) |

The velocity-capped swipe materially reduced OCR garbling on the dense
Settings list (normalization pass 0/3 → 2/3). The remaining failure in 2/3
runs is an INDEPENDENT external-transition issue: the permissioncontroller
cold start appears 3-4 observations after the tap, and the bounded settle
budget (6 frames × ~1-3s uiautomator dump interval) can still exhaust before
the external foreground is captured — the transition itself succeeds (external
frames observed), it is the settle-vs-latency margin that fails closed.

## 7. Remaining Risk

- Slower swipes lengthen real-device exploration (EBD already minutes-scale);
  velocity cap 900px/s is a first calibration — tunable with real-device data.
- The external-transition settle budget/latency margin remains an open,
  INDEPENDENT item (permissioncontroller cold start vs uiautomator frame
  interval) — out of this task's scroll-profile scope; reported for a separate
  scoped decision (larger budget or faster frame acquisition).
- Deterministic suites unaffected (no real swipe); any future velocity-cap
  change must re-run the EBD comparison.
