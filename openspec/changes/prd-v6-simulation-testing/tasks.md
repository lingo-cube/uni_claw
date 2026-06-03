# Tasks: PRD V6.0 Simulation Testing System

## Phase 1: MockVisionService Enhancement (2-3 days)

### 1.1 Implement PageAnalyzer Component

**File**: `src/simulation/page_analyzer.py`

**Tasks**:
- [x] Create `PageAnalyzer` class with `__init__` method accepting `virtual_pages`
- [x] Implement `analyze_page(path)` method with caching logic
- [x] Implement `_get_raw_page_data(path)` method with error handling
- [x] Implement `_structure_page_analysis(path, raw_data)` method
- [x] Implement `_process_elements(elements)` method for UI element processing
- [x] Implement `_infer_page_type(page_data)` method with heuristic rules
- [x] Implement `_infer_action_hint(element)` method for action suggestions
- [x] Add `PageNotFoundError` exception class

**Dependencies**: None
**Estimated Time**: 4-6 hours
**Validation**: Unit tests for all methods with >80% coverage

### 1.2 Enhance MockVisionService

**File**: `src/simulation/mock_vision.py`

**Tasks**:
- [x] Update `MockVisionService.__init__` to integrate `PageAnalyzer`
- [x] Implement `_build_path_mapping(virtual_pages)` method
- [x] Implement `set_context(context)` method supporting multiple context types
- [x] Implement `_infer_path_from_tracer(tracer)` method
- [x] Update `analyze_screenshot()` to use `PageAnalyzer`
- [x] Implement `_get_current_path()` method
- [x] Add `get_call_count()` method for testing
- [x] Add `reset()` method for state management

**Dependencies**: Task 1.1 (PageAnalyzer)
**Estimated Time**: 3-4 hours
**Validation**: Integration tests with PageAnalyzer

### 1.3 Add Unit Tests for PageAnalyzer

**File**: `tests/simulation/test_page_analyzer.py`

**Tasks**:
- [x] Create test fixtures for virtual pages data
- [x] Test `analyze_page()` with valid paths
- [x] Test `analyze_page()` with invalid paths (PageNotFoundError)
- [x] Test caching mechanism (repeated calls)
- [x] Test `_infer_page_type()` with various page types
- [x] Test `_process_elements()` with different element configurations
- [x] Test `_infer_action_hint()` with different element types
- [x] Performance test: ensure <10ms per analysis

**Dependencies**: Task 1.1, 1.2
**Estimated Time**: 3-4 hours
**Validation**: All tests pass, >80% code coverage

### 1.4 Add Integration Tests for MockVisionService

**File**: `tests/simulation/test_mock_vision.py`

**Tasks**:
- [x] Test path mapping functionality
- [x] Test context integration with TraversalContext
- [x] Test context integration with InMemoryTracer
- [x] Test analyze_screenshot() returns correct PageAnalysis
- [x] Test call counting functionality
- [x] Test reset() functionality
- [x] Test error handling for missing pages

**Dependencies**: Task 1.2, 1.3
**Estimated Time**: 2-3 hours
**Validation**: All integration tests pass

## Phase 2: SimulationRunner Completion (3-4 days)

### 2.1 Enhance MockActionExecutor

**File**: `src/simulation/mock_action.py`

**Tasks**:
- [x] Add `OperationRecord` TypedDict with complete structure
- [x] Implement `set_context(context)` method
- [x] Implement `set_page_context(page_context)` method
- [x] Enhance `_record_operation()` method with comprehensive recording
- [x] Implement node stack management (`push_node`, `pop_node`)
- [x] Add `get_history()` method
- [x] Add `get_operations_by_type(action_type)` method
- [x] Add `get_operation_count()` method
- [x] Add `reset()` method
- [x] Update all action methods (click, scroll, input_text, go_back)

**Dependencies**: None
**Estimated Time**: 4-5 hours
**Validation**: Unit tests for recording functionality

