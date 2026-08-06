## ADDED Requirements

### Requirement: ErrorHandling handler delegates to RecoveryExecutor
HandleErrorHandling SHALL delegate recovery logic to RecoveryExecutor (existing dispatch-table pattern). It SHALL NOT implement recovery logic directly. It SHALL call RecoveryExecutor.Execute(errorStrategy, context) where errorStrategy is determined by ErrorClassifier + ErrorStrategySelector (existing components).

#### Scenario: Retry strategy selected
- **WHEN** HandleErrorHandling is invoked and ErrorClassifier determines Retry strategy
- **THEN** RecoveryExecutor.Execute returns ErrorRecoveryResult with Strategy=Retry, Outcome=Success
- **THEN** handler transitions FSM to Execute (retry current action)

#### Scenario: Backtrack strategy selected
- **WHEN** ErrorClassifier determines Backtrack strategy
- **THEN** RecoveryExecutor.Execute returns Backtrack result
- **THEN** handler transitions FSM to NodeSelect (go back, select different node)

#### Scenario: Skip strategy selected
- **WHEN** ErrorClassifier determines Skip strategy
- **THEN** RecoveryExecutor.Execute returns Skip result
- **THEN** handler transitions FSM to Branch (skip current node, move to next child)

#### Scenario: Continue strategy selected
- **WHEN** ErrorClassifier determines Continue strategy (pretend error didn't occur)
- **THEN** RecoveryExecutor.Execute returns Continue result
- **THEN** handler transitions FSM to NodeSelect (proceed to next node selection)

#### Scenario: Abort strategy selected
- **WHEN** ErrorClassifier determines Abort strategy
- **THEN** RecoveryExecutor.Execute returns Abort + RecoveryOutcome.Failure
- **THEN** handler transitions FSM to FrameComplete (terminate traversal)

### Requirement: ErrorHandling handler tracks consecutive errors

HandleErrorHandling SHALL use TraversalRuntimeContext.ConsecutiveErrors for consecutive error tracking. It SHALL increment ConsecutiveErrors by 1 for every error recovery attempt, regardless of strategy (Retry, Backtrack, Skip, Continue, or Abort). It SHALL NOT reset ConsecutiveErrors on any strategy — the counter accumulates across successive recovery attempts within the same traversal frame. The call sites that route the FSM into ErrorHandling (StepAsync exception catch, HandlePreconditionCheckAsync, HandleExecuteAsync catch) SHALL NOT increment ConsecutiveErrors — HandleErrorHandlingAsync is the sole increment point (D-242).

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

HandleErrorHandling SHALL clear `TraversalRuntimeContext.LastError` to null upon completing a recovery attempt, at all three return points: the main strategy-return path, the page-item-limit gate path (≥5 distinct failed items → PressBack → FrameComplete), and the consecutive-errors gate path (≥3 consecutive errors → PressBack → FrameComplete). The NoStepContext stub fallback (returns NodeSelect without executing handler logic) is exempt from this requirement (D-243).

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

### Requirement: ErrorHandling handler records trace on each recovery decision

HandleErrorHandling SHALL call TraceCoordinator.RecordStateDecision with the selected ErrorStrategy and FSM transition target. It SHALL also call TraceCoordinator.RecordErrorSpan with the error details (ErrorType, ErrorMessage, Severity).

#### Scenario: Trace recording on Retry
- **WHEN** ErrorHandling selects Retry and transitions to Execute
- **THEN** TraceCoordinator.RecordStateDecision called with "Retry→Execute"
- **THEN** TraceCoordinator.RecordErrorSpan called with error details
HandleErrorHandling SHALL call TraceCoordinator.RecordStateDecision with the selected ErrorStrategy and FSM transition target. It SHALL also call TraceCoordinator.RecordErrorSpan with the error details (ErrorType, ErrorMessage, Severity).

#### Scenario: Trace recording on Retry
- **WHEN** ErrorHandling selects Retry and transitions to Execute
- **THEN** TraceCoordinator.RecordStateDecision called with "Retry→Execute"
- **THEN** TraceCoordinator.RecordErrorSpan called with error details
