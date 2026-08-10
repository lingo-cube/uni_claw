# REALITY MODEL ADMISSION CONTRACT

> Status: **PROPOSED — awaiting Human Gate `HUMAN_ADOPT_REALITY_MODEL_ADMISSION_CONTRACT`**
> Version: 1.0 (draft)
> Generated: 2026-08-09
> Author role: Project Leader / Reality Governance Architect — mode CONTRACT_AUTHORING / HUMAN_GATE_PREPARATION
> Roadmap anchor: `PHASE_B_REALITY_MODEL_FOUNDATION` / B1 (`docs/system/post-s0-reality-grounded-usability-roadmap.md`)
> Inputs: Steps 1–6 decision chain + visual supplement + traversal supplement + unified pressure portfolio + runtime architecture contract
> Scope: Governance/specification only. This contract does NOT extract Reality Models, does NOT modify Runtime, does NOT create Candidates, does NOT start S1/S2/S3, and does NOT perform the CP-12 challenge.

---

## 0. Purpose

The purpose of this contract is to define the rules by which a claim about the external world may enter the Reality Model corpus. It exists to prevent, in order of severity:

1. **Legacy migration disguised as reality** — legacy system beliefs (FSM states, `AllVisited`, `IsEnd`, stack/frame bookkeeping) presented as world facts.
2. **AI interpretation as truth** — AI outputs (intent extraction, classification, advice) asserted as world facts.
3. **Observation output as authoritative truth** — a device query result treated as the world, without separating the raw record from its interpretation.
4. **Duplicate Reality Models** — the same world-fact cluster admitted multiple times under different names.
5. **Overfitted fixture models** — models whose world facts exist only to justify a test oracle (answers embedded in reality).
6. **Unsupported facts/transitions** — claims about the world with no evidence chain at any strength.
7. **Premature conclusions** — inferences asserted as fact before independent validation.

This contract is the single normative source for admission. All subsequent Phase B work (B2 extraction, B3 validation, B4 admission) operates under these frozen rules.

---

## 1. Core Definition of Reality

**Foundational principle (frozen, verbatim):**

> Reality is not: what the legacy system believed; what the new Runtime infers; what an AI says happened; what a test expected; what a Planner predicted.
> Reality Models are: the minimal implementation-independent world facts, observations, transitions, disturbances, and uncertainty that are justified by available evidence and are sufficient to reproduce a relevant real-world pressure.
> All inference must remain explicitly distinguishable from directly supported fact.

**Corollary (roadmap frozen principle):** Reality != legacy interpretation; Reality != Runtime belief; Reality != AI assertion. Reality Models are evidence-supported minimal models of world facts, observations and transitions.

**Operational consequences:**

- A Reality Model is a **cluster of claims about the external world** (screens, elements, transitions, device behavior) — never a description of the legacy system's internals, the Runtime's belief state, or a test's expectation.
- Every claim in a Reality Model belongs to exactly one of five layers (Section 3): WORLD FACT, OBSERVATION RECORD, REALITY INFERENCE, EXPECTED REQUIREMENT, or legacy claim (marked as such, kept outside the normative model).
- Repository truth wins: where this contract conflicts with any other document, the contract must be reconciled with the repository; where evidence and narrative conflict, evidence wins.

---

## 2. Authority Model

Admission decisions are made by the following actors. No actor may occupy two roles that create a conflict of interest (in particular: the Author of a model may never be its Validator, and the Deduplication Arbiter may not be the Author of the models it arbitrates).

| # | Actor | Role | Authority |
|---|---|---|---|
| 1 | **Reality Governance Architect (RG)** | Owns this contract; sole interpreter of admission rules; guardian of corpus integrity | Final ruling on any admission dispute; proposes contract revisions (revision requires Human gate) |
| 2 | **Evidence Steward (ES)** | Owns evidence records (`E-XX`, `TE-XX`, `VE-XX`), provenance chains, and strength classifications E4–E0 | Classifies evidence strength; certifies provenance chains; may not fabricate or repair evidence post-hoc |
| 3 | **Reality Model Author (RA)** | Proposes candidate Reality Models during B2 extraction | Drafts candidates from the filtered corpus only; may NOT validate own models |
| 4 | **Independent Validator (IV)** | Validates candidates during B3 independent validation | Issues `PASS` / `CONDITIONAL_PASS` / `FAIL`; must be distinct from the RA of the candidate it validates |
| 5 | **Dedup Arbiter (DA)** | Adjudicates deduplication, merge, variant, and novelty questions (Gate G5) | Rules `MERGE_EXISTING_MODEL` / `ADD_VARIANT` / `ADD_EVIDENCE` / `ACCEPT_NEW_MODEL`; distinct from the RA |
| 6 | **Architecture Neutrality Reviewer (ANR)** | Runs Gate G2 on every candidate | Verifies legacy-mechanism exclusion and implementation independence; can reject as `REJECT_LEGACY_MECHANISM` |
| 7 | **Pressure Portfolio Owner (PP)** | Owns the canonical pressure registry (`CP-01`..`CP-14`) | Approves the Primary/Secondary CP relation of each model; runs the new-CP registration path (which additionally requires Human gate) |
| 8 | **Project Leader / Human Authority (PL)** | The human decision authority | Freezes this contract (`HUMAN_ADOPT_REALITY_MODEL_ADMISSION_CONTRACT`); approves contract revisions; decides Open Questions; approves new CP registration and the CP-12 challenge initiation |

