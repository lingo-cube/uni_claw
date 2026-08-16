# Tasks: perception-actionable-toggle-evidence

## 0. Baseline

- [x] 0.1 Confirm buyer: ACTIONABLE_TOGGLE_EVIDENCE
- [x] 0.2 Confirm gap classification: DETECTOR_CLASS_GAP (primary)
- [x] 0.3 Confirm ObservedElement contract: SUFFICIENT (no changes needed)
- [x] 0.4 Confirm RuntimeSemanticModelChangeRequired: NO
- [x] 0.5 Confirm AdapterContractChangeRequired: NO
- [x] 0.6 Confirm YOLOTrainingRequired: UNDECIDED (try fusion first)

## 1. OpenSpec

- [x] 1.1 Create proposal.md
- [x] 1.2 Create design.md
- [x] 1.3 Create spec.md
- [x] 1.4 Create tasks.md
- [x] 1.5 Create .openspec.yaml
- [ ] 1.6 Run `openspec validate perception-actionable-toggle-evidence --strict --no-interactive`

## 2. Fusion heuristic implementation

- [ ] 2.1 Add toggle inference heuristic to `heuristics.py`:
  - Detect compact right-side elements near text row end
  - Infer toggle type from aspect ratio and position
  - Associate with label row via vertical overlap
- [ ] 2.2 Add switch state inference:
  - Determine ON/OFF from visual features (knob position, brightness)
  - Emit switch_state = null if ambiguous
- [ ] 2.3 Ensure canonical type: emit `type = "switch"` for inferred toggles
- [ ] 2.4 Add Python unit tests for the new heuristics

## 3. PER-T1..T12 tests

- [ ] 3.1 PER-T1: OFF toggle
- [ ] 3.2 PER-T2: ON toggle
- [ ] 3.3 PER-T3: Ambiguous state
- [ ] 3.4 PER-T4: Multiple rows
- [ ] 3.5 PER-T5: Unrelated nearby control
- [ ] 3.6 PER-T6: Text-only row
- [ ] 3.7 PER-T7: Observation locality
- [ ] 3.8 PER-T8: Freshness
- [ ] 3.9 PER-T9: No scenario leakage
- [ ] 3.10 PER-T10: Readback not perception
- [ ] 3.11 PER-T11: Single pass
- [ ] 3.12 PER-T12: No LLM/VLM

## 4. Integration tests

- [ ] 4.1 Add Binding integration test (production path: Perception -> ObservedElement -> BindingAnalysis -> BindingReconciler)
- [ ] 4.2 Add StateBeliefReducer integration test (production path: ObservedElement -> StateBeliefReducer)

## 5. API 35 assets

- [ ] 5.1 Capture OFF toggle asset
- [ ] 5.2 Capture ON toggle asset
- [ ] 5.3 Capture multiple-row asset
- [ ] 5.4 Capture text-only (negative) asset
- [ ] 5.5 Capture ambiguous state asset if available

## 6. Validation

- [ ] 6.1 Run Python perception tests
- [ ] 6.2 Run targeted toggle fusion tests
- [ ] 6.3 Run Adapter/perception integration tests
- [ ] 6.4 Run Binding tests
- [ ] 6.5 Run StateBeliefReducer tests
- [ ] 6.6 Run architecture guards
- [ ] 6.7 Run `dotnet build src/UniClaw.Runtime.sln`
- [ ] 6.8 Run `dotnet test src/UniClaw.Runtime.sln`
- [ ] 6.9 Run `scripts/check-consistency.sh`
- [ ] 6.10 Run `openspec validate perception-actionable-toggle-evidence --strict --no-interactive`
