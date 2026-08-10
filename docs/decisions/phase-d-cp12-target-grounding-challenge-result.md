# PHASE_D_CP12_TARGET_GROUNDING_CHALLENGE_RESULT

> Generated: 2026-08-09
> Role: Reality Governance Architect — CP-12 Challenge Assessment
> Authority: `HUMAN_ADOPT_REALITY_MODEL_ADMISSION_CONTRACT` (2026-08-09)
> Inputs: CP-12 definition · RM-09 (Visibility ≠ Navigability) · Visual Pressure Supplement · VE-03/05/06/07 · S0 Runtime perception capability
> Scope: Challenge assessment only — no GroundingEngine design, no architecture, no new CP, no Runtime modification, no solution

---

## Challenge Question

**Can the current S0 Runtime establish: "the element the system selected as the target IS the element the user intended"?**

CP-12's canonical requirement: "A coordinate proximity or text substring match between a target description and an observed element is not proof that the element is the semantically correct target. Element type, spatial context, and post-interaction outcome must verify the match. 'Close enough' is not 'correct.'"

---

## Current Runtime Target Grounding Capability

The S0 Runtime resolves targets through a pipeline with these observable steps:

1. **Target description** arrives as text (from Plan step, scenario JSON, or Intent→Plan compilation)
2. **Perception** produces an element inventory: each element has `type` (from YOLO label mapping), `text` (from OCR), `coordinates` (normalized 0–1)
3. **Matching** finds candidate elements via text matching rules (exact → Contains → fuzzy), historically type-blind
4. **Action dispatch** taps at the matched element's coordinates
5. **Post-action** waits (1500ms) and presses back — no page-identity verification

**What exists:** `CandidateAuthorizationEvidence` (safety gating — dangerous vs safe), `FindSafeNavigation` whitelist, 9-case OCR normalization (FixVerificationTests L6), type-aware Contains fix (skip text-type items — FixVerificationTests).

**What does NOT exist:**
- Semantic target identity verification (confirming the tapped element IS the intended target)
- Type-aware candidate disambiguation when multiple elements match
- Post-tap page-identity verification (did we reach the right destination?)
- Perception confidence threshold gating (refuse to act when perception confidence is too low)
- Spatial context reasoning (is this element in the right region of the screen for its alleged type?)

---

## Challenge Case Analysis

### Case 1: OCR / Text Match Ambiguity

**Evidence:** VE-04 (9 OCR variants for same element), VE-07 (substring overmatch "notifications" ⊆ "Flash notifications", "Network_1" ⊆ "Network_10")

**Current Runtime behavior:**
- OCR normalization (9 cases) exists — "Bluetooth, pairing" and "Bluetooth,pairing" normalize to the same key. **Partial coverage.**
- Contains matching is historically type-blind. Fix: skip text-type items in Contains matching. **Partial coverage — the fix addresses one failure mode but does not establish positive semantic identity.**
- Substring overmatch ("Network_1" ⊆ "Network_10") is NOT addressed — the system counts Network_10 as "covered" when Network_1 is visited. **Gap.**

**Can the Runtime establish "selected target == intended target" from text alone?**
**No.** Text matching (even normalized, even type-aware) can only establish "the selected element's text is related to the target description." It cannot establish semantic identity. Two elements can share text (Settings home has "Wi‑Fi" menu_item; Wi‑Fi sub-page has "Wi‑Fi" title text) — text match alone cannot distinguish them.

**Verdict: GAP** — text matching is a necessary filter, not a sufficient identity verification.

---

### Case 2: Multiple Candidate Elements Match the Target Description

**Evidence:** VE-07: "notifications" matches both menu_item "Notifications" (correct target) and text "Flash notifications" (wrong target). Both pass Contains matching.

