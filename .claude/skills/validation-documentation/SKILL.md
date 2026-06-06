---
name: validation-documentation
description: Generate standardized validation reports with consistent naming and formatting
metadata:
  type: documentation
---

# Validation Documentation Skill

Standardize validation report generation with consistent naming, formatting, and workflow integration.

## When to Use

Use this skill when you need to:
- Generate validation reports after testing
- Document test results and analysis
- Create standardized project documentation
- Track validation progress across sessions
- Maintain consistent documentation structure

## Core Principles

### 0. Standardized Data Input

All test-related validation reports derive data **exclusively** from standardized JSON files in `test_results/` directory.

#### Data Source

- **Primary**: `test_results/{module}_unit.json` - Minimal test result contract
- **Optional**: `test_results/{module}_coverage.xml` - Coverage data in Cobertura format

#### Data Ingestion Protocol

**Step 1: Availability Check**
```bash
# List all available test result files
ls test_results/*_unit.json 2>/dev/null || echo "No test results found"
```

**Availability Decision Tree:**
```
ls test_results/*_unit.json succeeds?
├── YES → Proceed to Step 2 (Data Loading)
└── NO  → Execute fallback protocol
    ├── Check test_results/ directory exists
    ├── If missing: "Create test_results/ directory first"
    ├── If exists but empty: "No test results found. Run module-test first."
    └── Suggest command: `python .claude/skills/module-test/test_runner.py {module_name}`
```

**Step 2: Data Loading**
For each JSON file in `test_results/`:

1. **File Validation**
   - Verify file exists and is readable
   - Check file size > 0 bytes
   - Verify JSON extension

2. **JSON Parsing**
   - Parse JSON content
   - Extract required fields: `module`, `timestamp`, `summary`, `failures`, `coverage`
   - Validate field types (strings, numbers, objects)

3. **Schema Validation**
   - `module`: non-empty string
   - `timestamp`: ISO 8601 format or epoch milliseconds
   - `summary`: object with `total`, `passed`, `failed`, `error`, `skipped` integers
   - `failures`: array (may be empty)
   - `coverage`: object with `percent`, `lines`, `branches` (optional)

**Step 3: Data Aggregation**
Calculate global statistics across all modules:

```python
# Aggregation logic
total_tests = sum(module.summary.total for module in modules)
total_passed = sum(module.summary.passed for module in modules)
total_failed = sum(module.summary.failed for module in modules)
total_errors = sum(module.summary.error for module in modules)
total_skipped = sum(module.summary.skipped for module in modules)

pass_rate = (total_passed / total_tests) * 100 if total_tests > 0 else 0
overall_status = "PASSED" if (total_failed + total_errors) == 0 else "HAS FAILURES"

# Coverage aggregation
avg_coverage = weighted_average(module.coverage.percent, module.summary.total)
```

**Step 4: Freshness Check**
For each module timestamp, calculate data age:

```python
# Freshness calculation
from datetime import datetime, timezone

def calculate_freshness(timestamp_str):
    """Calculate hours since test run"""
    try:
        # Parse timestamp (ISO 8601 or epoch)
        if timestamp_str.isdigit():
            test_time = datetime.fromtimestamp(int(timestamp_str) / 1000, tz=timezone.utc)
        else:
            test_time = datetime.fromisoformat(timestamp_str.replace('Z', '+00:00'))
        
        age_hours = (datetime.now(timezone.utc) - test_time).total_seconds() / 3600
        return age_hours
    except Exception as e:
        return None  # Timestamp parsing failed
```

**Freshness Thresholds:**
- **FRESH** (< 24 hours): No warning
- **ACCEPTABLE** (24-48 hours): Minor warning
- **STALE** (> 48 hours): Major warning
- **VERY STALE** (> 7 days): Critical warning

**Warning Format:**
```markdown
⚠️ **Data Freshness Warning**: Test results for some modules are older than 48 hours.

| Module | Age | Last Run |
|--------|-----|----------|
| {module1} | {X} hours | {date1} |
| {module2} | {Y} hours | {date2} |

Consider re-running tests for current validation:
```bash
python .claude/skills/module-test/test_runner.py {module_name}
```
```

**Step 5: Report Generation**
After successful data ingestion, generate reports:

