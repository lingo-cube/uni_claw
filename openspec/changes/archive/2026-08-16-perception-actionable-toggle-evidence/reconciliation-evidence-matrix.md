# Task Evidence Matrix — perception-actionable-toggle-evidence

> Reconciliation date: 2026-08-16
> Mode: DOCS_TASK_TRUTH_RECONCILIATION_ONLY (no production/test mutation)
> Child graduated evidence: `perception-actionable-toggle-evidence-reality-repair`
>   (PERCEPTION_ACTIONABLE_TOGGLE_REALITY_REPAIR_INTEGRATED, 2026-08-16)
> Classification legend:
>   A = SATISFIED_BY_EXISTING_PARENT_IMPLEMENTATION
>   B = SATISFIED_BY_GRADUATED_REALITY_REPAIR
>   C = STILL_REQUIRED_AND_UNIMPLEMENTED
>   D = GOVERNANCE_VALIDATION_PENDING
>   E = OBSOLETED_OR_SUPERSEDED
>   F = NOT_PROVEN

## 0. Baseline (all pre-existing complete, verified)

| TaskId | OriginalRequirement | Classification | EvidencePath | EvidenceSymbol/Test | SatisfiedBy | StillRequired | Reason |
|---|---|---|---|---|---|---|---|
| 0.1 | Confirm buyer ACTIONABLE_TOGGLE_EVIDENCE | A | proposal.md header | Buyer = LIVE PHYSICAL SEMANTIC ACTIONABILITY | PARENT | NO | Archived baseline |
| 0.2 | Confirm gap DETECTOR_CLASS_GAP | A | proposal.md header | Gap = DETECTOR_CLASS_GAP (primary) | PARENT | NO | Archived baseline |
| 0.3 | ObservedElement contract SUFFICIENT | A | proposal.md | "Confirm ObservedElement contract: SUFFICIENT" | PARENT | NO | Archived baseline |
| 0.4 | RuntimeSemanticModelChangeRequired NO | A | proposal.md Non-Goals | "No Runtime semantic model changes" | PARENT | NO | Archived baseline |
| 0.5 | AdapterContractChangeRequired NO | A | proposal.md Non-Goals | "No new adapter contracts" | PARENT | NO | Archived baseline |
| 0.6 | YOLOTrainingRequired UNDECIDED | A | proposal.md | UNDECIDED (try fusion first) | PARENT | NO | Archived; resolved by repair (NO training) |

## 1. OpenSpec

| TaskId | OriginalRequirement | Classification | EvidencePath | EvidenceSymbol/Test | SatisfiedBy | StillRequired | Reason |
|---|---|---|---|---|---|---|---|
| 1.1 | Create proposal.md | A | openspec/changes/perception-actionable-toggle-evidence/proposal.md | file exists | PARENT | NO | Present |
| 1.2 | Create design.md | A | design.md | file exists | PARENT | NO | Present |
| 1.3 | Create spec.md | A | specs/.../spec.md | file exists | PARENT | NO | Present |
| 1.4 | Create tasks.md | A | tasks.md | file exists | PARENT | NO | Present |
| 1.5 | Create .openspec.yaml | A | .openspec.yaml | file exists | PARENT | NO | Present |
| 1.6 | Run openspec validate | D | repair graduation §13 | "openspec validate --strict: PASS" | OTHER_GRADUATED_CHANGE | PENDING | Parent-wide fresh validation pending; repair validated its own scope |

## 2. Fusion heuristic implementation

