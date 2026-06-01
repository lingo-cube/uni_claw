# Exception Handling Capability Specification

## ADDED Requirements

### Requirement: Click Result Classification
The system SHALL classify all possible click outcomes.

#### Scenario: No Change Result
- **WHEN** clicked item produces no visible change
- **THEN** returns ClickResult.NO_CHANGE

#### Scenario: Popup Result
- **WHEN** clicked item triggers popup dialog
- **THEN** returns ClickResult.POPUP

#### Scenario: Page Jump Result
- **WHEN** clicked item causes navigation to different page
- **THEN** returns ClickResult.PAGE_JUMP

#### Scenario: Normal Result
- **WHEN** clicked item responds with expected change
- **THEN** returns ClickResult.NORMAL

#### Scenario: No Feedback Result
- **WHEN** clicked item and child elements also produce no response
- **THEN** returns ClickResult.NO_FEEDBACK

#### Scenario: Error Result
- **WHEN** click operation fails
- **THEN** returns ClickResult.ERROR

### Requirement: Popup Handling
The system SHALL detect and handle popup dialogs.

#### Scenario: Popup Detected
- **WHEN** AI analysis detects is_popup=true
- **THEN** system records popup in content tree
- **AND** taps close button if available
- **OR** presses back button if no close button
- **AND** waits for popup to dismiss

#### Scenario: Popup Recording
- **WHEN** popup is detected
- **THEN** popup is added as child node in content tree
- **AND** node_type is "popup"

#### Scenario: Popup Title Recording
- **WHEN** popup contains title
- **THEN** title is recorded in popup node

#### Scenario: Popup Close Failure
- **WHEN** popup cannot be closed after retries
- **THEN** system handles as error condition

### Requirement: Page Jump Handling
The system shall detect and handle page navigation jumps.

#### Scenario: Jump Detected
- **WHEN** current_path changes after click
- **THEN** system records jump in content tree
- **AND** navigates back to original page

#### Scenario: Jump Recording
- **WHEN** page jump is detected
- **THEN** jump target is added as child node
- **AND** node_type is "jump"

#### Scenario: Back Navigation
- **WHEN** returning from jump
- **THEN** system taps back button if available
- **OR** presses system back key if no back button

### Requirement: No Feedback Handling
The system SHALL handle items that produce no response.

#### Scenario: No Feedback Detected
- **WHEN** ClickResult.NO_CHANGE is returned
- **THEN** system attempts to click child elements

#### Scenario: Child Element Retry
- **WHEN** parent element has no feedback
- **THEN** system searches for child elements with matching parent field
- **AND** attempts to click each child
- **AND** checks if child click produces response

#### Scenario: No Feedback Final
- **WHEN** all child elements also produce no response
- **THEN** item is marked with node_type "no_feedback"
- **AND** is added to content tree

#### Scenario: No Feedback Recording
- **WHEN** item produces no feedback
- **THEN** item is added to content tree
- **AND** node_type is "no_feedback"

### Requirement: Error Counting
The system SHALL track consecutive errors.

#### Scenario: Error Increment
- **WHEN** ClickResult.ERROR is returned
- **THEN** consecutive_errors is incremented

#### Scenario: Error Reset
- **WHEN** non-error result is returned
- **THEN** consecutive_errors is reset to 0

#### Scenario: Error Threshold
- **WHEN** consecutive_errors reaches 3
- **THEN** traversal is terminated
- **AND** too_many_errors event is emitted

### Requirement: Popup Info Structure
The system shall maintain popup information structure.

#### Scenario: Popup Info Contents
- **WHEN** popup is detected
- **THEN** popup_info contains: title, content, close_button coordinates

#### Scenario: No Popup
- **WHEN** no popup is present
- **THEN** popup_info is None

### Requirement: Before/After Comparison
The system SHALL compare screenshots to detect changes.

#### Scenario: Path Change Detection
- **WHEN** comparing before_analysis.current_path and after_analysis.current_path
- **THEN** difference indicates page jump

#### Scenario: Items Count Change
- **WHEN** comparing before_analysis.items and after_analysis.items
- **THEN** different count indicates content change

#### Scenario: Popup Detection
- **WHEN** after_analysis.is_popup is true
- **THEN** popup is detected regardless of other changes

### Requirement: Back Button Detection
The system SHALL detect back navigation buttons.

#### Scenario: Back Button Present
- **WHEN** page contains back button
- **THEN** back_button field contains coordinates

#### Scenario: No Back Button
- **WHEN** page has no back button
- **THEN** back_button is None
- **AND** system uses system back key

### Requirement: Current Tab Node Finding
The system shall locate current tab node in content tree.

#### Scenario: Find by Path
- **WHEN** _find_current_tab_node_id() is called
- **THEN** searches for node matching current_path[1]
- **AND** node.level equals 2

#### Scenario: Fallback to Root
- **WHEN** matching node cannot be found
- **THEN** returns "0" as parent_id

### Requirement: Close Button Handling
The system SHALL handle popup close buttons.

#### Scenario: Close Button Available
- **WHEN** popup has close_button
- **THEN** system taps at close_button coordinates

#### Scenario: No Close Button
- **WHEN** popup has no close_button
- **THEN** system presses system back key
