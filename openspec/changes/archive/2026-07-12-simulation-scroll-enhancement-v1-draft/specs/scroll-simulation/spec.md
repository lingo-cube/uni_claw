# Scroll Simulation Specification

## ADDED Requirements

### Requirement: Scroll segment data model

The system SHALL provide a `ScrollSegment` record type that defines element visibility at a specific scroll progress threshold.

A `ScrollSegment` SHALL consist of:
- `Threshold` (double): Scroll progress value (0.0-1.0) at which elements become visible
- `Elements` (ImmutableArray<PageElement>): List of elements visible at this threshold

#### Scenario: Scroll segment with zero threshold
- **WHEN** a ScrollSegment is created with Threshold = 0.0
- **THEN** all elements in the segment SHALL be visible when scroll progress is 0.0 or greater

#### Scenario: Scroll segment with mid threshold
- **WHEN** a ScrollSegment is created with Threshold = 0.5
- **THEN** elements in the segment SHALL NOT be visible when scroll progress < 0.5
- **AND** elements SHALL be visible when scroll progress >= 0.5

---

### Requirement: Scroll state tracking

The system SHALL provide a `ScrollState` record type that tracks scroll progress and operation history.

A `ScrollState` SHALL consist of:
- `CurrentProgress` (double): Current scroll progress from 0.0 (top) to 1.0 (bottom)
- `ScrollCount` (int): Number of scroll operations performed
- `ScrollHistory` (ImmutableArray<double>): Progress values after each scroll operation

#### Scenario: Initial scroll state
- **WHEN** a ScrollState is created
- **THEN** CurrentProgress SHALL be 0.0
- **AND** ScrollCount SHALL be 0
- **AND** ScrollHistory SHALL be empty

#### Scenario: Scroll state update
- **WHEN** a scroll operation increases progress by 0.1
- **THEN** CurrentProgress SHALL be updated to the new value (capped at 1.0)
- **AND** ScrollCount SHALL be incremented by 1
- **AND** the new progress value SHALL be appended to ScrollHistory

---

### Requirement: Accumulation mode element visibility

The system SHALL implement accumulation mode where elements from all segments with threshold <= current progress are visible.

The element visibility logic SHALL:
1. Iterate through segments ordered by threshold (ascending)
2. For each segment with threshold <= progress, include its elements
3. Apply element deduplication by ID (first occurrence wins)

#### Scenario: Single segment visibility
- **GIVEN** a page with one ScrollSegment (Threshold = 0.0, Elements = [A, B])
- **WHEN** scroll progress is 0.0
- **THEN** both elements A and B SHALL be visible

#### Scenario: Multi-segment accumulation
- **GIVEN** a page with two ScrollSegments:
  - Segment0 (Threshold = 0.0, Elements = [A, B])
  - Segment1 (Threshold = 0.5, Elements = [C, D])
- **WHEN** scroll progress is 0.5
- **THEN** elements A, B, C, D SHALL all be visible

#### Scenario: Segment not yet reached
- **GIVEN** a page with two ScrollSegments:
  - Segment0 (Threshold = 0.0, Elements = [A])
  - Segment1 (Threshold = 0.5, Elements = [B])
- **WHEN** scroll progress is 0.25
- **THEN** only element A SHALL be visible
- **AND** element B SHALL NOT be visible

---

### Requirement: Element deduplication

The system SHALL deduplicate elements by ID across scroll segments.

When the same element ID appears in multiple segments, the element from the segment with the lowest threshold SHALL be used.

#### Scenario: Element ID uniqueness
- **GIVEN** two segments with the same element ID:
  - Segment0 (Threshold = 0.0, Elements = [wifi_switch])
  - Segment1 (Threshold = 0.5, Elements = [wifi_switch])
- **WHEN** collecting visible elements
- **THEN** only one wifi_switch element SHALL be returned
- **AND** it SHALL be from Segment0 (lower threshold)

---

### Requirement: is_end_of_list calculation

The system SHALL calculate `IsEndOfList` based on scroll progress relative to maximum segment threshold.

`IsEndOfList` SHALL be true when `CurrentProgress >= max(segment thresholds)`.

