# CP12_CAPABILITY_SEMANTIC_GATE_PREPARATION_RESULT

> Generated: 2026-08-09
> Role: Project Leader / Semantic Governance Owner
> Phase: PHASE_D_CP12_TARGET_GROUNDING → CAPABILITY_AUTHORIZATION_PREPARATION
> Authority: `HUMAN_ADOPT_REALITY_MODEL_ADMISSION_CONTRACT` (2026-08-09)
> Input: RM-10 (ACCEPTED) · CP-12 Challenge Result (5/5 GAP) · RM-09 · Unified CP Portfolio
> Contract: `docs/system/reality-model-admission-contract.md` §19 (Human Gates)

---

## 1. Reality Model Reference

**RM:** `RM-10` — Target Identity Grounding Under Multiple Perceptually-Matching Candidates

**Primary Pressure:** CP-12 — Target Grounding Must Verify Semantic Identity Beyond Coordinate/Text Match

**Primary Reality Distinction:** VRD-03 (Coordinate/Text Match != Semantic Target Identity)

**RM-10 defines:**

| Layer | Content |
|---|---|
| **World Facts** (5) | WF-29..WF-33: multi-candidate matching, same-type candidates, text matching ≠ identity, coordinate proximity ≠ identity, intended target produces distinct world effect |
| **Observation Records** (4) | OB-23..OB-26: recorded wrong-target selections from real runs at E3/E2/E1 |
| **Reality Inferences** (4) | RI-20..RI-23: text matching = filter not proof, coordinates = aid not proof, world effect distinguishes targets, multi-candidate ambiguity is a world property |
| **Expected Requirements** (4) | ER-25..ER-28: evidence beyond text, type-interaction consistency, post-selection verification, no-guess under ambiguity |

**RM-10 status:** ACCEPTED (B4, 2026-08-09). All 7 gates PASS. No conditions.

---

## 2. Capability Question

**Does the current S0 Runtime already possess the capability to satisfy RM-10's Expected Requirements?**

**Answer: CAPABILITY_GAP**

The Phase D challenge assessed 5 cases against the current Runtime. All 5 were GAP. The Runtime's target grounding is: "find the element whose text most closely matches the target description, at approximately the right coordinates, trust the perception pipeline's type label, tap, and hope."

This capability does not satisfy RM-10's Expected Requirements. A new capability is needed.

**What is missing is NOT:**
- Better perception (CP-11 domain — RM-09)
- Better safety gating (existing `dangerousSemantics`)
- Better text matching (stricter substring rules)

**What is missing IS:** a capability whose purpose is to answer the question: **"is the element the system selected as the target the element the user intended?"**

No existing Runtime component has this purpose. Text matching selects candidates. Safety gating rejects dangerous actions. Neither establishes target identity.

---

## 3. Capability Boundary

### Required Capability

The system needs the ability to **establish target identity under perceptual ambiguity.**

Specifically, the capability must enable the system to:

1. **Disambiguate candidates using evidence beyond text matching.** When N > 1 observable elements match a target description, the system must use additional observable evidence — element type, spatial position relative to other elements, screen region context, or prior interaction history — to select the intended target from the candidate set. Text matching is a necessary precondition for candidate discovery; it is not sufficient for candidate selection.

2. **Verify type-interaction consistency before action dispatch.** Before dispatching an action on a selected element, the system must confirm that the element's observable properties are consistent with the intended interaction. An element used for navigation must have properties consistent with being a navigation target. An element used for state change must have properties consistent with being a stateful control. This verification is distinct from safety gating — it confirms correctness, not just safety.

3. **Verify post-interaction world-state consistency.** After interacting with a selected target, the system must observe the resulting world state and assess whether it is consistent with the expected outcome of selecting the intended target. If the world did not change as expected — wrong page, no page change, unexpected element inventory — the selected element was likely not the intended target.

