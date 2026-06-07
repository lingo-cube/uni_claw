# Spec: Problem Detection

## ADDED Requirements

### Requirement: Problem Detector Configuration
The system SHALL provide configurable problem detection with adjustable thresholds.

#### Scenario: Default configuration
- **WHEN** ProblemDetector is initialized without config
- **THEN** default thresholds SHALL be used (max_action_repeats=3, max_loop_depth=5)
- **AND** all detection features SHALL be enabled

#### Scenario: Custom configuration
- **WHEN** ProblemDetectorConfig is created with max_action_repeats=5
- **THEN** the detector SHALL use max_action_repeats=5 for detection

### Requirement: Sensitivity Level Configuration
The system SHALL support sensitivity levels that adjust detection thresholds.

#### Scenario: Low sensitivity
- **WHEN** loop_detection_sensitivity is set to "low"
- **THEN** effective max_action_repeats SHALL be doubled
- **AND** effective max_loop_depth SHALL be doubled

#### Scenario: High sensitivity
- **WHEN** loop_detection_sensitivity is set to "high"
- **THEN** effective max_action_repeats SHALL be halved (minimum 1)
- **AND** effective max_loop_depth SHALL be halved (minimum 2)

### Requirement: Infinite Loop Detection
The system SHALL detect infinite loop patterns in execution traces.

#### Scenario: Detect repeated action loop
- **WHEN** the same action is repeated 4 times on the same element
- **AND** max_action_repeats is 3
- **THEN** a Problem SHALL be detected with type=INFINITE_LOOP
- **AND** severity SHALL be "critical"

#### Scenario: Detect state sequence loop
- **WHEN** a state sequence "EXECUTING -> AUTO_ESCAPE -> EXECUTING" repeats
- **THEN** a Problem SHALL be detected with type=INFINITE_LOOP
- **AND** severity SHALL be "warning"
- **AND** evidence SHALL contain the loop pattern

### Requirement: Repeated Action Detection
The system SHALL detect abnormal repeated actions on the same node.

#### Scenario: Detect consecutive repeated action
- **WHEN** the same action type is executed consecutively 3 times on the same node
- **AND** max_action_repeats is 3
- **THEN** a Problem SHALL be detected with type=REPEATED_ACTION
- **AND** severity SHALL be "warning"

### Requirement: Unvisited Node Detection
The system SHALL detect nodes that were expected but not visited.

#### Scenario: Detect unvisited expected node
- **WHEN** ExpectedBehavior defines visited_nodes={root, btn1, btn2}
- **AND** actual trace only contains {root, btn1}
- **THEN** a Problem SHALL be detected with type=UNVISITED_NODE
- **AND** severity SHALL be "warning"
- **AND** location SHALL be "btn2"

### Requirement: State Machine Error Detection
The system SHALL detect state machine errors and invalid transitions.

#### Scenario: Detect final ERROR state
- **WHEN** simulation result has final_state.value="ERROR"
- **THEN** a Problem SHALL be detected with type=STATE_MACHINE_ERROR
- **AND** severity SHALL be "critical"

#### Scenario: Detect invalid state transition
- **WHEN** a state transition occurs from "COMPLETED" to "EXECUTING"
- **THEN** a Problem SHALL be detected with type=STATE_MACHINE_ERROR
- **AND** severity SHALL be "error"
- **AND** description SHALL specify the invalid transition

### Requirement: Page Mismatch Detection
The system SHALL detect potential page transition failures.

#### Scenario: Detect failed page transition
- **WHEN** a page transition has from_page equal to to_page
- **THEN** a Problem SHALL be detected with type=PAGE_MISMATCH
- **AND** severity SHALL be "warning"
- **AND** description SHALL indicate possible transition failure

### Requirement: Orphan Node Detection
The system SHALL detect dynamic nodes that were created but never executed.

#### Scenario: Detect orphaned dynamic node
- **WHEN** a dynamic node has a "created" lifecycle event but no "executed" event
- **THEN** a Problem SHALL be detected with type=ORPHAN_NODE
- **AND** severity SHALL be "warning"
- **AND** evidence SHALL contain the lifecycle events

### Requirement: Feature Toggles
The system SHALL support enabling/disabling specific detection features.

#### Scenario: Disable infinite loop detection
- **WHEN** enable_infinite_loop_detection is set to false
- **THEN** infinite loop problems SHALL NOT be detected
- **AND** other detection types SHALL continue to work

#### Scenario: Enable all features
- **WHEN** all enable_* flags are set to true
- **THEN** all detection types SHALL be active

### Requirement: Problem Evidence Collection
The system SHALL collect evidence data for each detected problem.

#### Scenario: Collect action evidence
- **WHEN** a repeated action problem is detected
- **THEN** Problem.evidence SHALL contain the action details and repeat count

#### Scenario: Collect loop pattern evidence
- **WHEN** an infinite loop is detected
- **THEN** Problem.evidence SHALL contain the repeating pattern

### Requirement: Valid State Transitions
The system SHALL define valid state machine transitions for validation.

#### Scenario: Valid IDLE transitions
- **WHEN** state transitions from "IDLE"
- **THEN** valid target states SHALL be ["BINDING", "EXECUTING"]

#### Scenario: Valid COMPLETED transitions
- **WHEN** state transitions from "COMPLETED"
- **THEN** valid target states SHALL be [] (no outgoing transitions)

### Requirement: Problem Report Structure
The system SHALL provide structured problem reports.

#### Scenario: Problem serialization
- **WHEN** a Problem is converted to dictionary
- **THEN** the result SHALL contain type, description, severity, location, and evidence
- **AND** type SHALL be the enum value string

### Requirement: Repeating Pattern Detection
The system SHALL detect repeating patterns in state sequences.

#### Scenario: Detect ABAB pattern
- **WHEN** state sequence is ["A", "B", "A", "B", "A", "B"]
- **THEN** a repeating pattern "A -> B" SHALL be detected

#### Scenario: No pattern in unique sequence
- **WHEN** state sequence has no repeats
- **THEN** no repeating pattern SHALL be detected
