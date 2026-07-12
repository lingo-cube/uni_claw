# Proposal: Context Decomposition

## Why

`TraversalRuntimeContext` is a God Object with 30 mutable fields across 5 subsystems (Navigation, Error, Session, Progress, Cache). This creates three problems: unclear responsibility boundaries, difficulty testing individual subsystems in isolation, and high coupling where changes to one subsystem require understanding the entire context. Decomposing into 5 separate sub-contexts will clarify boundaries, enable isolated mocking, and allow independent evolution of each subsystem.

## What Changes

- **BREAKING**: Create 5 new sub-context classes (`NavigationContext`, `ErrorContext`, `SessionContext`, `ProgressContext`, `CacheContext`) with corresponding read-only interfaces
- **BREAKING**: Modify `TraversalRuntimeContext` to Container pattern — holds 5 sub-contexts and delegates existing properties
- **BREAKING**: Update all consumers to use nested access pattern (`context.VisitedPages` → `context.Navigation.VisitedPages`)
- Add read-only interfaces for each sub-context (`INavigationContext`, `IErrorContext`, etc.)
- Keep all sub-contexts in `UniClaw.Core.StateMachine` namespace
- Implement each sub-context as mutable `sealed class` (not `record`) to avoid runtime copy overhead
- Implement phase-by-phase: Navigation → Error → Session → Progress → Cache

## Capabilities

### New Capabilities

- `navigation-context`: DFS traversal state — node stack, current path, page identity, visited tracking
- `error-context`: Error tracking — failed nodes, consecutive errors, retry count, error chain
- `session-context`: Macro session state — trace ID, global FSM state, device/AI configuration
- `progress-context`: Progress control — step count, max depth, completion policy, action history
- `cache-context`: Cache and configuration — page cache, cache validity, Phase 3 reserved fields

### Modified Capabilities

None. This is an internal refactoring; spec-level behavior requirements do not change.

## Impact

**Affected Code**:
- `TraversalRuntimeContext` — becomes Container holding 5 sub-contexts
- `DynamicChildManager` — accesses `context.Navigation.VisitedLevel1Menus`, `context.Navigation.PageTree`
- `ErrorHandler`/`RecoveryExecutor` — accesses `context.Error.FailedNodes`, `context.Error.ConsecutiveErrors`
- `GlobalFSM` — accesses `context.Session.GlobalState`
- `CompletionDetector` — accesses `context.Progress.StepCount`, `context.Progress.MaxDepth`
- `PageCacheManager` — accesses `context.Cache.PageCache`, `context.Cache.CacheValid`
- `TraceCoordinator` — accesses `context.Session.TraceId`
- `NodeStackAdapter` — accesses `context.Navigation.NodeStack`
- `StepOrchestrator` — accesses multiple sub-contexts during step execution
- All tests using `TraversalRuntimeContext` — approximately 603 tests

**Dependencies**:
- D-15 (subsystem canonical naming) — provides the 5-subsystem definition
- D-V (interface extraction) — establishes the pattern for read-only interfaces

**Follow-up Changes**:
- D-III (ITraversalContext reform) — will simplify ITraversalContext after decomposition completes
- D-IV (StepOrchestrator decomposition) — can leverage sub-context boundaries for further refactoring
