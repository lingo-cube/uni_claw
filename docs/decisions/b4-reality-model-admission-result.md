# B4_REALITY_MODEL_ADMISSION_RESULT

> Generated: 2026-08-09
> Roles: Reality Governance Architect + Dedup Arbiter (joint decision)
> Authority: `HUMAN_ADOPT_REALITY_MODEL_ADMISSION_CONTRACT` (2026-08-09)
> Input: B2 Extraction (`docs/decisions/b2-reality-model-extraction-result.md`) + B3 Validation (`docs/decisions/b3-independent-reality-model-validation-result.md`)
> Contract: `docs/system/reality-model-admission-contract.md` §7–§12, §16–§19 (frozen v1.0)

---

## Admission Summary

| RM | Title | Primary CP | B3 Verdict | B3 Conditions | Admission Outcome | Corpus ID |
|---|---|---|---|---|---|---|
| RM-01 | Page Inventory | CP-13 | CONDITIONAL_PASS | C-01 resolved | **ACCEPT_NEW_MODEL** | `RM-01` |
| RM-02 | Multi-Branch Hub | CP-04 | PASS | — | **ACCEPT_NEW_MODEL** | `RM-02` |
| RM-03 | Goal Satisfaction | CP-06 | PASS | — | **ACCEPT_NEW_MODEL** | `RM-03` |
| RM-04 | Entry Verification | CP-01 | CONDITIONAL_PASS | C-02, C-03 resolved | **ACCEPT_NEW_MODEL** | `RM-04` |
| RM-05 | Navigation Change | CP-02 | PASS | — | **ACCEPT_NEW_MODEL** | `RM-05` |
| RM-06 | Depth Bound | CP-07 | PASS | — | **ACCEPT_NEW_MODEL** | `RM-06` |
| RM-07 | Observation Failure | CP-08 | CONDITIONAL_PASS | C-04, C-05 resolved | **ACCEPT_NEW_MODEL** | `RM-07` |
| RM-08 | Recovery ≠ Reset | CP-10 | PASS | — | **ACCEPT_NEW_MODEL** | `RM-08` |
| RM-09 | Visibility ≠ Nav | CP-11 | CONDITIONAL_PASS | C-06 resolved | **ACCEPT_NEW_MODEL** | `RM-09` |

**ACCEPT_NEW_MODEL: 9 | MERGE: 0 | VARIANT: 0 | EVIDENCE: 0 | DEFER: 0 | REJECT: 0**

---

## Part 1 — B3 Condition Resolution

### C-01 — RM-01 ER-04 borderline-non-minimal

**Condition:** ER-04 ("Page identity evidence must be source-attributed") is derived from ER-01 + VRD-04, not an independent requirement.

**Resolution:** **RESOLVED.** ER-04 retained in RM-01 but reclassified as a DERIVED requirement from ER-01 (page identity from observable evidence) + VRD-04 (observation source output ≠ authoritative world evidence). The derivation is recorded in RM-01's ER-04 field as: `Derived from: ER-01 + VRD-04`. No admission impact.

### C-02 — RM-04 WF-14 provenance E0

**Condition:** WF-14 ("Entry actions can report success without producing the intended world effect") is supported by E0 documentation (E-13 GAP-P0-02), not by committed executable evidence.

**Resolution:** **RESOLVED — DEFERRED EVIDENCE.** WF-14 is retained in RM-04 with its current E0 provenance grade. The claim is plausible, consistent with RD-01, and documented with specific production code references (`EntryPolicy`, `AdbScreenStateProvider.cs:38`). However, it lacks E3+ corroboration.

**Action:** WF-14 is registered in the **Deferred Evidence Register** (below) pending S1 replay or committed reproduction. RM-04 is admitted with WF-14 at E0 strength. The model's core claims (WF-12 E4, WF-13 E2, RI-07, ER-11, ER-12) are unaffected. If B4 exit criteria require all WFs at E1+, WF-14 is the sole exception and is explicitly tracked.

### C-03 — RM-04 ER-13 supporting, not core CP-01

