# PROJECT_LEADER_EXTERNAL_BOUNDARY_TRANSITION_SETTLE_FIX_RESULT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_EXTERNAL_BOUNDARY_TRANSITION_SETTLE_FIX — stop the
> ExternalBoundary flow from misjudging a SUCCESSFUL external transition as
> failure because it judged the FIRST post-action frame.
>
> **AuthorityDelta: NONE — ArchitectureDelta: ADDITIVE** (Agent external-
> transition handler only; no normal-dispatch / scroll-stability / OCR-Vision /
> Semantic / ADB-primary / ownership / scenario-knowledge changes).

---

## 1. Human Symptom

点击 "App location permissions" 后系统**实际进入了外部权限页**
（com.android.permissioncontroller），但 Runtime 用 tap 后**第一帧**判断外部
状态——第一帧仍在原应用（转场中）→ 误判失败，流程中止。

## 2. Evidence Confirmation

- 外部页面真实出现：逐帧 uiautomator 证据显示
  `com.android.permissioncontroller` 帧在 tap 后若干帧持续出现
  （"Location / Apps with this permission... / Allowed all the time"）。
- 失败不是 Action（tap 成功）、Vision/Grounding（正确）、Foreground
  detection（后续帧正确识别外部包）——而是**判断时机**：第一帧仍在 owned。
- 普通 branch dispatch 有 `SettlePostActionObservationAsync`（bounded）；
  external boundary 的进入判断原本直接读第一帧（其返回判断反而有 settle）。

## 3. Implementation Summary

**Production** (`src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs`):

| change | detail |
|--------|--------|
| `SettleExternalTransitionAsync` (new) | BOUNDED external-transition settle: a CANDIDATE frame whose foreground has LEFT the owned application, CONFIRMED by a consecutive frame with the SAME external foreground (stable external identity). Budget `MaxExternalTransitionObservations = 6` — larger than the same-app settle (3) because a cross-application cold start is slower (real-device evidence: the external activity appeared 3-4 observations after the tap). Budget exhausted → fail closed (never assume success). |
| `TryHandleExternalBoundaryAsync` | entering judgment now uses the settle's CONFIRMED external frame (`externalFrame`) instead of `firstPostAction`; the SystemBack action executes with `externalFrame` as context. Fail message: "did not settle into an external foreground: ...". |

**Tests** (`tests/UniClaw.Runtime.Tests/Scenario/ExternalBoundaryTests.cs`):

| change | detail |
|--------|--------|
| `BoundaryWorld` | `DelayExternalTransition` (foreground stays on owned app for 2 frames after the tap, then the external activity appears and stays stable) and `ExternalNeverAppears` (the external page never opens) switches; `SystemBackCount`. |
| `EBD19_ExternalTransitionDelayed_SettlesAndRecognized` | delayed transition: the boundary is OBSERVED, the obligation records `ExternalForeground == ExternalApp`, exactly one SystemBack, verified return — no first-frame premature failure (semantic assertions, no fixed frame counts). |
| `EBD20_ExternalTransitionNeverAppears_FailsClosed` | the external page never opens: bounded settle exhausts → fail closed (no SystemBack, no obligation, no assumed success). |
| existing EBD1-EBD18 | re-verified — the immediate-transition worlds settle on the 2nd frame; assertions are transition-completion semantics (obligation/SystemBack/verified-return), not first-frame success/fail. |

## 4. AuthorityDelta

**NONE** — Agent authority, DFS ownership, Traversal, GoalEvidence, Lifecycle
ownership unchanged; no scenario knowledge; no auxiliary-data bypass.

## 5. ArchitectureDelta

**ADDITIVE** — a bounded transition confirmation inside the Agent's existing
external-transition handler, mirroring the ordinary dispatch settle. Not
BREAKING; fail-closed preserved.

## 6. Test Result

- New `EBD19`/`EBD20`: **2/2 PASS** (delayed external transition recognized;
  never-appearing transition fails closed; semantic assertions).
- EBD deterministic suite (EBD1-EBD20): **20/20 PASS**.
- Broad deterministic sweep (stability, revisit, OpenWorld, provenance,
  U2OpenWorld, settle, scroll-artifact, Capstone formal, Settings):
  **133/133 PASS**.
- Full regression: **1961 PASS / 1 FAIL / 1962 total** — the only failure is
  `ExternalBoundary_RealDevice` (see §7).

## 7. Real Device Result

- **Capstone**: PASS (full suite).
- **ExternalBoundary**: the transition-settle fix is in place and
  deterministic-proofed; real-device evidence confirms the external page
  appears and stays (permissioncontroller frames). The remaining real-device
  failures observed across repeated runs are dominated by an INDEPENDENT,
  known real-device limitation: **OCR random garbling breaks the root
  exploration's source normalization** (the harness's OCR-only canonical
  evidence assembly + scroll-stability confirmation already maximize
  mitigation, but character-level OCR noise cannot be fully eliminated
  generically; runs fail at "Source normalization is unresolved" before
  reaching the boundary stage). This is not the transition settle — the
  settle path itself was reached in earlier runs and failed only on the
  budget-vs-latency margin (fixed by the larger external budget).

## 8. Remaining Risk

- **OCR random garbling** (independent, known): EBD real-device passes are not
  deterministic until the perception layer stabilizes dense-list OCR; the
  harness-level canonical assembly is the current mitigation.
- **Transition latency vs budget**: `MaxExternalTransitionObservations = 6`
  covers the observed 3-4-frame delay with margin; a cold-start slower than
  the budget fails closed (conservative, never assumed success).
- EBD assertions remain semantic (external foreground observed, exactly one
  SystemBack, external never a container) — no weakening.
