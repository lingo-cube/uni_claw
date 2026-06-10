# Failure Mode Classification Taxonomy
## Phase 0.0 - State Machine Test Migration

> **Purpose**: Establish unified failure classification standard before analyzing specific tests
> **Version**: 1.0
> **Created**: 2026-06-10
> **Change**: state-machine-test-migration

---

## Overview

This taxonomy provides a standardized framework for categorizing test failures in the V6.15.0 State Machine Test Migration. Each failure mode has distinct characteristics, root causes, and fix strategies.

---

## Decision Tree

```
                    ┌─────────────────────┐
                    │  Test Failure?      │
                    └──────────┬──────────┘
                               │
                ┌──────────────┴──────────────┐
                │                             │
         ┌──────▼──────┐              ┌───────▼───────┐
         │ Static Error?│              │ Runtime Error?│
         └──────┬──────┘              └───────┬───────┘
                │                             │
         ┌──────▼──────┐              ┌───────▼───────┐
         │ TEST_DESIGN │              │ Check Context  │
         │   Problem   │              └───────┬───────┘
         └─────────────┘                       │
                                     ┌─────────┴─────────┐
                                     │                   │
                              ┌──────▼──────┐     ┌──────▼──────┐
                              │ Mock Setup  │     │ Product     │
                              │   Problem   │     │ Logic       │
                              └─────────────┘     └──────┬──────┘
                                                        │
                                         ┌──────────────┼──────────────┐
                                         │              │              │
                                  ┌──────▼──────┐ ┌────▼────┐ ┌─────▼─────┐
                                  │ Coverage    │ │ Timing  │ │ State     │
                                  │ Gap         │ │ Issue   │ │ Management│
                                  └─────────────┘ └─────────┘ └───────────┘
```

---

## Failure Mode Details

### 1. Mock Setup Problem

**Category**: Test Infrastructure
**Severity**: Medium
**Fix Complexity**: Low-Medium

#### Description
Test mock objects are incomplete, missing required fields, or improperly configured. The production code attempts to access attributes or methods that don't exist in the mock, causing runtime errors or unexpected behavior.

#### Root Causes
- Fixture missing required fields
- Incomplete nested structure initialization
- Mock spec doesn't match actual interface
- Required attributes not set on Mock objects
- Nested objects (dicts, lists) not properly initialized

#### Detection Pattern
```python
# Symptoms:
AttributeError: Mock object has no attribute 'X'
TypeError: 'Mock' object is not subscriptable
AssertionError: Expected STATE_A but got ERROR_HANDLING

# Code Pattern:
mock_object = Mock()
# Missing: mock_object.required_field = Mock()
```

#### Typical Locations
- Test fixtures (`@pytest.fixture`)
- Test setup methods (`setUp`, `setup_method`)
- Mock object initialization
- Context object construction

#### Fix Strategy
1. Identify all attributes accessed by production code
2. Add missing attributes to mock objects
3. Initialize nested structures (dicts, lists)
4. Use `spec` parameter to match interface
5. Create comprehensive fixture builders

#### Example
```python
# Before (Incomplete):
context = Mock()
context.node_stack = []

# After (Complete):
context = Mock(spec=TraversalRuntimeContext)
context.current_path = Mock()
context.context_tree = Mock()
context.node_stack = []
context.failed_nodes = {}
context.current_page_analysis = Mock()
# ... all 20+ fields
```

#### Related Tests (V6.15.0)
- test_auto_escape_clicks_unvisited_menu
- test_auto_escape_fallback_to_back_when_no_unvisited
- test_retry_with_remaining_retries

#### Verification
```bash
# Run single test with full verbosity
pytest tests/v6/test_state_machine_intelligence.py::test_name -vv --tb=long

# Check attribute access in production code
rg "context\.\w+" src/state_machine/traversal_fsm.py | sort -u
```

---

### 2. Test Design Problem

**Category**: Test Implementation
**Severity**: Low
**Fix Complexity**: Low

#### Description
Test code contains logical errors, incorrect assertions, or uses wrong variable names. These are bugs in the test itself, not in the production code.

#### Root Causes
- Variable name typos
- Incorrect assertion logic
- Wrong test expectations
- Off-by-one errors in assertions
- Incorrect test setup order

#### Detection Pattern
```python
# Symptoms:
NameError: name 'action' is not defined
AssertionError: Expected X but got Y
Test passes but assertions don't actually verify behavior

# Code Pattern:
result = handler.execute(action, context)  # 'action' undefined
assert result == expected  # Wrong expectation
```

#### Typical Locations
- Test method bodies
- Assertion statements
- Test logic flow
- Variable usage within tests

#### Fix Strategy
1. Review test code for typos
2. Verify variable names match fixtures
3. Check assertion logic matches expected behavior
4. Ensure test actually verifies what it claims
5. Add assertions at correct verification points