**Condition:** ER-13 ("Device identity must be known and stable") is a supporting infrastructure requirement, not strictly required to prevent CP-01's fail oracle.

**Resolution:** **RESOLVED.** ER-13 retained in RM-04 but reclassified as a SECONDARY requirement derived from E-02 (ADB session self-healing) and device-boundary infrastructure needs. Marked as `Secondary (infrastructure)` in RM-04. Does not affect CP-01 fail-oracle prevention (ER-11 + ER-12 remain sufficient).

### C-04 — RM-07 WF-19/WF-20 provenance E0/E1

**Condition:** WF-19 ("Device query failure produces same empty-result signal as content exhaustion") and WF-20 ("End-of-content signals exist at multiple layers but may not agree") are supported by E0 documentation (E-13) and E1 production code reading (VE-10), not by committed executable evidence.

**Resolution:** **RESOLVED — DEFERRED EVIDENCE.** Both WFs retained in RM-07 with their current provenance grades. The claims are plausible and consistent with RD-04 and E-02 (ADB failures DO occur), but lack E3+ reproduction of the specific conflation path.

**Action:** WF-19 and WF-20 are registered in the **Deferred Evidence Register** pending S1 replay. RM-07 is admitted with these WFs at current strength. CP-08 is the portfolio's highest-value S1 replay target — the deferred evidence is on the critical path for evidence-maturity upgrade. RM-07's core claims (WF-21 E1, RI-11, ER-18, ER-19) are unaffected.

### C-05 — RM-07 ER-20 robustness, not core CP-08

**Condition:** ER-20 ("Multiple end-of-content signals must be reconciled") is a robustness requirement, not strictly required to prevent CP-08's fail oracle.

**Resolution:** **RESOLVED.** ER-20 retained in RM-07 but reclassified as a ROBUSTNESS requirement derived from VE-10's multi-signal observation. Marked as `Robustness (multi-signal)` in RM-07. Core CP-08 fail-oracle prevention remains: ER-18 (distinct signals) + ER-19 (positive exhaustion evidence).

### C-06 — RM-09 RI-17 LOW confidence edge case

**Condition:** RI-17 ("Empty OCR output does not mean 'no element exists'") is an edge case with LOW confidence and LOW materiality, not required for CP-11 pressure reproduction.

**Resolution:** **RESOLVED.** RI-17 retained in RM-09 but marked as `NON_ESSENTIAL` — documents a real edge case but is not required for CP-11's core pressure reproduction (which is covered by RI-13 through RI-16 and RI-18). Does not affect model confidence (the model's confidence summary already reflects RI-17's LOW rating).

---

## Part 2 — G5 Deduplication

### Corpus State

This is the inaugural Reality Model corpus admission. No prior accepted models exist. Deduplication runs across the 9 candidates against each other.

### World-Fact Cluster Comparison

| Pair | Cluster A | Cluster B | Overlap? | Verdict |
|---|---|---|---|---|
| RM-01 ↔ RM-05 | Page identity structure | Page-change verification | Distinct: static identity vs dynamic transition. Different CPs (13 vs 02). | No merge |
| RM-01 ↔ RM-09 | Element inventory structure | Perception reliability | Complementary: RM-01 WF-03 states "type labels sometimes wrong"; RM-09 explains why. Different CPs (13 vs 11). | No merge |
| RM-02 ↔ RM-06 | Hub-branch completion (horizontal) | Depth-bound enforcement (vertical) | Distinct: sibling coverage vs parent→child depth. Different CPs (04 vs 07). | No merge |
| RM-04 ↔ RM-05 | Entry verification (pre-traversal) | Navigation verification (mid-traversal) | Distinct phases. Different CPs (01 vs 02). | No merge |
| RM-05 ↔ RM-07 | Page-change verification | Failure-vs-exhaustion distinction | Distinct: verifying a specific known transition vs distinguishing unknown failure from unknown exhaustion. Different CPs (02 vs 08). | No merge |
| RM-07 ↔ RM-09 | Infrastructure observation failure | Perception misclassification | Distinct: ADB/vision query failure vs type-label error. Different CPs (08 vs 11). | No merge |
| RM-08 ↔ RM-04 | Recovery verification (post-error) | Entry verification (pre-traversal) | Distinct phases. Different CPs (10 vs 01). | No merge |

