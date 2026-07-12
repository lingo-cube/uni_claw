# Progress Context Specification

## ADDED Requirements

### Requirement: Progress context tracks progress state
The system SHALL provide a `ProgressContext` class that encapsulates progress control state including step count, max depth, completion policy, action history, and timing configuration.

#### Scenario: Progress context initialization
- **WHEN** a `ProgressContext` is created with maxDepth
- **THEN** the context initializes with maxDepth set, stepCount zero, null completion policy, and empty action history

### Requirement: Step counting
The system SHALL track the number of traversal steps executed.

#### Scenario: Increment step count
- **WHEN** a step completes via `ProgressContext.IncrementStepCount()`
- **THEN** StepCount increases by 1

#### Scenario: Read-only step count access
- **WHEN** consumer accesses `Progress.StepCount`
- **THEN** system returns the current integer value

### Requirement: Max depth constraint
The system SHALL maintain the maximum traversal depth constraint.

#### Scenario: Max depth is set at initialization
- **WHEN** `ProgressContext` is created with maxDepth parameter
- **THEN** MaxDepth is set to the provided value

#### Scenario: Read-only max depth access
- **WHEN** consumer accesses `Progress.MaxDepth`
- **THEN** system returns the max depth value

### Requirement: Completion policy
The system SHALL maintain a completion policy that determines when traversal should end.

#### Scenario: Set completion policy
- **WHEN** completion policy is set via `ProgressContext.SetCompletionPolicy(policy)`
- **THEN** CompletionPolicy is updated with the new policy

#### Scenario: Read-only completion policy access
- **WHEN** consumer accesses `Progress.CompletionPolicy`
- **THEN** system returns the current policy (nullable)

### Requirement: Action history audit
The system SHALL maintain a recent action history (max 5 entries) for auditing and debugging.

#### Scenario: Add action to history
- **WHEN** an action is executed via `ProgressContext.AddActionHistory(record)`
- **THEN** the record is added to ActionHistory

#### Scenario: Action history size limit
- **WHEN** ActionHistory exceeds 5 entries
- **THEN** the oldest entry is removed to maintain max 5 entries

#### Scenario: Read-only action history access
- **WHEN** consumer accesses `Progress.ActionHistory`
- **THEN** system returns `IReadOnlyList<ActionRecord>` that cannot be modified through the interface

### Requirement: Timing configuration
The system SHALL maintain the wait duration after each action for pacing.

#### Scenario: Set wait after action
- **WHEN** wait duration is set via `ProgressContext.SetWaitAfterActionMs(milliseconds)`
- **THEN** WaitAfterActionMs is updated with the new value

#### Scenario: Read-only wait duration access
- **WHEN** consumer accesses `Progress.WaitAfterActionMs`
- **THEN** system returns the current millisecond value

### Requirement: Read-only interface isolation
The system SHALL provide `IProgressContext` interface with only read-only property getters.

#### Scenario: Interface exposes no mutation methods
- **WHEN** consumer holds `IProgressContext` reference
- **THEN** only read-only properties are accessible (StepCount, MaxDepth, CompletionPolicy, ActionHistory, WaitAfterActionMs)

#### Scenario: Mutation methods only on concrete class
- **WHEN** consumer needs to mutate progress state
- **THEN** they must cast to or hold `ProgressContext` concrete class reference