| TaskId | OriginalRequirement | Classification | EvidencePath | EvidenceSymbol/Test | SatisfiedBy | StillRequired | Reason |
|---|---|---|---|---|---|---|---|
| 2.1 | Toggle inference heuristic (compact right-side, aspect ratio, vertical overlap) | B | platforms/perception/uniclaw_perception/fusion/heuristics.py | `apply_toggle_inference_heuristic` (aspect-ratio + `_vertical_overlap` + right-side proximity) | REALITY_REPAIR | NO | Production function present |
| 2.2 | Switch state inference (knob position/brightness; null if ambiguous) | B | heuristics.py | `_infer_switch_state_from_bounds` (knob contrast evidence; null on ambiguity) | REALITY_REPAIR | NO | Production function present; NON_AUTHORITATIVE |
| 2.3 | Canonical type `switch` for inferred toggles | B | heuristics.py + repair record §11 | emits `type="switch"`; adapter maps `switch`→`toggle` (repair record lines 126-128) | REALITY_REPAIR | NO | Canonical vocabulary |
| 2.4 | Python unit tests for heuristics | B | platforms/perception/tests/test_toggle_inference.py | `test_per_t1_off_toggle` … `test_per_t12_zero_cognitive_models` (tests production function) | REALITY_REPAIR | NO | 12 tests present |

## 3. PER-T1..T12 tests

| TaskId | OriginalRequirement | Classification | EvidencePath | EvidenceSymbol/Test | SatisfiedBy | StillRequired | Reason |
|---|---|---|---|---|---|---|---|
| 3.1 | PER-T1 OFF toggle | B | test_toggle_inference.py | `test_per_t1_off_toggle` | REALITY_REPAIR | NO | Present |
| 3.2 | PER-T2 ON toggle | B | test_toggle_inference.py | `test_per_t2_on_toggle` | REALITY_REPAIR | NO | Present |
| 3.3 | PER-T3 Ambiguous state | B | test_toggle_inference.py | `test_per_t3_ambiguous_state` | REALITY_REPAIR | NO | Present |
| 3.4 | PER-T4 Multiple rows | B | test_toggle_inference.py | `test_per_t4_multiple_rows` | REALITY_REPAIR | NO | Present |
| 3.5 | PER-T5 Unrelated nearby control | B | test_toggle_inference.py | `test_per_t5_unrelated_control` | REALITY_REPAIR | NO | Present |
| 3.6 | PER-T6 Text-only row | B | test_toggle_inference.py | `test_per_t6_text_only` | REALITY_REPAIR | NO | Present |
| 3.7 | PER-T7 Observation locality | B | test_toggle_inference.py | `test_per_t7_observation_locality` | REALITY_REPAIR | NO | Present |
| 3.8 | PER-T8 Freshness | B | test_toggle_inference.py | `test_per_t8_freshness` | REALITY_REPAIR | NO | Present |
| 3.9 | PER-T9 No scenario leakage | B | test_toggle_inference.py + test_reality_repair.py | `test_per_t9_no_scenario_leakage`; repair record §PER-T9 | REALITY_REPAIR | NO | Present |
| 3.10 | PER-T10 Readback not perception | B | test_toggle_inference.py | `test_per_t10_readback_not_perception` | REALITY_REPAIR | NO | Present |
| 3.11 | PER-T11 Single pass | B | test_toggle_inference.py | `test_per_t11_single_pass` | REALITY_REPAIR | NO | Present |
| 3.12 | PER-T12 No LLM/VLM | B | test_toggle_inference.py | `test_per_t12_zero_cognitive_models` | REALITY_REPAIR | NO | Present |

## 4. Integration tests

| TaskId | OriginalRequirement | Classification | EvidencePath | EvidenceSymbol/Test | SatisfiedBy | StillRequired | Reason |
|---|---|---|---|---|---|---|---|
| 4.1 | Binding integration (Perception→ObservedElement→BindingAnalysis→BindingReconciler) | B | tests/UniClaw.Runtime.Tests/Scenario/PerceptionToSemanticBindingTests.cs | `P5_EmptyToggle_VisibleToBinding` (empty-text toggle bound via PerceptionType="toggle" through BindingAnalysis→BindingReconciler) | REALITY_REPAIR | NO | Exact production path; empty-toggle case is the detector-gap scenario |
| 4.2 | StateBeliefReducer integration (ObservedElement→StateBeliefReducer, incl. ImageSwitchStateProvider state) | C | RuntimeInternalComponentizationTests.cs (unit: 3 tests) + SwitchStateReaderIntegrationTests.cs (ISwitchStateReader→SwitchState shape, no belief-output assertion) | `StateBeliefReducer_ExactlyOneCurrentToggle_ProducesBelief` et al. (unit, direct construction); no end-to-end ImageSwitchStateProvider→StateBeliefReducer assertion found | NONE | YES | Reducer logic unit-tested; production chain exists (Container.RefreshObjectStateBeliefs); but no single durable test walks raw-pixel→ImageSwitchStateProvider→Binding→StateBeliefReducer and asserts belief |

