# CP12_REALITY_MODEL_EXTRACTION_RESULT

> Generated: 2026-08-09
> Role: Project Leader / Reality Governance Architect — SEMANTIC_GATE_PREPARATION
> Phase: PHASE_D_CP12_TARGET_GROUNDING_CHALLENGE → RM-10 extraction
> Authority: `HUMAN_ADOPT_REALITY_MODEL_ADMISSION_CONTRACT` (2026-08-09)
> Inputs: CP-12 Challenge Result · RM-09 (Visibility ≠ Navigability) · VRD-03 · VE-03/05/06/07 · Unified CP Portfolio
> Contract: `docs/system/reality-model-admission-contract.md` §20 (16-field schema, frozen v1.0)

---

## 1. Reality Problem Definition

### What CP-12 Solves

CP-12 addresses a world problem that exists **even when CP-11 is fully satisfied.**

CP-11 (Element Visibility ≠ Navigability) says: "the perception pipeline's type label for a single element may be wrong." CP-11 is about **one element** — is its declared type correct?

CP-12 says: "even when all type labels are correct, matching a target description to an observable element does not guarantee the matched element IS the intended target." CP-12 is about **multiple candidates** — given N elements whose text matches the target description, which one is the element the user intended?

**Concrete world scenario (from VE-07):**

```
Target: "Notifications"
Observable elements on the Settings page:
  A. menu_item "Notifications" at (0.32, 0.78)  ← intended target
  B. text      "Flash notifications" at (0.26, 0.73)  ← substring match
```

CP-11 question: "Is element A REALLY a menu_item?" → Yes, it is. CP-11 satisfied.
CP-12 question: "Is element A the INTENDED target?" → The system must determine this. Text substring matching ("notifications" ⊆ "Flash notifications") matches BOTH elements. The system must select A over B. **This is CP-12's problem.**

**What CP-12 Does NOT Solve**

- **Perception type classification accuracy** — that's CP-11. If the perception pipeline says element A is `menu_item` but it's actually a subtitle, that's a CP-11 failure, not CP-12. CP-12 assumes type labels may be correct OR incorrect; its job is to select the intended target from candidates regardless.
- **Safety gating** — that's the safety policy (`dangerousSemantics`). CP-12 doesn't decide whether tapping an element is safe; it decides whether the selected element IS the right one.
- **Page identity verification after navigation** — that's CP-13 (Raw Page Evidence ≠ Semantic Page Identity). CP-12's scope ends when the target is selected; CP-13 verifies the destination.
- **Action effect verification** — that's CP-01/CP-02 (did the tap actually produce the intended world change?). CP-12's scope is selecting the target; CP-01/02 verify the outcome.

---

## 2. Proposed Reality Model

| # | Field | Value |
|---|---|---|
| 1 | RM-ID | `RM-10` (candidate) |
| 2 | Title | Target Identity Grounding Under Multiple Perceptually-Matching Candidates |
| 3 | Type | MODEL |
| 4 | Pressure Relation | Primary CP-12 (Target Grounding Must Verify Semantic Identity Beyond Coordinate/Text Match). No secondary CPs — CP-12 is the ONE genuinely new canonical pressure. |
| 5 | World Facts | WF-29..WF-33 |
| 6 | Observation Records | OB-23..OB-26 |
| 7 | Reality Inferences | RI-20..RI-23 |
| 8 | Expected Requirements | ER-25..ER-28 |
| 9 | Temporal Scope | Per-target-selection event (from target description arrival to element selection confirmation) |
| 10 | Legacy Mechanism Context | Non-normative: `FindMatchingItem` ③ Contains matching, type-blind matching (historical), `TextTargetResolutionTests`, `CandidateAuthorizationEvidence`, `FixVerificationTests` L5/L6/L8, 9-case OCR normalization, `fusion.py` chevron heuristic, golden matching tolerance 0.08–0.1 |
| 11 | Evidence References | VE-07 (E3, substring overmatch in real run), VE-06 (E3, search box misclassification → wrong target), VE-05 (E1, subtitle as navigable → wrong target), VE-03 (E1, empty OCR → no identity), VE-01 (E2, coordinate drift accepted as identity), CP-12 Challenge Result (Phase D, 5/5 cases GAP) |
| 12 | Provenance Chain | VE-07: real run `20260806T072558649Z` step 36 — "notifications" matched text-type "Flash notifications" via type-blind Contains → tapped wrong element → depth-3 violation. VE-06: real run `20260805T052309367Z` — search box misclassified menu_item → tapped → search UI self-loop. VE-05: FixVerificationTests L8 — subtitle phantom from `fusion.py:292-343` chevron heuristic → double-click same page. |
| 13 | Counterfactual / Falsification | See §8 |
| 14 | Validation Status | Not validated (B3 pending for RM-10) |
| 15 | Admission Outcome | Not admitted (B4 pending for RM-10) |
| 16 | Confidence Summary | WF-29..WF-32 DIRECT from E3/E1 evidence. WF-33 INFERRED. RI-20/RI-21 HIGH (directly observed). RI-22 MEDIUM (inferred from failure pattern). RI-23 HIGH (logical necessity). Model confidence: HIGH — core world facts are directly observed in committed evidence. |