#### Example
```python
# Before (Wrong variable):
result = handler.execute(action, context)  # NameError

# After (Correct variable):
result = handler.execute(mock_action, context)  # Works
```

#### Related Tests (V6.15.0)
- test_deeper_executes_back (line 229)

#### Verification
```bash
# Run test to see specific error
pytest tests/v6/test_state_machine_intelligence.py::test_deeper_executes_back -v

# Search for variable usage
rg -n "handler\.execute" tests/v6/test_state_machine_intelligence.py
```

---

### 3. Product Logic Change

**Category**: Product Code
**Severity**: High
**Fix Complexity**: Medium-High

#### Description
Production code behavior has changed due to API modifications, architecture refactoring, or intentional behavior changes. Test assumptions are no longer valid.

#### Root Causes
- API signature changes
- Architecture refactoring (V6.11.0 StepOrchestrator)
- New error handling mechanisms
- State transition logic modifications
- Configuration/policy changes
- Feature deprecation or removal

#### Detection Pattern
```python
# Symptoms:
AssertionError: Expected behavior matches old API
Test passes old API signature but new API differs
Documentation mismatch with implementation
Test verifies behavior that's intentionally changed

# Code Pattern:
# Old: service.process(data)
# New: service.process(data, context=ctx)
```

#### Typical Locations
- API boundaries (method signatures, parameters)
- State transition logic
- Error handling flows
- Configuration-dependent behavior

#### Fix Strategy
1. Verify change is intentional (not regression)
2. Update test to match new API
3. Add documentation if behavior change is significant
4. Use `pytest.mark.xfail` if product bug discovered
5. Consider backward compatibility if needed

#### Example
```python
# Before (Old API):
state = machine.transition(context)

# After (New API):
state = machine.transition(context, transition_type=TransitionType.STATE_CHANGE)

# Test update:
def test_new_behavior():
    state = machine.transition(context, transition_type=TransitionType.STATE_CHANGE)
    assert state == State.EXECUTE
```

#### Related Tests (V6.15.0)
- Possibly: test_backtrack_pops_stack (pending Phase 0.7 analysis)
- Possibly: test_abort_sets_terminated (pending Phase 0.7 analysis)

#### Verification
```bash
# Check API documentation
rg "def transition" src/state_machine/traversal_fsm.py -A 10

# Compare with test expectations
rg "machine\.transition" tests/v6/test_state_machine_intelligence.py -B 2 -A 2
```

#### Decision Framework
- **Fix test**: If change is intentional and documented
- **Mark xfail**: If change is a product bug
- **Delete test**: If feature is removed and no replacement exists

---

### 4. Coverage Gap

**Category**: Test Quality
**Severity**: Medium
**Fix Complexity**: Medium

#### Description
Test suite misses edge cases, boundary conditions, or error paths. Tests may pass normal scenarios but fail when encountering unusual inputs or states.

#### Root Causes
- Only happy path tested
- Missing edge case coverage
- Boundary conditions not tested
- Error paths unverified
- State transitions not fully covered

#### Detection Pattern
```python
# Symptoms:
Code coverage < 100%
Specific branch not executed
Error handling not tested
Edge case causes failure

# Code Pattern:
# Test: assert process(5) == 10  (Normal case)
# Missing: assert process(0) handles zero
# Missing: assert process(-1) handles negative
```

#### Typical Locations
- Conditional branches (if/else)
- Loop boundaries
- Error handling paths
- State transition edges
- Input validation

#### Fix Strategy
1. Run coverage analysis to identify gaps
2. Add tests for uncovered branches
3. Test boundary conditions (0, -1, max, min)
4. Verify error handling paths
5. Add state transition edge case tests

#### Example
```python
# Existing (Happy path only):
def test_retry_success():
    """Test retry succeeds on second attempt."""
    # Tests retry_count = 1, max_retries = 3

# Added (Boundary case):
def test_retry_exhausted():
    """Test retry fails when max_retries reached."""
    # Tests retry_count = 3, max_retries = 3

# Added (Edge case):
def test_retry_with_zero_max_retries():
    """Test retry when policy max_retries = 0."""
    # Tests edge case: no retries allowed
```

#### Related Tests (V6.15.0)
- May be discovered during Phase 5 coverage verification

#### Verification
```bash
# Generate coverage report
pytest tests/v6/test_state_machine_intelligence.py --cov=src/state_machine --cov-report=term-missing

# Check branch coverage
pytest tests/v6/test_state_machine_intelligence.py --cov=src/state_machine --cov-branch --cov-report=term-missing

# Identify missing lines
grep -A 20 "TOTAL" temp/coverage_baseline.txt
```

---

### 5. Timing Issue

