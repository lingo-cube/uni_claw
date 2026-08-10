# B2_REALITY_MODEL_EXTRACTION_RESULT

> Generated: 2026-08-09
> Role: Reality Model Author — B2 Extraction (evidence → RM candidates only)
> Authority: `HUMAN_ADOPT_REALITY_MODEL_ADMISSION_CONTRACT` (2026-08-09)
> Contract: `docs/system/reality-model-admission-contract.md` (frozen v1.0)
> Navigation: `docs/decisions/legacy-guidance-led-asset-discovery.md` (EP-01..EP-12)
> Inputs: Steps 1–6 evidence corpus + visual/traversal supplements + unified 14-CP portfolio

---

## Extraction Summary

| Metric | Count |
|---|---|
| Evidence sources consulted | EP-01..EP-12 (12 entrypoints); E-01..E-18 (18 primary); TE-01..TE-10 (10 traversal); VE-01..VE-10 (10 visual) |
| Committed E4/E3 evidence read directly | EP-03 (TraceTool success + failure), EP-04 (sim-replay export), EP-02 TraceReplay fixtures (verified present on feature/refactor) |
| Asset families cross-referenced | 23 (AF-01..AF-23, all verified against guidance map) |
| Reality Distinctions available | RD-01..RD-11, VRD-01..VRD-04, TRD-01..TRD-05 (20 total) |
| Canonical Pressures available | CP-01..CP-14 (14, 7 domains) |
| **Reality Models extracted** | **9** (one per evidence-grounded world structure, not one per CP) |
| World Facts extracted | 28 (WF-01..WF-28) |
| Observation Records extracted | 22 (OB-01..OB-22) |
| Reality Inferences derived | 19 (RI-01..RI-19) |
| Expected Requirements extracted | 24 (ER-01..ER-24) |

**Scope discipline:** Evidence → RM candidates only. No admission performed (B4). No independent validation performed (B3). No Runtime code modified. No architecture recommendations. No new CPs created. No Candidates generated.

---

## Extraction Method

Each Reality Model follows the frozen 16-field canonical schema (§20). Extraction order:

1. **WF (World Facts):** Read from E4/E3 evidence (committed trace fixtures, sim-replay export, TraceReplay fixtures, recorded result.json/manifest.json). Each WF carries `DIRECT` or `INFERRED` support kind. E4 evidence → DIRECT; E3/E2 → DIRECT with provenance note; E1 → INFERRED; E0 → not used for WF.
2. **OB (Observation Records):** Extracted from trace.jsonl record types (`state_transition`, `execution`, `page_analysis`), result.json fields (`completionReason`, `successCriteriaSatisfied`, `successEvidence`), and analysis.jsonl element inventories.
3. **RI (Reality Inferences):** Derived under contract §6 (confidence HIGH/MEDIUM/LOW, explicit alternatives, materiality assessment). Each RI cites supporting WF and is cross-checked against contract's counterfactual rule (§16).
4. **ER (Expected Requirements):** Extracted from scenario JSONs (`locate-one-item.v1.json`, `enumerate-settings-safely.v1.json`), safety policy (`settings-read-only-v1`), FSM design matrix, decision log (D-*), and ExpectedBehavior snapshots (AF-12).

Legacy mechanisms recorded in Legacy Mechanism Context only (§20 field 10). Guidance-vs-evidence classification from Pass 5 enforced: GUIDANCE_ONLY / POINTER_TO_EVIDENCE items not treated as evidence.

---

## Reality Models

---

### RM-01 — Android Device Screen as Page Inventory

**Primary CP:** CP-13 (Raw Page Evidence ≠ Semantic Page Identity)
**Secondary CPs:** CP-11, CP-12

| # | Field | Value |
|---|---|---|
| 1 | RM-ID | `RM-01` (candidate) |
| 2 | Title | Android Device Screen as Observable Page with Typed Element Inventory |
| 3 | Type | MODEL |
| 4 | Pressure Relation | Primary CP-13: raw page evidence (element list from vision) must not be conflated with semantic page identity. Page identity is inferred from element inventory, not declared by the perception pipeline. |
| 5 | World Facts | WF-01..WF-05 |
| 6 | Observation Records | OB-01..OB-04 |
| 7 | Reality Inferences | RI-01..RI-03 |
| 8 | Expected Requirements | ER-01..ER-04 |
| 9 | Temporal Scope | Per-observation snapshot (single ADB screencap → vision inference cycle, ~2–8s) |
| 10 | Legacy Mechanism Context | Non-normative: `PageAnalysis`, `SemanticPageName`, `IsStillMine`, `ViewportIdentity`, `Container.CurrentObservation`, `analysis.jsonl` element inventory, `YOLO`→`OCR`→`fusion.py` pipeline, `label-mapping.json` (`uniclaw.labelMapping.v1`), `Deki-Yolo` 21 labels |
| 11 | Evidence References | E-01 (E2), E-08 (E3), E-10 (E3), VE-01 (E2), VE-03 (E1), VE-04 (E1), VE-08 (E0), EP-03 success trace.jsonl (E4), EP-04 sim-replay pages (E3) |
| 12 | Provenance Chain | EP-03 success run `20260801T124355012Z` (committed, feature/refactor): `trace.jsonl` record_type `page_analysis` at step 1, `result.json` `successEvidence: ["target_page_identity:About emulated device"]`. EP-04 sim-replay run `20260805T083146853Z` (committed, feature/refactor): 4 pages (5+16+21+14 elements) with element types `menuitem`/`text`. E-01 locate scenario: `pending_verification` → post-hoc TraceTool VerifyEngine confirms page identity. |
| 13 | Counterfactual / Falsification | If two distinct Android screens produce identical element inventories (same text, same types, same coordinates), semantic page identity cannot be established from element inventory alone and an additional world signal (package name, activity name, ADB dumpsys) would be required. Observed: UIA path once provided `CurrentPath`/`Items` but was deleted (VE-08). |
| 14 | Validation Status | Not validated (B3 pending) |
| 15 | Admission Outcome | Not admitted (B4 pending) |
| 16 | Confidence Summary | WF: DIRECT from E4 trace.jsonl + E3 sim-replay; RI-01 (HIGH — multi-source corroborated), RI-02 (MEDIUM — coordinate drift observed), RI-03 (HIGH — type misclassification reproduced) |

#### WF-01 — A device screen presents as a finite list of elements
- **Support:** DIRECT
- **Evidence:** EP-04 sim-replay export: 4 pages each with finite element arrays (5, 16, 21, 14 elements). EP-03 success trace.jsonl: each step has exactly one `page_analysis` record producing a bounded element list. E-03 simulation: 7 pages with 18 declared elements.
- **RI citations:** RI-01

#### WF-02 — Each element has a type label and display text
- **Support:** DIRECT
- **Evidence:** EP-04 sim-replay: every element has `type` (menuitem/text) and `text` fields. EP-03 trace.jsonl `page_analysis`: elements classified by vision pipeline. E-01: real AI vision produces structured element list with types and text. AF-22 local vision: YOLO 21-label classification → OCR text extraction → fusion.
- **RI citations:** RI-02, RI-03

#### WF-03 — Element type labels are sometimes wrong
- **Support:** DIRECT
- **Evidence:** VE-05: subtitle "Bluetooth, pairing" classified as `menu_item` (navigable) — 91.9% of 123 pairs affected. VE-06: search box (real y=0.31) classified as `menu_item` instead of `input` — search UI self-loop. VE-07: type-blind Contains matching — "notifications" matched "Flash notifications" → tapped wrong element.
- **RI citations:** RI-03

#### WF-04 — Two screens may share element text but differ in element inventory
- **Support:** INFERRED
- **Evidence:** E-01 locate scenario: `pending_verification` status means Host cannot confirm page identity from vision alone — defers to TraceTool VerifyEngine. E-10: search box element appears on multiple pages with same text but different page context. RD-08 (RawPageEvidence != SemanticPageIdentity).
- **RI citations:** RI-01

#### WF-05 — A device screen's identity is observable but not declared by perception
- **Support:** INFERRED
- **Evidence:** EP-03 success: `successEvidence: ["target_page_identity:About emulated device"]` — identity confirmed AFTER the run, not during. VE-08: UIA provided `CurrentPath`/`Items` but was deleted; AI vision fills `Level1Menus`/`Level2Menus` — consumers cannot tell which path produced the data. RD-08, VRD-04.
- **RI citations:** RI-01

#### OB-01 — EP-03 success run page_analysis record
- **Source:** `feature/refactor:tests/UniClaw.TraceTool.Tests/Fixtures/success/trace/.../trace.jsonl`
- **Record:** `record_type: "execution"`, `action: "page_analysis"`, `status: "ok"`, `spanType: "pageAnalysis"`, step 1
- **Strength:** E4 (committed trace)

