# Proposal: Scroll-Enabled Baseline Test

## Why

Current baseline tests (`SimulationBaselineTests.cs`) only cover non-scrolling scenarios using `StatefulMockVisionService`. The scroll simulation enhancement has been fully implemented (change `scroll-simulation-enhancement` archived), but there are no integration-level baseline tests demonstrating scroll functionality in real traversal flows. We need scroll-enabled baseline tests to validate the complete scroll integration across all traversal layers.

## What Changes

- **New Test Class**: `ScrollableBaselineTests.cs` with 6 scroll scenarios
  - Full screen traversal (7 screens, 25 elements)
  - Scroll-back-to-top (upward scroll verification)
  - Element deduplication (overlapping elements)
  - Boundary conditions (progress 0.0/1.0, IsEndOfList)
  - Jump detection and recovery (sparse segments)
  - Adaptive step optimization (high overlap segments)

- **New Fixtures**: 3 scroll data fixtures
  - Main WiFi list fixture (7 screens, 25 elements with overlap)
  - Sparse jump fixture (4 segments with gaps)
  - Overlapping adaptive fixture (high overlap for step growth)

- **ExpectedBehavior Extensions**: Add scroll-specific metrics to `numericAnchor`
  - `scrollCount`, `scrollDistance`, `scrollUpCount`
  - `jumpDetected`, `jumpRecovered`
  - `finalProgress`, `adaptiveStepIncreases`

- **Test Fixtures Directory**: `tests/Baseline/Fixtures/expected/scroll/` for 6 ExpectedBehavior JSON files

## Capabilities

### New Capabilities
None (this extends existing baseline testing capability)

### Modified Capabilities
- **simulation-baseline**: Extend baseline testing capability with scroll-enabled scenarios. The existing spec covers 2 non-scroll scenarios; this change adds 6 scroll scenarios in a new test class while keeping the same baseline testing semantics.

## Impact

- **Tests**: New `tests/UniClaw.Core.Tests/Baseline/ScrollableBaselineTests.cs` class
- **Fixtures**: 3 new scroll data fixtures (WiFi list 7-screen, sparse jump, overlapping adaptive)
- **ExpectedBehavior**: 6 new JSON files in `tests/Baseline/Fixtures/expected/scroll/`
- **Documentation**: Update `docs/system/layers/simulation-baseline.md` to add §2 scroll scenarios section
- **Dependencies**: Reuses existing scroll infrastructure (`ScrollableMockVisionService`, `ScrollHandler`, `ScrollDataStore`)
