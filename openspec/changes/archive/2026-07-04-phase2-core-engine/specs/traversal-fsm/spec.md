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

`TraversalFSM.step()` SHALL execute a single FSM step. The method SHALL record the `from_state`, dispatch to the appropriate handler method based on `from_state`, and transition to the handler's returned `TraversalState`. The dispatch SHALL use enum-based switch, not if/elif chains.

#### Scenario: step dispatches to correct handler for each state
- **WHEN** `step()` is called while the FSM is in state `S`
- **THEN** the handler corresponding to `S` is invoked (e.g., `NodeSelect` → `_handle_node_select`, `PreconditionCheck` → `_handle_precondition_check`)

#### Scenario: step wraps handler execution in try-catch
- **WHEN** a handler method throws an unhandled exception during `step()`
- **THEN** the exception is caught, `context.last_error` is set to the exception, `consecutive_errors` is incremented, and the FSM routes to `ErrorHandling` regardless of which state the handler was for

#### Scenario: step records from_state before dispatch
- **WHEN** `step()` begins execution
- **THEN** the current state is recorded as `from_state` before any handler is invoked

#### Scenario: step calls transition_to with handler result
- **WHEN** a handler returns a `TraversalState` value `next_state`
- **THEN** `transition_to(from_state, next_state)` is called to validate and execute the state change

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

### Requirement: TraversalFSM and GlobalFSM are independent layers

`TraversalFSM` SHALL operate at the micro (step) level and `GlobalFSM` SHALL operate at the macro (session) level. `TraversalFSM` MUST NOT depend on `GlobalFSM` and vice versa — they SHALL NOT share state, transitions, or callback registries. The only coordination point is `TraversalRuntimeContext`, which holds a `GlobalState` field that `GlobalFSM` writes and `TraversalFSM` reads as context, never as a direct dependency.

#### Scenario: TraversalFSM does not import GlobalFSM types
- **WHEN** the `using` statements in the TraversalFSM implementation file are inspected
- **THEN** no `using` references the namespace containing `GlobalFSM` or `IGlobalStateMachine`

#### Scenario: GlobalFSM does not import TraversalFSM types
- **WHEN** the `using` statements in the GlobalFSM implementation file are inspected
- **THEN** no `using` references the namespace containing `TraversalFSM` or `TraversalState`

#### Scenario: Coordination is through TraversalRuntimeContext only
- **WHEN** both FSMs need to share macro state information
- **THEN** `TraversalRuntimeContext.GlobalState` is the sole coordination field; `GlobalFSM` writes it, `TraversalFSM` reads it as opaque context
