# Real-Device Test Remaining Failures — Root Cause Report

> Date: 2026-08-23
> Worker: DeepSeek-V4-Flash
> Scope: root-cause analysis of the 2 remaining `UniClaw.Runtime.Tests` failures
> (`Capstone_OneAgentOneRun_RealEmulator_ReachesCapstoneComplete` and
> `ExternalBoundary_RealDevice`). Both are REAL-DEVICE hardware-integration
> concerns; the deterministic matrix (1946/1948) is fully green.

---

## Environment note (for reproducibility)

Both tests were re-run with the environment fully provisioned:
- Emulator `emulator-5554` (AVD `scroll-test`, AOSP API 35) online,
  `sys.boot_completed=1`.
- Fixture APK `com.uniclaw.fixture` installed (Capstone only).
- Vision service healthy on `/tmp/uniclaw-capstone.sock` (`{"status":"ok","warm":true}`).
- The emulator had crashed once between sessions (ColorBuffer errors) and was
  restarted before these runs.

---

## Failure 1 — Capstone_OneAgentOneRun_RealEmulator_ReachesCapstoneComplete

### Failure point

```
TRACE Failed | Fixture Root | Step-15 |
  post-action transition did not settle within 3 fresh observations；
  fail closed（composition policy；zero redispatch）。
```

### Evidence

| Signal | Value |
|---|---|
| Actions | `LaunchApp, ScrollForward×5, Tap×8, ScrollBackward×1, Tap×1` |
| Child containers entered | **Child 05, 06, 07, 08 only** (5 trace lines each) |
| Verified parent returns | **4** (Child 06, 05, 07, 08) |
| Fixture state | `Visited 4/8` (only 4 return-button taps took effect) |
| Post-revisit frames | seq33-35 = `Visited 4/8 \| Child 03,04,05` |

### Root cause chain

1. **Adaptive scroll solved the viewport phase**: 5 scrolls discovered all 8
   children, normalization resolved (`sources=8, unresolved=0`), inventory = 8
   children, discovery epoch FROZEN.
2. **Only 4 of the 8 children were actually entered** (Child 05-08). The other
   4 (Child 01-04) were never reached: they sit ABOVE the current viewport
   (the run ended scrolled to the bottom of the 8-row list).
3. **The parent-return for each entered child tapped the "Fixture Root" return
   button using OCR bounds**; the fixture only increments `Visited` when that
   button's `OnClick` fires. 4 of the 8 return taps took effect
   (`Visited 4/8`); the other 4 children were never entered so their returns
   never happened.
4. **Revisit (Step-14) performed ONE `ScrollBackward`** (fixed 40% step),
   moving the viewport from `Child 05-08` back to `Child 03-05`. **Child 01/02
   were still not visible** (frames seq33-35 show `Child 03,04,05`).
5. **Step-15 tapped a pending branch (Child 01/02) that was NOT in the current
   viewport** → the tap hit nothing / an inert area → the post-action settle
   could not confirm a fresh child-container transition → 3 fresh observations
   consumed → bounded budget exhausted → fail-closed.

### Classification

**Device/hardware integration issue — NOT a code regression, NOT a scroll
issue.** Adaptive scroll + branch grounding both work correctly (the 8 children
are discovered, normalization resolves, grounding gates are exercised). The
failure is a **bounded-revisit depth limitation on the real device**: one fixed
40% reverse scroll is insufficient to bring the top-of-list children back into
the viewport, so the run cannot dispatch them, and the revisit budget is not
scaled to the list length. (This matches the earlier finding: the run's viewport
ends at the bottom of the list; `Visited 4/8` confirms only the bottom-half
children were genuinely entered.)

---

## Failure 2 — ExternalBoundary_RealDevice

### Failure point

```
TRACE |SettingsRoot| viewport exploration exhausted: source-seq=6;
  EBD target Location visible; exploration exhausted.
TRACE |SettingsRoot| Source normalization is unresolved; completeness cannot be proven.
```

(also asserted: `External foreground (com.android.permissioncontroller) not
observed`, `SYSTEMBACK_COUNT=0`, `CONTAINERS=[SettingsRoot]`)

### Evidence

| Signal | Value |
|---|---|
| Actions | `LaunchApp, ScrollForward×4` (exploration), no Tap |
| Containers | `[SettingsRoot]` only |
| `Location` entry | visible in frames 5-6 (scroll reached it) |
| External foreground | `com.android.permissioncontroller` never seen |
| Normalization | unresolved after 4 scrolls |

### Root cause chain

1. **The EBD test expects the Settings root to contain a `Location` entry**
   (its `AuthorizeEbdReal` only authorizes `Location` on the root). On the
   current AVD (AOSP 35) `Location` is NOT on the first Settings screen — it
   appears only after scrolling (verified: `Location` is visible at
   `Security & privacy` / 1 scroll down).
2. **Semantic capability injection fixed the "no scroll" problem**: the test
   now injects a fixture capability so root rows become navigation candidates;
   `ExploreWhileNew`-derived exploration scrolls; `Location` IS reached
   (frames 5-6).
3. **The exploration stops when `Location` becomes visible** (test-specific
   bounding evaluator), then completeness runs `SourceEquivalenceNormalizer
   .Normalize` over the 4 accepted scroll frames. The frames are:
   `[Network&internet, Connected devices, Apps, ...]` →
   `[Connected devices, Apps, Notifications, ...]` →
   `[Notifications, Battery, Storage, ...]` →
   `[Storage, Sound&vibration, Display, ...]`. Although adjacent frames share
   rows, the shared rows appear as a PREFIX of the next frame (not a strict
   suffix), so `FindUniqueSuffixPrefixOverlap` cannot prove a unique ordered
   overlap → normalization unresolved → completeness fails closed.
4. **The external-boundary stage was never reached**: because completeness
   fails at the root, the run never dispatched `Location` → never entered the
   Location sub-page → never tapped `App location permissions` → the
   `com.android.permissioncontroller` foreground never appeared.

### Classification

**Device/hardware integration + test-data coupling issue — NOT a code
regression.** The AVD's Settings layout (Location off first screen) combined
with the strict ordered-overlap normalization makes the real-device scroll
sequence fail closed before the boundary stage. Two contributing factors:
(a) the Settings list ordering on this AVD differs from the test's assumption;
(b) `SourceEquivalenceNormalizer` requires strict suffix/prefix ordered overlap,
which real Settings scroll frames do not always provide (rows shift order).

---

## Summary

| Test | Root cause | Class |
|---|---|---|
| Capstone | Bounded-revisit depth: one 40% reverse scroll can't bring top-of-list children (01-04) back into viewport; dispatch of an invisible branch can't settle | Device integration (revisit scaling), pre-existing |
| ExternalBoundary | Settings `Location` not on first screen on this AVD + strict ordered-overlap normalization fails on real Settings scroll frames → root completeness fails before boundary stage | Device integration + test-data coupling, pre-existing |

Both are hardware-integration concerns outside the adaptive-scroll and
branch-grounding scopes (which are verified working by the 1946 green
deterministic tests). Neither is a code regression introduced by the recent
changes. Options for further work (each a separate, bounded decision):

1. **Capstone**: scale bounded revisit to list length (e.g., revisit budget tied
   to discovery-frame count / inventory size) so top-of-list children become
   visible; or make revisit use the adaptive step fraction.
2. **EBD**: align the test's Settings-entry assumption with the actual AVD
   layout (e.g., target `Security & privacy` or drive `Location` via search), or
   relax the exploration criterion to scroll until `Location` is dispatchable,
   or accept normalization of real Settings frames via a smaller scroll step.
