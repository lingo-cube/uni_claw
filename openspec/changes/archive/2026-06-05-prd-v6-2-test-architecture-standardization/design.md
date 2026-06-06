# Design: Test Architecture Standardization

## Context

**Current State**
The Uni-Claw project has grown to V6 with 17+ modules and comprehensive testing infrastructure. However, test results are currently output in multiple formats:
- pytest stdout (human-readable)
- Various pytest plugins (different JSON formats)
- Manual validation documentation

This fragmentation creates manual overhead when generating validation reports and prevents AI from systematically analyzing test results across modules.

**Constraints**
- Code changes must be minimal (<100 lines)
- 100% backwards compatible with existing test workflows
- No breaking changes to existing test execution
- Must work with or without pytest-json-report plugin
- AI-friendly data structure for automated analysis

**Stakeholders**
- Development team: Needs reliable test results for quality assurance
- AI validation system: Needs structured data for automated reporting
- CI/CD pipeline: Needs standardized outputs for integration

## Goals / Non-Goals

**Goals:**
- Establish a minimal JSON contract for unit test results across all modules
- Modify test_runner.py to automatically generate standardized JSON during test execution
- Provide fallback mechanism when pytest-json-report is unavailable
- Enable AI to consume test results for automated validation documentation
- Maintain 100% backwards compatibility with existing workflows

**Non-Goals:**
- Modifying existing test code or test logic
- Changing how tests are executed or discovered
- Requiring new mandatory dependencies (pytest-json-report is recommended, not required)
- Complex data validation or enforcement (schema is reference only)
- Historical data storage or test result tracking over time

## Decisions

### Decision 1: Minimal JSON Contract (5 Core Fields)

**Choice**: Define a minimal schema with only 5 core fields:
- `module`: Module identifier
- `timestamp`: ISO-8601 UTC timestamp
- `summary`: Test counts (total, passed, failed, error, skipped)
- `failures`: Array of failure details
- `coverage`: Optional coverage metrics

**Rationale**: 
- Minimal schema reduces implementation complexity and AI parsing overhead
- Only fields needed for validation reporting are included
- Extensible design allows future additions without breaking changes
- Follows "data contract" pattern for clean module boundaries

**Alternatives Considered**:
- **Full pytest-json-report schema**: Rejected as too complex with many unused fields
- **Custom comprehensive schema**: Rejected due to implementation overhead

### Decision 2: Hybrid Generation Strategy (Plugin + Fallback)

**Choice**: Primary method using pytest-json-report plugin with stdout parsing as automatic fallback

**Rationale**:
- Plugin method provides reliable, structured data when available
- Fallback ensures system works without new dependencies
- Graceful degradation maintains 100% availability
- No breaking changes if plugin is missing

**Alternatives Considered**:
- **Plugin-only approach**: Rejected due to dependency requirement
- **stdout-only parsing**: Rejected due to fragility and limited error detail

### Decision 3: File Overwrite Strategy

**Choice**: Each test run overwrites the previous JSON file (no versioning or dates in filenames)

**Rationale**:
- Simplest implementation with no cleanup logic needed
- Git history provides historical data if needed
- Reduces complexity around file management
- Aligns with "latest results only" use case

**Alternatives Considered**:
- **Timestamped filenames**: Rejected due to cleanup complexity
- **Database storage**: Rejected as over-engineered for current needs

### Decision 4: Schema as Reference (Not Enforced)

**Choice**: JSON schema file is documentation only, not enforced by code

**Rationale**:
- Reduces implementation complexity
- Allows flexibility in edge cases
- Schema enforcement doesn't provide significant value for this use case
- AI can handle minor schema variations

**Alternatives Considered**:
- **Strict schema validation**: Rejected as unnecessary overhead

## Risks / Trade-offs

### Risk 1: Stdout Parser Fragility
**Risk**: Pytest output format changes could break fallback parser
**Mitigation**: 
- Parser only extracts core summary line and failure headers
- Minimal regex patterns reduce breakage surface
- Plugin method is primary; fallback is defensive

### Risk 2: File Permission Issues
**Risk**: test_results/ directory may not be writable in some environments
**Mitigation**:
- Explicit error messages guide users to fix permissions
- Directory creation with parents=True ensures proper structure
- Failure doesn't block test execution, only JSON generation

### Risk 3: Data Freshness
**Risk**: Old JSON files may be used for validation if tests aren't re-run
**Mitigation**:
- Timestamp field enables freshness checking
- Validation skill warns if data >48 hours old
- Clear documentation on file lifecycle

### Trade-off: Coverage Data
**Trade-off**: Coverage is optional rather than required
**Rationale**: Not all modules have coverage configured; making it optional allows broader adoption
**Impact**: Validation reports must handle missing coverage data gracefully

## Migration Plan

**Deployment Steps**:
1. Create `test_results/` directory structure and README
2. Add schema reference file (optional, documentation only)
3. Modify `test_runner.py` with JSON generation logic
4. Update skill documentation for `module-test` and `validation-documentation`
5. Test with existing modules to verify compatibility

**Rollback Strategy**:
- Changes are purely additive; removing JSON generation code restores previous behavior
- No database migrations or external state changes
- Safe to deploy and rollback without side effects

**Testing Strategy**:
1. Test with pytest-json-report available (primary path)
2. Test without plugin (fallback path)
3. Test with passing and failing test suites
4. Verify JSON output structure matches contract
5. Confirm AI can consume generated files

## Open Questions

None identified - all technical decisions are documented and implementation path is clear from PRD specifications.
