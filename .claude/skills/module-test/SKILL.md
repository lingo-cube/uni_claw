---
name: module-test
description: Execute module unit tests with intelligent failure handling and decision tracking
license: MIT
compatibility: Works with pytest, unittest, Python 3.8+
metadata:
  author: uni-claw-ai-team
  version: "2.0"
  tags: [testing, unit-tests, pytest, quality-gate, openspec-integration]
---

# Module Test Skill

Execute module unit tests with intelligent failure handling and decision tracking.

## When to Use

Use this skill when you need to:
- Run unit tests after code changes
- Handle test failures systematically
- Ensure code quality before completing tasks
- Integrate automated testing into OpenSpec workflows
- Track test coverage and quality metrics

## What It Does

1. **Smart Test Execution**: Automatically detects and runs appropriate tests:
   - Module-specific test directories (`src/{module}/test/`)
   - Centralized test directories (`tests/{module}/`)
   - Custom test scripts (`src/{module}/run_tests.py`)

2. **Intelligent Failure Handling**: 5-level priority system:
   - Level 0: Environment issues (ImportError, FileNotFoundError)
   - Level 1: Code implementation analysis
   - Level 2: Design document verification (check CLAUDE.md + read docs)
   - Level 3: User consultation
   - Level 4: Careful test case modification

3. **Quality Gates**: Enforces completion conditions:
   - All tests must pass (failed == 0, errors == 0)
   - Coverage thresholds met (default 80%)
   - All failures documented
   - Regression verification passed

4. **Decision Tracking**: Records all decisions in `.test_fix_log.md`

## How It Works

1. **Environment Preparation**: Clean test cache and install dependencies
2. **Change Detection**: Identify modified modules via git
3. **Design Understanding**: Find and analyze module design documents
4. **Test Discovery**: Find appropriate test paths and frameworks
5. **Test Execution**: Run tests with proper isolation
6. **Coverage Analysis**: Check code coverage metrics
7. **Failure Handling**: Process failures using priority system
8. **Documentation**: Record decisions and results
9. **Regression Verification**: Ensure fixes don't break existing tests

## Usage

### Basic Test Execution

```bash
# Quick test of changed modules
changed_modules=$(git diff --name-only | grep "src/.*\.py" | cut -d'/' -f2 | sort -u)
for module in $changed_modules; do python -m pytest src/$module/test/ -v; done
```

### Coverage Check

```bash
# Check coverage for all changed modules
for module in $changed_modules; do
    python -m pytest src/$module/test/ --cov=src.$module --cov-report=term-missing
done
```

### Failure Diagnosis

```bash
# Use diagnostic script for detailed analysis
python .claude/skills/module-test/test_diagnostic.py --module graph --test test_max_depth

# View decision log
cat .test_fix_log.md
```

### Advanced Test Management

```bash
# Use test runner for full workflow
python .claude/skills/module-test/test_runner.py

# Check coverage thresholds
python .claude/skills/module-test/coverage_checker.py --threshold 80
```

### Design Document Lookup (Level 2 Failure Handling)

When test failures require design document verification:

```bash
# Step 1: Read CLAUDE.md to find document index
Read CLAUDE.md

# Step 2: Read relevant design documents based on module
# For graph module:
Read docs/GRAPH_MODEL.md
Read docs/ARCHITECTURE.md

# For ai module:
Read src/ai/README.md
Read docs/ARCHITECTURE.md

# For state_machine module:
Read docs/hierarchical_state_machine.md
Read docs/state_machine_design.md
```

## Design Document Understanding

### Level 2: Design Document Verification Process

When test failures require design understanding (after Level 0 environment check and Level 1 code analysis):

**Step 1: Read Documentation Index**
```bash
Read CLAUDE.md
```
- Understand project documentation structure
- Identify relevant documents for the failing module
- Look for module-specific documentation links

**Step 2: Intelligent Document Selection**
Use AI judgment to determine which documents to read:
- What module is failing? → Look for module-specific docs
- What aspect is failing? → Architecture/API/Implementation docs
- What's the test asserting? → Design/PRD/Spec docs

**Step 3: Read Selected Documents**
```bash
# Examples (AI should select based on context):
Read docs/ARCHITECTURE.md           # For architecture questions
Read src/graph/README.md            # For module-specific design
Read docs/PRD_UNIFIED.md            # For product requirements
```

**Step 4: Compare Design vs Test**
- Does the test match documented behavior?
- Has design evolved since test was written?
- Are there architectural constraints?

**Fallback: Design Doc Finder**
If AI cannot determine relevant documents:
```bash
python .claude/skills/module-test/design_doc_finder.py <module_name>
```

### Key Principle
**AI Judgment First, Tools Second**
- Trust AI to identify relevant documentation from context
- Use design_doc_finder.py only when unclear
- Document the reasoning process in `.test_fix_log.md`

## Strict Rules

**NEVER**:
- ❌ Modify test assertions to make tests pass
- ❌ Delete or comment out failing tests
- ❌ Add always-passing assertions (like `assert True`)
- ❌ Ignore test results when marking tasks complete

**ALWAYS**:
- ✅ Follow the 5-level failure handling priority
- ✅ Document all decisions in `.test_fix_log.md`
- ✅ Ensure all tests pass before completion
- ✅ Verify coverage thresholds are met

## OpenSpec Integration

### Hook Integration

The skill integrates with OpenSpec via `openspec/hooks/module_test_hook.py`:

**Pre-Task Hook**: Captures test baseline
**Post-Task Hook**: Validates test integrity and triggers skill

### Workflow Integration

```
OpenSpec Task Start
    ↓
Pre-Task Hook - Capture baseline
    ↓
Execute Code Changes
    ↓
Post-Task Hook - Validate tests
    ↓
Trigger module-test skill
    ↓
Execute tests per skill workflow
    ↓
Record decisions to .test_fix_log.md
    ↓
Task Complete
```

## Configuration

### .test-config.yaml

Optional project configuration:

```yaml
# Test framework selection
test_runner: auto  # auto/pytest/unittest/tox

# Coverage requirements
coverage:
  enabled: true
  threshold: 80

# Parallel execution
parallel:
  enabled: true
  workers: "auto"

# Module dependencies
dependencies:
  - "src/utils -> src/graph"
  - "src/models -> src/ai"
  - "src/graph -> src/traversal"
```

## Output Reports

### Test Results
- Console output with pass/fail summary
- JSON reports where available
- Coverage statistics

### Decision Log
- `.test_fix_log.md` with complete decision history
- Problem analysis and resolution steps
- Verification and validation results

## Completion Conditions

**Task completion requires**:
1. ✅ **All tests pass** - `failed == 0`, `errors == 0`
2. ✅ **Coverage达标** - Meets configured threshold
3. ✅ **Issues documented** - All failures have resolution records
4. ✅ **Regression验证** - Related modules still pass tests

## Auxiliary Scripts

Detailed implementation is provided by helper scripts:
- `test_diagnostic.py` - Intelligent test failure analysis
- `test_runner.py` - Complete test execution workflow
- `coverage_checker.py` - Coverage threshold validation

## See Also

- `openspec/hooks/module_test_hook.py` - OpenSpec integration
- `openspec/hooks/MODULE_TEST_INTEGRATION.md` - Integration guide
- `.test-config.yaml` - Test configuration