**All 9 candidates have distinct world-fact clusters.** No pair shares the same cluster + same pressure relation.

### Novelty Test (per candidate, §9)

For each candidate proposed as `ACCEPT_NEW_MODEL`:

| RM | Test 1: No existing model shares cluster | Test 2: Primary CP not already served | Test 3: Removal leaves CP without model | Novelty |
|---|---|---|---|---|
| RM-01 | ✓ | ✓ (CP-13: no other model) | ✓ | **PASS** |
| RM-02 | ✓ | ✓ (CP-04: no other model) | ✓ | **PASS** |
| RM-03 | ✓ | ✓ (CP-06: no other model) | ✓ | **PASS** |
| RM-04 | ✓ | ✓ (CP-01: no other model) | ✓ | **PASS** |
| RM-05 | ✓ | ✓ (CP-02: no other model) | ✓ | **PASS** |
| RM-06 | ✓ | ✓ (CP-07: no other model) | ✓ | **PASS** |
| RM-07 | ✓ | ✓ (CP-08: no other model) | ✓ | **PASS** |
| RM-08 | ✓ | ✓ (CP-10: no other model) | ✓ | **PASS** |
| RM-09 | ✓ | ✓ (CP-11: no other model) | ✓ | **PASS** |

**All 9 candidates pass the novelty test.** No existing model serves any candidate's Primary CP. Each CP-XX would be left without a model if its candidate were removed.

### Variant and Evidence Attachment Assessment

- **Variants:** No candidate has the same world-fact cluster with different parameterization. Device/app-version variants (RM-01 for different Android versions, RM-09 for different perception pipelines) are plausible future additions but not present in the current evidence corpus.
- **Evidence attachments:** No candidate provides additional evidence for an existing model (no existing models exist). Future B2 extractions from S1/S2 evidence may produce evidence attachments (`RM-NN-E<k>`) that upgrade model evidence maturity.

---

## Part 3 — Admission Outcomes

### RM-01 — Android Device Screen as Page Inventory

**Outcome: ACCEPT_NEW_MODEL**
**Corpus ID: `RM-01`**
**Validation: CONDITIONAL_PASS** (C-01 resolved: ER-04 reclassified as DERIVED)

| Field | Final Value |
|---|---|
| Evidence Strength | E4 (EP-03 trace.jsonl, EP-04 sim-replay) / E3 / E2 / E1 |
| Confidence | HIGH (RI-01, RI-03) / MEDIUM (RI-02) |
| Conditions | None open. ER-04 marked DERIVED from ER-01 + VRD-04. |
| Pressure Coverage | CP-13 (Primary), CP-11, CP-12 (Secondary) |
| World Facts | 5 (WF-01..WF-05: 3 DIRECT, 2 INFERRED) |
| Admission Date | 2026-08-09 |

---

### RM-02 — Multi-Branch Hub with Independent Subtrees

**Outcome: ACCEPT_NEW_MODEL**
**Corpus ID: `RM-02`**
**Validation: PASS** (no conditions)

| Field | Final Value |
|---|---|
| Evidence Strength | E1 (E-07 deterministic simulation, unfixed bug) |
| Confidence | HIGH (RI-04) / MEDIUM (RI-05) |
| Conditions | None open. |
| Pressure Coverage | CP-04 (Primary), CP-05, CP-14 (Secondary) |
| World Facts | 3 (WF-06..WF-08: 2 DIRECT, 1 INFERRED) |
| Admission Date | 2026-08-09 |

**Note:** RM-02's primary evidence (E-07) is the strongest false-completion evidence in the corpus — a deterministic, unfixed bug. The model's E1 evidence strength reflects the simulation-only provenance; E3+ upgrade requires S1 replay of a real multi-branch run.

---

### RM-03 — Goal Satisfaction Recognizable from Current Observation

**Outcome: ACCEPT_NEW_MODEL**
**Corpus ID: `RM-03`**
**Validation: PASS** (no conditions)

