## ADDED Requirements

### Requirement: StepContext uses interface types
The system SHALL define StepContext with interface types for all service dependencies, enabling dependency injection and test mocking.

#### Scenario: StepContext parameters use interface types
- **WHEN** StepContext is defined as a sealed record
- **THEN** all service dependency parameters use interface types (ITraceCoordinator, IPageSnapshotManager, etc.)
- **AND** positional parameter structure remains unchanged
- **AND** parameter names remain unchanged for backward compatibility

#### Scenario: StepContext instantiation accepts interface implementations
- **WHEN** StepContext is instantiated
- **THEN** interface parameters accept concrete implementations
- **AND** interface parameters accept mock implementations for testing
- **AND** the record with-expression continues to work with interface types

#### Scenario: All StepContext consumers update to interface types
- **WHEN** existing code references StepContext service properties
- **THEN** references use interface types (e.g., StepContext.Trace is ITraceCoordinator)
- **AND** compile errors guide updates to any missed references
