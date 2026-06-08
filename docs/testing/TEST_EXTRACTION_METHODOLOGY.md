# Test Scenario Extraction Methodology

> **Quick Reference**: Systematic approach to extracting comprehensive test scenarios from design documents

---

## The 5-Step Process

### Step 1: Locate Design Documents

```
docs/architecture/modules/{module}-design.md
docs/architecture/concepts/{concept}-design.md
src/{module}/README.md
```

### Step 2: Identify Test Dimensions

| Dimension | What to Look For | Example |
|-----------|------------------|---------|
| **States** | State definitions, enums | GlobalState: IDLE, TRAVERSING, COMPLETED |
| **Transitions** | State transition tables | IDLE → TRAVERSING on start_traversal |
| **Inputs/Outputs** | API signatures | `def process(frame: Frame) -> Result` |
| **Boundaries** | Limits, thresholds | max_depth=50, timeout=5s |
| **Errors** | Error types, policies | RETRY, SKIP, BACKTRACK, ABORT |
| **Invariants** | "Must always", "Never" | Stack depth never exceeds max |
| **Features** | V6 enhancements, flags | POPUP_HANDLING, AUTO_ESCAPE |

### Step 3: Create Test Matrix

For each dimension, create a table:

| Test ID | Scenario | Input | Expected Output | Validation |
|---------|----------|-------|-----------------|------------|
| TEST-001 | Normal operation | valid input | success result | output == expected |

### Step 4: Categorize Tests

```
tests/{module}/
├── test_normal_flow.py      # Happy path
├── test_edge_cases.py       # Boundaries
├── test_errors.py           # Error scenarios
├── test_integration.py      # With dependencies
└── test_properties.py       # Invariant rules
```

### Step 5: Estimate Coverage

| Coverage Type | How to Measure | Target |
|---------------|----------------|--------|
| State coverage | States tested / Total states | 100% |
| Transition coverage | Transitions tested / Total | 100% |
| Boundary coverage | Boundaries tested / Known | 95%+ |
| Error coverage | Error types tested / Total | 100% |

---

## Quick Checklist

For any module, verify you have tests for:

- [ ] All public methods (at least 1 test each)
- [ ] All state transitions (if state machine)
- [ ] All boundary values (min, max, empty)
- [ ] All error conditions (null, invalid, timeout)
- [ ] All invariants (properties that must hold)
- [ ] Integration with dependencies
- [ ] Version-specific features (V6, V6.5, etc.)

---

## Example Application

For a new module `Foo`:

1. **Find design**: `docs/architecture/modules/foo-design.md`
2. **Extract dimensions**:
   - States: [INIT, RUNNING, DONE]
   - API: `start()`, `stop()`, `reset()`
   - Limits: `max_retries=3`
3. **Create matrix**:
   - FOO-001: INIT → RUNNING on start()
   - FOO-002: RUNNING → DONE on complete()
   - FOO-003: Retry 3 times then abort
4. **Generate tests**: Write test_foo.py with 15+ scenarios

---

**Related**: See STATE_MACHINE_TEST_SCENARIOS.md for full example
**Usage**: Apply this methodology to any module needing test coverage improvement
