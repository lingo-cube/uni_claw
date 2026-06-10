# Tasks: V6.15.0 State Machine Test Migration

## Phase 0: Data Collection (CRITICAL - Must Complete First)

### 0.0 Establish Failure Classification Taxonomy

**Task**: Create unified failure classification standard before analyzing specific tests.

**Definition of Done**:
- [x] Document failure modes: mock_setup, test_design, product_logic, coverage_gap, timing, state_management
- [x] Create classification decision tree
- [x] Document in Phase 0 appendix

**Status**: ✅ COMPLETE
**Document**: `docs/phase_0_failure_classification_taxonomy.md` (20KB, comprehensive taxonomy with decision tree, classification algorithm, and fix strategies)

**Commands**:
```bash
# Create classification document
cat > temp/failure_classification.md << 'EOF'
# Failure Mode Classification

## Mock Setup Problem
- Fixture missing required fields
- Incomplete nested structure initialization

## Test Design Problem  
- Variable name errors
- Incorrect assertion placement/timing

## Product Logic Change
- API changed in production code
- Architecture refactored behavior

## Coverage Gap
- Missing edge case tests
- Boundary conditions not covered

## Timing Issue
- Async operation timing errors
- Event synchronization issues

## State Management Issue
- Global state pollution
- State leakage between tests
EOF
```

---

### 0.0.b Verify pytest Environment

**Task**: Ensure pytest is available and tests can run.

**Definition of Done**:
- [ ] pytest version recorded
- [ ] Test collection successful
- [ ] Tests can execute (no import errors)
- [ ] Platform compatibility verified (Windows/Git Bash)

**Commands**:
```bash
# Verify pytest
pytest --version

# Collect tests
pytest tests/v6/test_state_machine_intelligence.py --collect-only -q

# Verify executable (Git Bash compatible)
pytest tests/v6/test_state_machine_intelligence.py -v > /dev/null 2>&1 && echo "Tests executable"
```

**Record in Appendix 9.1**: pytest version, test count, collection timestamp

---

### 0.1 Code Static Analysis

**Task**: Verify state machine implementation and exception handling.

**Definition of Done**:
- [ ] All `_handle_*_state` methods documented
- [ ] Exception handling mechanisms verified
- [ ] TraversalStateMachine class structure recorded
- [ ] Key code snippets in Appendix 9.1

**Commands**:
```bash
# State transition logic
rg "def _handle_.*_state" src/state_machine/traversal_fsm.py -A 20 > temp/state_handlers.txt

# Exception handling
rg "except Exception" src/state_machine/traversal_fsm.py -B 5 -A 10 > temp/exception_handling.txt

# Class structure
rg "class TraversalStateMachine" src/state_machine/ -A 30 > temp/state_machine_class.txt
```

---

### 0.2 Architecture Verification

**Task**: Trace call chain to confirm StepOrchestrator ↔ TraversalStateMachine relationship.

**Definition of Done**:
- [ ] StepOrchestrator → TraversalStateMachine call pattern documented
- [ ] TraversalStateMachine transition entry point verified
- [ ] Architecture diagram in Appendix 9.2

**Commands**:
```bash
# StepOrchestrator calls state machine
rg "state_machine\." src/traversal/step_orchestrator.py -B 3 -A 3 > temp/orchestrator_calls.txt

# State machine transition entry
rg "def transition" src/state_machine/traversal_fsm.py -A 10 > temp/transition_entry.txt
```

---

### 0.2.b Fixture Structure Analysis

**Task**: Analyze and document TraversalRuntimeContext vs current fixture.

**Definition of Done**:
- [ ] TraversalRuntimeContext complete attribute list (20+ fields)
- [ ] Current fixture attribute list (~7 fields)
- [ ] Missing fields gap analysis
- [ ] "Complete context mock" standard defined
- [ ] Fixture location and structure verified

**Commands**:
```bash
# Context class definition
rg "class TraversalRuntimeContext" src/trace/context.py -A 50 > temp/context_definition.txt

# Count fields
rg -c "^\s+\w+:" src/trace/context.py | head -20 > temp/field_count.txt

# Fixture definitions in test file
rg "@pytest.fixture" tests/v6/test_state_machine_intelligence.py -A 10 > temp/test_fixtures.txt

# Key fixtures
rg "def (sample_context|mock_stack|mock_action|sample_container_node)" tests/v6/test_state_machine_intelligence.py -A 15 > temp/key_fixtures.txt

# Verify conftest.py
cat tests/v6/conftest.py 2>/dev/null || echo "conftest.py not found (expected)"
```

