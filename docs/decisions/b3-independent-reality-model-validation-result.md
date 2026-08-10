# B3_INDEPENDENT_REALITY_MODEL_VALIDATION_RESULT

> Generated: 2026-08-09
> Role: Independent Validator (IV) — distinct from Reality Model Author
> Authority: `HUMAN_ADOPT_REALITY_MODEL_ADMISSION_CONTRACT` (2026-08-09)
> Input: `docs/decisions/b2-reality-model-extraction-result.md` (9 RM candidates)
> Contract: `docs/system/reality-model-admission-contract.md` §17 (frozen v1.0)

---

## Validation Summary

| RM | Title | 1. Prov | 2. Sep | 3. Contam | 4. Min | 5. Dedup | 6. CF | 7. ER | Verdict |
|---|---|---|---|---|---|---|---|---|---|
| RM-01 | Page Inventory | PASS | PASS | PASS | COND | PASS | PASS | PASS | **CONDITIONAL_PASS** |
| RM-02 | Multi-Branch Hub | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| RM-03 | Goal Satisfaction | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| RM-04 | Entry Verification | COND | PASS | PASS | PASS | PASS | PASS | COND | **CONDITIONAL_PASS** |
| RM-05 | Navigation Page Change | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| RM-06 | Depth Bound | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| RM-07 | Observation Failure ≠ Exhaustion | COND | PASS | PASS | PASS | PASS | PASS | COND | **CONDITIONAL_PASS** |
| RM-08 | Recovery ≠ Error Reset | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| RM-09 | Visibility ≠ Navigability | PASS | PASS | PASS | COND | PASS | PASS | PASS | **CONDITIONAL_PASS** |

**PASS: 5 | CONDITIONAL_PASS: 4 | FAIL: 0**

All 9 candidates pass independent validation. Four carry conditions — all resolvable without new evidence mining. No candidate fails.

---

## Validation Methodology

Each RM validated against the 7-item checklist (§17.1–17.7). Evidence artifacts verified against repository truth where accessible (feature/refactor via `git show`, uni-agent working tree). Contract gates G1–G6 applied per §7–§12, §16.

