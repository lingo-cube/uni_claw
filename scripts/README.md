# Scripts Directory

Project utility scripts for validation and testing.

## validate_test_result.py

**Purpose**: Validate test result JSON structure compliance with the minimal contract.

**Usage**:
```bash
# Validate all module test results
python scripts/validate_test_result.py

# Validate specific module
python scripts/validate_test_result.py simulation
```

**Validation Checks**:
- Required fields present (module, timestamp, summary, failures)
- Summary counts consistent (total = passed + failed + error + skipped)
- Failures array matches failed+error counts
- JSON parseable and well-formed

**Exit Codes**:
- `0` - All validations passed
- `1` - Validation failed or file not found
