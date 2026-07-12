# Spec: Scrollable Mock Services

## ADDED Requirements

### Requirement: ScrollableMockVisionService accumulates mode elements

The system SHALL provide `ScrollableMockVisionService` that returns elements using accumulation mode (all segments with threshold <= current progress are visible).

#### Scenario: Single segment visibility
- **WHEN** current progress is 0.5 and segments have thresholds [0.0, 1.0]
- **THEN** returns elements from segment at threshold 0.0 only

#### Scenario: Multiple segment accumulation
- **WHEN** current progress is 0.5 and segments have thresholds [0.0, 0.5, 1.0]
- **THEN** returns elements from segments at thresholds 0.0 and 0.5 (accumulated)

#### Scenario: Full visibility at max progress
- **WHEN** current progress is 1.0 and segments have thresholds [0.0, 0.5, 1.0]
- **THEN** returns elements from all segments

#### Scenario: No elements at zero progress with threshold > 0
- **WHEN** current progress is 0.0 and minimum segment threshold is 0.5
- **THEN** returns empty element set

### Requirement: Element deduplication in ScrollableMockVisionService

The system SHALL deduplicate elements by ID, preferring the instance from the lowest threshold segment.

#### Scenario: Same ID across segments
- **WHEN** element "wifi_switch" appears in segments at thresholds 0.0 and 0.5, progress is 1.0
- **THEN** returns only one "wifi_switch" instance (from threshold 0.0)

#### Scenario: Unique elements accumulation
- **WHEN** segment 0.0 has [A, B], segment 0.5 has [C, D], progress is 1.0
- **THEN** returns [A, B, C, D] (all unique elements)

#### Scenario: Partial overlap deduplication
- **WHEN** segment 0.0 has [A, B], segment 0.5 has [B, C], progress is 1.0
- **THEN** returns [A, B, C] with B from segment 0.0

### Requirement: Dynamic IsEndOfList calculation

The system SHALL compute `IsEndOfList` based on current progress vs maximum segment threshold.

#### Scenario: At bottom (progress equals max threshold)
- **WHEN** current progress is 1.0 and maximum segment threshold is 1.0
- **THEN** IsEndOfList=true

#### Scenario: Not at bottom (progress less than max threshold)
- **WHEN** current progress is 0.5 and maximum segment threshold is 1.0
- **THEN** IsEndOfList=false

#### Scenario: At bottom with epsilon tolerance
- **WHEN** current progress is 0.999 and maximum segment threshold is 1.0, epsilon is 0.001
- **THEN** IsEndOfList=true (within epsilon)

### Requirement: Dynamic HasScroll calculation

The system SHALL compute `HasScroll` based on existence of scroll data for the current page.

#### Scenario: Has scroll data
- **WHEN** page ID exists in ScrollDataStore with segments
- **THEN** HasScroll=true

#### Scenario: No scroll data
- **WHEN** page ID does not exist in ScrollDataStore
- **THEN** HasScroll=false

#### Scenario: Empty segments
- **WHEN** page ID exists but segment array is empty
- **THEN** HasScroll=true (data exists, even if empty)

### Requirement: Scroll progress management

The system SHALL manage scroll progress per page through `GetScrollProgress` and internal state updates.

#### Scenario: Initial progress
- **WHEN** retrieving scroll progress for page before any scroll
- **THEN** returns 0.0

#### Scenario: Progress after scroll
- **WHEN** scroll operation advances progress from 0.0 to 0.3
- **THEN** GetScrollProgress returns 0.3

#### Scenario: Progress per-page isolation
- **WHEN** scrolling on page A does not affect page B
- **THEN** GetScrollProgress for page B returns its own independent value

#### Scenario: Progress clamping at max threshold
- **WHEN** scroll operation would advance progress beyond max threshold (1.0)
- **THEN** progress is clamped to max threshold

### Requirement: ScrollableMockActionExecutor scroll execution

The system SHALL provide `ScrollableMockActionExecutor` that executes scroll operations and updates vision service state.

#### Scenario: ScrollDown execution
- **WHEN** ScrollDown(0.3) is called
- **THEN** calls vision service SimulateScroll with +0.3 delta

#### Scenario: ScrollUp execution
- **WHEN** ScrollUp(0.3) is called
- **THEN** calls vision service SimulateScroll with -0.3 delta

#### Scenario: Scroll action recording
- **WHEN** scroll operation executes
- **THEN** ScrollAction record is added to page's ScrollState

#### Scenario: Scroll count increment
- **WHEN** scroll operation executes
- **THEN** ScrollState.ScrollCount increments by 1

### Requirement: SimulateScroll state update

The system SHALL update scroll state when `SimulateScroll` is called on the vision service.

#### Scenario: Progress increment
- **WHEN** SimulateScroll(+0.3) is called at current progress 0.0
- **THEN** progress updates to 0.3

#### Scenario: Progress decrement
- **WHEN** SimulateScroll(-0.3) is called at current progress 0.5
- **THEN** progress updates to 0.2

#### Scenario: Progress clamping at zero
- **WHEN** SimulateScroll(-0.5) is called at current progress 0.3
- **THEN** progress updates to 0.0 (clamped)

#### Scenario: Progress history recording
- **WHEN** SimulateScroll updates progress
- **THEN** new progress value is appended to ScrollState.ScrollHistory

### Requirement: PageAnalysis integration

The system SHALL return `PageAnalysis` with computed `IsEndOfList` and `HasScroll` values.

#### Scenario: PageAnalysis with scroll data
- **WHEN** AnalyzeCurrentPageAsync is called for page with scroll data at progress 0.5
- **THEN** returns PageAnalysis with IsEndOfList=false, HasScroll=true

#### Scenario: PageAnalysis at bottom
- **WHEN** AnalyzeCurrentPageAsync is called for page at progress 1.0
- **THEN** returns PageAnalysis with IsEndOfList=true, HasScroll=true

#### Scenario: PageAnalysis without scroll data
- **WHEN** AnalyzeCurrentPageAsync is called for page without scroll data
- **THEN** returns PageAnalysis with IsEndOfList=true, HasScroll=false

### Requirement: StateFixtureBuilder integration

The system SHALL extend `StateFixtureBuilder` with `.ScrollSegments()` fluent API for configuring scroll data.

#### Scenario: Adding scroll segments
- **WHEN** builder calls .ScrollSegments(pageId, segments...)
- **THEN** ScrollDataStore is populated with segments for the page

#### Scenario: Multiple page configuration
- **WHEN** builder adds scroll segments for multiple pages
- **THEN** each page has its own segment configuration

#### Scenario: Backward compatibility
- **WHEN** builder is used without calling .ScrollSegments()
- **THEN** StateFixture builds successfully (scroll segments optional)
