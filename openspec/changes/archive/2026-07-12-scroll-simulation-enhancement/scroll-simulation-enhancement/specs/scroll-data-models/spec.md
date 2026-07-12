# Spec: Scroll Data Models

## ADDED Requirements

### Requirement: ScrollSegment represents threshold-bounded element sets

The system SHALL provide a `ScrollSegment` record that associates a threshold value with an immutable array of page elements.

#### Scenario: ScrollSegment creation
- **WHEN** creating a ScrollSegment with threshold 0.5 and elements A, B
- **THEN** the segment stores threshold=0.5 and elements=[A, B]

#### Scenario: ScrollSegment threshold validation
- **WHEN** creating a ScrollSegment with threshold < 0.0 or > 1.0
- **THEN** the system throws DomainValidationException

### Requirement: ScrollState tracks progress and history

The system SHALL provide a `ScrollState` record that tracks current scroll progress (0.0-1.0), scroll operation count, and historical progress values.

#### Scenario: Initial scroll state
- **WHEN** creating initial ScrollState
- **THEN** CurrentProgress=0.0, ScrollCount=0, ScrollHistory=empty

#### Scenario: Scroll state after operation
- **WHEN** recording a scroll operation from 0.0 to 0.3
- **THEN** CurrentProgress=0.3, ScrollCount=1, ScrollHistory=[0.0, 0.3]

#### Scenario: Scroll progress clamping
- **WHEN** setting CurrentProgress to 1.5
- **THEN** the value is clamped to 1.0

### Requirement: ScrollAction records operation details

The system SHALL provide a `ScrollAction` record that captures scroll operation type, step percentage, before/after progress, and timestamp.

#### Scenario: ScrollDown action recording
- **WHEN** recording a SCROLL_DOWN action with step 0.3 from 0.0 to 0.3
- **THEN** Action="SCROLL_DOWN", StepPercent=0.3, BeforeProgress=0.0, AfterProgress=0.3

#### Scenario: Timestamp auto-generation
- **WHEN** creating a ScrollAction
- **THEN** Timestamp is set to DateTimeOffset.UtcNow

### Requirement: ScrollDataStore manages segment data

The system SHALL provide a `ScrollDataStore` class that manages ScrollSegment collections indexed by page ID.

#### Scenario: Adding page segments
- **WHEN** adding segments for page "wifi_list"
- **THEN** the store associates the segments with "wifi_list"

#### Scenario: Retrieving page segments
- **WHEN** retrieving segments for existing page "wifi_list"
- **THEN** the system returns the associated ScrollSegment array

#### Scenario: Retrieving non-existent page
- **WHEN** retrieving segments for non-existent page
- **THEN** the system returns empty ImmutableArray<ScrollSegment>

#### Scenario: Checking scroll data existence
- **WHEN** checking if page has scroll data
- **THEN** the system returns true if segments were added, false otherwise

### Requirement: OverlapStatus categorizes element overlap

The system SHALL provide an `OverlapStatus` enum that categorizes the overlap state between before/after element sets.

#### Scenario: HasOverlap status
- **WHEN** both sets contain elements and share at least one ID
- **THEN** status is HasOverlap

#### Scenario: NoOverlap_BothHaveElements (jump)
- **WHEN** both sets contain elements and share zero IDs
- **THEN** status is NoOverlap_BothHaveElements (jump detected)

#### Scenario: NoOverlap_BeforeEmpty (initial)
- **WHEN** before set is empty and after set has elements
- **THEN** status is NoOverlap_BeforeEmpty (safe initial state)

#### Scenario: NoOverlap_AfterEmpty (end)
- **WHEN** after set is empty and before set has elements
- **THEN** status is NoOverlap_AfterEmpty (possible end of list)

#### Scenario: BothEmpty (empty list)
- **WHEN** both sets are empty
- **THEN** status is BothEmpty

### Requirement: ScrollVerifyResult captures verification details

The system SHALL provide a `ScrollVerifyResult` record that captures overlap status, element sets, and statistics.

#### Scenario: Verification result creation
- **WHEN** verifying scroll with before=[A,B,C], after=[C,D,E]
- **THEN** OverlapStatus=HasOverlap, OverlapCount=1, NewElementCount=2, DuplicateElementCount=1

#### Scenario: Jump detection result
- **WHEN** verifying scroll with before=[A,B], after=[C,D] (no overlap)
- **THEN** OverlapStatus=NoOverlap_BothHaveElements, OverlapCount=0, NewElementCount=2

### Requirement: JumpRecoveryResult tracks recovery outcome

The system SHALL provide a `JumpRecoveryResult` record that tracks recovery success, retry count, final step, and progress.

#### Scenario: Successful recovery
- **WHEN** recovery succeeds on first retry with step 0.25
- **THEN** Success=true, RetryCount=1, FinalStep=0.25, Reason describes success

#### Scenario: Failed recovery (max retries)
- **WHEN** recovery exceeds MaxJumpRetryCount
- **THEN** Success=false, RetryCount=max, Reason indicates max exceeded

### Requirement: ScrollHandlerConfig provides configurable parameters

The system SHALL provide a `ScrollHandlerConfig` record with all scroll parameters configurable via constructor.

#### Scenario: Default configuration
- **WHEN** creating ScrollHandlerConfig with no arguments
- **THEN** DefaultScrollStep=0.3, MinScrollStep=0.01, MaxScrollStep=0.5, MaxJumpRetryCount=3

#### Scenario: Custom step configuration
- **WHEN** creating config with DefaultScrollStep=0.5
- **THEN** the configuration uses 0.5 as default step

#### Scenario: Adaptive step configuration
- **WHEN** creating config with EnableAdaptiveStep=false
- **THEN** adaptive step calculation is disabled

#### Scenario: Custom retry configuration
- **WHEN** creating config with MaxJumpRetryCount=5
- **THEN** recovery allows up to 5 retries

### Requirement: ScrollContext captures execution context

The system SHALL provide a `ScrollContext` record that combines scroll decision, action type, step percent, and traversal context.

#### Scenario: ScrollContext creation
- **WHEN** creating context for ScrollDown with step 0.3
- **THEN** ActionType=ScrollDown, StepPercent=0.3, Decision contains progress info

### Requirement: ScrollActionResult reports execution outcome

The system SHALL provide a `ScrollActionResult` record that reports action type, success flag, new progress, and description.

#### Scenario: Successful scroll result
- **WHEN** ScrollDown executes successfully from 0.0 to 0.3
- **THEN** Action=ScrollDown, Success=true, NewProgress=0.3, Description describes operation

#### Scenario: Failed scroll result
- **WHEN** ScrollDown fails due to missing ScrollableMockActionExecutor
- **THEN** Action=ScrollDown, Success=false, NewProgress=original, Description indicates error