**Conflict-of-interest rule:** RA ⊥ IV (mandatory separation), RA ⊥ DA, ES may advise all roles but does not vote. The ANR may serve as IV for the same model only if the model passes G2 without modification.

---

## 3. Required Distinctions

Every claim in the corpus belongs to exactly one layer. Layer identity is part of the claim ID.

| Layer | ID prefix | Definition | Example |
|---|---|---|---|
| **WORLD FACT** | `WF-…` | A minimal, implementation-independent claim about the external world, justified by available evidence. Carries a support kind: `DIRECT` (directly evidenced by observation records) or `INFERRED` (derived through a cited inference chain). | `WF-…`: "The hub page displays two buttons labeled 'Go to List A' and 'Go to List B'." (DIRECT) |
| **OBSERVATION RECORD** | `OB-…` | A timestamped record of what was observed at a specific moment: device query output, frame content, ADB/UIA result, OCR raw string, coordinates, screenshot reference. No interpretation. | `OB-…`: "Frame at seq 12 contains text 'Go to List B' at (0.5, 0.4)." |
| **REALITY INFERENCE** | `RI-…` | A reasoned conclusion about the world that is not directly observed. Must carry confidence, enumerated alternatives, materiality, and the evidence it rests on. Never presented as fact. | `RI-…`: "The element at (0.5, 0.4) is a navigation button. Confidence: HIGH. Alternatives: it is a static row. Materiality: HIGH." |
| **REALITY MODEL** | `RM-…` | A minimal cluster of WF + OB + RI + ER, justified by evidence, sufficient to reproduce at least one canonical pressure (CP-XX). | `RM-…`: the multi-branch hub model. |
| **EXPECTED REQUIREMENT** | `ER-…` | A normative statement the system must satisfy so that the pressure does not recur. An ER is a target for the system, never a description of the world. | `ER-…`: "Completion must not be claimed while any in-scope navigation target remains undispatched." |

**Legacy claim marker (not a layer):** statements originating from legacy artifacts ("legacy reported `AllVisited`", "legacy claimed IsEnd=true") are recorded in the `Legacy Mechanism Context` field of a model, always marked as claims, and never serve as world facts or as ER justification on their own.

**Mixing is forbidden:** a single sentence may not assert a fact and an inference as if they were the same kind. "The screen shows a search box" (classification) must be written as `OB` (element + declared type) + `RI` (it is a search input) + supporting post-tap `OB` if confirmed.

---

## 4. Evidence Strength Model

Every evidence reference attached to a Reality Model carries one strength grade. The grade is assigned by the Evidence Steward, not by the model author.

| Grade | Name | Definition | Corpus examples |
|---|---|---|---|
| **E4** | LIVE_OR_RECORDED_EXTERNAL_WORLD | Direct recording of real external-world interaction: run artifacts, frames, ADB/UIA dumps, screenshots, live emulator/device observations. | Run `20260805T052309367Z` artifacts (`analysis.jsonl`, `plan.json`); real-run fixtures |
| **E3** | RECORDED_REALITY_DERIVED_EXECUTABLE_REPRODUCTION | An executable reproduction reconstructed from recorded reality (replay fixtures, reconstructed scenarios) that exercises the recorded world states. | `20260805T052309367Z_EnumerateFixtures.cs`; S1 replay fixtures |
| **E2** | EXECUTABLE_INTEGRATION_OR_PRODUCTION_SHAPED_REGRESSION | Executable integration tests on emulator with real transport (ADB) or production-shaped perception regressions. | `EmulatorScenarioIntegrationTests`; settings-enumerate regressions |
| **E1** | DETERMINISTIC_SIMULATION | Deterministic synthetic simulation evidence (scripted environments, stub perception). | `SimulationBaselineTests`; `MultiBranchNavigationTests` (E-07); current Runtime scenario tests |
| **E0** | DOCUMENT_ONLY_HUMAN_REPORT | Design documents, human reports, or narrative with no executable artifact. | design docs, `runner-through-engine-design.md` |