#### Scenario: Not at end with low progress
- **GIVEN** a page with ScrollSegments with thresholds [0.0, 0.5, 1.0]
- **WHEN** CurrentProgress is 0.5
- **THEN** IsEndOfList SHALL be false

#### Scenario: At end when progress reaches max threshold
- **GIVEN** a page with ScrollSegments with thresholds [0.0, 0.5, 1.0]
- **WHEN** CurrentProgress is 1.0
- **THEN** IsEndOfList SHALL be true

#### Scenario: Empty segments
- **GIVEN** a page with no ScrollSegments
- **WHEN** IsEndOfList is calculated
- **THEN** it SHALL return false

---

### Requirement: ScrollableMockVisionService

The system SHALL provide `ScrollableMockVisionService` that extends `StatefulMockVisionService` with scroll simulation capabilities.

`ScrollableMockVisionService` SHALL:
- Inherit from `StatefulMockVisionService`
- Maintain per-page scroll state tracking
- Override `AnalyzeCurrentPageAsync` to return elements based on scroll progress
- Provide `SimulateScroll(double delta)` method to update scroll progress
- Provide `SetScrollProgress(string pageId, double progress)` method for rollback

#### Scenario: Analyze page with scroll progress
- **GIVEN** ScrollableMockVisionService with scroll progress = 0.5
- **WHEN** `AnalyzeCurrentPageAsync` is called
- **THEN** returned PageAnalysis SHALL contain elements from segments with threshold <= 0.5
- **AND** IsEndOfList SHALL reflect current progress

#### Scenario: Simulate scroll down
- **GIVEN** ScrollableMockVisionService with CurrentProgress = 0.0
- **WHEN** `SimulateScroll(0.1)` is called
- **THEN** CurrentProgress SHALL be 0.1
- **AND** ScrollCount SHALL be 1
- **AND** ScrollHistory SHALL contain [0.1]

#### Scenario: Simulate scroll up (negative delta)
- **GIVEN** ScrollableMockVisionService with CurrentProgress = 0.5
- **WHEN** `SimulateScroll(-0.1)` is called
- **THEN** CurrentProgress SHALL be 0.4
- **AND** ScrollCount SHALL be 1

#### Scenario: Scroll progress clamping
- **GIVEN** ScrollableMockVisionService with CurrentProgress = 0.9
- **WHEN** `SimulateScroll(0.2)` is called
- **THEN** CurrentProgress SHALL be clamped to 1.0 (not 1.1)

#### Scenario: Set scroll progress for rollback
- **GIVEN** ScrollableMockVisionService with CurrentProgress = 0.5
- **WHEN** `SetScrollProgress(pageId, 0.3)` is called
- **THEN** CurrentProgress SHALL be 0.3
- **AND** ScrollCount SHALL NOT be incremented

---

### Requirement: StateFixtureBuilder scroll extension

The system SHALL extend `StateFixtureBuilder` with scroll segment definition support.

`PageStateBuilder` SHALL provide a `ScrollSegments` method that accepts:
- Variable number of `(double threshold, Action<ScrollSegmentBuilder> configure)` tuples

#### Scenario: Define scrollable page
- **WHEN** using StateFixtureBuilder to define a page
- **THEN** ScrollSegments can be added via fluent API:
  ```csharp
  .Page("wifi_list", p => p
      .ScrollSegments(
          (0.0, s => s.Element("wifi_switch", ...)),
          (0.5, s => s.Element("net1", ...))
      )
  )
  ```

#### Scenario: Backward compatibility
- **GIVEN** existing StateFixtureBuilder code without scroll segments
- **WHEN** building a StateFixture
- **THEN** it SHALL continue to work without modification

---

### Requirement: ScrollableMockActionExecutor

The system SHALL provide `ScrollableMockActionExecutor` that extends `StatefulMockActionExecutor` with scroll action execution capabilities.

`ScrollableMockActionExecutor` SHALL:
- Inherit from `StatefulMockActionExecutor`
- Accept `ScrollableMockVisionService` in constructor (instead of base `StatefulMockVisionService`)
- Override action execution to handle scroll_down and scroll_up operations
- Coordinate with `ScrollableMockVisionService.SimulateScroll()` to update progress
- Record scroll actions in history

