# Button Type Classification Capability Specification

## ADDED Requirements

### Requirement: Button Type Enumeration
The system SHALL provide extended button type enumeration covering all UI interaction patterns.

#### Scenario: Navigation Types
- **WHEN** MenuItemType enum is defined
- **THEN** it includes: MENU_ITEM, TAB, BACK_BUTTON for navigation elements

#### Scenario: Action Types
- **WHEN** MenuItemType enum is defined
- **THEN** it includes: SWITCH, TOGGLE, BUTTON for action elements

#### Scenario: Other Types
- **WHEN** MenuItemType enum is defined
- **THEN** it includes: ICON, LINK, TEXT, READONLY for other elements

#### Scenario: Backward Compatibility
- **WHEN** existing code uses ITEM type
- **THEN** it remains valid and is treated as MENU_ITEM

### Requirement: Expected Action Classification
The system SHALL classify expected button behavior into action categories.

#### Scenario: Navigate Action
- **WHEN** button is expected to change page or menu
- **THEN** expected_action is NAVIGATE
- **AND** expects_page_change is true

#### Scenario: Toggle Action
- **WHEN** button is expected to switch state without page change
- **THEN** expected_action is TOGGLE
- **AND** expects_state_change is true
- **AND** expects_page_change is false

#### Scenario: Generic Action
- **WHEN** button behavior is uncertain or complex
- **THEN** expected_action is ACTION
- **AND** expects_page_change may be true or false

#### Scenario: No Expected Action
- **WHEN** element is read-only or non-interactive
- **THEN** expected_action is NONE
- **AND** expects_page_change is false
- **AND** expects_state_change is false

### Requirement: MenuItem Data Model Extension
The system SHALL extend MenuItem model with behavior prediction fields.

#### Scenario: New Fields Present
- **WHEN** MenuItem is created from new AI analysis
- **THEN** it contains: expected_action, expects_page_change, expects_state_change

#### Scenario: Default Values
- **WHEN** MenuItem is loaded from old state file
- **THEN** new fields default to: expected_action=ACTION, expects_page_change=false, expects_state_change=false

#### Scenario: Field Validation
- **WHEN** MenuItem is validated
- **THEN** expected_action must be valid ExpectedAction enum value
- **AND** expects_page_change must be boolean
- **AND** expects_state_change must be boolean

### Requirement: Type-specific Wait Times
The system SHALL use different wait times based on button type.

#### Scenario: Navigate Wait Time
- **WHEN** button expected_action is NAVIGATE
- **THEN** wait time is at least 1.0 seconds

#### Scenario: Toggle Wait Time
- **WHEN** button expected_action is TOGGLE
- **THEN** wait time is at most 0.3 seconds

#### Scenario: Action Wait Time
- **WHEN** button expected_action is ACTION
- **THEN** wait time uses configured default (0.5 seconds)

#### Scenario: None Wait Time
- **WHEN** button expected_action is NONE
- **THEN** wait time is minimal (0.1 seconds for verification)

#### Scenario: Fallback Wait Time
- **WHEN** button expected_action is not recognized
- **THEN** system uses configured default wait time

### Requirement: Type-specific Verification
The system SHALL verify click results based on expected action.

#### Scenario: Navigate Verification
- **WHEN** button expected_action is NAVIGATE and is clicked
- **THEN** system verifies current_path changed
- **AND** returns PAGE_JUMP if path changed
- **AND** returns NO_CHANGE if path unchanged

#### Scenario: Toggle Verification
- **WHEN** button expected_action is TOGGLE and is clicked
- **THEN** system checks for state change
- **AND** returns NORMAL if state changed
- **AND** returns NO_CHANGE if state unchanged
- **AND** does NOT expect page jump

#### Scenario: Action Verification
- **WHEN** button expected_action is ACTION and is clicked
- **THEN** system checks for popup or page jump
- **AND** returns appropriate ClickResult

#### Scenario: None Verification
- **WHEN** button expected_action is NONE and is clicked
- **THEN** system quickly verifies no response
- **AND** may skip processing for read-only elements

### Requirement: AI Type Instruction
The system SHALL instruct AI to classify button types and behaviors.

#### Scenario: Type Classification Prompt
- **WHEN** PROMPT_STRUCTURE is sent to AI
- **THEN** it includes instructions to classify each element's type
- **AND** provides examples for each type

#### Scenario: Action Prediction Prompt
- **WHEN** PROMPT_STRUCTURE is sent to AI
- **THEN** it includes instructions to predict expected_action
- **AND** requests expects_page_change and expects_state_change flags

#### Scenario: Response Format
- **WHEN** AI returns analysis
- **THEN** each item includes: type, expected_action, expects_page_change, expects_state_change

### Requirement: Backward Compatibility
The system SHALL maintain compatibility with existing state files.

#### Scenario: Old State File Load
- **WHEN** state file from V1 is loaded
- **THEN** all items load successfully with default new field values

#### Scenario: New State File Save
- **WHEN** state is saved after processing
- **THEN** new fields are included in saved state

#### Scenario: Mixed Processing
- **WHEN** processing mix of old and new items
- **THEN** system handles both transparently
