# Spec: ScrollHandler

## ADDED Requirements

### Requirement: Scrollability detection

The system SHALL detect scrollability based on page data, end-of-list status, and current progress.

#### Scenario: NotScrollable detection
- **WHEN** page has no scroll data in ScrollDataStore
- **THEN** scrollability is NotScrollable

#### Scenario: CanScrollDown detection
- **WHEN** page has scroll data, IsEndOfList=false, HasScroll=true
- **THEN** scrollability is CanScrollDown

#### Scenario: AtBottom detection
- **WHEN** page has scroll data, IsEndOfList=true
- **THEN** scrollability is AtBottom

#### Scenario: CanScrollUp detection
- **WHEN** page has scroll data, CurrentProgress>0.0, not at bottom
- **THEN** scrollability is CanScrollUp

### Requirement: Scroll classification

The system SHALL classify scroll situations by computing current progress, max threshold, and recommended step.

#### Scenario: Classification with segments
- **WHEN** classifying with segments at thresholds [0.0, 0.5, 1.0] and current progress 0.3
- **THEN** CurrentProgress=0.3, MaxProgress=1.0, RecommendedStep=Min(0.3, 0.7)

#### Scenario: Classification with no segments
- **WHEN** classifying with empty segments
- **THEN** MaxProgress=1.0, RecommendedStep=0.3

#### Scenario: Classification near bottom
- **WHEN** classifying with CurrentProgress=0.9, MaxProgress=1.0, default step=0.3
- **THEN** RecommendedStep=0.1 (clamped to remaining distance)

### Requirement: Scroll decision mapping

The system SHALL map scrollability classification to concrete action type.

#### Scenario: CanScrollDown maps to ScrollDown
- **WHEN** scrollability is CanScrollDown
- **THEN** action type is ScrollDown

#### Scenario: CanScrollUp maps to ScrollUp
- **WHEN** scrollability is CanScrollUp
- **THEN** action type is ScrollUp

#### Scenario: AtBottom maps to None
- **WHEN** scrollability is AtBottom
- **THEN** action type is None

#### Scenario: NotScrollable maps to None
- **WHEN** scrollability is NotScrollable
- **THEN** action type is None

### Requirement: Scroll action execution

The system SHALL execute scroll actions via Hook Dispatch table with exception fallback.

#### Scenario: ScrollDown execution
- **WHEN** executing ScrollDown with step 0.3 via ScrollableMockActionExecutor
- **THEN** action executor performs scroll and returns success with new progress

#### Scenario: ScrollUp execution
- **WHEN** executing ScrollUp with step 0.3 via ScrollableMockActionExecutor
- **THEN** action executor performs reverse scroll and returns success with new progress

#### Scenario: None action execution
- **WHEN** executing None action
- **THEN** returns success with unchanged progress

#### Scenario: Missing handler fallback
- **WHEN** executing action with no registered handler
- **THEN** returns DefaultNone result (success, no progress change)

#### Scenario: Exception handling
- **WHEN** handler throws exception during execution
- **THEN** returns failure result with exception message, progress unchanged

### Requirement: Jump detection

The system SHALL detect scroll jumps by comparing element ID sets before and after scroll.

#### Scenario: Normal scroll (has overlap)
- **WHEN** scrolling from [A,B,C] to [C,D,E]
- **THEN** status is HasOverlap (no jump)

#### Scenario: Jump detected (no overlap, both have elements)
- **WHEN** scrolling from [A,B] to [C,D]
- **THEN** status is NoOverlap_BothHaveElements (jump detected)

#### Scenario: Initial scroll (before empty)
- **WHEN** scrolling from [] to [A,B]
- **THEN** status is NoOverlap_BeforeEmpty (safe initial state)

#### Scenario: End of list (after empty)
- **WHEN** scrolling from [A,B] to []
- **THEN** status is NoOverlap_AfterEmpty (possible end)

#### Scenario: Empty list (both empty)
- **WHEN** scrolling from [] to []
- **THEN** status is BothEmpty

### Requirement: Jump recovery

The system SHALL recover from detected jumps by rolling back and retrying with reduced step size.

#### Scenario: Successful recovery on first retry
- **WHEN** jump detected, rollback to original progress, retry with half step, overlap achieved
- **THEN** returns Success=true, RetryCount=1, FinalProgress=updated position

#### Scenario: Successful recovery after multiple retries
- **WHEN** first retry fails, second retry with quarter step succeeds
- **THEN** returns Success=true, RetryCount=2

#### Scenario: Failed recovery (max retries exceeded)
- **WHEN** all retries exhausted without overlap
- **THEN** returns Success=false, RetryCount=max, FinalProgress=original position

#### Scenario: Recovery step clamping
- **WHEN** recovery step calculation goes below MinScrollStep
- **THEN** step is clamped to MinScrollStep

### Requirement: Statistics collection

The system SHALL collect scroll operation statistics across the pipeline.

#### Scenario: Scrolled count tracking
- **WHEN** ScrollDown action executes successfully
- **THEN** ScrolledCount increments by 1

#### Scenario: Skipped count tracking
- **WHEN** scrollability is NotScrollable or AtBottom
- **THEN** SkippedCount increments by 1

#### Scenario: Jump detected count tracking
- **WHEN** jump detection triggers
- **THEN** JumpDetectedCount increments by 1

#### Scenario: Jump recovered count tracking
- **WHEN** jump recovery succeeds
- **THEN** JumpRecoveredCount increments by 1

#### Scenario: Total distance tracking
- **WHEN** scroll moves from 0.0 to 0.3
- **THEN** TotalDistance increases by 0.3

#### Scenario: Average step calculation
- **WHEN** three scrolls execute with steps 0.3, 0.3, 0.1
- **THEN** AverageStep = (0.3 + 0.3 + 0.1) / 3

### Requirement: HandleScroll orchestration

The system SHALL orchestrate the 7-step pipeline through ScrollHandler.HandleScroll().

#### Scenario: Full pipeline execution (scroll needed)
- **WHEN** calling HandleScroll with CanScrollDown situation
- **THEN** pipeline executes Detect→Classify→Decide→Execute→Verify→Statistics, returns success result

#### Scenario: Full pipeline execution (jump recovery)
- **WHEN** Execute causes jump, Verify detects it
- **THEN** pipeline executes Recover step, returns recovery result

#### Scenario: Pipeline skip (not scrollable)
- **WHEN** calling HandleScroll with NotScrollable situation
- **THEN** Detect returns NotScrollable, Decide returns None, Statistics records skip, returns skip result

#### Scenario: Pipeline skip (at bottom)
- **WHEN** calling HandleScroll with AtBottom situation
- **THEN** Detect returns AtBottom, Decide returns None, Statistics records skip, returns skip result
