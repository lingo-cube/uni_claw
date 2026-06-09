## ADDED Requirements

### Requirement: Scrollable mock vision service accumulates visible elements based on scroll progress
The ScrollableMockVisionService SHALL extend StatefulMockVisionService and return progressive element sets based on scroll progress using accumulation mode where all elements with threshold <= current_progress are visible.

#### Scenario: Initial screen shows zero threshold elements
- **WHEN** scroll progress is 0.0
- **THEN** service returns only elements from segments with threshold 0.0

#### Scenario: Scrolling reveals additional elements
- **WHEN** scroll progress increases from 0.0 to 0.5
- **THEN** service returns elements from segments with threshold 0.0 AND threshold 0.5 (accumulated)

#### Scenario: Full scroll shows all elements
- **WHEN** scroll progress reaches 1.0
- **THEN** service returns all elements from all segments

### Requirement: Scroll state tracked per page using current page ID
The service SHALL maintain independent ScrollState instances per page key using _current_page_id from the base class.

#### Scenario: Different pages have independent scroll progress
- **WHEN** user scrolls page A to 0.5 then navigates to page B
- **THEN** page B starts at scroll progress 0.0

#### Scenario: Returning to page preserves scroll state
- **WHEN** user returns to previously visited page
- **THEN** scroll progress is preserved from last visit

### Requirement: Scroll simulation updates progress and records history
The service SHALL provide simulate_scroll() method that updates scroll progress by delta amount and records scroll history.

#### Scenario: Scrolling down increases progress
- **WHEN** simulate_scroll(page_key, 0.3) is called with current progress 0.0
- **THEN** progress becomes 0.3 and scroll_count increments by 1

#### Scenario: Scrolling up decreases progress
- **WHEN** simulate_scroll(page_key, -0.2) is called with current progress 0.5
- **THEN** progress becomes 0.3

#### Scenario: Scroll progress bounded by 0.0 and 1.0
- **WHEN** simulate_scroll attempts to set progress below 0.0 or above 1.0
- **THEN** progress is clamped to valid range

### Requirement: Element deduplication by ID across segments
The service SHALL deduplicate elements by element ID when the same element appears in multiple scroll segments.

#### Scenario: Duplicate element appears once
- **WHEN** element with ID "net1" exists in both threshold 0.0 and 0.5 segments and progress is 0.5
- **THEN** "net1" appears only once in visible elements

### Requirement: Fault injection supports delay simulation
The service SHALL provide set_scroll_delay() method to inject artificial delays during scroll operations.

#### Scenario: Scroll delay is applied
- **WHEN** set_scroll_delay(page_key, 500) is called and simulate_scroll is executed
- **THEN** scroll operation pauses for at least 500ms

### Requirement: Fault injection supports unresponsiveness simulation
The service SHALL provide enable_scroll_failure() method to simulate scroll operations that fail to update progress.

#### Scenario: One-time scroll failure
- **WHEN** enable_scroll_failure(page_key, fail_once=True) is called and simulate_scroll is executed
- **THEN** first scroll returns unchanged progress, subsequent scrolls operate normally

#### Scenario: Persistent scroll failure
- **WHEN** enable_scroll_failure(page_key, fail_once=False) is called and simulate_scroll is executed
- **THEN** all scrolls return unchanged progress until failure is disabled

### Requirement: Service detects end of list
The service SHALL set is_end_of_list to True when scroll progress reaches 1.0.

#### Scenario: End of list detection
- **WHEN** scroll progress reaches 1.0
- **THEN** is_end_of_list is True and has_scroll is False

### Requirement: Service adapts PageAnalysis to MenuItem model
The service SHALL return PageAnalysis with items field containing MenuItem objects compatible with existing V6.11 models.

#### Scenario: PageAnalysis contains MenuItem items
- **WHEN** analyze_screenshot is called
- **THEN** returned PageAnalysis.items is a List[MenuItem] with id, name, type, and coordinate fields

### Requirement: Service supports both coordinate and bounds element formats
The service SHALL accept elements with either coordinate: {x, y} or bounds: [x, y, w, h] format.

#### Scenario: Coordinate format is parsed
- **WHEN** element has coordinate: {x: 0.5, y: 0.5}
- **THEN** MenuItem.coordinate is {x: 0.5, y: 0.5}

#### Scenario: Bounds format is converted to coordinate
- **WHEN** element has bounds: [100, 200, 50, 50]
- **THEN** MenuItem.coordinate is normalized to {x: ~0.09, y: ~0.10} (assuming 1080x1920 screen)
