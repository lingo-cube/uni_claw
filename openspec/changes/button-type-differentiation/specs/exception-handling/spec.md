# Exception Handling Capability Specification - Delta

## MODIFIED Requirements

### Requirement: No Feedback Handling
The system SHALL handle items that produce no response, considering button type.

#### Scenario: No Feedback Detected
- **WHEN** ClickResult.NO_CHANGE is returned
- **THEN** system checks item.expected_action before retry strategy

#### Scenario: Toggle-type No Feedback
- **WHEN** toggle-type item has no feedback
- **THEN** system marks as state unchanged
- **AND** does NOT attempt child element retry

#### Scenario: Navigate-type No Feedback
- **WHEN** navigate-type item has no feedback
- **THEN** system treats as navigation failure
- **AND** may retry navigation click

#### Scenario: Action-type No Feedback
- **WHEN** action-type item has no feedback
- **THEN** system attempts child element retry
- **AND** falls back to no_feedback marking if children also fail

#### Scenario: Child Element Retry
- **WHEN** parent element has no feedback and is action-type
- **THEN** system searches for child elements with matching parent field
- **AND** attempts to click each child
- **AND** checks if child click produces response

#### Scenario: No Feedback Final
- **WHEN** all retry attempts produce no response
- **THEN** item is marked with node_type "no_feedback"
- **AND** is added to content tree

### Requirement: Before/After Comparison
The system SHALL compare screenshots considering expected button behavior.

#### Scenario: Navigate-type Path Comparison
- **WHEN** comparing navigate-type click results
- **THEN** current_path change is expected and indicates success
- **AND** lack of path change indicates failure

#### Scenario: Toggle-type Path Comparison
- **WHEN** comparing toggle-type click results
- **THEN** current_path should NOT change
- **AND** unexpected path change indicates side effect

#### Scenario: Path Change Detection
- **WHEN** comparing before_analysis.current_path and after_analysis.current_path
- **THEN** interpretation depends on item.expected_action

#### Scenario: Items Count Change
- **WHEN** comparing before_analysis.items and after_analysis.items
- **THEN** different count may indicate state change for toggle-type
- **AND** different count may indicate content refresh for action-type

### Requirement: Popup Detection
The system shall detect popups considering button type context.

#### Scenario: Expected Popup from Action
- **WHEN** action-type button triggers popup
- **THEN** this is expected behavior
- **AND** popup is handled normally

#### Scenario: Unexpected Popup from Navigate
- **WHEN** navigate-type button triggers popup
- **THEN** this may indicate navigation blocker
- **AND** system handles popup but notes navigation interrupted

#### Scenario: Popup Detection
- **WHEN** after_analysis.is_popup is true
- **THEN** popup is detected regardless of button type
- **AND** button type is recorded in event data

## ADDED Requirements

### Requirement: Type-based Error Recovery
The system SHALL adapt error recovery based on button type.

#### Scenario: Navigate Failure Recovery
- **WHEN** navigate-type click fails (no path change)
- **THEN** system may retry navigation click
- **AND** uses same coordinates for retry

#### Scenario: Toggle Failure Handling
- **WHEN** toggle-type click shows no state change
- **THEN** system marks as failed state change
- **AND** does NOT retry (toggle state is binary)

#### Scenario: Action Failure Recovery
- **WHEN** action-type click fails
- **THEN** system attempts child element retry
- **AND** falls back to standard no_feedback handling

### Requirement: Expected Behavior Violation
The system SHALL detect when actual behavior differs from expected.

#### Scenario: Navigate Without Path Change
- **WHEN** navigate-type button does not change current_path
- **THEN** this is marked as expected behavior violation
- **AND** event includes violation information

#### Scenario: Toggle With Path Change
- **WHEN** toggle-type button changes current_path
- **THEN** this is marked as unexpected side effect
- **AND** system handles as page jump

#### Scenario: Action With No Response
- **WHEN** action-type button produces no response
- **THEN** this follows standard no_feedback handling
