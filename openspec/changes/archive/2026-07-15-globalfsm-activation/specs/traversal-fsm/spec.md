## ADDED Requirements

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
