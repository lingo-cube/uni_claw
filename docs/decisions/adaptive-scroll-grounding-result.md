# PROJECT_LEADER_ADAPTIVE_SCROLL_GROUNDING_RESULT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Decision: `APPROVED_WITH_CONSTRAINTS` — viewport exploration robustness
> improvement. Scope: adaptive scroll behavior ONLY.

---

## 1. Architecture Impact

Adaptive viewport progression was added to the EXISTING Agent.OpenWorld
exploration seam (`ExploreCurrentContainerViewportsAsync`). No new controller,
scroll manager, or FSM was introduced. Files touched:

- `src/UniClaw.Runtime/Model/Actions/DeviceAction.cs` — `ScrollForward` /
  `ScrollBackward` gained an optional `float StepFraction = 1.0f` (default 1.0 =
  the pre-existing fixed step; ALL existing call sites compile and behave
  identically — `is`/`OfType`/constructor patterns are unaffected).
- `src/UniClaw.Runtime.Adapters/Operator/DeviceActionTranslator.cs` —
  scroll swipe distance is scaled by `StepFraction` around the screen center
  (pure mechanism scaling; no semantic/page/scenario knowledge; clamped to
  [0.1, 2.0] so a degenerate fraction can never produce a zero/inverted swipe).
- `src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs` — the exploration loop now:
  1. starts at a **small step** (0.4 fraction) for observation continuity;
  2. after each accepted frame measures **navigation-signature overlap**
     (Jaccard over existing `OccurrencesOf` StructuredSignatures);
  3. **halves the step** when overlap is lost (a large jump skipped shared
     grounding anchors) for SUBSEQUENT scrolls — observation-only, zero extra
     scrolls, never a new admission gate (frames are accepted as before);
  4. **grows the step slowly** (+0.1, ceiling 0.8) only while more than one
     shared signature remains (never grow on a single remaining anchor);
  5. keeps **scrolling = container expansion**: a frame that fails same-
     Container continuity is an Unresolved exploration outcome (fail closed),
     never a page transition (viewport expansion does not leave the container).

## 2. Authority Verification

- **Agent authority: UNCHANGED.** The Agent remains the sole viewport
  exploration decision authority; only its step-selection heuristic changed.
- **DFS ownership: UNCHANGED.** Branch dispatch, ancestry/visited safety, and
  traversal ordering are untouched.
- **Traversal ownership: UNCHANGED.** Traversal still executes authorized
  actions only; `ExecuteLoweredActionAsync` is the same seam.
- **GoalEvidence ownership: UNCHANGED.** Completion still requires satisfied
  GoalEvidence from observation evidence.
- **Semantic capability boundary / Vision-first grounding / ADB auxiliary
  rules: UNCHANGED.** Overlap uses only the existing occurrence-signature
  mechanism; no scenario knowledge, no ADB requirement, no semantic capability
  controlling scroll.
- **STOP conditions held:** no Settings knowledge in scroll logic, ADB never
  required, semantic evidence never controls scrolling directly, DFS ownership
  unchanged, no new execution loop (the exploration loop is the pre-existing
  bounded seam with a larger step budget).

## 3. Algorithm Description

```text
Frame N accepted (anchor signatures)
        |
small initial scroll (0.4)
        |
Frame N+1
        |
navigation signature overlap (Jaccard)
        |
  +-- sufficient (>0.25)  -> grow step (+0.1, cap 0.8) if >1 shared signature
  |                           continue forward
  |
  +-- insufficient          -> HALVE step (floor 0.1) for subsequent scrolls
                                (bounded; observation-only — no extra scrolls,
                                 no reverse oscillation, no container restart)
```

Overlap answers "are we still observing a sufficiently related viewport
region?" — reusing `SourceEquivalenceNormalizer.OccurrencesOf` signatures. It
never answers "what page is this?"; no new identity, page, or scenario
classifier was introduced.

## 4. Tests

`tests/UniClaw.Runtime.Tests/Evidence/AdaptiveScrollGroundingTests.cs` — 6
deterministic proofs (all green):

| # | Test | Proves |
|---|---|---|
| 1 | `SmallScrollMaintainsOverlap_ForwardExploration` | small step keeps overlap; forward exploration scrolls multiple times with the adaptive step profile (start ≤0.4, grow +0.1, cap 0.8), no reverse, no normalization failure |
| 2 | `LargeJumpLosesOverlap_EngagesRecovery` | a large jump (4 > window 3) loses overlap; the adaptive gate halves the step (<0.4) |
| 3 | `RecoveryRestoresOverlap_Continues` | overlapping frames (jump 2 < window 4) continue without recovery and without normalization failure |
| 4 | `RecoveryBudgetExhausted_FailsClosed` | disjoint single-row frames fail closed with a bounded number of scrolls (no infinite oscillation) |
| 5 | `VisionOnly_NoAdbDependency` | the whole run uses the in-memory viewport world + fixture capability; no adb |
| 6 | `GenericWorld_NoSettingsFixture` | rows are generic "Item NN"; zero Settings/Android/WiFi vocabulary |

## 5. Regression Result

| Suite | Result |
|---|---|
| Build `src/UniClaw.Runtime.sln` (`-p:NuGetAudit=false`) | **0 errors / 0 warnings** |
| `UniClaw.Runtime.Tests` | **1941 / 1943** (+6 new adaptive tests; deterministic suite fully green) |
| Evidence/OpenWorld/VisionFirst/SourceEquivalence/Adaptive/Architecture (460) | **460 / 460** |
| `Semantic.Tests` | **32 / 32** |
| `check-consistency.sh` | ALL PASS |
| `git diff --check` | clean |
| Scenario-string check on new production diff | clean (no Settings/WiFi/Android/Location/Battery/Developer tokens) |

Remaining 2 failures are the PRE-EXISTING real-device items (Capstone
real-emulator + ExternalBoundary real-device), both environment/hardware
integration concerns unrelated to the adaptive scroll change (see §6).

## 6. Remaining Blockers

1. **Capstone real-emulator completion (pre-existing, NOT a scroll issue):**
   adaptive scroll fully solved the viewport problem — the run now explores all
   8 children (normalization resolved, `sources=8, unresolved=0`, 8 child
   dispatches, verified parent returns for 4). The remaining failure is a
   bounded-revisit/return interaction: after returning from the children the
   run re-dispatches a pending branch whose post-action settle cannot confirm a
   fresh child transition (`post-action transition did not settle within 3
   fresh observations`). This is a real-device integration concern
   (fixture `Visited 4/8` shows only 4 return-button taps took effect, i.e.
   half the parent-return Taps missed the fixture's return button), independent
   of adaptive scrolling.
2. **ExternalBoundary real-device (pre-existing):** the emulator did not reach
   the `com.android.permissioncontroller` foreground state.
3. No code-level blockers; deterministic matrix is fully green.

## 7. Conclusion

The approved adaptive scroll improvement is implemented, deterministic-proofed
(6/6), and regression-clean (1941/1943, +6 new). The scroll surface that was
previously the Capstone blocker (fixed large-step scrolls losing adjacent-frame
overlap → normalization ambiguous) is now handled by an evidence-driven
adaptive step that keeps the viewport sequence related. STOP conditions held
throughout. The two remaining real-device failures are pre-existing hardware
integration concerns and are outside the approved scroll scope.
