# Tasks: perception-actionable-toggle-evidence

> Reconcile note (2026-08-16, DOCS_TASK_TRUTH_RECONCILIATION_ONLY): task truth
> aligned to the independently graduated child `perception-actionable-toggle-evidence-reality-repair`
> (PERCEPTION_ACTIONABLE_TOGGLE_REALITY_REPAIR_INTEGRATED). Full evidence matrix:
> `reconciliation-evidence-matrix.md`. Fusion (2.x), PER-T falsifiers (3.x), Binding
> integration (4.1), reality assets (5.x), and most validation (6.x) are satisfied
> by the graduated repair's production code/tests. Remaining: 4.2 (StateBeliefReducer
> integration incl. ImageSwitchStateProvider state) + parent-wide validation (1.6/6.5/6.10).

## 0. Baseline

- [x] 0.1 Confirm buyer: ACTIONABLE_TOGGLE_EVIDENCE
- [x] 0.2 Confirm gap classification: DETECTOR_CLASS_GAP (primary)
- [x] 0.3 Confirm ObservedElement contract: SUFFICIENT (no changes needed)
- [x] 0.4 Confirm RuntimeSemanticModelChangeRequired: NO
- [x] 0.5 Confirm AdapterContractChangeRequired: NO
- [x] 0.6 Confirm YOLOTrainingRequired: UNDECIDED (try fusion first)
      — resolved by repair: NO training required

## 1. OpenSpec

- [x] 1.1 Create proposal.md
- [x] 1.2 Create design.md
- [x] 1.3 Create spec.md
- [x] 1.4 Create tasks.md
- [x] 1.5 Create .openspec.yaml
- [x] 1.6 Run `openspec validate perception-actionable-toggle-evidence --strict --no-interactive`
      — PASS (parent-wide fresh run, this closure gate)

## 2. Fusion heuristic implementation

> Satisfied by graduated reality repair (heuristics.py production functions;
> see reconciliation-evidence-matrix.md §2).

- [x] 2.1 Add toggle inference heuristic to `heuristics.py`:
  - Detect compact right-side elements near text row end
  - Infer toggle type from aspect ratio and position
  - Associate with label row via vertical overlap
      — SATISFIED_BY_GRADUATED_REALITY_REPAIR (`apply_toggle_inference_heuristic`)
- [x] 2.2 Add switch state inference:
  - Determine ON/OFF from visual features (knob position, brightness)
  - Emit switch_state = null if ambiguous
      — SATISFIED_BY_GRADUATED_REALITY_REPAIR (`_infer_switch_state_from_bounds`;
      Python switch_state remains NON_AUTHORITATIVE, ImageSwitchStateProvider is sole authority)
- [x] 2.3 Ensure canonical type: emit `type = "switch"` for inferred toggles
      — SATISFIED_BY_GRADUATED_REALITY_REPAIR (canonical switch→toggle mapping)
- [x] 2.4 Add Python unit tests for the new heuristics
      — SATISFIED_BY_GRADUATED_REALITY_REPAIR (test_toggle_inference.py)

## 3. PER-T1..T12 tests

> All 12 satisfied by graduated reality repair (test_toggle_inference.py);
> reality regression in test_reality_repair.py (RPER-1..12). See matrix §3.

- [x] 3.1 PER-T1: OFF toggle — test_per_t1_off_toggle
- [x] 3.2 PER-T2: ON toggle — test_per_t2_on_toggle
- [x] 3.3 PER-T3: Ambiguous state — test_per_t3_ambiguous_state
- [x] 3.4 PER-T4: Multiple rows — test_per_t4_multiple_rows
- [x] 3.5 PER-T5: Unrelated nearby control — test_per_t5_unrelated_control
- [x] 3.6 PER-T6: Text-only row — test_per_t6_text_only
- [x] 3.7 PER-T7: Observation locality — test_per_t7_observation_locality
- [x] 3.8 PER-T8: Freshness — test_per_t8_freshness
- [x] 3.9 PER-T9: No scenario leakage — test_per_t9_no_scenario_leakage
- [x] 3.10 PER-T10: Readback not perception — test_per_t10_readback_not_perception
- [x] 3.11 PER-T11: Single pass — test_per_t11_single_pass
- [x] 3.12 PER-T12: No LLM/VLM — test_per_t12_zero_cognitive_models

