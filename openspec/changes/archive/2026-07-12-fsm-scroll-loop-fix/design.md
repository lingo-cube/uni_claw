# Design: FSM Scroll Loop Fix

## Context

**Background:** The scroll-simulation-enhancement change (archived) implemented scroll infrastructure and integrated it into `TraversalFSM.TryHandleScroll`. However, the integration has a critical bug causing infinite loops when scroll is triggered with Static children strategy.

**Current State:** 
- `TraversalFSM.TryHandleScroll` is called when all Static children are visited
- After scroll executes, `RuntimeContext.ResetVisitedChildren(node.NodeId)` is called
- FSM then sees "unvisited" children again, re-enters `TryHandleScroll`, loops until `MaxSteps=1000`

**Test Evidence:** `ScrollableBaselineTests.WiFiList_ScrollThroughAllScreens_AllNetworksVisited` hits max_steps with 1000 steps, never completing.

**Root Cause:** The scroll logic assumes scroll reveals **new/different elements** (new element IDs). But Static children have fixed IDs, so after scroll, the same node IDs are revisited repeatedly.

**Constraints:**
- Must not break existing non-scroll tests (backward compatibility)
- Must work with both Static and DynamicMatch children strategies
- Must maintain scroll functionality for scenarios that genuinely have new content

## Goals / Non-Goals

**Goals:**
1. Fix infinite loop in `TryHandleScroll` when scroll doesn't reveal new content
2. Enable scroll to work correctly with Static children strategy
3. Extend scroll trigger support to DynamicMatch children strategy
4. Add scroll loop detection and early termination

**Non-Goals:**
- Changing scroll infrastructure (ScrollHandler, ScrollableMockVisionService remain unchanged)
- Modifying ExitCondition or other FSM behaviors
- Performance optimization (focus on correctness)

## Decisions

### D1: Progress-Based Loop Prevention

**Decision:** Before resetting VisitedChildren, verify that scroll **actually advanced progress**.

**Rationale:** 
- Scroll should only happen if it moves forward in the list
- If `newProgress <= currentProgress + epsilon`, scroll didn't advance
- No progress = no new content = should not reset VisitedChildren

**Implementation:**
```csharp
var progressDelta = newProgress - currentProgress;
if (progressDelta <= Config.ProgressEpsilon)
{
    // Scroll didn't advance - no new content
    return TraversalState.FrameComplete;
}
```

### D2: Element Count-Based Loop Prevention

**Decision:** Verify that scroll **revealed new deduplicated elements** before resetting VisitedChildren.

**Rationale:**
- Even with progress advance, content might be same (e.g., sparse segments with large gaps)
- Need to check if actual NEW elements are visible
- Compare deduplicated element counts before/after scroll

**Implementation:**
```csharp
var afterAnalysis = _currentStepContext?.Vision.AnalyzeCurrentPageAsync().GetAwaiter().GetResult();
var afterElementIds = afterAnalysis?.Items.Select(i => i.Name ?? "").Where(name => !string.IsNullOrEmpty(name)).ToImmutableArray() ?? ImmutableArray<string>.Empty;

// Count unique elements (before vs after)
var uniqueBefore = beforeElementIds.Distinct().Count();
var uniqueAfter = afterElementIds.Distinct().Count();

if (uniqueAfter <= uniqueBefore)
{
    // No new unique elements - don't reset VisitedChildren
    return TraversalState.FrameComplete;
}
```

### D3: DynamicMatch Scroll Trigger

**Decision:** Extend `HandleBranch` to check scroll for DynamicMatch when no new children can be generated.

**Rationale:**
- Current code skips scroll check entirely for DynamicMatch (line 349-350)
- DynamicMatch should also benefit from scroll when exhausted
- Need to check if engine's `GetUnvisitedChild` returns null

**Implementation:**
```csharp
// DYNAMIC_MATCH: check if engine has more children, then try scroll
if (strategy == ChildrenStrategyType.DynamicMatch)
{
    // Ask engine if there are unvisited children
    if (HasUnvisitedDynamicChildren(node))
        return TraversalState.NodeSelect;
    
    // No more children - check scroll
    return TryHandleScroll(node, depth);
}
```

### D4: Scroll Success → Selective Reset

**Decision:** Only reset VisitedChildren for elements that were **actually present before scroll**.

**Rationale:**
- Current reset clears ALL VisitedChildren for the node
- Should only reset elements that could potentially be revisited
- Elements that were NOT in beforeElementIds should remain marked visited

**Implementation:**
```csharp
// Only reset children that were in the before-element set
var beforeSet = beforeElementIds.ToHashSet();
var visitedForNode = Context.VisitedChildren.TryGetValue(node.NodeId, out var visited)
    ? visited : ImmutableHashSet<string>.Empty;

// Create new visited set excluding elements from before-scroll
var newVisited = visited.Where(id => !beforeSet.Contains(id)).ToImmutableHashSet();
Context.UpdatedVisitedChildren(node.NodeId, newVisited);
```

### D5: IsEndOfList Early Exit

**Decision:** Check `IsEndOfList` **before** attempting scroll, not after.

**Rationale:**
- Current code checks IsEndOfList inside ScrollHandler
- Checking earlier avoids unnecessary ScrollHandler creation
- More explicit control flow

## Risks / Trade-offs

### Risk 1: Breaking Existing Scroll Tests

**Risk:** Changes to `TryHandleScroll` might break existing `ScrollScenarioTests`.

**Mitigation:** Run all scroll scenario tests after fix. Verify they still pass (they test scroll infrastructure directly, not full traversal).

### Risk 2: Performance Impact from Double Analysis

**Risk:** Calling `AnalyzeCurrentPageAsync()` twice (once before scroll, once after) impacts performance.

**Mitigation:** The second analysis is necessary to detect new elements. Performance impact is minimal for test scenarios.

### Risk 3: DynamicMatch Engine Query Complexity

**Risk:** `HasUnvisitedDynamicChildren` requires querying TraversalEngine, creating circular dependency.

**Mitigation:** Use existing FSM `Context` mechanisms. The engine already tracks `VisitedNodes` - query that instead.

## Migration Plan

### Phase 1: Implement Loop Prevention

1. Add `ProgressEpsilon` check after scroll execution
2. Add element count comparison (before vs after)
3. Return `FrameComplete` if no new content detected

### Phase 2: Extend DynamicMatch Support

1. Add `HasUnvisitedDynamicChildren` method
2. Update `HandleBranch` to call `TryHandleScroll` for DynamicMatch
3. Test with both Static and DynamicMatch scenarios

### Phase 3: Verify Scroll Baseline Tests

1. Run `ScrollableBaselineTests` - should no longer hit MaxSteps
2. Verify scroll actually completes with `CompletionReason == "all_visited"`
3. Adjust ExpectedBehavior JSON values to actual runtime

**Rollback Strategy:** All changes are in `TraversalFSM`. Revert file if tests fail.

## Open Questions

### Q1: HasUnvisitedDynamicChildren Implementation

**Question:** How to check if DynamicMatch has more unvisited children without calling TraversalEngine?

**Options:**
- A. Check `Context.VisitedNodes.Contains(childId)` for each known static child
- B. Use engine's `HasUnvisitedChildren` method (requires circular dependency)
- C. Track DynamicMatch exhaustion in FSM state

**Resolution Plan:** Check existing FSM patterns for "no more children" detection in DynamicMatch scenarios.

### Q2: VisitedChildren Update API

**Question:** Does `ITraversalContext` have a method to update VisitedChildren for a specific node?

**Resolution Plan:** Check `INavigationContext` interface for update methods. If not present, may need extension.