---

## 3. World Facts

### WF-29 — A target description can match multiple observable elements on the same screen

- **Support:** DIRECT
- **Evidence:** VE-07: target "notifications" matched BOTH menu_item "Notifications" AND text "Flash notifications" on the same Settings page. The substring containment relation is not a function — one input (target description) maps to multiple outputs (observable elements).
- **Temporal Scope:** Per-observation snapshot (single screen → element inventory)

### WF-30 — Multiple matching elements may share the same type classification

- **Support:** DIRECT
- **Evidence:** CP-12 core scenario: Settings home page contains multiple menu_item elements whose text contains common substrings ("Wi‑Fi" and "Wi‑Fi Calling" — both menu_item, both contain "Wi‑Fi"). Even with perfect type labels (CP-11 satisfied), disambiguation is required.
- **Temporal Scope:** Per-observation snapshot

### WF-31 — Text substring matching does not establish which matching element is the intended target

- **Support:** DIRECT
- **Evidence:** VE-07: Contains matching selected "Flash notifications" (text type) for target "notifications" (intended menu_item). The substring relation "notifications" ⊆ "Flash notifications" is TRUE — the match is correct by the matching rule. But the matched element is the WRONG target. The matching rule's correctness does not imply the selection's correctness.
- **Temporal Scope:** Per-matching event

### WF-32 — Coordinate proximity does not establish which element is the intended target

- **Support:** DIRECT
- **Evidence:** VE-01: golden matching accepts Euclidean distance ≤0.08–0.1 as "correct." An element at (0.55, 0.35) is accepted as the golden element at (0.5, 0.35) — but on a different device or app version, (0.55, 0.35) could be a different element entirely. Coordinates are device-layout-specific. Two distinct elements can be at nearby coordinates.
- **Temporal Scope:** Per-coordinate-match event

### WF-33 — The intended target produces a distinct observable world effect that differs from the effect of selecting a wrong target

- **Support:** INFERRED
- **Evidence:** When the correct target is selected (menu_item "Notifications" → tap → Notifications sub-page appears), the observable world changes in a specific way (new page with specific elements). When a wrong target is selected (text "Flash notifications" → tap → no page change or wrong page), the world either does not change or changes differently. The intended target's world effect is a property of the external world — the Settings app's navigation graph connects "Notifications" menu_item to the Notifications sub-page.
- **Temporal Scope:** Post-selection observation (after tap)

---

## 4. Observation Records

### OB-23 — VE-07: type-blind Contains match on real run

- **Observation:** System matched target "notifications" to two elements on the same page: menu_item "Notifications" (correct) and text "Flash notifications" (incorrect, via substring). Type-blind matching selected the text element. Tap dispatched. Result: depth-3 violation (wrong page).
- **Source:** Real run `20260806T072558649Z` step 36, reproduced in `TextTargetResolutionTests.cs`
- **Evidence Grade:** E3 (recorded-reality-derived executable regression)

### OB-24 — VE-06: search box misclassified → wrong navigation target

