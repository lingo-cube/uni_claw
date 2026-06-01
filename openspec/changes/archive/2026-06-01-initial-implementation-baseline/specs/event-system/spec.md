# Event System Capability Specification

## ADDED Requirements

### Requirement: Event Data Structure
The system SHALL provide structured event data.

#### Scenario: Event Structure
- **WHEN** TraversalEvent is created
- **THEN** it contains: event_type (str), step (int), data (dict)

#### Scenario: Event String Representation
- **WHEN** event is converted to string
- **THEN** returns "[event_type] Step step: data"

### Requirement: Navigation Events
The system SHALL emit events during app navigation.

#### Scenario: Navigation Start
- **WHEN** navigate_to_app() is called
- **THEN** emits navigate_start event with target name

#### Scenario: Navigation Success
- **WHEN** app entry is found and clicked
- **THEN** emits navigate_success event with target name

#### Scenario: Navigation Failure
- **WHEN** app entry is not found
- **THEN** emits navigate_failed event with target and reason

### Requirement: Initialization Events
The system SHALL emit events during structure initialization.

#### Scenario: Initialization Start
- **WHEN** initialize_structure() is called
- **THEN** emits initialize_start event

#### Scenario: Initialization Complete
- **WHEN** structure initialization completes
- **THEN** emits initialize_complete event
- **AND** data includes: level1_count, level2_count, items_count

### Requirement: Step Events
The system SHALL emit events during each traversal step.

#### Scenario: Step Start
- **WHEN** run_step() begins execution
- **THEN** emits step_start event with step number

#### Scenario: Click Start
- **WHEN** item click begins
- **THEN** emits click_start event with item name and type

#### Scenario: Location Exhausted
- **WHEN** current location has no more unvisited items
- **THEN** emits location_exhausted event with current_path

### Requirement: Analysis Events
The system SHALL emit events after page analysis.

#### Scenario: Page Analyzed
- **WHEN** _capture_and_analyze() completes
- **THEN** emits page_analyzed event
- **AND** data includes: current_path, items_count, is_popup

### Requirement: Exception Events
The system SHALL emit events for exception conditions.

#### Scenario: Popup Detected
- **WHEN** popup is detected
- **THEN** emits popup_detected event with popup_info

#### Scenario: Page Jump
- **WHEN** page jump is detected
- **THEN** emits page_jump event with new current_path

#### Scenario: No Feedback
- **WHEN** item produces no feedback
- **THEN** emits no_feedback event with item name

### Requirement: Completion Events
The system SHALL emit events when traversal completes.

#### Scenario: Traversal Start
- **WHEN** run() is called
- **THEN** emits traversal_start event with max_steps

#### Scenario: Traversal Complete
- **WHEN** all items are visited
- **THEN** emits traversal_complete event with total_steps

#### Scenario: Traversal Finished
- **WHEN** traversal run finishes (complete or terminated)
- **THEN** emits traversal_finished event with summary

### Requirement: Termination Events
The system SHALL emit events for termination conditions.

#### Scenario: Max Steps Reached
- **WHEN** step count exceeds max_steps
- **THEN** emits max_steps_reached event with step number

#### Scenario: Too Many Errors
- **WHEN** consecutive errors reach threshold
- **THEN** emits too_many_errors event with step number

### Requirement: Event Callback Mechanism
The system SHALL support callback-based event delivery.

#### Scenario: Callback Registration
- **WHEN** TraversalEngine is created with event_callback
- **THEN** all events are delivered to callback function

#### Scenario: No Callback
- **WHEN** TraversalEngine is created without event_callback
- **THEN** events are still generated but not delivered

#### Scenario: Callback Signature
- **WHEN** callback is invoked
- **THEN** receives single TraversalEvent argument

### Requirement: Event Data Content
The system SHALL include relevant data in event payloads.

#### Scenario: Navigation Event Data
- **WHEN** navigate_start is emitted
- **THEN** data contains: {"target": app_name}

#### Scenario: Click Event Data
- **WHEN** click_start is emitted
- **THEN** data contains: {"item": item_name, "type": item_type}

#### Scenario: Analysis Event Data
- **WHEN** page_analyzed is emitted
- **THEN** data contains: {"current_path": path, "items_count": count, "is_popup": bool}

#### Scenario: Summary Event Data
- **WHEN** traversal_finished is emitted
- **THEN** data contains: {"total_steps": int, "elapsed_time": float, "visited_count": int, "final_path": list, "tree": str}

### Requirement: Step Number Tracking
The system SHALL track step number in events.

#### Scenario: Step Increment
- **WHEN** run_step() is called
- **THEN** _step counter is incremented
- **AND** events reflect updated step number

#### Scenario: Step in Events
- **WHEN** any event is emitted during step
- **THEN** event.step contains current step number

### Requirement: Event Emission Method
The system shall provide internal event emission method.

#### Scenario: Event Emission
- **WHEN** _emit(event_type, data) is called
- **THEN** TraversalEvent is created with current step
- **AND** callback is invoked if registered
- **AND** no error if callback is None

#### Scenario: Event Type Consistency
- **WHEN** events are emitted
- **THEN** event_type strings are consistent and documented
