# Design: Test Architecture Standardization Enhanced (V6.2)

**Status**: Ready for Implementation
**Created**: 2026-06-06
**Version**: Enhanced (with Dashboard Integration)
**Estimated Time**: 15 hours (2 working days)

---

## Executive Summary

This design establishes a complete closed-loop system: **standardized test results → JSON generation → Dashboard visualization → test triggering**.

The solution adds approximately 1,150 lines of code across 6 phases, maintaining minimal invasiveness while providing comprehensive testing infrastructure.

---

## Context

### Current State
- Uni-Claw has V6 with 17+ modules and comprehensive testing infrastructure
- Test results fragmented across multiple formats (stdout, various plugins)
- Manual validation documentation generation takes ~30 minutes
- No unified visualization of test status across modules
- No ability to trigger tests from a centralized interface

### Key Constraints
- Code changes must be minimal and focused
- 100% backwards compatible with existing workflows
- Must work with or without pytest-json-report plugin
- AI-friendly data structure for automated analysis
- Dashboard must be simple and maintainable

### Stakeholders
- **Development Team**: Reliable test results and quick test re-execution
- **AI Validation System**: Structured data for automated reporting
- **CI/CD Pipeline**: Standardized outputs for integration
- **Project Managers**: Visibility into test status via Dashboard

---

## Goals / Non-Goals

### Goals
1. ✅ Establish minimal JSON contract for unit test results
2. ✅ Auto-generate standardized JSON during test execution
3. ✅ Provide Dashboard for real-time test status visualization
4. ✅ Enable test triggering from Dashboard interface
5. ✅ Maintain 100% backwards compatibility
6. ✅ Support comprehensive unit testing of new code

### Non-Goals
- ❌ Modifying existing test code or test logic
- ❌ Complex historical data storage
- ❌ Mandatory new dependencies
- ❌ Enforcing schema validation at runtime
- ❌ Complex authentication/authorization for Dashboard

---

## Architecture Overview

### System Components

```
┌─────────────────────────────────────────────────────────────┐
│                     Test Execution Layer                      │
├─────────────────────────────────────────────────────────────┤
│  test_runner.py → Module Tests → pytest-json-report/stdout   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Data Transformation                        │
├─────────────────────────────────────────────────────────────┤
│  _convert_from_raw() / _convert_from_stdout()               │
│  → test_results/{module}_unit.json (Minimal Contract)       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      Data Consumption                          │
├─────────────────────────────────────────────────────────────┤
│  ┌──────────────────┐          ┌──────────────────┐        │
│  │  Dashboard       │          │  AI Validation    │        │
│  │  (Visualization) │          │  (Documentation)  │        │
│  └──────────────────┘          └──────────────────┘        │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      Test Triggering                           │
├─────────────────────────────────────────────────────────────┤
│  Dashboard → TestRunnerAPI → Background Test Execution        │
└─────────────────────────────────────────────────────────────┘
```

---

## Key Design Decisions

### Decision 1: Minimal JSON Contract with Schema Version

**Choice**: 5 core fields + schema_version

```json
{
  "schema_version": "1.0",
  "module": "simulation",
  "timestamp": "2026-06-06T12:00:00Z",
  "summary": {"total": 50, "passed": 48, "failed": 2, "error": 0, "skipped": 0},
  "failures": [
    {"name": "test_name", "message": "error", "type": "failure"}
  ],
  "coverage": {"line_rate": 0.87, "branch_rate": 0.74}
}
```

**Rationale**:
- `schema_version` enables future evolution and compatibility checks
- Minimal fields reduce implementation complexity
- Clear separation of required vs. optional fields
- Extension fields allowed (documented but not enforced)

### Decision 2: Hybrid Generation with Graceful Degradation

**Choice**: pytest-json-report (primary) + stdout parsing (fallback)

**Implementation**:
```python
def _generate_standard_result(self, module: str) -> Dict[str, Any]:
    # Try plugin method first
    if raw_file.exists():
        return self._convert_from_raw(raw_data, module)

    # Fallback to stdout parsing
    if self.last_stdout:
        return self._convert_from_stdout(self.last_stdout, module)

    raise RuntimeError("Unable to generate result")
```

**Rationale**:
- Primary method provides reliable structured data
- Fallback ensures 100% availability
- "Log and continue" error handling
- Does not block test execution on failure

### Decision 3: Extended Dashboard with Test Triggering

**Choice**: Dashboard serves as both visualization AND control interface

**Features**:
- Real-time test status display
- Module-level test cards
- Failure details table
- Data freshness warnings
- "Re-run Test" buttons per module
- Automatic refresh on test completion

**Rationale**:
- Single interface for visibility and action
- Reduces context switching between terminal and browser
- Enables quick re-testing of specific modules
- HTTP-based architecture for simplicity

### Decision 4: Complete Unit Testing Coverage

**Choice**: Full unit test suite for all new code

