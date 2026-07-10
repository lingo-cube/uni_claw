# ErrorHandler Spec

> Classify → Select → Execute pipeline for error recovery

## ADDED Requirements

### Requirement: ErrorClassifier priority chain classification

ErrorHandler SHALL provide an `ErrorClassifier` that maps raw error contexts to `ErrorType` enum values through a priority-chain pattern matching system.

#### Scenario: ErrorType enum values

WHEN `ErrorType` is defined
THEN it SHALL contain exactly 6 values: CRASH, PERMISSION, TIMEOUT, NETWORK, UI_ELEMENT, UNKNOWN
AND each value SHALL be distinct and SHALL NOT overlap in semantic scope

#### Scenario: CRASH priority — highest

WHEN `ErrorClassifier.classify()` is called on an error context
AND the error context matches the CRASH pattern
THEN the classifier SHALL return `ErrorType.CRASH`
AND CRASH SHALL have the highest priority in the classification chain (priority 1)

#### Scenario: PERMISSION priority — second

WHEN the error context does not match CRASH
AND the error context matches the PERMISSION pattern
THEN the classifier SHALL return `ErrorType.PERMISSION`
AND PERMISSION SHALL have priority 2 in the classification chain

#### Scenario: TIMEOUT priority — third

WHEN the error context does not match CRASH or PERMISSION
AND the error context matches the TIMEOUT pattern
THEN the classifier SHALL return `ErrorType.TIMEOUT`
AND TIMEOUT SHALL have priority 3 in the classification chain

#### Scenario: NETWORK priority — fourth

WHEN the error context does not match CRASH, PERMISSION, or TIMEOUT
AND the error context matches the NETWORK pattern
THEN the classifier SHALL return `ErrorType.NETWORK`
AND NETWORK SHALL have priority 4 in the classification chain

#### Scenario: UI_ELEMENT priority — fifth

WHEN the error context does not match CRASH, PERMISSION, TIMEOUT, or NETWORK
AND the error context matches the UI_ELEMENT pattern
THEN the classifier SHALL return `ErrorType.UI_ELEMENT`
AND UI_ELEMENT SHALL have priority 5 in the classification chain

#### Scenario: Exception type fallback before UNKNOWN

WHEN the error context does not match any of the 5 named patterns
AND the underlying exception type can be identified
THEN the classifier SHALL attempt to map the exception type to an `ErrorType`
AND if the exception type maps to a known ErrorType, SHALL return that mapped type
AND this fallback SHALL have priority 6 in the chain

#### Scenario: UNKNOWN — lowest priority catch-all

WHEN the error context does not match any named pattern
AND no exception type mapping is available or applicable
THEN the classifier SHALL return `ErrorType.UNKNOWN`
AND UNKNOWN SHALL have the lowest priority (priority 7, catch-all)

#### Scenario: Substring matching not regex

WHEN `ErrorClassifier.classify()` performs pattern matching against error messages or context strings
THEN the classifier SHALL use substring matching (contains/indexOf semantics)
AND SHALL NOT use regular expression (regex) matching
AND substring matching SHALL be case-insensitive

---

### Requirement: ErrorStrategySelector applicability-based selection

ErrorHandler SHALL provide an `ErrorStrategySelector` that maps classified `ErrorType` values to `ErrorStrategy` values through per-type strategy priority chains with applicability checks.

#### Scenario: ErrorStrategy enum values

WHEN `ErrorStrategy` is defined
THEN it SHALL contain exactly 5 values: RETRY, BACKTRACK, SKIP, CONTINUE, ABORT
AND each value SHALL represent a distinct recovery approach

#### Scenario: Each ErrorType has a strategy priority chain

WHEN `ErrorStrategySelector.select_strategy()` is called with a given `ErrorType`
THEN the selector SHALL evaluate strategies in a priority chain specific to that ErrorType
AND each ErrorType SHALL have its own ordered list of preferred strategies (6 ErrorType × strategy priority chains)

#### Scenario: RETRY applicability — retry count under maximum

WHEN the selector evaluates `ErrorStrategy.RETRY` as a candidate for the current ErrorType priority chain
THEN the selector SHALL check whether `retry_count < max_retries` for the current error context
AND if `retry_count >= max_retries`, SHALL skip RETRY and proceed to the next strategy in the chain
AND if `retry_count < max_retries`, SHALL select RETRY as the recovery strategy

#### Scenario: BACKTRACK applicability — can backtrack and stack depth > 1

WHEN the selector evaluates `ErrorStrategy.BACKTRACK` as a candidate
THEN the selector SHALL check both conditions: `can_backtrack == true` AND `node_stack.Count > 1`
AND if either condition is false, SHALL skip BACKTRACK and proceed to the next strategy
AND if both conditions are true, SHALL select BACKTRACK as the recovery strategy

#### Scenario: SKIP applicability — can skip