**Rules:**
- A model's overall strength is the minimum grade over its core world facts (the weakest link governs the model's maturity claim).
- E0 alone is sufficient to **admit** a model (provenance exists) but the model is then classified for evidence-maturity upgrade in Phase C; E0 cannot be the sole support for a transition fact claimed as DIRECT.
- Evidence may be **upgraded** only by attaching higher-grade evidence (e.g., S1 replay), never by re-classification.
- A single evidence artifact may be graded once; its grade is global.
- The 5 intent transformation boundaries and the documented contradictions (E-07 unfixed false-`AllVisited`; E-13 GAP-P0-02 EntryPolicy fake success + ADB scroll failure → IsEnd) keep their documented status and are attached to models as evidence with their known caveats, never silently repaired.

---

## 5. Fact Support Rule

Every World Fact `WF-…` must carry:

1. **Support kind**: `DIRECT` or `INFERRED`.
   - `DIRECT`: the fact is directly evidenced by one or more observation records `OB-…` within the same temporal scope. No inference chain required.
   - `INFERRED`: the fact is derived from observation records through a sound inference chain. It MUST cite at least one `RI-…` that carries the confidence and alternatives. The fact's confidence equals the confidence of its weakest cited RI.
2. **Evidence references**: at least one `E-XX` / `TE-XX` / `VE-XX` with strength grade (E4–E0), certified by the Evidence Steward.
3. **Temporal scope**: the time (sequence, timestamp, or relative t0..tn) at which the fact holds. A fact asserted for time t must be evidenced at time t; backfilling is prohibited.
4. **Implementation independence**: the fact must be expressible without any legacy mechanism vocabulary (Section 7) and without any Runtime type or component name.

**Prohibited as World Facts** (see Section 12 — No Answers Embedded in Reality): verdicts, completions, authorizations, and semantic identity claims (`SameContainer=true`, `BranchComplete=false`, `GoalSatisfied=true`, `IsEnd=true`, `AllVisited`, `TargetFound`, "navigation succeeded", "recovery verified", "authorized"). These may only ever appear as (a) EXPECTED REQUIREMENT targets, (b) legacy claims, or (c) system-side decision records.

**A fact with no evidence chain is not a fact.** It is either a hypothesis (→ hypotheses register, outside the corpus) or an unsupported claim (→ `REJECT_UNSUPPORTED`).

---

## 6. Reality Inference Rule

Every Reality Inference `RI-…` must carry:

| Field | Requirement |
|---|---|
| `RI-ID` | Stable identifier (`RI-<NN>`). |
| Statement | World-level, implementation-independent claim that is not directly observed. |
| Confidence | `HIGH` / `MEDIUM` / `LOW`. HIGH = directly corroborated by ≥2 independent observation records or by a confirmed world outcome; MEDIUM = supported by ≥1 record with plausible alternatives; LOW = plausible but weakly evidenced. |
| Alternatives | Explicitly enumerated alternative world states consistent with the same evidence. At least one alternative must be stated unless the inference is DIRECT-corroborated. |
| Materiality | `HIGH` / `LOW` — whether an error in this inference would change the pressure's outcome, the model's predictions, or the ER's meaning. |
| Evidence refs | The `OB`/`E-` records it rests on. |
| Method | The kind of inference: `deduction from observations`, `perception classification`, `recognition across observations`, `state reconstruction`, `AI output`, `statistical`, `analogy`. AI-derived inferences are always `AI output` and never upgrade in confidence without independent corroboration (see I-14: AI output is Semantic Evidence, not world truth). |

**Rules:**
- An RI is never embedded into a WF without citation; the WF then inherits the RI's confidence.
- An RI whose materiality is LOW must either be dropped or explicitly recorded as immaterial-but-preserved.
- An RI with confidence LOW may enter a model only if materiality is HIGH and the model flags it; the model's validation status must then be `CONDITIONAL_PASS` or `FAIL` until corroborated.
- Inference chains must be acyclic and terminate in observation records.

---

## 7. Legacy Contamination Gate (G2)

**Rule:** A candidate Reality Model's normative content (WF, OB-as-interpretation, RI, ER) must be fully expressible without legacy implementation vocabulary. Legacy terms may appear ONLY in the `Legacy Mechanism Context` field, explicitly marked as legacy.

**Excluded vocabulary (non-exhaustive, extensible by ANR):** `FSM`, `Frame`, `Stack`, `DFS`, `Graph`, `TraversalNode`, `DynamicMatch`, `StateRestorer`, `AllVisited`, `IsEnd`, `CompletionReason`, legacy cache types, and legacy owner names (`TraversalFSM`, `TraversalEngine`, `PlanCompiler`, `IntentExtractor`, `ScenarioPlanLoader`, `TraversalAdvisor`, `AdbScreenStateProvider`, …).

**Test:** Rewrite the candidate's world claims replacing every legacy mechanism reference with observable behavior. If the claim loses its meaning, the claim was mechanism-dependent → rewrite or reject.

