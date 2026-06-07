# Spec: Mock Validation Tests

## ADDED Requirements

### Requirement: M series tests SHALL verify MockVisionService element field mapping
The system SHALL provide tests that verify MockVisionService correctly maps JSON fields to MenuItem properties.

#### Scenario: Text field maps to MenuItem name
- **WHEN** M1 test MockVisionService analyzes page with element {"text": "Settings"}
- **THEN** resulting MenuItem.name equals "Settings"

#### Scenario: Coordinate field maps to Coordinate
- **WHEN** M2 test MockVisionService analyzes page with element {"coordinate": {"x": 0.5, "y": 0.3}}
- **THEN** resulting MenuItem.coordinate.x equals 0.5 and y equals 0.3

### Requirement: M series tests SHALL verify MockVisionService path context switching
The system SHALL provide tests that verify set_path_context() changes returned page.

#### Scenario: Path context returns correct page
- **WHEN** M3 test calls set_path_context(["Settings", "Display"]) then analyze_screenshot()
- **THEN** returned PageAnalysis.current_path equals ["Settings", "Display"]

### Requirement: M series tests SHALL verify MockVisionService non-existent path handling
The system SHALL provide tests that verify non-existent path returns empty page.

#### Scenario: Non-existent path returns empty analysis
- **WHEN** M4 test calls set_path_context(["NonExistent"]) then analyze_screenshot()
- **THEN** returned PageAnalysis has items=[] and is_valid=false

### Requirement: M series tests SHALL verify MockActionExecutor operation recording
The system SHALL provide tests that verify all operations are recorded in history.

#### Scenario: Click operation recorded
- **WHEN** M5 test MockActionExecutor executes {"action": "click"}
- **THEN** get_history() contains entry with action_type="click"

#### Scenario: Back operation recorded
- **WHEN** M5 test MockActionExecutor executes {"action": "back"}
- **THEN** get_history() contains entry with action_type="back"

#### Scenario: Swipe operation recorded
- **WHEN** M5 test MockActionExecutor executes {"action": "swipe"}
- **THEN** get_history() contains entry with action_type="swipe"
