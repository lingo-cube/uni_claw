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
HandleErrorHandling SHALL use TraversalRuntimeContext._consecutiveErrors for consecutive error tracking. On Retry strategy, it SHALL increment _consecutiveErrors. On non-Retry outcome (Backtrack/Skip/Continue/Abort), it SHALL reset _consecutiveErrors to 0.

#### Scenario: Consecutive error increment on Retry
- **WHEN** ErrorHandling selects Retry strategy
- **THEN** TraversalRuntimeContext.ConsecutiveErrors incremented by 1

#### Scenario: Consecutive error reset on Backtrack
- **WHEN** ErrorHandling selects Backtrack strategy
- **THEN** TraversalRuntimeContext.ConsecutiveErrors reset to 0

### Requirement: ErrorHandling handler records trace on each recovery decision
HandleErrorHandling SHALL call TraceCoordinator.RecordStateDecision with the selected ErrorStrategy and FSM transition target. It SHALL also call TraceCoordinator.RecordErrorSpan with the error details (ErrorType, ErrorMessage, Severity).

#### Scenario: Trace recording on Retry
- **WHEN** ErrorHandling selects Retry and transitions to Execute
- **THEN** TraceCoordinator.RecordStateDecision called with "Retry→Execute"
- **THEN** TraceCoordinator.RecordErrorSpan called with error details