**Define Standard**:
```python
# Minimum required fields for state machine tests:
REQUIRED_FIELDS = [
    'current_path',      # Current traversal path
    'context_tree',      # UI hierarchy tree
    'node_stack',        # Backtrack stack
    'failed_nodes',      # Failed node tracking with retry_count
]

# Test-specific fields as needed:
TEST_SPECIFIC_FIELDS = [
    'current_page_analysis',  # For FRAME_COMPLETE tests
    'visited_children',       # For unvisited node tests
]
```

---

### 0.3 Test Run Data Collection

**Task**: Collect full failure logs for all 8 failing tests.

**Definition of Done**:
- [ ] Full test run with verbose output captured
- [ ] Individual failure logs for each test
- [ ] Stack traces analyzed and categorized

**Commands**:
```bash
# Full test run
mkdir -p temp
pytest tests/v6/test_state_machine_intelligence.py -v --tb=long > temp/test_failures.log 2>&1

# Individual failures (for detailed analysis)
pytest tests/v6/test_state_machine_intelligence.py::test_deeper_executes_back -vv --tb=long > temp/test_3_1.log 2>&1
pytest tests/v6/test_state_machine_intelligence.py::test_auto_escape_clicks_unvisited_menu -vv --tb=long > temp/test_3_2.log 2>&1
pytest tests/v6/test_state_machine_intelligence.py::test_auto_escape_fallback_to_back_when_no_unvisited -vv --tb=long > temp/test_3_3.log 2>&1
pytest tests/v6/test_state_machine_intelligence.py::test_retry_with_remaining_retries -vv --tb=long > temp/test_3_4.log 2>&1
pytest tests/v6/test_state_machine_intelligence.py::test_backtrack_pops_stack -vv --tb=long > temp/test_3_5.log 2>&1
pytest tests/v6/test_state_machine_intelligence.py::test_abort_sets_terminated -vv --tb=long > temp/test_3_6.log 2>&1
pytest tests/v6/test_state_machine_intelligence.py::test_catches_handler_exception_and_routes_to_error_handling -vv --tb=long > temp/test_3_7.log 2>&1
pytest tests/v6/test_state_machine_intelligence.py::test_preserves_error_type_in_metadata -vv --tb=long > temp/test_3_8.log 2>&1
```

**Record in Appendix 9.1**: Categorized failures using taxonomy from 0.0

---

### 0.4 Coverage Baseline

**Task**: Establish coverage baseline before any fixes.

**Definition of Done**:
- [ ] Coverage report generated
- [ ] Baseline percentage recorded
- [ ] Missing lines documented

**Commands**:
```bash
# Coverage report
pytest tests/v6/test_state_machine_intelligence.py --cov=src/state_machine --cov-report=term-missing --cov-report=html:temp/coverage_baseline > temp/coverage_baseline.txt 2>&1

# Extract key metrics
grep -E "(TOTAL|src/state_machine)" temp/coverage_baseline.txt
```

**Record in Appendix 9.1**: Total coverage, line coverage, branch coverage, missing lines

---

### 0.5 Duration Baseline

**Task**: Record test suite runtime baseline.

**Definition of Done**:
- [ ] Total runtime recorded
- [ ] Per-test timing data collected
- [ ] Slowest tests identified

**Commands**:
```bash
# Duration baseline
pytest tests/v6/test_state_machine_intelligence.py --durations=0 > temp/duration_baseline.txt 2>&1
```

**Record in Appendix 9.1**: Total seconds, per-test average, slowest tests

---

### 0.7 Specialized Test Data Collection (Tests 3.5, 3.6, 3.7)

**Task**: Collect detailed logs for pending analysis tests.

**Definition of Done**:
- [ ] `test_backtrack_pops_stack` detailed log
- [ ] `test_abort_sets_terminated` detailed log
- [ ] `test_catches_handler_exception` detailed log
- [ ] All categorized using failure taxonomy

**Commands**:
```bash
# Already collected in 0.3, verify and analyze
# Focus on categorization using taxonomy from 0.0
```

**After Phase 0 Complete**:
- [ ] All failure modes classified
- [ ] Data collection appendix (9.1) complete
- [ ] Ready to proceed to fixes

---

## Phase 1: Simple Fix (Low Risk)

### 1.1 Fix test_deeper_executes_back

**Task**: Fix variable name error on line 229.

**Definition of Done**:
- [ ] Change `action` to `mock_action` on line 229
- [ ] Verify no other instances of incorrect variable name
- [ ] Test passes

**Commands**:
```bash
# Find the line
rg -n "handler.execute\(action" tests/v6/test_state_machine_intelligence.py

# Verify fix
rg -n "handler.execute\(mock_action" tests/v6/test_state_machine_intelligence.py

# Run test
pytest tests/v6/test_state_machine_intelligence.py::test_deeper_executes_back -v
```

---

## Phase 2: FrameCompleteHandler Fixes (Medium Risk)

