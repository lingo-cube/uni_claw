## Why

Baseline test verification currently produces `VerificationReport` objects that exist only in memory. Developers cannot cross-compare baseline numeric changes across runs, scroll metrics (ScrollCount, JumpRecovered, etc.) are not captured in reports, and there's no structured summary output after `dotnet test`. This makes it difficult to validate engine changes against baseline expectations.

## What Changes

- **Add report generation infrastructure**: `BaselineReportCollector` (gather test results) and `BaselineReportWriter` (JSON + Markdown output)
- **Generate dual-format reports**: JSON machine-readable per scenario + Markdown human-readable index with aggregate pass rate
- **Integrate into local dev workflow**: `dotnet test` automatically writes to `tests/.../Baseline/reports/` directory
- **Add scroll metrics extraction**: Extend mock services (`ScrollableMockActionExecutor`, `ScrollableMockVisionService`) with methods to extract scroll-related metrics
- **Add mock service methods**: `GetScrollUpCount()`, `GetScrollDistance()` (others deferred to Phase 3)

## Capabilities

### New Capabilities
- `baseline-report-generation`: Generate and persist baseline test reports in JSON (per-scenario) and Markdown (index.md) formats
- `scroll-metrics-extraction`: Extract scroll-related metrics from mock services for reporting

### Modified Capabilities
- `simulation-baseline`: Add report collection integration to existing baseline tests (non-breaking test changes)

## Impact

**Affected Code**:
- `tests/UniClaw.Core.Tests/Baseline/SimulationBaselineTests.cs` - Add `Collector.Add()` calls (2 lines)
- `tests/UniClaw.Core.Tests/Baseline/ScrollableBaselineTests.cs` - Add `Collector.Add()` calls with mock service params (6 tests × ~2 lines)
- `src/UniClaw.Core/Simulation/Scroll/ScrollableMockActionExecutor.cs` - Add `GetScrollUpCount()` method
- `src/UniClaw.Core/Simulation/Scroll/ScrollableMockVisionService.cs` - Add `GetScrollDistance()` method

**New Files**:
- `tests/UniClaw.Core.Tests/Baseline/BaselineReportCollector.cs` - Collection fixture + data aggregation
- `tests/UniClaw.Core.Tests/Baseline/BaselineReportWriter.cs` - JSON + Markdown output
- `tests/UniClaw.Core.Tests/Baseline/reports/.gitkeep` - Reports output directory (gitignored)

**API Changes**:
- No production API changes - all additions are in test infrastructure

**Dependencies**:
- No new external dependencies (uses System.Text.Json, System.IO)

**Systems**:
- Baseline test execution flow: `TraversalResult → Verify → Collector → ReportWriter → reports/`