#### Scenario: Execute scroll_down action
- **GIVEN** ScrollableMockActionExecutor with ScrollableMockVisionService
- **WHEN** a scroll_down action is executed with step_percent = 0.3
- **THEN** `ScrollableMockVisionService.SimulateScroll(0.3)` SHALL be called
- **AND** scroll progress SHALL increase by 0.3
- **AND** scroll count SHALL be incremented
- **AND** action SHALL be recorded in history

#### Scenario: Execute scroll_up action
- **GIVEN** ScrollableMockActionExecutor with ScrollableMockVisionService
- **WHEN** a scroll_up action is executed with step_percent = 0.3
- **THEN** `ScrollableMockVisionService.SimulateScroll(-0.3)` SHALL be called
- **AND** scroll progress SHALL decrease by 0.3
- **AND** scroll count SHALL be incremented

#### Scenario: Scroll action returns success
- **GIVEN** ScrollableMockActionExecutor executes a valid scroll operation
- **WHEN** the scroll completes
- **THEN** action execution SHALL return `true` (success)

---

### Requirement: Scroll action record

The system SHALL provide `ScrollAction` record type for tracking scroll operations in action history.

A `ScrollAction` SHALL consist of:
- `Action` (string): Action type ("SCROLL_DOWN" or "SCROLL_UP")
- `StepPercent` (double): Scroll step size (e.g., 0.3 for 30%)
- `BeforeProgress` (double): Progress value before scroll
- `AfterProgress` (double): Progress value after scroll
- `Timestamp` (DateTimeOffset): When the scroll occurred

#### Scenario: Scroll action record creation
- **GIVEN** a scroll_down operation with step 0.3 from progress 0.0
- **WHEN** ScrollAction is created
- **THEN** Action SHALL be "SCROLL_DOWN"
- **AND** StepPercent SHALL be 0.3
- **AND** BeforeProgress SHALL be 0.0
- **AND** AfterProgress SHALL be 0.3

---

### Requirement: HasScroll calculation

The system SHALL calculate `HasScroll` based on whether there are scroll segments beyond current progress.

`HasScroll` SHALL be true when `any(segment.threshold > current_progress)`.

#### Scenario: HasScroll true when segments remain
- **GIVEN** a page with ScrollSegments with thresholds [0.0, 0.5, 1.0]
- **WHEN** CurrentProgress is 0.5
- **THEN** HasScroll SHALL be true (segment with threshold 1.0 remains)

#### Scenario: HasScroll false at max progress
- **GIVEN** a page with ScrollSegments with thresholds [0.0, 0.5, 1.0]
- **WHEN** CurrentProgress is 1.0
- **THEN** HasScroll SHALL be false

#### Scenario: HasScroll false for non-scrollable pages
- **GIVEN** a page with no ScrollSegments
- **WHEN** HasScroll is calculated
- **THEN** it SHALL return false

---

### Requirement: Scroll decision logic in traversal

The system SHALL implement scroll decision logic during page traversal.

The traversal SHALL:
- After visiting all visible elements on current page
- Check if `HasScroll && !IsEndOfList`
- If true, execute scroll_down action to reveal more elements
- If false, consider page complete and move to next node

#### Scenario: Continue scrolling when more content exists
- **GIVEN** a scrollable page with HasScroll=true and IsEndOfList=false
- **WHEN** all visible elements have been visited
- **THEN** traversal SHALL execute a scroll_down action
- **AND** remain on the same page for more elements

#### Scenario: Stop scrolling when end reached
- **GIVEN** a scrollable page with HasScroll=false and IsEndOfList=true
- **WHEN** all visible elements have been visited
- **THEN** traversal SHALL NOT execute more scroll actions
- **AND** consider the page complete

#### Scenario: Non-scrollable page bypass
- **GIVEN** a non-scrollable page (HasScroll=false)
- **WHEN** all visible elements have been visited
- **THEN** traversal SHALL immediately consider the page complete
- **AND** move to next node without scrolling

---

### Requirement: Configurable scroll step parameters

The system SHALL provide configurable scroll step parameters through `ScrollHandlerConfig`.

