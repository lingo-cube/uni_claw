## ADDED Requirements (2026-07-20 — baseline-completion-fix)

### Requirement: InterceptionHandler SHALL compare parent-frame fingerprint to decide Pop vs PressBack+Pop (D-90 REVISED)

In `InterceptionHandler.OnDynamicMatchNodeSelect`, when a non-root (`depth > 1`) DynamicMatch node has no remaining children, no navigation detected (TryHandleNavigation returned false), and no scroll available (TryHandleScrollAsync returned false), the engine SHALL compare the **PARENT frame's** cached fingerprint against the current page fingerprint to decide whether to PressBack. This distinguishes two cases:
- **Parent on same page**: Pop-only (engine stays on parent's physical page, parent's DynamicMatch cache remains valid)
- **Parent on different page**: PressBack+Pop (engine must physically navigate back to parent's page)

#### Scenario: Non-root frame exhausted, parent on same page — Pop-only
- **WHEN** a non-root DynamicMatch frame has no remaining children, no navigation, and no scroll
- **AND** the parent frame's cached fingerprint matches the current page fingerprint (parent is on same physical page)
- **THEN** `InterceptionHandler` SHALL execute `ctx.Stack.Pop()` WITHOUT `ctx.Action.PressBackAsync()`
- **AND** `result.FrameCompleted` SHALL be `false`
- **AND** `result.NextState` SHALL be `TraversalState.NodeSelect`

#### Scenario: Non-root frame exhausted, parent on different page — PressBack+Pop
- **WHEN** a non-root DynamicMatch frame has no remaining children, no navigation, and no scroll
- **AND** the parent frame's cached fingerprint does NOT match the current page fingerprint (parent is on a different physical page, e.g. home vs wifi)
- **THEN** `InterceptionHandler` SHALL execute `await ctx.Action.PressBackAsync()` followed by `ctx.Stack.Pop()`
- **AND** the engine SHALL physically navigate back to the parent's page
- **AND** the parent frame's DynamicMatch cache SHALL remain valid after PressBack

#### Scenario: Parent frame has no cached fingerprint — default PressBack+Pop
- **WHEN** the parent frame has no cached fingerprint (e.g. static parent, no DynamicMatch)
- **THEN** `InterceptionHandler` SHALL execute PressBack+Pop (safe default: assume physical navigation needed)

#### Scenario: Root node exhausted — delegate to ContainerHandler (unchanged)
- **WHEN** the root DynamicMatch node has no remaining children and no scroll available
- **THEN** `InterceptionHandler` SHALL delegate to `DecideFrameCompletion` via ContainerHandler (existing behavior unchanged)

### Requirement: Parent-frame fingerprint obtained from NodeStack.Peek(1) + ChildMgr.GetCachedFingerprint

The parent frame's NodeId SHALL be obtained by `ctx.Context.NodeStack.Peek(1)` (offset 1 = second from top = parent frame). The parent's cached fingerprint SHALL be obtained from `ctx.ChildMgr.GetCachedFingerprint(parentFrame.NodeId)`. The current page fingerprint SHALL be obtained from `ctx.SnapshotMgr.Fingerprint(runtimeCtx?.CurrentPageAnalysis)`.

#### Scenario: Fingerprint comparison uses parent frame, not current frame
- **WHEN** `OnDynamicMatchNodeSelect` processes a DynamicMatch frame with no remaining children at depth > 1
- **THEN** the method SHALL compare the PARENT frame's cached fingerprint against the current page fingerprint
- **AND** NOT compare the current frame's cached fingerprint (which incorrectly gives Pop-only for navigable sub-pages)