### 2.2 Implement Complete SimulationRunner

**File**: `src/simulation/runner.py`

**Tasks**:
- [x] Create `SimulationRunner` class with complete initialization
- [x] Implement `_setup_context_integration()` method
- [x] Implement `run()` method with proper setup/teardown
- [x] Implement `_extract_results(engine_result)` method
- [x] Implement `_extract_visited_tree()` method
- [x] Implement `_extract_completion_reason()` method
- [x] Implement `_compute_statistics()` method
- [x] Implement `_handle_error(error)` method
- [x] Add `StructuredResult` dataclass
- [x] Add `SimulationResult` dataclass

**Dependencies**: Task 2.1 (MockActionExecutor), Phase 1 completion
**Estimated Time**: 6-8 hours
**Validation**: Integration tests with all components

### 2.3 Add Visualization Methods

**File**: `src/simulation/runner.py` (continued)

**Tasks**:
- [x] Implement `render_tree(max_depth)` method
- [x] Implement `render_mermaid()` method
- [x] Implement `export_trace(format)` method
- [x] Add ASCII tree rendering logic
- [x] Add Mermaid diagram generation logic
- [x] Add JSONL export functionality
- [x] Add HTML export functionality

**Dependencies**: Task 2.2
**Estimated Time**: 4-5 hours
**Validation**: Visual inspection of outputs

### 2.4 Add Unit Tests for MockActionExecutor

**File**: `tests/simulation/test_mock_action.py`

**Tasks**:
- [x] Test operation recording for all action types
- [x] Test context integration
- [x] Test node stack management
- [x] Test history retrieval methods
- [x] Test filtering by operation type
- [x] Test reset functionality
- [x] Test operation count accuracy

**Dependencies**: Task 2.1
**Estimated Time**: 2-3 hours
**Validation**: All unit tests pass

### 2.5 Add Integration Tests for SimulationRunner

**File**: `tests/simulation/test_runner.py`

**Tasks**:
- [x] Test complete simulation execution
- [x] Test result extraction accuracy
- [x] Test context integration
- [x] Test error handling
- [x] Test visualization methods
- [x] Test statistics computation
- [x] Test visited tree extraction
- [x] Test completion reason detection

**Dependencies**: Task 2.2, 2.3, 2.4
**Estimated Time**: 4-5 hours
**Validation**: All integration tests pass

## Phase 3: Test Framework Establishment (3-4 days)

### 3.1 Design AI-Friendly Test Case Format

**File**: `tests/simulation/fixtures/template/test_case_template.json`

**Tasks**:
- [x] Create test case JSON schema
- [x] Define `intent_slots` structure
- [x] Define `fixtures` structure
- [x] Define `expected` structure
- [x] Define `assertions` structure
- [x] Create validation schema for test cases
- [x] Document test case format

**Dependencies**: None
**Estimated Time**: 2-3 hours
**Validation**: Schema validation passes

### 3.2 Implement 5 Core Test Fixtures

**Files**: `tests/simulation/fixtures/e2e_*/test_case.json`

**Tasks**:
- [x] Create `e2e_all_traversal` test case (DFS validation)
- [x] Create `e2e_target_found` test case (target search)
- [x] Create `e2e_static_path` test case (static path execution)
- [x] Create `e2e_popup_handling` test case (popup handling)
- [x] Create `e2e_auto_escape` test case (auto level switching)
- [x] Create corresponding `plan_*.json` files
- [x] Create corresponding `pages_*.json` files
- [x] Validate all test cases against schema

**Dependencies**: Task 3.1
**Estimated Time**: 6-8 hours
**Validation**: All fixtures valid and loadable

### 3.3 Implement TraceAsserter

**File**: `tests/simulation/helpers/assertions.py`

