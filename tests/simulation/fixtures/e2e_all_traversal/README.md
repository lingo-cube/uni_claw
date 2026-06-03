# E2E All Traversal Test Set

## 📋 Overview

This test set validates depth-first traversal order and restore operations for a complete menu navigation scenario. It strictly follows the PRD specifications for graph models, state machines, and PageAnalysis definitions.

## 📁 Directory Structure

```
e2e_all_traversal/
├── test_case.json          # AI-friendly test case description
├── plan_all.json            # Traversal plan with dynamic matching
├── pages_all.json           # Virtual page library (PageAnalysis format)
├── expected_trace.json      # Expected trace for comparison
└── README.md                # This file
```

## 🎯 Test Purpose

- ✅ Validate depth-first navigation order
- ✅ Verify restore operations for interactive elements
- ✅ Test dynamic matching strategy
- ✅ Ensure complete menu traversal
- ✅ Verify error handling and edge cases

## 📊 Test Specifications

### Intent Slots
```json
{
  "target_app": "设置",
  "scope": "all_menus",
  "element_handling": "full_interaction",
  "navigation": "adaptive",
  "restore": true,
  "depth": 3
}
```

### Expected Results
- **Completion Reason**: `completed`
- **Key Events**: 14 expected events in sequence
- **Step Range**: 12-30 steps
- **Visited Nodes**: Minimum 6
- **Restore Operations**: Minimum 4

### Navigation Order
The test expects this depth-first order:
1. Settings
2. Display
3. Brightness (with restore)
4. Auto Brightness (with restore)
5. Sound
6. Volume (with restore)
7. Mute (with restore)

## 🔧 Technical Details

### PageAnalysis Format
Each page in `pages_all.json` follows the PageAnalysis structure:
- `current_path`: Array representing navigation path
- `items`: Array of UI elements with metadata
- `has_scroll`: Boolean indicating scrollability
- `is_popup`: Boolean for popup detection

### Dynamic Matching Strategy
The plan uses `dynamic_match` with three rules:
- **menu_rule**: Matches menu_items for navigation
- **switch_rule**: Matches switches for toggle operations
- **slider_rule**: Matches sliders for swipe operations

### Expected Trace
The `expected_trace.json` defines the exact execution sequence:
- 10 steps with specific actions and targets
- Restore operations marked with `restore: true`
- Page transitions tracked via `page_after` field

## 🚀 Usage

### Running the Test
```bash
# Using the test runner
python run_e2e.py clean

# Or specific test execution
python -m tests.simulation.helpers.test_runner \
  tests/simulation/fixtures/e2e_all_traversal/test_case.json
```

### Integration in CI/CD
```yaml
# GitHub Actions example
- name: Run E2E All Traversal Test
  run: |
    python run_e2e.py clean
```

## ✅ Success Criteria

The test passes when:
1. [x] Completion reason is "completed"
2. [x] All 14 key events occur in correct order
3. [x] Step count falls within 12-30 range
4. [x] Minimum 6 nodes visited
5. [x] Minimum 4 restore operations performed
6. [x] No error/exception/failure/crash events
7. [x] Depth-first navigation order maintained

## ❌ Failure Analysis

### Common Failure Points
- **Simulation stops early**: Check GraphTraversalEngine implementation
- **Events out of order**: Verify state machine transitions
- **Missing restore ops**: Check MockActionExecutor restore logic
- **Path mapping errors**: Verify MockVisionService path integration

### Debug Steps
1. Use `run_e2e.py detailed` for full I/O analysis
2. Check actual vs expected trace in `expected_trace.json`
3. Verify PageAnalysis parsing in MockVisionService
4. Validate dynamic rule matching in GraphTraversalEngine

## 🔒 Golden Standard

This test set is locked as the **golden standard**. Any test failures MUST be resolved by:
- Fixing state machine implementation
- Correcting executor logic
- Updating vision service integration
- Improving trace matching algorithms

**DO NOT** modify these test files to make tests pass.

## 📈 Metrics

### Performance Targets
- **Execution Time**: <5 seconds per test
- **Memory Usage**: <100MB per test
- **Success Rate**: 100% (when implementation is correct)

### Coverage
- **Nodes**: 4 pages × 7 elements = 28 test points
- **Transitions**: 6 navigation transitions
- **Restore Ops**: 4 restore operations
- **Total Validation Points**: 38 checks

## 🔗 Related Documentation

- [PRD V6.0](../../../../../docs/PRD_V6_0-simulation-testing.md)
- [Simulation Testing Guide](../../../../../docs/SIMULATION_TESTING_GUIDE.md)
- [Architecture](../../../../../docs/ARCHITECTURE.md)
- [Test Framework README](../../README.md)

## 📝 Version History

- **v1.0** (2026-06-03): Initial golden standard implementation
  - Dynamic matching strategy
  - PageAnalysis format
  - Expected trace validation
  - Restore operation verification

---

**Maintained by**: Uni-Claw Simulation Testing Team
**Status**: ✅ Golden Standard - Do not modify without explicit approval