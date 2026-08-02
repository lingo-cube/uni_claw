## MODIFIED Requirements

### Requirement: Consecutive error counter increments on all strategies
The `ConsecutiveErrors` counter SHALL increment on every error, not only on `ErrorStrategy.Retry`.

#### Scenario: Backtrack increments counter
- **WHEN** `HandleErrorHandlingAsync` processes an error AND `ErrorHandler` returns `ErrorStrategy.Backtrack`
- **THEN** `ctx.IncrementConsecutiveErrors()` SHALL be called
- **AND** the counter SHALL NOT be reset to 0

#### Scenario: Counter resets on successful action
- **WHEN** an action succeeds (no error in Execute state)
- **THEN** `ConsecutiveErrors` SHALL be reset to 0

### Requirement: Advisor is called during error handling
The `TraversalAdvisor` SHALL be consulted before the `ErrorHandler` pipeline when `IUniBrain.Advisor` is available.

#### Scenario: Advisor provides recommendation
- **WHEN** `brain.Advisor` is not null
- **THEN** `HandleErrorHandlingAsync` SHALL call `Advisor.DecideAsync(errorContext, pageAnalysis)`
- **AND** the result SHALL be merged with the `ErrorHandler` pipeline output

### Requirement: AI empty response is classified as structural error
The error classifier SHALL recognize AI empty responses as structural errors that SHALL NOT be retried.

#### Scenario: Empty response in AnalyzeOnceAsync
- **WHEN** `ModelResponse.Success` is true AND `ModelResponse.Content` is empty
- **THEN** `PageAnalyzer.IsTransient` SHALL return false
- **AND** the attempt SHALL NOT be retried
