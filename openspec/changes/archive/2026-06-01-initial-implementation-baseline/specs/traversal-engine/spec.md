# Traversal Engine Capability Specification

## ADDED Requirements

### Requirement: Engine Initialization
The system SHALL initialize traversal engine with required dependencies.

#### Scenario: Standard Initialization
- **WHEN** TraversalEngine is created
- **THEN** it accepts: adb_client, vision_service, state, config, event_callback

#### Scenario: Optional Event Callback
- **WHEN** event_callback is not provided
- **THEN** engine operates without event notifications

### Requirement: App Navigation
The system SHALL navigate from home screen to target app.

#### Scenario: Successful Navigation
- **WHEN** navigate_to_app() is called with existing app name
- **THEN** system captures screenshot
- **AND** AI finds app icon
- **AND** system taps icon
- **AND** waits for transition
- **AND** returns True

#### Scenario: App Not Found
- **WHEN** navigate_to_app() is called with non-existent app
- **THEN** AI returns None
- **AND** system emits navigate_failed event
- **AND** returns False

### Requirement: Structure Initialization
The system SHALL analyze and cache initial page structure.

#### Scenario: First Time Initialization
- **WHEN** initialize_structure() is called on fresh app entry
- **THEN** system captures and analyzes screenshot
- **AND** caches all level1 menus
- **AND** caches level2 menus for current level1
- **AND** caches items for current location
- **AND** builds initial content tree skeleton

#### Scenario: Navigation to Start Point
- **WHEN** current position is not at first menu/tab
- **THEN** system taps first level1 menu
- **AND** taps first level2 tab if exists
- **AND** updates current_path

#### Scenario: State Update
- **WHEN** initialization completes
- **THEN** current_phase is set to "traversing"
- **AND** current_path reflects start position

### Requirement: Item Selection
The system SHALL select next unvisited item for traversal.

#### Scenario: Normal Selection
- **WHEN** _select_next_item() finds unvisited items
- **THEN** returns first unvisited item from current location

#### Scenario: All Visited
- **WHEN** all items in current location are visited
- **THEN** returns None

#### Scenario: Empty Location
- **WHEN** current location has no items
- **THEN** returns None

### Requirement: Item Clicking
The system SHALL click items and determine result type.

#### Scenario: Normal Response
- **WHEN** clicked item responds with content change
- **THEN** returns ClickResult.NORMAL

#### Scenario: Popup Detection
- **WHEN** clicked item triggers popup
- **THEN** returns ClickResult.POPUP
- **AND** popup is handled

#### Scenario: Page Jump
- **WHEN** clicked item causes page navigation
- **THEN** returns ClickResult.PAGE_JUMP
- **AND** jump is handled

#### Scenario: No Change
- **WHEN** clicked item produces no visible change
- **THEN** returns ClickResult.NO_CHANGE

### Requirement: Level 2 Tab Switching
The system SHALL switch to next level2 tab when current is exhausted.

#### Scenario: Next Tab Exists
- **WHEN** current level2 tab has no more unvisited items
- **AND** next level2 tab exists
- **THEN** system taps next tab
- **AND** updates current_path
- **AND** returns True

#### Scenario: No More Tabs
- **WHEN** current level2 tab is last
- **THEN** returns False

### Requirement: Level 1 Menu Switching
The system SHALL switch to next level1 menu when current is exhausted.

#### Scenario: Next Menu Exists
- **WHEN** current level1 menu has no more unvisited tabs
- **AND** next level1 menu exists
- **THEN** system taps next menu
- **AND** analyzes new page for level2 tabs
- **AND** caches new level2 structure
- **AND** updates current_path
- **AND** returns True

#### Scenario: No More Menus
- **WHEN** current level1 menu is last
- **THEN** returns False

#### Scenario: New Menu New Tabs
- **WHEN** switching to new level1 menu
- **THEN** system analyzes and caches its level2 tabs
- **AND** level2 structure may differ from previous menu

### Requirement: Main Traversal Loop
The system SHALL execute traversal step by step.

#### Scenario: Normal Step
- **WHEN** run_step() is called with unvisited items available
- **THEN** selects next item
- **AND** clicks item
- **AND** marks as visited
- **AND** returns True to continue

#### Scenario: Location Exhausted
- **WHEN** current location has no more unvisited items
- **THEN** attempts to switch tab/menu
- **AND** returns True if switch successful
- **AND** returns False if all done

#### Scenario: Max Steps Reached
- **WHEN** step count exceeds config.max_steps
- **THEN** emits max_steps_reached event
- **AND** returns False

#### Scenario: Too Many Errors
- **WHEN** consecutive_errors reaches 3
- **THEN** emits too_many_errors event
- **AND** returns False

### Requirement: Complete Traversal Run
The system SHALL run full traversal until completion.

#### Scenario: Successful Completion
- **WHEN** run() is called
- **THEN** executes steps until all items visited
- **AND** returns summary with: total_steps, elapsed_time, visited_count, tree

#### Scenario: Early Termination
- **WHEN** max_steps or error limit reached
- **THEN** stops traversal
- **AND** returns summary with partial results

### Requirement: Wait Time Handling
The system SHALL wait after UI actions.

#### Scenario: Default Wait
- **WHEN** _wait() is called
- **THEN** system sleeps for config.wait_time seconds

#### Scenario: Tap and Wait
- **WHEN** _tap_and_wait() is called
- **THEN** system taps coordinate
- **AND** waits configured time

### Requirement: Screenshot and Analysis
The system SHALL capture screenshot and analyze with AI.

#### Scenario: Standard Analysis
- **WHEN** _capture_and_analyze() is called
- **THEN** captures screenshot via ADB
- **AND** analyzes via VisionService
- **AND** returns PageAnalysis
- **AND** emits page_analyzed event

### Requirement: Tree Building from Analysis
The system shall build content tree from AI analysis.

#### Scenario: Level 1 Node
- **WHEN** analysis contains current_path
- **THEN** creates level1 node from current_path[0]
- **AND** node level is 1

#### Scenario: Level 2 Node
- **WHEN** analysis contains second path element
- **THEN** creates level2 node from current_path[1]
- **AND** node level is 2
- **AND** parent is level1 node

#### Scenario: Item Nodes
- **WHEN** analysis contains items
- **THEN** creates node for each item
- **AND** node level is 3
- **AND** parent is level2 node

### Requirement: Event Emission
The system SHALL emit events for key traversal actions.

#### Scenario: Navigation Events
- **WHEN** navigate_to_app() is called
- **THEN** emits navigate_start and navigate_success/navigate_failed

#### Scenario: Initialization Events
- **WHEN** initialize_structure() is called
- **THEN** emits initialize_start and initialize_complete

#### Scenario: Step Events
- **WHEN** each step executes
- **THEN** emits step_start, click_start, location_exhausted

#### Scenario: Completion Events
- **WHEN** traversal completes
- **THEN** emits traversal_complete and traversal_finished

### Requirement: Configuration
The system SHALL accept traversal configuration.

#### Scenario: Default Configuration
- **WHEN** no config provided
- **THEN** uses defaults: max_steps=200, wait_time=0.5, max_retries=2

#### Scenario: Custom Configuration
- **WHEN** custom TraversalConfig is provided
- **THEN** uses provided values

### Requirement: Visited Marking
The system SHALL mark elements as visited after processing.

#### Scenario: Mark Visited
- **WHEN** item is successfully processed
- **THEN** creates VisitFingerprint with current_path and item_name
- **AND** adds to visited set

#### Scenario: Visited Check
- **WHEN** checking if element is visited
- **THEN** checks if fingerprint exists in visited set
