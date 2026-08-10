# B4_REALITY_MODEL_ADMISSION_RM10_RESULT

> Generated: 2026-08-09
> Role: Reality Governance Architect (RG)
> Phase: PHASE_D_CP12_TARGET_GROUNDING → CORPUS_ADMISSION
> Authority: `HUMAN_ADOPT_REALITY_MODEL_ADMISSION_CONTRACT` (2026-08-09)
> Candidate: RM-10 — Target Identity Grounding Under Multiple Perceptually-Matching Candidates
> Contract: `docs/system/reality-model-admission-contract.md` §18 (frozen v1.0)

---

## Condition Resolution

| Condition | Element | Change | Status |
|---|---|---|---|
| C-RM10-01 | WF-30 | DIRECT → INFERRED | **RESOLVED** |
| C-RM10-02 | WF-32 | DIRECT → INFERRED | **RESOLVED** |
| C-RM10-03 | ER-26 | "declared type" → "observable properties" | **RESOLVED** |

### C-RM10-01 — WF-30 Reclassified

**Before:** `DIRECT` — "Multiple matching elements may share the same type classification"

**After:** `INFERRED`

**Inference chain:**
1. WF-29 (DIRECT, VE-07 E3): a single target description can match multiple observable elements on the same screen
2. Android Settings home screen contains multiple `menu_item` elements with overlapping text ("Wi‑Fi", "Wi‑Fi Calling", "Bluetooth", "Connected devices")
3. If N elements match a target description and all N are drawn from the same element inventory, some of them will share the same type classification by the pigeonhole principle (more candidates than distinct types)
4. Therefore: multiple matching elements may share the same type

**Confidence:** HIGH — the inference follows from WF-29 + real-world GUI structure knowledge. The "Wi‑Fi" / "Wi‑Fi Calling" example is not directly observed in committed evidence but is a verifiable property of Android Settings.

**Supporting evidence:** WF-29 (DIRECT), VE-07 (E3, two elements match one target), real-world Android Settings layout.

**Alternatives:** (a) type classification always perfectly disambiguates candidates — refuted by the pigeonhole argument (N candidates, <N distinct types → at least two share a type). (b) multiple same-type matches are a perception artifact — refuted by the "Wi‑Fi" / "Wi‑Fi Calling" case (both are genuinely distinct menu_item elements, correctly classified).

### C-RM10-02 — WF-32 Reclassified

**Before:** `DIRECT` — "Coordinate proximity does not establish which element is the intended target"

**After:** `INFERRED`

**Supporting observations:**
- OB-26 (VE-01, E2): golden matching accepts Euclidean distance ≤0.08–0.1 as "correct identification" — the system's matching rule treats coordinate proximity as identity
- Cross-device layout variation: the same logical element ("Wi‑Fi" menu_item) appears at different normalized coordinates on different devices, screen sizes, and OS versions
- Two distinct elements can be at nearby coordinates on the same screen (adjacent menu items in a list)

**Inference method:** The observation that coordinate proximity is accepted as identity by the system does not directly prove it is insufficient as a world fact. The inference step is: (a) the system's rule is "coordinate proximity = identity" → (b) but coordinates are device-layout-specific → (c) therefore, coordinate proximity does not logically entail semantic identity. Step (b) is real-world GUI knowledge, not an observation in committed evidence.

**Confidence:** HIGH — the inference is logically sound. Coordinate proximity is a metric property; semantic identity is a categorical property. No metric property can logically entail a categorical property without additional premises.

**Materiality:** HIGH — if coordinate proximity DID establish identity, CP-12 would not exist for coordinate-based grounding. The entire CP-12 fail oracle for coordinate-only taps depends on this fact.

### C-RM10-03 — ER-26 Wording Normalized

**Before:** "the system must confirm that the element's **declared type** supports the intended interaction"

**After:** "the system must confirm that the element's **observable properties** are consistent with the intended interaction. An element whose observable properties indicate it is a text label must not be selected for a navigation action. An element whose observable properties indicate it is a navigation target must not be selected for a state-changing action."

**Rationale:** "Declared type" originates from the perception pipeline's vocabulary (YOLO label → type mapping → "declared" type). ERs must describe external behavior requirements in world terms. "Observable properties" describes what the system can observe about the element (visual appearance, spatial context, text content, interaction behavior) without referencing how the perception pipeline produces its type labels.

**Meaning preserved:** The ER still requires type-consistency checking before action dispatch. The change is purely lexical — from implementation vocabulary to behavioral vocabulary.

---

## Final Validation (Post-Condition-Resolution)

| Gate | Result | Notes |
|---|---|---|
| G1 Provenance | **PASS** | WF-29/31 DIRECT from E3. WF-30/32/33 INFERRED with documented inference chains, confidence, alternatives, and materiality. All OBs cite specific run IDs and file paths. |
| G2 Architecture Neutrality | **PASS** | "declared type" removed from ER-26. All normative content uses world-behavior vocabulary: "observable properties," "target description," "text matching," "coordinate proximity," "world effect." Legacy terms confined to Legacy Mechanism Context. |
| G3 Fact/Inference Separation | **PASS** | 5 WFs: 2 DIRECT, 3 INFERRED — correctly labeled. 4 RIs: confidence/alternatives/materiality present. No verdict-as-WF. No unlabeled inference. |
| G4 Minimality | **PASS** | All 13 normative elements required. Independently verified in B3. |
| G5 Deduplication | **PASS** | RM-10 world-fact cluster distinct from RM-09 (single-element type reliability) and RM-01 (page-level identity). Boundary verified orthogonal. |
| G6 Counterfactual | **PASS** | Falsification condition stated: "if every target description matched exactly one element and that element was always correct, CP-12 would be unnecessary." Already falsified by VE-07. Model adjustment scenario specified. |
| G7 ER Adequacy | **PASS** | ER-25..ER-28 each prevent a distinct CP-12 fail-oracle path. All ERs expressed as behavioral requirements. No implementation prescriptions. |

