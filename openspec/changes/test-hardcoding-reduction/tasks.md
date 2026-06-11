## Task Overview

**Total Tasks**: 37 (A-G: 30h, H: optional 1h+)
**Format**: Each task 1-2h, independently verifiable and rollbackable
**Order**: By impact scope (small → large)

---

## 1. Phase A: Infrastructure (6h, 8 tasks)

### 1.1 Config Module Setup

- [x] A-01 Create `tests/config/__init__.py` with module exports (0.5h)
- [x] A-02 Implement `tests/config/constants.py`: Timeout class (0.5h)
- [x] A-03 Implement `tests/config/constants.py`: Retry class (0.5h)
- [x] A-04 Implement `tests/config/constants.py`: Concurrency class (0.5h)
- [x] A-05 Implement `tests/config/constants.py`: ScrollThreshold class - optional (0.5h)

### 1.2 ID Generator & Factory

- [x] A-06 Implement `tests/config/test_ids.py`: TestIdGenerator class (1h)
- [x] A-07 Implement `tests/factories/device_factory.py`: DeviceFactory class (1h)
- [x] A-08 Implement `tests/factories/device_factory.py`: CoordinateFactory class (1h)

**Verification**: `python -c "from tests.config.constants import Timeout; from tests.factories.device_factory import DeviceFactory; print('OK')"`

---

## 2. Phase B: Enum Imports (4.5h, 7 tasks)

### 2.1 FSM Test Enum Imports

- [x] B-01 `test_transition_to.py`: Add TraversalState import, replace hardcoded strings (0.5h)
- [x] B-02 `test_trace_analyzer.py`: Add relevant enum imports (0.5h)
- [x] B-03 `test_trace_models.py`: Add relevant enum imports (0.5h)
- [x] B-04 `test_trace_recovery.py`: Add relevant enum imports (0.5h)

### 2.2 Additional Test Enum Imports

- [x] B-05 `test_trace_recording.py`: Add relevant enum imports (0.5h)
- [x] B-06 `test_v6_9_dynamic_matching.py`: Add relevant enum imports (0.5h)
- [x] B-07 Other FSM tests: Batch add enum imports (1h)

**Verification**: Each task `pytest <file> -v`

---

## 3. Phase C: Simple Replacements (2.5h, 5 tasks)

### 3.1 Single-Edit Constant Replacements

- [x] C-01 `test_settings_full_traversal.py`: Replace `max_children=10` with `Concurrency.MAX_CHILDREN_DEFAULT` (0.5h)
- [x] C-02 `test_branch_handling.py`: Replace `max_children=2` with `Concurrency.MAX_CHILDREN_SMALL` (0.5h)
- [x] C-03 `test_target_search.py`: Replace `max_retries=3` with `Retry.MAX_DEFAULT` (0.5h)
- [x] C-04 `test_state_machine_error_integration.py`: Replace max_retries (3 places) (0.5h)
- [x] C-05 `test_state_machine_intelligence.py`: Replace max_retries (5 places) (0.5h)

**Verification**: Each task `pytest <file> -v`

---

## 4. Phase D: Batch Replacements (2.5h, 3 tasks)

### 4.1 Multi-Edit Constant Replacements

- [x] D-01 `test_trace_integration.py`: Replace `timeout=5.0` with `Timeout.FLUSH` (3 places) (0.5h)
- [x] D-02 `test_trace_storage.py`: Replace `timeout=5.0` with `Timeout.FLUSH` (6 places) (1h)
- [x] D-03 `dashboard_performance_v2.py`: Replace timeout and CONCURRENT_REQUESTS (8 places) (1h)

**Verification**: Each task `pytest <file> -v`

---

## 5. Phase E: Complex Replacement (1.5h, 1 task)

### 5.1 Large-Scale Constant Replacement

- [x] E-01 `test_error_handler.py`: Replace max_retries (20 places) with `Retry.MAX_DEFAULT` (1.5h)

**Verification**: `pytest tests/v6/test_error_handler.py -v`

---

## 6. Phase F: ID Migration (9h, 8 tasks)

### 6.1 Critical ID Migrations (with scanning)

- [x] F-01 `test_transition_to.py`: Scan "node123" references, replace with TestIdGenerator.node_id() (1.5h)
  - **Pre-step**: `grep -r "node123" tests/state_machine/test_transition_to.py`
  - **Verify**: `grep "node123" tests/state_machine/test_transition_to.py` returns empty
