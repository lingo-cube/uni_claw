## ADDED Requirements

### Requirement: NodeStackAdapter interface extraction
The system SHALL provide an `INodeStackAdapter` interface that defines the contract for node stack operations, enabling test mocking without real stack state.

#### Scenario: Interface defines stack operations
- **WHEN** INodeStackAdapter is defined
- **THEN** it includes method signatures for Push, Pop, Peek, and Depth query
- **AND** it exposes read-only access to stack state properties
- **AND** methods return appropriate stack frame or node information

#### Scenario: Concrete class implements interface
- **WHEN** NodeStackAdapter class is defined
- **THEN** it implements INodeStackAdapter
- **AND** all interface members are publicly accessible

#### Scenario: Test code can create mock implementation
- **WHEN** a unit test requires a mock NodeStackAdapter
- **THEN** it can create a class implementing INodeStackAdapter
- **AND** the mock can simulate stack operations without real stack state
