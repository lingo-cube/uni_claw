# Proposal: FSM Scroll Loop Fix

## Why

The scroll integration in `TraversalFSM.TryHandleScroll` has a critical bug that causes infinite loops when using Static children strategy with scroll. When scroll executes and resets `VisitedChildren`, the FSM sees "unvisited" children again and loops until `MaxSteps` (1000) is exhausted. This blocks scroll-enabled baseline tests from functioning.

## What Changes

- **Fix `TraversalFSM.TryHandleScroll` loop prevention logic**
  - Add check: only reset VisitedChildren if scroll **actually advanced progress**
  - Add check: only reset VisitedChildren if scroll **revealed new deduplicated elements**
  - Add guard: prevent scroll retry if progress doesn't change (return FrameComplete instead)

- **Enable scroll for DynamicMatch children strategy**
  - Extend `HandleBranch` to check scroll for DynamicMatch when no new children can be generated
  - Maintain backward compatibility with non-scroll scenarios

- **Add scroll loop detection**
  - Detect when scroll is called but progress doesn't advance
  - Log warning and terminate with FrameComplete instead of looping

## Capabilities

### New Capabilities
- **fsm-scroll-prevention**: Scroll loop prevention logic in TraversalFSM

### Modified Capabilities
- **traversal-fsm**: Extend scroll trigger conditions to include DynamicMatch

## Impact

- **Code**: `src/UniClaw.Core/StateMachine/TraversalFSM.cs` (TryHandleScroll, HandleBranch methods)
- **Tests**: Update existing scroll scenario tests to verify loop prevention
- **Dependencies**: Unblocks `scrollable-baseline-test` change implementation
- **Backward Compatibility**: Non-scroll scenarios unaffected (opt-in via ScrollableMockVisionService)
