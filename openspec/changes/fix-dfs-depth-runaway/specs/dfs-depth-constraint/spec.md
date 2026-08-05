## ADDED Requirements

### Requirement: NodeStack respects plan-level effective max depth

`TraversalEngine.Initialize()` SHALL compute `effectiveMaxDepth = min(config.MaxDepth, plan.IntentSlots?.Depth ?? int.MaxValue)` BEFORE creating `TraversalRuntimeContext`, and pass it as the `maxDepth` parameter. `NodeStack.Push` SHALL return `false` when `Depth >= effectiveMaxDepth`, preventing subframe nodes from being pushed beyond the plan's depth limit.

#### Scenario: enumerate plan depth=2 prevents depth-3 push

- **WHEN** a `TraversalPlan` has `IntentSlots.Depth = 2` and engine `config.MaxDepth = 10`
- **THEN** `effectiveMaxDepth` is computed as `2`
- **THEN** `NodeStack(2).Push()` returns `false` when depth reaches 2

#### Scenario: plan without depth uses config default

- **WHEN** a `TraversalPlan` has `IntentSlots = null` or `IntentSlots.Depth = null`
- **THEN** `effectiveMaxDepth = config.MaxDepth` (default 10)
- **THEN** existing behavior is preserved (backward compatible)

#### Scenario: DFS traversal stops at maxDepth

- **WHEN** engine traverses a nested structure (Settings → L1 → L2 → L3) with `effectiveMaxDepth = 2`
- **THEN** engine visits L1 and L2 but does NOT push L3 nodes onto NodeStack
- **THEN** `CompletionReason` is `AllVisited` (not `MaxSteps`)

### Requirement: Simulation test verifies depth constraint

A simulation test SHALL exist that creates a 4-level fixture (Settings → L1 → L2 → L3) with `effectiveMaxDepth = 2` and asserts the engine does not visit level-3 pages.

#### Scenario: DeepNestedFixture depth=2 stops at level 2

- **WHEN** `DeepNestedFixture` (4 levels) is used with a plan that has `depth = 2`
- **THEN** `result.VisitedPages` does NOT contain `Wi‑Fi` or any level-3 page identifier
- **THEN** the test passes (fix verified)
