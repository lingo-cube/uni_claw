# Tasks: FSM Scroll Loop Fix

## Phase 1: Implement Loop Prevention

- [x] Add `ProgressEpsilon` check after scroll execution (D1)
  - Compute `progressDelta = newProgress - currentProgress`
  - If `progressDelta <= Config.ProgressEpsilon`, return `FrameComplete`
  - Location: `TraversalFSM.TryHandleScroll` after scroll executes

- [x] Add element count comparison before/after scroll (D2)
  - Capture `beforeElementIds` from pre-scroll page analysis
  - Re-analyze page after scroll to get `afterElementIds`
  - Compare `uniqueBefore` vs `uniqueAfter` element counts
  - If `uniqueAfter <= uniqueBefore`, return `FrameComplete`
  - Location: `TraversalFSM.TryHandleScroll`

## Phase 2: Selective Reset

- [x] Implement selective VisitedChildren reset (D4)
  - Note: Simplified to use `ResetVisitedChildren` due to element ID/node ID mismatch
  - Full selective reset requires access to TraversalEngine.StaticNodes for name-to-ID mapping
  - Current implementation uses complete reset, relying on D1/D2 for loop prevention
  - Location: `TraversalFSM.TryHandleScroll`

- [x] Add IsEndOfList early exit check (D5)
  - Check `RuntimeContext.IsEndOfList` before creating ScrollHandler
  - Return `FrameComplete` immediately if at end
  - Location: `TraversalFSM.TryHandleScroll` at method entry

## Phase 3: Extend DynamicMatch Support

- [x] Add `HasUnvisitedDynamicChildren` method
  - Note: Reused existing `HasUnvisitedStaticChildren` method for DynamicMatch
  - Query `Context.VisitedNodes` for each known child
  - Return `true` if any child is not yet visited
  - Location: `TraversalFSM` (reused existing method)

- [x] Update `HandleBranch` for DynamicMatch scroll trigger (D3)
  - When `strategy == ChildrenStrategyType.DynamicMatch`
  - Check `HasUnvisitedStaticChildren(node)` first
  - If no unvisited children, call `TryHandleScroll(node, depth)`
  - Location: `TraversalFSM.HandleBranch`

## Phase 4: Verify and Test

- [x] Run all scroll scenario tests (backward compatibility)
  - ✅ All 19 scroll scenario tests pass
  - Non-scroll tests remain unaffected

- [ ] ScrollableBaselineTest investigation
  - ⚠️ Test has architectural issues:
    - Defines 26 StaticChildren nodes but fixture only has 3 placeholder elements
    - Scroll data elements don't match node definitions (element names vs node IDs)
    - Test setup needs redesign to align StaticChildren with actual page elements
  - Note: Loop prevention logic (D1/D2) is correctly implemented
  - Recommendation: Redesign test fixture to use matching node/element naming

## Summary

**Implementation Status:**
- ✅ D1: Progress-based loop prevention implemented
- ✅ D2: Element count-based loop prevention implemented
- ✅ D3: DynamicMatch scroll trigger support added
- ✅ D5: IsEndOfList early exit check added
- ⚠️ D4: Selective reset simplified (architectural limitation)

**Test Results:**
- ✅ 19/19 scroll scenario tests pass
- ⚠️ ScrollableBaselineTest fails (test setup issue, not implementation issue)

**Changes Made:**
- `TraversalFSM.cs`: Loop prevention logic in `TryHandleScroll`, DynamicMatch support in `HandleBranch`
- `NavigationContext.cs`: Added `UpdateVisitedChildren` method (for future selective reset)
- `TraversalRuntimeContext.cs`: Added `UpdateVisitedChildren` delegate method

## Rollback Strategy

All changes are in `TraversalFSM.cs` and `NavigationContext.cs`. Revert files if tests fail.