**Current Runtime behavior:**
- Type-aware fix: Contains matching skips text-type items. **This prevents the specific VE-07 failure mode (tapping the text element).**
- But what if TWO menu_item elements both contain the target text? A Settings page with "Wi‑Fi" and "Wi‑Fi Calling" — both are menu_item, both contain "Wi‑Fi." The system has no disambiguation logic. **Gap.**
- Multiple candidates at the same confidence level → the system picks the first match or the one with the best Contains score. No semantic reasoning.

**Can the Runtime establish "selected target == intended target" when multiple candidates match?**
**No.** The type-aware fix eliminates text-type false matches but does not handle the case where multiple elements of the CORRECT type match. Disambiguation is first-match or best-substring-score — not semantic.

**Verdict: GAP** — the type-aware fix narrows the problem but does not solve it. Multi-candidate disambiguation requires semantic reasoning the Runtime does not possess.

---

### Case 3: Element Type Mismatch — Perception Says "Navigable" but Element Is Not

**Evidence:**
- VE-05: Subtitle "Bluetooth, pairing" classified as `menu_item` (91.9% rate). Chevron heuristic (`fusion.py:292-343`) is the root cause.
- VE-06: Search box (y=0.31) classified as `menu_item` instead of `input`.

**Current Runtime behavior:**
- The Runtime trusts the perception pipeline's type label. If YOLO says `menu_item`, the Runtime treats it as navigable.
- `CandidateAuthorizationEvidence` gates on SAFETY (dangerous vs safe), not on TYPE CORRECTNESS.
- `dangerousSemantics` list (button, checkbox, input, slider, toggle) prevents interaction with dangerous types — but this is a safety constraint, not a correctness verification. The search box misclassified as `menu_item` passes the safety gate because `menu_item` is NOT in the dangerous list.
- **There is no step that says: "the perception pipeline says this is a menu_item, but is it REALLY a menu_item?"**

**Can the Runtime establish "selected target == intended target" when the type label is wrong?**
**No.** The Runtime has zero ability to detect or correct perception type misclassification. It trusts the label and acts on it. When the label is wrong (91.9% rate for subtitles, reproducible for search boxes), the Runtime navigates to the wrong page.

**Verdict: GAP** — the Runtime is entirely dependent on perception accuracy for type classification, and the perception pipeline is provably unreliable for this purpose.

---

### Case 4: Coordinate Match Without Semantic Identity

**Evidence:**
- VE-01: Golden matching accepts Euclidean distance ≤0.08–0.1 as "correct" — an element at (0.55, 0.35) is accepted as the golden element at (0.5, 0.35).
- VE-02: Coordinate-only tap without post-tap visual verification. `TapAsync(x, y)` + `WaitAsync(1500)` + `PressBackAsync()`. Never checks what page appeared.

**Current Runtime behavior:**
- The Runtime dispatches taps at vision-provided coordinates. It does not verify what element is at those coordinates.
- Post-action: wait + press back. **No page-identity verification after navigation.**
- The locate scenario (`pending_verification`) proves the Host cannot confirm page identity — it defers to offline TraceTool VerifyEngine. **The Runtime itself cannot verify that the tap reached the intended destination.**

**Can the Runtime establish "selected target == intended target" from coordinates alone?**
**No.** Coordinate proximity proves nothing about semantic identity. An element at (0.5, 0.35) could be Wi‑Fi settings on one device and Bluetooth settings on another (different OEM, different Settings layout). Coordinates are device-layout-specific, not semantically meaningful.

**Verdict: GAP** — coordinate-based targeting without post-tap verification is the weakest form of grounding. The Runtime's post-action behavior (wait + back) does not verify the destination.

---

### Case 5: Perception Confidence Insufficient for Grounding Decision

**Evidence:**
- VE-05: V2 downgrade threshold 0.035 in crop space — the threshold calculation is wrong for 91.9% of cases, but the Runtime never knows this because there's no confidence feedback loop.
- VE-06: V5 exclusion zone (y<0.10) designed to catch search boxes — real search box at y=0.31, exclusion never fires. No confidence-based fallback.
- General: YOLO confidence scores exist per detection but are not propagated to the Runtime's grounding decision. The Runtime acts on the type label regardless of the underlying detection confidence.

