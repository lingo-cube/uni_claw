# Tasks: Scroll Simulation Enhancement

## 1. Data Model Implementation

- [x] 1.1 Create `src/UniClaw.Core/Simulation/Scroll/` namespace directory
- [x] 1.2 Implement `ScrollSegment` record with threshold validation
- [x] 1.3 Implement `ScrollState` record with progress clamping
- [x] 1.4 Implement `ScrollAction` record with timestamp
- [x] 1.5 Implement `ScrollDataStore` class with page-keyed segment storage
- [x] 1.6 Implement `OverlapStatus` enum with 5 states
- [x] 1.7 Implement `ScrollVerifyResult` record
- [x] 1.8 Implement `JumpRecoveryResult` record
- [x] 1.9 Implement `ScrollHandlerConfig` record with all parameters
- [x] 1.10 Implement `ScrollContext` record
- [x] 1.11 Implement `ScrollActionResult` record
- [x] 1.12 Add data model unit tests

## 2. Builder Extensions

- [x] 2.1 Create `ScrollSegmentBuilder` for fluent segment construction
- [x] 2.2 Add `PageStateBuilder.ScrollSegments()` extension method
- [x] 2.3 Verify backward compatibility (non-scroll fixtures work unchanged)

## 3. ScrollableMockVisionService

- [x] 3.1 Create `ScrollableMockVisionService` class extending vision service pattern
- [x] 3.2 Implement scroll state management per page (`_scrollStates` dictionary)
- [x] 3.3 Implement `ScrollDataStore` integration
- [x] 3.4 Implement accumulation mode element collection (threshold-based visibility)
- [x] 3.5 Implement element deduplication (ID-based, lowest threshold wins)
- [x] 3.6 Implement `IsEndOfList` dynamic calculation (progress vs max threshold)
- [x] 3.7 Implement `HasScroll` calculation (scroll data existence check)
- [x] 3.8 Implement `GetScrollProgress(string pageId)` method
- [x] 3.9 Implement `SimulateScroll(double delta)` with progress update and history recording
- [x] 3.10 Add `ScrollableMockVisionService` unit tests

## 4. ScrollableMockActionExecutor

- [x] 4.1 Create `ScrollableMockActionExecutor` class
- [x] 4.2 Implement `ScrollDown(double stepPercent)` method
- [x] 4.3 Implement `ScrollUp(double stepPercent)` method
- [x] 4.4 Implement scroll action recording
- [x] 4.5 Integrate with `ScrollableMockVisionService.SimulateScroll`
- [x] 4.6 Add `ScrollableMockActionExecutor` unit tests

## 5. ScrollHandler Components

- [x] 5.1 Create `src/UniClaw.Core/StateMachine/Scroll/` namespace directory
- [x] 5.2 Implement `ScrollabilityDetector` (Step 1: Detect)
- [x] 5.3 Implement `ScrollClassifier` (Step 2: Classify)
- [x] 5.4 Implement `ScrollDecider` (Step 3: Decide)
- [x] 5.5 Implement `ScrollActionExecutor` with Hook Dispatch table (Step 4: Execute)
- [x] 5.6 Implement `JumpDetector` (Step 5: Verify)
- [x] 5.7 Implement `JumpRecoveryHandler` (Step 6: Recover)
- [x] 5.8 Implement `AdaptiveStepCalculator` pure function
- [x] 5.9 Implement `ScrollStatisticsCollector` (Step 7: Statistics)
- [x] 5.10 Implement `ScrollHandler` orchestration class
- [x] 5.11 Implement `HandleScroll()` 7-step pipeline method
- [x] 5.12 Add ScrollHandler component unit tests

## 6. Adaptive Strategy Implementation

- [x] 6.1 Implement adaptive step increase logic (duplicate ratio >= threshold)
- [x] 6.2 implement `MinSampleSize` validation before step increase
- [x] 6.3 Implement step clamping (MinScrollStep, MaxScrollStep)
- [x] 6.4 Implement safe step calculation (remaining distance clamp)
- [x] 6.5 Implement jump recovery step reduction (JumpRecoveryFactor)
- [x] 6.6 Implement progress epsilon comparison for boundary checks
- [x] 6.7 Add adaptive strategy unit tests

## 7. FSM Integration

- [x] 7.1 Add scroll check point in `TraversalFSM.HandleBranch()`
- [x] 7.2 Implement "all children visited → check scroll" logic
- [x] 7.3 Implement scroll success → reset VisitedChildren → NodeSelect flow
- [x] 7.4 Implement scroll failure → FrameComplete flow
- [x] 7.5 Add FSM integration tests

## 8. Test Scenarios

- [x] 8.1 Create `tests/UniClaw.Core.Tests/Simulation/Scroll/` directory
- [x] 8.2 Implement basic scenarios (single-screen, dual-screen, multi-screen, empty list)
- [x] 8.3 Implement boundary scenarios (top, bottom, near-bottom, precise-end)
- [x] 8.4 Implement element scenarios (deduplication, repeat, dynamic change)
- [x] 8.5 Implement step size scenarios (small, default, large, adaptive)
- [x] 8.6 Implement jump scenarios (normal, detection, recovery, failure)
- [x] 8.7 Add scroll scenario E2E tests

## 9. Documentation

- [x] 9.1 Update `docs/system/layers/simulation-baseline.md` with scroll support
- [x] 9.2 Update `docs/system/layers/state-machine.md` with ScrollHandler integration
- [x] 9.3 Add XML code comments to public APIs
- [x] 9.4 Update this change document with final implementation notes

## 10. Verification

- [x] 10.1 Run all tests (ensure 0 failures, 0 functional warnings)
- [x] 10.2 Verify existing non-scroll tests still pass (backward compatibility)
- [x] 10.3 Verify scroll scenarios cover all 19 cases
- [x] 10.4 Verify CI passes with all changes

## 11. Archive Preparation

- [x] 11.1 Run `/opsx:archive` to extract decisions
- [x] 11.2 Sync decisions to `docs/system/decisions/log.md`
- [x] 11.3 Move change to `openspec/changes/archive/`