**Tasks**:
- [x] Create `TraceAsserter` class
- [x] Implement `step_to_nl(step)` method
- [x] Implement `is_subsequence(expected, actual)` method
- [x] Implement `assert_trace_matches_expected(trace, expected)` method
- [x] Add `AssertionResult` dataclass
- [x] Add helper methods for event matching
- [x] Add violation detection logic
- [x] Add step count validation
- [x] Add completion reason validation

**Dependencies**: None
**Estimated Time**: 4-5 hours
**Validation**: Unit tests for all assertion logic

### 3.4 Implement SimulationTestRunner

**File**: `tests/simulation/helpers/test_runner.py`

**Tasks**:
- [x] Create `SimulationTestRunner` class
- [x] Implement `run_simulation_test(test_case_path, config)` method
- [x] Implement `run_all_tests(tests_dir, pattern, config)` method
- [x] Add test case loading logic
- [x] Add result parsing logic
- [x] Add error handling for malformed test cases
- [x] Add configuration management

**Dependencies**: Task 3.2, 3.3, Phase 2 completion
**Estimated Time**: 3-4 hours
**Validation**: Integration with all fixtures

### 3.5 Implement Pytest Integration

**File**: `tests/simulation/test_simulation_framework.py`

**Tasks**:
- [x] Create pytest configuration file
- [x] Implement pytest fixtures for test setup
- [x] Create pytest tests for all 5 core scenarios
- [x] Add pytest markers for different test categories
- [x] Implement test discovery for test_case.json files
- [x] Add parameterized tests for multiple scenarios
- [x] Configure pytest reporting plugins

**Dependencies**: Task 3.4
**Estimated Time**: 4-5 hours
**Validation**: `pytest tests/simulation/` passes

### 3.6 Add Unit Tests for TraceAsserter

**File**: `tests/simulation/test_assertions.py`

**Tasks**:
- [x] Test `step_to_nl()` with various step types
- [x] Test `is_subsequence()` with different sequences
- [x] Test `assert_trace_matches_expected()` with valid traces
- [x] Test `assert_trace_matches_expected()` with invalid traces
- [x] Test key event matching
- [x] Test violation detection
- [x] Test step count validation
- [x] Test completion reason validation

**Dependencies**: Task 3.3
**Estimated Time**: 2-3 hours
**Validation**: All unit tests pass, 100% assertion accuracy

## Phase 4: CI Ecosystem Integration (2-3 days)

### 4.1 Implement SimulationReportGenerator

**File**: `tests/simulation/helpers/report_generator.py`

**Tasks**:
- [x] Create `SimulationReportGenerator` class
- [x] Implement `generate_report(test_id, result, test_case, assertion_result)` method
- [x] Add `TestReport` dataclass
- [x] Add `TestReportDetails` dataclass
- [x] Implement `export_json_report(report)` method
- [x] Implement `export_html_report(report)` method
- [x] Add HTML template for report visualization
- [x] Add styling and formatting for HTML reports

**Dependencies**: Phase 3 completion
**Estimated Time**: 4-5 hours
**Validation**: Generate valid JSON and HTML reports

### 4.2 Implement CLI Tool

**File**: `cli/simtest.py`

**Tasks**:
- [x] Create `simtest` command-line interface
- [x] Implement `simtest run <test_path>` command
- [x] Implement `simtest suite <tests_dir>` command
- [x] Implement `simtest show <report_path>` command
- [x] Add `--output` option for report generation
- [x] Add `--report` option for suite reports
- [x] Add `--format` option for output format
- [x] Add `--verbose` and `--quiet` options
- [x] Create entry point for pip installation

**Dependencies**: Task 4.1
**Estimated Time**: 5-6 hours
**Validation**: All CLI commands work correctly

### 4.3 Create GitHub Actions Workflow

**File**: `.github/workflows/simulation-tests.yml`

**Tasks**:
- [x] Create GitHub Actions workflow file
- [x] Configure trigger events (push, pull_request)
- [x] Set up matrix testing for Python versions
- [x] Add dependency installation step
- [x] Add simulation test execution step
- [x] Add coverage report generation
- [x] Configure artifact upload for reports
- [x] Add PR comment integration for test results
- [x] Configure branch protection rules