**Category**: Concurrency/Async
**Severity**: Medium
**Fix Complexity**: Medium

#### Description
Tests fail due to timing dependencies, race conditions, or incorrect synchronization with asynchronous operations.

#### Root Causes
- Async operations not awaited
- Event-driven code not synchronized
- Race conditions between threads/processes
- Timing assumptions (sleep, timeout)
- Event order dependencies

#### Detection Pattern
```python
# Symptoms:
Flaky tests (pass/fail inconsistently)
Timeout errors
Event not received assertions
Tests pass with sleep but fail without

# Code Pattern:
# Test:
async_op()
assert result == expected  # Result not ready yet

# Fix:
await async_op()
assert result == expected
```

#### Typical Locations
- Async/await code
- Event handlers
- Callback-driven code
- Thread synchronization
- Timeout/polling logic

#### Fix Strategy
1. Add explicit waits/synchronization
2. Use proper async/await patterns
3. Add event synchronization primitives
4. Eliminate race conditions in test setup
5. Use polling with timeout instead of sleep

#### Example
```python
# Before (Race condition):
def test_state_update():
    machine.transition(context)
    assert context.state == State.EXECUTE  # May not be updated yet

# After (Synchronized):
def test_state_update():
    machine.transition(context)
    wait_for_state(context, State.EXECUTE, timeout=1.0)
    assert context.state == State.EXECUTE

# Helper:
def wait_for_state(context, expected_state, timeout=1.0):
    start = time.time()
    while context.state != expected_state:
        if time.time() - start > timeout:
            raise TimeoutError(f"State not reached: {expected_state}")
        time.sleep(0.01)
```

#### Related Tests (V6.15.0)
- Potentially: test_catches_handler_exception_and_routes_to_error_handling (pending analysis)

#### Verification
```bash
# Run test multiple times to detect flakiness
for i in {1..10}; do
  pytest tests/v6/test_state_machine_intelligence.py::test_name -v
done

# Check for timing-related code
rg "sleep\|timeout\|wait" tests/v6/test_state_machine_intelligence.py
```

---

### 6. State Management Issue

**Category**: Test Isolation
**Severity**: Medium
**Fix Complexity**: Medium

#### Description
Tests interfere with each other through shared state, global variables, or improper cleanup. Tests may pass in isolation but fail when run in suites.

#### Root Causes
- Global state pollution
- Class-level state not reset
- Singleton pattern state leakage
- Database/filesystem not cleaned up
- Module-level variables modified

#### Detection Pattern
```python
# Symptoms:
Test passes alone but fails in suite
Test order-dependent failures
AssertionError: Expected X but got Y (where Y is from previous test)
State inconsistencies between tests

# Code Pattern:
# Test A:
def test_add_item():
    global_cache.add("item")
    assert "item" in global_cache

# Test B (fails if run after A):
def test_empty_cache():
    assert len(global_cache) == 0  # False! Contains "item"
```

#### Typical Locations
- Global variables
- Class attributes
- Module-level state
- Singleton objects
- External resources (DB, files)

#### Fix Strategy
1. Reset state in setUp/tearDown
2. Use fixtures with proper scope
3. Avoid global state in tests
4. Clean up external resources
5. Use isolation patterns (fresh instances per test)

#### Example
```python
# Before (State leakage):
class TestStateMachine:
    machine = None

    def test_case_1(self):
        self.machine = TraversalStateMachine()
        self.machine.state = State.EXECUTE

    def test_case_2(self):
        # FAILS: machine.state is still EXECUTE from test_case_1
        assert self.machine.state == State.INITIAL

# After (Proper isolation):
class TestStateMachine:
    def setUp(self):
        self.machine = TraversalStateMachine()

    def test_case_1(self):
        self.machine.state = State.EXECUTE

    def test_case_2(self):
        # PASSES: Fresh instance
        assert self.machine.state == State.INITIAL
```

#### Related Tests (V6.15.0)
- Potentially any test that modifies global state or singletons

#### Verification
```bash
# Run tests in isolation
pytest tests/v6/test_state_machine_intelligence.py::test_name -v

# Run full suite and compare
pytest tests/v6/test_state_machine_intelligence.py -v

# Check for global state usage
rg "^[A-Z_]+\s*=" src/state_machine/ | grep -v "class\|def\|const"
```

---

## Classification Algorithm

When analyzing a test failure, follow these steps:

### Step 1: Error Type Analysis
```python
if error_type in [NameError, SyntaxError]:
    return TEST_DESIGN_PROBLEM

elif error_type == AttributeError:
    if "Mock" in str(error):
        return MOCK_SETUP_PROBLEM
    else:
        return PRODUCT_LOGIC_CHANGE

elif error_type == AssertionError:
    analyze_expectation_vs_reality()

elif error_type in [TimeoutError, AsyncTimeout]:
    return TIMING_ISSUE

else:
    analyze_runtime_context()
```

