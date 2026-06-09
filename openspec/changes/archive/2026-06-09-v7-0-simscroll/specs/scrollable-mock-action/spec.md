## ADDED Requirements

### Requirement: Mock action executor supports scroll_down action
The ScrollableMockActionExecutor SHALL extend StatefulMockActionExecutor and implement scroll_down action that updates scroll progress in the vision service.

#### Scenario: Scroll down updates vision service
- **WHEN** scroll_down action is executed with step_percent 0.3
- **THEN** vision service simulate_scroll is called with delta 0.3

#### Scenario: Scroll down records action history
- **WHEN** scroll_down action is executed
- **THEN** ScrollAction is appended to scroll_actions list with action=DOWN, before_progress, after_progress, and timestamp

### Requirement: Mock action executor supports scroll_up action
The ScrollableMockActionExecutor SHALL implement scroll_up action that decreases scroll progress in the vision service.

#### Scenario: Scroll up decreases progress
- **WHEN** scroll_up action is executed with step_percent 0.3
- **THEN** vision service simulate_scroll is called with delta -0.3

#### Scenario: Scroll up records action history
- **WHEN** scroll_up action is executed
- **THEN** ScrollAction is appended to scroll_actions list with action=UP

### Requirement: Executor maintains scroll action history
The executor SHALL maintain chronological list of all scroll actions executed.

#### Scenario: Scroll history tracks all actions
- **WHEN** multiple scroll_down and scroll_up actions are executed
- **THEN** scroll_actions list contains all actions in execution order with metadata

### Requirement: Executor provides scroll count statistics
The executor SHALL provide get_scroll_count() method returning number of scroll actions executed for a given path.

#### Scenario: Scroll count reflects actions
- **WHEN** three scroll_down actions are executed on "wifi_list" page
- **THEN** get_scroll_count("wifi_list") returns 3

### Requirement: Executor calculates total scroll distance
The executor SHALL provide get_total_scroll_distance() method returning cumulative scroll distance for a given path.

#### Scenario: Total distance accumulates deltas
- **WHEN** scroll_down(0.3) and scroll_down(0.2) are executed
- **THEN** get_total_scroll_distance() returns 0.5

### Requirement: Executor delegates non-scroll actions to base class
The executor SHALL delegate click, back, and input_text actions to the base StatefulMockActionExecutor implementation.

#### Scenario: Click actions are delegated
- **WHEN** click action is executed
- **THEN** action is handled by base class implementation

#### Scenario: Back actions are delegated
- **WHEN** back action is executed
- **THEN** action is handled by base class implementation