#### OB-02 — EP-03 success run result.json successEvidence
- **Source:** `feature/refactor:tests/UniClaw.TraceTool.Tests/Fixtures/success/result.json`
- **Record:** `successEvidence: ["target_action_executed:3", "target_page_identity:About emulated device", "steps/0008/after.png"]`
- **Strength:** E4 (committed result)

#### OB-03 — EP-04 sim-replay 4-page element inventory
- **Source:** `feature/refactor:artifacts/sim-replay/trace-replay-export.json`
- **Record:** 4 pages: "" (5 elements: 3 menuitem + 2 text), "settings" (16 elements: mixed menuitem/text including "Network&internet", "QSearch settings"×3), "network_internet" (21 elements), "internet" (14 elements)
- **Strength:** E3 (committed replay export)

#### OB-04 — EP-03 failure run zero actions attempted
- **Source:** `feature/refactor:tests/UniClaw.TraceTool.Tests/Fixtures/failure/result.json`
- **Record:** `actionsAttempted: 0`, `actionsSucceeded: 0`, `completionReason: "target_page_identity_not_verified"`, `stepsConsumed: 4`, `successCriteriaSatisfied: false`
- **Strength:** E4 (committed result)

#### RI-01 — Page identity is inferable from element inventory but not equivalent to it
- **Confidence:** HIGH
- **Alternatives considered:** (a) page identity = element inventory identity — refuted by WF-04 (two screens share text but differ in inventory); (b) page identity requires external signal (package/activity name) — supported by VE-08 but that signal was deleted, making this alternative unavailable in the current system.
- **Materiality:** HIGH — conflating page identity with element inventory causes false-positive page-match verdicts (E-01 `pending_verification` risk, VE-09 20% byte-length heuristic false success).
- **Supporting WF:** WF-01, WF-04, WF-05
- **Supporting OB:** OB-01, OB-02, OB-04

#### RI-02 — Element coordinates drift across observations of the same screen
- **Confidence:** MEDIUM
- **Alternatives considered:** (a) coordinates are stable — refuted by VE-01 (golden matching tolerates Euclidean ≤0.08–0.1 drift); (b) coordinate drift is always semantically significant — refuted by VE-01 (drifted elements accepted as "correct").
- **Materiality:** MEDIUM — coordinate-only tap (VE-02) without post-tap visual verification risks hitting empty space.
- **Supporting WF:** WF-02
- **Supporting OB:** VE-01 golden matching tolerance

#### RI-03 — Vision pipeline type labels are unreliable for navigability decisions
- **Confidence:** HIGH
- **Alternatives considered:** (a) type labels are reliable — refuted by WF-03 (subtitle as menu_item, search box as menu_item); (b) type labels are always wrong — refuted by E-01 (locate succeeds on real device with correct menu_item labels).
- **Materiality:** HIGH — misclassification causes navigation to wrong pages (VE-05 double-click), search UI self-loops (VE-06), and depth-bound violations (VE-07).
- **Supporting WF:** WF-02, WF-03
- **Supporting OB:** VE-05, VE-06, VE-07

#### ER-01 — Page identity must be verified from observable world evidence, not assumed from plan
- **Source:** CP-13 (RD-08), E-01 locate `pending_verification`, EP-03 failure `target_page_identity_not_verified`
- **Strength:** E2 (integration) + E4 (committed failure)

#### ER-02 — Element type classification must not be the sole basis for navigability decisions
- **Source:** CP-11 (RD-05, VRD-02), VE-05/VE-06/VE-07
- **Strength:** E1+E3

#### ER-03 — Element text matching must be semantic (identity), not substring (Contains)
- **Source:** VE-07, CP-12 (VRD-03)
- **Strength:** E1

#### ER-04 — Page identity evidence must be source-attributed (which pipeline produced it)
- **Source:** VE-08 (C4/D4), VRD-04
- **Strength:** E0

---

### RM-02 — Multi-Branch Hub with Independent Subtrees

**Primary CP:** CP-04 (Multi-Branch Hub Must Not Report Complete With Unvisited Branch)
**Secondary CPs:** CP-05, CP-14

| # | Field | Value |
|---|---|---|
| 1 | RM-ID | `RM-02` (candidate) |
| 2 | Title | Multi-Branch Hub Page Where Each Branch Is an Independent Subtree Requiring Exhaustive Coverage |
| 3 | Type | MODEL |
| 4 | Pressure Relation | Primary CP-04: a hub page with N branches (N≥2) where visiting one branch does not satisfy the completion condition for unvisited siblings. Pass oracle: all branches visited; fail oracle: system reports complete after visiting only branch A. |
| 5 | World Facts | WF-06..WF-08 |
| 6 | Observation Records | OB-05..OB-06 |
| 7 | Reality Inferences | RI-04..RI-05 |
| 8 | Expected Requirements | ER-05..ER-07 |
| 9 | Temporal Scope | Per-traversal-session (from hub entry to hub exit) |
| 10 | Legacy Mechanism Context | Non-normative: `BranchProgressEvidence`, `ApprovedSiblingEvidence`, `CompletedSiblingEvidence`, `IsSubtreeComplete`, `TraversalFSM.Branch` state, `ChildrenStrategy.DYNAMIC_MATCH`, `HandleBranchAsync`, `NodeStack`, `MultiBranchNavigationTests` hub fixture |
| 11 | Evidence References | E-07 (E1, MANDATORY unfixed bug — strongest false-completion evidence), CP-05 (E1, idempotence), TE-04 (E1, both branches discovered, one dispatched), E-03 (E1, 7-page exhaustive coverage), TRD-02 |
| 12 | Provenance Chain | E-07 `MultiBranchNavigationTests.cs`: hub with "Go to List A" (16 items) + "Go to List B" (16 items). Engine walks branch A (16/16), branch B (0/16), yet `CompletionReason = AllVisited`. TDD tests currently FAIL. Reproduces with 3 static items/branch (no scroll required). Archived OpenSpec `navigation-subpage-frames` marked complete but bug persists. |
| 13 | Counterfactual / Falsification | If both branches A and B produce identical page identities and element inventories at every depth level, the system cannot distinguish them and false-completion is undetectable from element inventory alone. Distinguishability requires at least one differentiating world signal (element text, page identity, or structural difference) between branches. |
| 14 | Validation Status | Not validated (B3 pending) |
| 15 | Admission Outcome | Not admitted (B4 pending) |
| 16 | Confidence Summary | WF-06/07 (DIRECT from E-07 fixture), WF-08 (INFERRED from CP-05 idempotence evidence), RI-04 (HIGH — reproduced), RI-05 (MEDIUM — single fixture class) |

#### WF-06 — A hub page can contain N≥2 navigable branches leading to independent subtrees
- **Support:** DIRECT
- **Evidence:** E-07: hub with "Go to List A" and "Go to List B", each leading to 16-item subtrees. E-03: Settings home with 6 button-type elements (Wi‑Fi, Bluetooth, Display, Storage, Battery, Apps), each leading to independent sub-pages. RD-09 (PreviouslyVisited != Unexplored).
- **RI citations:** RI-04

#### WF-07 — Visiting branch A does not change the world state of unvisited branch B
- **Support:** DIRECT
- **Evidence:** E-07: after visiting List A's 16 items and pressing back, List B still has 16 unvisited items. The world state of branch B is independent of branch A traversal. CP-05: revisiting a page does not reset exploration state — idempotence is preserved but does not substitute for initial visit.
- **RI citations:** RI-04

#### WF-08 — A branch is a subtree, not a single page
- **Support:** INFERRED
- **Evidence:** E-03: each Settings entry leads to a sub-page with its own elements (Wi‑Fi page has 1 switch + 3 network buttons; Display page has 2 switches + 1 wallpaper button). E-08: subframe depth=4 under enumerate — branches have internal structure. E-12: non-root FrameCompleted unconsumed → stuck child frame.
- **RI citations:** RI-05

#### OB-05 — E-07 AllVisited with 0/16 on branch B
- **Source:** `feature/refactor:tests/UniClaw.Core.Tests/Baseline/MultiBranchNavigationTests.cs`
- **Record:** Hub → List A (16/16) → List B (0/16) → `CompletionReason = AllVisited`
- **Strength:** E1 (deterministic simulation, documented failing test)

#### OB-06 — E-03 exhaustive 7-page traversal
- **Source:** `feature/refactor:tests/UniClaw.Core.Tests/Baseline/SimulationBaselineTests.cs`
- **Record:** 19 pages visited, 24 actions dispatched, 99 FSM steps, all 18 declared elements visited, `AllVisited`
- **Strength:** E1 (deterministic simulation, S1–S6 snapshot-gated)

#### RI-04 — A hub completion decision based on a single branch's exhaustion is a false completion
- **Confidence:** HIGH
- **Alternatives considered:** (a) visiting any branch satisfies hub completion — refuted by CP-04 pass oracle (all branches must be visited); (b) the bug is scroll-dependent — refuted by E-07 (reproduces with 3 static items/branch, no scroll).
- **Materiality:** HIGH — this is the strongest false-completion evidence in the corpus (MANDATORY, P0, unfixed bug).
- **Supporting WF:** WF-06, WF-07
- **Supporting OB:** OB-05

