# Spec: Stateful Mock Services

## ADDED Requirements

### Requirement: Stateful Vision Service
The system SHALL provide a StatefulMockVisionService that manages page state and simulates page transitions.

#### Scenario: Initial page state
- **WHEN** StatefulMockVisionService is initialized with a fixture
- **THEN** current_page_id SHALL be the fixture's initial page
- **AND** navigation_history SHALL be empty

### Requirement: Page Analysis Generation
The system SHALL generate PageAnalysis objects compatible with existing GraphEngine code.

#### Scenario: Correct PageAnalysis structure
- **WHEN** analyze_screenshot is called
- **THEN** the result SHALL be a PageAnalysis object
- **AND** the result SHALL have an 'items' attribute (not 'menu_items')
- **AND** items SHALL be a list of MenuItem objects

### Requirement: MenuItem Field Mapping
The system SHALL correctly map fixture fields to MenuItem model fields.

#### Scenario: Text to name mapping
- **WHEN** an element defines text="Button1"
- **THEN** the MenuItem.name SHALL be "Button1" (not MenuItem.text)

#### Scenario: Type enum conversion
- **WHEN** an element defines type="button"
- **THEN** the MenuItem.type SHALL be MenuItemType.BUTTON enum value
- **AND** MenuItem.type.value SHALL return "button"

#### Scenario: Coordinate object creation
- **WHEN** an element defines coordinate={x: 0.3, y: 0.5}
- **THEN** the MenuItem.coordinate SHALL be a Coordinate object with x=0.3, y=0.5

### Requirement: Expected Action Inference
The system SHALL infer appropriate ExpectedAction values based on element properties.

#### Scenario: Navigation action inferred
- **WHEN** an element defines action_target="detail"
- **THEN** the MenuItem.expected_action SHALL be ExpectedAction.NAVIGATE
- **AND** MenuItem.expects_page_change SHALL be true

#### Scenario: Toggle action inferred
- **WHEN** an element defines type="switch"
- **THEN** the MenuItem.expected_action SHALL be ExpectedAction.TOGGLE
- **AND** MenuItem.expects_state_change SHALL be true

### Requirement: Action Simulation
The system SHALL simulate element actions and update page state accordingly.

#### Scenario: Successful button click
- **WHEN** simulate_action is called with element_id="btn1" and action="click"
- **AND** a valid transition exists from current_page via btn1
- **THEN** current_page_id SHALL change to the transition's to_page
- **AND** the previous page SHALL be added to navigation_history
- **AND** simulate_action SHALL return true

#### Scenario: Invalid action on element
- **WHEN** simulate_action is called with action="swipe" but transition defines action="click"
- **THEN** simulate_action SHALL return false
- **AND** current_page_id SHALL not change

#### Scenario: Action on wrong page
- **WHEN** current_page is "detail" and simulate_action is called for a transition from "home"
- **THEN** simulate_action SHALL return false
- **AND** current_page_id SHALL remain "detail"

### Requirement: Navigation Back
The system SHALL support navigating back to the previous page.

#### Scenario: Navigate back succeeds
- **WHEN** user has navigated from "home" to "detail" to "settings"
- **AND** navigate_back is called
- **THEN** current_page_id SHALL change to "detail"
- **AND** "settings" SHALL be removed from navigation_history

#### Scenario: Navigate back at root
- **WHEN** navigation_history is empty
- **AND** navigate_back is called
- **THEN** navigate_back SHALL return false
- **AND** current_page_id SHALL not change

### Requirement: State Reset
The system SHALL support resetting to the initial page state.

#### Scenario: Reset to initial
- **WHEN** user has navigated to "settings"
- **AND** reset_to_initial is called
- **THEN** current_page_id SHALL be the fixture's initial page
- **AND** navigation_history SHALL be empty

### Requirement: Action Executor Coordination
The system SHALL provide a StatefulMockActionExecutor that coordinates with StatefulMockVisionService.

#### Scenario: Executor updates vision service
- **WHEN** ActionExecutor.execute is called with a click operation on btn1
- **THEN** the executor SHALL call vision.simulate_action("btn1", "click")
- **AND** the vision service's current_page_id SHALL update
- **AND** ExecutionResult.success SHALL reflect the action result

#### Scenario: Action history tracking
- **WHEN** actions are executed through the executor
- **THEN** get_history() SHALL return a list of all executed actions with metadata
- **AND** each entry SHALL contain action_type, target, node_id, and success status

### Requirement: Compatibility with DynamicMatcher
The system SHALL ensure MenuItem objects are compatible with DynamicMatcher input format.

#### Scenario: DynamicMatcher format compatibility
- **WHEN** PageAnalysis.items are converted to the format expected by DynamicMatcher
- **THEN** each item SHALL have: type (string), text (from MenuItem.name), index, coordinate (dict), expected_action (string)
- **AND** DynamicMatcher SHALL successfully process these items

### Requirement: Backward Compatibility
The system SHALL maintain backward compatibility with existing MockVisionService.

#### Scenario: Existing tests unaffected
- **WHEN** existing tests use MockVisionService
- **THEN** those tests SHALL continue to pass without modification
- **AND** MockVisionService behavior SHALL remain unchanged
