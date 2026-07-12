## ADDED Requirements

### Requirement: PageSnapshotManager interface extraction
The system SHALL provide an `IPageSnapshotManager` interface that defines the contract for page fingerprint and snapshot comparison, enabling test mocking without real I/O.

#### Scenario: Interface defines snapshot methods
- **WHEN** IPageSnapshotManager is defined
- **THEN** it includes method signatures for capturing snapshots and comparing page changes
- **AND** methods accept PageAnalysis for comparison
- **AND** comparison methods return boolean indicating change detection

#### Scenario: Concrete class implements interface
- **WHEN** PageSnapshotManager class is defined
- **THEN** it implements IPageSnapshotManager
- **AND** all interface members are publicly accessible

#### Scenario: Test code can create mock implementation
- **WHEN** a unit test requires a mock PageSnapshotManager
- **THEN** it can create a class implementing IPageSnapshotManager
- **AND** the mock can simulate page changes without real vision API calls