### Step 2: Context Analysis
```python
def analyze_runtime_context():
    if test_passes_in_isolation_but_fails_in_suite:
        return STATE_MANAGEMENT_ISSUE

    if failure_occurs_in_async_operation:
        return TIMING_ISSUE

    if production_code_API_changed:
        return PRODUCT_LOGIC_CHANGE

    if accessed_mock_attribute_missing:
        return MOCK_SETUP_PROBLEM

    return INVESTIGATE_FURTHER
```

### Step 3: Verification
```python
def verify_classification(classification):
    if classification == MOCK_SETUP_PROBLEM:
        verify_mock_completeness()

    if classification == PRODUCT_LOGIC_CHANGE:
        verify_intentional_change()

    if classification == TEST_DESIGN_PROBLEM:
        verify_fix_is_simple()
```

---

## Application to V6.15.0 Tests

### Preliminary Classification (From PRD)

| Test | Preliminary Classification | Confidence | Notes |
|------|---------------------------|------------|-------|
| test_deeper_executes_back | TEST_DESIGN_PROBLEM | High | Simple NameError: action vs mock_action |
| test_auto_escape_clicks_unvisited_menu | MOCK_SETUP_PROBLEM | High | Missing context attributes |
| test_auto_escape_fallback_to_back_when_no_unvisited | MOCK_SETUP_PROBLEM | High | Missing context attributes |
| test_retry_with_remaining_retries | MOCK_SETUP_PROBLEM | High | Missing retry_count in failed_nodes |
| test_backtrack_pops_stack | PENDING | Low | Needs Phase 0.7 data collection |
| test_abort_sets_terminated | PENDING | Low | Needs Phase 0.7 data collection |
| test_catches_handler_exception | PENDING | Low | Needs Phase 0.7 data collection |
| test_preserves_error_type_in_metadata | TIMING_ISSUE or PRODUCT_LOGIC_CHANGE | Low | Needs dataclass behavior verification |

### Phase 0.7 Focus
Tests 3.5, 3.6, 3.7 require detailed log analysis to finalize classification:
- Collect detailed logs with `-vv --tb=long`
- Analyze stack traces for actual failure points
- Verify production code behavior matches test expectations
- Apply classification algorithm to finalize

---

## Fix Strategy Matrix

| Classification | Fix Strategy | Verification | Success Criteria |
|---------------|--------------|--------------|------------------|
| MOCK_SETUP_PROBLEM | Add missing mock attributes | Run test with verbose output | Test passes without AttributeError |
| TEST_DESIGN_PROBLEM | Fix typos, correct assertions | Run single test | Test passes, assertions correct |
| PRODUCT_LOGIC_CHANGE | Update test or mark xfail | Review documentation, verify intent | Test matches current API or properly marked |
| COVERAGE_GAP | Add missing test cases | Run coverage report | Coverage increases or reaches 100% |
| TIMING_ISSUE | Add synchronization or explicit waits | Run test multiple times | Test passes consistently |
| STATE_MANAGEMENT_ISSUE | Add cleanup/reset logic | Run in suite and isolation | Test passes in both contexts |

---

## Documentation Template

For each classified failure, document:

```markdown
### Test Name

**Classification**: [Category]
**Confidence**: [High/Medium/Low]
**Error Type**: [Exception type]
**File**: [File path]
**Line**: [Line number]

**Evidence**:
```
[Error message]
[Stack trace]
[Code snippet]
```

**Root Cause**:
[Analysis of why this failure occurred]

**Fix Strategy**:
```python
[Code change needed]
```

**Verification**:
```bash
[Commands to verify fix]
```

**Related Tests**:
[List of related tests if any]

**Notes**:
[Any additional context or pending analysis]
```

---

## Usage Guidelines

1. **Apply consistently**: Use the same classification criteria for all failures
2. **Document evidence**: Keep detailed notes on why classification was chosen
3. **Verify with data**: Use Phase 0 data collection to validate classifications
4. **Update as needed**: If new evidence emerges, reclassify and document reasoning
5. **Share learnings**: Update taxonomy if new failure patterns emerge

---

## Appendix: Related Documentation

- **PRD V6.15.0**: `docs/prd/PRD_V6_15_0_State_Machine_Test_Migration.md`
- **Tasks**: `openspec/changes/state-machine-test-migration/tasks.md`
- **Design**: `openspec/changes/state-machine-test-migration/design.md`
- **Phase 0 Appendix**: To be created in `docs/phase_0_baseline_data.md`

---

**Maintainer**: Uni-Claw Development Team
**Last Updated**: 2026-06-10
**Status**: Active - Phase 0.0 Complete