**Example:** "The DFS revisit loop re-entered the Internet page" is contaminated. The admissible translation is: `OB`: frame shows Internet page re-entered at t2; `RI`: the system did not recognize the page as previously visited (confidence HIGH, alternatives: distinct-but-similar page); the loop/DFS terminology moves to Legacy Mechanism Context.

**Outcome:** candidates that cannot be de-contaminated are rejected `REJECT_LEGACY_MECHANISM`. Legacy evidence still attaches as evidence; only the normative content must be mechanism-free.

---

## 8. Minimality Gate (G4)

**Rule:** Every WF, OB, RI, and ER in a candidate model must be required for at least one of: (a) reproducing the canonical pressure, (b) stating the expected requirement, or (c) justifying another element's inference chain.

**Procedure (per element):**
1. Remove the element.
2. Does the model still reproduce the pressure with the same oracle? Does the ER remain stateable and actionable? Does any remaining element lose its evidence chain?
3. If the model is unchanged in all three: the element is redundant → remove.

**Derived duplicates** (facts derivable from other facts already in the model) are always removed. The deduplication gate (G5) operates across models; minimality operates within one model.

---

## 9. Deduplication Gate (G5) and Novelty Test

**Across-corpus dedup:** before any candidate is admitted, the Dedup Arbiter runs it against the accepted corpus:

| Comparison result | Outcome |
|---|---|
| Same world-fact cluster + same pressure relation | `MERGE_EXISTING_MODEL` (merge candidate evidence into the existing model) |
| Same world-fact cluster + different parameterization (device/app version, perception conditions, evidence maturity) | `ADD_VARIANT` (new variant of the existing model) |
| Same model + additional/stronger evidence only | `ADD_EVIDENCE` (evidence attachment to the existing model) |
| Genuinely new world-fact cluster or pressure relation not covered | `ACCEPT_NEW_MODEL` (passes the novelty test below) |

**Novelty test (required for `ACCEPT_NEW_MODEL`):** the candidate is novel if and only if ALL hold:
1. No accepted model or variant expresses the same world-fact cluster (under G5 comparison).
2. The candidate's Primary CP relation is not already served by an accepted model with the same oracle.
3. Removing the candidate would leave its Primary Canonical Pressure without any model, OR the candidate's facts materially change the pressure's oracle or fail oracle.

**Registry rule:** the canonical pressure registry is `CP-01`..`CP-14` (7 domains). The contract does not register new pressures; new-CP registration runs through the Pressure Portfolio Owner and requires the Human gate.

---

## 10. Variant Rule

A **VARIANT** shares the parent model's world-fact cluster and pressure relation, and differs only in parameterization:

- Device / OS / app version;
- Perception conditions (OCR variants, classification-error profiles, viewport size);
- Evidence maturity level of the attached evidence (same facts, E1 vs E3 vs E4);
- Parameterization of the same world structure (depth bound value, list length, branch count).

**Constraints:**
- A variant MUST NOT introduce world facts absent from the parent. If it does, it is a new MODEL, not a variant.
- A variant inherits the parent's ERs; it may specialize them but may not weaken them.
- Variants are admitted under the parent's RM-ID with a suffix (`RM-<NN>-V<k>`).

---

## 11. Pressure Relation

- Every Reality Model MUST declare exactly one **Primary Canonical Pressure** `CP-XX` from the registry (`CP-01`..`CP-14`) and MAY declare secondary CPs.
- The relation must be justified: the model's world facts, when combined with the ER, reproduce the pressure's core scenario and satisfy its oracle distinction (its primary Reality Distinction — `RD-XX` / `VRD-XX` / `TRD-XX`).
- A pressure with no admitted model is recorded as a coverage gap in the corpus index (Phase C will classify it).
- CP-06 is FULLY_CLOSED (Phase A, 2026-08-09). Plan-length-independent initial GoalEvidence authority is proven — both empty and non-empty branches (Assertion6–Assertion9). Plan existence does not create an obligation to act. Closed status affects Phase C classification only; CP-06 remains a valid Primary CP for admission, and its reality model is admitted primarily as ER + evidence.
- CP-12 (target grounding) carries `CHALLENGE_REQUIRED`. Admission of visual models whose primary pressure is CP-12 is governed by Section 18 (permission note) and does not by itself initiate the challenge.

---

## 12. No Answers Embedded in Reality

**Rule:** The world must not be invented, selected, or adjusted to make a system verdict or test oracle come out right.

1. **Verdicts are never world facts** (see Section 5 prohibition list). `SameContainer=true`, `BranchComplete=false`, `GoalSatisfied=true`, `IsEnd=true`, `AllVisited`, `TargetFound`, "navigation succeeded", "recovery verified", authorization decisions — none may appear as WF or as OB-interpretation. They may appear as: ER targets, legacy claims, or system-side decision records.
2. **Fixture-answer facts are prohibited**: a WF whose sole purpose is to justify a test oracle, and which no independent observation record supports, is a fixture answer → rejected (`REJECT_UNSUPPORTED`).
3. **The reverse direction is also prohibited**: a WF may not be silently dropped because it would make the system's completion easier to prove. Honest unresolved states (`null`, unknown) are admissible reality.
4. The completion/verdict-producing claims live exclusively in the EXPECTED REQUIREMENT layer, where they belong as normative targets.

