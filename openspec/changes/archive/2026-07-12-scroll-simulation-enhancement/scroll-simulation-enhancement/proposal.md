# Proposal: Scroll Simulation Enhancement

## Why

The current C# simulation infrastructure lacks scroll support, preventing testing of scrollable list scenarios (e.g., WiFi list traversal). `StatefulMockVisionService` returns fixed element sets with static `IsEndOfList` values, making it impossible to verify scroll logic or element deduplication across scroll positions. This change adds comprehensive scroll simulation capabilities to enable testing of real-world list traversal scenarios.

## What Changes

This change introduces scroll simulation infrastructure for the C# UniClaw.Core project:

- **Scroll Data Models**: Core types for scroll simulation including `ScrollSegment` (threshold-bounded element sets), `ScrollState` (progress tracking), `ScrollDataStore` (segment management), and scroll validation types
- **ScrollHandler 7-step Pipeline**: Detect (scrollability), Classify (scroll decision), Decide (action type), Execute (scroll action), Verify (jump detection), Recover (rollback and retry), Statistics (collection)
- **ScrollableMockVisionService**: Enhanced mock vision service that returns different element sets based on scroll progress using accumulation mode (all segments with `threshold <= progress` are visible)
- **ScrollableMockActionExecutor**: Mock action executor that performs scroll operations and updates scroll state
- **Adaptive Step Calculation**: Algorithm that increases scroll step when duplicate element ratio exceeds threshold (reducing redundant scrolling)
- **Jump Detection and Recovery**: Core chain mechanism that detects element discontinuity during scroll and recovers by rolling back and retrying with reduced step size
- **Element Deduplication**: Ensures same-element IDs across multiple segments only appear once (lower threshold preferred)
- **19 Scroll Scenarios**: Comprehensive test coverage including basic, boundary, element, step size, and jump scenarios

## Implementation Notes (2026-07-12)

### Completed Implementation

**Core Infrastructure:**
- ✅ Data Models (11 types): ScrollSegment, ScrollState, ScrollAction, ScrollDataStore, OverlapStatus, ScrollVerifyResult, JumpRecoveryResult, ScrollHandlerConfig, ScrollContext, ScrollActionResult, ScrollSegmentBuilder
- ✅ ScrollHandler 7-step Pipeline: Detect → Classify → Decide → Execute → Verify → Recover → Statistics
- ✅ Mock Services: ScrollableMockVisionService (accumulation mode + deduplication), ScrollableMockActionExecutor (scroll execution)
- ✅ Adaptive Strategies: Adaptive step calculation with duplicate ratio detection, jump recovery with step reduction

**Test Coverage:**
- ✅ 683 tests passing (0 failures, 0 functional warnings)
- ✅ 19 scroll scenarios covered (basic, boundary, element, step size, jump)
- ✅ Data model unit tests
- ✅ ScrollHandler component unit tests
- ✅ Backward compatibility verified (existing non-scroll tests pass unchanged)

**Documentation:**
- ✅ Updated `docs/system/layers/simulation-baseline.md` with scroll simulation section
- ✅ Updated `docs/system/layers/state-machine.md` with ScrollHandler integration documentation

**Deferred to Phase 2.5:**
- FSM Integration (Section 7): Integration point in `TraversalFSM.HandleBranch()` to trigger scroll when all children visited
- This requires coordination with the main traversal flow and is deferred for architectural review

### Files Created

**Production Code (19 files):**
```
src/UniClaw.Core/Simulation/Scroll/
  ScrollSegment.cs
  ScrollState.cs
  ScrollAction.cs
  ScrollDataStore.cs
  OverlapStatus.cs
  ScrollVerifyResult.cs
  JumpRecoveryResult.cs
  ScrollHandlerConfig.cs
  ScrollContext.cs
  ScrollActionResult.cs
  ScrollSegmentBuilder.cs
  ScrollExtensions.cs
  ScrollableMockVisionService.cs
  ScrollableMockActionExecutor.cs

src/UniClaw.Core/StateMachine/Scroll/
  ScrollabilityDetector.cs
  ScrollClassifier.cs
  ScrollDecider.cs
  ScrollActionExecutor.cs
  JumpDetector.cs
  JumpRecoveryHandler.cs
  AdaptiveStepCalculator.cs
  ScrollStatisticsCollector.cs
  ScrollHandler.cs
```

**Test Code (3 files):**
```
tests/UniClaw.Core.Tests/Simulation/Scroll/
  ScrollDataModelTests.cs
  ScrollHandlerComponentTests.cs
  ScrollScenarioTests.cs
```

### Key Design Decisions Confirmed

1. **Accumulation Mode**: All segments with `Threshold <= CurrentProgress` are visible — matches Python V7.0 behavior
2. **Element Deduplication**: Same ID → lowest threshold instance — prevents duplicate visits
3. **Jump Detection as Core Chain**: Jump detection and recovery is operational logic, not test validation
4. **Adaptive Step Calculation**: Increases step when duplicate ratio >= 70% with min sample size of 3
5. **Scrollable Services as Extensions**: Separate classes (ScrollableMockVisionService, ScrollableMockActionExecutor) — backward compatible

## Capabilities

### New Capabilities

- `scroll-data-models`: Core scroll data structures (ScrollSegment, ScrollState, ScrollAction, ScrollDataStore, validation types, and configuration)

- `scroll-handler`: 7-step pipeline for scroll handling (detect, classify, decide, execute, verify, recover, statistics) with jump detection and recovery as core chain logic

- `scrollable-mock-services`: Mock services for scroll simulation including ScrollableMockVisionService (accumulation mode element collection, dynamic HasScroll/IsEndOfList calculation) and ScrollableMockActionExecutor (scroll action execution)

- `scroll-adaptive-strategies`: Adaptive step calculation (increases step on high duplicate ratio) and jump recovery (rollback with half-step retry) strategies with configurable parameters

### Modified Capabilities

None. This is a new feature addition that does not modify existing spec-level behavior.

## Impact

**Affected Code**:
- `src/UniClaw.Core/Simulation/Scroll/` — New namespace for scroll data models
- `src/UniClaw.Core/StateMachine/Scroll/` — New ScrollHandler and subcomponents
- `src/UniClaw.Core.Tests/Simulation/Scroll/` — New test namespace for scroll scenarios
- `StatefulMockVisionService` — Extended to ScrollableMockVisionService (backward compatible)
- `StatefulMockActionExecutor` — Extended to ScrollableMockActionExecutor (backward compatible)

**API Changes**:
- New public types in `UniClaw.Core.Simulation.Scroll` namespace
- New public types in `UniClaw.Core.StateMachine.Scroll` namespace
- ScrollHandlerConfig with all parameters configurable (default values provided)

**Dependencies**:
- No new external dependencies
- Uses existing System.Collections.Immutable for immutable collections

**Backward Compatibility**:
- Existing non-scroll tests continue to work without modification
- ScrollableMockVisionService and ScrollableMockActionExecutor are opt-in extensions, not replacements
