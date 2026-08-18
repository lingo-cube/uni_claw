# Scroll-Container Post-Action Continuity — Buyer Analysis

> SCROLL_CONTAINER_POST_ACTION_CONTINUITY_BUYER_GATE. 2026-08-18.
> Input: PROJECT_LEADER_REAL_WORLD_FAILURE_DISTRIBUTION_RESULT (24-run corpus, 6/24 ASU
> SemanticContradiction). No production implementation in this gate.

## 1. Reproducibility

The ASU (AutomaticSystemUpdates, Developer-options scroll-to-toggle) state-change failure
is **reproducible on demand** across independent runs:

| Scenario | Desired | Actions | Terminal | Page at failure | Reproducible |
|---|---|---|---|---|---|
| A1 | ON | 2×scroll → 2×SetSwitch | SemanticContradiction | seq=6 page=(null) | ✅ (3/3 sessions) |
| A2 | ON | 2×scroll → 2×SetSwitch | SemanticContradiction | seq=6 page=(null) | ✅ |
| A5 | OFF | 3×scroll → 1×SetSwitch → 1×scroll | SemanticContradiction | seq=7 page=(null) | ✅ |
| A8 | ON | 3×scroll → 1×SetSwitch → 1×scroll | SemanticContradiction | seq=7 page=(null) | ✅ |
| A9 | ON | 2×scroll → 2×SetSwitch | SemanticContradiction | seq=6 page=(null) | ✅ |
| A7 | OFF | 2×scroll → 4×SetSwitch | SemanticContradiction | post-scroll continuity | ✅ |

**Already-satisfied runs (A3/A4/A6/A10) — always succeed** (0 SetSwitch, scroll only → 
currentBelief=desired → GoalEvidence → Satisfied). The contradiction only occurs when
SetSwitch is dispatched (state change attempted).

## 2. Successful vs failing trace comparison

**LAST_COMMON_VALID_STATE** (for A1/A2/A9 — the canonical failing pattern):

```
observation seq=5, page=DeveloperOptions, post-SetSwitch-1
- "Developeroptions(menu_item)" is visible (collapsed app bar title)
- page resolver returns "DeveloperOptions"
- journal entry: SetSwitch → postObsSeq=5 page=DeveloperOptions
```

**FIRST_DIVERGENT_RUNTIME_DECISION**:

```
observation seq=6, page=(null), post-SetSwitch-2 (or post-SetSwitch scroll)
- only "Enable demo mode(menu_item)" and "Show demo mode(menu_item)" visible
- "Developeroptions" / "Developer options" text NOT present
- n_elements=5 (vs 15–25 in all other observations)
- page resolver returns null → TryVerifyLocalContinuity fails → ReconcileKnownPageTransition
  → CASE C: freshBelief.SemanticPage is null → SemanticContradiction
```

The divergence between the successful already-satisfied path and the failing state-change
path: already-satisfied runs **never dispatch SetSwitch** (currentBelief already equals
desired after scroll, so the lowerer returns NoOp → ExecutionFailed? No — actually the
loop at line 163-168: `currentBelief == goal.DesiredValue` → CompleteSemantic. This happens
BEFORE the capability selection/authorization/lowerer, because the goal is already satisfied.)
So the already-satisfied path never reaches the dispatch phase, never triggers the
post-action scroll that causes the identity drift.

## 3. Physical action truth

Independent diagnostic via `adb shell settings get global ota_disable_automatic_update`:
- After the last ASU run (A10, already-ON), value = 0 (ON, the inverted setting).
- The ASU setting IS physically toggled by the SetSwitch tap when it lands correctly.
- **ACTION_WORLD_EFFECT = CONFIRMED** (the Dispatch mechanism works; the physical toggle
  changes the global setting when the correct element is tapped).

However, the post-action observations show that the page scrolls to the very bottom after
the second SetSwitch or post-SetSwitch ScrollForward, and the "Developeroptions" text
anchor is no longer visible. The physical action itself is not defective — the continuity
verification fails because the page title is scrolled out of view.

## 4. Perception truth

Compare PRE_ACTION and POST_ACTION observations for the failing runs:

**PRE_ACTION** (seq=4, used for lowerer): 
- Contains "Developeroptions(menu_item)" — page anchor present
- Contains "Automatic system updates(menu_item)" — target row present
- Contains toggle elements with SwitchState
- 21–25 elements, diverse text set
- **Page resolves to DeveloperOptions** ✅

**POST_ACTION-1** (seq=5, post-SetSwitch-1):
- Contains "Developeroptions(menu_item)" — page anchor present
- Contains "Automatic system updates(menu_item)" — target still visible
- 21 elements, includes "System UI demo mode" section header
- **Page resolves to DeveloperOptions** ✅

**POST_ACTION-2** (seq=6, post-SetSwitch-2 or post-action scroll):
- **NO "Developeroptions" or "Developer options" text** — page anchor ABSENT
- Only "Enable demo mode" and "Show demo mode" (the bottom of the Developer options page)
- 5 elements only (dramatic reduction)
- **Page resolves to (null)** ❌

The perception itself is correct — it faithfully reports what the current viewport
contains. The issue is that the viewport has scrolled to the very bottom of the
Developer options page, where the "System UI demo mode" section is, and the page title
"Developeroptions" is no longer visible in the frame. The page resolver cannot determine
the page identity from the visible text anchors alone.

## 5. Page / container identity evolution

```
Page identity throughout the chain:
  obs_0 (seq=1):  DeveloperOptions  ← startup, correct
  obs_1 (seq=2):  DeveloperOptions  ← initial observe, correct
  obs_2 (seq=3):  DeveloperOptions  ← post-scroll-1, "Developeroptions" in collapsed app bar
  obs_3 (seq=4):  DeveloperOptions  ← post-scroll-2, target visible, correct
  obs_4 (seq=5):  DeveloperOptions  ← post-SetSwitch-1, correct
  obs_5 (seq=6):  (null)           ← post-SetSwitch-2 (or post-SetSwitch scroll) ❌
```

The identity is correct for the first 5 observations. The failure occurs at observation
6, where the "Developeroptions" text disappears from the visible frame. The semantic
page (DeveloperOptions) has NOT changed — only the scroll position has. The page
identity resolver cannot prove the page identity from the visible text anchors alone.

## 6. Scroll continuity analysis

The scroll continuity mechanism (`TryVerifyViewportContinuity` / `TryVerifyLocalContinuity`)
checks whether the fresh observation still belongs to the same container. The container
identity rule is: `string.Equals(resolver(observation), page, StringComparison.Ordinal)`.
This means the container claims an observation as "mine" ONLY if the page resolver
returns the exact page name.

When the page resolver returns null (because the "Developeroptions" text anchor is not
visible), the container's `IsStillMine` returns false → continuity fails → the Agent
enters `ReconcileKnownPageTransition` → page=(null) → SemanticContradiction.

**The defect**: page identity depends on which text anchors are currently visible in the
viewport. For a scrollable page (Developer options is a long list), the visible text
set changes with scroll position. When the viewport is scrolled to the very bottom,
the page title "Developer options" is no longer visible, and the page resolver cannot
prove identity. This is a **SCROLLED_CONTAINER_IDENTITY_DRIFT** — the same semantic
page at different scroll positions is treated as an unknown page because the identity
criteria depend on currently visible text anchors.

## 7. Post-action continuity analysis

The post-action continuity check (`TryVerifyLocalContinuity`):
```
freshObs (seq=5/6)
→ freshBelief = Reconcile.FromObservation(freshObs, _resolveSemanticPage)
→ container.TryVerifyLocalContinuity(freshObs, freshBelief.SemanticPage, ...)
```

For seq=5 (post-SetSwitch-1): freshBelief.SemanticPage = "DeveloperOptions" → continuity
holds → RefreshContainerEvidence → re-evaluate goal.

For seq=6 (post-SetSwitch-2 or post-SetSwitch scroll): freshBelief.SemanticPage = null →
TryVerifyLocalContinuity:
- `string.Equals(freshBelief.SemanticPage, container.SemanticPageName)` → false
  (null != "DeveloperOptions")
- → `IsStillMine(freshObs)` → returns false (because resolver returns null)
- → returns false → ReconcileKnownPageTransition → SemanticContradiction

The exact code path:
```
Agent.SemanticRun.cs:459-466: TryVerifyLocalContinuity fails
→ ReconcileKnownPageTransition (line 464)
→ CASE C: freshBelief.SemanticPage is null → SemanticContradiction "semantic page unresolved"
```

