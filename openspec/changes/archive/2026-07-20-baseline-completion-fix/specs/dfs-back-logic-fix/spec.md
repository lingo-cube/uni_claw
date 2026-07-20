## ADDED Requirements

### Requirement: InterceptionHandler SHALL not PressBack when no navigation occurred for non-root DynamicMatch nodes

In `InterceptionHandler.OnDynamicMatchNodeSelect`, when a non-root (`depth > 1`) DynamicMatch node has no remaining children, no navigation (fingerprint unchanged), and no scroll available, the engine SHALL only `Stack.Pop()` WITHOUT `PressBackAsync`. `PressBackAsync` SHALL only be called when the page fingerprint actually changed (navigation occurred during the current step). This prevents the engine from physically navigating away from the current page when it should remain on the same page and continue visiting remaining children of the parent frame.

#### Scenario: Non-root DynamicMatch child exhausted, no navigation — only Pop
- **WHEN** a non-root DynamicMatch child (e.g., `dyn_menu_container_HomeNetwork_*`) has no remaining children
- **AND** no navigation occurred (page fingerprint unchanged since entering the child)
- **AND** no scroll is available
- **THEN** `InterceptionHandler` SHALL execute `ctx.Stack.Pop()` WITHOUT `ctx.Action.PressBackAsync()`
- **AND** `result.FrameCompleted` SHALL be `false`
- **AND** `result.NextState` SHALL be `TraversalState.NodeSelect`

#### Scenario: Non-root DynamicMatch child exhausted, navigation occurred — PressBack + Pop
- **WHEN** a non-root DynamicMatch child has no remaining children
- **AND** navigation occurred during the step (page fingerprint changed, e.g., tapping a button that navigated to a sub-page)
- **THEN** `InterceptionHandler` SHALL execute `await ctx.Action.PressBackAsync()` followed by `ctx.Stack.Pop()`
- **AND** the engine SHALL return to the parent page where the parent frame's DynamicMatch cache remains valid

#### Scenario: Root node exhausted — delegate to ContainerHandler (unchanged)
- **WHEN** the root DynamicMatch node has no remaining children and no scroll available
- **THEN** `InterceptionHandler` SHALL delegate to `DecideFrameCompletion` via ContainerHandler (existing behavior unchanged)

### Requirement: InterceptionHandler SHALL detect navigation before deciding Pop vs PressBack+Pop

`OnDynamicMatchNodeSelect` SHALL compare the page fingerprint before and after child processing to determine whether navigation occurred. When the fingerprint is unchanged (same page), Pop-only is correct. When the fingerprint changed (navigation occurred), PressBack+Pop is correct.

#### Scenario: Fingerprint comparison for navigation detection
- **WHEN** `OnDynamicMatchNodeSelect` processes a DynamicMatch child with no remaining children
- **AND** depth > 1 (non-root)
- **THEN** the method SHALL compare the cached fingerprint of the current frame (from `DynamicChildManager.GetCachedFingerprint`) against the current page fingerprint (from `PageSnapshotManager.Fingerprint`)
- **AND** if fingerprints match (no navigation): Pop-only
- **AND** if fingerprints differ (navigation occurred): PressBack+Pop