---

## 13. Action/World Separation

- **System-side action records** (action dispatched, timed out, rejected, tap coordinates, scroll command, recovery action, plan step execution) are OBSERVATION RECORDS of the system (`OB-SYS-…`), not world facts.
- **World effect** must be established by world observation: `WF` may state "tapping X produced navigation to page Y" only when DIRECTLY evidenced by pre-action and post-action observation records (see CP-02).
- `TimedOut`/`Dispatched` never implies a world outcome (see CP-03 / SC-P3-001: re-observe before verdict).
- In a Reality Model, the system's actions appear as a recorded sequence for the pressure's reproduction, and the ER expresses what the system must verify — the world facts never "know" the system's verdict.

---

## 14. Perception Separation

- **Raw perception output** (raw OCR string, coordinates, declared element type from a classifier, UIA tree, frame) is an OBSERVATION RECORD.
- **Interpreted claims** ("the element IS a menu_item", "the element's label IS X", "this is the same page as before") are REALITY INFERENCES (method: `perception classification` / `recognition across observations`), unless directly confirmed by a world outcome record.
- **Declared type ≠ actual type.** When a declared type and a confirmed world outcome conflict (e.g., element declared `menu_item` whose tap opens a search UI), the world outcome wins for the WF; the declared type remains an OB of the classifier, and the conflict is recorded as an RI with the conflicting alternatives (see CP-11, TSP-03, VE-05/06/07).
- OCR text variants are RI alternatives under the same OB string (see VSP-02 → CP-13 variant).

---

## 15. Temporal / Failure Evolution

- Every claim carries a temporal scope (Section 5.3). Facts are asserted for the time at which they were evidenced.
- **Failure evolution records** capture, for a failure at time t: the world state before t (OB), the system action/decision at t (OB-SYS or legacy claim), the disturbance/event (WF if observable, else RI), and the observed aftermath (OB). The "legacy claimed X" is always marked as a legacy claim; the "system actually observed Y" is the OB.
- **No backfilling**: a fact not observed at t may not be asserted for t on the strength of later evidence, unless the later evidence is a reconstruction explicitly graded E3 and labeled as such.
- **Disturbances** (toasts, popups, drift, phantom content) enter models as world facts only when directly observed; otherwise as RI with alternatives.

---

## 16. Counterfactual Check

Every candidate model must pass three counterfactual tests before admission:

1. **Per WF (minimality counterfactual):** if this fact were false, would the model still reproduce the pressure and state the ER? Yes → remove the fact (redundant).
2. **Per RI (materiality counterfactual):** if the alternative world state were true instead, would the model's reproduction or the ER's meaning change? No → the RI is immaterial (drop or mark immaterial).
3. **Per model (falsifiability counterfactual):** the model MUST state what observation would refute it. If no refuting observation can be stated, the model is unfalsifiable → not admissible as reality (`DEFER` or `REJECT_UNSUPPORTED` per Section 18).

---

## 17. Independent Validation (B3)

Validation is performed by the Independent Validator (IV), who MUST be distinct from the model's Author.

**Checklist (each item → `PASS` / `FAIL` / `CONDITIONAL`):**
1. Provenance re-verification: every WF/OB/RI/ER traceable to its cited evidence artifact (file/commit/run ID) — re-checked against repository truth, not trusted.
2. Fact/inference separation: no unlabeled inference, no RI-as-fact, no verdict as WF.
3. Contamination re-check: G2 vocabulary absent from normative content.
4. Minimality re-check: G4 procedure re-run.
5. Deduplication re-check: G5 comparison re-run against the accepted corpus.
6. Counterfactual re-check: Section 16 tests re-run.
7. ER adequacy: the ER, if enforced, actually prevents the pressure's fail oracle.

**Verdicts:**
- `PASS` — all items PASS. Model enters the corpus.
- `CONDITIONAL_PASS` — all items PASS except listed conditions (e.g., LOW-confidence RI pending corroboration). Model enters the corpus with an open-conditions register; each condition has an owner and a resolution deadline; a condition that expires unresolved downgrades the model.
- `FAIL` — any item FAILS with reasons. Model returns to the Author with the failure record; re-submission is allowed but each failure is logged and the model's provenance chain is not repaired retroactively.

**Authority note:** extraction, deduplication, and candidate validation under frozen rules require NO approval (roadmap Human Gate note). Only contract changes, new CP registration, and the CP-12 challenge require the Human gate.

---

## 18. Admission Outcomes

