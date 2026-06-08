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

## doc_audit.py

**Purpose**: Comprehensive documentation audit for structure, freshness, coverage, and naming conventions.

**Usage**:
```bash
# Run full audit and generate markdown report
python scripts/doc_audit.py

# Custom output location
python scripts/doc_audit.py --output docs/my_audit.md

# JSON output for CI/CD integration
python scripts/doc_audit.py --json
```

**Audit Checks**:
- **Structure**: Verifies expected documentation files and directories exist
- **Freshness**: Identifies stale documents (>180 days) and warnings (>90 days)
- **Coverage**: Maps source modules to documentation, calculates coverage ratio
- **Naming**: Validates PRD and design document naming conventions

**Report Location**: `docs/reports/doc_audit_YYYY-MM-DD.md`

**Exit Codes**:
- `0` - All checks passed (documentation healthy)
- `1` - Critical issues found (failures)
- `2` - Warnings only (improvements needed)

**See Also**: `README_doc_audit.md` for detailed documentation