### 2.1 Fix test_auto_escape_clicks_unvisited_menu

**Task**: Add complete context mock for FRAME_COMPLETE state test.

**Definition of Done**:
- [ ] Fixture enhanced with all required fields
- [ ] `current_page_analysis` properly mocked
- [ ] `node_stack` initialized
- [ ] `failed_nodes` initialized
- [ ] Test passes

**Implementation**:
```python
# In test fixture or test setup
@pytest.fixture
def frame_complete_context():
    context = Mock(spec=TraversalRuntimeContext)
    # Required fields
    context.current_page_analysis = Mock()
    context.node_stack = []
    context.failed_nodes = {}
    # ... all required fields from Phase 0.2.b analysis
    return context

# Update test to use enhanced fixture
def test_auto_escape_clicks_unvisited_menu(frame_complete_context):
    # Use frame_complete_context instead of incomplete mock
    ...
```

**Verification**:
```bash
pytest tests/v6/test_state_machine_intelligence.py::test_auto_escape_clicks_unvisited_menu -v
```

---

### 2.2 Fix test_auto_escape_fallback_to_back_when_no_unvisited

**Task**: Same as 2.1 - add complete context mock.

**Definition of Done**:
- [ ] Uses same enhanced fixture as 2.1
- [ ] Test passes

**Verification**:
```bash
pytest tests/v6/test_state_machine_intelligence.py::test_auto_escape_fallback_to_back_when_no_unvisited -v
```

---

## Phase 3: ErrorHandler Fixes (Medium Risk)

### 3.1 Fix test_retry_with_remaining_retries

**Task**: Add `retry_count` to `failed_nodes` setup.

**Definition of Done**:
- [ ] `failed_nodes` structure includes `retry_count`
- [ ] `retry_count < max_retries` (default 3)
- [ ] Test returns EXECUTE state

**Implementation**:
```python
# In test setup
context.failed_nodes = {
    'test_node': {
        'retry_count': 0,  # Critical: must be < max_retries
        'last_error': Exception("test error")
    }
}
```

**Verification**:
```bash
pytest tests/v6/test_state_machine_intelligence.py::test_retry_with_remaining_retries -v
```

---

### 3.2 Analyze and Fix test_backtrack_pops_stack

**Task**: Analyze actual backtrack implementation, fix accordingly.

**Definition of Done**:
- [ ] Backtrack implementation verified via code analysis
- [ ] Root cause identified using Phase 0.7 log
- [ ] Test fixed or properly marked (xfail if product bug)
- [ ] Test passes or correctly marked

**Commands**:
```bash
# Verify backtrack implementation
rg "def.*backtrack" src/state_machine/traversal_fsm.py -A 20

# Check for stack.pop usage
rg "stack\.pop\(\)" src/state_machine/ -B 3 -A 3

# Use Phase 0.7 log to determine actual issue
```

---

### 3.3 Analyze and Fix test_abort_sets_terminated

**Task**: Analyze termination conditions, fix accordingly.

**Definition of Done**:
- [ ] Termination condition logic verified
- [ ] Root cause identified using Phase 0.7 log
- [ ] Test fixed or properly marked
- [ ] Test passes or correctly marked

**Commands**:
```bash
# Verify termination logic
rg "def.*terminated" src/state_machine/traversal_fsm.py -A 20
rg "TERMINATED" src/state_machine/traversal_fsm.py -B 2 -A 5

# Use Phase 0.7 log to determine actual issue
```

---

## Phase 4: StepExceptionHandling Fixes (Medium Risk)

### 4.1 Fix test_catches_handler_exception_and_routes_to_error_handling

**Task**: Verify exception triggering, fix if needed.

**Definition of Done**:
- [ ] Exception trigger mechanism verified
- [ ] `_last_error` recording confirmed
- [ ] Test passes or properly marked

**Commands**:
```bash
# Verify exception handling
rg "_last_error" src/state_machine/traversal_fsm.py -B 5 -A 5

# Verify exception capture in handlers
rg "except Exception" src/state_machine/traversal_fsm.py -B 5 -A 10 | grep -A 10 "_last_error"
```

---

### 4.2 Fix test_preserves_error_type_in_metadata

**Task**: Analyze dataclass behavior, adjust verification timing.

**Definition of Done**:
- [ ] TraversalStateTransition dataclass definition verified
- [ ] `metadata` field type confirmed (default_factory vs plain dict)
- [ ] `transition_to()` call pattern analyzed
- [ ] Test assertion timing adjusted if needed
- [ ] Test passes

**Commands**:
```bash
# Verify dataclass definition
rg "class TraversalStateTransition" src/state_machine/ -A 20

# Check metadata field definition
rg "metadata.*field" src/state_machine/ -A 5

# Verify transition_to usage
rg "transition_to\(" src/state_machine/ -B 3 -A 3
```

