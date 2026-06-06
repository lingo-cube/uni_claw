# Test Architecture Standardization - Examples

This document provides examples demonstrating the standardized test result workflow.

## Example 1: Generate Test Results

```bash
# Run tests for a specific module
python -m pytest tests/v6/test_state_machine.py -v

# Generate standardized JSON automatically
python .claude/skills/module-test/test_runner.py --module state_machine

# Verify generated JSON
cat test_results/state_machine_unit.json
```

**Output**: `test_results/state_machine_unit.json`
```json
{
  "module": "state_machine",
  "timestamp": "2026-06-05T13:59:35.880037+00:00",
  "summary": {
    "total": 35,
    "passed": 30,
    "failed": 0,
    "error": 0,
    "skipped": 5
  },
  "failures": [],
  "coverage": {
    "line_rate": 0.3026,
    "branch_rate": 0.0
  }
}
```

## Example 2: Validate Test Results

```bash
# Validate JSON structure
python scripts/validate_test_result.py state_machine

# Validate all module results
python scripts/validate_test_result.py
```

## Example 3: Generate Validation Documentation

```bash
# Use validation-documentation skill to generate reports
# Reads from test_results/{module}_unit.json files
# Outputs standardized validation reports
```

## Example 4: With Coverage

```bash
# Run tests with coverage
python -m pytest tests/v6/test_state_machine.py \
  --cov=src.state_machine \
  --cov-report=xml:test_results/state_machine_coverage.xml

# Coverage data automatically included in JSON
```

## Example 5: Multiple Modules

```bash
# Generate results for multiple modules
for module in state_machine graph models; do
    python .claude/skills/module-test/test_runner.py --module $module
done

# Check all results
ls test_results/*_unit.json
```
