# Implementation Tasks

## Phase 1: Infrastructure (1 hour)

### 1.1 Create Directory Structure
- [ ] Create `test_results/` directory
- [ ] Create `test_results/schema/` subdirectory
- [ ] Verify Git tracking

### 1.2 Create Documentation
- [ ] Write `test_results/README.md`
  - Purpose and file format
  - Naming rules
  - Data freshness guidelines
  - File lifecycle
- [ ] Create `test_results/schema/unit_result.schema.json`
  - Include schema_version field
  - Document required and optional fields
  - Add examples

---

## Phase 2: Core Code Changes (3 hours)

### 2.1 Update test_runner.py
- [ ] Add required imports (json, datetime, pathlib)
- [ ] Modify `_build_test_command()` method
  - Add JSON output flags
  - Add module parameter validation
- [ ] Implement `_generate_standard_result()` method
  - Try plugin method first
  - Fallback to stdout parsing
  - Error handling
- [ ] Implement `_convert_from_raw()` method
  - Parse pytest-json-report format
  - Extract summary statistics
  - Extract failure details
  - Parse coverage XML (optional)
- [ ] Implement `_convert_from_stdout()` method
  - Parse summary line with regex
  - Extract failure details
  - Handle edge cases
- [ ] Implement `_write_final_json()` method
  - Write with proper encoding
  - Error handling for permissions
- [ ] Update `_run_single_module()` method
  - Call `_generate_standard_result()`
  - Handle non-blocking errors
  - Store stdout for fallback

### 2.2 Verify Changes
- [ ] Code follows project style
- [ ] Type hints present
- [ ] Docstrings complete
- [ ] No code duplication

---

## Phase 3: Unit Tests (3 hours)

### 3.1 Create Test Files
- [ ] Create `tests/v6/test_unit/` directory
- [ ] Write `test_json_contract.py`
  - Test schema validation
  - Test required fields
  - Test data consistency
- [ ] Write `test_convert_from_raw.py`
  - Test normal conversion
  - Test missing fields handling
  - Test coverage extraction
  - Test edge cases
- [ ] Write `test_convert_from_stdout.py`
  - Test summary parsing (multiple formats)
  - Test failure extraction
  - Test edge cases (0 tests, all failed)
  - Test regex patterns
- [ ] Write `test_standard_result.py`
  - Test main workflow
  - Test error handling
  - Test fallback behavior

### 3.2 Achieve Coverage
- [ ] Run tests with coverage
- [ ] Verify ≥90% coverage for new code
- [ ] Fix any uncovered paths

---

## Phase 4: Skill Documentation (2 hours)

### 4.1 Update module-test Skill
- [ ] Edit `.claude/skills/module-test/SKILL.md`
  - Add "Standardized Output" section
  - Document JSON generation behavior
  - Document fallback mechanism
  - Include quality gates

### 4.2 Update validation-documentation Skill
- [ ] Edit `.claude/skills/validation-documentation/SKILL.md`
  - Add "Standardized Data Input" section
  - Document data ingestion protocol
  - Include freshness checking
  - Document error handling

---

## Phase 5: Dashboard Integration (4 hours)

### 5.1 Create Dashboard Server
- [ ] Create `dashboards/test_report_server.py`
  - Implement `TestResultsDataSource` class
  - Implement `TestResultsAnalyzer` class
  - Implement `TestRunnerAPI` class
  - Create HTTP request handler
  - Add test triggering endpoints

### 5.2 Create UI Components
- [ ] Design Dashboard HTML template
  - Global statistics cards
  - Module status grid
  - Failure details table
  - Re-run buttons
  - Auto-refresh logic
- [ ] Add CSS styling
  - Color coding for status
  - Responsive layout
  - Clean, modern design

### 5.3 Implement Interactions
- [ ] JavaScript for button handlers
- [ ] Polling mechanism for running tests
- [ ] Auto-refresh on completion
- [ ] Error handling and feedback

### 5.4 Update Documentation
- [ ] Update `dashboards/README.md`
  - Add test report section
  - Document API endpoints
  - Include usage examples

---

## Phase 6: Quality Verification (2 hours)

### 6.1 Integration Testing
- [ ] Run end-to-end workflow
- [ ] Test with existing modules
- [ ] Verify JSON generation
- [ ] Verify Dashboard display
- [ ] Test trigger functionality

### 6.2 Performance Testing
- [ ] Measure Dashboard load time (<1s target)
- [ ] Test with 10+ modules
- [ ] Verify auto-refresh works

### 6.3 Documentation Review
- [ ] Review all updated files
- [ ] Verify completeness
- [ ] Check for placeholders or TBDs
- [ ] Validate code examples

### 6.4 Acceptance Criteria
- [ ] F1-F6 functional criteria met
- [ ] Q1-Q3 quality criteria met
- [ ] All unit tests passing
- [ ] No blocking issues

---

## Verification Checklist

Before marking complete:

### Functional
- [ ] All modules generate `{module}_unit.json`
- [ ] JSON schema includes `schema_version` field
- [ ] Plugin method works (pytest-json-report)
- [ ] Fallback method works (stdout parsing)
- [ ] Dashboard displays test status
- [ ] Dashboard shows failure details
- [ ] Test triggering works from Dashboard
- [ ] Auto-refresh functions correctly

### Quality
- [ ] Unit test coverage ≥90%
- [ ] All tests passing
- [ ] Code follows project style
- [ ] Documentation complete
- [ ] No console warnings/errors

### Reliability
- [ ] Handles missing plugin gracefully
- [ ] JSON generation doesn't block tests
- [ ] Dashboard handles stale data
- [ ] Concurrent test triggering safe

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