**Current Runtime behavior:**
- No perception confidence threshold for grounding. If the vision pipeline returns a type label, the Runtime uses it.
- No "I'm not sure — refuse to act" path for low-confidence perception outputs.
- No confidence-based candidate ranking (e.g., "this element has detection confidence 0.95 vs that element at 0.45 — prefer the high-confidence one").

**Can the Runtime establish "selected target == intended target" when perception confidence is low?**
**No.** The Runtime has no mechanism to assess or act on perception confidence. It treats all perception outputs as equally authoritative, which they provably are not (VE-05: 91.9% subtitle misclassification rate).

**Verdict: GAP** — the Runtime cannot distinguish high-confidence from low-confidence perception outputs, and has no "refuse to act" path for uncertain grounding.

---

## Composite Assessment

| Case | Current Runtime | Verdict |
|---|---|---|
| 1. OCR/text ambiguity | 9-case normalization + type-aware Contains fix | **GAP** — text match ≠ semantic identity |
| 2. Multi-candidate | Type-aware fix eliminates text mismatches; no disambiguation for multiple same-type candidates | **GAP** — first-match selection, not semantic reasoning |
| 3. Type mismatch | Trusts perception label; no correctness verification | **GAP** — 91.9% subtitle misclassification rate proves labels are unreliable |
| 4. Coordinate-only | Tap at coordinates + wait + back; no destination verification | **GAP** — coordinates are device-specific, post-tap unverified |
| 5. Confidence insufficient | No confidence threshold; no "refuse to act" path | **GAP** — all perception outputs treated as equally authoritative |

**5 of 5 challenge cases: GAP.**

---

## What WOULD "Covered" Look Like?

For reference (not a design — a capability specification):

A CP-12-covered system would need to establish, for each grounding decision, that the selected element IS the intended target. This requires at minimum:

1. **Text identity verification beyond substring:** The element's text matches the target description via semantic identity (exact match OR normalized alias match), not substring containment.

2. **Type-aware candidate ranking with disambiguation:** When multiple candidates match, prefer the candidate whose type matches the expected interaction (e.g., `menu_item` for navigation, `switch` for toggle). If multiple same-type candidates match, additional signals (spatial position, parent context, screen region) are required.

3. **Post-interaction destination verification:** After tapping, verify the destination page matches expectation. If it doesn't, the tap hit the wrong element.

4. **Perception confidence gating:** Refuse to act when the perception pipeline's confidence in its type classification or coordinate localization is below a threshold. Route low-confidence cases to alternative strategies (scroll, re-observe, request human clarification).

5. **Spatial context reasoning:** Use element position relative to other elements (e.g., "search box is at the top of the screen, below the status bar, above the first menu item") to validate or reject type classifications.

The current Runtime has NONE of these capabilities. It has safety gating (dangerous vs safe) and OCR normalization — necessary prerequisites, but not sufficient for semantic target identity verification.

---

## RM-09 Relationship

RM-09 (Visibility ≠ Navigability) is the accepted Reality Model for CP-11 (element visibility must not imply navigability). CP-12 (target grounding) is a distinct pressure: CP-11 says "visible ≠ navigable"; CP-12 says "matched ≠ intended."

RM-09 covers the perception→navigability gap (the type label might be wrong). CP-12 covers the matching→identity gap (even if the type label is CORRECT, the matched element might not be the INTENDED one). They are complementary:

| | Type label correct | Type label wrong |
|---|---|---|
| **Text match correct** | ✓ Grounded correctly | CP-11 failure: wrong element treated as navigable |
| **Text match wrong** | CP-12 failure: right-type element, wrong identity | Both CP-11 AND CP-12 fail simultaneously |

RM-09 prevents the right column. CP-12 would prevent the bottom row. Both are needed for reliable grounding.

