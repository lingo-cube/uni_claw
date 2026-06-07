# Spec: Smart Correction Tests

## ADDED Requirements

### Requirement: P series tests SHALL verify precondition satisfaction shortcut
The system SHALL provide tests that verify precondition satisfied nodes skip correction and directly enter EXECUTE state.

#### Scenario: Precondition satisfied bypasses correction
- **WHEN** P1 test has node with precondition that matches current page
- **THEN** state machine transitions directly to EXECUTE without vision call

### Requirement: P series tests SHALL verify NAVIGABLE correction
The system SHALL provide tests that verify NAVIGABLE relation correction clicks same-level menu.

#### Scenario: NAVIGABLE 1-round correction succeeds
- **WHEN** P2 test precondition fails with NAVIGABLE relation and menu contains target
- **THEN** state machine clicks target menu item and vision verifies success

#### Scenario: NAVIGABLE 3-round correction exhausts
- **WHEN** P3 test NAVIGABLE correction fails 3 times in a row
- **THEN** state machine transitions to ERROR_HANDLING after 3rd retry

### Requirement: P series tests SHALL verify DEEPER correction
The system SHALL provide tests that verify DEEPER relation correction uses back action.

#### Scenario: DEEPER correction succeeds
- **WHEN** P4 test precondition fails with DEEPER relation
- **THEN** state machine executes back and vision verifies path match

#### Scenario: DEEPER over-back returns UNKNOWN
- **WHEN** P5 test back too far and expected page is in path but not at end
- **THEN** classify_relation returns UNKNOWN and correction continues with back

### Requirement: P series tests SHALL verify UNKNOWN correction
The system SHALL provide tests that verify UNKNOWN relation correction uses back retry.

#### Scenario: UNKNOWN迷失 recovery exhausts retries
- **WHEN** P6 test has UNKNOWN relation and 3 back operations don't reach target
- **THEN** state machine transitions to ERROR_HANDLING after retries exhausted

### Requirement: P series tests SHALL verify vision failure tolerance
The system SHALL provide tests that verify precondition check handles vision failures gracefully.

#### Scenario: Vision failure during correction
- **WHEN** P7 test vision.analyze_screenshot() raises exception during correction
- **THEN** state machine continues to next retry without crashing

### Requirement: P series tests SHALL verify concurrent precondition handling
The system SHALL provide tests that verify multiple nodes with preconditions are all checked.

#### Scenario: Multiple preconditions all verified
- **WHEN** P8 test has 3 nodes in sequence each with precondition
- **THEN** each precondition is checked and corrected independently

### Requirement: P series tests SHALL verify precondition timeout handling
The system SHALL provide tests that verify precondition timeout is recorded after retries exhausted.

#### Scenario: Precondition timeout after 3 retries
- **WHEN** P9 test precondition check exhausts 3 retries
- **THEN** error metrics record "PreconditionTimeout" error_type

### Requirement: P series tests SHALL verify post-correction page unchanged handling
The system SHALL provide tests that verify correction when action executes but page doesn't change.

#### Scenario: Correction action succeeds but page unchanged
- **WHEN** P10 test executes correction action but vision shows same page
- **THEN** state machine continues to next retry with incremented count