`ScrollHandlerConfig` SHALL include:
- `DefaultScrollStep` (double): Default scroll step size (default 0.3 = 30%)
- `MinScrollStep` (double): Minimum scroll step size (default 0.01 = 1%)
- `MaxScrollStep` (double): Maximum scroll step size (default 0.5 = 50%)
- `MaxJumpRetryCount` (int): Maximum number of jump recovery retries (default 3)
- `JumpRecoveryFactor` (double): Jump recovery step reduction factor (default 0.5)
- `EnableAdaptiveStep` (bool): Whether to enable adaptive step calculation (default true)
- `DuplicateRatioThreshold` (double): Duplicate ratio threshold for adaptive step (default 0.7)
- `AdaptiveStepIncrease` (double): Adaptive step increase factor (default 1.5)
- `MinSampleSize` (int): Minimum sample size for adaptive step (default 3)
- `ProgressEpsilon` (double): Progress comparison precision (default 0.001)

All parameters SHALL be configurable with sensible defaults.

#### Scenario: Default configuration
- **WHEN** ScrollHandlerConfig is created with no parameters
- **THEN** DefaultScrollStep SHALL be 0.3
- **AND** MinScrollStep SHALL be 0.01
- **AND** MaxScrollStep SHALL be 0.5
- **AND** MaxJumpRetryCount SHALL be 3

#### Scenario: Custom configuration
- **WHEN** ScrollHandlerConfig is created with custom parameters
- **THEN** all custom parameters SHALL be applied
- **AND** unspecified parameters SHALL use defaults

---

### Requirement: Jump detection (core pipeline)

The system SHALL detect scroll jumps as part of the core scroll pipeline.

Jump detection SHALL:
1. Capture element IDs before scroll
2. Capture element IDs after scroll
3. Calculate overlap status between before and after elements
4. Trigger recovery if no overlap exists when both sets have elements

#### Scenario: Normal scroll with overlap
- **GIVEN** scroll from progress 0.0 with elements [A, B] to progress 0.3 with elements [A, B, C]
- **WHEN** jump detection is performed
- **THEN** overlap status SHALL be HasOverlap
- **AND** no recovery SHALL be triggered

#### Scenario: Jump detected (no overlap)
- **GIVEN** scroll from progress 0.0 with elements [A, B] to progress 1.0 with elements [E]
- **WHEN** jump detection is performed
- **THEN** overlap status SHALL be NoOverlap_BothHaveElements
- **AND** recovery SHALL be triggered

#### Scenario: Initial state (no before elements)
- **GIVEN** scroll from initial state (no elements) to progress 0.3 with elements [A, B]
- **WHEN** jump detection is performed
- **THEN** overlap status SHALL be NoOverlap_BeforeEmpty
- **AND** no recovery SHALL be triggered

---

### Requirement: Jump recovery mechanism

The system SHALL recover from detected jumps by rolling back and retrying with reduced step size.

Jump recovery SHALL:
1. Roll back scroll progress to before-jump value
2. Reduce step size by half (but not below MinScrollStep)
3. Retry scroll operation
4. Repeat up to MaxJumpRetryCount times
5. Return success if overlap achieved
6. Return failure if max retries exceeded

#### Scenario: Successful jump recovery
- **GIVEN** a jump is detected with step 0.3
- **WHEN** jump recovery is executed
- **THEN** progress SHALL be rolled back
- **AND** step SHALL be reduced to 0.15
- **AND** scroll SHALL be retried
- **AND** if successful, recovery SHALL return success

#### Scenario: Jump recovery failure
- **GIVEN** MaxJumpRetryCount is 3
- **WHEN** jump still occurs after 3 retries
- **THEN** recovery SHALL return failure
- **AND** progress SHALL be rolled back to original value

#### Scenario: Step size clamping during recovery
- **GIVEN** current step is 0.02 and MinScrollStep is 0.01
- **WHEN** step is halved for recovery
- **THEN** new step SHALL be 0.01 (clamped to minimum)
- **AND** subsequent retries SHALL use 0.01

---

### Requirement: Adaptive step calculation

The system SHALL calculate adaptive scroll steps based on duplicate element ratio.

Adaptive step calculation SHALL:
1. Calculate duplicate element ratio (duplicate count / total after elements)
2. Calculate new element count (elements not in before set)
3. If duplicate ratio >= threshold AND new elements >= minimum sample size:
   - Increase step by AdaptiveStepIncrease factor
   - Clamp to MaxScrollStep
