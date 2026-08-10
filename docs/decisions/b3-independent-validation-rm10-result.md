# B3_INDEPENDENT_VALIDATION_RM10_RESULT

> Generated: 2026-08-09
> Role: Independent Validator (IV) — distinct from RM-10 Author
> Authority: `HUMAN_ADOPT_REALITY_MODEL_ADMISSION_CONTRACT` (2026-08-09)
> Candidate: RM-10 — Target Identity Grounding Under Multiple Perceptually-Matching Candidates
> Contract: `docs/system/reality-model-admission-contract.md` §17 (frozen v1.0)
> Precedent: `docs/decisions/b3-independent-reality-model-validation-result.md` (RM-01..RM-09)

---

## Validation Matrix

| Gate | Result | Notes |
|---|---|---|
| G1 Provenance | **COND** | WF-30, WF-32 support labels need correction (see Finding 1) |
| G2 Architecture Neutrality | **COND** | ER-26 "declared type" borderline — see Finding 2 |
| G3 Fact/Inference Separation | **PASS** | RI confidence/alternatives/materiality present; no verdict-as-WF |
| G4 Minimality | **PASS** | All 13 elements required; no redundancy found |
| G5 Deduplication | **PASS** | Distinct from RM-09 and RM-01; boundary analysis verified |
| G6 Counterfactual | **COND** | Falsification condition is valid but could be more specific — see Finding 3 |
| G7 ER Adequacy | **PASS** | All 4 ERs prevent distinct CP-12 fail-oracle paths |

**Overall: CONDITIONAL_PASS** — 3 conditions, all labeling/minor, none blocking.

---

## Findings

### Finding 1 — WF Support Label Corrections (G1)

**WF-30** is labeled DIRECT but cites "CP-12 core scenario" — a constructed example, not committed evidence. The claim "multiple matching elements may share the same type" is not directly observed in the cited E3/E2 evidence. VE-07 shows two elements matching "notifications" but they have DIFFERENT types (menu_item vs text). The scenario where both candidates share the same type ("Wi‑Fi" vs "Wi‑Fi Calling") is constructed from real-world knowledge of Android Settings, not from committed evidence.

**WF-32** is labeled DIRECT but the evidence (VE-01) shows the system's matching rule ACCEPTING coordinate proximity — this is a system behavior observation, not a world fact. The claim "coordinate proximity does not establish which element is the intended target" is an inference from: (a) VE-01 shows the system accepts coordinate proximity as identity, (b) cross-device layout variation means the same coordinates can point to different elements. The inference is valid, but the support kind should be INFERRED, not DIRECT.

**Required action:** Reclassify WF-30 from DIRECT → INFERRED. Reclassify WF-32 from DIRECT → INFERRED. Evidence citations remain valid; only the support kind changes.

**Impact:** Model confidence unchanged. The reclassification reflects the evidence chain more accurately — both facts are logically sound but derived from evidence + reasoning, not directly observed in committed artifacts.

---

### Finding 2 — ER-26 Borderline Legacy Vocabulary (G2)

**ER-26** states: "the system must confirm that the element's declared type supports the intended interaction."

"Declared type" is a perception-pipeline concept — the type label is DECLARED by the vision pipeline (YOLO → label mapping). The ER should be expressed in world-behavior terms: what observable property of the element must be verified, not what pipeline output must be checked.

**Recommended rewrite** (not executed — this is a validation finding, the Author decides):

> "Before dispatching an action on a selected element, the system must confirm that the element's observable properties are consistent with the intended interaction. An element whose observable properties indicate it is a text label must not be selected for a navigation action. An element whose observable properties indicate it is a navigation target must not be selected for a state-changing action."

This removes "declared type" (legacy mechanism concept) and replaces it with "observable properties" (world-behavior concept).

**Impact:** Minor. The current wording is borderline — "declared type" is close to "observable property" in practice but originates from the perception pipeline's vocabulary. Fixing it strengthens G2 without changing the ER's meaning.

---

### Finding 3 — Counterfactual Specificity (G6)

The falsification condition (§8) is:

> "RM-10 would be refuted if, for every real GUI screen observed, every target description matched exactly one element and that element was always the correct target."

This is logically correct but **untestable in practice** — it requires universal quantification ("for every real GUI screen"). A more useful falsification condition would specify a concrete, observable counterexample:

**Recommended addition:**

> "RM-10 would be refuted by a recorded run in which: (a) a target description matched exactly one element, (b) that element was selected and interacted with, (c) the post-interaction observation confirmed the element was the intended target, AND (d) this pattern held for ≥20 distinct target descriptions across ≥3 distinct GUI screens without a single wrong-target selection. In that world, the CP-12 pressure would be absent — text matching + coordinate proximity alone would be sufficient grounding."

This provides a concrete evidentiary threshold that could actually be observed and verified.

**Impact:** Low. The current falsification condition is logically valid. The recommended addition improves testability but is not required for G6 to pass.

---

### No-Finding Items

**G3 Fact/Inference Separation — PASS.** All 5 WFs carry explicit support kind (DIRECT/INFERRED). All 4 RIs carry confidence (3 HIGH, 1 MEDIUM), explicit alternatives considered, and materiality assessment. No RI is presented as WF. No verdict is embedded as a world fact. OB records are correctly classified as observation records (system behavior), not world facts.