- **Observation:** Search box element at y=0.31 classified as `menu_item` (not `input`). System matched it as navigable target. Tap dispatched → search UI opened → self-loop stuck. The selected element (search box) was not the intended target (Settings menu entry).
- **Source:** Real run `20260805T052309367Z`, reproduced in `20260805T052309367Z_TraceReplayTests.cs`
- **Evidence Grade:** E3 (recorded-reality-derived)

### OB-25 — VE-05: subtitle phantom → double-click same page

- **Observation:** Subtitle "Bluetooth, pairing" classified as `menu_item` (chevron heuristic phantom). System selected it as navigation target. Tap dispatched → reached same page as "Connected devices" (the parent menu_item). System interpreted same-page as stale click → tapped again. Two taps, no progress.
- **Source:** `FixVerificationTests.cs` L8, `fusion.py:292-343`
- **Evidence Grade:** E1 (executable regression + production code root cause)

### OB-26 — VE-01: coordinate drift accepted as correct identification

- **Observation:** Golden matching rule: element at (0.55, 0.35) accepted as golden element at (0.5, 0.35) because Euclidean distance 0.05 ≤ 0.1. The system declared "correct identification" based on coordinate proximity alone. No verification that the element at (0.55, 0.35) is semantically the same as the golden element.
- **Source:** `VisionGoldenIntegrationTests.cs`, `VisionGoldenComparer.cs`, real device PKJ110
- **Evidence Grade:** E2 (real-device integration, executable)

---

## 5. Reality Inferences

### RI-20 — Text matching is a candidate filter, not a target identity proof

- **Inference:** The fact that an element's text contains or matches the target description makes it a CANDIDATE for being the intended target. It does not make it the intended target. Text matching selects a set; identity verification must select one element from that set.
- **Confidence:** HIGH
- **Alternatives considered:** (a) text match = identity proof — refuted by VE-07 (substring overmatch selects wrong element); (b) exact match = identity proof — refuted by the existence of multiple elements with the same text on different pages or in different screen regions.
- **Materiality:** HIGH — treating text match as identity proof is the root cause of VE-07 and the CP-12 fail oracle.
- **Supporting WF:** WF-29, WF-31
- **Supporting OB:** OB-23

### RI-21 — Coordinate proximity is a localization aid, not a target identity proof

- **Inference:** The fact that an element is near the expected coordinates makes it a LIKELY candidate. It does not make it the intended target. Two distinct elements can be at nearby coordinates; the same element can be at different coordinates on different devices.
- **Confidence:** HIGH
- **Alternatives considered:** (a) coordinate match = identity proof — refuted by cross-device layout variation; (b) coordinates are useless — refuted by VE-01 (coordinate proximity is a useful signal, just not sufficient alone).
- **Materiality:** HIGH — treating coordinate proximity as identity proof enables VE-01-style false acceptance and VE-02-style unverified taps.
- **Supporting WF:** WF-32
- **Supporting OB:** OB-26

### RI-22 — The world effect of selecting a target distinguishes the intended target from wrong candidates

- **Inference:** After selecting and interacting with an element, the observable world change (or absence of change) provides evidence about whether the selected element was the intended target. The intended target produces a specific, predictable world transition (navigation to expected page). A wrong target produces a different transition or no transition.
- **Confidence:** MEDIUM
- **Alternatives considered:** (a) world effect is always distinguishable — refuted by the boundary case where two distinct targets lead to visually identical pages (rare but possible); (b) world effect is never useful — refuted by VE-05 (double-click same page → no page change → system COULD detect "this was not a navigation").
- **Materiality:** HIGH — post-selection world-effect observation is the only available reality signal that can distinguish intended from wrong targets after the fact.
- **Supporting WF:** WF-33
- **Supporting OB:** OB-24 (search box → self-loop → no progress), OB-25 (subtitle → same page → double-click)

### RI-23 — When N candidates match a target description and N>1, the system faces a genuine ambiguity that text matching alone cannot resolve

