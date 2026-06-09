## ADDED Requirements

### Requirement: ScrollSegment defines threshold and elements
The ScrollSegment dataclass SHALL define threshold (float 0.0-1.0) and elements (List[Dict[str, Any]]) fields.

#### Scenario: ScrollSegment creation
- **WHEN** ScrollSegment is created with threshold=0.5 and elements=[{"id": "item1"}]
- **THEN** segment.threshold equals 0.5 and segment.elements contains one element

#### Scenario: ScrollSegment to_dict serialization
- **WHEN** to_dict() is called on ScrollSegment
- **THEN** returns dictionary with threshold and elements keys

### Requirement: ScrollState tracks scroll progress and history
The ScrollState dataclass SHALL track current_progress (float), last_scroll_time (Optional[float]), scroll_count (int), scroll_history (List[float]), fail_next_scroll (bool), and simulate_delay_ms (int).

#### Scenario: ScrollState initialization
- **WHEN** ScrollState is created without parameters
- **THEN** current_progress is 0.0, scroll_count is 0, scroll_history is empty list

#### Scenario: ScrollState tracks progress updates
- **WHEN** current_progress is updated from 0.0 to 0.5
- **THEN** progress is stored and can be retrieved

#### Scenario: ScrollState records scroll history
- **WHEN** scroll progresses through 0.3, 0.6, 0.9
- **THEN** scroll_history contains [0.3, 0.6, 0.9]

#### Scenario: ScrollState fault injection fields
- **WHEN** fail_next_scroll is set to True
- **THEN** flag indicates next scroll should fail

#### Scenario: ScrollState delay field
- **WHEN** simulate_delay_ms is set to 500
- **THEN** delay value is stored for use during scroll operations

### Requirement: ScrollState reserves simulate_jumps field for V7.x
The ScrollState dataclass SHALL include commented-out simulate_jumps and jump_delta_multiplier fields with TODO annotation for V7.x.

#### Scenario: Reserved fields are commented
- **WHEN** ScrollState source code is inspected
- **THEN** simulate_jumps field is present but commented with TODO for V7.x

### Requirement: ScrollAction records scroll operation metadata
The ScrollAction dataclass SHALL record action (str), path (str), step_percent (float), before_progress (float), after_progress (float), and timestamp (float).

#### Scenario: ScrollAction for down scroll
- **WHEN** ScrollAction is created with action="DOWN", before_progress=0.0, after_progress=0.3
- **THEN** all fields are populated with provided values

#### Scenario: ScrollAction for up scroll
- **WHEN** ScrollAction is created with action="UP", before_progress=0.6, after_progress=0.3
- **THEN** after_progress is less than before_progress

### Requirement: ScrollPage aggregates scroll segments
The ScrollPage dataclass SHALL define path (str), has_scroll (bool), and scroll_segments (List[ScrollSegment]) fields.

#### Scenario: ScrollPage with multiple segments
- **WHEN** ScrollPage is created with three scroll segments at thresholds 0.0, 0.5, 1.0
- **THEN** scroll_segments list contains all three segments in order

#### Scenario: ScrollPage to_dict serialization
- **WHEN** to_dict() is called on ScrollPage
- **THEN** returns dictionary with path, has_scroll, and scroll_segments keys