**G4 Minimality — PASS.** Each of 13 elements (5 WFs + 4 OBs + 4 RIs + 4 ERs... wait, the OBs aren't subject to minimality — they're evidence records). For the normative elements (5 WFs + 4 RIs + 4 ERs):

| Element | If removed | CP-12 still reproducible? | Verdict |
|---|---|---|---|
| WF-29 (multi-match) | No multi-match world → CP-12 unnecessary | No | Required |
| WF-30 (same-type matches) | Type always disambiguates → CP-12 reduces to CP-11 | No | Required |
| WF-31 (text ≠ identity) | Text match always correct → CP-12 fail oracle impossible | No | Required |
| WF-32 (coordinates ≠ identity) | Coordinates always correct → CP-12 fail oracle impossible | No | Required |
| WF-33 (distinct world effect) | No post-selection signal → RI-22, ER-27 lose foundation | No | Required |
| RI-20 (text = filter) | Core inference linking WF-29/31 to ER-25 | No | Required |
| RI-21 (coordinates = aid) | Core inference linking WF-32 to ER-25 | No | Required |
| RI-22 (world effect distinguishes) | Links WF-33 to ER-27 | No | Required |
| RI-23 (ambiguity = world property) | Meta-inference — if removed, CP-12 framed as algorithm problem | No | Required |
| ER-25 (evidence beyond text) | Text-only selection permitted → VE-07 unfixed | No | Required |
| ER-26 (type consistency) | Type-blind selection permitted → VE-07 unfixed | No | Required |
| ER-27 (post-selection verify) | No destination check → VE-05/VE-06 undetected | No | Required |
| ER-28 (no guess) | First-match selection permitted → any multi-candidate case | No | Required |

All elements required. No redundancy. ✓

**G5 Deduplication — PASS.**

- **RM-10 vs RM-09:** RM-09 addresses single-element type reliability (CP-11). RM-10 addresses multi-candidate target selection (CP-12). The boundary analysis (§7) is verified correct: CP-11 and CP-12 are orthogonal. The "Wi‑Fi" / "Wi‑Fi Calling" example proves both can be correctly typed (CP-11 satisfied) while target grounding still fails (CP-12 unsolved). No merge.
- **RM-10 vs RM-01:** RM-01 addresses page-level identity from element inventory (CP-13). RM-10 addresses element-level identity from candidate set (CP-12). Different granularity, different CP. No merge.
- **RM-10 vs RM-05:** RM-05 addresses post-navigation page-change verification (CP-02). RM-10 ER-27 addresses post-selection destination verification (CP-12). They operate at different points: RM-05 verifies that ANY navigation produced a page change; RM-10 ER-27 verifies that the SPECIFIC intended destination was reached. Related but distinct. No merge.
- **Novelty test:** RM-10's world-fact cluster (multi-candidate target grounding) is not covered by any existing model. Removing RM-10 would leave CP-12 without model coverage. ✓

**G7 ER Adequacy — PASS.**

CP-12 fail oracle: system selects wrong element as target → dispatches action → wrong or no world effect.

| ER | Fail-oracle path prevented | Evidence |
|---|---|---|
| ER-25 (evidence beyond text) | Text-only selection → substring overmatch | VE-07: "Flash notifications" selected for "notifications" |
| ER-26 (type consistency) | Type-blind selection → text element tapped for navigation | VE-07: text element selected for navigation action |
| ER-27 (post-selection verify) | No destination check → wrong-target undetected | VE-05: double-click same page; VE-06: search UI self-loop |
| ER-28 (no guess) | First-match selection under ambiguity | Multi-candidate case: any scenario where N>1 candidates match |

If ER-25..ER-28 are enforced, all four distinct CP-12 fail-oracle paths are prevented. ✓

**ER language check:** All four ERs describe external behavior requirements:
- ER-25: "use evidence beyond text matching" → behavioral, not implementational
- ER-26: "confirm element's declared type supports intended interaction" → borderline (Finding 2) but behavioral intent is clear
- ER-27: "observe whether resulting world state is consistent" → purely behavioral
- ER-28: "must not arbitrarily select; must signal ambiguity" → purely behavioral

No ER prescribes algorithms, models, architectures, or implementation mechanisms. ✓

---

## Final Verdict

**CONDITIONAL_PASS**

Three conditions, all labeling/minor:

| ID | Condition | Owner | Resolution |
|---|---|---|---|
| C-RM10-01 | WF-30 reclassify DIRECT → INFERRED | RM-10 Author | Before B4 admission |
| C-RM10-02 | WF-32 reclassify DIRECT → INFERRED | RM-10 Author | Before B4 admission |
| C-RM10-03 | ER-26 "declared type" → "observable properties" | RM-10 Author | Before B4 admission (recommended, not mandatory) |

**No condition blocks corpus entry.** C-RM10-01 and C-RM10-02 are labeling corrections — the evidence chain is valid, only the support kind label is inaccurate. C-RM10-03 is a recommended wording improvement for G2 robustness.

**RM-10 is valid.** The model addresses a genuine reality gap (CP-12 — the ONE canonical pressure without model coverage). The CP-11 boundary is clean and independently verified. All 7 gates pass or pass with minor conditions.

## Next Task

**B4_REALITY_MODEL_ADMISSION_RM10** — resolve 3 conditions, issue admission outcome, admit RM-10 to corpus.

## Repository Changes

`docs/decisions/b3-independent-validation-rm10-result.md` — created. No other files modified. RM-10 content not modified (conditions referred to Author).

STOP.