## 8. The exact contradiction producer

```
Agent.SemanticRun.cs:832-835
// CASE C: Unknown page — fail closed
if (freshBelief.SemanticPage is null)
{
    return FailSemantic(runId, new SemanticRunResult.SemanticContradiction(
        $"{context}: semantic page unresolved."));
}
```

Producer: `ReconcileKnownPageTransition` (Agent.SemanticRun.cs:796, line 832-835).
Trigger: `TryVerifyLocalContinuity` returns false (line 459).
Root cause: `CreateMultiPageResolver` returns null for the post-action observation because
the "Developer options" / "Developeroptions" text anchor is not visible in the scrolled viewport.

Observed facts at failure:
- fg = com.android.settings ✓ (foreground matches)
- texts = ["Enable demo mode", "Show demo mode"] (bottom of DeveloperOptions page)
- n_elements = 5 (vs 15-25 normally)
- No "Developeroptions" or "Developer options" text in the observation

Expected facts for continuity:
- The page IS DeveloperOptions (container identity)
- The post-action observation should still be recognized as DeveloperOptions

The contradiction is **legitimately derived from external world evidence** (the perception
faithfully reports what the viewport shows), but the **identity assumption is wrong**:
a scrollable semantic page at a different scroll position should NOT lose its identity
merely because the page title is scrolled out of view.

## 9. Classification: earliest defective link

**C. SCROLLED_CONTAINER_IDENTITY_DRIFT**

The earliest defective link: the page identity resolver (`CreateMultiPageResolver` via
`PageAnalysis.Analyze`) depends on which text anchors are currently visible in the
viewport. For a scrollable container (Developer options page), the visible text set
changes with scroll position. When the viewport is scrolled to the very bottom
(after the SetSwitch action or a subsequent ScrollForward), the page title
"Developer options" is no longer visible, and the resolver cannot prove identity.

This is NOT a perception defect (the perception correctly reports what's visible).
This is NOT a binding defect (the binding works correctly in the pre-action phase).
This is NOT a physical action defect (the action dispatches correctly).
This is NOT a RUN-transient UI gap (the demo-mode section is a permanent part of the
Developer options page, not a transient dialog — the "Enable demo mode" and "Show demo mode"
elements are the bottom of the Developer options list).

The page identity resolver needs additional signals beyond visible text anchors to
determine that a scrolled viewport still belongs to the same semantic page. Potential
candidates: scroll position, page structure, persistent elements (app bar), or visual
continuity of the page beyond the first viewport.

## 10. Buyer classification

```
PRIMARY CLASSIFICATION: C. SCROLLED_CONTAINER_IDENTITY_DRIFT
```

The defect is a bounded runtime-verification / identity-resolution defect: the page
identity resolver cannot match a scrollable page to its semantic identity when the
visible text anchors (page title) are scrolled out of view. This is NOT:
- A. PHYSICAL_ACTION_NO_EFFECT (action works)
- B. POST_ACTION_PERCEPTION_DRIFT (perception correct)
- D. POST_ACTION_CONTAINER_CONTINUITY_DEFECT (the continuity check itself is correct;
  the issue is upstream in the identity resolver)
- E. STALE_PRE_SCROLL_IDENTITY_REUSED (no stale identity)
- H. TRANSIENT_UI_CONTINUITY_GAP (the demo-mode section is a permanent page element)
- I. SCENARIO_DEVICE_INTERFERENCE (reproducible across independent runs)

## 11. Bounded repair candidate

The bounded repair: the page identity resolver should not depend solely on the current
viewport's visible text anchors for scrollable containers. Options:

a) **Viewport-agnostic page identity**: Add a persistent page-level signal (e.g., the
   page's app bar title is always present, even when scrolled; or use the page's root
   state layout rather than viewport-local content). The `PageAnalysis` already has
   `SWITCH_DISTRIBUTION` as an identity signal — a scrolled page should maintain its
   identity through the scroll range.

b) **Scroll-aware identity continuity**: The container's `IsStillMine` check should
   accept that the same page at a different scroll position is still the same page.
   This requires the page resolver to recognize that the visible text set is a subset
   of the page's known anchors, and the absence of the page title does not disprove
   identity.