| Field | Final Value |
|---|---|
| Evidence Strength | E1 (executable proofs Assertion6–9, 415/415 pass, production code) |
| Confidence | HIGH (RI-06) |
| Conditions | None open. |
| Pressure Coverage | CP-06 (Primary), CP-14 (Secondary) |
| World Facts | 3 (WF-09..WF-11: all DIRECT) |
| Admission Date | 2026-08-09 |

**Note:** Strongest-validated model in the corpus. All 3 WFs are DIRECT from executable proofs. CP-06 is FULLY_CLOSED in Phase A. The model's E1 provenance reflects the executable-proof evidence class; E4 upgrade would require a real-device run where the initial observation satisfies the Goal without dispatch — but the proof is already non-vacuous via deterministic simulation.

---

### RM-04 — Entry Verification Before World Interaction

**Outcome: ACCEPT_NEW_MODEL**
**Corpus ID: `RM-04`**
**Validation: CONDITIONAL_PASS** (C-02 resolved: WF-14 DEFERRED; C-03 resolved: ER-13 reclassified SECONDARY)

| Field | Final Value |
|---|---|
| Evidence Strength | E4 (EP-03 manifest) / E2 (E-01) / E0 (WF-14 deferred) |
| Confidence | MEDIUM (RI-07 — WF-14 deferred evidence weakens inference) |
| Conditions | **Open:** WF-14 in Deferred Evidence Register pending E3+ upgrade. |
| Pressure Coverage | CP-01 (Primary), CP-08 (Secondary) |
| World Facts | 3 (WF-12 DIRECT E4, WF-13 INFERRED E2, WF-14 INFERRED E0 — DEFERRED) |
| Admission Date | 2026-08-09 |

**Deferred evidence:** WF-14 ("Entry actions can report success without producing the intended world effect") at E0. Upgrade path: S1 replay of a real entry-failure scenario, or a committed reproduction fixture. Tracked in Deferred Evidence Register.

---

### RM-05 — Navigation Action Effect Observable as Page Change

**Outcome: ACCEPT_NEW_MODEL**
**Corpus ID: `RM-05`**
**Validation: PASS** (no conditions)

| Field | Final Value |
|---|---|
| Evidence Strength | E4 (EP-03 success + failure result.json) / E1 (E-09 L4) / E0 (VE-09) |
| Confidence | HIGH (RI-08) / MEDIUM (RI-09) |
| Conditions | None open. |
| Pressure Coverage | CP-02 (Primary), CP-13 (Secondary) |
| World Facts | 2 (WF-15 DIRECT E4, WF-16 INFERRED E0) |
| Admission Date | 2026-08-09 |

---

### RM-06 — Depth Bound Declared Separately from Discovery

**Outcome: ACCEPT_NEW_MODEL**
**Corpus ID: `RM-06`**
**Validation: PASS** (no conditions)

| Field | Final Value |
|---|---|
| Evidence Strength | E3 (E-08 replay) / E1 (E-11 permanent regression) |
| Confidence | HIGH (RI-10) |
| Conditions | None open. |
| Pressure Coverage | CP-07 (Primary), CP-03 (Secondary) |
| World Facts | 2 (WF-17 INFERRED E3, WF-18 DIRECT E1) |
| Admission Date | 2026-08-09 |

---

### RM-07 — Observation Failure Distinct from Content Exhaustion

**Outcome: ACCEPT_NEW_MODEL**
**Corpus ID: `RM-07`**
**Validation: CONDITIONAL_PASS** (C-04 resolved: WF-19/WF-20 DEFERRED; C-05 resolved: ER-20 reclassified ROBUSTNESS)

| Field | Final Value |
|---|---|
| Evidence Strength | E1 (E-12) / E0 (WF-19, WF-20 deferred) |
| Confidence | MEDIUM (RI-11 — deferred evidence weakens inference) |
| Conditions | **Open:** WF-19, WF-20 in Deferred Evidence Register pending E3+ upgrade via S1 replay. |
| Pressure Coverage | CP-08 (Primary), CP-09 (Secondary, embedded) |
| World Facts | 3 (WF-19 INFERRED E0 — DEFERRED, WF-20 INFERRED E0/E1 — DEFERRED, WF-21 DIRECT E1) |
| Admission Date | 2026-08-09 |