- `unit_test_status.md` - Overall unit test status
- `integration_test_status.md` - Integration test details (if applicable)
- `comprehensive_status.md` - Comprehensive status across all modules

#### Data Quality Requirements

| Requirement | Specification | Validation Method |
|-------------|---------------|-------------------|
| **Format** | Valid JSON with UTF-8 encoding | JSON parsing with error capture |
| **Schema** | All required fields present | Schema validation against contract |
| **Integrity** | total = passed + failed + error + skipped | Arithmetic validation |
| **Consistency** | Timestamps are recent (< 48h) | Freshness check |
| **Completeness** | At least one module result available | File count check |
| **Accuracy** | Percent values 0-100, counts non-negative | Range validation |

#### Error Handling

**Category 1: File System Errors**
```
Error: Directory test_results/ not found
→ Action: Create directory with `mkdir -p test_results/`
→ Message: "Created test_results/ directory. Please run module-test first."

Error: No test result files found
→ Action: Suggest running module-test
→ Message: "No test results in test_results/. Run: python .claude/skills/module-test/test_runner.py <module>"
```

**Category 2: JSON Parsing Errors**
```
Error: Invalid JSON in {file_path}
→ Action: Report file path and line number
→ Message: "Failed to parse {file_path}: {error} at line {line_number}"
→ Recovery: Skip file, continue with others

Error: JSON decoding failed
→ Action: Verify file not corrupted
→ Message: "File {file_path} may be corrupted. Re-run tests for this module."
```

**Category 3: Schema Validation Errors**
```
Error: Missing required field '{field}' in {module}
→ Action: Report specific field and module
→ Message: "Schema validation failed for {module}: missing '{field}' field"
→ Recovery: Exclude module from aggregation

Error: Invalid type for field '{field}' in {module}
→ Action: Report expected vs actual type
→ Message: "Invalid type for {field} in {module}: expected {expected_type}, got {actual_type}"
→ Recovery: Use default value or exclude module
```

**Category 4: Data Integrity Errors**
```
Error: Summary counts don't add up in {module}
→ Action: Report arithmetic inconsistency
→ Message: "Data integrity error in {module}: total ({total}) ≠ passed ({passed}) + failed ({failed}) + error ({error}) + skipped ({skipped})"
→ Recovery: Recalculate total from components, flag for review

Error: Invalid percentage in coverage for {module}
→ Action: Report out-of-range value
→ Message: "Coverage percentage {percent}% is out of range [0, 100] in {module}"
→ Recovery: Clamp to valid range, flag for review
```

**Category 5: Freshness Warnings**
```
Warning: Test results older than 48 hours for modules: {list}
→ Action: Include prominent warning in report header
→ Message: "⚠️ Some test data is stale. Consider re-running tests."
→ Recovery: Allow report generation with warning

Warning: Could not parse timestamp for {module}
→ Action: Report timestamp format issue
→ Message: "Invalid timestamp format '{timestamp}' in {module}"
→ Recovery: Use file modification time, flag for review
```

#### Error Recovery Strategy

**Continue on Error Mode:**
- Parse all available files even if some fail
- Document which files were skipped and why
- Generate partial report with caveats
- Include error summary in report footer

**Fail Fast Mode:**
- Stop on first critical error
- Require manual intervention
- Use for strict validation scenarios

**Default Behavior: Continue on Error**
```
Processing test_results/:
✅ graph_models_unit.json (48 tests, 100% pass)
✅ trace_recorder_unit.json (32 tests, 100% pass)
❌ simulation_runner_unit.json (JSON parse error at line 15)
✅ graph_engine_unit.json (44 tests, 100% pass)

Report generated with 3/4 modules. 1 file skipped due to errors.
See errors section below for details.
```

---

### 1. Fixed Naming (Overwrite Mode)
Use consistent, version-independent names to prevent file accumulation:

```bash
docs/validation/
├── final_report.md                 # Final validation summary
├── unit_test_status.md             # Unit test analysis
├── integration_test_status.md       # Integration test results
├── system_infrastructure_analysis.md  # Infrastructure analysis
├── test_data_quality.md             # Test data quality
├── progress_report.md              # Current progress (overwrites)
└── planned_features.md             # Future roadmap
```

### 2. Version Information in Content
Never include version numbers in filenames. Always document versions in file headers:

