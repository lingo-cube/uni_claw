## Why

Current StateMachine and Traversal components use concrete types directly (e.g., `TraceCoordinator`, `PageCacheManager`), preventing test mocking and creating tight coupling. This blocks unit test coverage for StepOrchestrator, TraversalFSM handlers, and error recovery logic — a critical bottleneck for P2 Context Decomposition (D-I) which requires stable test baselines before refactoring the 30-field TraversalRuntimeContext God Object.

Interface extraction enables:
1. **Testability**: Mock dependencies in unit tests without hitting real I/O or AI services
2. **Architectural health**: Decouple components before Context decomposition (P2 prerequisite)
3. **Coverage ceiling**: Remove hard cap on test coverage (currently limited by concrete dependencies)

This is P1 priority in the refactoring roadmap ([20-b-refactoring-roadmap-design.md §5](../refactor/20-b-refactoring-roadmap-design.md)).

## What Changes

- **Extract 6+ interfaces** from concrete StateMachine/Traversal classes:
  - `IDynamicChildManager` from `DynamicChildManager`
  - `ITraceCoordinator` from `TraceCoordinator`
  - `IEntryPolicyExecutor` from `EntryPolicyExecutor`
  - `IPageCacheManager` from `PageCacheManager`
  - `IPageSnapshotManager` from `PageSnapshotManager`
  - `INodeStackAdapter` from `NodeStackAdapter`
- **Ripple fix**: Update `StepContext` (12 positional init-only parameters) to use interface types instead of concrete classes
- **Update consumers**: Modify `TraversalEngine` constructor and all injection sites to accept interface types
- **Add unit tests**: Create interface-based mocks for testing StepOrchestrator and FSM handlers

**BREAKING**: StepContext constructor signature changes (concrete → interface types). All StepContext instantiation sites must update.

## Capabilities

### New Capabilities

- `dynamic-child-manager-abstraction`: DynamicChildManager interface for mocking node generation
- `trace-coordinator-abstraction`: TraceCoordinator interface for mocking trace recording
- `entry-policy-abstraction`: EntryPolicyExecutor interface for mocking policy decisions
- `page-cache-abstraction`: PageCacheManager interface for mocking caching behavior
- `page-snapshot-abstraction`: PageSnapshotManager interface for mocking fingerprint generation
- `node-stack-abstraction`: NodeStackAdapter interface for mocking stack operations
- `step-context-interface-types`: StepContext using interface types for dependency injection

### Modified Capabilities

None (this is implementation-level refactoring, no spec-level behavior changes).

## Impact

**Affected code**:
- `src/UniClaw.Core/StateMachine/` — 6 interface extractions
- `src/UniClaw.Core/Traversal/StepOrchestrator.cs` — StepContext signature change
- `src/UniClaw.Core/Traversal/TraversalEngine.cs` — constructor injection types
- `tests/UniClaw.Core.Tests/` — New interface-based test doubles

**Dependencies**: None new (pure extraction)

**Systems**:
- StateMachine layer components become mockable
- Traversal engine gains test injection points
- Paves way for P2 Context Decomposition (D-I)