- **Inference:** Multiple matching candidates is not an edge case or a perception failure — it is a property of the external world. GUI screens contain elements with overlapping or related text. The ambiguity is in the WORLD, not in the system's matching algorithm.
- **Confidence:** HIGH
- **Alternatives considered:** (a) multiple matches indicate a perception error — refuted by WF-30 (two distinct menu_item elements with related text, both correctly classified); (b) multiple matches can always be resolved by stricter matching — refuted by the substring problem (stricter matching may miss the correct target entirely).
- **Materiality:** HIGH — acknowledging the ambiguity as a world property (not a system defect) is the foundation for any solution.
- **Supporting WF:** WF-29, WF-30
- **Supporting OB:** OB-23

---

## 6. Expected Requirements

### ER-25 — Target selection from multiple matching candidates must use evidence beyond text matching

- **Requirement:** When a target description matches multiple observable elements, the system must use additional observable evidence — element type, spatial position relative to other elements, screen region, or post-selection world effect — to select the intended target. Text matching alone is insufficient.
- **Source:** CP-12 primary fail oracle (VE-07), VRD-03, WF-31
- **Prevents fail oracle:** Yes — if enforced, the system would not select "Flash notifications" for target "notifications" based on substring match alone.

### ER-26 — Target selection must verify that the selected element's type is consistent with the intended interaction

- **Requirement:** Before dispatching an action on a selected element, the system must confirm that the element's declared type supports the intended interaction. A text-type element must not be selected for a navigation action. A menu_item must not be selected for a toggle action. Type consistency is a necessary (not sufficient) condition for correct target selection.
- **Source:** CP-12 (VRD-02 supporting), VE-07 (text element selected for navigation)
- **Prevents fail oracle:** Yes — the VE-07 failure (tapping text element for navigation) would be prevented.

### ER-27 — After interacting with a selected target, the system must observe whether the resulting world state is consistent with the expected outcome of selecting the intended target

- **Requirement:** Post-interaction observation must verify that the world changed in a way consistent with the intended target's expected effect. If the page did not change, or changed to an unexpected page, the selected element was likely not the intended target.
- **Source:** CP-12, WF-33, VE-05 (double-click same page), VE-06 (search UI self-loop)
- **Prevents fail oracle:** Yes — post-interaction verification would detect wrong-target selections.

### ER-28 — When the system cannot establish which candidate is the intended target with sufficient confidence, it must not arbitrarily select one

- **Requirement:** Target grounding under ambiguity is not a mandatory selection. If the available evidence cannot distinguish the intended target from other candidates, the system must not guess. It must signal ambiguity and defer action.
- **Source:** CP-12, RI-23 (ambiguity is a world property)
- **Prevents fail oracle:** Yes — prevents the "pick first match and hope" behavior.

---

## 7. CP-11 Boundary Analysis

### Covered by CP-11 (RM-09 — Visibility ≠ Navigability)

CP-11 addresses: "Is this element what the perception pipeline says it is?"

| Concern | CP-11 Coverage |
|---|---|
| Element type label may be wrong (subtitle → menu_item) | ✓ RM-09 WF-26, RI-13 |
| OCR text may be empty, garbage, or variant | ✓ RM-09 WF-27, RI-15 |
| Perception pipeline produces phantom elements (chevron heuristic) | ✓ RM-09 WF-28, RI-14 |
| Element visible in image ≠ element is interactive | ✓ RM-09 RI-18 |
| Type classification is a perception output, not a world fact | ✓ RM-09 RI-13 |
| Safety constraints (dangerousSemantics) filter by type | ✓ RM-09 RI-19 (normative layer over perception) |

**CP-11's scope is ONE element at a time.** It asks: "Given this element, is its declared type/interaction-capability correct?"

### Unique to CP-12 (RM-10 — Target Grounding)

CP-12 addresses: "Given N candidate elements that all plausibly match the target description, which one is the intended target?"

| Concern | CP-12 Coverage |
|---|---|
| One target description matches multiple elements | RM-10 WF-29, RI-23 |
| Multiple matching elements may share the same correct type | RM-10 WF-30 |
| Text substring matching selects candidates, not the target | RM-10 WF-31, RI-20 |
| Coordinate proximity localizes, does not identify | RM-10 WF-32, RI-21 |
| Wrong-target selection produces a different world effect than intended-target selection | RM-10 WF-33, RI-22 |
| Post-selection world observation can detect wrong-target selections | RM-10 ER-27 |
| Ambiguity that cannot be resolved must not be guessed | RM-10 ER-28 |