#### RI-05 — A branch's internal structure (subtree depth, element count) is discovered during traversal, not known a priori
- **Confidence:** MEDIUM
- **Alternatives considered:** (a) branch structure is known before traversal — refuted by TE-01 (DynamicMatch generates children from type rules, not pre-enumerated steps); (b) branch structure is always depth=1 — refuted by E-08 (subframe depth=4).
- **Materiality:** MEDIUM — affects completion detection (when is a sub-tree "done"?).
- **Supporting WF:** WF-08
- **Supporting OB:** OB-06, E-08 depth runaway

#### ER-05 — Every navigable branch from a hub page must be visited or explicitly skipped before hub completion
- **Source:** CP-04 (RD-02, RD-09), E-07 fail oracle
- **Strength:** E1

#### ER-06 — Hub completion must be gated on all-siblings evidence, not single-branch exhaustion
- **Source:** CP-04, TRD-02 (TaskScope != ConcreteWorkInventory), E-07
- **Strength:** E1

#### ER-07 — Branch revisit must be idempotent — must not reset exploration state
- **Source:** CP-05 (RD-09), E-07 (back-navigation after List A returns to hub, not List A restart)
- **Strength:** E1

---

### RM-03 — Goal Satisfaction Recognizable from Current World Observation

**Primary CP:** CP-06 (Goal Satisfaction Must Be Recognizable Without Execution)
**Secondary CPs:** CP-14

| # | Field | Value |
|---|---|---|
| 1 | RM-ID | `RM-03` (candidate) |
| 2 | Title | Goal Satisfaction Evaluable from Current Observation Without Requiring Plan-Step Dispatch |
| 3 | Type | MODEL |
| 4 | Pressure Relation | Primary CP-06: when the external world already satisfies a goal, the system must recognize satisfaction from current observation. Plan existence does not create an obligation to act. FULLY_CLOSED in Phase A (2026-08-09): plan-length-independent initial GoalEvidence authority proven — both empty and non-empty branches (Assertion6–Assertion9). |
| 5 | World Facts | WF-09..WF-11 |
| 6 | Observation Records | OB-07..OB-09 |
| 7 | Reality Inferences | RI-06 |
| 8 | Expected Requirements | ER-08..ER-10 |
| 9 | Temporal Scope | At any observation point during a run (initial post-Startup or post-action) |
| 10 | Legacy Mechanism Context | Non-normative: `GoalEvidence`, `Goal.EvidenceEvaluator`, `GoalEvidence.Satisfied`, `Complete(runId, evidence)`, `Agent.cs` unconditional pre-loop evaluation, `ExecutionPlan.Steps`, `ScenarioGoals.EnableWifi`, `ScenarioGoals.EvaluateWifiSwitchEvidence` |
| 11 | Evidence References | CP-06 FULLY_CLOSED record (Assertion6–Assertion9, 415/415 pass), E-03 (E1, target search stops when found), RD-10 (GoalExpression != GoalState), RD-07 (TaskIntent != ExecutionMethod), ER from scenario/policy JSONs |
| 12 | Provenance Chain | Production `Agent.cs` (uni-agent branch, working tree): unconditional `goal.EvidenceEvaluator(initialObservation)` before main step loop — `Steps.Length == 0` guard removed. Assertion6 (empty + satisfied → Completed, 0 dispatch, seq=2 evidence). Assertion7 (empty + unsatisfied → Failed). Assertion8 (non-empty + satisfied → Completed, 0 Plan-step dispatch). Assertion9 (non-empty + unsatisfied → normal execution). Test suite 415/415 PASS. |
| 13 | Counterfactual / Falsification | If a world-state change that satisfies the Goal requires an action whose effect is ONLY observable after the action completes (e.g., "Wi‑Fi switch must be ON" but switch is hidden behind a scroll), the current observation cannot establish satisfaction and the model correctly defers to normal execution. The model would be falsified if an already-satisfied Goal produced unnecessary actions whose only effect was to temporarily violate then restore the Goal. |
| 14 | Validation Status | Not validated (B3 pending) |
| 15 | Admission Outcome | Not admitted (B4 pending) |
| 16 | Confidence Summary | WF-09/10/11 (DIRECT from executable proofs), RI-06 (HIGH — both branches proven, negative controls pass) |

#### WF-09 — A Goal is a predicate over observable world state
- **Support:** DIRECT
- **Evidence:** CP-06 FULLY_CLOSED: `Goal.EvidenceEvaluator(observation)` returns `GoalEvidence` with `Satisfied` boolean. The evaluator reads world state from the observation (e.g., `obs.Elements.Any(e => e.Text == "Wi‑Fi" && e.SwitchState == true)`). RD-10 (GoalExpression != GoalState).
- **RI citations:** RI-06

#### WF-10 — The world state observable at the start of a task may already satisfy the Goal
- **Support:** DIRECT
- **Evidence:** Assertion6: `InitialGoalSatisfied()` variant launches to `WiFiSettingsOn` (Wi‑Fi already ON) → initial observation seq=2 already satisfies `EnableWifi` Goal → 0 Plan-step dispatches, Completed. Assertion8: same world with non-empty `WifiEnableSequence` plan → still 0 Plan-step dispatches, Completed from seq=2 evidence.
- **RI citations:** RI-06

#### WF-11 — Plan existence does not alter whether the world satisfies the Goal
- **Support:** DIRECT
- **Evidence:** Assertion8 vs Assertion6: same world (Wi‑Fi ON), different plans (empty vs 3-step non-empty) → same result (Completed, 0 dispatches). Plan length is irrelevant to Goal satisfaction. RD-07 (TaskIntent != ExecutionMethod): the plan describes an execution hypothesis, not the world.
- **RI citations:** RI-06

#### OB-07 — Assertion6 trace: empty plan, satisfied Goal, 0 Plan-step dispatches
- **Source:** `tests/UniClaw.Runtime.Tests/Scenario/GoalEvidenceCompletionTests.cs` Assertion6
- **Record:** RunState.Completed, GoalEvidence.Satisfied=true, SourceObservationSequence=2, ActionHistory contains only LaunchApp (no Plan-step actions), exactly one Completed Trace event
- **Strength:** E1 (executable proof)

#### OB-08 — Assertion8 trace: non-empty plan, satisfied Goal, 0 Plan-step dispatches
- **Source:** `tests/UniClaw.Runtime.Tests/Scenario/GoalEvidenceCompletionTests.cs` Assertion8
- **Record:** RunState.Completed, 0 Plan-step dispatches, evidence from seq=2, non-empty `WifiEnableSequence` plan bypassed
- **Strength:** E1 (executable proof)

#### OB-09 — Assertion9 trace: non-empty plan, unsatisfied Goal, normal execution
- **Source:** `tests/UniClaw.Runtime.Tests/Scenario/GoalEvidenceCompletionTests.cs` Assertion9
- **Record:** 3 Plan-step dispatches, Completed from post-action evidence (not prematurely from seq=2), normal execution path preserved
- **Strength:** E1 (executable proof)

#### RI-06 — Plan-length-independent initial GoalEvidence evaluation prevents unnecessary world mutation
- **Confidence:** HIGH
- **Alternatives considered:** (a) only empty plans should skip execution — refuted by CP-06 fail oracle (non-empty plan with already-satisfied Goal would execute unnecessary steps, toggling Wi‑Fi OFF then ON — world briefly contradicts Goal); (b) all plans should always execute at least one step — refuted by same fail oracle.
- **Materiality:** HIGH — prevents the CP-06 fail oracle (system navigates to Wi‑Fi page, toggles switch OFF, reports "goal achieved" because prescribed steps were executed, world now contradicts goal).
- **Supporting WF:** WF-09, WF-10, WF-11
- **Supporting OB:** OB-07, OB-08, OB-09

#### ER-08 — Goal satisfaction must be evaluable from any admissible observation, not only post-action observations
- **Source:** CP-06 FULLY_CLOSED (RD-10), Assertion6/8
- **Strength:** E1 (proven)

#### ER-09 — Plan-step dispatch must not be required when the Goal is already satisfied by current world evidence
- **Source:** CP-06 FULLY_CLOSED, Assertion6/8, RD-07
- **Strength:** E1 (proven)

#### ER-10 — Plan length must not gate GoalEvidence authority
- **Source:** CP-06 FULLY_CLOSED, `HUMAN_AUTHORIZE_PLAN_LENGTH_INDEPENDENT_INITIAL_GOAL`, Assertion8 vs Assertion6
- **Strength:** E1 (proven)

---

### RM-04 — Entry Verification Before World Interaction

**Primary CP:** CP-01 (Entry Must Verify Foreground App Before Traversal)
**Secondary CPs:** CP-08

