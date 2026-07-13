## Context

**Current State**: Baseline tests use `ExpectedBehavior.Verify(TraversalResult)` to generate `VerificationReport` in memory, then assert on `AllPassed`. Reports are not persisted, so developers cannot compare baseline values across runs or debug regressions easily.

**Constraints**:
- Reports must be test infrastructure only (no changes to production code)
- Must not break existing tests - only additive changes
- Should not add external dependencies
- Must handle xUnit collection lifecycle correctly

**Stakeholders**: Developers working on traversal engine changes need quick visual feedback on baseline stability.

## Goals / Non-Goals

**Goals:**
- Generate JSON per-scenario reports + Markdown index summary after every test run
- Extract and report scroll metrics (ScrollCount, ScrollDistance, ScrollUpCount)
- Integrate seamlessly into existing `dotnet test` workflow
- Keep test changes minimal (1-2 lines per test)

**Non-Goals:**
- CI artifact upload (deferred)
- Historical trend analysis or diff reports (deferred)
- Web dashboard (deferred)
- Full jump/recovery/adaptive-step detection logic (Phase 3)

## Decisions

### 1. Collector + Writer Separation

**Decision**: Separate `BaselineReportCollector` (collection + lifecycle) from `BaselineReportWriter` (serialization + I/O).

**Rationale**:
- Single Responsibility: Collector owns xUnit fixture lifecycle, Writer owns serialization
- Easier testing: Can test Writer independently with mock data
- Matches existing patterns in the codebase

**Alternatives considered**:
- Single class with both responsibilities - rejected as too coupled
- Writer static methods only - rejected for testability

### 2. xUnit Collection Fixture Lifecycle

**Decision**: Use `ICollectionFixture<BaselineReportCollector>` with `DisableParallelization = true`.

**Rationale**:
- Ensures all tests run sequentially through the same Collector instance
- xUnit guarantees `Dispose()` runs after all tests complete
- No race conditions on shared collection state

**Alternatives considered**:
- Per-class static Collector - rejected for complexity and lifecycle issues
- Parallel with locks - rejected as unnecessary complexity

### 3. actualNumeric Construction in Collector

**Decision**: Collector accepts optional `executor?` and `vision?` parameters, constructs `actualNumeric` internally by merging `TraversalResult` data with mock service metrics.

**Rationale**:
- Centralizes data extraction logic (one place to maintain)
- Keeps test code simple (1-2 lines vs 8 lines of manual extraction)
- TraversalResult already provides 70% of needed data

**Alternatives considered**:
- Tests construct and pass full actualNumeric - rejected as too verbose
- Modify TraversalResult to include scroll metrics - rejected as changes production types

### 4. Scroll Metrics from Existing Data

**Decision**: Extract scroll metrics from existing `ScrollHistory` and `ScrollState` rather than adding new state tracking.

**Rationale**:
- YAGNI: Don't add state until needed for verification logic
- `ScrollHistory` already records each scroll operation
- Jump/Recovery/Adaptive metrics return 0 for now (Phase 3 adds real detection)

**Alternatives considered**:
- Add counter fields to ScrollState - rejected as redundant with ScrollHistory
- Defer all scroll metrics - rejected as basic metrics (ScrollCount, ScrollDistance) are useful now

### 5. Error Handling: Silent Fail with Console Logging

**Decision**: Wrap all I/O in try-catch, log errors to `Console.WriteLine`, never fail tests.

**Rationale**:
- Report generation is informational - baseline quality is enforced by Assert
- Console output visible during local dev, doesn't break CI
- Individual file failures don't prevent other files from writing

**Alternatives considered**:
- Throw exceptions - rejected as would break tests
- Silent only (no Console) - rejected as leaves developers blind to issues

### 6. BaselineReport: Minimal Fields Only

**Decision**: `BaselineReport` contains only `Scenario`, `Timestamp`, `AllPassed`, `Details`, `ExpectedNumeric`, `ActualNumeric`.

**Rationale**:
- Removed `Description` (no data source), `TotalScenarios/PassedScenarios` (aggregate-level, computed during index generation)
- Simpler record with clear purpose

**Alternatives considered**:
- Include aggregate stats - rejected as these belong at collector/index level

## Risks / Trade-offs

### Risk 1: xUnit Collection Execution Order

**Risk**: Tests might not execute in expected order, affecting Collector state.

**Mitigation**: Use `ICollectionFixture` which guarantees lifecycle but NOT order. Since each test calls `Collector.Add()` independently and order doesn't matter for the final report, this is acceptable.

### Risk 2: Mock Service Type Casting

**Risk**: `Collector.Add()` casts `engine.ActionExecutor` and `engine.VisionProvider` to scroll-specific types.

**Mitigation**: Casts are safe in scroll tests (we control the test setup). For non-scroll tests, these parameters are null and handled gracefully.

### Risk 3: Disk Write Failures

**Risk**: Permission issues, disk full, or invalid paths could prevent report generation.

**Mitigation**: `Directory.CreateDirectory` + try-catch + Console logging ensures tests pass even if reports fail to write.

### Trade-off: Scroll Metrics Incomplete

**Trade-off**: Jump/Recovery/Adaptive metrics return 0 instead of real values.

**Acceptance**: These are informational only in Phase 1. Blocking verification happens in Phase 3 when real detection logic is added. Current value is having basic scroll metrics (ScrollCount, ScrollDistance) reported.

## Migration Plan

**Phase 1** (P0):
- Add `BaselineReportCollector.cs` and `BaselineReportWriter.cs`
- Add `GetScrollUpCount()` to `ScrollableMockActionExecutor`
- Add `GetScrollDistance()` to `ScrollableMockVisionService`
- Update `.gitignore` for `reports/` directory

**Phase 2** (P1):
- Integrate into `SimulationBaselineTests.cs` (2 tests × 1 line)
- Integrate into `ScrollableBaselineTests.cs` (6 tests × 2 lines)

**Phase 3** (P2):
- Extend `ExpectedBehavior.Verify.cs` `VerifyNumericAnchor` to check scroll fields
- Add jump/recovery detection logic if needed

**Phase 4** (P3):
- Update `docs/system/layers/simulation-baseline.md`
- Add decision record to `docs/system/decisions/log.md`

**Rollback**: Delete the two new files and remove `Collector.Add()` lines from tests. No production code changes.

## Open Questions

None - design decisions complete based on PRD review and brainstorming session.
