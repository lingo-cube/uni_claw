## ADDED Requirements

### Requirement: TraversalFSM defines exactly 8 states

`TraversalFSM` SHALL define exactly 8 states as a `TraversalState` enum: `NodeSelect`, `PreconditionCheck`, `Execute`, `ResultVerify`, `Branch`, `FrameComplete`, `ErrorHandling`, `PopupHandling`. No other enum members SHALL exist. `DynamicMatch` MUST NOT appear as a `TraversalState` value — it is a `ChildrenStrategyType` value, not an FSM state.

#### Scenario: Enum members match the 8 canonical states
- **WHEN** `TraversalState` enum members are enumerated
- **THEN** exactly 8 members exist: `NodeSelect`, `PreconditionCheck`, `Execute`, `ResultVerify`, `Branch`, `FrameComplete`, `ErrorHandling`, `PopupHandling`

#### Scenario: DynamicMatch is not a TraversalState
- **WHEN** `TraversalState` enum members are enumerated
- **THEN** `DynamicMatch` is not present among the members

#### Scenario: No additional states beyond the 8
- **WHEN** `Enum.GetValues<TraversalState>()` is called
- **THEN** the count is exactly 8

### Requirement: TraversalFSM transition matrix is enforced with D-1 correction

`TraversalFSM` SHALL enforce a strict transition matrix. Each transition from a source state to a target state MUST be validated against the matrix. Invalid transitions SHALL throw `DomainValidationException`. The transition matrix SHALL reflect decision D-1: the `PreconditionCheck → Branch` transition is REMOVED because the Python V6.7 `_handle_precondition_check` handler never returns `Branch` (only `Execute` or `ErrorHandling`), and `precondition_failed()` is dead code.

The canonical transition matrix:

| Source | Allowed Targets |
|--------|-----------------|
| `NodeSelect` | `PreconditionCheck`, `Branch` |
| `PreconditionCheck` | `Execute`, `ErrorHandling` |
| `Execute` | `ResultVerify`, `Branch`, `ErrorHandling` |
| `ResultVerify` | `Branch`, `PopupHandling` |
| `Branch` | `NodeSelect`, `PreconditionCheck`, `FrameComplete`, `ErrorHandling` |
| `FrameComplete` | `NodeSelect`, `ErrorHandling` |
| `ErrorHandling` | `NodeSelect`, `Execute`, `FrameComplete`, `Branch` |
| `PopupHandling` | `ResultVerify`, `ErrorHandling` |

#### Scenario: All valid transitions are accepted
- **WHEN** `TraversalFSM.transition_to(from, to)` is called for every (from, to) pair listed in the canonical matrix
- **THEN** each call succeeds without exception

#### Scenario: PreconditionCheck to Branch is rejected (D-1 enforcement)
- **WHEN** `TraversalFSM.transition_to(PreconditionCheck, Branch)` is called
- **THEN** `DomainValidationException` is thrown with `FieldName` indicating the invalid transition and `IllegalValue` containing `"PreconditionCheck→Branch"`

#### Scenario: Every invalid transition is rejected
- **WHEN** `TraversalFSM.transition_to(from, to)` is called for any (from, to) pair NOT listed in the canonical matrix
- **THEN** `DomainValidationException` is thrown

#### Scenario: Transition matrix covers all 8 source states
- **WHEN** the transition matrix entries are inspected
- **THEN** every `TraversalState` enum member appears as a source key with at least one allowed target

#### Scenario: No source state allows transition to itself
- **WHEN** each source state's allowed targets are inspected
- **THEN** the source state itself is not among its allowed targets (no self-loops)

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

#### Scenario: Empty stub deleted from TraversalState.cs
- **WHEN** the empty `public interface IGraphTraversalEngine {}` at TraversalState.cs:152-155 is removed
- **THEN** only `UniClaw.Core.Traversal.IGraphTraversalEngine` remains as the canonical interface definition