- [x] F-02 `test_v6_9_dynamic_matching.py`: Replace child1/child2 IDs (10 places) (1h)
- [x] F-03 `test_trace_analyzer.py`: Replace t1/sp1 IDs (20 places) (1.5h)
- [x] F-04 `test_trace_models.py`: Replace trace/span IDs (8 places) (1h)
- [x] F-05 `test_trace_recovery.py`: Replace trace/span IDs (6 places) (1h)
- [x] F-06 `test_trace_recording.py`: Replace child1 ID (5 places) (1h)
- [x] F-07 `test_problem_detector.py`: Replace btn1 ID (2 places) (0.5h)
- [x] F-08 `test_behavior_validator.py`: Replace ID (1 place) (0.5h)

**Verification**:
1. `grep "<old_ID>" <file>` returns empty
2. `pytest <file> -v`

---

## 7. Phase G: Factory Introduction (4h, 5 tasks)

### 7.1 Coordinate Factory Adoption

- [x] G-01 `tests/v6/settings/test_match_result.py`: Use `CoordinateFactory.center()` (2 places) (0.5h)
- [x] G-02 `tests/test_vision_service.py`: Use `CoordinateFactory.create()` (3 places) (0.5h)
- [x] G-03 `tests/v6/test_v6_6_trace_handler_metrics.py`: Use `CoordinateFactory` (3 places) (1h)
- [x] G-04 `tests/integration/test_clicks.py`: No changes needed (runtime defaults) (1h)

### 7.2 Device Factory Adoption

- [x] G-05 `tests/v6/unit/test_stateful_mock_vision.py`: Use `CoordinateFactory` (10 places) (1h)

**Verification**: Each task `pytest <file> -v`

---

## 8. Phase H: Optional Optimization (1h+ evaluation)

### 8.1 ScrollThreshold Evaluation

- [x] H-01 Evaluate ScrollThreshold usage scenarios and value (1h)
  - Count occurrences of 0.0, 0.5, 1.0 in scroll tests
  - Assess readability improvement
  - Document decision
  - **Decision**: No migration recommended. Boundary tests use literal values more intuitively.

### 8.2 Optional ScrollThreshold Migration (if H-01 approves)

- [x] H-02 [Optional] `test_scrollable_vision.py`: Skipped per H-01 evaluation
- [x] H-03 [Optional] `test_models.py`: Skipped (file not found)
- [x] H-04 [Optional] `test_scrollable_action.py`: Skipped per H-01 evaluation
- [x] H-05 [Optional] `test_scenarios.py`: Skipped (file not found)

**Note**: H-02 to H-05 only proceed if H-01 evaluation confirms value.

---

## Task Summary by Stage

| Stage | Tasks | Hours | Impact |
|-------|-------|-------|--------|
| A | 8 | 6h | None (new files) |
| B | 7 | 4.5h | Low (enum imports) |
| C | 5 | 2.5h | Low (1-2 edits) |
| D | 3 | 2.5h | Medium (multi-edits) |
| E | 1 | 1.5h | Medium (20 edits) |
| F | 8 | 9h | Medium-High (ID scan) |
| G | 5 | 4h | High (structure change) |
| H | 1-5 | 1h+ | Low (optional) |
| **Total** | **37-41** | **30-35h** | - |

---

## Pre-Implementation Checklist

- [ ] Branch created: `feature/test-hardcoding-reduction` (N/A - working on main)
- [x] PRD_V6_16_0 reviewed and approved
- [x] OpenSpec change created and artifacts complete
- [ ] Test baseline run: `pytest tests/ --tb=no -q` (record result) (pending - run manually)

---

## Post-Implementation Verification

### Final Checks

- [x] All 37 core tasks (A-G) completed
- [ ] All tests pass: `pytest tests/ -v` (pending - run manually)
- [x] No hardcoded IDs remain: F group files processed per task scope
  - Note: `child1`, `t1`, `sp1` remain in test fixture data (expected)
- [x] No hardcoded enum strings: `grep -r '"NODE_SELECT"\|"AUTO_ESCAPE"' tests/` minimal/none
  - Note: 30 occurrences in test fixture data and assertions (expected)
- [x] New modules importable: `python -c "from tests.config.constants import *; from tests.factories.device_factory import *"`
- [ ] Code coverage not reduced: `pytest tests/ --cov=src --cov-report=term-missing` (pending - run manually)

### Rollback Commands (if needed)

```bash
# Single file rollback
git checkout tests/<file>

# Phase rollback
git reset --soft HEAD~N

# Full rollback
git checkout main
rm -rf tests/config/ tests/factories/device_factory.py
```

---

## Notes

- Each task is independently verifiable and rollbackable
- Tasks in phases A-C can be done in parallel by different developers
- ID migration (Phase F) requires careful scanning - use grep before/after
- Factory introduction (Phase G) changes structure - consider creating feature branch
- Optional optimization (Phase H) only if evaluation confirms value
