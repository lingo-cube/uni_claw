# External Boundary Evidence Analysis

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Phase: **1 — Evidence First** (of PROJECT_LEADER_EXTERNAL_BOUNDARY_REALDEVICE_RECOVERY_FIX),
> evidence-driven workflow (`evidence-driven-debugging` skill, protocol §17).
>
> Evidence source: repeated real-emulator runs of
> `ExternalBoundary_RealDevice` (com.android.settings → Location → external
> boundary crossing), evidence dump `/tmp/ebd_real_evidence.txt` + per-frame
> uiautomator XMLs `/tmp/ebd_obs_*.xml` + consumer-side normalization probes.
> **No Runtime production code was modified during evidence collection.**

---

## Collected Evidence

| artifact | content |
|----------|---------|
| runtime trace | Root exploration (5 scroll frames) → 19-source inventory FROZEN → authz rejects of non-Location rows → **tap settle failure** |
| observation timeline | per-frame OCR element lists with text/type/bounds (screen-ordered by the provider) |
| source normalization result | `Source normalization is unresolved` on the ORIGINAL evidence; resolved=True on every frame after OCR-only evidence assembly |
| branch inventory | 19 canonical navigation sources, epoch FROZEN, unresolved=0 |
| action history | 4-5 forward scrolls (adaptive 0.4→0.8), 1 tap on "location" |
| terminal reason | `post-action transition did not settle within 3 fresh observations；fail closed` (after the tap mis-navigated) |
| device-state frames | post-tap XMLs show **Safety & emergency** page (not Location) |

### Key measurements

1. **OCR detection noise on the Settings root** (per-frame): duplicate detections of the
   same row ("Q Search settings" ×3-4, "Network & internet" ×2, "Battery" ×2-3);
   unstable perception types for one row (`menu_item` ↔ `text_block`); unstable OCR
   text for one row ("Notification history, conversations" ↔ "Notification
   history,conversations"; "38%used-9.96GBfree" ↔ "38% used - 9.96 GB free");
   phantom detections ("LoO"/"Lo"/"Lou" — the location-pin icon read as text;
   "Bluetooth, pairing" detected in frames where it was at the top edge and missed
   in the previous frame); garbled text ("Securitv&nrivacy").
2. **OCR bounding-box imprecision** (measured): the OCR "Location" box at
   y≈0.80-0.83 (≈1565px); the uiautomator title node at y≈0.541-0.578 (center
   ≈1073px) — **~500px error**. A tap on the OCR box lands on the row BELOW
   ("Safety & emergency"), confirmed by the post-tap device frames.
3. **Ordered-overlap contract**: with the ORIGINAL raw evidence the adjacent-frame
   suffix/prefix overlap fails on every transition after frame 1 (duplicates →
   NORM4 duplicate rejection; then text/type/order instability → overlap absent).
   With OCR-only evidence assembly (one canonical occurrence per row, screen
   order, top-edge row excluded) every transition resolves (normResolved=True).

## Required Answers

### 1. Failure Stage

- **Original failure: Discovery** — `Source normalization is unresolved` at
  completeness (the accepted scroll frames' navigation evidence violates the
  frozen ordered-overlap contract; the normalizer correctly fails closed).
- **Current failure (after fixture evidence fix): Execution** — the dispatch tap
  on the OCR-identified "location" row mis-navigates (opened "Safety & emergency"
  — OCR bounding-box imprecision) → post-action settle fails closed.

### 2. Owner

- **Environment / perception layer** — the real-device OCR (vision provider)
  produces detection instability (duplicates, text/type variance, phantoms,
  garbles) and imprecise bounding boxes on the dense Settings list. This is the
  primary owner of the exposed defects.
- **Test Fixture** — the EBD harness assumed clean, stable, screen-consistent OCR
  evidence and reused a structured-dependent page resolver. Fixture-side evidence
  assembly (OCR-only) fixes the normalization; the page resolver was made
  Vision-first.
- **NOT the Runtime** — no Runtime mechanism (normalizer, exploration, dispatch,
  settle) was found defective; all fail-closed behaviors were correct.

### 3. Evidence Level

**E4** — trace timeline + observation frames + environment state (uiautomator
device-state, auxiliary analysis only) + action history + reproduction context,
across multiple runs.

---

## Phase 2 — Root Cause Classification

**B (test assumption mismatch) + C (environment/AVD OCR behavior).** The test
assumed the real-device primary OCR channel satisfies the normalizer's
ordered-overlap consistency contract on the dense Settings list; it does not
(evidence above). The Runtime is not defective (A rejected: all fail-closed
behaviors correct; D rejected: no missing wiring — the capability/harness
produces evidence; E partially: the evidence contract between the raw OCR and
the frozen normalizer is the friction point, correctly fail-closed).

## Phase 3 — Architecture Check

```
AuthorityDelta:   NONE   (no production code changed; Agent/FSM/GoalEvidence untouched)
ArchitectureDelta: NONE  (test-harness changes only)
```

Confirmed unaffected: Agent authority, DFS ownership, GoalEvidence, Semantic
Capability authority, Vision-first contract, ADB auxiliary-only rule.

---

## Exposed Defects (ESCALATION — reported, NOT one-shot-fixed)

Per the working discipline: uiautomator is auxiliary analysis only (not a flow
component), and defects exposed by investigation are REPORTED to the owning
layer rather than papered over in the harness.

| # | defect | evidence | owner | recommended direction (NOT performed here) |
|---|--------|----------|-------|----------------------------------------------|
| D1 | Real-device OCR on dense lists violates the NORM4 ordered-overlap contract (duplicates / text+type instability / phantoms / garbles) | per-frame signature sequences, normResolved transitions | perception / semantic-capability layer | capability-level detection stabilization (dedup + stable labels), or a documented contract on what evidence the OCR must provide; the frozen normalizer stays fail-closed |
| D2 | OCR bounding boxes ~500px off on the Settings list → taps hit adjacent rows (opened "Safety & emergency") | measured box offset + post-tap frames | perception layer (vision provider box accuracy) | box-accuracy investigation/calibration; the harness must NOT compensate via uiautomator coordinates (Vision-first) |
| D3 | EBD test reused a structured-dependent page resolver while the Runtime is Vision-first | startup page-resolution failure when structured was removed | test fixture | fixed here: EBD-specific Vision-first resolver (OCR title + auxiliary root marker) |

## Current State

- Root exploration normalization: **PASSES** with OCR-only evidence assembly
  (one canonical occurrence per row, screen order, top-edge excluded).
- Inventory/dispatch/authorization: **PASSES** (19 sources FROZEN; correct rejects).
- External-boundary flow: **BLOCKED by D2** (tap mis-targeting) — the exposed
  perception defect; requires the perception-layer direction above (or a scoped
  test decision), not a harness workaround.
