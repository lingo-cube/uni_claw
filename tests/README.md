# Tests Directory

This directory contains the test suite for the Uni-Claw Android UI traversal system.

## Directory Structure

```
tests/
├── assets/              # Test assets and utilities
│   ├── fixtures/        # Test data fixtures (JSON files)
│   └── utils/           # Reusable test utilities
├── models/              # Model tests (core business models)
│   ├── test_content_tree.py      # Page analysis models
│   ├── test_graph_nodes.py       # Graph node models
│   ├── test_state_machine.py     # State machine models
│   ├── test_context.py           # Runtime context models
│   ├── test_exception.py         # Exception handling models
│   ├── test_ai_types.py          # AI capability models
│   ├── test_trace.py             # Trace models
│   └── test_enums.py             # Enum helper methods tests
├── archive/             # Archived tests (deprecated/legacy)
├── integration/         # Integration tests
├── unit/                # Unit tests
└── conftest.py          # Pytest configuration
```

## Test Categories

### Model Tests (`tests/models/`)

Comprehensive tests for all core business models:

- **Content Tree Models** (`test_content_tree.py`): Tests for `Coordinate`, `MenuInfo`, `MenuItem`, `PopupInfo`, `PageAnalysis`, `ContentNode`, `ContentTree`, `VisitFingerprint`, and `TraversalState`
- **Graph Node Models** (`test_graph_nodes.py`): Tests for `Target`, `RestoreAction`, `Operation`, `Precondition`, `DynamicRule`, `ChildrenStrategy`, `ErrorPolicy`, `TraversalNode`, and enum types
- **State Machine Models** (`test_state_machine.py`): Tests for `GlobalStateMachine`, `TraversalStateMachine`, `StackFrame`, `NodeStack`, and related enums
- **Context Models** (`test_context.py`): Tests for `TraversalContext`, `ErrorRecord`, and `ActionRecord`
- **Exception Models** (`test_exception.py`): Tests for exception types, `ExceptionContext`, and `ExceptionHandlingResult`
- **AI Types Models** (`test_ai_types.py`): Tests for AI decision types, `ContainerInference`, `TraversalPlan`, and safety models
- **Trace Models** (`test_trace.py`): Tests for trace recording, `TraceStep`, `StateSnapshot`, and `TraversalTrace`
- **Enum Tests** (`test_enums.py`): Unified tests for enum helper methods (`values()`, `from_value()`, `is_valid()`)

### Test Assets (`tests/assets/`)

Reusable test utilities and fixtures:

- **Fixtures** (`fixtures/`): JSON files containing sample data for testing
  - `page_analysis.json`: Sample page analysis data
  - `graph_nodes.json`: Sample graph node configurations
  - `state_machines.json`: Sample state machine data
  - `trace_data.json`: Sample trace data
  - `ai_data.json`: Sample AI capability data

- **Utils** (`utils/`): Test utility modules
  - `model_helpers.py`: Helper functions for model testing
  - `assertions.py`: Custom assertions for model validation

## Running Tests

### Run all model tests:
```bash
pytest tests/models/ -v
```

### Run specific test file:
```bash
pytest tests/models/test_content_tree.py -v
```

### Run specific test class:
```bash
pytest tests/models/test_content_tree.py::TestCoordinate -v
```

### Run with coverage:
```bash
pytest tests/models/ --cov=src --cov-report=term-missing
```

## Test Standards

All model tests follow these standards:

1. **Field Validation**: Tests verify required fields, type checking, value ranges, and default values
2. **Serialization**: Tests verify `to_dict()` and JSON serialization (if applicable)
3. **Deserialization**: Tests verify `from_dict()` and JSON deserialization (if applicable)
4. **Boundary Conditions**: Tests cover edge cases like empty values, extreme values, and invalid inputs
5. **Enum Helpers**: All enum types are tested for `values()`, `from_value()`, and `is_valid()` methods

## Coverage Goals

- **Core Models**: 80%+ coverage (PageAnalysis, TraversalNode, TraversalContext, etc.)
- **Auxiliary Models**: 60%+ coverage (MenuInfo, Coordinate, enums, etc.)

## Archiving Legacy Tests

The `tests/archive/` directory contains deprecated or legacy test files that have been superseded by the new test structure. These are kept for reference but are not executed in the standard test suite.

## Adding New Tests

When adding new model tests:

1. Create a new test file in `tests/models/` following the naming convention `test_<module>.py`
2. Organize tests into classes, one per model type
3. Follow the existing test patterns for consistency
4. Add fixture data to `tests/assets/fixtures/` if needed
5. Update this README if adding new test categories

## Test Utilities

The `tests/assets/utils/` module provides reusable testing utilities:

- `model_helpers.py`: Functions for creating test model instances
- `assertions.py`: Custom assertion helpers for model validation

See `tests/assets/README.md` for more details on test assets.

---

## Refactoring Verification

### Automated Verification Script

Before committing any refactoring, run the verification script:

```bash
# Full verification (tests + coverage + linting + type check)
python scripts/verify_refactor.py

# Fast mode (skip coverage for quicker feedback)
python scripts/verify_refactor.py --fast

# Auto-fix linting and formatting issues
python scripts/verify_refactor.py --fix

# Skip specific checks
python scripts/verify_refactor.py --skip-type-check
python scripts/verify_refactor.py --skip-lint
```

### Manual Verification Checklist

If you prefer manual verification or need to run specific checks:

1. **Model Tests**: `pytest tests/models/ -v`
   - All core business model tests must pass
   - Includes: content_tree, graph_nodes, state_machine, context, exception, ai_types, trace

2. **Coverage Check**: `pytest tests/models/ --cov=src --cov-report=term-missing`
   - Core Models: 80%+ coverage
   - Auxiliary Models: 60%+ coverage

3. **Type Checking**: `mypy src/`
   - Ensures type hints are correct
   - Catches potential type-related bugs

4. **Linting**: `ruff check src/`
   - Code style and potential issues

5. **Formatting**: `ruff format src/ --check`
   - Consistent code formatting

### Coverage Goals

- **Core Models** (PageAnalysis, TraversalNode, TraversalContext): 80%+
- **Auxiliary Models** (MenuInfo, Coordinate, enums): 60%+

If coverage decreases after refactoring, add tests to cover the affected code.

### Continuous Integration

The verification script can be integrated into:

1. **Pre-commit hooks**: Run `verify_refactor.py --fast` before each commit
2. **CI/CD pipelines**: Run full `verify_refactor.py` on pull requests
3. **Manual workflow**: Run before pushing to main branch
