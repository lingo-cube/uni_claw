# Traversal Engine Capability Specification - Delta

## MODIFIED Requirements

### Requirement: Item Clicking
The system SHALL click items with type-specific wait times and verify results based on expected action.

#### Scenario: Type-specific Wait Time
- **WHEN** item is clicked
- **THEN** wait time is determined by item.expected_action
- **AND** NAVIGATE actions wait at least 1.0 seconds
- **AND** TOGGLE actions wait at most 0.3 seconds
- **AND** ACTION actions use default configured time

#### Scenario: Navigate-type Verification
- **WHEN** clicked item has expected_action=NAVIGATE
- **THEN** system verifies current_path changed
- **AND** returns PAGE_JUMP if path changed
- **AND** returns NO_CHANGE if path unchanged and no popup

#### Scenario: Toggle-type Verification
- **WHEN** clicked item has expected_action=TOGGLE
- **THEN** system verifies state change without page change
- **AND** returns NORMAL if state changed
- **AND** returns NO_CHANGE if no state change
- **AND** treats unexpected page jump as PAGE_JUMP

#### Scenario: Action-type Verification
- **WHEN** clicked item has expected_action=ACTION
- **THEN** system verifies popup or page jump occurred
- **AND** returns appropriate ClickResult based on what occurred

#### Scenario: Normal Response
- **WHEN** clicked item responds with expected change
- **THEN** returns ClickResult.NORMAL

#### Scenario: Popup Detection
- **WHEN** clicked item triggers popup
- **THEN** returns ClickResult.POPUP
- **AND** popup is handled

#### Scenario: No Change
- **WHEN** clicked item produces no visible change
- **THEN** returns ClickResult.NO_CHANGE

### Requirement: Wait Time Handling
The system SHALL use configurable wait times based on button type.

#### Scenario: Default Wait
- **WHEN** _wait() is called without item context
- **THEN** system sleeps for config.wait_time seconds

#### Scenario: Type-based Wait
- **WHEN** _tap_and_wait() is called with item
- **THEN** system determines wait time from item.expected_action
- **AND** waits for calculated duration
- **AND** falls back to config.wait_time if expected_action unknown

### Requirement: Read-only Element Handling
The system SHALL handle read-only elements appropriately.

#### Scenario: Read-only Detection
- **WHEN** item has expected_action=NONE or type=READONLY
- **THEN** system may skip clicking
- **OR** click with minimal wait time

#### Scenario: Read-only Verification
- **WHEN** read-only element is clicked
- **THEN** system expects NO_CHANGE result
- **AND** marks as no_feedback if clicked

## ADDED Requirements

### Requirement: Wait Time Calculation
The system SHALL calculate wait times based on button expected action.

#### Scenario: Navigate Wait Calculation
- **WHEN** item.expected_action is NAVIGATE
- **THEN** wait time is max(config.wait_time, 1.0)

#### Scenario: Toggle Wait Calculation
- **WHEN** item.expected_action is TOGGLE
- **THEN** wait time is min(config.wait_time, 0.3)

#### Scenario: None Wait Calculation
- **WHEN** item.expected_action is NONE
- **THEN** wait time is 0.1 seconds (minimal verification)

#### Scenario: Unknown Action Fallback
- **WHEN** item.expected_action is not recognized
- **THEN** system uses config.wait_time as default

### Requirement: Action-based Verification
The system SHALL verify click results using action-specific logic.

#### Scenario: Navigate Verification Method
- **WHEN** verifying navigate-type click
- **THEN** system compares before.current_path and after.current_path
- **AND** checks for popup occurrence
- **AND** does NOT expect state change within same page

#### Scenario: Toggle Verification Method
- **WHEN** verifying toggle-type click
- **THEN** system checks for item state changes
- **AND** expects current_path to remain same
- **AND** checks for unexpected popup or jump

#### Scenario: Generic Verification Method
- **WHEN** verifying action-type or unknown click
- **THEN** system uses generic verification logic
- **AND** checks for popup, jump, or content change

### Requirement: Button Type Utilization
The system SHALL utilize button type information throughout traversal.

#### Scenario: Selection Consideration
- **WHEN** selecting next item
- **THEN** system may prioritize certain button types
- **AND** may defer read-only elements

#### Scenario: Tree Recording
- **WHEN** recording item in content tree
- **THEN** node_type reflects button type
- **AND** metadata includes expected_action

#### Scenario: Event Data
- **WHEN** emitting click_start event
- **THEN** event data includes button type and expected action
