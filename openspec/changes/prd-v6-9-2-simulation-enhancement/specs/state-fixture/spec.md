# Spec: State Fixture

## ADDED Requirements

### Requirement: Fixture YAML Format
The system SHALL support a YAML-based fixture format for defining page states and transition rules.

#### Scenario: Valid fixture loads successfully
- **WHEN** a valid StateFixture YAML file is loaded
- **THEN** the system SHALL parse all pages, elements, and transitions without errors
- **AND** all page IDs, element IDs, and transition IDs SHALL be accessible

### Requirement: Page State Definition
The system SHALL support defining page states with elements, page name, and completion flag.

#### Scenario: Home page with buttons
- **WHEN** a fixture defines a home page with two button elements
- **THEN** the system SHALL create a PageState with id="home", page_name="HomeScreen", and two PageElement entries
- **AND** each element SHALL have id, type, text, coordinate, and optional action_target

### Requirement: Page Transition Rules
The system SHALL support defining page transition rules triggered by element actions.

#### Scenario: Button click transitions to detail page
- **WHEN** a transition is defined with trigger="btn1", from_page="home", to_page="detail", action="click"
- **THEN** the system SHALL create a PageTransition linking the btn1 element to the detail page
- **AND** the transition SHALL only be valid when action matches

### Requirement: Fixture Validation
The system SHALL validate fixture configuration for completeness and consistency.

#### Scenario: Detect missing target page
- **WHEN** a transition references a to_page that doesn't exist
- **THEN** the validator SHALL return an error: "Transition <id> to_page '<page>' not found"

#### Scenario: Detect missing trigger element
- **WHEN** a transition references a trigger element that doesn't exist in the from_page
- **THEN** the validator SHALL return an error: "Transition <id> trigger '<element>' not found in page '<page>'"

#### Scenario: Valid fixture passes validation
- **WHEN** all transitions reference existing pages and trigger elements
- **THEN** the validator SHALL return an empty error list

### Requirement: Navigation History Tracking
The system SHALL track navigation history up to a configurable depth.

#### Scenario: History records page transitions
- **WHEN** user navigates from home to detail to settings
- **THEN** navigation_history SHALL contain ["home", "detail"]
- **AND** current_page SHALL be "settings"

#### Scenario: History depth limit enforced
- **WHEN** history_depth is set to 10 and user navigates 15 times
- **THEN** navigation_history SHALL contain only the last 10 pages
- **AND** older entries SHALL be discarded

### Requirement: Element Coordinate Mapping
The system SHALL map fixture coordinate definitions to MenuItem coordinate objects.

#### Scenario: Coordinate conversion
- **WHEN** an element defines coordinate as {x: 0.5, y: 0.9}
- **THEN** the system SHALL create a Coordinate object with x=0.5, y=0.9

### Requirement: Element Type Mapping
The system SHALL map fixture type strings to MenuItemType enum values.

#### Scenario: Button type mapping
- **WHEN** an element defines type="button"
- **THEN** the system SHALL map to MenuItemType.BUTTON

#### Scenario: Switch type mapping
- **WHEN** an element defines type="switch"
- **THEN** the system SHALL map to MenuItemType.SWITCH

### Requirement: Initial Page Selection
The system SHALL select the first page as the initial page when not explicitly specified.

#### Scenario: Default initial page
- **WHEN** a fixture defines pages ["home", "detail", "settings"]
- **THEN** get_initial_page() SHALL return "home"

### Requirement: Page Completeness Flag
The system SHALL support an is_complete flag to indicate page completion status.

#### Scenario: Complete page marker
- **WHEN** a page defines is_complete: true
- **THEN** the PageAnalysis.is_end_of_list SHALL be true
- **AND** the state machine SHALL recognize this as a completion condition