```markdown
# Document Title

**Project**: Uni-Claw  
**Version**: V6.0  
**Component**: Simulation Testing System  
**Generated**: 2026-06-05  
**Change**: implementation-validation  
**Task**: 2.3 - Verify Unit Test Suite Completeness  
**Git Commit**: abc123def456
```

### 3. Standardized Document Structure
Each validation report should follow this template:

```markdown
# [Report Title]

**Generated**: [Date]  
**Status**: [COMPLETE/IN_PROGRESS]  
**Change**: [Change Name]  
**Task**: [Task ID] - [Task Description]

---

## Executive Summary
[Brief overview of findings]

## Detailed Analysis
[Comprehensive analysis content]

## Conclusions & Recommendations
[Key findings and next steps]
```

## How It Works

### Phase 1: Document Planning
1. **Identify document type** - Choose appropriate standard name
2. **Check existing files** - See what already exists
3. **Plan content scope** - Define what to document

### Phase 2: Content Generation
1. **Use standard template** - Follow structure above
2. **Include metadata** - Add project/version info in header
3. **Be comprehensive** - Cover all relevant aspects
4. **Use overwrite mode** - Replace existing file if needed

### Phase 3: Quality Check
1. **Verify naming** - Ensure follows standard naming
2. **Check formatting** - Consistent markdown structure
3. **Validate completeness** - All sections present
4. **Git tracking** - Commit with clear message

## Naming Rules

### ✅ ACCEPTED (Standard Names)
- `final_report.md`
- `unit_test_status.md`
- `integration_test_status.md`
- `system_infrastructure_analysis.md`
- `test_data_quality.md`
- `progress_report.md`
- `planned_features.md`

### ❌ AVOID (Problematic Patterns)
- **Version-specific**: `V6_unimplemented_features.md`, `V5_integration_test.md`
- **Date-based**: `progress_report_2026-06-04.md`, `unit_test_2026-06-05.md`
- **Numbered**: `final_report_v2.md`, `analysis_iteration_1.md`
- **Redundant prefixes**: `simulation_infrastructure_` → `system_infrastructure_`

### File Mapping for Common Types

| Document Type | Standard Name | Replaces |
|---------------|---------------|------------|
| Final validation summary | `final_report.md` | Any final/summary report |
| Unit test analysis | `unit_test_status.md` | Any unit test report |
| Integration results | `integration_test_status.md` | Any integration report |
| Infrastructure analysis | `system_infrastructure_analysis.md` | `simulation_infrastructure_analysis.md`, `infra_analysis.md` |
| Data quality analysis | `test_data_quality.md` | `fixture_dataset_quality.md`, `data_quality.md` |
| Current progress | `progress_report.md` | Any progress/status report with date |
| Future roadmap | `planned_features.md` | `unimplemented_features.md`, `roadmap.md` |

## Workflow Integration

### With OpenSpec Changes
When creating OpenSpec tasks that require documentation:

**Task Description Template:**
```markdown
### Task X.Y: Generate [Document Type]

**Description**: [What to document and why]

**Output**: `docs/validation/[standard_name].md` (overwrite mode)

**Guidance**: Follow `validation-documentation` skill naming standards
```

**Example:**
```markdown
### Task 3.2: Document System Infrastructure Analysis

**Description**: Analyze simulation testing infrastructure components

**Output**: `docs/validation/system_infrastructure_analysis.md`

**Guidance**: Use validation-documentation skill for standard formatting
```

### With Testing Workflow
After running test suites, generate reports:

1. **Run tests** - `pytest tests/unit/ -v`
2. **Generate report** - Use this skill to document results
3. **Use standard name** - `unit_test_status.md`
4. **Commit changes** - Git tracks full history

## Usage Examples

### Example 1: After Running Unit Tests
```bash
# 1. Run tests
python -m pytest tests/unit/ -v

# 2. Generate report (use this skill)
# Target: docs/validation/unit_test_status.md
# Content: Document test results, pass rates, coverage

# 3. Follow naming rules
# ✅ Use: unit_test_status.md
# ❌ Don't use: unit_test_2026_06_05.md
```

### Example 2: Infrastructure Analysis
```bash
# Target: docs/validation/system_infrastructure_analysis.md
# Content: Mock components, trace system, test fixtures

# ✅ Use generic name
# ❌ Avoid: simulation_infrastructure_analysis.md (too specific)
```

