## MODIFIED Requirements

### Requirement: TraversalFSM transition matrix is enforced with D-1 correction

`TraversalFSM` SHALL enforce a strict transition matrix. Each transition from a source state to a target state MUST be validated against the matrix. Invalid transitions SHALL throw `DomainValidationException`. The transition matrix SHALL reflect decision D-1: the `PreconditionCheck → Branch` transition is REMOVED because the Python V6.7 `_handle_precondition_check` handler never returns `Branch` (only `Execute` or `ErrorHandling`), and `precondition_failed()` is dead code. The matrix SHALL also exclude three additional dead edges identified by fsm-analyzer handler return-value audit (2026-08-05): `Execute→Branch` (HandleExecuteAsync never returns Branch), `Branch→PreconditionCheck` (HandleBranchAsync never returns PreconditionCheck), `FrameComplete→ErrorHandling` (HandleFrameCompleteAsync is pure Task.FromResult and cannot throw). The matrix SHALL contain exactly 19 edges covering all 8 source states, each edge having at least one handler that explicitly returns it.

The canonical transition matrix (19 edges):

| Source | Allowed Targets |
|--------|-----------------|
| `NodeSelect` | `PreconditionCheck`, `Branch` |
| `PreconditionCheck` | `Execute`, `ErrorHandling` |
| `Execute` | `ResultVerify`, `ErrorHandling` |
| `ResultVerify` | `Branch`, `PopupHandling`, `ErrorHandling` |
| `Branch` | `NodeSelect`, `FrameComplete`, `ErrorHandling` |
| `FrameComplete` | `NodeSelect` |
| `ErrorHandling` | `NodeSelect`, `Execute`, `FrameComplete`, `Branch` |
| `PopupHandling` | `ResultVerify`, `ErrorHandling` |

#### Scenario: All valid transitions are accepted
- **WHEN** `TraversalFSM.transition_to(from, to)` is called for every (from, to) pair listed in the 19-edge canonical matrix
- **THEN** each call succeeds without exception

#### Scenario: PreconditionCheck to Branch is rejected (D-1 enforcement)
- **WHEN** `TraversalFSM.transition_to(PreconditionCheck, Branch)` is called
- **THEN** `DomainValidationException` is thrown with `FieldName` indicating the invalid transition and `IllegalValue` containing `"PreconditionCheck→Branch"`

#### Scenario: Removed dead edges are rejected
- **WHEN** `TraversalFSM.transition_to(from, to)` is called for Execute→Branch, Branch→PreconditionCheck, FrameComplete→ErrorHandling, NodeSelect→ErrorHandling, ErrorHandling→ErrorHandling, or FrameComplete→FrameComplete
- **THEN** `DomainValidationException` is thrown for each

#### Scenario: Every invalid transition is rejected
- **WHEN** `TraversalFSM.transition_to(from, to)` is called for any (from, to) pair NOT listed in the 19-edge canonical matrix
- **THEN** `DomainValidationException` is thrown

#### Scenario: Transition matrix covers all 8 source states
- **WHEN** the transition matrix entries are inspected
- **THEN** every `TraversalState` enum member appears as a source key with at least one allowed target

#### Scenario: No source state allows transition to itself
- **WHEN** each source state's allowed targets are inspected
- **THEN** the source state itself is not among its allowed targets (no self-loops)

### Requirement: HandleErrorHandling selects recovery strategy and transitions FSM

HandleErrorHandling SHALL delegate to RecoveryExecutor for 5-strategy recovery (Retry→Execute, Backtrack→NodeSelect, Skip→Branch, Continue→NodeSelect, Abort→FrameComplete). It SHALL track consecutive errors via TraversalRuntimeContext._consecutiveErrors, incrementing once per recovery attempt for all strategies (never resetting). ConsecutiveErrors SHALL be the sole increment point for error recovery counting—caller sites (StepAsync catch, HandlePreconditionCheckAsync, HandleExecuteAsync catch) SHALL NOT increment. After a complete error handling cycle (regardless of strategy outcome), HandleErrorHandling SHALL clear LastError to null at all three return points: the main strategy-return path, the page-item-limit gate path (≥5 distinct failed items), and the consecutive-errors gate path (≥3 consecutive errors).

#### Scenario: Error recovery with Retry strategy
- **WHEN** ErrorClassifier determines Retry strategy
- **THEN** RecoveryExecutor.Execute returns Retry result
- **THEN** handler transitions FSM to Execute
- **THEN** ConsecutiveErrors incremented by 1
- **THEN** LastError cleared to null

