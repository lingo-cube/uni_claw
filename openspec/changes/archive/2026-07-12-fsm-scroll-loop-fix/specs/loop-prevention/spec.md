# Spec: Scroll Loop Prevention

## Context

The scroll integration in `TraversalFSM.TryHandleScroll` has a critical bug causing infinite loops when using Static children strategy. After scroll executes and resets `VisitedChildren`, the FSM sees "unvisited" children again and loops until `MaxSteps` (1000) is exhausted.

## Requirements

### Progress-Based Check (D1)

**WHEN** scroll execution completes with a `newProgress` value
**THEN** the FSM SHALL compute `progressDelta = newProgress - currentProgress`
**AND** if `progressDelta <= Config.ProgressEpsilon`
**THEN** the FSM SHALL return `TraversalState.FrameComplete` instead of resetting VisitedChildren

**Rationale:** Scroll should only happen if it moves forward in the list. No progress means no new content, so we should not reset VisitedChildren.

### Element Count-Based Check (D2)

**WHEN** scroll execution completes
**THEN** the FSM SHALL:
1. Capture `beforeElementIds` from the pre-scroll page analysis
2. Re-analyze the page after scroll to get `afterElementIds`
3. Compute `uniqueBefore = beforeElementIds.Distinct().Count()`
4. Compute `uniqueAfter = afterElementIds.Distinct().Count()`
5. If `uniqueAfter <= uniqueBefore`, return `TraversalState.FrameComplete`

**Rationale:** Even with progress advance, content might be the same (e.g., sparse segments). Need to verify actual NEW elements are visible.

## Acceptance Criteria

- [ ] `WiFiList_ScrollThroughAllScreens_AllNetworksVisited` test completes without hitting MaxSteps=1000
- [ ] Scroll loop is detected when progress doesn't advance
- [ ] Scroll loop is detected when element count doesn't increase
- [ ] Non-scroll scenarios remain unaffected (backward compatibility)