**Test Files**:
- `test_json_contract.py` - Schema validation
- `test_convert_from_raw.py` - Plugin conversion
- `test_convert_from_stdout.py` - Fallback parser
- `test_standard_result.py` - Main workflow
- `test_dashboard_integration.py` - Dashboard functionality

**Rationale**:
- stdout parsing is fragile and needs thorough testing
- Ensures reliability of critical data pipeline
- Facilitates future refactoring
- Maintains code quality standards

---

## Data Contract Specification

### File Structure
```
test_results/
├── schema/
│   └── unit_result.schema.json   # Reference documentation
├── {module}_unit.json            # Standardized results (required)
└── {module}_coverage.xml         # Coverage data (optional)
```

### Schema Definition

**Required Fields**:
| Field | Type | Constraints | AI Purpose |
|-------|------|-------------|------------|
| `schema_version` | string | `\d+\.\d+` | Compatibility checking |
| `module` | string | `^[a-z][a-z0-9_]*$` | Module identification |
| `timestamp` | string | ISO-8601 UTC | Freshness checking |
| `summary.total` | int | ≥0 | Aggregation |
| `summary.passed` | int | ≥0 | Statistics |
| `summary.failed` | int | ≥0 | Statistics |
| `summary.error` | int | ≥0 | Statistics |
| `summary.skipped` | int | ≥0 | Statistics |
| `failures` | array | - | Failure analysis |

**Optional Fields**:
| Field | Type | Constraints | Purpose |
|-------|------|-------------|---------|
| `coverage.line_rate` | float | 0.0-1.0 | Coverage metrics |
| `coverage.branch_rate` | float | 0.0-1.0 | Coverage metrics |

**Extension Fields** (documented but not enforced):
- `test_files` - Test file breakdown
- `modules_tested` - Module coverage list
- Custom module-specific fields

---

## Implementation Phases

### Phase 1: Infrastructure (1 hour)

**Deliverables**:
1. `test_results/` directory structure
2. `test_results/README.md` with usage guidelines
3. `test_results/schema/unit_result.schema.json` reference file

**Code**: ~100 lines (documentation)

### Phase 2: Core Code Changes (3 hours)

**Target File**: `.claude/skills/module-test/test_runner.py`

**Changes**:
1. Update `_build_test_command()` to add JSON output flags
2. Add `_generate_standard_result()` method
3. Add `_convert_from_raw()` method (~60 lines)
4. Add `_convert_from_stdout()` method (~70 lines)
5. Add `_write_final_json()` method (~15 lines)
6. Update `_run_single_module()` to trigger generation

**Code**: ~270 lines

### Phase 3: Unit Tests (3 hours)

**Files Created**:
- `tests/v6/test_unit/test_json_contract.py`
- `tests/v6/test_unit/test_convert_from_raw.py`
- `tests/v6/test_unit/test_convert_from_stdout.py`
- `tests/v6/test_unit/test_standard_result.py`

**Coverage Goals**:
- 90%+ line coverage for new code
- All edge cases tested
- Regex patterns validated against multiple pytest versions

**Code**: ~400 lines

### Phase 4: Skill Documentation (2 hours)

**Files Updated**:
1. `.claude/skills/module-test/SKILL.md`
   - Add "Standardized Output" section
   - Document JSON generation behavior

2. `.claude/skills/validation-documentation/SKILL.md`
   - Add "Standardized Data Input" section
   - Document data ingestion protocol

**Code**: ~80 lines (documentation)

### Phase 5: Dashboard Integration (4 hours)

**Files Created**:
1. `dashboards/test_report_server.py` (~300 lines)
   - TestResultsDataSource class
   - TestResultsAnalyzer class
   - TestRunnerAPI class
   - HTTP request handling
   - HTML generation

2. `dashboards/templates/test_report.html` (embedded)
   - Module status cards
   - Global statistics
   - Failure details table
   - Re-run buttons
   - Auto-refresh logic

**Features**:
- Load and aggregate test results
- Calculate global statistics
- Detect stale data (>48 hours)
- Trigger test execution via API
- Real-time status polling

**Code**: ~300 lines

### Phase 6: Quality Verification (2 hours)

**Activities**:
1. End-to-end integration testing
2. Dashboard functionality verification
3. Performance testing (<1s load time)
4. Documentation final review
5. Acceptance criteria validation

---

## Dashboard Design Specification

### User Interface Layout