When a candidate completes all gates, the Dedup Arbiter + Independent Validator jointly issue exactly one outcome:

| Outcome | Meaning |
|---|---|
| `ACCEPT_NEW_MODEL` | Passed G1–G6; novel per Section 9; enters corpus as `RM-<NN>`. |
| `MERGE_EXISTING_MODEL` | Same world-fact cluster; candidate's evidence/ER content merged into the existing model (merges are recorded in the model's history). |
| `ADD_VARIANT` | Same cluster, different parameterization; enters as `RM-<NN>-V<k>`. |
| `ADD_EVIDENCE` | Additional or stronger evidence for an existing model; enters as evidence attachment `RM-<NN>-E<k>`; may upgrade the model's evidence maturity. |
| `DEFER` | Evidence exists but is insufficient today (strength too low for the claim, replay pending, conditions pending). Model is not in the corpus; recorded in the deferred register with its evidence. |
| `REJECT_LEGACY_MECHANISM` | Normative content cannot be expressed without legacy mechanism vocabulary; rejected per Section 7. |
| `REJECT_UNSUPPORTED` | **Decision: INCLUDED as an outcome.** Core claims have no supporting evidence at any strength (no E0 or better), or the cited evidence contradicts the claims, or the claims are fixture answers (Section 12.2). An unsupported claim is a hypothesis, not reality: it is recorded in the clearly-labeled hypotheses register (outside the corpus) and tracked until evidence appears or the hypothesis is dropped. |

**DEFER vs REJECT_UNSUPPORTED:** DEFER = evidence exists but is insufficient now; REJECT_UNSUPPORTED = no evidence at all, or evidence contradicts the claim, or the claim is a fixture answer.

**Phase B4 exit condition:** corpus contains only models with accepted outcomes; deferred register and hypotheses register tracked; each accepted model has validation status `PASS` or `CONDITIONAL_PASS` with owned conditions.

---

## 19. Human Gates

| Gate | Trigger | Decision |
|---|---|---|
| `HUMAN_ADOPT_REALITY_MODEL_ADMISSION_CONTRACT` | B1 completion — THIS artifact | Adoption freezes the admission rules; all downstream B2–B4 work runs without approval |
| Contract revision | Any change to admission rules, evidence strength semantics, or gate definitions | Human gate via Reality Governance Architect proposal |
| New CP registration | A pressure not expressible as CP-01..CP-14 | Pressure Portfolio Owner proposal + Human gate |
| CP-12 challenge initiation | Phase D (`CHALLENGE_REQUIRED` status) | Existing Semantic Gate path (Phase D) — not opened by this contract |
| Individual model admissions | Any candidate under frozen rules | NO approval required (delegated to this contract) |

**Boundary:** `STOP_AT_REALITY_MODEL_ADMISSION_CONTRACT` — no Reality Model admission, S1/S2/S3 work, new semantics, new Candidates, or U1 execution is authorized until the Human gate above.

---

## 20. Canonical Reality Model Schema

The field list is FIXED. Authors may not add fields; a genuinely necessary field is a contract revision (Section 19).

| # | Field | Requirement |
|---|---|---|
| 1 | `RM-ID` | `RM-<NN>` (variants `-V<k>`, evidence attachments `-E<k>`), assigned by RG at admission |
| 2 | `Title` | One line, behavior-naming the world structure |
| 3 | `Type` | `MODEL` / `VARIANT` / `EVIDENCE_ATTACHMENT`; for non-MODEL: `Parent RM-ID` |
| 4 | `Pressure Relation` | Exactly one Primary `CP-XX` + optional secondary CPs; one line of justification |
| 5 | `World Facts` | `WF-…` list, each with support kind `DIRECT`/`INFERRED` and its RI citations |
| 6 | `Observation Records` | `OB-…` list (world) + `OB-SYS-…` (system action records) |
| 7 | `Reality Inferences` | `RI-…` list with confidence, alternatives, materiality |
| 8 | `Expected Requirements` | `ER-…` list (normative targets) |
| 9 | `Temporal Scope` | Time window or t0..tn frame over which facts hold |
| 10 | `Legacy Mechanism Context` | Legacy terms/claims, explicitly marked, non-normative |
| 11 | `Evidence References` | `E-XX`/`TE-XX`/`VE-XX` IDs, each with strength grade E4–E0 (ES-certified) |
| 12 | `Provenance Chain` | Source files / commits / run IDs for every claim |
| 13 | `Counterfactual / Falsification Statement` | What observation would refute the model (Section 16.3) |
| 14 | `Validation Status` | `PASS` / `CONDITIONAL_PASS` (with owned conditions) / `FAIL` |
| 15 | `Admission Outcome` | One of Section 18 outcomes + date + gate evidence |
| 16 | `Confidence Summary` | Per-RI confidence rollup; model-level confidence = weakest core RI |

---

## 21. Reality Model Identity: MODEL vs VARIANT vs EVIDENCE_ATTACHMENT