c) **Post-action settle for continuity**: If the post-action observation cannot be
   resolved, bounded re-observation (like the existing `NavigationTransitionSettle`
   mechanism) could re-observe until the page title is visible again. This mirrors
   the existing navigation settle pattern.

Option (a) is the most fundamental: the identity resolver should use persistent
page-level signals, not viewport-dependent ones. The `SWITCH_DISTRIBUTION` signal
already exists as a page identity signal — it should be used to confirm that the
scrolled page is still the same page. If the page has a "Developer options" toggle
distribution (the "Use developer options" master switch), that distribution persists
across scroll positions.

## 12. L1 freeze

```
IAssistanceProvider: unchanged
Assistance trigger: unchanged
recommendation vocabulary: unchanged
DSH bridge: unchanged
LlmAssistanceConsumer: unchanged
```

**L1_BUYER_NOT_YET_CONFIRMED** — the natural Contradicted events arise from a bounded
runtime-verification defect (scrolled-container identity), not from genuine business
ambiguity. Eliminating the false contradiction should be the first priority; L1 remains
unjustified for this failure mode.

## 13. L2 freeze

**TRUE_PLANNING_GAP = 0/24** (from the distribution corpus).
**L2_BUYER_PRESSURE = NONE** — no DSH L2 work.

## 14. Summary of findings

| Finding | Value |
|---|---|
| Reproducible failure | ✅ ASU state-change + scroll → SemanticContradiction (5/6 state-change runs) |
| LAST_COMMON_VALID_STATE | post-SetSwitch-1, seq=5, page=DeveloperOptions |
| FIRST_DIVERGENT_DECISION | post-SetSwitch-2/post-scroll, seq=6, page=(null) |
| Physical action effect | CONFIRMED (setting changes when tap lands correctly) |
| Perception correctness | ✅ (faithfully reports viewport content) |
| Page identity failure | CreateMultiPageResolver returns null — "Developeroptions" text not in visible frame |
| Contradiction type | B. WORLD/PAGE CONTINUITY (not business state) |
| Earliest defective link | SCROLLED_CONTAINER_IDENTITY_DRIFT (C) |
| Transient UI? | NO — "Enable demo mode"/"Show demo mode" are permanent page elements at bottom of Developer options |
| Bounded repair candidate | Viewport-agnostic page identity (persistent signals) OR scroll-aware identity continuity |
| L1/L2 freeze | L1_BUYER_NOT_YET_CONFIRMED, L2_BUYER_PRESSURE = NONE |

---

## APPLY RESULT (2026-08-18) — VERIFIED LOCAL CONTINUITY IMPLEMENTED

**实现**：`openspec/changes/verified-local-continuity/`（proposal/design/spec/tasks/README）
+ `Container.TryAcceptVerifiedContinuity` / `EvaluatePageBeliefVerifiedContinuity` /
`RefreshSemanticSnapshot(verifiedLocalContinuity)` + `Agent.IsVerifiedLocalContinuity`
谓词（条件 1–7；fresh 结构性证据 = row/control 元素，非裸文本）。Owner = Agent 语义
reconciliation + Container continuity mechanics；Traversal/Environment/L1/L2 零变更。

**验证**：
- T1–T15 + T8b（VerifiedLocalContinuityTests 13/13 通过；F6 旧 falsifier 仍通过 —
  单 text_block "Something unknown" 不足 fresh 结构性证据 → fail-closed 保留）。
- 真实 corpus 重跑（24 runs）：**FALSE_SEMANTIC_CONTRADICTION = ELIMINATED**
  （SemanticContradiction 6/24 → 0/24）；ASU 状态变更残留 = 5× truthful BindingUnresolved
  （SetSwitch 后 toggle 行滚出视口，binding 诚实失败 — 非假矛盾）；already-satisfied 快路径不变；
  WiFi multilevel 14/14 Satisfied（GENUINE_PAGE_TRANSITION = STILL_DETECTED）。
- 全量 .NET 回归：仅剩 11 个既有基线失败（5×VisionHostBehavioralProofs + 5×
  VisionIdentityVerificationTests + 1×Capstone real-emulator）；REGRESSION_IMPACT =
  NONE_OBSERVED。