#### Scenario: handler for NodeSelect produces correct outcomes
- **WHEN** `_handle_node_select` is invoked
- **THEN** if the node stack is empty, it SHALL return `Branch`; if the stack has a current node, it SHALL return `PreconditionCheck`

#### Scenario: handler for PreconditionCheck produces only Execute or ErrorHandling
- **WHEN** `_handle_precondition_check` is invoked
- **THEN** it SHALL return either `Execute` (precondition passed or no precondition) or `ErrorHandling` (precondition failed after retries); it MUST NOT return `Branch`

### Requirement: GlobalFSM defines exactly 8 macro-lifecycle states

`GlobalFSM` SHALL define exactly 8 states as a `GlobalState` enum: `Idle`, `Initializing`, `Traversing`, `Paused`, `Error`, `Recovering`, `Completed`, `Terminated`. `Completed` and `Terminated` SHALL be terminal states with no allowed outgoing transitions.

#### Scenario: GlobalState enum has exactly 8 members
- **WHEN** `GlobalState` enum members are enumerated
- **THEN** exactly 8 members exist: `Idle`, `Initializing`, `Traversing`, `Paused`, `Error`, `Recovering`, `Completed`, `Terminated`

#### Scenario: Completed is a terminal state
- **WHEN** `GlobalFSM.transition_to(Completed, any)` is called for any target state
- **THEN** `DomainValidationException` is thrown

#### Scenario: Terminated is a terminal state
- **WHEN** `GlobalFSM.transition_to(Terminated, any)` is called for any target state
- **THEN** `DomainValidationException` is thrown

### Requirement: GlobalFSM transition matrix is enforced

`GlobalFSM` SHALL enforce a strict transition matrix. Invalid transitions SHALL throw `DomainValidationException`. The transition matrix SHALL ensure that `Error` does not transition directly to `Traversing` — recovery MUST pass through `Recovering → Initializing → Traversing`.

The canonical transition matrix:

| Source | Allowed Targets |
|--------|-----------------|
| `Idle` | `Initializing` |
| `Initializing` | `Traversing`, `Error` |
| `Traversing` | `Paused`, `Error`, `Completed` |
| `Paused` | `Traversing`, `Terminated` |
| `Error` | `Recovering`, `Terminated` |
| `Recovering` | `Initializing`, `Terminated` |
| `Completed` | {} (locked) |
| `Terminated` | {} (locked) |

#### Scenario: All valid GlobalFSM transitions are accepted
- **WHEN** `GlobalFSM.transition_to(from, to)` is called for every (from, to) pair listed in the canonical matrix
- **THEN** each call succeeds without exception

#### Scenario: Error to Traversing is rejected (recovery must go through Initializing)
- **WHEN** `GlobalFSM.transition_to(Error, Traversing)` is called
- **THEN** `DomainValidationException` is thrown

#### Scenario: Recovering to Traversing is rejected
- **WHEN** `GlobalFSM.transition_to(Recovering, Traversing)` is called
- **THEN** `DomainValidationException` is thrown — recovery MUST pass through `Initializing` as a validation checkpoint

#### Scenario: Idle to any non-Initializing state is rejected
- **WHEN** `GlobalFSM.transition_to(Idle, <any state other than Initializing>)` is called
- **THEN** `DomainValidationException` is thrown

#### Scenario: Every invalid GlobalFSM transition is rejected
- **WHEN** `GlobalFSM.transition_to(from, to)` is called for any (from, to) pair NOT listed in the canonical matrix
- **THEN** `DomainValidationException` is thrown

### Requirement: GlobalFSM provides callback mechanism

`GlobalFSM` SHALL provide `register_state_callback(state, callback)` to register callbacks that are invoked when the FSM enters a given state. Callbacks SHALL be invoked after the state transition is validated and completed. Exceptions thrown by callbacks SHALL be caught and logged but MUST NOT propagate to the caller — callback failure SHALL NOT disrupt FSM operation.

