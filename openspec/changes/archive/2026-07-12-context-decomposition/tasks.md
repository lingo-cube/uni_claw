# Tasks: Context Decomposition

## 1. Phase 1 - NavigationContext

- [x] 1.1 Create `StateMachine/Navigation/INavigationContext.cs` with read-only properties (NodeStack, CurrentPath, CurrentPageAnalysis, CurrentFingerprint, VisitedPages, VisitedNodes, VisitedChildren, VisitedLevel1Menus, VisitedLevel2Menus, PageTree, CurrentFrame)
- [x] 1.2 Create `StateMachine/Navigation/NavigationContext.cs` sealed class implementing INavigationContext with 12 private fields
- [x] 1.3 Add mutation methods to NavigationContext: AppendPath, PopPath, MarkVisited, MarkNodeVisited, AddVisitedChild, SetCurrentPageAnalysis, SetCurrentFingerprint, SetPageTree, CurrentFrame setter
- [x] 1.4 Implement VisitedChildren lazy rebuild with ReadOnlySetWrapper (Level 3 safety)
- [x] 1.5 Modify `TraversalRuntimeContext` to add `Navigation` property and delegate navigation-related properties (NodeStack, CurrentPath, VisitedPages, VisitedNodes, VisitedChildren, CurrentFrame)
- [x] 1.6 Update TraversalRuntimeContext constructor to create NavigationContext with traceId, maxDepth, nodeStack
- [x] 1.7 Update `DynamicChildManager` to use `context.Navigation.VisitedLevel1Menus` and `context.Navigation.PageTree`
- [x] 1.8 Update `NodeStackAdapter` to use `context.Navigation.NodeStack`
- [x] 1.9 Update `StepOrchestrator` to use navigation sub-context properties
- [x] 1.10 Update all tests referencing navigation fields to use nested access pattern
- [x] 1.11 Run `dotnet test` — ensure all 603+ tests pass (617 tests passing)
- [x] 1.12 Commit Phase 1 changes

## 2. Phase 2 - ErrorContext