**All 7 gates PASS. No conditions remain.**

---

## Admission Decision

### RM-10 — Target Identity Grounding Under Multiple Perceptually-Matching Candidates

| Field | Final Value |
|---|---|
| **RM-ID** | `RM-10` |
| **Title** | Target Identity Grounding Under Multiple Perceptually-Matching Candidates |
| **Type** | MODEL |
| **Primary CP** | CP-12 (Target Grounding Must Verify Semantic Identity Beyond Coordinate/Text Match) |
| **Admission Outcome** | **ACCEPT_NEW_MODEL** |
| **Admission Date** | 2026-08-09 |
| **Validation** | PASS (all 7 gates, post-condition-resolution) |

| Metric | Value |
|---|---|
| World Facts | 5 (WF-29..WF-33: 2 DIRECT E3/E2, 3 INFERRED) |
| Observation Records | 4 (OB-23..OB-26: E3/E2/E1) |
| Reality Inferences | 4 (RI-20..RI-23: 3 HIGH, 1 MEDIUM) |
| Expected Requirements | 4 (ER-25..ER-28) |
| Evidence Strength | E3 (recorded-reality-derived failure mode) |
| Confidence | HIGH (core WFs directly observed; inferences logically sound) |
| Conditions | None open |

---

## Corpus Reconciliation

### Corpus Growth

| Metric | Before (RM-01..RM-09) | After (+RM-10) |
|---|---|---|
| Accepted Models | 9 | **10** |
| World Facts | 28 | **33** |
| Reality Inferences | 19 | **23** |
| Expected Requirements | 24 | **28** |
| CPs Covered | 12 of 14 | **13 of 14** |

### CP Coverage Update

| CP | Before | After |
|---|---|---|
| CP-01..CP-11, CP-13 | COVERED (9 RMs) | COVERED (unchanged) |
| **CP-12** | **GAP** | **COVERED — RM-10** |
| CP-14 | GAP (FUTURE_CAPABILITY) | GAP (unchanged — Phase 5/6) |

**13 of 14 canonical pressures now covered by accepted Reality Models.** CP-14 remains the sole gap — explicitly deferred to Phase 5/6 (Intent→Goal/Plan synthesis).

### RM-09 Boundary — Verified Unchanged

RM-10 admission does not modify, weaken, or overlap with RM-09 (Visibility ≠ Navigability, CP-11). The boundary analysis from RM-10 §7 is confirmed:

- **RM-09 scope:** ONE element — "is its type classification correct?"
- **RM-10 scope:** N candidates — "which one is the intended target?"
- **Orthogonal:** you can have perfect type classification and still fail target grounding ("Wi‑Fi" vs "Wi‑Fi Calling")

No merge, no variant, no evidence attachment — RM-10 is a genuinely new world-fact cluster.

### RM-01 Boundary — Verified Unchanged

RM-01 (Page Identity, CP-13) addresses page-level identity from element inventory. RM-10 addresses element-level identity from candidate set. Different granularity. No overlap.

### Corpus Index Update

The Reality Model corpus now contains:

| ID | Title | CP | Status |
|---|---|---|---|
| `RM-01` | Page Inventory | CP-13 | ACCEPTED |
| `RM-02` | Multi-Branch Hub | CP-04 | ACCEPTED |
| `RM-03` | Goal Satisfaction | CP-06 | ACCEPTED |
| `RM-04` | Entry Verification | CP-01 | ACCEPTED |
| `RM-05` | Navigation Change | CP-02 | ACCEPTED |
| `RM-06` | Depth Bound | CP-07 | ACCEPTED |
| `RM-07` | Observation Failure | CP-08 | ACCEPTED |
| `RM-08` | Recovery ≠ Reset | CP-10 | ACCEPTED |
| `RM-09` | Visibility ≠ Nav | CP-11 | ACCEPTED |
| **`RM-10`** | **Target Grounding** | **CP-12** | **ACCEPTED** |

---

## Explicit Boundary

RM-10 admission authorizes:

- RM-10 as a normative Reality Model in the corpus
- CP-12 as COVERED in the pressure × reality matrix
- RM-10's WFs, RIs, and ERs as authoritative for CP-12

RM-10 admission does **NOT** authorize:

- GroundingEngine design or implementation
- Candidate model generation (Phase D candidate generation requires a separate Semantic Gate)
- Matching algorithm specification
- Vision architecture changes
- Perception pipeline modifications
- Runtime code changes
- Any architecture or implementation activity

RM-10 describes **what reality requires.** It does not describe **how to build it.** The gap between "CP-12 is modeled" and "CP-12 is implemented" is bridged by a future Semantic Gate for candidate generation — not by this admission.

---

## Next Phase Recommendation

**CP12_CAPABILITY_SEMANTIC_GATE_PREPARATION**

With CP-12 now having an accepted Reality Model (RM-10), the next phase is to prepare a Semantic Gate that authorizes candidate generation for the CP-12 capability. The gate would:

1. Define the capability boundary (what the system must do to satisfy RM-10's ERs)
2. Produce Scenario Contracts for target grounding verification
3. Establish architecture constraints for the grounding capability
4. Authorize Candidate generation (Phase D proper)

This is a recommendation only — not executed by this artifact.

## Repository Changes

`docs/decisions/b4-reality-model-admission-rm10-result.md` — created (this report). No other files modified. No Runtime code changed. No architecture designed.

STOP.
