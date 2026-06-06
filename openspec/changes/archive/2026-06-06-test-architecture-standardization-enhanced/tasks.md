# Implementation Tasks

## Phase 1: Infrastructure (1 hour)

### 1.1 Create Directory Structure
- [x] Create `test_results/` directory
- [x] Create `test_results/schema/` subdirectory
- [x] Verify Git tracking

### 1.2 Create Documentation
- [x] Write `test_results/README.md`
  - Purpose and file format
  - Naming rules
  - Data freshness guidelines
  - File lifecycle
- [x] Create `test_results/schema/unit_result.schema.json`
  - Include schema_version field
  - Document required and optional fields
  - Add examples

---

## Phase 2: Core Code Changes (3 hours)

### 2.1 Update test_runner.py
- [x] Add required imports (json, datetime, pathlib)
- [x] Modify `_build_test_command()` method
  - Add JSON output flags
  - Add module parameter validation
- [x] Implement `_generate_standard_result()` method
  - Try plugin method first
  - Fallback to stdout parsing
  - Error handling
- [x] Implement `_convert_from_raw()` method
  - Parse pytest-json-report format
  - Extract summary statistics
  - Extract failure details
  - Parse coverage XML (optional)
- [x] Implement `_convert_from_stdout()` method
  - Parse summary line with regex
  - Extract failure details
  - Handle edge cases
- [x] Implement `_write_final_json()` method
  - Write with proper encoding
  - Error handling for permissions
- [x] Update `_run_single_module()` method
  - Call `_generate_standard_result()`
  - Handle non-blocking errors
  - Store stdout for fallback

### 2.2 Verify Changes
- [x] Code follows project style
- [x] Type hints present
- [x] Docstrings complete
- [x] No code duplication

---

## Phase 3: Unit Tests (3 hours)

### 3.1 Create Test Files
- [x] Create `tests/v6/test_unit/` directory
- [x] Write `test_json_contract.py`
  - Test schema validation
  - Test required fields
  - Test data consistency
- [x] Write `test_convert_from_raw.py`
  - Test normal conversion
  - Test missing fields handling
  - Test coverage extraction
  - Test edge cases
- [x] Write `test_convert_from_stdout.py`
  - Test summary parsing (multiple formats)
  - Test failure extraction
  - Test edge cases (0 tests, all failed)
  - Test regex patterns
- [x] Write `test_standard_result.py`
  - Test main workflow
  - Test error handling
  - Test fallback behavior

### 3.2 Achieve Coverage
- [x] Run tests with coverage
- [x] Verify ≥90% coverage for new code
- [x] Fix any uncovered paths

---

## Phase 4: Skill Documentation (2 hours)

### 4.1 Update module-test Skill
- [x] Edit `.claude/skills/module-test/SKILL.md`
  - Add "Standardized Output" section
  - Document JSON generation behavior
  - Document fallback mechanism
  - Include quality gates

### 4.2 Update validation-documentation Skill
- [x] Edit `.claude/skills/validation-documentation/SKILL.md`
  - Add "Standardized Data Input" section
  - Document data ingestion protocol
  - Include freshness checking
  - Document error handling

---

## Phase 5: Dashboard Integration (4 hours)

### 5.1 Create Dashboard Server
- [x] Create `dashboards/test_report_server.py`
  - Implement `TestResultsDataSource` class
  - Implement `TestResultsAnalyzer` class
  - Implement `TestRunnerAPI` class
  - Create HTTP request handler
  - Add test triggering endpoints

### 5.2 Create UI Components
- [x] Design Dashboard HTML template
  - Global statistics cards
  - Module status grid
  - Failure details table
  - Re-run buttons
  - Auto-refresh logic
- [x] Add CSS styling
  - Color coding for status
  - Responsive layout
  - Clean, modern design

### 5.3 Implement Interactions
- [x] JavaScript for button handlers
- [x] Polling mechanism for running tests
- [x] Auto-refresh on completion
- [x] Error handling and feedback

### 5.4 Update Documentation
- [x] Update `dashboards/README.md`
  - Add test report section
  - Document API endpoints
  - Include usage examples

---

## Phase 6: Quality Verification (2 hours)

### 6.1 Integration Testing
- [x] Run end-to-end workflow
- [x] Test with existing modules
- [x] Verify JSON generation
- [x] Verify Dashboard display
- [x] Test trigger functionality

### 6.2 Performance Testing
- [x] Measure Dashboard load time (<1s target)
- [x] Test with 10+ modules
- [x] Verify auto-refresh works

### 6.3 Documentation Review
- [x] Review all updated files
- [x] Verify completeness
- [x] Check for placeholders or TBDs
- [x] Validate code examples

### 6.4 Acceptance Criteria
- [x] F1-F6 functional criteria met
- [x] Q1-Q3 quality criteria met
- [x] All unit tests passing
- [x] No blocking issues

---

## Verification Checklist

Before marking complete:

### Functional
- [x] All modules generate `{module}_unit.json`
- [x] JSON schema includes `schema_version` field
- [x] Plugin method works (pytest-json-report)
- [x] Fallback method works (stdout parsing)
- [x] Dashboard displays test status
- [x] Dashboard shows failure details
- [x] Test triggering works from Dashboard
- [x] Auto-refresh functions correctly

### Quality
- [x] Unit test coverage ≥90%
- [x] All tests passing
- [x] Code follows project style
- [x] Documentation complete
- [x] No console warnings/errors

### Reliability
- [x] Handles missing plugin gracefully
- [x] JSON generation doesn't block tests
- [x] Dashboard handles stale data
- [x] Concurrent test triggering safe

---

## Estimated Completion Time

| Phase | Estimate | Cumulative |
|-------|----------|------------|
| Phase 1 | 1h | 1h |
| Phase 2 | 3h | 4h |
| Phase 3 | 3h | 7h |
| Phase 4 | 2h | 9h |
| Phase 5 | 4h | 13h |
| Phase 6 | 2h | 15h |

**Total: 15 hours (approximately 2 working days)**