#### Scenario: Error recovery with Backtrack strategy
- **WHEN** ErrorClassifier determines Backtrack strategy
- **THEN** RecoveryExecutor.Execute returns Backtrack result
- **THEN** handler transitions FSM to NodeSelect
- **THEN** ConsecutiveErrors incremented by 1 (NOT reset)
- **THEN** LastError cleared to null

#### Scenario: Consecutive errors gate triggers PressBack and clears LastError
- **WHEN** ConsecutiveErrors reaches 3 and depth > 1
- **THEN** handler executes PressBack and transitions FSM to FrameComplete
- **THEN** LastError cleared to null

#### Scenario: Page-item limit gate triggers PressBack and clears LastError
- **WHEN** NodeFailedItems reaches 5 and depth > 1
- **THEN** handler executes PressBack and transitions FSM to FrameComplete
- **THEN** LastError cleared to null

#### Scenario: Full error cycle does not double-increment ConsecutiveErrors
- **WHEN** an Execute handler throws → StepAsync catch routes to ErrorHandling (without incrementing) → HandleErrorHandlingAsync executes recovery
- **THEN** ConsecutiveErrors is incremented exactly once (by HandleErrorHandlingAsync)

### Requirement: HandlePopupHandling dismisses popup and routes to next state

HandlePopupHandling SHALL delegate to PopupHandler 6-step pipeline for popup detection and dismissal. On successful dismiss, it SHALL route to ResultVerify. On failed dismiss, it SHALL set LastError to an `InvalidOperationException` describing the failure (message SHALL begin with `"Popup dismiss failed:"`; message SHALL NOT contain PopupType or DismissStrategy enum names to prevent ErrorClassifier substring collision) before routing to ErrorHandling.

#### Scenario: Popup dismissed successfully
- **WHEN** PopupHandler.HandlePopup returns Success=true
- **THEN** handler transitions FSM to ResultVerify

#### Scenario: Popup dismiss failed
- **WHEN** PopupHandler.HandlePopup returns Success=false (fallback)
- **THEN** handler sets LastError to InvalidOperationException with message "Popup dismiss failed: dismiss_action=<action>" (or "Popup dismiss failed: action=<action>" when Classification is null)
- **THEN** handler transitions FSM to ErrorHandling

#### Scenario: Popup dismiss failure message does not collide with ErrorClassifier
- **WHEN** PopupHandler.HandlePopup returns Success=false
- **THEN** the LastError exception message does NOT contain the substrings "Permission", "Error", "Timeout", "Ad", "Dialog", or "Anr"

## ADDED Requirements

### Requirement: StepAsync exception routing uses safe degradation

`TraversalFSM.StepAsync` SHALL NOT unconditionally route to ErrorHandling when a handler throws. Instead, it SHALL guard the transition with `CanTransitionTo(TraversalState.ErrorHandling)`. If the current state's matrix row does not include ErrorHandling as a valid target, it SHALL degrade to a state-specific safe fallback: NodeSelect→Branch, FrameComplete→NodeSelect, ErrorHandling→FrameComplete. The degradation SHALL NOT throw DomainValidationException.

#### Scenario: Handler exception from ErrorHandling state degrades safely
- **WHEN** HandleErrorHandlingAsync throws an uncaught exception
- **THEN** StepAsync catch sets LastError to the thrown exception
- **THEN** CanTransitionTo(ErrorHandling) returns false (self-loop not in matrix)
- **THEN** nextState is set to FrameComplete (ErrorHandling→FrameComplete is valid)
- **THEN** no DomainValidationException is thrown

#### Scenario: Handler exception from NodeSelect state degrades safely
- **WHEN** a handler throws from NodeSelect state
- **THEN** CanTransitionTo(ErrorHandling) returns false (not in NodeSelect row)
- **THEN** nextState is set to Branch (NodeSelect→Branch is valid)
- **THEN** no DomainValidationException is thrown

#### Scenario: Handler exception from states with ErrorHandling in matrix routes normally
- **WHEN** a handler throws from PreconditionCheck, Execute, ResultVerify, Branch, or PopupHandling state
- **THEN** CanTransitionTo(ErrorHandling) returns true
- **THEN** nextState is set to ErrorHandling (existing behavior preserved)
