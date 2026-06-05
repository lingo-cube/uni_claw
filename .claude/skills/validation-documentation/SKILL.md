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