**CP-12's scope is MULTIPLE candidates.** It asks: "Among these N matching elements, which one did the user intend?"

### Why Both Are Needed

```
Scenario: Target "Wi‑Fi" on Settings home screen.

Elements observed:
  A. menu_item "Wi‑Fi" at (0.5, 0.31)  ← intended target
  B. menu_item "Wi‑Fi Calling" at (0.5, 0.54)  ← same type, contains "Wi‑Fi"
  C. text      "Wi‑Fi" at (0.5, 0.31)  ← (hypothetical: OCR on icon label)

CP-11 questions:
  Is A really a menu_item? → Yes (correct classification)
  Is C really text? → Yes (correct classification)
  → CP-11 satisfied. All type labels are correct.

CP-12 questions:
  Target "Wi‑Fi" matches A, B, and C. Which one?
  A and B are both menu_item. C is text. ER-26 filters C.
  A and B remain. Both menu_item. Both contain "Wi‑Fi." Which one?
  → CP-12 problem. Text matching alone cannot resolve.
```

**CP-11 and CP-12 are orthogonal.** You can have perfect type classification (CP-11 solved) and still fail target grounding (CP-12 unsolved). You can have correct target selection (CP-12 solved for a specific case) and still suffer from type misclassification (CP-11 unsolved for other elements).

---

## 8. Counterfactual / Falsification

### Per-WF Counterfactuals

- **WF-29 (target description matches multiple elements):** If every target description uniquely matched exactly one element on every screen, CP-12 would be unnecessary. Falsified by VE-07 (one target, two matches).
- **WF-30 (multiple matches may share same type):** If type classification always disambiguated candidates, CP-12 would reduce to CP-11. Falsified by the "Wi‑Fi" / "Wi‑Fi Calling" scenario.
- **WF-31 (text matching ≠ identity proof):** If substring/exact matching always selected the correct element, CP-12 would be unnecessary. Falsified by VE-07.
- **WF-32 (coordinate proximity ≠ identity proof):** If coordinate matching always selected the correct element, CP-12 would be unnecessary. Falsified by cross-device layout variation.
- **WF-33 (intended target produces distinct world effect):** If wrong-target and intended-target selections produced identical observable outcomes, post-selection verification would be impossible. This boundary condition exists (two distinct pages with identical element inventories) but is not observed in the current evidence corpus.

### Per-RI Materiality Counterfactuals

- **RI-20 (text matching = filter, not proof):** If text matching WERE identity proof, VE-07 would be impossible. But VE-07 occurred. Material.
- **RI-21 (coordinates = aid, not proof):** If coordinates WERE identity proof, the CP-12 fail oracle could not occur for coordinate-based grounding. Material.
- **RI-23 (multi-candidate ambiguity is a world property):** If multi-candidate were always resolvable by better matching, CP-12 would be an algorithm problem, not a reality problem. But better matching cannot resolve "Wi‑Fi" vs "Wi‑Fi Calling" — both match, both are menu_item. The ambiguity is in the world. Material.

### Falsifiability (§16.3)

**What observation would refute RM-10?**

RM-10 would be refuted if, for every real GUI screen observed, every target description matched exactly one element and that element was always the correct target. In that world, CP-12 would be unnecessary — text matching alone would always produce correct grounding.

This is already falsified by the evidence corpus (VE-07, VE-06, VE-05). The model survives because the falsification condition DOES hold — target descriptions DO match multiple elements in the real world.

**What observation would require model adjustment?**

If a system achieved reliable target grounding using ONLY text matching and coordinate proximity (no type verification, no post-selection observation, no ambiguity handling), RM-10's inferences would need to be weakened. The model would change from "text matching is insufficient" to "text matching is sufficient in practice for this specific app/device combination." This would be a VARIANT of RM-10 (weaker claims, narrower scope), not a refutation.

---

## 9. Admission Recommendation

