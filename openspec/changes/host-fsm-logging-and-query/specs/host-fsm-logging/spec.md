## ADDED Requirements

### Requirement: SafeActionExecutor logs action execution at Info level

`SafeActionExecutor` SHALL log each executed action (click / scroll / back / input / long_press / wait)
at `Information` level with the action name and result (ok / failed).

#### Scenario: Successful action execution log
- **WHEN** a `TapAsync` call passes safety gate and executes successfully
- **THEN** a log line `action=click result=ok` is written at Information level with category `SafeActionExecutor`

#### Scenario: Failed action execution log
- **WHEN** a `SwipeAsync` call executes but the underlying device operation returns false
- **THEN** a log line `action=scroll result=failed` is written at Information level

### Requirement: SafeActionExecutor logs denied actions at Warning level

`SafeActionExecutor` SHALL log each safety-gate-denied action at `Warning` level,
including the action name and the rule ID that denied it.

#### Scenario: Action denied by safety gate
- **WHEN** a `SwipeAsync` call is evaluated by `DecideAsync` and the safety decision is `!Allowed`
- **THEN** a log line `action=scroll → deny rule=<RuleId>` is written at Warning level with category `SafeActionExecutor`

### Requirement: InvalidatingPageAnalysisCache logs analysis summary at Info level

`InvalidatingPageAnalysisCache` SHALL log each cache-miss page analysis at `Information` level,
including the page path, item count, scroll availability, and end-of-list status.

#### Scenario: Cache-miss analysis log
- **WHEN** `AnalyzeCurrentPageAsync` is called, the cache is empty or invalidated, and the inner analyzer returns a non-null `PageAnalysis`
- **THEN** a log line `page=<Path> items=<Count> scroll=<HasScroll> endOfList=<EndOfList>` is written at Information level with category `InvalidatingPageAnalysisCache`

#### Scenario: Cache-hit analysis produces no log
- **WHEN** `AnalyzeCurrentPageAsync` is called and a valid cached analysis exists
- **THEN** no log line is written from the cache-hit path

### Requirement: TraversalFSM logs normal state transitions at Info level

`TraversalFSM` SHALL log each successful state transition at `Information` level,
including the source state, target state, and current step number.

#### Scenario: Normal FSM transition log
- **WHEN** `TransitionTo(nextState)` is called on a valid transition
- **THEN** a log line `FSM <FromState>→<ToState> step=<StepNumber>` is written at Information level with category `TraversalFSM`

#### Scenario: Rejected transition preserves existing Warning behavior
- **WHEN** `TransitionTo` throws `DomainValidationException` for an invalid transition
- **THEN** the existing `LogWarning` for rejected transitions is preserved unchanged

### Requirement: TraversalEngine logs termination reason at Info level

`TraversalEngine` SHALL log the engine termination reason and step count at `Information` level
immediately before returning each `TraversalResult`.

#### Scenario: Engine termination log on all_visited
- **WHEN** the engine completes with completion reason `AllVisited`
- **THEN** a log line `Engine terminated reason=all_visited steps=<StepCount>` is written at Information level with category `TraversalEngine`

#### Scenario: Engine termination log on max_steps
- **WHEN** the engine reaches `MaxSteps` and returns
- **THEN** a log line `Engine terminated reason=max_steps steps=<StepCount>` is written at Information level

### Requirement: ILogger injection follows NullLogger optional pattern

New `ILogger<T>` parameters on `SafeActionExecutor` and `InvalidatingPageAnalysisCache` SHALL be
optional constructor parameters with `NullLogger<T>.Instance` default, matching the existing pattern
on `TraversalFSM`, `ErrorHandler`, and `TraversalEngine`.

#### Scenario: No logger injected — NullLogger default
- **WHEN** a caller constructs `SafeActionExecutor` without providing an `ILogger<SafeActionExecutor>` parameter
- **THEN** no log output is produced and no exception is thrown

#### Scenario: Logger injected through composition root
- **WHEN** `HostCommands.CreateRunServices` constructs `SafeActionExecutor` with an `ILogger<SafeActionExecutor>` created from `LoggerFactory`
- **THEN** log lines are written to both the console provider and file provider via the shared factory