## 4. Integration tests

- [x] 4.1 Add Binding integration test (production path: Perception -> ObservedElement -> BindingAnalysis -> BindingReconciler)
      — SATISFIED_BY_GRADUATED_REALITY_REPAIR (`P5_EmptyToggle_VisibleToBinding` in
      PerceptionToSemanticBindingTests.cs walks the exact production path)
- [x] 4.2 Add StateBeliefReducer integration test (production path: ObservedElement -> StateBeliefReducer)
      — SATISFIED: `PerceptionToggleToStateBeliefIntegrationTests.RealPerceptionCandidates_ToStateBelief_OnAndOff_ThroughProductionChain`
      (real Python perception pipeline → candidate bounds → same-frame ImageSwitchStateProvider →
      production BindingAnalysis/BindingReconciler → StateBeliefReducer; ON→true, OFF→false asserted).
      Semantic modeling is truthful to the fixture: ON row "Use developer options (master)" →
      `DeveloperOptionsMaster.Enabled`, OFF row "Automatic system updates" →
      `AutomaticSystemUpdates.Enabled` (test-only semantic objects; regression guard forbids
      WifiConnectivity/Bluetooth mis-binding).
      Fixture: `platforms/perception/tests/fixtures/reality/developer-options-falsification.png`.
      Bridge: `tests/UniClaw.Runtime.Tests/Perception/bridge_emit_toggle_candidates.py` (test-only).

## 5. API 35 assets

> Satisfied by graduated reality repair (repo-owned reality fixtures,
> SHA-256 verified in fixtures/reality/README.md). See matrix §5.

- [x] 5.1 Capture OFF toggle asset
      — SATISFIED_BY_GRADUATED_REALITY_REPAIR (developer-options-falsification.png: 4 real switches)
- [x] 5.2 Capture ON toggle asset
      — SATISFIED_BY_GRADUATED_REALITY_REPAIR (same asset: ON switches, knob right)
- [x] 5.3 Capture multiple-row asset
      — SATISFIED_BY_GRADUATED_REALITY_REPAIR (developer-options-scrolled2.png + synthetic battery)
- [x] 5.4 Capture text-only (negative) asset
      — SATISFIED_BY_GRADUATED_REALITY_REPAIR (Settings home 0-candidate asset)
- [x] 5.5 Capture ambiguous state asset if available
      — SATISFIED_BY_GRADUATED_REALITY_REPAIR (synthetic partial/clipped switch, fail-closed)

## 6. Validation

> 6.1-6.4, 6.6-6.9 satisfied by graduated repair's durable validation record
> (repair graduation §13: Python 55/55, training 99/99, C# build 0 errors,
> 1043/1056 with 13 Vision environmental, guards PASS, consistency ALL PASS).
> See matrix §6.

- [x] 6.1 Run Python perception tests — SATISFIED_BY_GRADUATED_REALITY_REPAIR (55/55)
- [x] 6.2 Run targeted toggle fusion tests — SATISFIED_BY_GRADUATED_REALITY_REPAIR
- [x] 6.3 Run Adapter/perception integration tests — SATISFIED_BY_GRADUATED_REALITY_REPAIR (P1-P6)
- [x] 6.4 Run Binding tests — SATISFIED_BY_GRADUATED_REALITY_REPAIR
- [x] 6.5 Run StateBeliefReducer tests — SATISFIED
      (unit tests in RuntimeInternalComponentizationTests + new 4.2 integration test; run below)
- [x] 6.6 Run architecture guards — SATISFIED_BY_GRADUATED_REALITY_REPAIR
- [x] 6.7 Run `dotnet build src/UniClaw.Runtime.sln` — SATISFIED_BY_GRADUATED_REALITY_REPAIR (0 errors)
- [x] 6.8 Run `dotnet test src/UniClaw.Runtime.sln` — SATISFIED_BY_GRADUATED_REALITY_REPAIR (1043/1056)
- [x] 6.9 Run `scripts/check-consistency.sh` — SATISFIED_BY_GRADUATED_REALITY_REPAIR (ALL PASS)
- [x] 6.10 Run `openspec validate perception-actionable-toggle-evidence --strict --no-interactive`
      — PASS (parent-wide fresh run, this closure gate)
