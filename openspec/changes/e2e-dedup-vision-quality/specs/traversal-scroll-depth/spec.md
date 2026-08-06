## ADDED Requirements

### Requirement: D-G11 depth gate is removed
The scroll handler (`InterceptionHandler.TryHandleScrollAsync`) SHALL NOT skip scrolling based on the current frame depth relative to `EffectiveMaxDepth`. The gate `depth >= maxDepth → return (false, ...)` at lines 487-490 SHALL be removed.

#### Scenario: Frame at maxDepth can scroll
- **WHEN** the current frame depth equals `EffectiveMaxDepth` (e.g., depth = 2, maxDepth = 2)
- **THEN** scrolling is attempted (not skipped) if `HasScroll()` is true and `IsEndOfList()` is false

#### Scenario: Frame beyond maxDepth can scroll
- **WHEN** the current frame depth exceeds `EffectiveMaxDepth` (e.g., depth = 3, maxDepth = 2)
- **THEN** scrolling is attempted (not skipped) — the depth gate no longer blocks

#### Scenario: Scroll budget is still enforced
- **WHEN** scrolling is attempted at any depth
- **THEN** the existing `maxScrolls` budget constraint still applies and prevents unbounded scrolling

### Requirement: D-G7 child push gate is unchanged
The child node generation gate (`TryHandleNavigation` depth check) SHALL remain unchanged. `NodeStack.Push` SHALL continue to reject nodes whose depth would exceed `EffectiveMaxDepth`. This ensures the tree does not grow deeper than configured.

#### Scenario: Child at maxDepth+1 is still rejected
- **WHEN** a child node would be pushed at depth = maxDepth + 1
- **THEN** `NodeStack.Push` rejects the push (existing D-G7 / P3 behavior preserved)

### Requirement: FixVerificationTests covers D-G11 removal
A regression test SHALL verify that a frame at `depth == maxDepth` can still scroll after D-G11 removal.

#### Scenario: depth=2 scrollable fixture
- **WHEN** a fixture is set up with depth = 2 and maxDepth = 2, HasScroll = true, IsEndOfList = false
- **THEN** `TryHandleScrollAsync` returns `(true, ...)` indicating scroll was attempted