**Dependencies**: Task 4.2
**Estimated Time**: 3-4 hours
**Validation**: CI pipeline runs successfully

### 4.4 Create Pre-commit Hook

**File**: `.git/hooks/pre-commit`

**Tasks**:
- [x] Create pre-commit hook script
- [x] Add fast simulation test execution
- [x] Configure test result checking
- [x] Add user-friendly error messages
- [x] Create installation script for hook setup
- [x] Document pre-commit hook usage

**Dependencies**: Task 4.2
**Estimated Time**: 1-2 hours
**Validation**: Hook prevents failing commits

### 4.5 Add Helper Scripts

**Files**: `scripts/check_simulation_results.py`, `scripts/verify_simulation_setup.py`

**Tasks**:
- [x] Create `check_simulation_results.py` script
- [x] Add result validation logic
- [x] Create `verify_simulation_setup.py` script
- [x] Add environment verification
- [x] Add dependency checks
- [x] Add configuration validation
- [x] Document helper script usage

**Dependencies**: Phase 4 completion
**Estimated Time**: 2-3 hours
**Validation**: Scripts provide helpful diagnostics

### 4.6 Create Documentation

**Files**: `tests/simulation/README.md`, `docs/SIMULATION_TESTING_GUIDE.md`

**Tasks**:
- [x] Create simulation testing README
- [x] Document test case format
- [x] Document 5 core test scenarios
- [x] Create comprehensive testing guide
- [x] Add troubleshooting section
- [x] Document CLI usage
- [x] Add CI/CD integration guide
- [x] Create example test cases

**Dependencies**: All previous tasks
**Estimated Time**: 3-4 hours
**Validation**: Documentation is clear and comprehensive

## Final Integration & Validation (1-2 days)

### 5.1 End-to-End Testing

**Tasks**:
- [x] Run complete test suite end-to-end
- [x] Validate all 5 core scenarios
- [x] Test CI/CD pipeline locally
- [x] Verify report generation accuracy
- [x] Test CLI tool functionality
- [x] Validate performance metrics
- [x] Test error recovery mechanisms

**Dependencies**: All previous tasks
**Estimated Time**: 4-5 hours
**Validation**: All acceptance criteria met

### 5.2 Performance Validation

**Tasks**:
- [x] Measure simulation execution time (<5 seconds per test)
- [x] Measure report generation time (<1 second per report)
- [x] Monitor memory usage (<100MB per test)
- [x] Test CI pipeline execution time
- [x] Run stability test (100 consecutive runs)
- [x] Validate caching effectiveness
- [x] Profile and optimize bottlenecks

**Dependencies**: Task 5.1
**Estimated Time**: 3-4 hours
**Validation**: All performance targets met

### 5.3 Documentation Completion

**Tasks**:
- [x] Update CLAUDE.md with simulation testing section
- [x] Update project README with testing badges
- [x] Create architecture diagrams
- [x] Document deployment process
- [x] Create training materials
- [x] Record demo videos (optional)

**Dependencies**: Task 5.2
**Estimated Time**: 3-4 hours
**Validation**: Documentation is complete and accurate

### 5.4 Release Preparation

**Tasks**:
- [x] Create release notes
- [x] Tag release version
- [x] Update CHANGELOG.md
- [x] Create migration guide (if needed)
- [x] Prepare announcement
- [x] Plan post-release monitoring

**Dependencies**: Task 5.3
**Estimated Time**: 2-3 hours
**Validation**: Release materials ready

## Task Dependencies Summary