4. Otherwise, maintain current step

#### Scenario: Adaptive step increase
- **GIVEN** scroll with 8 elements after, 7 duplicates, 1 new element
- **AND** DuplicateRatioThreshold is 0.7, MinSampleSize is 3, AdaptiveStepIncrease is 1.5
- **WHEN** adaptive step is calculated
- **THEN** new element count (1) < MinSampleSize (3)
- **AND** step SHALL NOT be increased

#### Scenario: Adaptive step increase with sufficient samples
- **GIVEN** scroll with 10 elements after, 8 duplicates, 2 new elements
- **AND** DuplicateRatioThreshold is 0.7, MinSampleSize is 2, AdaptiveStepIncrease is 1.5
- **AND** current step is 0.3
- **WHEN** adaptive step is calculated
- **THEN** duplicate ratio (0.8) >= threshold (0.7)
- **AND** new element count (2) >= MinSampleSize (2)
- **AND** next step SHALL be 0.45 (0.3 * 1.5)

#### Scenario: Adaptive step clamping
- **GIVEN** current step is 0.4, MaxScrollStep is 0.5
- **AND** adaptive increase conditions are met
- **WHEN** adaptive step is calculated
- **THEN** next step SHALL be 0.5 (0.4 * 1.5 clamped to max)

#### Scenario: Adaptive step disabled
- **GIVEN** EnableAdaptiveStep is false
- **WHEN** adaptive step is calculated
- **THEN** step SHALL remain unchanged regardless of duplicate ratio

---

### Requirement: ScrollHandler 7-step pipeline

The system SHALL implement ScrollHandler with 7-step pipeline:

1. **Detect**: Determine scrollability (NotScrollable, CanScrollDown, AtBottom, CanScrollUp)
2. **Classify**: Create ScrollDecision with current progress and recommended step
3. **Decide**: Map ScrollDecision to ScrollActionType (None, ScrollDown, ScrollUp)
4. **Execute**: Perform scroll action via dispatch table
5. **Verify**: Check for jumps using overlap detection
6. **Recover**: Handle jump detection with rollback and retry (if needed)
7. **Statistics**: Record scroll metrics (count, distance, jumps, retries)

#### Scenario: Full pipeline execution
- **GIVEN** a scrollable page at progress 0.0
- **WHEN** HandleScroll is called
- **THEN** all 7 steps SHALL execute in order
- **AND** final result SHALL indicate success with updated progress

#### Scenario: Pipeline skip on non-scrollable page
- **GIVEN** a non-scrollable page
- **WHEN** HandleScroll is called
- **THEN** pipeline SHALL skip to statistics after Detect step
- **AND** result SHALL indicate skip with appropriate description

---

### Requirement: Scroll statistics collection

The system SHALL collect comprehensive scroll statistics.

Scroll statistics SHALL include:
- `ScrolledCount`: Number of successful scroll operations
- `SkippedCount`: Number of times scroll was skipped (AtBottom/NotScrollable)
- `JumpDetectedCount`: Number of jumps detected
- `JumpRecoveredCount`: Number of jumps successfully recovered
- `TotalDistance`: Total scroll distance accumulated
- `AverageStep`: Average step size used

#### Scenario: Statistics tracking
- **GIVEN** a scroll session with multiple operations
- **WHEN** GetStatistics is called
- **THEN** all statistics SHALL be accurately calculated
- **AND** AverageStep SHALL equal TotalDistance / ScrolledCount

---

### Requirement: Four-tier anti-miss mechanism

The system SHALL implement four-tier protection against missing elements:

1. **Overlap Detection**: Detect jumps by checking element overlap between scrolls
2. **Progress Clamp**: Ensure step never exceeds remaining distance
3. **Element Deduplication**: Deduplicate by ID (lowest threshold wins)
4. **Visited Tracking**: Track visited children to prevent re-visited loops

#### Scenario: Complete anti-miss protection
- **GIVEN** a multi-segment page with 5 segments
- **WHEN** scrolling from top to bottom
- **THEN** all elements SHALL be visited exactly once
- **AND** no elements SHALL be missed
- **AND** no elements SHALL be visited twice