- [x] 2.1 Create `StateMachine/Error/IErrorContext.cs` with read-only properties (FailedNodes, ConsecutiveErrors, RetryCount, LastError, ExceptionChain)
- [x] 2.2 Create `StateMachine/Error/ErrorContext.cs` sealed class implementing IErrorContext with 5 private fields
- [x] 2.3 Add mutation methods to ErrorContext: IncrementConsecutiveErrors, ResetConsecutiveErrors, IncrementRetryCount, AddFailedNode, Setters
- [x] 2.4 Modify `TraversalRuntimeContext` to add `Error` property and delegate error-related properties (FailedNodes not currently exposed, but prepare for delegation)
- [x] 2.5 Update TraversalRuntimeContext constructor to create ErrorContext
- [x] 2.6 Update `ErrorHandler` to use `context.Error.FailedNodes`, `context.Error.ConsecutiveErrors
- [x] 2.7 Update `RecoveryExecutor` to use `context.Error.RetryCount`, `context.Error.ConsecutiveErrors`
- [x] 2.8 Update `TraversalFSM.HandleErrorHandling` to use error sub-context
- [x] 2.9 Update all tests referencing error fields to use nested access pattern
- [x] 2.10 Run `dotnet test` — ensure all tests pass (617 tests passing)
- [x] 2.11 Commit Phase 2 changes

## 3. Phase 3 - SessionContext

- [x] 3.1 Create `StateMachine/Session/ISessionContext.cs` with read-only properties (TraceId, GlobalState getter, DeviceExperience, AIProvider)
- [x] 3.2 Create `StateMachine/Session/SessionContext.cs` sealed class implementing ISessionContext with 4 private fields
- [x] 3.3 Add GlobalState setter to concrete SessionContext class (not on interface per D-7)
- [x] 3.4 Add setters for DeviceExperience and AIProvider
- [x] 3.5 Modify `TraversalRuntimeContext` to add `Session` property and delegate session-related properties (TraceId, GlobalState)
- [x] 3.6 Update TraversalRuntimeContext constructor to create SessionContext with traceId
- [x] 3.7 Update `GlobalFSM` to use `context.Session.GlobalState`
- [x] 3.8 Update `TraceCoordinator` to use `context.Session.TraceId`
- [x] 3.9 Update all tests referencing session fields to use nested access pattern
- [x] 3.10 Run `dotnet test` — ensure all tests pass (617 tests passing)
- [x] 3.11 Commit Phase 3 changes

## 4. Phase 4 - ProgressContext

- [x] 4.1 Create `StateMachine/Progress/IProgressContext.cs` with read-only properties (StepCount, MaxDepth, CompletionPolicy, ActionHistory, WaitAfterActionMs)
- [x] 4.2 Create `StateMachine/Progress/ProgressContext.cs` sealed class implementing IProgressContext with 5 private fields
- [x] 4.3 Add mutation methods to ProgressContext: IncrementStepCount, AddActionHistory, SetCompletionPolicy, SetWaitAfterActionMs
- [x] 4.4 Implement ActionHistory size limit (max 5 entries, remove oldest when exceeded)
- [x] 4.5 Modify `TraversalRuntimeContext` to add `Progress` property and delegate progress-related properties (StepCount)
- [x] 4.6 Update TraversalRuntimeContext constructor to create ProgressContext with maxDepth
- [x] 4.7 Update `CompletionDetector` to use `context.Progress.StepCount`, `context.Progress.MaxDepth`
- [x] 4.8 Update `StepOrchestrator` to use `context.Progress.IncrementStepCount()`
- [x] 4.9 Update all tests referencing progress fields to use nested access pattern
- [x] 4.10 Run `dotnet test` — ensure all tests pass (617 tests passing)
- [x] 4.11 Commit Phase 4 changes

## 5. Phase 5 - CacheContext

- [x] 5.1 Create `StateMachine/Cache/ICacheContext.cs` with read-only properties (PageCache, CacheValid)
- [x] 5.2 Create `StateMachine/Cache/CacheContext.cs` sealed class implementing ICacheContext with 2 core private fields + 2 Phase 3 reserved fields
- [x] 5.3 Add Phase 3 reserved fields: `object? ScrollHandler`, `object? CurrentSnapshot`
- [x] 5.4 Add mutation method: SetCacheValid
- [x] 5.5 Modify `TraversalRuntimeContext` to add `Cache` property and delegate cache-related properties (currently none exposed via ITraversalContext)
- [x] 5.6 Update TraversalRuntimeContext constructor to create CacheContext
- [x] 5.7 Update `PageCacheManager` to use `context.Cache.PageCache`, `context.Cache.CacheValid`
- [x] 5.8 Update `PageSnapshotManager` to use `context.Cache` if needed
- [x] 5.9 Update all tests referencing cache fields to use nested access pattern
- [x] 5.10 Run `dotnet test` — ensure all tests pass
- [x] 5.11 Commit Phase 5 changes

## 6. Finalize Container Delegation

- [x] 6.1 Verify all `ITraversalContext` properties delegate to appropriate sub-contexts
- [x] 6.2 Verify `TraversalRuntimeContext.CreateReadOnlySnapshot()` still works
- [x] 6.3 Ensure all 5 sub-contexts are created in constructor and never replaced (immutable references)
- [x] 6.4 Run full test suite `dotnet test` — ensure all 603+ tests pass
- [x] 6.5 Final commit with all phases

## 7. Documentation

- [x] 7.1 Update `docs/system/layers/state-machine.md` §5 with new sub-context structure
- [x] 7.2 Add decision entries to `docs/system/decisions/log.md` for D-I completion
- [x] 7.3 Update design doc `docs/refactor/2026-07-12-context-decomposition-design.md` with implementation notes