```
Phase 1: MockVisionService Enhancement
├── 1.1 PageAnalyzer (no dependencies)
├── 1.2 MockVisionService (depends on 1.1)
├── 1.3 PageAnalyzer Tests (depends on 1.1, 1.2)
└── 1.4 MockVisionService Tests (depends on 1.2, 1.3)

Phase 2: SimulationRunner Completion
├── 2.1 MockActionExecutor (no dependencies)
├── 2.2 SimulationRunner (depends on 2.1, Phase 1)
├── 2.3 Visualization (depends on 2.2)
├── 2.4 MockActionExecutor Tests (depends on 2.1)
└── 2.5 SimulationRunner Tests (depends on 2.2, 2.3, 2.4)

Phase 3: Test Framework Establishment
├── 3.1 Test Case Format (no dependencies)
├── 3.2 Core Fixtures (depends on 3.1)
├── 3.3 TraceAsserter (no dependencies)
├── 3.4 SimulationTestRunner (depends on 3.2, 3.3, Phase 2)
├── 3.5 Pytest Integration (depends on 3.4)
└── 3.6 TraceAsserter Tests (depends on 3.3)

Phase 4: CI Ecosystem Integration
├── 4.1 ReportGenerator (depends on Phase 3)
├── 4.2 CLI Tool (depends on 4.1)
├── 4.3 GitHub Actions (depends on 4.2)
├── 4.4 Pre-commit Hook (depends on 4.2)
├── 4.5 Helper Scripts (depends on Phase 4 completion)
└── 4.6 Documentation (depends on all Phase 4 tasks)

Final Integration
├── 5.1 End-to-End Testing (depends on all previous)
├── 5.2 Performance Validation (depends on 5.1)
├── 5.3 Documentation (depends on 5.2)
└── 5.4 Release Preparation (depends on 5.3)
```

## Acceptance Criteria Summary

### Phase 1 Completion
- [x] PageAnalyzer all methods test coverage >80%
- [x] MockVisionService path mapping tests pass
- [x] Context integration tests pass
- [x] Performance test: single analysis <10ms

### Phase 2 Completion
- [x] SimulationRunner integration tests pass
- [x] All result extraction methods verified
- [x] MockActionExecutor recording completeness
- [x] End-to-end test execution successful

### Phase 3 Completion
- [x] All 5 standard fixture tests pass
- [x] TraceAsserter assertion accuracy 100%
- [x] Pytest integration without issues
- [x] New test case addition time <30 minutes

### Phase 4 Completion
- [x] CI pipeline runs successfully
- [x] Report generation tests pass
- [x] CLI tool functionality complete
- [x] Development workflow integration verified

### Final Completion
- [x] All performance targets met
- [x] All acceptance criteria validated
- [x] Documentation complete
- [x] Release ready

## Risk Mitigation

### High-Priority Risks
1. **GraphTraversalEngine interface changes**
   - Mitigation: Version isolation, adapter pattern
   - Contingency: Phase 2 buffer time (1 day)

2. **Performance targets not met**
   - Mitigation: Early performance testing, optimization reserved
   - Contingency: Performance optimization sprint (2 days)

3. **CI integration issues**
   - Mitigation: Local validation, gradual deployment
   - Contingency: Manual testing fallback (1 day)

### Medium-Priority Risks
1. **Test case maintenance difficulties**
   - Mitigation: Documentation, templated tools
   - Contingency: Maintenance guide update (0.5 day)

2. **Cross-platform compatibility**
   - Mitigation: Matrix testing, containerization
   - Contingency: Platform-specific fixes (1 day)

## Success Metrics

### Quantitative Metrics
- **Test Reliability**: 100% simulation success rate
- **Development Speed**: 10x improvement in testing iteration
- **Test Coverage**: 5x increase in automated scenarios
- **CI Integration**: 100% automated quality gates
- **Maintenance Cost**: 100% reduction in AI API costs
- **Debugging Efficiency**: 5x improvement in failure location

### Qualitative Metrics
- **Developer Experience**: Intuitive test case creation
- **Debugging Experience**: Clear, actionable failure information
- **CI/CD Integration**: Seamless workflow integration
- **Documentation Quality**: Comprehensive guides and examples
- **Code Quality**: Maintainable, extensible architecture