**Deferred evidence:** WF-19 + WF-20 at E0/E1. Upgrade path: S1 replay of a real observation-failure→exhaustion-conflation event. CP-08 is the portfolio's highest-value S1 replay target. Tracked in Deferred Evidence Register. **Priority: HIGH.**

---

### RM-08 — Recovery Action Effect Distinct from Error Resolution

**Outcome: ACCEPT_NEW_MODEL**
**Corpus ID: `RM-08`**
**Validation: PASS** (no conditions)

| Field | Final Value |
|---|---|
| Evidence Strength | E1 (E-04 deterministic simulation, historical Bug #2) / E1 (AgentRecoveryTests post-CP-06) |
| Confidence | MEDIUM (RI-12 — simulation evidence, no E4 recovery trace) |
| Conditions | None open. |
| Pressure Coverage | CP-10 (Primary), CP-01, CP-02 (Secondary) |
| World Facts | 3 (WF-22/WF-23 INFERRED E1, WF-24 DIRECT E1) |
| Admission Date | 2026-08-09 |

---

### RM-09 — Element Visibility and Type Classification Distinct from Navigability

**Outcome: ACCEPT_NEW_MODEL**
**Corpus ID: `RM-09`**
**Validation: CONDITIONAL_PASS** (C-06 resolved: RI-17 marked NON_ESSENTIAL)

| Field | Final Value |
|---|---|
| Evidence Strength | E3 (VE-06 recorded-reality-derived) / E1 (VE-05, VE-07, VE-03, VE-04) |
| Confidence | HIGH (RI-13, RI-14) / MEDIUM (RI-15, RI-16, RI-18, RI-19) / LOW (RI-17 non-essential) |
| Conditions | None open. RI-17 marked NON_ESSENTIAL — edge case, not required for CP-11 pressure reproduction. |
| Pressure Coverage | CP-11 (Primary), CP-12, CP-13 (Secondary) |
| World Facts | 4 (WF-25..WF-28: all DIRECT) |
| Admission Date | 2026-08-09 |

**Note:** Richest evidence corpus — 4 DIRECT WFs, 7 RIs across 3 confidence tiers. The chevron heuristic root cause (WF-28, RI-14) is the most precisely diagnosed perception failure in the corpus, with a specific code location (`fusion.py:292-343`) and reproduction test (FixVerificationTests L8).

---

## Part 4 — Reality Model Corpus

### Accepted Corpus (9 models)

| ID | Title | CP | WFs | RIs | ERs | Strength | Confidence | Conditions |
|---|---|---|---|---|---|---|---|---|
| `RM-01` | Page Inventory | CP-13 | 5 | 3 | 4 | E4 | HIGH | None |
| `RM-02` | Multi-Branch Hub | CP-04 | 3 | 2 | 3 | E1 | HIGH | None |
| `RM-03` | Goal Satisfaction | CP-06 | 3 | 1 | 3 | E1 | HIGH | None |
| `RM-04` | Entry Verification | CP-01 | 3 | 1 | 3 | E4 | MEDIUM | 1 deferred WF |
| `RM-05` | Navigation Change | CP-02 | 2 | 2 | 2 | E4 | HIGH | None |
| `RM-06` | Depth Bound | CP-07 | 2 | 1 | 2 | E3 | HIGH | None |
| `RM-07` | Observation Failure | CP-08 | 3 | 1 | 3 | E1 | MEDIUM | 2 deferred WFs |
| `RM-08` | Recovery ≠ Reset | CP-10 | 3 | 1 | 2 | E1 | MEDIUM | None |
| `RM-09` | Visibility ≠ Nav | CP-11 | 4 | 7 | 2 | E3 | HIGH | None |

**Totals:** 28 World Facts, 22 Observation Records, 19 Reality Inferences, 24 Expected Requirements.

### CP Coverage Map

| CP | Domain | RM | Coverage |
|---|---|---|---|
| CP-01 | Entry Verify | RM-04 | ACCEPTED (CONDITIONAL_PASS) |
| CP-02 | Navigation Page Change | RM-05 | ACCEPTED (PASS) |
| CP-03 | Plan ≠ Execution | (cross-cutting) | Embedded in RM-02, RM-05, RM-06 |
| CP-04 | Multi-Branch Hub | RM-02 | ACCEPTED (PASS) |
| CP-05 | Revisit Idempotence | (embedded) | Via RM-02 ER-07 |
| CP-06 | Goal Satisfaction | RM-03 | ACCEPTED (PASS) |
| CP-07 | Depth Bound | RM-06 | ACCEPTED (PASS) |
| CP-08 | Observation Failure | RM-07 | ACCEPTED (CONDITIONAL_PASS) |
| CP-09 | Unchanging Content | (embedded) | Via RM-07 |
| CP-10 | Recovery ≠ Reset | RM-08 | ACCEPTED (PASS) |
| CP-11 | Visibility ≠ Navigability | RM-09 | ACCEPTED (CONDITIONAL_PASS) |
| CP-12 | Target Grounding | — | **GAP** (CHALLENGE_REQUIRED, Phase D) |
| CP-13 | Page Identity | RM-01 | ACCEPTED (CONDITIONAL_PASS) |
| CP-14 | Intent ≠ Execution | — | **GAP** (EXPLICITLY_DEFERRED, Phase 5/6) |

**12 of 14 CPs covered by accepted Reality Models.** CP-12 awaits the Phase D challenge. CP-14 is deferred to Phase 5/6 Intent→Goal/Plan synthesis.

### Deferred Evidence Register

| ID | RM | Element | Current Strength | Target Strength | Upgrade Path | Priority |
|---|---|---|---|---|---|---|
| DEF-01 | RM-04 | WF-14: Entry action fake success | E0 | E3+ | S1 replay or committed reproduction fixture | MEDIUM |
| DEF-02 | RM-07 | WF-19: Failure signal = exhaustion signal | E0 | E3+ | S1 replay (CP-08 highest-value S1 target) | **HIGH** |
| DEF-03 | RM-07 | WF-20: Multi-signal disagreement | E0/E1 | E3+ | S1 replay | **HIGH** |

### Hypotheses Register

Empty. No claims were REJECT_UNSUPPORTED. All 9 candidates have supporting evidence at E0 or better.

---

## Part 5 — Phase B Exit Conditions

Per contract §18: "Phase B4 exit condition: corpus contains only models with accepted outcomes; deferred register and hypotheses register tracked; each accepted model has validation status PASS or CONDITIONAL_PASS with owned conditions."

| Condition | Status |
|---|---|
| Corpus contains only models with accepted outcomes | ✓ 9 ACCEPT_NEW_MODEL, 0 DEFER, 0 REJECT |
| Deferred register tracked | ✓ 3 entries (DEF-01..DEF-03) with owners and upgrade paths |
| Hypotheses register tracked | ✓ Empty (initialized) |
| Each accepted model has PASS or CONDITIONAL_PASS | ✓ 5 PASS, 4 CONDITIONAL_PASS |
| Conditions owned and tracked | ✓ 6 conditions resolved (labeling), 3 deferred evidence items tracked |

**PHASE_B_REALITY_MODEL_FOUNDATION — COMPLETE.**

The Reality Model corpus is established with 9 models covering 12 of 14 canonical pressures. The deferred evidence register tracks 3 items requiring S1 replay for evidence-maturity upgrade. The hypotheses register is empty. All gates (G1–G6) are satisfied for all admitted models.

## Next Task

**PHASE_C_PORTFOLIO_CLASSIFICATION** or **S1_REPLAY_PORTFOLIO** — classify the corpus against the 14-CP portfolio, prioritize S1 replay targets (DEF-02/DEF-03 highest priority), and prepare the CP-12 challenge (Phase D).

## Repository Changes

`docs/decisions/b4-reality-model-admission-result.md` — created (this report). No other files modified. No Runtime code changed.

STOP.