4. **Detect unresolvable ambiguity and refuse to act.** When the available evidence cannot distinguish the intended target from other candidates, the system must recognize this as ambiguity and refuse to arbitrarily select one. It must signal that grounding is incomplete and defer action rather than guess.

### Non-Requirements

This capability explicitly does NOT require:

- **Perfect visual recognition.** The capability operates on perception outputs, not raw pixels. It must work with whatever type labels and text the perception pipeline produces — including imperfect ones. It does not require the perception pipeline to be improved (that's CP-11).
- **Single-model output.** The capability is a verification step, not a replacement for the perception pipeline. It may use multiple evidence sources; it is not constrained to a single model, algorithm, or heuristic.
- **Specific algorithm or architecture.** The capability boundary defines WHAT the system must be able to do. It does not prescribe HOW — no algorithm, model architecture, data structure, or implementation mechanism is specified.
- **GroundingEngine as a named component.** The capability may manifest as a new component, an extension of existing components, or a cross-cutting verification step. The organizational structure is an implementation decision, not a capability requirement.
- **Universal grounding.** The capability must handle the cases where ambiguity CAN be resolved and must detect the cases where it CANNOT. It is not required to resolve every possible ambiguity — that would require perfect world knowledge.
- **Real-time or low-latency operation.** The capability's correctness is more important than its speed. A correct grounding decision that takes longer is preferable to a fast wrong one.

---

## 4. Current Runtime Assessment (Per RM-10 ER)

### ER-25 — Evidence Beyond Text Matching

**Requirement:** "When a target description matches multiple observable elements, the system must use additional observable evidence to select the intended target. Text matching alone is insufficient."

**Current Runtime: GAP**

**Evidence:**
- The Runtime's primary target selection mechanism is `FindMatchingItem` with text matching rules (exact → Contains → fuzzy)
- VE-07 (E3): target "notifications" matched two elements via substring. The system selected the text-type element because Contains matching was type-blind. The type-aware fix (skip text-type items) addresses ONE failure mode but does not constitute general evidence-beyond-text-matching.
- No additional observable evidence (element type ranking, spatial context, screen region, interaction history) is consulted for target selection. The selection is: "best text match → that's the target."
- Multiple same-type candidates (e.g., "Wi‑Fi" and "Wi‑Fi Calling" — both menu_item, both contain "Wi‑Fi") are not disambiguated. The system selects the first or best substring match.

**What would satisfy ER-25:** The system must, when multiple candidates match, use at least one additional category of observable evidence beyond text matching to select from the candidate set. The type-aware fix is a step in this direction but only covers the text-vs-non-text case — it does not constitute a general capability.

### ER-26 — Type-Interaction Consistency

**Requirement:** "Before dispatching an action, the system must confirm that the element's observable properties are consistent with the intended interaction."

**Current Runtime: GAP (partial)**

**Evidence:**
- The type-aware Contains fix (skip text-type items for navigation) implements a NEGATIVE check: "is this element definitely NOT suitable for this action?" → skip it.
- No POSITIVE check exists: "is this element's type the CORRECT type for this action?"
- The `CandidateAuthorizationEvidence` system gates on safety (dangerous vs safe), not on type-interaction consistency. A menu_item selected for a toggle action would pass the safety gate (menu_item is not in `dangerousSemantics`) but is the wrong interaction type.
- No verification that a `switch` element is selected for a toggle action, or that a `menu_item` is selected for a navigation action. The system trusts the Plan step's action type and assumes the matched element supports it.

**What would satisfy ER-26:** The system must, for every action dispatch, confirm that the selected element's observable properties are CONSISTENT with the action's intended interaction type. This is a positive check (confirm match), not just a negative filter (reject mismatch). It must cover the general case — any element type + any action type — not just the text-vs-navigation special case.

### ER-27 — Post-Selection World-State Verification

**Requirement:** "After interacting with a selected target, the system must observe whether the resulting world state is consistent with the expected outcome of selecting the intended target."

**Current Runtime: GAP**

**Evidence:**
- Post-action behavior: `WaitAsync(1500)` + `PressBackAsync()`. No page-identity verification.
- VE-02 (E2): coordinate-only tap without post-tap visual verification. The test verifies the tap was dispatched, not what page appeared.
- VE-05 (E1): subtitle tapped → navigated to same page as parent → system detected "same page" as stale click → tapped again. The system OBSERVED the same-page condition but interpreted it as a stale click (retry), not as wrong-target evidence (stop).
- VE-06 (E3): search box tapped → search UI opened → self-loop stuck. The system never checked whether the destination was the expected Settings sub-page.
- E-01 locate scenario: `pending_verification` — the Host cannot confirm page identity. Defers to offline TraceTool VerifyEngine. The Runtime itself has no post-selection destination verification.

**What would satisfy ER-27:** After every target interaction, the system must observe the resulting world state and compare it to the expected outcome. If the world state is INCONSISTENT with the expected outcome (wrong page, no page change, unexpected element inventory), the system must treat this as evidence that the wrong target was selected — not as a stale click, not as a retry opportunity, not as "verify later offline."

### ER-28 — Ambiguity-Aware Refusal

**Requirement:** "When the system cannot establish which candidate is the intended target with sufficient confidence, it must not arbitrarily select one."

**Current Runtime: GAP**

**Evidence:**
- First-match or best-substring-score selection. No ambiguity detection.
- No confidence threshold for grounding decisions.
- No "refuse to act" path for ambiguous target selection.
- The system always selects a target. It never says "I cannot determine which element is the intended target." Even when N > 1 candidates match with equal confidence and the same type, the system picks one (first match) and proceeds.

**What would satisfy ER-28:** The system must have a capability to DETECT when the available evidence is insufficient to distinguish the intended target from other candidates. When such ambiguity is detected, the system must signal that grounding is incomplete and DEFER action — not select arbitrarily.

---

## 5. Capability Delta

### Existing

The current S0 Runtime already has:

| Capability | Used For |
|---|---|
| Text matching (exact, Contains, fuzzy) | Candidate discovery |
| Type-aware text matching (skip text-type items) | Negative filter: reject clearly-wrong candidates |
| OCR normalization (9 cases) | Stable text identity across observations |
| Safety gating (`dangerousSemantics`, `CandidateAuthorizationEvidence`) | Reject dangerous actions |
| Post-action observation (`Observe→Verify`, `IsStillMine`) | Page-change verification (CP-02) |
| Stale-click detection (3× circuit breaker) | Detect repeated failed actions |
| Confidence thresholds (`confidenceThresholds` in safety policy) | Safety decisions only (not grounding) |

**These are necessary prerequisites.** Text matching provides the candidate set. Safety gating prevents catastrophic wrong-target actions. Post-action observation infrastructure exists. But none of these capabilities answers the question: "is the selected element the intended target?"

### Missing

RM-10 requires the system to be able to:

| Requirement | What the Runtime Cannot Currently Do |
|---|---|
| **Candidate disambiguation** (ER-25) | Select from N > 1 same-type candidates using evidence beyond text matching. The Runtime has no disambiguation logic — it picks the first or best text match. |
| **Type-interaction consistency** (ER-26) | Positively confirm that the selected element's observable properties match the intended interaction type. The Runtime has only a negative text-type filter. |
| **Post-selection verification** (ER-27) | Observe the world state after interaction and compare it to the expected outcome. The Runtime observes but does not compare to expectation — it checks for ANY page change (CP-02), not the EXPECTED page change. |
| **Ambiguity-aware refusal** (ER-28) | Detect when candidate disambiguation is impossible and refuse to act. The Runtime always selects a target — it has no "I don't know" path for grounding. |

### Delta

```
Existing:
  Text matching → candidate SET
  Safety gating → reject DANGEROUS actions
  Post-action observation → detect ANY page change

Missing (RM-10 requires):
  Candidate disambiguation → select from candidate SET
  Type-interaction consistency → confirm RIGHT type for RIGHT action
  Post-selection verification → detect EXPECTED vs UNEXPECTED outcome
  Ambiguity detection → recognize when selection is UNSUPPORTED

Delta:
  The gap is not in perception, safety, or text matching.
  The gap is a missing verification step between
  "these elements match the target description"
  and
  "dispatch action on this element."

  No existing component has the purpose of establishing
  target identity under perceptual ambiguity.
```

---

## 6. Semantic Gate Decision

**AUTHORIZE_CAPABILITY_EXPLORATION**

### Rationale

1. **RM-10 is an accepted Reality Model.** CP-12 is not a hypothetical pressure — it is modeled with 5 WFs (2 DIRECT from E3 evidence), 4 RIs, and 4 ERs. The world facts are observed in committed evidence.

2. **The capability gap is real and specific.** All 4 ERs are GAP against the current Runtime. The Phase D challenge found 5/5 cases GAP. This is not a speculative gap — it is demonstrated by E3 evidence (VE-07, VE-06).

3. **CP-12 is the critical-path blocker for U1.** The U1 usability slice requires reliable target grounding at 4 steps (find Wi‑Fi entry → tap it → verify Wi‑Fi page → find Wi‑Fi switch). Without CP-12 capability, U1 cannot proceed reliably.

4. **The capability boundary is well-defined.** RM-10's ERs define WHAT the system must be able to do. The non-requirements exclude implementation design. The exploration can proceed within clear constraints.

5. **This gate authorizes exploration, not implementation.** The next phase is Candidate Generation — producing candidate approaches to satisfy RM-10's ERs, evaluated against architecture invariants. Implementation requires a subsequent gate.

### Authorization Scope

This gate authorizes:

- **Candidate Generation:** Produce one or more candidate approaches to satisfy RM-10's ER-25..ER-28
- **Scenario Contract authoring:** Define verifiable scenarios that prove the capability satisfies each ER
- **Architecture constraint validation:** Ensure candidates respect existing invariants (Agent owns completion, external world authoritative, Plan ≠ reality, etc.)
- **Reality Model reference:** Use RM-10 as the authoritative specification for what the capability must achieve

### Explicitly NOT Authorized

- Architecture design or component specification
- GroundingEngine implementation
- Runtime code modification
- Perception pipeline changes
- New Reality Model creation
- New CP registration
- S1/S2/S3 execution
- U1 implementation

---

## 7. Human Gate Requirement

**Required: `HUMAN_AUTHORIZE_CP12_CAPABILITY_CANDIDATE_GENERATION`**

The Semantic Gate decision (`AUTHORIZE_CAPABILITY_EXPLORATION`) requires Human authorization because:

1. CP-12 is the ONE genuinely new canonical pressure — it was not covered by the 13 Scenario Pressures or S0 graduation.
2. The capability gap is architectural — it requires a new verification step in the Runtime's action pipeline.
3. Candidate generation will produce design alternatives that must be evaluated against architecture invariants — this is a gated activity.

**The Human Gate asks:** "Should the project authorize exploration of candidate approaches to satisfy RM-10's target grounding requirements, within the defined capability boundary and explicit non-requirements?"

---

## 8. Explicit Non-Actions

Confirmed:

- **No Candidate Generated** — this artifact only authorizes future generation
- **No Architecture Designed** — capability boundary defined, no components specified
- **No Runtime Changed** — current Runtime assessed, not modified
- **No GroundingEngine designed** — capability described in behavioral terms only
- **No CP created** — CP-12 is the existing canonical pressure from the unified portfolio
- **No RM modified** — RM-10 is referenced, not changed

---

## Next Step (contingent on Human authorization)

**CP12_CAPABILITY_CANDIDATE_GENERATION** — produce candidate approaches to satisfy RM-10 ER-25..ER-28, with Scenario Contracts, architecture constraint validation, and Reality Model traceability.

## Repository Changes

`docs/decisions/cp12-capability-semantic-gate-preparation.md` — created. No other files modified.

STOP.
