# Tasks: perception-actionable-toggle-evidence-reality-repair

## 0. Baseline

- [x] 0.1 Create falsification decision record
- [x] 0.2 Create OpenSpec (proposal, design, spec, tasks)
- [x] 0.3 Confirm root cause: RAW_CONTROL_CANDIDATE_GENERATION_GAP

## 1. Raw-pixel candidate generation

- [x] 1.1 Pass decoded image to fusion engine
- [x] 1.2 Implement raw-pixel toggle candidate detection in heuristics
- [x] 1.3 Integrate with production fusion pipeline
- [x] 1.4 Add reality regression test (PER-REAL-01)

## 2. Reality tests

- [x] 2.1 RPER-1: text_block-only YOLO + real toggle -> toggle discovered
- [x] 2.2 RPER-2: multiple real toggles -> correct candidate bounds
- [x] 2.3 RPER-3: real chevron/non-toggle -> rejected
- [x] 2.4 RPER-4: text-only row -> rejected
- [x] 2.5 RPER-5: partial/ambiguous control -> fail closed
- [x] 2.6 RPER-6: canonical switch->toggle propagation
- [x] 2.7 RPER-7: same-frame ImageSwitchStateProvider ON
- [x] 2.8 RPER-8: same-frame ImageSwitchStateProvider OFF
- [x] 2.9 RPER-9: Binding production path
- [x] 2.10 RPER-10: StateBelief production path
- [x] 2.11 RPER-11: single screenshot / single perception pass
- [x] 2.12 RPER-12: zero LLM/VLM

## 3. Validation

- [x] 3.1 Run Python perception tests
- [x] 3.2 Run new reality tests
- [x] 3.3 Run C# integration tests
- [x] 3.4 Run full regression
- [x] 3.5 Run consistency check
- [x] 3.6 Run OpenSpec validation