| # | Field | Value |
|---|---|---|
| 1 | RM-ID | `RM-04` (candidate) |
| 2 | Title | Foreground Application Must Be Verified Before the System Assumes It Can Interact With the Target World |
| 3 | Type | MODEL |
| 4 | Pressure Relation | Primary CP-01: the system must verify the foreground application matches the target before beginning traversal. Entry without verification creates false assumptions about the observable world. |
| 5 | World Facts | WF-12..WF-14 |
| 6 | Observation Records | OB-10..OB-11 |
| 7 | Reality Inferences | RI-07 |
| 8 | Expected Requirements | ER-11..ER-13 |
| 9 | Temporal Scope | Pre-traversal (between Startup and first Plan-step dispatch) |
| 10 | Legacy Mechanism Context | Non-normative: `Startup.cs` foreground verify, `ColdLaunch`, `EntryPolicy`, `AdbScreenStateProvider`, `sys.boot_completed==1` probe, `LaunchApp`, `doctor` probes (`android-emulator.md`) |
| 11 | Evidence References | E-01 (E2, real emulator cold launch), E-13 (E0, EntryPolicy returns fake success with zero device ops), CP-01 (FROZEN_CAPABILITY_COVERED in Runtime), EP-03 success manifest (E4, `appPackage: "com.android.settings"`, `deviceSerial: "emulator-5554"`) |
| 12 | Provenance Chain | EP-03 success manifest: `appPackage: "com.android.settings"`, `deviceSerial: "emulator-5554"`. Runtime `Startup.cs`: foreground verify before traversal entry. E-13: `EntryPolicy` returns fake success ("Cold launched...") with zero device ops — the gap. |
| 13 | Counterfactual / Falsification | If the target app is not in foreground and the system proceeds with traversal, all subsequent observations are of the wrong app. The model would be falsified if entry verification succeeds (app confirmed in foreground) but the app crashes or is backgrounded during traversal without detection. |
| 14 | Validation Status | Not validated (B3 pending) |
| 15 | Admission Outcome | Not admitted (B4 pending) |
| 16 | Confidence Summary | WF-12/13 (DIRECT from committed manifest), WF-14 (INFERRED from E-13 documentation), RI-07 (MEDIUM — gap documented but not reproduced in committed evidence) |

#### WF-12 — A device runs exactly one foreground application at a time
- **Support:** DIRECT
- **Evidence:** EP-03 success manifest: `appPackage: "com.android.settings"` — single target app. E-01: cold launch explicitly sets foreground.
- **RI citations:** RI-07

#### WF-13 — The foreground application can change between the intent to launch and the first observation
- **Support:** INFERRED
- **Evidence:** E-01: `pending_verification` on locate — Host cannot confirm the observed page matches the target without post-hoc verification. RD-01 (ActionExecution != ActionEffect): launching an app is an action; the app being in foreground is the effect.
- **RI citations:** RI-07

#### WF-14 — Entry actions can report success without producing the intended world effect
- **Support:** INFERRED
- **Evidence:** E-13: `EntryPolicy` returns fake success ("Cold launched...") with zero device ops. `AdbScreenStateProvider.cs:38` swallows ADB failures → `IsEnd=true` — scroll failure indistinguishable from end-of-list. GAP-P0-02.
- **RI citations:** RI-07

#### OB-10 — EP-03 success manifest: app package and device identity
- **Source:** `feature/refactor:tests/UniClaw.TraceTool.Tests/Fixtures/success/manifest.json`
- **Record:** `appPackage: "com.android.settings"`, `deviceSerial: "emulator-5554"`, `scenarioId: "locate-one-item"`, `policyId: "settings-read-only-v1"`, `providerId: "sensenova"`, `model: "sensenova-6.7-flash-lite"`
- **Strength:** E4 (committed manifest)

#### OB-11 — EP-03 success trace: safety.launch allowed
- **Source:** `feature/refactor:tests/UniClaw.TraceTool.Tests/Fixtures/success/trace/.../trace.jsonl`
- **Record:** `action: "safety.launch"`, `status: "allow"`, `spanType: "stateDecision"`, `ruleId: "allow.preparation"`, `reason: "Explicit Settings preparation action is allowed."`
- **Strength:** E4 (committed trace)

#### RI-07 — Entry without foreground verification creates a false premise for all subsequent world interaction
- **Confidence:** MEDIUM
- **Alternatives considered:** (a) launch always succeeds — refuted by E-13 (fake success); (b) post-launch observation is sufficient — partially correct but the gap between launch-intent and observation means the system may be observing the wrong app for an unbounded time.
- **Materiality:** HIGH — all subsequent observations, actions, and completion decisions depend on the correct foreground app.
- **Supporting WF:** WF-12, WF-13, WF-14
- **Supporting OB:** OB-10, OB-11

#### ER-11 — Foreground application must be verified against target before any Plan step is dispatched
- **Source:** CP-01 (RD-01, RD-11)
- **Strength:** E2 (integration) + E0 (gap documentation)

#### ER-12 — Entry action success must be verified by observable world effect, not by action completion
- **Source:** CP-01 (RD-01), E-13
- **Strength:** E0

#### ER-13 — The device identity (serial, emulator vs physical) must be known and stable before traversal
- **Source:** EP-03 manifest, E-02 ADB session self-healing
- **Strength:** E2

---

### RM-05 — Navigation Action Effect Observable as Page Change

**Primary CP:** CP-02 (Navigation Must Verify Observable Page Change)
**Secondary CPs:** CP-13

| # | Field | Value |
|---|---|---|
| 1 | RM-ID | `RM-05` (candidate) |
| 2 | Title | Navigation Action Effect Must Be Observable as a Distinct Page Change Before the System Treats the Navigation as Successful |
| 3 | Type | MODEL |
| 4 | Pressure Relation | Primary CP-02: after dispatching a navigation action (tap, back), the system must verify that the observable page changed. A navigation action that produces no observable page change is a failed action, not a successful navigation. |
| 5 | World Facts | WF-15..WF-16 |
| 6 | Observation Records | OB-12..OB-13 |
| 7 | Reality Inferences | RI-08..RI-09 |
| 8 | Expected Requirements | ER-14..ER-15 |
| 9 | Temporal Scope | Per-action cycle (action dispatch → observe → compare) |
| 10 | Legacy Mechanism Context | Non-normative: `IsStillMine`, `Observe→Verify`, `ViewportIdentity`, `Container.CurrentObservation`, `stale-click fuse` (3× circuit breaker), `PressBack no-root-page guard`, `20% byte-length heuristic` (VE-09), `SemanticPageName` |
| 11 | Evidence References | E-01 (E2, locate scenario navigation), E-08 (E3, replay depth runaway), E-09 L4 (E1, stale-click 3× circuit breaker), VE-09 (E0, 20% byte-length false success), EP-03 success (E4, 3 actions succeeded, 8 steps, target_page_identity verified), EP-03 failure (E4, 0 actions attempted, target_page_identity_not_verified) |
| 12 | Provenance Chain | EP-03 success: 3 actions succeeded → `successCriteriaSatisfied: true` → `target_page_identity:About emulated device`. EP-03 failure: 0 actions attempted → `successCriteriaSatisfied: false` → `completionReason: target_page_identity_not_verified`. E-09 L4: same element tapped 3× without page change → circuit breaker skips. VE-09: `target_page_visual_transition_verified` on screenshot that changed bytes but not page. |
| 13 | Counterfactual / Falsification | If a navigation action produces a page change that is visually indistinguishable from the previous page (same elements, same text, same layout), the system cannot verify the transition from observation alone. The model would be falsified if two distinct pages with identical element inventories exist and the system treats them as the same page. |
| 14 | Validation Status | Not validated (B3 pending) |
| 15 | Admission Outcome | Not admitted (B4 pending) |
| 16 | Confidence Summary | WF-15 (DIRECT from EP-03), WF-16 (INFERRED from VE-09), RI-08 (HIGH — multi-source corroborated), RI-09 (MEDIUM — single documented false-success) |

#### WF-15 — A successful navigation action changes the observable element inventory
- **Support:** DIRECT
- **Evidence:** EP-03 success: 3 actions succeeded, `target_page_identity` changed from Settings home to "About emulated device". EP-04: 4 distinct pages with different element inventories (5→16→21→14). E-03: 7 pages connected by 12 transitions, each page with distinct element sets.
- **RI citations:** RI-08

#### WF-16 — A page can change visually (different screenshot bytes) without changing semantically
- **Support:** INFERRED
- **Evidence:** VE-09: 20% byte-length heuristic triggered `target_page_visual_transition_verified` on a screenshot that changed bytes (e.g., notification bar clock update) but the page content was unchanged. Real run `20260729T200940861Z`.
- **RI citations:** RI-09

#### OB-12 — EP-03 success: 3 actions succeeded, page identity verified
- **Source:** `feature/refactor:tests/UniClaw.TraceTool.Tests/Fixtures/success/result.json`
- **Record:** `actionsAttempted: 3`, `actionsSucceeded: 3`, `stepsConsumed: 8`, `scrollsConsumed: 2`, `successCriteriaSatisfied: true`, `successEvidence: ["target_action_executed:3", "target_page_identity:About emulated device"]`
- **Strength:** E4 (committed result)