#### Scenario: Callback is invoked on state entry
- **WHEN** a callback is registered for state `S` via `register_state_callback(S, callback)`
- **AND** `GlobalFSM` transitions to state `S`
- **THEN** the registered callback is invoked

#### Scenario: Multiple callbacks for the same state are all invoked
- **WHEN** two callbacks are registered for the same state `S`
- **AND** `GlobalFSM` transitions to state `S`
- **THEN** both callbacks are invoked in registration order

#### Scenario: Callback exception does not propagate
- **WHEN** a registered callback throws an exception during invocation
- **THEN** the exception is caught, logged as a warning, and the FSM transition is NOT reversed — the state change remains valid

#### Scenario: No callback for a state is a no-op
- **WHEN** `GlobalFSM` transitions to state `S` and no callback is registered for `S`
- **THEN** the transition completes normally with no callback invocation overhead

### Requirement: GlobalFSM maintains transition history

`GlobalFSM` SHALL maintain `_transition_history` as a chronological log of all state transitions. Each history entry SHALL record the source state, target state, and timestamp. The history SHALL be accessible for observability and debugging but MUST NOT be mutable by external consumers.

#### Scenario: Transition history records every state change
- **WHEN** `GlobalFSM.transition_to(from, to)` is called and succeeds
- **THEN** a history entry is appended containing `from`, `to`, and the timestamp of the transition

#### Scenario: Failed transition does not appear in history
- **WHEN** `GlobalFSM.transition_to(from, to)` is called and throws `DomainValidationException`
- **THEN** no entry is appended to `_transition_history`

#### Scenario: Transition history is read-only to external consumers
- **WHEN** an external consumer accesses `_transition_history`
- **THEN** the returned collection SHALL be `IReadOnlyList<TransitionRecord>` (or equivalent immutable view); direct mutation of the history SHALL NOT be possible

### Requirement: GlobalFSM instance SHALL be activated and owned by SessionContext

`SessionContext` SHALL own a `GlobalFSM` instance and expose `GlobalState` as a read-only property computed from `GlobalFSM.CurrentState`. The public setter for `GlobalState` SHALL be removed. `SessionContext` SHALL expose the `IGlobalStateMachine` interface publicly for transition queries (`CanTransitionTo`, `GetValidTransitions`), and SHALL expose the concrete `GlobalFSM` internally (`InternalGlobalFSM`) for callback registration (`RegisterStateCallback`, declared on the concrete class, not the interface) and the `ForceState` recovery path.

#### Scenario: GlobalState is read-only via GlobalFSM
- **WHEN** `SessionContext.GlobalState` is accessed
- **THEN** it SHALL return `_globalFsm.CurrentState`
- **AND** there SHALL be no public setter for `GlobalState`

#### Scenario: Callback registration via internal GlobalFSM
- **WHEN** an engine-internal consumer needs to register a state transition callback
- **THEN** it SHALL call `SessionContext.InternalGlobalFSM.RegisterStateCallback(state, callback)`
- **AND** the callback SHALL be invoked when `TransitionTo(targetState)` reaches the specified state

### Requirement: GlobalFSM transitions SHALL be matrix-validated for normal state changes

All normal global state changes SHALL go through `GlobalFSM.TransitionTo()`, which validates the transition against the 8-state matrix, records the transition in `_transitionHistory`, and invokes registered callbacks. Invalid transitions SHALL throw `DomainValidationException`. `SetGlobalState(GlobalState, string?)` on `TraversalRuntimeContext` SHALL delegate to `TransitionTo()`.

#### Scenario: Valid transition succeeds and records history
- **WHEN** `SetGlobalState(Completed, "all_visited")` is called while in `Traversing` state
- **THEN** `TransitionTo` SHALL succeed (Traversing→Completed is valid)
- **AND** the transition SHALL appear in `GetTransitionHistory()`
- **AND** callbacks registered for `GlobalState.Completed` SHALL be invoked