**If dataclass uses `field(default_factory=dict)`**:
- Metadata is new dict per instance
- Mutation should affect original object
- Test may be checking wrong timing

**If transition creates copy**:
- Need to access metadata after exception handling
- May need to check `_last_error` or other mechanism

---

## Phase 5: Coverage Verification (Required)

### 5.1 Verify Coverage Baseline

**Task**: Ensure coverage hasn't decreased.

**Definition of Done**:
- [ ] Coverage report generated
- [ ] Compared against Phase 0.4 baseline
- [ ] No coverage regression
- [ ] Gaps identified if any

**Commands**:
```bash
# Generate coverage report
pytest tests/v6/test_state_machine_intelligence.py --cov=src/state_machine --cov-report=term-missing --cov-report=html:temp/coverage_final > temp/coverage_final.txt 2>&1

# Compare baselines
diff temp/coverage_baseline.txt temp/coverage_final.txt || echo "Coverage changed"
```

---

### 5.2 State Transition Path Coverage

**Task**: Verify all state transition paths are covered.

**Definition of Done**:
- [ ] State transition diagram complete (Appendix 9.2)
- [ ] Each transition path mapped to test(s)
- [ ] All paths have at least one passing test
- [ ] Coverage report complete (Appendix 9.3)

**State Paths to Verify**:
1. EXECUTE → NODE_SELECT (success)
2. EXECUTE → ERROR_HANDLING (failure)
3. ERROR_HANDLING → EXECUTE (retry)
4. ERROR_HANDLING → NODE_SELECT (no retry)
5. FRAME_COMPLETE → NODE_SELECT (unvisited)
6. FRAME_COMPLETE → BACKTRACK (no unvisited)
7. BACKTRACK → NODE_SELECT (stack not empty)
8. BACKTRACK → TERMINATED (stack empty)

**Commands**:
```bash
# Count state handler methods
rg "def _handle_.*_state" src/state_machine/traversal_fsm.py | wc -l

# Analyze state enum
rg "class State" src/state_machine/traversal_fsm.py -A 20
```

---

## Phase 6: Test Helper Layer (Future Enhancement)

### 6.1 Analyze Existing Helper Layer

**Task**: Review V6.14.0 `api_migration_helper.py` for reusable patterns.

**Definition of Done**:
- [ ] Existing helper functions catalogued
- [ ] Identify gaps for state machine testing
- [ ] Decision: extend vs create new

**Commands**:
```bash
# Review existing helpers
cat tests/v6/helpers/api_migration_helper.py

# Catalog helper functions
rg "^def " tests/v6/helpers/api_migration_helper.py
```

---

### 6.2 Design State Machine Helper

**Task**: Design helper functions for state machine testing.

**Definition of Done**:
- [ ] Helper function requirements defined
- [ ] API design documented
- [ ] Decision made: extend api_migration_helper.py or create state_machine_helper.py

**Helper Functions to Consider**:
```python
# State transition verification
def assert_state_transition(test_context, expected_state, message=""):
    """Verify state machine transitioned to expected state."""
    ...

# Test context builder
def build_test_context(required_fields=None, test_specific_fields=None):
    """Build complete TraversalRuntimeContext mock."""
    ...

# Transition history tracker
class TransitionTracker:
    """Track and verify state transition sequences."""
    ...
```

---

### 6.3 Implement Helper Layer

**Task**: Implement helper functions.

**Definition of Done**:
- [ ] Helper functions implemented
- [ ] Documentation complete
- [ ] Examples provided
- [ ] Existing tests updated to use helpers (optional)

**Commands**:
```bash
# Create helper file if needed
touch tests/v6/helpers/state_machine_helper.py

# Or extend existing
# Edit tests/v6/helpers/api_migration_helper.py
```

---

## Verification Checklist

### Before Marking Complete

- [ ] All 8 tests analyzed and categorized
- [ ] Phase 0 data collection complete
- [ ] All tests pass or properly marked (xfail with reason)
- [ ] Coverage ≥ Phase 0 baseline
- [ ] State transition paths verified
- [ ] Documentation complete (Appendices 9.1, 9.2, 9.3)

### Final Verification Command

```bash
# Run full test suite
pytest tests/v6/test_state_machine_intelligence.py -v

# Coverage check
pytest tests/v6/test_state_machine_intelligence.py --cov=src/state_machine --cov-report=term-missing

# Verify no regressions
pytest tests/v6/ -v
```

---

## Appendix Reference

- **9.1**: Baseline data (pytest version, fixture analysis, test failures, coverage, duration)
- **9.2**: State transition diagram
- **9.3**: Final coverage report
