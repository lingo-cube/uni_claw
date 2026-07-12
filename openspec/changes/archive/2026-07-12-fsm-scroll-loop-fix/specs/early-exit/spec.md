# Spec: IsEndOfList Early Exit

## Context

Current code checks `IsEndOfList` inside `ScrollHandler`, after creating the handler instance. Checking earlier would avoid unnecessary handler creation.

## Requirements

### Early End-of-List Check (D5)

**WHEN** considering scroll in `TryHandleScroll`
**THEN** the FSM SHALL check `IsEndOfList` **before** creating `ScrollHandler`
**AND** if `IsEndOfList == true`, return `TraversalState.FrameComplete` immediately

**Implementation:**
```csharp
// Early exit if already at end
if (RuntimeContext.IsEndOfList)
    return TraversalState.FrameComplete;
```

**Rationale:** More explicit control flow and avoids unnecessary ScrollHandler creation when already at end.

## Acceptance Criteria

- [ ] `IsEndOfList` is checked before ScrollHandler creation
- [ ] FrameComplete is returned immediately when at end of list
- [ ] ScrollHandler is not created when `IsEndOfList == true`