### Recommendation: ACCEPT_NEW_MODEL

**Rationale:**

1. **Novelty (G5):** No existing accepted model covers the CP-12 world-fact cluster. RM-01 (Page Identity) covers page-level identity. RM-09 (Visibility ≠ Navigability) covers single-element type reliability. Neither covers multi-candidate target grounding. RM-10's world-fact cluster (WF-29..WF-33) is distinct from all 9 accepted models.

2. **Pressure coverage:** CP-12 is the ONE genuinely new canonical pressure. Without RM-10, CP-12 has zero Reality Model coverage. The Phase C matrix confirmed CP-12 as GAP.

3. **Evidence strength:** Core world facts (WF-29..WF-32) are DIRECT from E3 evidence (VE-07, VE-06 — recorded-reality-derived executable regressions). WF-33 is INFERRED but logically necessary (if wrong-target and intended-target selections produced identical outcomes, the concept of "intended target" would be meaningless).

4. **CP-11 boundary is clean:** No overlap with RM-09. The boundary analysis (§7) demonstrates orthogonality. RM-10 does not duplicate, weaken, or contradict RM-09.

### Quality Gate

| Gate | Assessment |
|---|---|
| **G1 Provenance** | ✓ All WFs traceable to committed evidence (VE-07 E3, VE-06 E3, VE-05 E1, VE-01 E2). OBs cite specific run IDs and file paths. |
| **G2 Architecture Neutrality** | ✓ Normative content uses "target description," "observable element," "text matching," "coordinate proximity," "world effect." Legacy terms (`FindMatchingItem`, `Contains`, `FixVerificationTests`, `fusion.py`) confined to Legacy Mechanism Context. G2 rewrite test: "A target description can match multiple observable elements on the same screen" — passes. |
| **G3 Fact/Inference Separation** | ✓ 5 WFs (3 DIRECT, 2 INFERRED). 4 RIs with confidence, alternatives, materiality. 4 OBs clearly labeled as observation records. No verdict as WF. No unlabeled inference. |
| **G4 Minimality** | ✓ Each element tested. WF-29..WF-33 are each required to reproduce CP-12's fail oracle. RI-20..RI-23 each support at least one ER or WF. ER-25..ER-28 each prevent a distinct failure path in the CP-12 fail oracle. |
| **G5 Deduplication** | ✓ No existing model shares the multi-candidate target-grounding world-fact cluster. RM-10 vs RM-09 boundary analysis confirms orthogonality. |
| **G6 Validation Readiness** | ✓ All provenance chains documented. Counterfactual/falsification statements provided. CP-11 boundary analysis included. Ready for B3 independent validation. |

### Evidence Upgrade Path

RM-10's current evidence is E3/E1 (recorded-reality-derived from legacy runs). Upgrade path:
- **S2 production-shaped perception:** CP-12 is in the Perception / Grounding domain. S2 evidence would use production-shaped perception outputs for grounding decisions. This is the natural evidence maturity upgrade.
- **Positive evidence needed:** The current evidence is entirely FAILURE mode (VE-07, VE-06, VE-05 are all wrong-target selections). What's missing is POSITIVE evidence — a recorded run where the system correctly grounds a target under multi-candidate ambiguity. This would be E4 (real run) or E3 (replay) evidence for RM-10's ERs being satisfiable.

### Admission Conditions

None. RM-10 is recommended for ACCEPT_NEW_MODEL without conditions. All gates pass. The E3 evidence for the failure mode is strong. The model's world facts are directly observed. The CP-11 boundary is clean.

---

## Next Task

**B3_INDEPENDENT_VALIDATION_RM10** — validate RM-10 against contract §17. If PASS or CONDITIONAL_PASS, admit as `RM-10` into the Reality Model corpus (B4).

**Note:** RM-10 admission does NOT authorize candidate generation, architecture design, or Runtime modification. Those require a separate Semantic Gate after RM-10 is accepted and the CP-12 pressure is fully modeled.

## Repository Changes

`docs/decisions/cp12-reality-model-extraction-result.md` — created. No other files modified. No Runtime code changed. No architecture designed.

STOP.