---

## Classification

**GAP_REQUIRING_CAPABILITY**

The current S0 Runtime cannot establish "selected target == intended target." It can succeed when:
- The perception pipeline happens to produce correct type labels (not guaranteed — 91.9% failure rate for subtitles)
- The text matching happens to be unambiguous (not guaranteed — substring overmatch, multi-candidate)
- The coordinates happen to land on the right element (not verified — no post-tap destination check)

These are coincidences, not capabilities. The Runtime's target grounding is: **"find the element whose text most closely matches the target description, at approximately the right coordinates, trust the perception pipeline's type label, tap, and hope."**

### What This Means for U1

CP-12 is a **CRITICAL blocker for U1 usability.** The U1 slice ("确保 WiFi 已开启" end-to-end on emulator) requires the system to reliably:
1. Find the Wi‑Fi entry on the Settings home screen → **requires text matching + type verification**
2. Tap it → **requires coordinate grounding + post-tap verification**
3. Verify it reached the Wi‑Fi settings page → **requires page identity verification**
4. Find the Wi‑Fi switch → **requires type disambiguation (switch vs text label)**
5. Toggle it → **requires action-effect verification**

Steps 1–4 are all CP-12 territory. The current Runtime's failure to establish "selected target == intended target" means U1 cannot proceed reliably.

### What CP-12 Is NOT

- **NOT a perception problem.** CP-12 is about what the system DOES with perception outputs, not about improving perception accuracy. Better YOLO models, better OCR, or different vision providers would reduce CP-11 failures (type misclassification) but would not solve CP-12 (matching→identity gap). Even with perfect type labels, the system would still need to disambiguate multiple same-type candidates and verify post-tap destination.
- **NOT a safety problem.** Safety gating (`dangerousSemantics`, `CandidateAuthorizationEvidence`) prevents catastrophic actions but does not verify that the CORRECT safe element was selected. Tapping "Display" instead of "Wi‑Fi" is safe but wrong.
- **NOT implementable by tweaking existing parameters.** Threshold adjustments (stricter Contains matching, tighter coordinate tolerance) reduce failure rates but do not establish semantic identity. The gap is architectural: there is no step in the current pipeline whose purpose is "verify that the selected element IS the intended target."

---

## Next Step Options

The challenge result (`GAP_REQUIRING_CAPABILITY`) authorizes ONE of:

### Option A: Semantic Gate — CP-12 Candidate Generation

Trigger the Semantic Gate to authorize a new Runtime capability (target grounding verification). This would produce:
- A CP-12 Reality Model (positive path — what "correct grounding" looks like as observable behavior)
- Scenario Contracts for target grounding verification
- Architecture constraints for the grounding capability
- **NOT a GroundingEngine design** — the Semantic Gate authorizes capability SPECIFICATION, not implementation

### Option B: Defer CP-12, Pursue S1 Replay First

Accept the CP-12 gap as known and deferred. Prioritize S1 replay (CP-08 observation failure, CP-04 multi-branch false completion) to strengthen the evidence corpus before tackling the architectural challenge. CP-12 would remain as a documented gap blocking U1.

### Recommendation

**Option A — Semantic Gate for CP-12 candidate generation.** Rationale:
1. CP-12 is the ONE genuinely new canonical pressure — it was not covered by the 13 Scenario Pressures or S0 graduation.
2. CP-12 is the critical-path blocker for U1 usability. Every day CP-12 is deferred is a day U1 cannot proceed.
3. The evidence for the FAILURE mode is strong (VE-05/06/07, E3/E1, reproduced). What's missing is the POSITIVE model.
4. The 5 challenge cases define the capability boundary clearly — the Semantic Gate has a precise specification to work with.

---

## Repository Changes

`docs/decisions/phase-d-cp12-target-grounding-challenge-result.md` — created. No other files modified. No Runtime code changed. No architecture designed. No new CP created.

STOP.