#### Scenario: Invalid transition throws DomainValidationException
- **WHEN** `SetGlobalState(Completed)` is called while in `Idle` state
- **THEN** `TransitionTo` SHALL throw `DomainValidationException` (Idle→Completed is not in matrix)

#### Scenario: Termination from Traversing uses two-step path
- **WHEN** the engine must reach `Terminated` from `Traversing` (StopAsync, cancellation, timeout)
- **THEN** it SHALL transition `Traversing→Paused("stopping")` then `Paused→Terminated` (the locked matrix has no `Traversing→Terminated` edge)

### Requirement: ForceState SHALL bypass matrix for internal state recovery

`GlobalFSM` SHALL provide an `internal ForceState(GlobalState)` method that directly sets `CurrentState` without matrix validation, without invoking callbacks, but still records the transition in `_transitionHistory` with reason `"force_restore"`. This SHALL only be accessible via the internal `InternalGlobalFSM` property on `SessionContext` and SHALL only be used for state restoration scenarios (e.g., `PopupHandler` restoring preserved state after popup handling).

#### Scenario: ForceState records history without callbacks
- **WHEN** `ForceState(Traversing)` is called from `Error` state
- **THEN** `CurrentState` SHALL be set to `Traversing`
- **AND** a `TransitionRecord(Error, Traversing, "force_restore")` SHALL be appended to history
- **AND** no callbacks SHALL be invoked

#### Scenario: ForceState is not accessible via public interface
- **WHEN** a consumer accesses `IGlobalStateMachine`
- **THEN** `ForceState` SHALL NOT be available (not on the interface)

### Requirement: GlobalFSM transitions SHALL be traced via RegisterStateCallback

`TraversalEngine` SHALL register callbacks on `GlobalFSM` during initialization to write state transitions to `ITraceRecorder` via `RecordTransitionAsync`. The `StateTransition.FsmType` SHALL be `"GlobalFSM"` to distinguish from `"TraversalFSM"` transitions.

#### Scenario: GlobalFSM Completion transition is traced
- **WHEN** `TransitionTo(Completed, "all_visited")` is called
- **THEN** a `StateTransition` record with `FsmType = "GlobalFSM"`, `ToState = "Completed"` SHALL be written to `ITraceRecorder`

#### Scenario: ForceState does not produce trace records
- **WHEN** `ForceState(Traversing)` is called
- **THEN** no `StateTransition` trace record SHALL be produced (callbacks are not invoked)

### Requirement: TraversalFSM and GlobalFSM are independent layers

`TraversalFSM` SHALL operate at the micro (step) level and `GlobalFSM` SHALL operate at the macro (session) level. `TraversalFSM` MUST NOT depend on `GlobalFSM` and vice versa — they SHALL NOT share state, transitions, or callback registries. The only coordination point is `TraversalRuntimeContext`, which holds a `GlobalState` field that `GlobalFSM` writes and `TraversalFSM` reads as context, never as a direct dependency. TraversalEngine coordinates both FSMs through `ctx.GlobalState` field — this is the same coordination mechanism described in the original requirement (GlobalFSM writes, TraversalFSM reads as opaque context). The coordination does NOT create shared state between the FSMs — TraversalRuntimeContext.GlobalState is a data field, not FSM infrastructure.

#### Scenario: TraversalFSM does not import GlobalFSM types
- **WHEN** the `using` statements in the TraversalFSM implementation file are inspected
- **THEN** no `using` references the namespace containing `GlobalFSM` or `IGlobalStateMachine`

#### Scenario: GlobalFSM does not import TraversalFSM types
- **WHEN** the `using` statements in the GlobalFSM implementation file are inspected
- **THEN** no `using` references the namespace containing `TraversalFSM` or `TraversalState`