```
┌─────────────────────────────────────────────────────────┐
│  🧪 Uni-Claw 测试报告          [更新: 2026-06-06 12:00]  │
├─────────────────────────────────────────────────────────┤
│  ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐                       │
│  │ 150 │ │ 98% │ │  2  │ │  5  │  全局统计卡片          │
│  │总数 │ │通过 │ │失败 │ │模块 │                       │
│  └─────┘ └─────┘ └─────┘ └─────┘                       │
├─────────────────────────────────────────────────────────┤
│  ⚠️ 陈旧数据: module_a, module_b (>48小时)              │
├─────────────────────────────────────────────────────────┤
│  模块状态矩阵                                            │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐                │
│  │simulation│ │   trace  │ │    ai    │                │
│  │ ✓ 33     │ │ ✓ 123    │ │ ✗ 2/15   │                │
│  │[重新运行]│ │[重新运行]│ │[运行中⏳]│                │
│  └──────────┘ └──────────┘ └──────────┘                │
├─────────────────────────────────────────────────────────┤
│  失败详情                                                │
│  │ 模块    │ 测试名称                  │ 错误信息       ││
│  │ ai      │ test_llm_response        │ Timeout...     ││
│  │ ai      │ test_vision_analysis     │ AssertionError││
│  └─────────────────────────────────────────────────────┘
│                                                           │
│  [自动刷新: 30秒]  [立即刷新所有]                        │
└─────────────────────────────────────────────────────────┘
```

### API Endpoints

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/` | GET | Display main dashboard |
| `/api/test/run/{module}` | POST | Trigger test execution |
| `/api/test/status/{module}` | GET | Check test status |

### Color Coding

- 🟢 **Green**: All tests passing
- 🔴 **Red**: Tests failing
- 🟡 **Yellow**: Tests running
- ⚠️ **Warning**: Stale data detected

---

## Risk Analysis & Mitigation

### Risk 1: Stdout Parser Fragility
**Risk Level**: Medium
**Impact**: Fallback parser may fail with pytest version changes
**Mitigation**:
- Comprehensive unit tests with multiple pytest versions
- Minimal regex patterns targeting stable output sections
- Primary method uses plugin (more stable)
- Fallback failure is non-blocking

### Risk 2: Concurrent Test Execution
**Risk Level**: Low
**Impact**: `self.last_stdout` could be overwritten in parallel runs
**Mitigation**:
- Use `self.stdout_cache = {}` dictionary instead of single variable
- Module-level isolation prevents cross-contamination

### Risk 3: Dashboard File Watching
**Risk Level**: Low
**Impact**: Dashboard may show stale data during test execution
**Mitigation**:
- Auto-refresh every 30 seconds
- Manual refresh button
- "Running" status prevents premature refresh

### Risk 4: JSON Schema Evolution
**Risk Level**: Low
**Impact**: Future schema changes may break compatibility
**Mitigation**:
- `schema_version` field enables version detection
- AI validation can handle multiple schema versions
- Document extension field policy

---

## Success Metrics

### Technical Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| JSON generation success rate | ≥98% | Automated monitoring |
| Schema compliance | 100% | Optional validation tool |
| End-to-end success rate | ≥95% | Integration tests |
| Dashboard load time | <1s | Performance testing |
| Test trigger success rate | ≥95% | API monitoring |

### Business Metrics

| Metric | Before | After | Measurement |
|--------|--------|-------|-------------|
| Validation report generation | 30min | 30sec | Time measurement |
| Test re-execution effort | Manual | 1-click | User feedback |
| Cross-module visibility | Low | High | Dashboard usage |
| Data freshness tracking | Manual | Automatic | Timestamp checking |

---

## Migration & Deployment

### Deployment Steps

1. **Infrastructure Setup**
   ```bash
   mkdir -p test_results/schema
   ```

2. **Code Deployment**
   - Deploy updated `test_runner.py`
   - Deploy new test files
   - Deploy Dashboard server

3. **Documentation Update**
   - Update skill documentation
   - Update README files

4. **Verification**
   - Run existing module tests
   - Verify JSON generation
   - Test Dashboard functionality
   - Validate end-to-end workflow

### Rollback Strategy

- Changes are purely additive
- Removing JSON generation code restores previous behavior
- No database or external state changes
- Safe to deploy and rollback without side effects

---

## Open Questions

**None identified** - All technical decisions are documented and implementation path is clear from this specification.

---

## Appendices

### Appendix A: File Changes Summary

| File Type | Files | Lines Added | Lines Modified |
|-----------|-------|-------------|----------------|
| Documentation | 3 | +100 | 0 |
| Core Logic | 1 | +270 | ~50 |
| Unit Tests | 4 | +400 | 0 |
| Dashboard | 1 | +300 | 0 |
| Skill Docs | 2 | +80 | 0 |
| **Total** | **11** | **~1,150** | **~50** |

### Appendix B: Dependencies

**Required**:
- Python ≥ 3.8
- pytest ≥ 7.0

**Recommended**:
- pytest-json-report ≥ 1.5
- pytest-cov ≥ 4.0

**Dashboard**:
- No additional dependencies (uses standard library)

### Appendix C: Testing Checklist

Before considering implementation complete:

- [ ] All unit tests passing
- [ ] JSON generates for all modules
- [ ] Schema validation passes
- [ ] Dashboard loads correctly
- [ ] Test triggering works
- [ ] Auto-refresh functions
- [ ] Fallback parser tested
- [ ] Documentation updated
- [ ] Acceptance criteria met

---

**Design Approval**: Pending user review
**Implementation Status**: Not started
**Last Updated**: 2026-06-06
