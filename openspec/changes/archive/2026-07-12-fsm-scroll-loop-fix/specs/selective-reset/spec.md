# Spec: Selective VisitedChildren Reset

## Context

Current implementation clears ALL VisitedChildren for the node after scroll. This is incorrect - elements that were NOT present before scroll should remain marked visited.

## Requirements

### Selective Reset (D4)

**WHEN** scroll succeeds and new content is detected
**THEN** the FSM SHALL only reset VisitedChildren for elements that were **actually present before scroll**

**Implementation:**
```csharp
// Only reset children that were in the before-element set
var beforeSet = beforeElementIds.ToHashSet();
var visitedForNode = Context.VisitedChildren.TryGetValue(node.NodeId, out var visited)
    ? visited : ImmutableHashSet<string>.Empty;

// Create new visited set excluding elements from before-scroll
var newVisited = visited.Where(id => !beforeSet.Contains(id)).ToImmutableHashSet();
Context.UpdateVisitedChildren(node.NodeId, newVisited);
```

**Rationale:** Elements that were NOT in `beforeElementIds` should remain marked visited. This prevents re-visiting elements that were already seen before scroll started.

## Acceptance Criteria

- [ ] Only elements from `beforeElementIds` are removed from VisitedChildren
- [ ] Elements not in `beforeElementIds` remain marked visited
- [ ] `INavigationContext.UpdateVisitedChildren` API exists (or is added)