#### Scenario: Coordination is through TraversalRuntimeContext only
- **WHEN** both FSMs need to share macro state information
- **THEN** `TraversalRuntimeContext.GlobalState` is the sole coordination field; `GlobalFSM` writes it, `TraversalFSM` reads it as opaque context

### Requirement: All 8 FSM handlers return Task<TraversalState>

Every handler method in `TraversalFSM` SHALL return `Task<TraversalState>` and use the `Async` suffix. Handlers that do not perform I/O (HandleNodeSelect, HandlePreconditionCheck, HandleBranch, HandleFrameComplete, HandleErrorHandling, HandlePopupHandling) SHALL wrap their synchronous logic in `Task.FromResult()` or be declared `async` with no await.

#### Scenario: HandleNodeSelectAsync returns Task<TraversalState>
- **WHEN** `HandleNodeSelectAsync()` is invoked
- **THEN** it returns `Task<TraversalState>` (Branch or PreconditionCheck)

#### Scenario: HandleBranchAsync returns Task<TraversalState>
- **WHEN** `HandleBranchAsync()` is invoked
- **THEN** it returns `Task<TraversalState>` (NodeSelect or FrameComplete)

## MODIFIED Requirements

### Requirement: HandlePreconditionCheck determines next state based on precondition
HandlePreconditionCheck SHALL transition FSM to Execute when precondition passes, or ErrorHandling when precondition fails. Current implementation always returns Execute (assume pass). Real precondition checking requires ITraversalNode.Precondition (Phase 3 extension). Until then, handler SHALL return Execute with explicit TraceCoordinator.RecordDecision logging the "assume pass" decision.

#### Scenario: Precondition assumed pass
- **WHEN** HandlePreconditionCheck is invoked
- **THEN** handler transitions FSM to Execute
- **THEN** TraceCoordinator.RecordDecision called with "precondition_assume_pass"

#### Scenario: Precondition fails (Phase 3 future)
- **WHEN** ITraversalNode.Precondition returns false (future capability)
- **THEN** handler transitions FSM to ErrorHandling
- **THEN** TraceCoordinator.RecordDecision called with "precondition_failed"

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

### Requirement: HandleErrorHandling selects recovery strategy and transitions FSM
HandleErrorHandling SHALL delegate to RecoveryExecutor for 5-strategy recovery (Retry→Execute, Backtrack→NodeSelect, Skip→Branch, Continue→NodeSelect, Abort→FrameComplete). It SHALL track consecutive errors via TraversalRuntimeContext._consecutiveErrors. Current stub always returns NodeSelect without recovery logic.

#### Scenario: Error recovery with Retry strategy
- **WHEN** ErrorClassifier determines Retry strategy
- **THEN** RecoveryExecutor.Execute returns Retry result
- **THEN** handler transitions FSM to Execute
- **THEN** ConsecutiveErrors incremented

#### Scenario: Error recovery with Abort strategy
- **WHEN** ErrorClassifier determines Abort strategy
- **THEN** RecoveryExecutor.Execute returns Abort + Failure outcome
- **THEN** handler transitions FSM to FrameComplete
- **THEN** ConsecutiveErrors reset to 0

### Requirement: HandlePopupHandling dismisses popup and routes to next state
HandlePopupHandling SHALL delegate to PopupHandler 6-step pipeline for popup detection and dismissal. On successful dismiss, it SHALL route to ResultVerify. On failed dismiss, it SHALL route to ErrorHandling. Current stub always returns ResultVerify without popup handling logic.

#### Scenario: Popup dismissed successfully
- **WHEN** PopupHandler.HandlePopup returns Success=true
- **THEN** handler transitions FSM to ResultVerify

#### Scenario: Popup dismiss failed
- **WHEN** PopupHandler.HandlePopup returns Success=false (fallback)
- **THEN** handler transitions FSM to ErrorHandling

