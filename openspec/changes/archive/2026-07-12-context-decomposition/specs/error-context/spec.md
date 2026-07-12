# Error Context Specification

## ADDED Requirements

### Requirement: Error context tracks error state
The system SHALL provide an `ErrorContext` class that encapsulates error tracking state including failed nodes, consecutive errors, retry count, and error chain.

#### Scenario: Error context initialization
- **WHEN** an `ErrorContext` is created
- **THEN** the context initializes with empty FailedNodes dictionary and zero counters

### Requirement: Failed nodes registry
The system SHALL maintain a dictionary mapping node IDs to their error records.

#### Scenario: Add failed node
- **WHEN** a node fails via `ErrorContext.AddFailedNode(nodeId, errorRecord)`
- **THEN** the nodeId is added to FailedNodes with the associated error record

#### Scenario: Overwrite existing failed node
- **WHEN** AddFailedNode is called for an existing nodeId
- **THEN** the error record is overwritten with the new value

#### Scenario: Read-only failed nodes access
- **WHEN** consumer accesses `Error.FailedNodes`
- **THEN** system returns `IReadOnlyDictionary<string, ErrorRecord>` that cannot be modified through the interface

### Requirement: Consecutive error tracking
The system SHALL track a streak of consecutive errors for recovery decisions.

#### Scenario: Increment consecutive errors
- **WHEN** an error occurs via `ErrorContext.IncrementConsecutiveErrors()`
- **THEN** ConsecutiveErrors counter increases by 1

#### Scenario: Reset consecutive errors on success
- **WHEN** an operation succeeds via `ErrorContext.ResetConsecutiveErrors()`
- **THEN** ConsecutiveErrors counter is reset to 0

#### Scenario: Read-only consecutive errors access
- **WHEN** consumer accesses `Error.ConsecutiveErrors`
- **THEN** system returns the current integer value

### Requirement: Retry counting for backoff
The system SHALL track retry count for the current node to support exponential backoff.

#### Scenario: Increment retry count
- **WHEN** a retry is attempted via `ErrorContext.IncrementRetryCount()`
- **THEN** RetryCount counter increases by 1

#### Scenario: Reset retry count on new node
- **WHEN** processing moves to a new node
- **THEN** RetryCount is reset (typically by creating fresh ErrorContext or explicit reset)

#### Scenario: Read-only retry count access
- **WHEN** consumer accesses `Error.RetryCount`
- **THEN** system returns the current integer value

### Requirement: Last error tracking
The system SHALL track the most recent exception for immediate error context.

#### Scenario: Set last error
- **WHEN** an exception occurs via setting `ErrorContext.LastError` property
- **THEN** LastError is updated with the exception reference

#### Scenario: Read-only last error access
- **WHEN** consumer accesses `Error.LastError`
- **THEN** system returns the current exception (nullable)

### Requirement: Exception chain for error accumulation
The system SHALL maintain a chain of exceptions for debugging and recovery analysis.

#### Scenario: Set exception chain
- **WHEN** multiple exceptions accumulate via setting `ErrorContext.ExceptionChain` property
- **THEN** ExceptionChain is updated with the list of exceptions

#### Scenario: Read-only exception chain access
- **WHEN** consumer accesses `Error.ExceptionChain`
- **THEN** system returns the current exception list (nullable)

### Requirement: Read-only interface isolation
The system SHALL provide `IErrorContext` interface with only read-only property getters.

#### Scenario: Interface exposes no mutation methods
- **WHEN** consumer holds `IErrorContext` reference
- **THEN** only read-only properties are accessible (FailedNodes, ConsecutiveErrors, RetryCount, etc.)

#### Scenario: Mutation methods only on concrete class
- **WHEN** consumer needs to mutate error state
- **THEN** they must cast to or hold `ErrorContext` concrete class reference