WHEN the selector evaluates `ErrorStrategy.SKIP` as a candidate
THEN the selector SHALL check whether `can_skip == true` for the current error context
AND if `can_skip == false`, SHALL skip SKIP and proceed to the next strategy
AND if `can_skip == true`, SHALL select SKIP as the recovery strategy

#### Scenario: CONTINUE applicability — always applicable

WHEN the selector evaluates `ErrorStrategy.CONTINUE` as a candidate
THEN the selector SHALL always consider CONTINUE as applicable
AND SHALL NOT require any precondition check
AND SHALL select CONTINUE if no higher-priority applicable strategy is found

#### Scenario: ABORT applicability — always applicable

WHEN the selector evaluates `ErrorStrategy.ABORT` as a candidate
THEN the selector SHALL always consider ABORT as applicable
AND SHALL NOT require any precondition check
AND SHALL select ABORT if no higher-priority applicable strategy is found
AND ABORT SHALL be the terminal fallback in every ErrorType's priority chain

---

### Requirement: RecoveryExecutor hook dispatch with backoff

ErrorHandler SHALL provide a `RecoveryExecutor` that executes selected `ErrorStrategy` actions through a hook-based dispatch table with exponential backoff for retries.

#### Scenario: Hook dispatch table structure

WHEN `RecoveryExecutor` is initialized
THEN it SHALL contain a dispatch table of type `Dictionary<ErrorStrategy, Func<ErrorContext, ErrorRecoveryResult>>`
AND the dispatch table SHALL contain exactly 5 hooks for the 5 `ErrorStrategy` values: SKIP, RETRY, BACKTRACK, CONTINUE, ABORT
AND each hook SHALL be a `Func<ErrorContext, ErrorRecoveryResult>` delegate

#### Scenario: SKIP hook — returns success

WHEN `RecoveryExecutor.execute()` is called with `ErrorStrategy.SKIP`
THEN the executor SHALL invoke the SKIP hook delegate
AND the hook SHALL return an `ErrorRecoveryResult` with outcome `success`
AND SHALL NOT attempt any retry or navigation change

#### Scenario: RETRY hook — exponential backoff capped at 10 seconds

WHEN `RecoveryExecutor.execute()` is called with `ErrorStrategy.RETRY`
THEN the executor SHALL invoke the RETRY hook delegate
AND the hook SHALL apply exponential backoff with delay formula `min(2^retry_count, 10)` seconds
AND the backoff delay SHALL NOT exceed 10 seconds regardless of retry_count
AND SHALL return an `ErrorRecoveryResult` indicating the retry was scheduled

#### Scenario: BACKTRACK hook — pop stack and return to parent

WHEN `RecoveryExecutor.execute()` is called with `ErrorStrategy.BACKTRACK`
THEN the executor SHALL invoke the BACKTRACK hook delegate
AND the hook SHALL pop the current node from the navigation stack
AND SHALL return navigation to the parent node (the node at the top of the stack after pop)
AND SHALL return an `ErrorRecoveryResult` indicating backtrack was performed

#### Scenario: CONTINUE hook — returns success

WHEN `RecoveryExecutor.execute()` is called with `ErrorStrategy.CONTINUE`
THEN the executor SHALL invoke the CONTINUE hook delegate
AND the hook SHALL return an `ErrorRecoveryResult` with outcome `success`
AND SHALL NOT alter navigation state or retry counters

#### Scenario: ABORT hook — returns failure

WHEN `RecoveryExecutor.execute()` is called with `ErrorStrategy.ABORT`
THEN the executor SHALL invoke the ABORT hook delegate
AND the hook SHALL return an `ErrorRecoveryResult` with outcome `failure`
AND SHALL signal that the traversal session cannot continue

#### Scenario: Exception fallback to ABORT

WHEN any hook delegate throws an exception during execution
THEN the executor SHALL NOT propagate the exception
AND SHALL fall back to returning an `ErrorRecoveryResult` with outcome `failure` (equivalent to ABORT outcome)
AND SHALL NOT attempt any other recovery strategy as fallback

---

### Requirement: ErrorHandler statistics

ErrorHandler SHALL track and report error recovery statistics across all handled errors.

#### Scenario: Statistics fields

WHEN `ErrorHandler.statistics` is accessed
THEN it SHALL expose the following fields:
- `total_errors`: total number of errors that entered the handler pipeline
- `recovered_count`: number of errors that were recovered to a successful outcome
- `error_statistics`: `Dictionary<ErrorType, int>` counting how many times each ErrorType was classified
- `recovery_rate`: ratio of recovered_count to total_errors (computed as total_errors > 0 ? recovered_count / total_errors : 0.0)

#### Scenario: Statistics are immutable snapshots

WHEN `ErrorHandler.statistics` is read
THEN the returned statistics object SHALL be an immutable snapshot at the point of query
AND subsequent handler activity SHALL NOT mutate the previously returned snapshot
AND each read SHALL produce a new snapshot reflecting the current state

---

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