#### OB-13 — EP-03 failure: 0 actions, page identity not verified
- **Source:** `feature/refactor:tests/UniClaw.TraceTool.Tests/Fixtures/failure/result.json`
- **Record:** `actionsAttempted: 0`, `actionsSucceeded: 0`, `completionReason: "target_page_identity_not_verified"`, `successCriteriaSatisfied: false`
- **Strength:** E4 (committed result)

#### RI-08 — Page-change verification requires semantic comparison, not raw byte comparison
- **Confidence:** HIGH
- **Alternatives considered:** (a) any byte change indicates a page change — refuted by VE-09 (20% byte-length false success); (b) semantic comparison alone is sufficient — partially correct but requires a definition of "semantic" (element inventory comparison, page identity inference).
- **Materiality:** HIGH — false page-change verdicts cause the system to believe it has progressed when it has not (stale-click loops, depth-bound violations).
- **Supporting WF:** WF-15, WF-16
- **Supporting OB:** OB-12, OB-13

#### RI-09 — A repeated action on the same page without observable change is a stale navigation attempt
- **Confidence:** MEDIUM
- **Alternatives considered:** (a) repeat actions always indicate user intent to retry — refuted by E-09 L4 (same element tapped 3× without page change → circuit breaker); (b) circuit breaker should be 1× — the 3× threshold is an implementation artifact, not a world fact.
- **Materiality:** MEDIUM — without a stale-click detector, the system loops on the same page indefinitely.
- **Supporting WF:** WF-15
- **Supporting OB:** E-09 L4

#### ER-14 — After each navigation action, observable page change must be verified before proceeding
- **Source:** CP-02 (RD-01, RD-05)
- **Strength:** E2 (integration) + E4 (committed success/failure)

#### ER-15 — Page-change verification must use semantic comparison (element inventory + page identity), not raw signal comparison
- **Source:** CP-02, VE-09, CP-13 (RD-08)
- **Strength:** E0 (historical false-success)

---

### RM-06 — Depth Bound Declared Separately from Discovery

**Primary CP:** CP-07 (Declared Depth Bound Must Be Enforced During Discovery)
**Secondary CPs:** CP-03

| # | Field | Value |
|---|---|---|
| 1 | RM-ID | `RM-06` (candidate) |
| 2 | Title | Traversal Depth Bound Is a Declared Constraint That Must Constrain Discovery, Not Merely an Input Parameter |
| 3 | Type | MODEL |
| 4 | Pressure Relation | Primary CP-07: a depth bound declared before traversal (e.g., depth≤2) must constrain the discovery process. Discovery that exceeds the declared bound violates the constraint regardless of what was discovered. |
| 5 | World Facts | WF-17..WF-18 |
| 6 | Observation Records | OB-14 |
| 7 | Reality Inferences | RI-10 |
| 8 | Expected Requirements | ER-16..ER-17 |
| 9 | Temporal Scope | Per-traversal-session (discovery phase → enforcement) |
| 10 | Legacy Mechanism Context | Non-normative: `maxDepth`, `MaxSubframeDepth`, `Depth >= MaxDepth+1` → container degrades to `leaf_info`, `DynamicMatch` sub-frame generation, `CAND-008` depth bound, `PlanCompiler` depth propagation, `SettingsEnumerateRegression` |
| 11 | Evidence References | E-08 (E3, subframe depth=4 vs declared ≤2 — depth runaway), E-11 (E1, SettingsEnumerateRegression — post-fix stops at depth=2), E-09 L2/L3/L7 (E1, depth bounds), TE-05 (E1), TRD-01 |
| 12 | Provenance Chain | E-08: `TraceReplayFromRunTests.cs` Step2 diagnoses depth runaway (subframe depth=4 vs declared ≤2) from real run artifacts. Step3 verifies fix. E-11: `SettingsEnumerateRegression.cs` — API-35 4-level Settings fixture; pre-fix enters Wi‑Fi (depth=3) despite declared depth=2; post-fix stops at depth=2. E-09 L7: `Depth >= MaxDepth+1` → container degrades to `leaf_info` (NoAction). |
| 13 | Counterfactual / Falsification | If the world's page hierarchy is shallower than the declared depth bound, the bound never fires and the constraint is vacuously satisfied. The model would be falsified if a page at exactly depth=MaxDepth contained navigable elements that were entered — proving the bound was not enforced. Observed pre-fix (E-11): Wi‑Fi at depth=3 entered when bound=2. |
| 14 | Validation Status | Not validated (B3 pending) |
| 15 | Admission Outcome | Not admitted (B4 pending) |
| 16 | Confidence Summary | WF-17 (INFERRED from E-08/E-11), WF-18 (DIRECT from RD-03), RI-10 (HIGH — reproduced, fixed, regression-guarded) |

#### WF-17 — Device screen hierarchy has observable depth (home → sub-page → sub-sub-page)
- **Support:** INFERRED
- **Evidence:** E-08: subframe depth=4 observed in real run replay. E-11: API-35 Settings has 4 observable levels (home → Network → Wi‑Fi → Advanced). EP-04: 4 pages in sim-replay — depth implied by navigation sequence. RD-03 (ConstraintDeclared != ConstraintEnforced).
- **RI citations:** RI-10

#### WF-18 — A declared depth bound is a constraint on the system, not a property of the world
- **Support:** DIRECT
- **Evidence:** RD-03: ConstraintDeclared != ConstraintEnforced. E-11: same world (4-level Settings), same declared depth=2 — pre-fix violated, post-fix enforced. The world did not change; the enforcement did. TRD-01: TypeLevelTraversalSpecification != ConcreteFutureRoute — the spec declares intent; the world's actual depth may exceed it.
- **RI citations:** RI-10

#### OB-14 — E-11 pre-fix: depth=2 declared, Wi‑Fi entered at depth=3
- **Source:** `feature/refactor:tests/UniClaw.Core.Tests/Simulation/TraceReplay/SettingsEnumerateRegression.cs`
- **Record:** API-35 4-level Settings fixture; pre-fix DynamicMatch sub-frame generation ignored maxDepth → depth runaway; Wi‑Fi (depth=3), wifi/advanced/Wi-Fi (depth=4) entered. Post-fix: stops at depth=2, Wi‑Fi absent from visited pages.
- **Strength:** E1 (deterministic simulation, historical failure)

#### RI-10 — The world's actual depth hierarchy is independent of the declared traversal depth bound
- **Confidence:** HIGH
- **Alternatives considered:** (a) the world's depth always respects the declared bound — refuted by E-11 (Wi‑Fi at depth=3 entered when bound=2); (b) depth bound is advisory — refuted by CP-07 (must be enforced).
- **Materiality:** HIGH — unenforced depth bounds cause unbounded traversal into dangerous or irrelevant sub-pages.
- **Supporting WF:** WF-17, WF-18
- **Supporting OB:** OB-14

#### ER-16 — Depth bound must constrain dynamic discovery, not only static plan steps
- **Source:** CP-07 (RD-03, TRD-01), E-08, E-11
- **Strength:** E3 (replay) + E1 (regression-guarded)

#### ER-17 — Elements at depth ≥ MaxDepth+1 must be treated as non-navigable regardless of type classification
- **Source:** CP-07, E-09 L7 (`Depth >= MaxDepth+1` → `leaf_info` degradation)
- **Strength:** E1

---

### RM-07 — Observation Failure Distinct from Content Exhaustion

**Primary CP:** CP-08 (Observation Failure Must Not Become Content Exhaustion)
**Secondary CPs:** CP-09

