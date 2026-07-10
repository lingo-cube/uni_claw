## ADDED Requirements

### Requirement: ErrorHandler.HandleError() unified pipeline entry

ErrorHandler SHALL provide a `sealed class ErrorHandler` with a `HandleError(ErrorClassificationContext classificationCtx, StrategySelectionContext strategyCtx, Exception? exception = null)` method that executes a 3-step pipeline: classify → select → execute. The pipeline SHALL wrap all 3 steps in a try/catch that returns `ErrorRecoveryResult(Abort, Failure, 0, "Unhandled exception...")` on any unhandled exception.

#### Scenario: Normal pipeline execution — classify→select→execute
- **WHEN** HandleError() is called with valid ErrorClassificationContext, StrategySelectionContext, and optional Exception
- **THEN** ErrorClassifier.Classify() SHALL be called first
- **THEN** ErrorStrategySelector.SelectStrategy() SHALL be called with the ErrorType and StrategySelectionContext
- **THEN** RecoveryExecutor.Execute() SHALL be called with the ErrorStrategy and an ErrorRecoveryContext built from (errorType, strategyCtx.RetryCount, exception)
- **THEN** the ErrorRecoveryResult from the executor SHALL be returned

#### Scenario: Pipeline-level fallback on any step exception
- **WHEN** any step in the HandleError pipeline throws an Exception
- **THEN** the exception SHALL NOT propagate to the caller
- **THEN** the method SHALL return `ErrorRecoveryResult(ErrorStrategy.Abort, RecoveryOutcome.Failure, 0, "Unhandled exception during error handling: {ex.GetType().Name}: {ex.Message}")`

#### Scenario: ErrorRecoveryContext.RetryCount takes strategyCtx.RetryCount (D-G5)
- **WHEN** HandleError constructs ErrorRecoveryContext
- **THEN** RetryCount MUST come from StrategySelectionContext.RetryCount (authoritative source for strategy decision and backoff calculation)
- **THEN** ErrorClassificationContext.RetryCount MUST NOT be used (it is a noise field — ErrorClassifier never reads it)

#### Scenario: Exception parameter bridges actual Exception object
- **WHEN** HandleError is called with an Exception object
- **THEN** it SHALL be passed to ErrorRecoveryContext.Exception (ErrorClassificationContext.ExceptionType is string, not Exception)
- **WHEN** no Exception is provided
- **THEN** ErrorRecoveryContext.Exception MUST be null (default)

#### Scenario: Constructor injection with optional sub-components
- **WHEN** ErrorHandler is constructed with no arguments
- **THEN** it SHALL create default instances of ErrorClassifier, ErrorStrategySelector, and RecoveryExecutor
- **WHEN** custom sub-component instances are passed via constructor
- **THEN** they SHALL be used instead of defaults

### Requirement: ErrorRecoveryResult.Description field extension

ErrorRecoveryResult SHALL be extended with a `string? Description = null` field. This field MUST be backward compatible — existing code that constructs ErrorRecoveryResult without Description MUST continue to work (default null).

#### Scenario: Pipeline fallback preserves exception diagnostic info
- **WHEN** HandleError pipeline-level try/catch catches an exception
- **THEN** ErrorRecoveryResult.Description MUST contain "Unhandled exception during error handling: {ex.GetType().Name}: {ex.Message}"

#### Scenario: Backward compatibility — existing constructors unaffected
- **WHEN** existing code constructs ErrorRecoveryResult without Description
- **THEN** Description MUST default to null
- **THEN** no existing test or production code MUST break

#### Scenario: Consistency with ContainerActionResult and PopupHandlingResult
- **WHEN** comparing ErrorRecoveryResult with ContainerActionResult and PopupHandlingResult
- **THEN** all 3 handler result types MUST have a Description-like field (ContainerActionResult.Description, PopupHandlingResult.Description, ErrorRecoveryResult.Description)
