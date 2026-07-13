# Tasks: ScrollHandler Integration

Implementation checklist for integrating ScrollHandler into TraversalEngine to enable scroll-aware traversal.

## 1. Phase 1: IVisionProvider Interface Extension

- [ ] 1.1 Add `HasScroll()`, `GetScrollProgress()`, `IsEndOfList()` methods to IVisionProvider interface with default implementations
- [ ] 1.2 Implement scroll-aware methods in ScrollableMockVisionService
- [ ] 1.3 Verify StatefulMockVisionService inherits default implementations (no code changes needed)
- [ ] 1.4 Add unit tests for IVisionProvider scroll state query methods

## 2. Phase 2: TraversalRuntimeContext Extension

- [ ] 2.1 Add `CurrentScrollProgress`, `HasScrollableContent`, `IsAtScrollEnd` properties to TraversalRuntimeContext
- [ ] 2.2 Implement `UpdateScrollProgress(double progress)` method
- [ ] 2.3 Wire up scroll context initialization in TraversalEngine.Initialize()
- [ ] 2.4 Add unit tests for scroll progress state management

## 3. Phase 3: TraversalFSM State Extension

- [ ] 3.1 Add `ScrollCheck` state to TraversalState enum
- [ ] 3.2 Implement ScrollCheck state transition logic in TraversalFSM
- [ ] 3.3 Update state transition table to include ScrollCheck transitions
- [ ] 3.4 Add ScrollFSM integration tests verifying state transitions

## 4. Phase 4: StepOrchestrator Scroll Decision Integration

- [ ] 4.1 Add optional `ScrollHandler? _scrollHandler` field to StepOrchestrator with constructor injection
- [ ] 4.2 Implement `ShouldCheckForScroll(StepContext ctx)` method with scroll condition checks
- [ ] 4.3 Implement `HandleScrollDecision(StepContext ctx)` method delegating to ScrollHandler
- [ ] 4.4 Implement `ExecuteScrollAction(StepContext ctx, ScrollAction action)` method
- [ ] 4.5 Integrate scroll checkpoint into main `ExecuteStep()` flow
- [ ] 4.6 Add integration tests verifying scroll decision triggering

## 5. Phase 5: ExitCondition Extension

- [ ] 5.1 Add `AllChildrenVisitedOrScrollEnd` value to ExitConditionType enum
- [ ] 5.2 Update ExitCondition evaluation logic to handle new type
- [ ] 5.3 Add unit tests verifying AllChildrenVisitedOrScrollEnd behavior
- [ ] 5.4 Verify backward compatibility with existing AllChildrenVisited type

## 6. Phase 6: Baseline Test Adaptation

- [ ] 6.1 Update HierarchyBaselineTests to verify 4-layer complete traversal (4 scenarios)
- [ ] 6.2 Update LongListBaselineTests to verify 20-30 item list complete traversal (3 scenarios)
- [ ] 6.3 Add ScrollFSM integration tests for scroll state transitions
- [ ] 6.4 Verify all 15 baseline scenarios pass (8 existing + 7 newly unblocked)

## 7. Phase 7: Documentation

- [ ] 7.1 Update `docs/system/layers/state-machine.md` with ScrollCheck state and transition table
- [ ] 7.2 Update `docs/system/layers/traversal.md` with StepOrchestrator scroll decision logic
- [ ] 7.3 Extract ScrollHandler integration decisions to `docs/system/decisions/log.md`
- [ ] 7.4 Update `docs/system/layers/simulation-baseline.md` with scroll-aware traversal capabilities

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| src/Traversal/ | docs/system/layers/traversal.md |
| src/StateMachine/ | docs/system/layers/state-machine.md |
| src/Simulation/ | docs/system/layers/simulation-baseline.md |
| tests/ | docs/TEST_GUIDE.md |