| # | Field | Value |
|---|---|---|
| 1 | RM-ID | `RM-07` (candidate) |
| 2 | Title | Device Query Failure Is a Distinct World Event from Scroll-Page Content Exhaustion |
| 3 | Type | MODEL |
| 4 | Pressure Relation | Primary CP-08: when the system cannot observe new content (ADB query fails, vision provider errors, timeout), this is an observation failure, not proof that content is exhausted. Conflating the two causes false end-of-list verdicts. |
| 5 | World Facts | WF-19..WF-21 |
| 6 | Observation Records | OB-15..OB-16 |
| 7 | Reality Inferences | RI-11 |
| 8 | Expected Requirements | ER-18..ER-20 |
| 9 | Temporal Scope | Per-observation cycle (especially scroll-page observations) |
| 10 | Legacy Mechanism Context | Non-normative: `IsEndOfList`, `endProven`, `scroll_roi_end_reached` (production, ignored by verifier), `scroll_no_new_elements_end_reached`, `ViewportExplorationEvidence` tri-state, `ScenarioCompletionVerifier.cs:124-130`, `AdbScreenStateProvider.cs:38` (swallows ADB failures → `IsEnd=true`) |
| 11 | Evidence References | E-13 (E0, AdbScreenStateProvider swallows failures → IsEnd=true — GAP-P0-02), E-12 (E1, scroll stability K=3 → false AllVisited), VE-10 (E0+E1, ROI end-of-scroll signal ignored), CP-09 (E1, unchanging content must not loop), E-02 (E2, ADB session self-healing proves external failure is recoverable) |
| 12 | Provenance Chain | E-13: `AdbScreenStateProvider.cs:38` swallows missing XML attributes → `IsEnd=true` — scroll failure indistinguishable from end-of-list. Also: 429/5xx/timeout no retry. VE-10: production `scroll_roi_end_reached` signal exists but `ScenarioCompletionVerifier.cs:124-130` only accepts legacy `scroll_no_new_elements_end_reached` → `endProven` always false. E-12 Pattern 1: content stability K=3 identical fingerprints → `AllVisited` (was infinite scroll → MaxSteps). |
| 13 | Counterfactual / Falsification | If the system could reliably distinguish "no more content exists" from "cannot currently observe content," every observation failure would correctly route to error/recovery instead of completion. The model would be falsified if a genuine end-of-list condition (3+ consecutive identical observations with exhaustive element coverage) was misclassified as an observation failure. |
| 14 | Validation Status | Not validated (B3 pending) |
| 15 | Admission Outcome | Not admitted (B4 pending) |
| 16 | Confidence Summary | WF-19 (INFERRED from E-13), WF-20 (DIRECT from VE-10 production code), WF-21 (DIRECT from E-12), RI-11 (MEDIUM — documented gap, not reproduced in committed E4 evidence) |

#### WF-19 — Device query failure produces the same empty-result signal as genuine content exhaustion
- **Support:** INFERRED
- **Evidence:** E-13: `AdbScreenStateProvider.cs:38` catches missing XML attributes → `IsEnd=true`. ADB failures, 429/5xx/timeout all return empty/error indistinguishable from "no more content." RD-04 (ObservationFailed != ContentExhausted).
- **RI citations:** RI-11

#### WF-20 — End-of-content signals exist at multiple layers but may not agree
- **Support:** DIRECT
- **Evidence:** VE-10: production `scroll_roi_end_reached` (ROI-based) exists but verifier only accepts `scroll_no_new_elements_end_reached` (legacy). The production signal is produced but never consumed for completion decisions.
- **RI citations:** RI-11

#### WF-21 — Stable content over consecutive observations can indicate either exhaustion or stagnation
- **Support:** DIRECT
- **Evidence:** E-12 Pattern 1: content stability K=3 identical fingerprints was treated as `AllVisited` but was actually infinite scroll → should have been `MaxSteps`. Pattern 2: non-root `FrameCompleted` unconsumed → stuck child frame.
- **RI citations:** RI-11

#### OB-15 — E-13: AdbScreenStateProvider.cs:38 failure swallowing
- **Source:** `feature/refactor:src/UniClaw.Host/Device/AdbScreenStateProvider.cs` line 38
- **Record:** Missing XML attributes caught → `IsEnd=true` returned — scroll failure indistinguishable from end-of-list
- **Strength:** E0 (documentation referencing production code)

#### OB-16 — E-12 Pattern 1: scroll stability false AllVisited
- **Source:** `feature/refactor:tests/UniClaw.Core.Tests/Traversal/ContainerGatewayTests.cs`
- **Record:** Content stability K=3 identical fingerprints → `AllVisited` verdict. Was actually infinite scroll content → should have been `MaxSteps`.
- **Strength:** E1 (deterministic simulation, historical failure)

#### RI-11 — The absence of observable new content is not logically equivalent to the absence of content
- **Confidence:** MEDIUM
- **Alternatives considered:** (a) empty observation = end of content — refuted by E-13 (ADB failure → empty result ≠ no content); (b) multiple consecutive empty observations prove exhaustion — refuted by E-12 Pattern 1 (K=3 stability on infinite scroll).
- **Materiality:** HIGH — false end-of-list verdicts terminate enumeration prematurely, leaving content undiscovered (CP-04 fail oracle at scroll level).
- **Supporting WF:** WF-19, WF-20, WF-21
- **Supporting OB:** OB-15, OB-16

#### ER-18 — Observation failure must produce a distinct signal from content exhaustion
- **Source:** CP-08 (RD-04), E-13
- **Strength:** E0 (documentation gap)

#### ER-19 — End-of-content must be proven by positive evidence (all elements covered, no new elements in N consecutive observations), not by the absence of errors
- **Source:** CP-08, CP-09, E-12
- **Strength:** E1

#### ER-20 — Multiple end-of-content signals must be reconciled before a completion verdict
- **Source:** VE-10, CP-08
- **Strength:** E0+E1

---

### RM-08 — Recovery Action Effect Distinct from Error Resolution

**Primary CP:** CP-10 (Recovery Attempt Must Not Imply Error Resolution)
**Secondary CPs:** CP-01, CP-02