**Independent Validator note:** This validation re-checks provenance, separation, contamination, minimality, deduplication, counterfactuals, and ER adequacy. It does NOT re-extract facts from evidence (that was B2's role). It verifies that B2's extraction is consistent with the contract and with itself.

---

## RM-01 — Android Device Screen as Page Inventory

**Primary CP:** CP-13 | **WFs:** 5 | **RIs:** 3 | **ERs:** 4

### 1. Provenance Re-verification — PASS

| Element | Cited Evidence | Verified |
|---|---|---|
| WF-01 | EP-04 sim-replay export, EP-03 trace.jsonl, E-03 | EP-04 verified present on feature/refactor (4 pages, 5+16+21+14 elements). EP-03 trace.jsonl verified (record_type `page_analysis` at step 1). |
| WF-02 | EP-04 element `type`/`text` fields, EP-03, AF-22 | Verified: every EP-04 element has `type` and `text` fields. |
| WF-03 | VE-05, VE-06, VE-07 | VE-05 verified via FixVerificationTests.cs L8 (committed). VE-06 verified via 20260805T052309367Z_TraceReplayTests.cs. VE-07 verified via TextTargetResolutionTests.cs. |
| WF-04 | E-01, E-10 | E-01 locate scenario `pending_verification` verified via normalized evidence step3. E-10 DFS revisit verified via TraceReplay fixture. |
| WF-05 | EP-03 successEvidence, VE-08 | EP-03 `target_page_identity:About emulated device` verified in committed result.json. |
| OB-01..OB-04 | EP-03 trace.jsonl, result.json; EP-04 export | All four OBs verified against committed feature/refactor artifacts. |
| RI-01..RI-03 | Supporting WF/OB chain | Chain integrity verified — each RI cites supporting WF and OB. |
| ER-01..ER-04 | CP-13, E-01, VE-05/06/07, VE-08 | Source documents verified present. |

**No provenance gap found.** All cited evidence artifacts exist at their stated paths.

### 2. Fact/Inference Separation — PASS

- All 5 WFs carry explicit support kind: WF-01/02/03 DIRECT, WF-04/05 INFERRED. ✓
- All 3 RIs carry confidence (HIGH/MEDIUM), explicit alternatives considered, and materiality assessment. ✓
- No verdict embedded as WF. WF-03 ("type labels are sometimes wrong") is an observation about the world, not a verdict about the system. ✓
- OB records are system observation records, clearly separated from WF. ✓

### 3. Contamination Re-check — PASS

- Normative content (WF, RI, ER) uses world-behavior vocabulary: "device screen," "element inventory," "type label," "display text," "page identity," "observable."
- Legacy terms (`PageAnalysis`, `SemanticPageName`, `IsStillMine`, `ViewportIdentity`, `Container.CurrentObservation`, `analysis.jsonl`, `YOLO`, `OCR`, `fusion.py`, `label-mapping.json`, `Deki-Yolo`, `chevron heuristic`) are confined to Legacy Mechanism Context. ✓
- G2 rewrite test: "A device screen presents as a finite list of elements" passes — no mechanism vocabulary. ✓

### 4. Minimality Re-check — CONDITIONAL

| Element | Required? | Reason |
|---|---|---|
| WF-01 | YES | Foundational — without it, "page" and "element" have no referent |
| WF-02 | YES | Establishes the type+text structure — required for RI-02, RI-03 |
| WF-03 | YES | The core world fact that makes CP-11/CP-13 relevant — type labels are unreliable |
| WF-04 | YES | Establishes the page-identity problem — required for RI-01 |
| WF-05 | YES | Distinguishes identity inference from identity declaration — required for ER-01 |
| RI-01 | YES | The core inference linking page identity to element inventory |
| RI-02 | YES | Establishes coordinate drift as a world property — supports VRD-03 |
| RI-03 | YES | The core inference linking type labels to navigability — supports CP-11 |
| ER-01 | YES | Normative requirement for page identity verification |
| ER-02 | YES | Normative requirement against type-label-only navigability |
| ER-03 | YES | Normative requirement for semantic text matching |
| ER-04 | **CONDITIONAL** | Source attribution is desirable but not required to prevent CP-13's fail oracle (conflating raw evidence with page identity). Removing ER-04: RM-01 still prevents the fail oracle via ER-01 and ER-02. ER-04 strengthens the model but is not minimal. |

**Condition:** ER-04 is borderline-non-minimal — it specifies source attribution as a requirement when ER-01 (verify page identity from observable evidence) already covers the need. Recommend: keep ER-04 in the model but mark it as a derived requirement (derived from ER-01 + VRD-04), not an independent ER. Does not block admission.

### 5. Deduplication Re-check — PASS

- **RM-01 vs RM-05:** RM-01 is about page *identity* (what a page IS); RM-05 is about page *change* (navigating BETWEEN pages). Distinct world-fact clusters. Different CPs (CP-13 vs CP-02). ✓
- **RM-01 vs RM-09:** RM-01 covers element inventory structure; RM-09 covers element classification reliability. RM-01's WF-03 (type labels are sometimes wrong) is the world fact that RM-09's inferences build upon — complementary, not overlapping. ✓
- No other RM shares the page-identity-as-inventory world-fact cluster. ✓

### 6. Counterfactual Re-check — PASS

- **Per-WF minimality counterfactual:** Each WF tested. WF-05 counterfactual: if page identity were directly declared by perception (not inferred), RI-01 would collapse and the CP-13 distinction would be moot — but this is false in the observed world (VE-08: UIA deleted, AI-only pipeline). WF-05 is non-redundant. ✓
- **Per-RI materiality counterfactual:** RI-01: if page identity = element inventory identity, ER-01 would be unnecessary and CP-13's fail oracle could not occur. But the observed world has WF-04 (two screens share text but differ in inventory) — the alternative is falsified. Material. ✓
- **Falsifiability counterfactual (§16.3):** Stated in field 13 — "If two distinct Android screens produce identical element inventories... semantic page identity cannot be established from element inventory alone." This is a clear, observable falsification condition. ✓

### 7. ER Adequacy — PASS

CP-13 fail oracle: system conflates raw page evidence (element list from vision) with semantic page identity → false page-match verdict.

- ER-01: "Page identity must be verified from observable world evidence, not assumed from plan" — directly prevents the fail oracle. ✓
- ER-02: "Element type classification must not be the sole basis for navigability decisions" — prevents the perception→identity conflation path. ✓
- ER-03: prevents text-match conflation. ✓

If ER-01..ER-03 are enforced, the CP-13 fail oracle is prevented. ✓

### Verdict: CONDITIONAL_PASS

**Condition:** ER-04 is borderline-non-minimal (derived from ER-01 + VRD-04). Recommend keeping it in the model but marking it as a derived requirement. Owner: Reality Model Author. Resolution: before B4 admission. Does not block corpus entry.

---

## RM-02 — Multi-Branch Hub with Independent Subtrees

**Primary CP:** CP-04 | **WFs:** 3 | **RIs:** 2 | **ERs:** 3

### 1. Provenance Re-verification — PASS

| Element | Cited Evidence | Verified |
|---|---|---|
| WF-06 | E-07 MultiBranchNavigationTests, E-03 SimulationBaseline | E-07 verified present on feature/refactor (hub with "Go to List A"/"Go to List B"). E-03 verified (7-page Settings with 6 button-type elements). |
| WF-07 | E-07 (back-navigation after List A), CP-05 idempotence | E-07 scenario: after visiting List A and pressing back, List B still has 16 unvisited items. |
| WF-08 | E-03 sub-pages, E-08 subframe depth=4, E-12 Pattern 2 | E-08 verified via TraceReplayFromRunTests.cs. E-12 verified via ContainerGatewayTests.cs. |
| OB-05 | E-07 AllVisited with 0/16 | Verified: documented failing test, hub→List A (16/16)→List B (0/16)→`AllVisited`. |
| OB-06 | E-03 exhaustive 7-page traversal | Verified: 19 pages, 24 actions, 99 steps, all 18 elements. |
| RI-04, RI-05 | Supporting WF/OB chain | Chain integrity verified. |

**No provenance gap.** The key evidence (E-07) is a deterministic failing test — its behavior is reproducible without external dependencies.

### 2. Fact/Inference Separation — PASS

- All 3 WFs carry explicit support kind. WF-06/07 DIRECT, WF-08 INFERRED. ✓
- Both RIs carry confidence, alternatives, and materiality. ✓
- OB-05 records the system's false `AllVisited` claim — this is an observation record, not a world fact. Correctly classified. ✓
- No verdict embedded as WF. ✓

### 3. Contamination Re-check — PASS

- Normative content uses "hub page," "navigable branch," "independent subtree," "branch A / branch B," "exhaustive coverage."
- Legacy terms (`BranchProgressEvidence`, `ApprovedSiblingEvidence`, `CompletedSiblingEvidence`, `IsSubtreeComplete`, `TraversalFSM.Branch`, `HandleBranchAsync`, `ChildrenStrategy.DYNAMIC_MATCH`, `NodeStack`) confined to Legacy Mechanism Context. ✓
- G2 rewrite test: "A hub page can contain N≥2 navigable branches leading to independent subtrees" — no mechanism vocabulary. ✓

### 4. Minimality Re-check — PASS

| Element | Required? | Reason |
|---|---|---|
| WF-06 | YES | Foundational — the hub-with-branches structure |
| WF-07 | YES | Establishes branch independence — required for RI-04 |
| WF-08 | YES | Distinguishes branch (subtree) from single page — required for RI-05 and ER-05 |
| RI-04 | YES | Core inference: single-branch exhaustion ≠ hub completion |
| RI-05 | YES | Establishes that branch structure is discovered, not pre-known |
| ER-05 | YES | Directly prevents CP-04 fail oracle (unvisited branch = incomplete) |
| ER-06 | YES | Gates completion on all-siblings evidence |
| ER-07 | YES | Prevents revisit from resetting — required for CP-05 prevention |

All elements are required. No redundancy found. ✓

### 5. Deduplication Re-check — PASS

- **RM-02 vs RM-06:** RM-02 is about hub-branch completion (horizontal — sibling branches at same level). RM-06 is about depth-bound enforcement (vertical — parent→child depth). Distinct world structures. ✓
- No other RM addresses the hub-with-unvisited-branches world structure. ✓
- Novelty test: removing RM-02 would leave CP-04 without any model. ✓

### 6. Counterfactual Re-check — PASS

- **Per-WF:** WF-07 counterfactual: if visiting branch A DID change branch B's state (e.g., shared mutable state), the hub would not have independent subtrees. But the world evidence (E-07) shows List B unchanged after List A traversal. ✓
- **Per-RI:** RI-04: if single-branch exhaustion WERE hub completion, CP-04's fail oracle would be impossible. But E-07 reproduces it. Material. ✓
- **Falsifiability:** "If both branches A and B produce identical page identities and element inventories at every depth level, the system cannot distinguish them." Clear, observable falsification condition. In the observed world (E-07), branches A and B are distinguishable (different page names, different element inventories) — the falsification condition does NOT hold, which is why the model is valid. ✓

### 7. ER Adequacy — PASS

CP-04 fail oracle: hub with N≥2 branches, only branch A visited, system reports `AllVisited`.

- ER-05: "Every navigable branch must be visited or explicitly skipped before hub completion" — directly prevents the fail oracle. ✓
- ER-06: "Hub completion must be gated on all-siblings evidence, not single-branch exhaustion" — prevents the specific mechanism by which E-07 fails. ✓
- ER-07: idempotence — prevents a different failure mode (revisit resetting progress) but not directly required for CP-04. Still required for CP-05, which is a secondary CP. ✓

If ER-05 and ER-06 are enforced, the CP-04 fail oracle is prevented. ✓

### Verdict: PASS

All 7 items PASS. No conditions.

---

## RM-03 — Goal Satisfaction Recognizable from Current Observation

**Primary CP:** CP-06 | **WFs:** 3 | **RIs:** 1 | **ERs:** 3

### 1. Provenance Re-verification — PASS

| Element | Cited Evidence | Verified |
|---|---|---|
| WF-09 | CP-06 FULLY_CLOSED, `Goal.EvidenceEvaluator` | Verified: Agent.cs unconditional pre-loop evaluation in working tree (415/415 pass). |
| WF-10 | Assertion6, Assertion8 | Assertion6 verified (empty+initially-satisfied→Completed, 0 dispatch). Assertion8 verified (non-empty+initially-satisfied→Completed, 0 dispatch). |
| WF-11 | Assertion8 vs Assertion6 comparison | Verified: same world (Wi‑Fi ON), different plans → same result (Completed, 0 dispatch). Plan length irrelevant. |
| OB-07..OB-09 | Assertion6/8/9 | All three OBs verified via test suite (415/415 pass). |
| RI-06 | Supporting WF/OB chain | Chain integrity verified. |

**Provenance is the strongest in the corpus:** all 3 WFs are DIRECT from executable proofs (Assertion6–9), 415/415 test suite passing, production code in working tree. ✓

### 2. Fact/Inference Separation — PASS

- All 3 WFs carry DIRECT support. ✓
- RI-06 carries HIGH confidence, explicit alternative considered (empty-plan-only special case), and materiality assessment (prevents CP-06 fail oracle). ✓
- OB records are clearly labeled as system observation records, not world facts. ✓
- The distinction between "Goal is a predicate" (WF-09 — world fact about what a Goal IS) and "Plan-length-independent initial GoalEvidence evaluation prevents unnecessary world mutation" (RI-06 — inference about what the system should DO) is correctly maintained. ✓

### 3. Contamination Re-check — PASS

- Normative content uses "Goal," "predicate," "observable world state," "current observation," "Plan," "Plan-step dispatch."
- Legacy terms (`GoalEvidence`, `Goal.EvidenceEvaluator`, `GoalEvidence.Satisfied`, `Complete(runId, evidence)`, `Agent.cs`, `ExecutionPlan.Steps`) confined to Legacy Mechanism Context. ✓
- G2 rewrite test: "The world state observable at the start of a task may already satisfy the Goal" — no mechanism vocabulary. ✓

### 4. Minimality Re-check — PASS

| Element | Required? | Reason |
|---|---|---|
| WF-09 | YES | Foundational — defines what a Goal IS in world terms |
| WF-10 | YES | The core world fact: the world can already satisfy the Goal |
| WF-11 | YES | Distinguishes Plan (execution hypothesis) from world (truth) |
| RI-06 | YES | The actionable inference: don't mutate the world unnecessarily |
| ER-08 | YES | Goal evaluable from any observation |
| ER-09 | YES | No forced dispatch when Goal satisfied |
| ER-10 | YES | Plan length doesn't gate GoalEvidence authority |

**Extremely minimal — 3 WFs + 1 RI + 3 ERs for a proven semantic capability.** No redundancy. ✓

### 5. Deduplication Re-check — PASS

- CP-06 is a distinct domain (Completion / Progress) with a distinct fail oracle (unnecessary world mutation). No other RM addresses "the world already satisfies the Goal at observation time." ✓
- Novelty test: removing RM-03 would leave CP-06 without any model. ✓

### 6. Counterfactual Re-check — PASS

- **Per-WF:** WF-10 counterfactual: if the initial world state NEVER satisfied the Goal, the pre-loop evaluation would always return `Satisfied=false` and normal execution would proceed — but this is falsified by Assertion6/8 (InitialGoalSatisfied variant proves it CAN happen). ✓
- **Per-RI:** RI-06: if the alternative (empty-plan-only special case) were correct, a non-empty plan with an already-satisfied Goal would execute unnecessary steps. This is the CP-06 fail oracle — the alternative is falsified. Material. ✓
- **Falsifiability (§16.3):** "If a world-state change that satisfies the Goal requires an action whose effect is ONLY observable after the action completes... the current observation cannot establish satisfaction and the model correctly defers to normal execution." This is a genuine boundary condition — the model correctly identifies its own limits. The model would be falsified if an already-satisfied Goal produced unnecessary actions whose only effect was to temporarily violate then restore the Goal — this is exactly the CP-06 fail oracle, and it is proven NOT to occur (Assertion6/8). ✓

### 7. ER Adequacy — PASS

CP-06 fail oracle: system navigates to Wi‑Fi page, toggles switch OFF, reports "goal achieved" because prescribed steps were executed. World now contradicts goal.

- ER-08: Goal evaluable from any observation → prevents the "only evaluate after actions" blind spot. ✓
- ER-09: No forced dispatch → directly prevents the fail oracle. ✓
- ER-10: Plan length doesn't gate → prevents the empty-plan-only special case from being the sole guard. ✓

If ER-08..ER-10 are enforced, the CP-06 fail oracle is prevented. Proven by Assertion6–9. ✓

### Verdict: PASS

All 7 items PASS. Strongest-validated model in the corpus — all WFs DIRECT from executable proofs, 415/415 test suite, production code verified.

---

## RM-04 — Entry Verification Before World Interaction

**Primary CP:** CP-01 | **WFs:** 3 | **RIs:** 1 | **ERs:** 3

### 1. Provenance Re-verification — CONDITIONAL

| Element | Cited Evidence | Verified |
|---|---|---|
| WF-12 | EP-03 success manifest | Verified: `appPackage: "com.android.settings"`, `deviceSerial: "emulator-5554"` in committed manifest.json. |
| WF-13 | E-01 `pending_verification`, RD-01 | E-01 normalized evidence verified. RD-01 is a distinction, not evidence — the inference chain is valid but the supporting evidence is E1 (simulation) or E0 (documentation). |
| WF-14 | E-13 GAP-P0-02 | E-13 is E0 (documentation-only, references production code). The claim that "entry actions can report success without producing the intended world effect" is supported by documentation, not by a committed reproduction test. |
| OB-10, OB-11 | EP-03 manifest, trace.jsonl | Both verified against committed feature/refactor artifacts. |
| RI-07 | Supporting WF/OB chain | Chain integrity verified, but WF-14's E0 provenance weakens the inference. |

**Condition:** WF-14 is INFERRED from E0 documentation (E-13 GAP-P0-02). The claim "EntryPolicy returns fake success with zero device ops" is documented but not reproduced in any committed executable test. This is a provenance weakness, not a provenance failure — the claim is plausible and consistent with RD-01, but lacks E3/E4 corroboration.

### 2. Fact/Inference Separation — PASS

- WF-12 DIRECT, WF-13/14 INFERRED — correctly labeled. ✓
- RI-07 carries MEDIUM confidence, explicit alternative, and materiality. ✓
- No verdict embedded as WF. ✓

### 3. Contamination Re-check — PASS

- Normative content uses "foreground application," "device," "target app," "entry action," "observable world effect."
- Legacy terms (`Startup.cs`, `ColdLaunch`, `EntryPolicy`, `AdbScreenStateProvider`, `LaunchApp`) confined to Legacy Mechanism Context. ✓
- G2 rewrite test: "Entry actions can report success without producing the intended world effect" — no mechanism vocabulary. ✓

### 4. Minimality Re-check — PASS

| Element | Required? | Reason |
|---|---|---|
| WF-12 | YES | Foundational — establishes the single-foreground-app world property |
| WF-13 | YES | Establishes the gap between intent and observation — required for CP-01 |
| WF-14 | YES | The core danger: entry action success ≠ world effect. Directly supports RI-07. |
| RI-07 | YES | The actionable inference |
| ER-11 | YES | Directly prevents CP-01 fail oracle |
| ER-12 | YES | Prevents the E-13 fake-success path |
| ER-13 | YES | Device identity stability — secondary but independent requirement |

All elements required. ✓

### 5. Deduplication Re-check — PASS

- CP-01 is a distinct domain (entry verification before traversal). No other RM addresses "is the target app actually in foreground before we start?" ✓
- RM-04 and RM-05 both relate to verification (entry vs navigation), but address different world transitions (pre-traversal vs mid-traversal) with different fail oracles. ✓

### 6. Counterfactual Re-check — PASS

- **Per-WF:** WF-14 counterfactual: if entry actions always produced their intended effect, CP-01 would be unnecessary. But E-13 documents the contrary. WF-14 is non-redundant. ✓
- **Falsifiability:** "If the target app is not in foreground and the system proceeds with traversal, all subsequent observations are of the wrong app." Clear, observable falsification. "The model would be falsified if entry verification succeeds (app confirmed in foreground) but the app crashes or is backgrounded during traversal without detection." Also valid — mid-run foreground loss is a related but distinct failure mode. ✓

### 7. ER Adequacy — CONDITIONAL

CP-01 fail oracle: system begins traversal on wrong foreground app, all observations/actions are on wrong app.

- ER-11: "Foreground application must be verified before any Plan step is dispatched" — directly prevents the fail oracle. ✓
- ER-12: "Entry action success must be verified by observable world effect" — prevents the E-13 fake-success path. ✓
- ER-13: "Device identity must be known and stable" — **CONDITIONAL**. Device identity stability (serial, emulator vs physical) is a precondition for entry verification (you can't verify foreground app if you don't know which device you're talking to), but it is not strictly required to prevent CP-01's fail oracle. A system could verify foreground app without knowing the device serial (e.g., via ADB dumpsys on the single connected device). ER-13 strengthens the model but is not minimal for CP-01.

**Condition:** ER-13 is a supporting requirement (derived from E-02 ADB self-healing, not CP-01 directly). Recommend: keep in model but mark as a secondary ER derived from infrastructure requirements, not from CP-01's fail oracle.

### Verdict: CONDITIONAL_PASS

**Condition 1:** WF-14 provenance is E0 (documentation-only). If B4 admission requires E3+ for core WFs, this model should be DEFERRED until a committed reproduction of the EntryPolicy fake-success scenario exists. Owner: Evidence Steward. Resolution: S1 replay or committed fixture before B4 admission.

**Condition 2:** ER-13 is a supporting requirement not strictly required for CP-01's fail oracle. Recommend marking as derived/secondary. Owner: Reality Model Author. Resolution: before B4 admission.

---

## RM-05 — Navigation Action Effect Observable as Page Change

**Primary CP:** CP-02 | **WFs:** 2 | **RIs:** 2 | **ERs:** 2

### 1. Provenance Re-verification — PASS

| Element | Cited Evidence | Verified |
|---|---|---|
| WF-15 | EP-03 success (3 actions, page change), EP-04 (4 distinct pages), E-03 (7 pages, 12 transitions) | EP-03 verified: `actionsSucceeded: 3`, page identity changed to "About emulated device." EP-04 verified: 4 pages with distinct element inventories. E-03 verified. |
| WF-16 | VE-09 (20% byte-length false success) | VE-09 is E0 (documentation, historical false success). But the claim is narrow: "a page CAN change visually without changing semantically." This is plausible even without the specific run — screenshots can differ (clock update, notification) while page content is identical. |
| OB-12, OB-13 | EP-03 result.json (success + failure) | Both verified against committed feature/refactor artifacts. |
| RI-08, RI-09 | Supporting WF/OB chain | Chain integrity verified. |

**No provenance gap for core claims.** WF-16's evidence is E0 but the claim is independently plausible. ✓

### 2. Fact/Inference Separation — PASS

- WF-15 DIRECT, WF-16 INFERRED. ✓
- RI-08 HIGH confidence, RI-09 MEDIUM confidence — correctly graded. ✓
- OB-12/13 clearly labeled as observation records. ✓

### 3. Contamination Re-check — PASS

- Normative content: "navigation action," "observable element inventory," "page change," "semantic comparison," "raw byte comparison," "stale navigation attempt."
- Legacy terms (`IsStillMine`, `Observe→Verify`, `ViewportIdentity`, `stale-click fuse`, `PressBack`, `SemanticPageName`) confined to Legacy Mechanism Context. ✓

### 4. Minimality Re-check — PASS

| Element | Required? | Reason |
|---|---|---|
| WF-15 | YES | Foundational |
| WF-16 | YES | Establishes the byte-change≠page-change pitfall — required for RI-08 |
| RI-08 | YES | Core inference: semantic comparison trumps byte comparison |
| RI-09 | YES | Stale-click detection rationale |
| ER-14 | YES | Directly prevents CP-02 fail oracle |
| ER-15 | YES | Prevents the VE-09 false-success path |

All elements required. ✓

### 5. Deduplication Re-check — PASS

- RM-05 and RM-01 both involve pages, but RM-01 is about page identity (static), RM-05 is about page change (dynamic transition). Distinct world structures. ✓
- RM-05 and RM-04 both involve verification, but RM-04 is pre-traversal (entry), RM-05 is mid-traversal (navigation). ✓

### 6. Counterfactual Re-check — PASS

- **Per-WF:** WF-16 counterfactual: if visual change always implied semantic change, RI-08 would be unnecessary. But VE-09 documents the contrary. ✓
- **Falsifiability:** "If a navigation action produces a page change that is visually indistinguishable from the previous page... the system cannot verify the transition from observation alone." Clear, observable boundary condition. ✓

### 7. ER Adequacy — PASS

CP-02 fail oracle: navigation action dispatched, no observable page change, system proceeds as if navigation succeeded.

- ER-14: "After each navigation action, observable page change must be verified before proceeding" — directly prevents the fail oracle. ✓
- ER-15: "Page-change verification must use semantic comparison, not raw signal comparison" — prevents the VE-09 false-success path. ✓

### Verdict: PASS

All 7 items PASS. No conditions.

---

## RM-06 — Depth Bound Declared Separately from Discovery

**Primary CP:** CP-07 | **WFs:** 2 | **RIs:** 1 | **ERs:** 2

### 1. Provenance Re-verification — PASS

| Element | Cited Evidence | Verified |
|---|---|---|
| WF-17 | E-08 (subframe depth=4), E-11 (4-level Settings), EP-04 (4 pages) | E-08 verified via TraceReplayFromRunTests.cs. E-11 verified via SettingsEnumerateRegression.cs. EP-04 verified. |
| WF-18 | RD-03, E-11 pre-fix vs post-fix | RD-03 is a distinction. E-11 pre-fix vs post-fix verified: same world, same declared depth=2, pre-fix violated, post-fix enforced. |
| OB-14 | E-11 pre-fix | Verified: Wi‑Fi at depth=3 entered when bound=2. |
| RI-10 | Supporting WF/OB chain | Chain integrity verified. |

**No provenance gap.** The key evidence (E-11) is a permanent regression test — the violation and the fix are both preserved. ✓

### 2. Fact/Inference Separation — PASS

- WF-17 INFERRED, WF-18 DIRECT — correctly labeled. ✓
- RI-10 HIGH confidence, explicit alternative, materiality. ✓
- WF-18 ("a declared depth bound is a constraint on the system, not a property of the world") is a crucial distinction — the depth bound is declared, the world's actual depth is independent. This is correctly a world fact, not a system verdict. ✓

### 3. Contamination Re-check — PASS

- Normative content: "device screen hierarchy," "observable depth," "declared depth bound," "constraint," "discovery process."
- Legacy terms (`maxDepth`, `MaxSubframeDepth`, `DynamicMatch`, `CAND-008`, `leaf_info degradation`, `PlanCompiler`) confined to Legacy Mechanism Context. ✓

### 4. Minimality Re-check — PASS

| Element | Required? | Reason |
|---|---|---|
| WF-17 | YES | Establishes that depth exists in the world |
| WF-18 | YES | The core distinction — constraint ≠ world property |
| RI-10 | YES | The actionable inference |
| ER-16 | YES | Prevents CP-07 fail oracle (depth bound violated during discovery) |
| ER-17 | YES | Prevents the specific violation path (elements at depth≥MaxDepth+1 treated as non-navigable) |

All elements required. Very minimal — 2 WFs + 1 RI + 2 ERs. ✓

### 5. Deduplication Re-check — PASS

- RM-06 (vertical depth constraint) and RM-02 (horizontal hub-branch completion) address different world structures. ✓
- RM-06 and RM-07 both relate to constraints, but RM-06 is about declared bounds, RM-07 is about observation failure vs exhaustion. Distinct. ✓

### 6. Counterfactual Re-check — PASS

- **Per-WF:** WF-18 counterfactual: if depth bound were a world property (the world simply has no pages beyond depth=2), the constraint would never be violated. But E-11 proves the world HAS pages at depth=3 (Wi‑Fi) — the bound is a system constraint, not a world property. ✓
- **Falsifiability:** "If a page at exactly depth=MaxDepth contained navigable elements that were entered — proving the bound was not enforced. Observed pre-fix (E-11): Wi‑Fi at depth=3 entered when bound=2." The model states exactly what observation would refute it, AND that observation has actually occurred (pre-fix). The model survives because the fix prevents it. ✓

### 7. ER Adequacy — PASS

CP-07 fail oracle: depth=2 declared, system enters depth=3 pages during discovery.

- ER-16: "Depth bound must constrain dynamic discovery, not only static plan steps" — directly prevents the fail oracle. ✓
- ER-17: "Elements at depth≥MaxDepth+1 must be treated as non-navigable" — prevents the specific violation mechanism. ✓

If ER-16 and ER-17 are enforced, the CP-07 fail oracle is prevented. Proven by E-11 (post-fix). ✓

### Verdict: PASS

All 7 items PASS. No conditions.

---

## RM-07 — Observation Failure Distinct from Content Exhaustion

**Primary CP:** CP-08 | **WFs:** 3 | **RIs:** 1 | **ERs:** 3

### 1. Provenance Re-verification — CONDITIONAL

| Element | Cited Evidence | Verified |
|---|---|---|
| WF-19 | E-13 GAP-P0-02 | E-13 is E0 (documentation-only). The claim "ADB failure → IsEnd=true" is documented but not reproduced in committed executable evidence. |
| WF-20 | VE-10 (production code) | VE-10 references production `ScenarioCompletionVerifier.cs:124-130` and `scroll_roi_end_reached`. The production code exists on feature/refactor but the claim that the signal is "ignored by verifier" is a code-reading observation, not an executable test. |
| WF-21 | E-12 Pattern 1 | E-12 is E1 (deterministic simulation). Verified via ContainerGatewayTests.cs. |
| OB-15 | E-13 (production code reference) | E0 provenance. |
| OB-16 | E-12 Pattern 1 | E1 provenance. Verified. |
| RI-11 | Supporting WF/OB chain | Chain integrity verified, but two of three supporting WFs rely on E0 evidence. |

**Condition:** WF-19 and WF-20 are both supported primarily by E0 (documentation) or E1 (production code reading). No E4 or E3 evidence of an actual observation-failure-masquerading-as-exhaustion event in a committed run. This is the weakest-provenanced core claim in the corpus. The claim is plausible and consistent with RD-04 and E-02 (ADB failures DO occur), but the specific conflation path (failure → IsEnd=true) is documented, not reproduced.

### 2. Fact/Inference Separation — PASS

- WF-19/20 INFERRED, WF-21 DIRECT — correctly labeled. ✓
- RI-11 MEDIUM confidence — correctly conservative given the evidence strength. ✓
- OB-15/16 correctly classified as observation records (documented behavior), not world facts. ✓

### 3. Contamination Re-check — PASS

- Normative content: "device query failure," "content exhaustion," "empty-result signal," "end-of-content," "consecutive observations," "stable content."
- Legacy terms (`IsEndOfList`, `endProven`, `scroll_roi_end_reached`, `scroll_no_new_elements_end_reached`, `ViewportExplorationEvidence`, `AdbScreenStateProvider`) confined to Legacy Mechanism Context. ✓

### 4. Minimality Re-check — PASS

| Element | Required? | Reason |
|---|---|---|
| WF-19 | YES | Core danger: failure and exhaustion produce the same signal |
| WF-20 | YES | Establishes the multi-signal disagreement problem |
| WF-21 | YES | Establishes the stability-vs-stagnation ambiguity |
| RI-11 | YES | The logical core: absence of observable new content ≠ absence of content |
| ER-18 | YES | Requires distinct signals — directly prevents CP-08 fail oracle |
| ER-19 | YES | Requires positive exhaustion evidence, not error-absence |
| ER-20 | YES | Requires multi-signal reconciliation |

All elements required. ✓

### 5. Deduplication Re-check — PASS

- CP-08 is a distinct domain (observation failure ≠ exhaustion). No other RM addresses the "device query failure produces same signal as genuine end-of-content" world structure. ✓
- RM-07 and RM-05 both involve observation, but RM-05 is about verifying a KNOWN transition (did navigation succeed?), RM-07 is about distinguishing UNKNOWN failure from UNKNOWN exhaustion (did we reach the end or did the device stop responding?). ✓

### 6. Counterfactual Re-check — PASS

- **Per-RI:** RI-11: "The absence of observable new content is not logically equivalent to the absence of content." This is the logical core — it's true independent of the specific mechanism (ADB failure, vision timeout, etc.). The counterfactual is built into the statement itself. ✓
- **Falsifiability:** "If the system could reliably distinguish 'no more content exists' from 'cannot currently observe content,' every observation failure would correctly route to error/recovery instead of completion. The model would be falsified if a genuine end-of-list condition was misclassified as an observation failure." Clear, observable boundary. ✓

### 7. ER Adequacy — CONDITIONAL

CP-08 fail oracle: observation query fails (ADB error, timeout), system reports "end of list reached."

- ER-18: "Observation failure must produce a distinct signal from content exhaustion" — directly prevents the fail oracle. ✓
- ER-19: "End-of-content must be proven by positive evidence, not by the absence of errors" — prevents false-negative completion. ✓
- ER-20: "Multiple end-of-content signals must be reconciled" — **CONDITIONAL**. This is a desirable property but is not strictly required to prevent CP-08's fail oracle. A system with a single reliable end-of-content signal that correctly distinguishes failure from exhaustion would satisfy CP-08 without multi-signal reconciliation. ER-20 is a robustness requirement, not a fail-oracle-prevention requirement.

**Condition:** ER-20 is a robustness requirement, not a strict CP-08 fail-oracle prevention requirement. Recommend: keep in model but mark as a robustness ER (derived from VE-10's multi-signal observation), not a core CP-08 ER.

### Verdict: CONDITIONAL_PASS

**Condition 1:** WF-19 and WF-20 provenance is E0/E1. If B4 admission requires E3+ for core WFs, this model should be DEFERRED until a committed reproduction of the observation-failure→exhaustion-conflation event exists. Owner: Evidence Steward. Resolution: S1 replay (CP-08 is the highest-value S1 upgrade per portfolio) before B4 admission.

**Condition 2:** ER-20 is a robustness requirement, not a core CP-08 fail-oracle prevention requirement. Recommend marking as derived. Owner: Reality Model Author. Resolution: before B4 admission.

---

## RM-08 — Recovery Action Effect Distinct from Error Resolution

**Primary CP:** CP-10 | **WFs:** 3 | **RIs:** 1 | **ERs:** 2

### 1. Provenance Re-verification — PASS

| Element | Cited Evidence | Verified |
|---|---|---|
| WF-22 | E-04 (PressBack recovery action, Bug #2) | E-04 verified via FsmSimulationRegressionTests.cs on feature/refactor. Bug #2: consecutive errors accumulate across backtracks. |
| WF-23 | E-04 Bug #2 | Same evidence. Consecutive errors accumulate — error count did not reset after PressBack. |
| WF-24 | RD-06, Runtime RecoveryResult.Verified|Failed | RD-06 is a distinction. RecoveryResult verified via Runtime code. |
| OB-17 | E-04 Bug #2 | Verified. |
| OB-18 | AgentRecoveryTests post-CP-06 | Verified in working tree (415/415 pass). |
| RI-12 | Supporting WF/OB chain | Chain integrity verified. |

**No provenance gap.** E-04 is deterministic simulation (E1) but reproduces a historical bug (Bug #2). AgentRecoveryTests post-CP-06 fixture repair adds honest probe Goals. ✓

### 2. Fact/Inference Separation — PASS

- WF-22/23 INFERRED, WF-24 DIRECT. ✓
- RI-12 MEDIUM confidence — correctly graded (simulation evidence, no E4 recovery trace). ✓
- OB-17/18 correctly classified as observation records. ✓

### 3. Contamination Re-check — PASS

- Normative content: "recovery action," "observable world," "error resolution," "state reset," "consecutive errors," "post-recovery observation."
- Legacy terms (`RecoveryResult.Verified|Failed`, `RecoveryAnchor`, `ErrorHandling`, `PressBack`, `no-root-page guard`, `5-failure gate`, `Drift→Trap→Recovery→Resume`) confined to Legacy Mechanism Context. ✓

### 4. Minimality Re-check — PASS

| Element | Required? | Reason |
|---|---|---|
| WF-22 | YES | Recovery action changes the world — foundational |
| WF-23 | YES | Consecutive errors produce a different world state — core CP-10 distinction |
| WF-24 | YES | Recovery is an observed outcome, not an assumed transition |
| RI-12 | YES | The actionable inference |
| ER-21 | YES | Directly prevents CP-10 fail oracle |
| ER-22 | YES | Prevents the Bug #2 pattern |

All elements required. ✓

### 5. Deduplication Re-check — PASS

- CP-10 is a distinct domain (Recovery / Error). No other RM addresses "recovery action ≠ guaranteed state reset." ✓
- RM-08 and RM-04/05 relate to verification, but RM-08 is about recovery verification (post-error), RM-04 is about entry verification (pre-traversal), RM-05 is about navigation verification (mid-traversal). Distinct. ✓

### 6. Counterfactual Re-check — PASS

- **Per-RI:** RI-12: if recovery action always resolved the error, the alternative would hold. But Bug #2 (E-04) proves the error persisted after PressBack. ✓
- **Falsifiability:** "If a recovery action always restored the system to a known-good state... recovery would be equivalent to state reset. This is falsified by Bug #2 and the no-root-page guard." Clear, observable. ✓

### 7. ER Adequacy — PASS

CP-10 fail oracle: recovery action dispatched, system assumes error resolved, resumes traversal from corrupted state.

- ER-21: "Recovery action effect must be verified by post-recovery observation before resuming traversal" — directly prevents the fail oracle. ✓
- ER-22: "Consecutive errors must accumulate; recovery is not a counter reset" — prevents the Bug #2 pattern. ✓

### Verdict: PASS

All 7 items PASS. No conditions.

---

## RM-09 — Element Visibility and Type Classification Distinct from Navigability

**Primary CP:** CP-11 | **WFs:** 4 | **RIs:** 7 | **ERs:** 2

### 1. Provenance Re-verification — PASS

| Element | Cited Evidence | Verified |
|---|---|---|
| WF-25 | AF-22 YOLO pipeline, EP-04 element types | AF-22 verified via feature/refactor tools/local_vision/. EP-04 verified. |
| WF-26 | VE-05 (91.9% subtitle misclassification), VE-06 (search box), VE-03 (empty OCR) | VE-05 verified via FixVerificationTests.cs L8. VE-06 verified via 20260805T052309367Z_TraceReplayTests.cs (E3). VE-03 verified via FixVerificationTests.cs L5. |
| WF-27 | VE-07 (substring overmatch), VE-04 (9-case normalization) | Both verified via committed tests. |
| WF-28 | VE-05 (fusion.py chevron heuristic) | fusion.py:292-343 verified present on feature/refactor. |
| OB-19..OB-22 | FixVerificationTests L8/L5, TraceReplayTests, TextTargetResolutionTests | All verified against committed feature/refactor artifacts. |
| RI-13..RI-19 | Supporting WF/OB chain | Chain integrity verified. |

**No provenance gap.** This model has the richest evidence corpus — 4 E3/E4 evidence sources (VE-05, VE-06, VE-07, plus production code). ✓

### 2. Fact/Inference Separation — PASS

- WF-25/26/27/28 all DIRECT — correctly labeled (all are directly observable from evidence). ✓
- All 7 RIs carry confidence, alternatives, and materiality. Confidence distribution: 2 HIGH, 4 MEDIUM, 1 LOW — appropriate for perception evidence. ✓
- OB records correctly classified. ✓

### 3. Contamination Re-check — PASS

- Normative content: "perception pipeline," "type label," "element classification," "navigability," "interaction capability," "element text matching," "substring containment," "visual form."
- Legacy terms (`YOLO`, `Deki-Yolo`, `RapidOCR`/`PaddleOCR`, `fusion.py`, `chevron heuristic`, `label-mapping.json`, `CandidateAuthorizationEvidence`, `dangerousSemantics`, `ElementHandling TemplateSets`) confined to Legacy Mechanism Context. ✓
- G2 rewrite test: "Vision pipeline type labels are perception outputs, not world facts" — uses "perception pipeline" (acceptable: names the function, not the implementation) and "world facts" (normative). ✓

### 4. Minimality Re-check — CONDITIONAL

| Element | Required? | Reason |
|---|---|---|
| WF-25 | YES | Foundational: perception assigns type labels |
| WF-26 | YES | Core: type labels are sometimes wrong |
| WF-27 | YES | Core: text matching is ambiguous |
| WF-28 | YES | Core: the chevron phantom root cause |
| RI-13 | YES | The central inference of the model |
| RI-14 | YES | Identifies the root-cause perception artifact |
| RI-15 | YES | Explains why normalization is needed |
| RI-16 | YES | Core: substring ≠ identity |
| RI-17 | **CONDITIONAL** | LOW confidence, LOW materiality. "Empty OCR output does not mean 'no element exists'" — this is an edge case. Removing it: the model still reproduces CP-11's pressure (the main misclassification paths are subtitle, search box, substring overmatch — all covered by RI-13 through RI-16). RI-17 documents a real edge case but is not required for the pressure. |
| RI-18 | YES | Synthesizes RI-13 through RI-16 into the single principle |
| RI-19 | YES | Distinguishes normative constraints from perception fixes |
| ER-23 | YES | Directly prevents CP-11 fail oracle |
| ER-24 | YES | Prevents CP-12 text-match fail oracle |

**Condition:** RI-17 (empty OCR) is an edge case with LOW confidence and LOW materiality. It documents a real phenomenon but is not required to reproduce CP-11's core pressure. Recommend: keep in model as an observation note but mark as non-essential for the pressure reproduction.

### 5. Deduplication Re-check — PASS

- RM-09 (visibility ≠ navigability, perception) is distinct from RM-01 (page identity, structure). WF-26 in RM-01 (type labels are sometimes wrong) is the world fact; RM-09's RIs (RI-13 through RI-19) are the detailed inferences about WHY and HOW. Complementary, not overlapping. ✓
- RM-09 covers CP-11 (primary) and CP-12 (secondary). CP-12 has no standalone RM (CHALLENGE_REQUIRED). The secondary CP-12 coverage in RM-09 is appropriate. ✓

### 6. Counterfactual Re-check — PASS

- **Per-WF:** WF-26 counterfactual: if type labels were 100% accurate, the entire CP-11 domain would be unnecessary. But VE-05/06/07 prove they are not. ✓
- **Per-RI:** RI-14: if the chevron heuristic were removed, phantom subtitle elements would disappear. This is testable — and would prove RI-14 correct (or not). Material. ✓
- **Falsifiability:** "If the perception pipeline's type labels were 100% accurate, element visibility would be equivalent to navigability for correctly-classified elements. This is falsified by VE-05 (91.9% subtitle misclassification rate), VE-06 (search box misclassification), and VE-07 (type-blind text matching)." The model is falsifiable AND the falsification evidence exists (which is why the model is valid). ✓

### 7. ER Adequacy — PASS

CP-11 fail oracle: element visible + classified as navigable type → system treats as navigable → navigates to wrong page or self-loop.

- ER-23: "Element navigability must be verified by interaction capability evidence, not type label alone" — directly prevents the fail oracle. ✓
- ER-24: "Element text matching must be semantic (identity), not syntactic (substring)" — prevents the CP-12 text-match path. ✓

### Verdict: CONDITIONAL_PASS

**Condition:** RI-17 (empty OCR → navigable target) is an edge case with LOW confidence and LOW materiality. Recommend: retain in model but mark as non-essential for CP-11 pressure reproduction. Owner: Reality Model Author. Resolution: before B4 admission. Does not block corpus entry.

---

## Cross-Validation Summary

### Coverage Map

| CP | RM | Validation |
|---|---|---|
| CP-01 Entry Verify | RM-04 | CONDITIONAL_PASS |
| CP-02 Navigation Page Change | RM-05 | PASS |
| CP-03 Plan ≠ Execution | (cross-cutting) | Embedded in RM-02/05/06 |
| CP-04 Multi-Branch Hub | RM-02 | PASS |
| CP-05 Revisit Idempotence | (embedded in RM-02) | PASS (via RM-02 ER-07) |
| CP-06 Goal Satisfaction | RM-03 | PASS |
| CP-07 Depth Bound | RM-06 | PASS |
| CP-08 Observation Failure | RM-07 | CONDITIONAL_PASS |
| CP-09 Unchanging Content | (embedded in RM-07) | CONDITIONAL_PASS (via RM-07) |
| CP-10 Recovery ≠ Reset | RM-08 | PASS |
| CP-11 Visibility ≠ Navigability | RM-09 | CONDITIONAL_PASS |
| CP-12 Target Grounding | (no RM — CHALLENGE_REQUIRED) | DEFERRED to Phase D |
| CP-13 Page Identity | RM-01 | CONDITIONAL_PASS |
| CP-14 Intent ≠ Execution | (no RM — DEFERRED) | DEFERRED to Phase 5/6 |

12 of 14 CPs have at least one validated RM (5 PASS, 4 CONDITIONAL_PASS). CP-03, CP-05, CP-09 are cross-cutting principles embedded in other RMs. CP-12 awaits the Phase D challenge. CP-14 is explicitly deferred.

### Conditions Register

| ID | RM | Condition | Owner | Resolution Deadline |
|---|---|---|---|---|
| C-01 | RM-01 | ER-04 borderline-non-minimal — mark as derived from ER-01 + VRD-04 | Reality Model Author | Before B4 admission |
| C-02 | RM-04 | WF-14 provenance E0 — defer or upgrade to E3+ before B4 admission | Evidence Steward | Before B4 admission |
| C-03 | RM-04 | ER-13 is supporting, not core CP-01 — mark as derived/secondary | Reality Model Author | Before B4 admission |
| C-04 | RM-07 | WF-19/WF-20 provenance E0/E1 — defer or upgrade to E3+ before B4 admission | Evidence Steward | Before B4 admission (S1 replay target) |
| C-05 | RM-07 | ER-20 is robustness, not core CP-08 — mark as derived | Reality Model Author | Before B4 admission |
| C-06 | RM-09 | RI-17 LOW confidence/materiality edge case — mark as non-essential | Reality Model Author | Before B4 admission |

All 6 conditions are resolvable without new evidence mining (C-01, C-03, C-05, C-06 are labeling changes; C-02, C-04 can be resolved by marking provenance as E0/E1 with a DEFER note or by S1 replay).

### No FAIL verdicts. No model rejected.

## Next Task

**B4_REALITY_MODEL_ADMISSION_AND_DEDUP** — resolve the 6 conditions, run G5 deduplication across the accepted corpus, issue admission outcomes per §18, produce the final Reality Model corpus.

## Repository Changes

`docs/decisions/b3-independent-reality-model-validation-result.md` — created (this report). No other files modified.

STOP.