| Identity | World-fact cluster | Parameterization | Evidence | Admitted as |
|---|---|---|---|---|
| `MODEL` | New (passes novelty test) | — | Any E4–E0 | `RM-<NN>` |
| `VARIANT` | Same as parent | Different (device/version/perception/maturity) | Any | `RM-<NN>-V<k>` |
| `EVIDENCE_ATTACHMENT` | Same as parent | Same | Additional/stronger only | `RM-<NN>-E<k>` |

- Identity is decided by the Dedup Arbiter under G5; disputes go to the Reality Governance Architect.
- A variant or attachment never changes the parent's world-fact cluster. If it does, it is a new model.
- A merged model (Section 18) keeps the existing RM-ID and records the merge in its history.

---

## 22. Contract Self-Test (six known cases)

The contract is applied conceptually to the six hardest known cases from the corpus. This is a consistency check only — no Reality Models are extracted by this contract.

### Case 1 — Multi-branch hub: branch A done, branch B unresolved (E-07, CP-04)

- `OB`: hub frame shows buttons "Go to List A" and "Go to List B".
- `WF` (DIRECT): hub page displayed two navigation targets at t0; list A traversal observed to completion (16 items) at t1..t16; hub re-observed at t17 with "Go to List B" still present.
- Legacy claim (non-normative): "legacy reported `AllVisited`" — NOT a WF.
- `RI`: list B content is unexplored (confidence HIGH; alternative: list B was already exhausted before observation, contradicted by the hub re-observation).
- `ER`: completion must not be claimed while any in-scope navigation target remains undispatched; re-observation of the hub must re-open undispatched targets.
- Contract treatment: `AllVisited` rejected as WF (Section 5/12); the model is a completion-honesty model under CP-04; E-07 attaches as E1 evidence. **CONSISTENT.**

### Case 2 — ADB/observation failure treated as end-of-content (E-13-B, CP-08)

- `OB`: scroll query at t failed/timed out (device-side failure record).
- Legacy claim: "legacy set IsEnd=true" — rejected as WF.
- `RI`: scroll state unknown at t (confidence HIGH that state is unknown; alternatives: end reached / more content exists — both live).
- `WF`: none about exhaustion — exhaustion is NOT evidenced.
- `ER`: an observation failure must produce an unresolved state, never positive exhaustion; the unresolved state must be diagnosable.
- Contract treatment: `IsEnd=true` is a verdict (Section 12) and additionally contradicted by evidence; the failure is recorded as disturbance/RI, never as WF. **CONSISTENT.**

### Case 3 — "Flash notifications" misclick, type-blind match (VE-07, CP-11)

- `OB`: element with text "Flash notifications" at coordinates c; declared type (classifier output).
- `RI`: the element is a camera "Flash" toggle (confidence MEDIUM; alternatives: it is a settings row "Flash notifications" that is not the camera toggle — this alternative is corroborated by the misclick outcome).
- `OB-SYS`: tap dispatched at c; `OB` (post-tap): world shows the wrong page — navigation occurred to an unintended destination.
- `ER`: dispatch must not be driven by text-substring matching alone; the outcome of the tap must be verified against the intended target.
- Contract treatment: classification and intent-of-tap are RI; the world outcome OB wins over declared type (Section 14). **CONSISTENT.**

### Case 4 — Search box classified as menu_item and acted upon (VE-06 / E-10-C, CP-11)

- `OB`: element at coords with declared type `menu_item`; raw text "Search" present.
- `RI`: the element is a menu_item (confidence LOW after contradictory outcome; alternatives: it is a search input — corroborated by the outcome).
- `OB` (post-tap): search UI entered; the world shows an input page, not a menu destination.
- `WF` (DIRECT): tapping the element opened a search UI (confirmed by post-tap observation).
- `ER`: per-instance verification before dispatch — category authorization (menu_item allowed) is not per-instance existence (this element is not a menu_item); declared-type conflicts with confirmed outcome must resolve in favor of outcome.
- Contract treatment: declared type stays an OB; actual type is RI resolved by world outcome; the misclassification profile is a legitimate variant parameter (CP-11 variant per VSP-03). **CONSISTENT.**

### Case 5 — Action timeout where the world may have changed (SP-02/SC-P3-001, CP-02/CP-03)

- `OB-SYS`: tap dispatched; transport `TimedOut`.
- `WF`: none — the world state at timeout is NOT evidenced; asserting "world unchanged" would be an unlabeled inference.
- `RI`: world may or may not have changed (confidence HIGH that it is unknown; alternatives: page changed / page unchanged — both live).
- `OB` (post-timeout): fresh observation.
- `ER`: a timed-out dispatch must not be treated as either success or failure; a fresh observation must precede any verdict (re-observe before verdict).
- Contract treatment: action record is OB-SYS; world outcome requires world observation; the ER prevents the dispatch-as-proof conflation. **CONSISTENT.**

