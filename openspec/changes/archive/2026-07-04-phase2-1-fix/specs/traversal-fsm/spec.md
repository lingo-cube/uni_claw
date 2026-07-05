## MODIFIED Requirements

### Requirement: TraversalFSM defines exactly 8 states

`TraversalFSM` SHALL define exactly 8 states as a `TraversalState` enum: `NodeSelect`, `PreconditionCheck`, `Execute`, `ResultVerify`, `Branch`, `FrameComplete`, `ErrorHandling`, `PopupHandling`. `DynamicMatch` MUST NOT appear as a `TraversalState` value — it is a `ChildrenStrategy` value, not an FSM state.

#### Scenario: Enum members match the 8 canonical states
- **WHEN** `TraversalState` enum members are enumerated
- **THEN** exactly 8 members exist: `NodeSelect`, `PreconditionCheck`, `Execute`, `ResultVerify`, `Branch`, `FrameComplete`, `ErrorHandling`, `PopupHandling`

#### Scenario: DynamicMatch is not a TraversalState
- **WHEN** `TraversalState` enum members are enumerated
- **THEN** `DynamicMatch` is not present among the members

#### Scenario: No code references TraversalState.DynamicMatch
- **WHEN** a grep for `TraversalState.DynamicMatch` across all source and test files is performed
- **THEN** the grep SHALL return 0 results