| # | Field | Value |
|---|---|---|
| 1 | RM-ID | `RM-08` (candidate) |
| 2 | Title | Recovery Action Dispatch Is a World Interaction Whose Effect Must Be Verified, Not a Guaranteed State Reset |
| 3 | Type | MODEL |
| 4 | Pressure Relation | Primary CP-10: dispatching a recovery action (PressBack, re-launch) does not guarantee the error is resolved. The recovery action is a world interaction — its effect must be observed and verified. Consecutive errors accumulate; recovery is not a state reset. |
| 5 | World Facts | WF-22..WF-24 |
| 6 | Observation Records | OB-17..OB-18 |
| 7 | Reality Inferences | RI-12 |
| 8 | Expected Requirements | ER-21..ER-22 |
| 9 | Temporal Scope | Per-error event → recovery attempt → verification cycle |
| 10 | Legacy Mechanism Context | Non-normative: `RecoveryResult.Verified|Failed`, `RecoveryAnchor`, `ErrorHandling` FSM state, `PressBack` recovery action, `no-root-page guard`, `5-failure gate`, `consecutive errors accumulate across backtracks`, `AgentRecoveryTests`, `TrapEmissionTests`, `Drift→Trap→Recovery→Resume` pipeline |
| 11 | Evidence References | E-04 (E1, 5-failure gate → PressBack; consecutive errors accumulate — Bug #2), CP-10 (FROZEN_CAPABILITY_COVERED), E-09 L4 (E1, stale-click 3× circuit breaker as recovery-trigger), AgentRecoveryTests (E1, honest probe Goals post-CP-06) |
| 12 | Provenance Chain | E-04 `FsmSimulationRegressionTests.cs`: 7 fault-injection scenarios. Bug #2: consecutive errors accumulate across backtracks — recovery action (PressBack) dispatched but error count did not reset. 5-failure gate triggers PressBack; if no root page guard, PressBack exits the app. Runtime `RecoveryResult.Verified|Failed`: recovery is an observed outcome, not an assumed reset. |
| 13 | Counterfactual / Falsification | If a recovery action always restored the system to a known-good state from which traversal could resume identically, recovery would be equivalent to state reset. This is falsified by Bug #2 (consecutive errors accumulate — the world state after recovery is different from the initial state) and the no-root-page guard (PressBack on root page exits the app, which is a worse state, not a reset). |
| 14 | Validation Status | Not validated (B3 pending) |
| 15 | Admission Outcome | Not admitted (B4 pending) |
| 16 | Confidence Summary | WF-22/23 (INFERRED from E-04), WF-24 (DIRECT from RD-06), RI-12 (MEDIUM — simulation evidence, no E4 recovery trace) |

#### WF-22 — A recovery action (PressBack) changes the observable world
- **Support:** INFERRED
- **Evidence:** E-04: PressBack dispatched as recovery action → world transitions to previous page or exits app (if at root). Bug #2: PressBack dispatched, but error count did not reset — the world changed (page changed) but the error condition persisted.
- **RI citations:** RI-12

#### WF-23 — Consecutive errors produce a world state different from the pre-error state
- **Support:** INFERRED
- **Evidence:** E-04 Bug #2: consecutive errors accumulate across backtracks. Each recovery action changes the page; the error condition may persist or compound. RD-06 (RecoveryAction != ErrorStateReset).
- **RI citations:** RI-12

#### WF-24 — Recovery is an observable outcome, not an assumed state transition
- **Support:** DIRECT
- **Evidence:** RD-06: RecoveryAction != ErrorStateReset. Runtime `RecoveryResult.Verified|Failed`: recovery must be verified by observation, not assumed from action dispatch. CP-10 FROZEN_CAPABILITY_COVERED.
- **RI citations:** RI-12

#### OB-17 — E-04 Bug #2: consecutive errors accumulate across backtracks
- **Source:** `feature/refactor:tests/UniClaw.Core.Tests/StateMachine/FsmSimulationRegressionTests.cs`
- **Record:** 5-failure gate triggers PressBack; consecutive errors accumulate — error count did not reset after recovery action
- **Strength:** E1 (deterministic simulation, historical failure)

#### OB-18 — AgentRecoveryTests post-CP-06: honest probe Goals
- **Source:** `tests/UniClaw.Runtime.Tests/Unit/AgentRecoveryTests.cs` #1, #6 (uni-agent branch, working tree)
- **Record:** Drift → Trap → Recovery (observe/verify/rebind) → Resume → Completed. Honest Goal: `!obs.Elements.Any(e => e.Text == "ProbeTarget")` — completion when ProbeTarget cleared from world, not when foreground matches baseline.
- **Strength:** E1 (executable proof, fixture repaired)

#### RI-12 — Recovery action dispatch and error resolution are separate world events
- **Confidence:** MEDIUM
- **Alternatives considered:** (a) recovery action = error resolved — refuted by E-04 Bug #2 (error persisted after PressBack); (b) recovery is always a full state reset — refuted by no-root-page guard (PressBack on root exits app, a worse state).
- **Materiality:** HIGH — treating recovery as guaranteed reset causes the system to resume traversal from a corrupted world state.
- **Supporting WF:** WF-22, WF-23, WF-24
- **Supporting OB:** OB-17, OB-18

#### ER-21 — Recovery action effect must be verified by post-recovery observation before resuming traversal
- **Source:** CP-10 (RD-06, RD-01), I-9 (Recovery must be verified)
- **Strength:** E1

#### ER-22 — Consecutive errors must accumulate; recovery is not a counter reset
- **Source:** CP-10, E-04 Bug #2
- **Strength:** E1

---

### RM-09 — Element Visibility and Type Classification Distinct from Navigability

**Primary CP:** CP-11 (Element Visibility Must Not Imply Navigability)
**Secondary CPs:** CP-12, CP-13

| # | Field | Value |
|---|---|---|
| 1 | RM-ID | `RM-09` (candidate) |
| 2 | Title | Vision Pipeline Element Classification Is a Perception Output, Not a Navigability Authorization |
| 3 | Type | MODEL |
| 4 | Pressure Relation | Primary CP-11: an element being visible and classified as a navigable type (menu_item, button) does not mean the element is actually navigable. The perception pipeline's type label is a prediction; the element's true interaction capability is a property of the external world. |
| 5 | World Facts | WF-25..WF-28 |
| 6 | Observation Records | OB-19..OB-22 |
| 7 | Reality Inferences | RI-13..RI-19 |
| 8 | Expected Requirements | ER-23..ER-24 |
| 9 | Temporal Scope | Per-observation cycle (perception → classification → navigability decision) |
| 10 | Legacy Mechanism Context | Non-normative: `YOLO` Deki-Yolo 21-label classification, `RapidOCR`/`PaddleOCR` text extraction, `fusion.py` chevron heuristic (lines 292–343 — phantom subtitle source), `label-mapping.json` (`uniclaw.labelMapping.v1`), `CandidateAuthorizationEvidence`, `dangerousSemantics` list, `ElementHandling` TemplateSets (`menu_only`, `full_interaction`, `safe_mode`, `read_only`), `chevron_heuristic` subtitle → menu_item phantom, `double-crop coordinate bug` (1.143×), `search-box misclassification`, `substring Contains matching` |
| 11 | Evidence References | VE-05 (E1+E0, subtitle "Bluetooth, pairing" misclassified menu_item — 91.9% of 123 pairs, chevron heuristic source), VE-06 (E3, search box y=0.31 misclassified menu_item → search UI self-loop), VE-07 (E1, type-blind Contains matching — "notifications" matched "Flash notifications"), VE-03 (E1, empty/whitespace OCR → navigable empty target), VE-04 (E1, 9-case OCR normalization), TE-09 (E1, ElementHandling TemplateSets as type-level safety constraint), VRD-01 (OCR Text Output != Semantic Element Identity), VRD-02 (Element Classification Output != Interaction Capability) |
| 12 | Provenance Chain | VE-05: `fusion.py:292-343` chevron heuristic — the subtitle phantom root cause. `FixVerificationTests.cs` L8: subtitle "Bluetooth, pairing" (dy_full=0.0336) → V2 downgrade threshold 0.035 in crop space missed at 91.9% of 123 pairs → double-click same page. VE-06: `20260805T052309367Z` run — search box (real y=0.31) misclassified `menu_item`; V5 exclusion zone (y<0.10) never fires. VE-07: `TextTargetResolutionTests.cs` + run `20260806T072558649Z` — substring overmatch "Network_1" ⊆ "Network_10" inflates coverage. |
| 13 | Counterfactual / Falsification | If the perception pipeline's type labels were 100% accurate, element visibility would be equivalent to navigability for correctly-classified elements. This is falsified by VE-05 (91.9% subtitle misclassification rate), VE-06 (search box misclassification), and VE-07 (type-blind text matching). The model would be further falsified if a post-perception verification step (e.g., attempting to interact and observing the result) never caught a misclassification. |
| 14 | Validation Status | Not validated (B3 pending) |
| 15 | Admission Outcome | Not admitted (B4 pending) |
| 16 | Confidence Summary | WF-25/26/27/28 (DIRECT from VE-05/VE-06/VE-07/VE-03), RI-13 (HIGH — directly observed), RI-14 (HIGH — reproduced with run evidence), RI-15 (MEDIUM — single fixture class), RI-16 (MEDIUM — two observed cases), RI-17 (LOW — documented not reproduced), RI-18 (MEDIUM), RI-19 (MEDIUM) |

#### WF-25 — The perception pipeline assigns a type label to every detected element
- **Support:** DIRECT
- **Evidence:** AF-22: YOLO Deki-Yolo 21-label classification. EP-04 sim-replay: every element has `type` field (menuitem/text). AF-03 analysis.jsonl: every element carries a type.
- **RI citations:** RI-13, RI-14

#### WF-26 — Type labels are sometimes semantically wrong
- **Support:** DIRECT
- **Evidence:** VE-05: subtitle text classified as `menu_item` (91.9% rate). VE-06: search box classified as `menu_item` instead of `input`. VE-03: empty/whitespace OCR text → `""` → nodeId `dyn_menu_container__root` — navigable empty target.
- **RI citations:** RI-13

#### WF-27 — Element text matching can be ambiguous (substring, whitespace variants)
- **Support:** DIRECT
- **Evidence:** VE-07: "notifications" substring-matched "Flash notifications" → tapped wrong element. VE-04: 9-case OCR normalization required (lowercase, whitespace collapse, comma spacing, null→"") — without it, duplicate element entries and inflated step counts.
- **RI citations:** RI-15, RI-16

#### WF-28 — The chevron heuristic in the vision pipeline fabricates phantom subtitle elements
- **Support:** DIRECT
- **Evidence:** VE-05: `fusion.py:292-343` — the chevron heuristic creates phantom `menu_item` elements from subtitle text (e.g., "Bluetooth, pairing") at coordinates that do not correspond to interactive elements. This is the root cause of the subtitle misclassification.
- **RI citations:** RI-17

#### OB-19 — VE-05: subtitle double-click from chevron phantom
- **Source:** `feature/refactor:tests/UniClaw.Core.Tests/Simulation/TraceReplay/FixVerificationTests.cs` L8
- **Record:** "Bluetooth, pairing" dy_full=0.0336 → V2 downgrade threshold 0.035 missed → classified menu_item → tapped → page didn't change → tapped again
- **Strength:** E1 (executable regression) + E0 (documentation of root cause)

#### OB-20 — VE-06: search box misclassification → self-loop
- **Source:** `feature/refactor:tests/UniClaw.Core.Tests/Simulation/TraceReplay/20260805T052309367Z_TraceReplayTests.cs`
- **Record:** Search box at real y=0.31 classified `menu_item` → navigation action → search UI opens → self-loop stuck. V5 exclusion zone (y<0.10) never fires.
- **Strength:** E3 (recorded-reality-derived)

#### OB-21 — VE-07: substring overmatch "Network_1" ⊆ "Network_10"
- **Source:** `feature/refactor:tests/UniClaw.Core.Tests/.../TextTargetResolutionTests.cs` + run `20260806T072558649Z`
- **Record:** Contains matching inflated element coverage by matching substring prefixes
- **Strength:** E1 (executable regression)

#### OB-22 — VE-03: empty OCR → navigable empty target
- **Source:** `feature/refactor:tests/UniClaw.Core.Tests/Simulation/TraceReplay/FixVerificationTests.cs` L5
- **Record:** Empty/whitespace-only OCR text → `""` → nodeId `dyn_menu_container__root` → navigable empty target → "Click Text target 异常"
- **Strength:** E1 (executable regression)

#### RI-13 — Vision pipeline type labels are perception outputs, not world facts
- **Confidence:** HIGH
- **Alternatives considered:** (a) type labels are reliable world facts — refuted by WF-26 (91.9% subtitle misclassification, search box misclassification); (b) type labels are always unreliable — refuted by E-01 locate succeeding on real device with correct menu_item labels.
- **Materiality:** HIGH — navigability decisions based solely on type labels cause navigation to wrong pages, self-loops, and depth-bound violations.
- **Supporting WF:** WF-25, WF-26
- **Supporting OB:** OB-19, OB-20

#### RI-14 — The chevron heuristic is a perception artifact, not a world structure
- **Confidence:** HIGH
- **Alternatives considered:** (a) chevron-indicated elements are real navigable elements — refuted by VE-05 (double-click, no page change); (b) chevron heuristic should be removed — this is an architecture recommendation (out of B2 scope); the world fact is that the heuristic produces phantom elements, regardless of what should be done about it.
- **Materiality:** HIGH — the heuristic is the root cause of the most prevalent misclassification (91.9% of 123 pairs).
- **Supporting WF:** WF-28
- **Supporting OB:** OB-19

#### RI-15 — OCR text normalization is required because the same world text appears in multiple visual forms
- **Confidence:** MEDIUM
- **Alternatives considered:** (a) OCR output is always identical for the same text — refuted by VE-04 (9 normalization cases required); (b) normalization can be perfect — the 9 cases cover known variants but unknown variants may exist.
- **Materiality:** MEDIUM — without normalization, duplicate element entries inflate step counts and coverage claims.
- **Supporting WF:** WF-27
- **Supporting OB:** VE-04

#### RI-16 — Element text identity is not equivalent to substring containment
- **Confidence:** MEDIUM
- **Alternatives considered:** (a) Contains matching is sufficient for element identity — refuted by VE-07 ("notifications" ≠ "Flash notifications", "Network_1" ⊆ "Network_10"); (b) exact matching is always correct — refuted by VE-04 (normalization needed — exact match would miss valid variants).
- **Materiality:** HIGH — substring overmatch inflates coverage, causing false-positive completion.
- **Supporting WF:** WF-27
- **Supporting OB:** OB-21

#### RI-17 — Empty OCR output does not mean "no element exists"
- **Confidence:** LOW
- **Alternatives considered:** (a) empty OCR = no element — refuted by OB-22 (empty text → navigable target); (b) empty OCR should skip the element — the current behavior makes it navigable, which is the wrong default.
- **Materiality:** LOW — edge case; most elements have non-empty OCR text.
- **Supporting WF:** WF-26
- **Supporting OB:** OB-22

#### RI-18 — Element visibility (detected by YOLO) is a necessary but not sufficient condition for navigability
- **Confidence:** MEDIUM
- **Alternatives considered:** (a) visibility = navigability — refuted by the entire CP-11 corpus; (b) nothing visible is navigable — refuted by E-01 (visible menu items successfully navigated).
- **Materiality:** HIGH — conflating visibility with navigability is the root error behind CP-11.
- **Supporting WF:** WF-25, WF-26
- **Supporting OB:** OB-19, OB-20, OB-21, OB-22

#### RI-19 — Type-level safety constraints (ElementHandling TemplateSets) are a normative layer over perception, not a perception fix
- **Confidence:** MEDIUM
- **Alternatives considered:** (a) safety constraints can compensate for perception errors — partially true (TE-09 filters actionable types) but constraints operate on type labels that may already be wrong (a misclassified `menu_item` passes the `menu_only` template).
- **Materiality:** MEDIUM — TemplateSets reduce but do not eliminate the impact of misclassification.
- **Supporting WF:** WF-25, WF-26
- **Supporting OB:** TE-09

#### ER-23 — Element navigability must be verified by interaction capability evidence, not type label alone
- **Source:** CP-11 (RD-05, VRD-02), VE-05, VE-06
- **Strength:** E3 (recorded-reality-derived) + E1 (reproduced)

#### ER-24 — Element text matching for target identity must be semantic (identity match), not syntactic (substring)
- **Source:** CP-12 (VRD-03), VE-07
- **Strength:** E1

---

## Cross-Cutting Observations

### Evidence gaps (no WF extracted — recorded for B3/B4)

1. **CP-12 Target Grounding Challenge:** No committed E4 evidence of a coordinate-mismatch tap (plan says tap at (0.5,0.5) but target element moved to (0.5,0.6)). This is `CHALLENGE_REQUIRED` per the portfolio. No RM extracted for CP-12 — deferred to the Phase D challenge.
2. **CP-09 Unchanging Content Loop:** Evidence exists (E-12) but is simulation-only (E1). No E4 recorded-run evidence of a scroll-loop in production. RM-07 partially covers this under CP-08.
3. **CP-03 Plan Validity ≠ Execution Success:** Covered as a cross-cutting principle (RD-11) embedded in RM-02, RM-05, RM-06. No standalone RM — the distinction is a meta-principle over multiple models.
4. **CP-05 Revisit Idempotence:** Embedded in RM-02 (ER-07). No standalone RM — idempotence is a property of the branch-progress mechanism, not a distinct world structure.
5. **CP-14 Intent ≠ Execution Method:** Embedded in RM-03 (ER-10, RD-07) and RM-06 (TRD-01). No standalone RM — this is `EXPLICITLY_DEFERRED_CAPABILITY` per the portfolio (Phase 5/6).

### Provenance warnings carried forward from guidance map (Pass 6)

- Cited runs `20260806T072534Z` / `20260806T072558649Z` are gitignored — E4 claims doc-anchored only. B3 must verify on-disk existence before validating derived RMs.
- Integration evidence (E-01, E-02) is scope-gated — tests skipped by default. E2 claims must be qualified with "when scope-enabled."
- `all_visited` in EP-04 (AF-09) is a legacy verdict, not a world fact — consistent with contract §12 (no answers embedded in reality).
- Python-era trace assets (AF-21) referenced by stale skills are absent from feature/refactor — no WF extracted from them.

### Reality Distinction coverage

| RD | Covered by RM |
|---|---|
| RD-01 ActionExecution != ActionEffect | RM-04, RM-05, RM-08 |
| RD-02 WorkDispatched != WorkCompleted | RM-02 |
| RD-03 ConstraintDeclared != ConstraintEnforced | RM-06 |
| RD-04 ObservationFailed != ContentExhausted | RM-07 |
| RD-05 ElementPresence != ElementNavigability | RM-09 |
| RD-06 RecoveryAction != ErrorStateReset | RM-08 |
| RD-07 TaskIntent != ExecutionMethod | RM-03 |
| RD-08 RawPageEvidence != SemanticPageIdentity | RM-01 |
| RD-09 PreviouslyVisited != Unexplored | RM-02 |
| RD-10 GoalExpression != GoalState | RM-03 |
| RD-11 PlanConstructed != ExecutionGuaranteed | (cross-cutting, embedded in RM-02/05/06) |
| VRD-01 OCR Text Output != Semantic Element Identity | RM-09 |
| VRD-02 Element Classification Output != Interaction Capability | RM-09 |
| VRD-03 Coordinate/Text Match != Semantic Target Identity | RM-09 |
| VRD-04 Observation Source Output != Authoritative World Evidence | RM-01 |
| TRD-01 TypeLevelTraversalSpecification != ConcreteFutureRoute | RM-06 |
| TRD-02 TaskScope != ConcreteWorkInventory | RM-02 |
| TRD-03 PlanStepExhaustion != TraversalCompletion | (cross-cutting) |
| TRD-04 PlanConstructionValidation != WorldCorrespondenceValidation | (cross-cutting) |
| TRD-05 ElementCategoryAuthorization != ConcreteCandidateExistence | RM-09 |

20/20 distinctions covered across 9 Reality Models.

---

## B3 Readiness

**READY_FOR_B3_INDEPENDENT_VALIDATION.**

The 9 Reality Models contain:
- 28 World Facts (WF-01..WF-28) with DIRECT/INFERRED support classification
- 22 Observation Records (OB-01..OB-22) sourced from E4/E3/E2/E1 evidence
- 19 Reality Inferences (RI-01..RI-19) with confidence, alternatives, and materiality
- 24 Expected Requirements (ER-01..ER-24) sourced from scenario JSONs, safety policy, FSM matrix, decision log, and ExpectedBehavior snapshots

All 16 canonical fields populated for each RM. Counterfactual/falsification statements provided for each model. Legacy mechanism context explicitly marked non-normative. Provenance chains cite specific commits, run IDs, and file paths.

B3 must independently verify:
- Each WF against its cited evidence (does the evidence actually support the claimed world fact?)
- Each RI against contract §6 (confidence, alternatives, materiality)
- Each ER against its source (scenario JSON, safety policy, FSM matrix, decision log)
- Contract gates G1–G6 per RM
- Counterfactual/falsification statements

## Next Task

**B3_INDEPENDENT_REALITY_MODEL_VALIDATION** — validate the 9 RM candidates against the contract's independent validation rules (§17: PASS / CONDITIONAL_PASS / FAIL).

## Repository Changes

`docs/decisions/b2-reality-model-extraction-result.md` — created (this report). No Runtime code modified. No other files changed.

STOP.
