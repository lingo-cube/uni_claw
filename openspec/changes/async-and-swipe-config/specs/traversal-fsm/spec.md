# Capability: Traversal FSM — Delta

## MODIFIED Requirements

### Requirement: TraversalFSM step dispatches by from_state to handler methods

`TraversalFSM.StepAsync()` SHALL execute a single FSM step asynchronously. The method SHALL record the `from_state`, dispatch to the appropriate async handler method based on `from_state` via `DispatchHandlerAsync()`, and transition to the handler's returned `TraversalState`. The dispatch SHALL use enum-based switch, not if/elif chains. All 8 handler methods SHALL return `Task<TraversalState>` and be named with the `Async` suffix. `HasUnvisitedChildren(IGraphTraversalEngine?)` parameter type SHALL reference `UniClaw.Core.Traversal.IGraphTraversalEngine`. `TraversalFSM.cs` SHALL add `using UniClaw.Core.Traversal;`.

#### Scenario: HasUnvisitedChildren receives TraversalEngine instance
- **WHEN** TraversalEngine implements IGraphTraversalEngine and passes itself to TraversalFSM
- **THEN** HasUnvisitedChildren can query the engine's visited children state (no longer always null/dead code)

#### Scenario: StepAsync dispatches to correct async handler for each state
- **WHEN** `StepAsync()` is called while the FSM is in state `S`
- **THEN** the async handler corresponding to `S` is invoked (e.g., `NodeSelect` → `HandleNodeSelectAsync`, `PreconditionCheck` → `HandlePreconditionCheckAsync`)

#### Scenario: StepAsync wraps handler execution in try-catch
- **WHEN** an async handler method throws an unhandled exception during `StepAsync()`
- **THEN** the exception is caught, `context.last_error` is set to the exception, `consecutive_errors` is incremented, and the FSM routes to `ErrorHandling` regardless of which state the handler was for

#### Scenario: StepAsync records from_state before dispatch
- **WHEN** `StepAsync()` begins execution
- **THEN** the current state is recorded as `from_state` before any handler is invoked

#### Scenario: StepAsync calls transition_to with handler result
- **WHEN** a handler returns a `TraversalState` value `next_state`
- **THEN** `transition_to(from_state, next_state)` is called to validate and execute the state change

#### Scenario: StepAsync clears _currentStepContext in finally
- **WHEN** `StepAsync()` completes (including after exception)
- **THEN** `_currentStepContext` is set to null in the finally block

### Requirement: HandleExecute dispatches operations asynchronously

`HandleExecuteAsync` SHALL dispatch primary and optional restore operations asynchronously via `await OperationDispatcher.DispatchAsync()`. It SHALL NOT use `.GetAwaiter().GetResult()`. After execution, it SHALL return `TraversalState.ResultVerify`. On exception, it SHALL set last error and return `TraversalState.ErrorHandling`.

#### Scenario: Primary operation dispatched asynchronously
- **WHEN** `HandleExecuteAsync` is invoked and the current node has a non-NoAction operation
- **THEN** `await OperationDispatcher.DispatchAsync(operation, action)` is called
- **AND** no `.GetAwaiter().GetResult()` is present in the method body

#### Scenario: Restore operation dispatched asynchronously
- **WHEN** the primary operation succeeds and has a Restore action
- **THEN** `await OperationDispatcher.DispatchAsync(restoreOperation, action)` is called

#### Scenario: Restore failure is non-critical
- **WHEN** the restore operation throws an exception
- **THEN** the exception is caught, and `HandleExecuteAsync` still returns `ResultVerify`

### Requirement: HandleResultVerify verifies action result asynchronously

`HandleResultVerifyAsync` SHALL call `await vision.AnalyzeCurrentPageAsync()` to get post-action page analysis. It SHALL NOT use `.GetAwaiter().GetResult()`. Retry loop (up to 3 rounds) SHALL also use `await` for vision re-calls. If popup detected during retry, it SHALL route to `PopupHandling`. If all retries fail to show page change, it SHALL route to `Branch`.

#### Scenario: First check calls AnalyzeCurrentPageAsync with await
- **WHEN** `HandleResultVerifyAsync` performs its first page check
- **THEN** `await vision.AnalyzeCurrentPageAsync()` is called
- **AND** no `.GetAwaiter().GetResult()` is present in the method body

#### Scenario: Retry loop uses await for vision re-calls
- **WHEN** the first check shows no page change and retry round N executes
- **THEN** `await vision.AnalyzeCurrentPageAsync()` is called for the re-analysis

#### Scenario: Popup detected during async retry
- **WHEN** `HandleResultVerifyAsync` detects popup during async retry round
- **THEN** handler returns `Task.FromResult(TraversalState.PopupHandling)`

## ADDED Requirements

### Requirement: All 8 FSM handlers return Task<TraversalState>

Every handler method in `TraversalFSM` SHALL return `Task<TraversalState>` and use the `Async` suffix. Handlers that do not perform I/O (HandleNodeSelect, HandlePreconditionCheck, HandleBranch, HandleFrameComplete, HandleErrorHandling, HandlePopupHandling) SHALL wrap their synchronous logic in `Task.FromResult()` or be declared `async` with no await.

#### Scenario: HandleNodeSelectAsync returns Task<TraversalState>
- **WHEN** `HandleNodeSelectAsync()` is invoked
- **THEN** it returns `Task<TraversalState>` (Branch or PreconditionCheck)

#### Scenario: HandleBranchAsync returns Task<TraversalState>
- **WHEN** `HandleBranchAsync()` is invoked
- **THEN** it returns `Task<TraversalState>` (NodeSelect or FrameComplete)
