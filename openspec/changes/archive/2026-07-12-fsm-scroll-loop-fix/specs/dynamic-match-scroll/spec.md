# Spec: DynamicMatch Scroll Trigger

## Context

Current code skips scroll check entirely for DynamicMatch (lines 349-350 in `TraversalFSM.HandleBranch`). DynamicMatch should also benefit from scroll when exhausted.

## Requirements

### Scroll Trigger for DynamicMatch (D3)

**WHEN** children strategy is `ChildrenStrategyType.DynamicMatch`
**AND** no new children can be generated (engine exhausted)
**THEN** the FSM SHALL call `TryHandleScroll(node, depth)`

**Implementation:**
```csharp
if (strategy == ChildrenStrategyType.DynamicMatch)
{
    // Ask engine if there are unvisited children
    if (HasUnvisitedDynamicChildren(node))
        return TraversalState.NodeSelect;
    
    // No more children - check scroll
    return TryHandleScroll(node, depth);
}
```

**Rationale:** DynamicMatch should also benefit from scroll when child generation is exhausted.

### HasUnvisitedDynamicChildren Query

**WHEN** checking if DynamicMatch has more children
**THEN** the FSM SHALL query `Context.VisitedNodes` for each known child
**AND** return `true` if any child is not yet visited

**Rationale:** Use existing FSM context mechanisms to avoid circular dependency with TraversalEngine.

## Acceptance Criteria

- [ ] DynamicMatch scenarios trigger scroll when child generation is exhausted
- [ ] `HasUnvisitedDynamicChildren` method uses existing context mechanisms
- [ ] No circular dependency with TraversalEngine introduced