### Case 6 — Legacy DFS/Frame/Stack-only description (E-10-A, CP-13)

- Legacy description: "DFS revisit loop; Internet page re-entered via stack; navigation tasks regenerated" — mechanism-bound.
- Translation under the contract: `OB`: frame evidence shows the Internet page re-entered at t2 with navigation tasks regenerated; `RI`: the system failed to recognize the page as previously visited (confidence HIGH; alternatives: a distinct-but-similar page — contradicted by element evidence); `WF` (DIRECT): the same logical page was re-observed. Legacy terms (`DFS`, `stack`, `frame`) move to Legacy Mechanism Context.
- `ER`: re-observed pages must be recognized as previously visited; revisit must not reset exploration state (CP-05 relation as secondary).
- Contract treatment: the candidate is admissible only in translated form (Section 7); the untranslated form is `REJECT_LEGACY_MECHANISM`. **CONSISTENT.**

**Self-test conclusion:** all six known cases resolve consistently under the contract with no contradictions and no need to stretch any rule.

---

## 23. Contract Quality Gate

The contract claims the eight quality dimensions. Each maps to a mechanical verification already in the contract:

| Dimension | Verification in this contract |
|---|---|
| RELIABLE | Every WF/RI/ER must trace to ES-certified evidence (Sections 5, 17.1) |
| CORRECT | Fact/inference separation (Sections 3, 5, 6); no contradiction with the frozen corpus (E-07, E-13 caveats preserved, Section 4) |
| ARCHITECTURE-NEUTRAL | G2 (Section 7) + implementation-independence requirement (Section 5.4) |
| MINIMAL | G4 (Section 8) + counterfactual test per WF (Section 16.1) |
| NON-REDUNDANT | G5 (Section 9) + novelty test (Section 9) |
| REVISABLE | Counterfactual/falsification statement in every model (Section 16.3) + evidence upgrade path (Section 4) + DEFER/hypotheses register (Section 18) |
| INDEPENDENTLY_VALIDATED | B3 IV separation (Sections 2, 17) |
| PRESSURE-USEFUL | Mandatory Primary CP relation with oracle justification (Section 11) + ER adequacy check (Section 17.7) |

**Self-assessment:** the contract satisfies all eight dimensions by construction; each dimension has a named check that a candidate must pass.

---

## 24. Open Questions (requiring Human decision)

1. **Evidence grade of code-derived failure evidence.** E-13-B's ADB scroll failure is evidenced from the production code line (`AdbScreenStateProvider.cs:38`) plus the failing-path test, with no preserved live run artifact. Should this be graded E3 (reconstructed executable reproduction — recommended, since the failure path is executable) or E2? The grade affects whether the CP-08 model can claim recorded-reality maturity before S1 replay.
2. **Failure models as corpus content.** Should Reality Models whose world facts describe a *failed* system interaction (misclicks, misclassifications, revisit loops) be admitted as full models (recommended — the failure is real world behavior and the ER expresses the prevention), or restricted to ER + evidence attachments? The contract currently admits them as full models.
3. **CP-12 visual models before the challenge.** The contract permits admission of visual models under Primary CP-12 while the challenge itself remains `CHALLENGE_REQUIRED` (Phase D). Confirm this ordering is acceptable: corpus admission first, challenge later under the Semantic Gate path (recommended), rather than blocking visual-model admission until Phase D resolves.

---

## 25. Recommendation

**`HUMAN_ADOPT_REALITY_MODEL_ADMISSION_CONTRACT`**

**Rationale:**
- The evidence corpus is fully mined and filtered (Steps 1–6 + visual + traversal supplements; E-01..E-18, 48 atomic cases, RD/VRD/TRD distinctions, 14 canonical pressures) — the contract governs admission of this corpus, not further mining.
- The contract is consistent with the roadmap B1 spec (World Fact / Reality Inference / Reality Model, provenance, evidence strength, minimization, deduplication, independent validation, admission authority) and with the frozen Phase B principle.
- The contract preserves the foundational reality principle verbatim and encodes it as operational rules (Sections 1, 3, 5, 6, 12).
- It resolves the pending design question (REJECT_UNSUPPORTED: INCLUDED) and passes the six-case self-test and the eight-dimension quality gate.
- It keeps the authority boundaries intact: no Runtime changes, no CP-12 challenge, no S1/S2/S3, no Candidates.

**Next Task After Adoption:** `LEGACY_REALITY_MODEL_EXTRACTION` (B2) — extract from the already-filtered evidence corpus ONLY, under the frozen rules of this contract.

---

## 26. Repository Changes

- `docs/system/reality-model-admission-contract.md` — this contract (NEW).
- `docs/decisions/reality-model-admission-contract-gate.md` — decision/gate artifact (NEW).

No other files modified.