## 5. API 35 assets

| TaskId | OriginalRequirement | Classification | EvidencePath | EvidenceSymbol/Test | SatisfiedBy | StillRequired | Reason |
|---|---|---|---|---|---|---|---|
| 5.1 | Capture OFF toggle asset | B | platforms/perception/tests/fixtures/reality/developer-options-falsification.png | 4 real switches incl. ON (knob right) and OFF (knob left), GT json | REALITY_REPAIR | NO | Repo-owned, SHA-256 verified |
| 5.2 | Capture ON toggle asset | B | same fixture | ON switches (teal, knob right) | REALITY_REPAIR | NO | Same asset covers |
| 5.3 | Capture multiple-row asset | B | developer-options-scrolled2.png + synthetic battery | multiple real toggles per row + synthetic multi-control | REALITY_REPAIR | NO | Present |
| 5.4 | Capture text-only (negative) asset | B | Settings home asset (repair record §14) | 0 candidates; text-only rows rejected | REALITY_REPAIR | NO | Present |
| 5.5 | Capture ambiguous state asset if available | B | synthetic battery (partial/clipped switch) | partial/clipped switch fail-closed | REALITY_REPAIR | NO | Ambiguity covered synthetically |

## 6. Validation

| TaskId | OriginalRequirement | Classification | EvidencePath | EvidenceSymbol/Test | SatisfiedBy | StillRequired | Reason |
|---|---|---|---|---|---|---|---|
| 6.1 | Run Python perception tests | B | repair record §13 | Python suites 55/55 | REALITY_REPAIR | NO | Durable record |
| 6.2 | Run targeted toggle fusion tests | B | repair record §13 | toggle inference tests green | REALITY_REPAIR | NO | Durable record |
| 6.3 | Run Adapter/perception integration tests | B | PerceptionToSemanticBindingTests.cs | P1-P6 green | REALITY_REPAIR | NO | Present |
| 6.4 | Run Binding tests | B | repair record §13 | Binding targeted suite green | REALITY_REPAIR | NO | Durable record |
| 6.5 | Run StateBeliefReducer tests | D | RuntimeInternalComponentizationTests.cs | 3 unit tests exist; integration gap per 4.2 | NONE | PENDING | Reducer unit tests green; 4.2 integration pending |
| 6.6 | Run architecture guards | B | repair record §13 | guards PASS | REALITY_REPAIR | NO | Durable record |
| 6.7 | Run dotnet build | B | repair record §13 | 0 errors | REALITY_REPAIR | NO | Durable record |
| 6.8 | Run dotnet test | B | repair record §13 | 1043/1056 (13 Vision environmental) | REALITY_REPAIR | NO | Durable record |
| 6.9 | Run check-consistency | B | repair record §13 | ALL PASS | REALITY_REPAIR | NO | Durable record |
| 6.10 | Run openspec validate (parent) | D | — | parent-wide fresh validation pending | NONE | PENDING | Repair validated its own change; parent-wide validation pending |

## Reconciliation Summary

- TasksSatisfiedByParent: 11 (0.1-0.6 + 1.1-1.5)
- TasksSatisfiedByRealityRepair: 30 (2.1-2.4, 3.1-3.12, 4.1, 5.1-5.5, 6.1-6.4, 6.6-6.9)
- TasksStillRequired: 1 (4.2 StateBeliefReducer integration test)
- TasksGovernancePending: 3 (1.6, 6.5, 6.10 — parent-wide validation)
- TasksObsoleteOrSuperseded: 0
- TasksNotProven: 0
