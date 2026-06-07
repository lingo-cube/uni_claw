# Spec: Simulation Runner

## ADDED Requirements

### Requirement: Stateful Service Integration
The system SHALL support stateful mock services in simulation runner.

#### Scenario: Initialize with StateFixture
- **WHEN** SimulationRunner is initialized with a StateFixture and TraversalPlan
- **THEN** the runner SHALL create a StatefulMockVisionService from the fixture
- **AND** the runner SHALL create a StatefulMockActionExecutor linked to the vision service
- **AND** both services SHALL be passed to GraphTraversalEngine

### Requirement: Enhanced Trace Collection
The system SHALL collect enhanced trace data during simulation.

#### Scenario: Collect page transitions
- **WHEN** simulation runs with stateful services
- **THEN** the result SHALL contain page transition spans
- **AND** navigation_history SHALL be available in the result

#### Scenario: Collect dynamic node lifecycle
- **WHEN** simulation generates dynamic nodes
- **THEN** the result SHALL contain dynamic node lifecycle spans
- **AND** lifecycle events SHALL be ordered by timestamp

### Requirement: Action History Collection
The system SHALL collect action execution history during simulation.

#### Scenario: Record action history
- **WHEN** actions are executed during simulation
- **THEN** each action SHALL be recorded with action_type, target, node_id, and success status
- **AND** action_history SHALL be available in the simulation result

### Requirement: Simulation Result Structure
The system SHALL provide a comprehensive simulation result structure.

#### Scenario: Result contains all execution data
- **WHEN** simulation completes
- **THEN** SimulationResult SHALL contain:
  - trace_id: identifier for the trace
  - completion_reason: final state of the traversal
  - elapsed_seconds: execution time
  - trace_nodes: complete trace data
  - final_state: final traversal state object
  - navigation_history: sequence of page IDs
  - action_history: sequence of executed actions

### Requirement: Backward Compatibility
The system SHALL maintain backward compatibility with existing simulation tests.

#### Scenario: Existing tests unchanged
- **WHEN** existing tests use the old SimulationRunner interface
- **THEN** those tests SHALL continue to pass
- **AND** existing behavior SHALL be preserved

### Requirement: Mock Service Selection
The system SHALL allow selection between stateless and stateful mock services.

#### Scenario: Use stateless service
- **WHEN** SimulationRunner is initialized without a StateFixture
- **THEN** the runner SHALL use the original MockVisionService
- **AND** state management features SHALL not be available

#### Scenario: Use stateful service
- **WHEN** SimulationRunner is initialized with a StateFixture
- **THEN** the runner SHALL use StatefulMockVisionService
- **AND** page transitions SHALL be tracked

### Requirement: Trace Recorder Integration
The system SHALL integrate with trace recorder for enhanced span recording.

#### Scenario: Trace recorder receives page transitions
- **WHEN** page transitions occur during simulation
- **THEN** the trace recorder SHALL record PageTransitionSpan nodes
- **AND** spans SHALL include from_page, to_page, and trigger_element

#### Scenario: Trace recorder receives lifecycle events
- **WHEN** dynamic nodes are created and executed
- **THEN** the trace recorder SHALL record DynamicNodeLifecycleSpan nodes
- **AND** spans SHALL include event type and node_id

### Requirement: State Reset Between Runs
The system SHALL support resetting mock service state between simulation runs.

#### Scenario: Reset state for new run
- **WHEN** a simulation runner executes multiple runs
- **THEN** each run SHALL start from the initial page state
- **AND** navigation_history SHALL be cleared between runs
