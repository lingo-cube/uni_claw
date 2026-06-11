# Proposal: V6.15.0 State Machine Test Migration

## Why

Fix 8 state machine tests that failed after V6.11.0 engine refactor. The failures are primarily due to:
- **Test setup issues**: Missing mock context attributes, incomplete fixtures
- **Test design bugs**: Variable name errors, incorrect assertion timing
- **API changes**: State machine now uses TraversalRuntimeContext with 20+ fields vs. previous 7-field fixture

These are **not product logic changes** - the TraversalStateMachine still exists and functions correctly. The failures indicate test code needs updating to match the new architecture.

## What Changes

### Test Fixes (8 tests across 4 categories)

| Category | Tests | Root Cause | Fix Strategy |
|----------|-------|------------|--------------|
| **PreconditionHandler** | 1 | Variable name error (`action` → `mock_action`) | Simple rename |
| **FrameCompleteHandler** | 2 | Missing mock context attributes | Add complete context fixture |
| **ErrorHandler** | 3 | Missing `retry_count` in `failed_nodes` | Set up retry context properly |
| **StepExceptionHandling** | 2 | Metadata snapshot timing, exception capture timing | Adjust verification timing |

### Infrastructure Improvements

- **Fixture Enhancement**: Extend test fixtures to cover all 20+ TraversalRuntimeContext fields
- **Helper Layer**: Extend `tests/v6/helpers/api_migration_helper.py` or create `state_machine_helper.py` for state machine-specific test utilities
- **Documentation**: Document state transition paths and coverage baseline

## Capabilities

### New Capabilities

- `state-machine-test-coverage`: Comprehensive test coverage for TraversalStateMachine state transitions
- `enhanced-test-fixtures`: Complete mock fixtures matching TraversalRuntimeContext structure
- `test-helper-layer`: Reusable utilities for state machine testing (future Phase 6)

### Modified Capabilities

- `state-machine-tests`: Existing test suite restored to 100% pass rate

## Impact

- **Code Changes**: Test code only in `tests/v6/test_state_machine_intelligence.py`
- **Dependencies**: V6.13.0 (state migration) and V6.14.0 (test API migration) - both completed
- **Test Execution**: After fix, `pytest tests/v6/test_state_machine_intelligence.py` should pass 100%
- **Coverage**: Must maintain or improve coverage baseline measured in Phase 0
- **Time Estimate**: 6 phases, with Phase 0 (data collection) as critical first step
- **Risk**: Low - only test code changes, no production logic modifications

## Related Changes

- Depends on: `v6-13-0-state-migration` (completed)
- Depends on: `v6-14-0-test-api-migration` (completed)
- Related to: `v6-engine-refactor`, `v6-step-orchestrator`