### Requirement: TryHandleScroll prevents infinite loops with progress-based check (D1)
`TryHandleScroll` SHALL prevent infinite loops when scroll does not advance progress. When scroll execution completes, the FSM SHALL compute `progressDelta = newProgress - currentProgress`. If `progressDelta <= Config.ProgressEpsilon`, the FSM SHALL return `FrameComplete` instead of resetting VisitedChildren.

#### Scenario: Scroll without progress advance returns FrameComplete
- **WHEN** scroll execution completes with `progressDelta <= Config.ProgressEpsilon`
- **THEN** FSM returns `FrameComplete`
- **THEN** VisitedChildren is NOT reset

#### Scenario: Scroll with progress advance continues
- **WHEN** scroll execution completes with `progressDelta > Config.ProgressEpsilon`
- **THEN** FSM proceeds to element count check (D2)

### Requirement: TryHandleScroll prevents infinite loops with element count-based check (D2)
`TryHandleScroll` SHALL prevent infinite loops when scroll reveals no new deduplicated elements. Before scroll, the FSM SHALL capture `beforeElementIds` from current page analysis. After scroll, the FSM SHALL re-analyze the page to get `afterElementIds`. The FSM SHALL compute `uniqueBefore = beforeElementIds.Distinct().Count()` and `uniqueAfter = afterElementIds.Distinct().Count()`. If `uniqueAfter <= uniqueBefore`, the FSM SHALL return `FrameComplete`.

#### Scenario: Scroll without new elements returns FrameComplete
- **WHEN** scroll completes but `uniqueAfter <= uniqueBefore`
- **THEN** FSM returns `FrameComplete`
- **THEN** VisitedChildren is NOT reset

#### Scenario: Scroll with new elements continues
- **WHEN** scroll completes and `uniqueAfter > uniqueBefore`
- **THEN** FSM proceeds to reset VisitedChildren

### Requirement: TryHandleScroll checks IsEndOfList before creating ScrollHandler (D5)
`TryHandleScroll` SHALL check `IsEndOfList` BEFORE creating ScrollHandler to avoid unnecessary handler creation. If `IsEndOfList == true`, the FSM SHALL return `FrameComplete` immediately.

#### Scenario: Early exit when at end of list
- **WHEN** `TryHandleScroll` is invoked and `RuntimeContext.IsEndOfList == true`
- **THEN** FSM returns `FrameComplete` immediately
- **THEN** ScrollHandler is NOT created

#### Scenario: ScrollHandler created when not at end
- **WHEN** `TryHandleScroll` is invoked and `RuntimeContext.IsEndOfList == false`
- **THEN** FSM proceeds to create ScrollHandler

### Requirement: HandleBranch supports scroll trigger for DynamicMatch (D3)
`HandleBranch` SHALL check scroll for `DynamicMatch` strategy when no new children can be generated. When `strategy == ChildrenStrategyType.DynamicMatch`, the FSM SHALL first check if there are unvisited children. If no unvisited children exist, the FSM SHALL call `TryHandleScroll(node, depth)`.

#### Scenario: DynamicMatch with unvisited children continues normally
- **WHEN** `HandleBranch` is invoked with `DynamicMatch` strategy and unvisited children exist
- **THEN** FSM returns `NodeSelect`
- **THEN** Scroll is NOT attempted

#### Scenario: DynamicMatch without unvisited children tries scroll
- **WHEN** `HandleBranch` is invoked with `DynamicMatch` strategy and no unvisited children exist
- **THEN** FSM calls `TryHandleScroll(node, depth)`

### Requirement: TryHandleScroll returns FrameComplete when scroll exhausted at root
When scroll is exhausted at the root node (depth=1), `TryHandleScroll` SHALL return `FrameComplete` to complete traversal, NOT `NodeSelect` which would cause an infinite loop.

#### Scenario: Root node scroll exhaustion completes traversal
- **WHEN** `TryHandleScroll` is invoked at depth=1 and scroll is exhausted
- **THEN** FSM returns `FrameComplete`
- **THEN** Traversal completes