### Example 3: Progress Updates
```bash
# Target: docs/validation/progress_report.md
# Content: Current validation status, blockers, next steps

# ✅ Overwrites previous progress report
# ❌ Don't create: progress_report_2026-06_05.md (causes accumulation)
```

## File Operations

### Creating New Document
1. Check if standard name already exists
2. If exists, it will be overwritten (intended behavior)
3. Use standard template structure
4. Include proper metadata in header

### Updating Existing Document
1. Open existing standard name file
2. Update content as needed
3. Maintain header metadata
4. Save changes (Git will track differences)

### Renaming Non-Compliant Files
If you find files with non-standard names:

```bash
# V6 specific → Generic
git mv docs/validation/V6_UNIMPLEMENTED_FEATURES.md docs/validation/planned_features.md

# Date-based → Standard
git mv docs/validation/progress_report_2026-06-04.md docs/validation/progress_report.md

# Simulation specific → Generic
git mv docs/validation/simulation_infrastructure_analysis.md docs/validation/system_infrastructure_analysis.md

# Fixture specific → Generic  
git mv docs/validation/fixture_dataset_quality.md docs/validation/test_data_quality.md
```

## Version Tracking

### How to Track Different Versions
Since filenames are fixed, track versions through:

**1. Git History**
```bash
git log --follow docs/validation/final_report.md
git diff HEAD~5 docs/validation/progress_report.md
```

**2. Document Metadata**
```markdown
**Version**: V6.0  
**Change**: implementation-validation  
**Generated**: 2026-06-05
```

**3. Git Tags** (optional)
```bash
git tag validation-v6-final -m "V6 final validation complete"
```

## Quality Checklist

Before considering a validation document complete:

- [ ] **Naming**: Uses standard fixed name (no version/date/numbers)
- [ ] **Header**: Includes project, version, date metadata
- [ ] **Structure**: Follows standard template
- [ ] **Comprehensive**: Covers all relevant aspects
- [ ] **Git Committed**: Changes committed with clear message
- [ ] **No Accumulation**: No duplicate files created

## Migration Guide

### Current Files to Rename

```bash
# Rename existing non-compliant files
git mv docs/validation/progress_report_2026-06-04.md docs/validation/progress_report.md
git mv docs/validation/V6_UNIMPLEMENTED_FEATURES.md docs/validation/planned_features.md
git mv docs/validation/simulation_infrastructure_analysis.md docs/validation/system_infrastructure_analysis.md  
git mv docs/validation/fixture_dataset_quality.md docs/validation/test_data_quality.md

# Commit renaming
git commit -m "standardize validation document naming to generic overwrite mode"
```

### Future Workflow

**When generating validation reports:**
1. Think: "What TYPE of document is this?"
2. Choose: Standard name from the mapping table
3. Generate: Content with proper metadata header
4. Save: Overwrite existing file (intended)
5. Commit: Clear commit message

**Result:** No file accumulation, consistent naming, full history tracking.

## Benefits

### Immediate Benefits
- **No file clutter** - Fixed names prevent accumulation
- **Easy to find** - Predictable file locations
- **Version tracking** - Git history shows evolution
- **Session independent** - Any session uses same names

### Long-term Benefits
- **Scalable** - Works for any project/version
- **Maintainable** - Clear documentation structure
- **Professional** - Consistent presentation
- **Archive-ready** - Easy to move old reports to archive/

## Integration Points

### OpenSpec Workflow
Add to OpenSpec task templates:
```markdown
**Output**: `docs/validation/[standard_name].md`
**Guidance**: Follow `validation-documentation` skill standards
```

### Testing Workflow
Add to test runner hooks:
```bash
# After test completion
if [[ "$TEST_TYPE" == "integration" ]]; then
    # Generate report using validation-documentation skill
    # Target: docs/validation/integration_test_status.md
fi
```

### Documentation Workflow
Include in project documentation:
```markdown
## Validation Documentation
When creating validation reports, follow the `validation-documentation` skill to ensure consistent naming and formatting.
```

---

**Usage**: Generate validation reports during testing phases  
**Output**: Standardized documentation in `docs/validation/`  
**Priority**: Follow naming rules for consistency  
**Integration**: Works with OpenSpec and testing workflows