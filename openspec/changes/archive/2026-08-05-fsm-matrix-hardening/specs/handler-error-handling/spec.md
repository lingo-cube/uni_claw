## MODIFIED Requirements

### Requirement: ErrorHandling handler tracks consecutive errors

HandleErrorHandling SHALL use TraversalRuntimeContext.ConsecutiveErrors for consecutive error tracking. It SHALL increment ConsecutiveErrors by 1 for every error recovery attempt, regardless of strategy (Retry, Backtrack, Skip, Continue, or Abort). It SHALL NOT reset ConsecutiveErrors on any strategy — the counter accumulates across successive recovery attempts within the same traversal frame. The call sites that route the FSM into ErrorHandling (StepAsync exception catch, HandlePreconditionCheckAsync, HandleExecuteAsync catch) SHALL NOT increment ConsecutiveErrors — HandleErrorHandlingAsync is the sole increment point.

#### Scenario: Consecutive error increment on Retry
- **WHEN** ErrorHandling selects Retry strategy
- **THEN** TraversalRuntimeContext.ConsecutiveErrors incremented by 1

#### Scenario: Consecutive error increment on Backtrack
- **WHEN** ErrorHandling selects Backtrack strategy
- **THEN** TraversalRuntimeContext.ConsecutiveErrors incremented by 1 (NOT reset)

#### Scenario: Consecutive error increment on Skip
- **WHEN** ErrorHandling selects Skip strategy
- **THEN** TraversalRuntimeContext.ConsecutiveErrors incremented by 1

#### Scenario: Consecutive error increment on Continue
- **WHEN** ErrorHandling selects Continue strategy
- **THEN** TraversalRuntimeContext.ConsecutiveErrors incremented by 1

#### Scenario: Consecutive error increment on Abort
- **WHEN** ErrorHandling selects Abort strategy
- **THEN** TraversalRuntimeContext.ConsecutiveErrors incremented by 1

#### Scenario: StepAsync catch does not increment ConsecutiveErrors
- **WHEN** a handler throws and StepAsync catch routes to ErrorHandling
- **THEN** ConsecutiveErrors is NOT incremented by the catch block (increment deferred to HandleErrorHandlingAsync)

### Requirement: ErrorHandling handler clears LastError after recovery

HandleErrorHandling SHALL clear `TraversalRuntimeContext.LastError` to null upon completing a recovery attempt, at all three return points: the main strategy-return path, the page-item-limit gate path (≥5 distinct failed items → PressBack → FrameComplete), and the consecutive-errors gate path (≥3 consecutive errors → PressBack → FrameComplete). The NoStepContext stub fallback (returns NodeSelect without executing handler logic) is exempt from this requirement.

#### Scenario: LastError cleared after Retry recovery
- **WHEN** ErrorHandling completes a Retry recovery cycle and returns Execute
- **THEN** TraversalRuntimeContext.LastError is null

#### Scenario: LastError cleared after Backtrack recovery
- **WHEN** ErrorHandling completes a Backtrack recovery cycle and returns NodeSelect
- **THEN** TraversalRuntimeContext.LastError is null

#### Scenario: LastError cleared after consecutive-errors gate triggers
- **WHEN** ConsecutiveErrors reaches 3 and PressBack is executed
- **THEN** TraversalRuntimeContext.LastError is null

#### Scenario: LastError cleared after page-item-limit gate triggers
- **WHEN** NodeFailedItems reaches 5 and PressBack is executed
- **THEN** TraversalRuntimeContext.LastError is null
