## ADDED Requirements

### Requirement: TraceCoordinator interface extraction
The system SHALL provide an `ITraceCoordinator` interface that defines the contract for trace recording operations, enabling test mocking without real I/O operations.

#### Scenario: Interface defines trace lifecycle methods
- **WHEN** ITraceCoordinator is defined
- **THEN** it includes method signatures for BuildCorrelation, RecordStepStart, RecordStepEnd, RecordStateTransition, RecordDecision, RecordErrorSpan, and RecordSpan
- **AND** all methods accept appropriate context parameters
- **AND** the TraceId property is exposed as a read-only accessor

#### Scenario: Concrete class implements interface
- **WHEN** TraceCoordinator class is defined
- **THEN** it implements ITraceCoordinator
- **AND** all interface members are publicly accessible

#### Scenario: Test code can create mock implementation
- **WHEN** a unit test requires a mock TraceCoordinator
- **THEN** it can create a class implementing ITraceCoordinator
- **AND** the mock can verify method calls without writing to real trace storage
