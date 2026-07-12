## ADDED Requirements

### Requirement: ScrollableBaselineTests provides WiFi list fixture with 7 screens and 25 elements

ScrollableBaselineTests.cs SHALL contain a `private static ScrollDataStore WiFiScrollData()` method that constructs a 7-segment WiFi list fixture via ScrollDataStore API. The fixture SHALL contain 25 network elements distributed across 7 screens (progress segments: 0.0, 0.2, 0.4, 0.6, 0.8, 1.0) with overlapping elements (Network3 at segments 0.0/0.2, Network6 at segments 0.4/0.6) to verify element deduplication. The fixture SHALL include a "BackToSettings" button at segment 0.0 for upward scroll testing.

#### Scenario: WiFi list fixture contains all 7 segments
- **WHEN** WiFiScrollData() is called
- **THEN** the resulting ScrollDataStore contains segments at progress values 0.0, 0.2, 0.4, 0.6, 0.8, and 1.0

#### Scenario: WiFi list fixture contains 25 network elements
- **WHEN** WiFiScrollData() is called
- **THEN** each segment contains the expected number of network elements (6 at 0.0, 4 at 0.2, 4 at 0.4, 4 at 0.6, 4 at 0.8, 8 at 1.0)

#### Scenario: WiFi list fixture has overlapping elements for dedup testing
- **WHEN** WiFiScrollData() is called
- **THEN** Network3 appears in segments 0.0 AND 0.2, and Network6 appears in segments 0.4 AND 0.6

#### Scenario: WiFi list fixture has BackToSettings button for upward scroll
- **WHEN** WiFiScrollData() is called
- **THEN** segment 0.0 contains a "BackToSettings" element for upward scroll scenario

### Requirement: WiFiList_ScrollThroughAllScreens_AllNetworksVisited tests full scroll traversal

ScrollableBaselineTests.cs SHALL contain a `[Fact]` test method `WiFiList_ScrollThroughAllScreens_AllNetworksVisited` that uses the WiFi list fixture with `ScrollableMockVisionService` and TraversalEngine with CompletionPolicy=null. The test SHALL verify: (1) `result.Success == true`, (2) `result.CompletionReason == TraversalResult.Reasons.AllVisited`, (3) `result.VisitedElements.Count >= 25` (all networks including dedup), (4) `result.TotalSteps > 0`, (5) ExpectedBehavior JSON `wifi-list-scroll-all-screens.json` validates scrollCount >= 6, scrollDistance >= 0.9, finalProgress >= 0.95.

#### Scenario: Full scroll traversal visits all screens
- **WHEN** WiFiList_ScrollThroughAllScreens_AllNetworksVisited runs with 7-screen WiFi fixture
- **THEN** result.Success is true and CompletionReason is "all_visited"

#### Scenario: All 25 network elements are visited
- **WHEN** WiFiList_ScrollThroughAllScreens_AllNetworksVisited runs
- **THEN** VisitedElements.Count >= 25 (including overlapping Network3/6 counted once)

#### Scenario: Six scroll operations occur
- **WHEN** WiFiList_ScrollThroughAllScreens_AllNetworksVisited runs
- **THEN** ExpectedBehavior validates scrollCount >= 6 (one scroll per screen transition)

#### Scenario: Final progress reaches bottom
- **WHEN** WiFiList_ScrollThroughAllScreens_AllNetworksVisited runs
- **THEN** ExpectedBehavior validates finalProgress >= 0.95 (reached end of list)

### Requirement: WiFiList_ScrollBackToTop_ProgressRevertsCorrectly tests upward scroll

ScrollableBaselineTests.cs SHALL contain a `[Fact]` test method `WiFiList_ScrollBackToTop_ProgressRevertsCorrectly` that uses the WiFi list fixture with a custom traversal that clicks "BackToSettings" after reaching bottom, triggering upward scroll. The test SHALL verify: (1) upward scroll occurs (scrollUpCount >= 1 in ExpectedBehavior), (2) progress reverts correctly (finalProgress < 0.5), (3) BackToSettings element is in VisitedElements.

#### Scenario: Upward scroll triggered by BackToSettings click
- **WHEN** WiFiList_ScrollBackToTop_ProgressRevertsCorrectly runs and clicks BackToSettings
- **THEN** ExpectedBehavior validates scrollUpCount >= 1

#### Scenario: Progress reverts after upward scroll
- **WHEN** WiFiList_ScrollBackToTop_ProgressRevertsCorrectly runs
- **THEN** ExpectedBehavior validates finalProgress < 0.5 (progress reverted towards top)

#### Scenario: BackToSettings element is visited
- **WHEN** WiFiList_ScrollBackToTop_ProgressRevertsCorrectly runs
- **THEN** VisitedElements contains "BackToSettings"

### Requirement: WiFiList_ElementDeduplication_OverlappingElementsVisitedOnce tests dedup logic

ScrollableBaselineTests.cs SHALL contain a `[Fact]` test method `WiFiList_ElementDeduplication_OverlappingElementsVisitedOnce` that uses the WiFi list fixture and verifies that overlapping elements (Network3, Network6) are visited exactly once despite appearing in multiple segments. The test SHALL verify: (1) Network3 appears once in VisitedElements, (2) Network6 appears once in VisitedElements, (3) no duplicate entries for overlapping elements.

#### Scenario: Network3 visited only once despite appearing in 2 segments
- **WHEN** WiFiList_ElementDeduplication_OverlappingElementsVisitedOnce runs
- **THEN** Network3 appears exactly once in VisitedElements (not duplicated from segments 0.0 and 0.2)

#### Scenario: Network6 visited only once despite appearing in 2 segments
- **WHEN** WiFiList_ElementDeduplication_OverlappingElementsVisitedOnce runs
- **THEN** Network6 appears exactly once in VisitedElements (not duplicated from segments 0.4 and 0.6)

### Requirement: WiFiList_BoundaryConditions_TopAndBottomCorrect tests progress boundaries

ScrollableBaselineTests.cs SHALL contain a `[Fact]` test method `WiFiList_BoundaryConditions_TopAndBottomCorrect` that verifies correct handling of progress boundaries (0.0 at top, 1.0 at bottom). The test SHALL verify: (1) initial progress = 0.0 at start, (2) IsEndOfList false until progress >= 0.95, (3) IsEndOfList true at final progress >= 0.95, (4) no scroll-up possible at progress 0.0, (5) no scroll-down possible at progress 1.0.

#### Scenario: Initial progress is 0.0 at top
- **WHEN** WiFiList_BoundaryConditions_TopAndBottomCorrect starts traversal
- **THEN** initial progress equals 0.0

#### Scenario: IsEndOfList becomes true near bottom
- **WHEN** WiFiList_BoundaryConditions_TopAndBottomCorrect runs and reaches final screen
- **THEN** IsEndOfList is true when progress >= 0.95

#### Scenario: No scroll-up possible at progress 0.0
- **WHEN** WiFiList_BoundaryConditions_TopAndBottomCorrect starts at progress 0.0
- **THEN** scroll-up operation is blocked or has no effect

### Requirement: SparseList_JumpRecovery_AllElementsVisited tests jump detection and recovery

ScrollableBaselineTests.cs SHALL contain a `[Fact]` test method `SparseList_JumpRecovery_AllElementsVisited` that uses a sparse fixture with segments at 0.0, 0.4, 0.7, 1.0 (large gaps) to trigger jump detection. The test SHALL verify: (1) jump is detected (jumpDetected >= 1 in ExpectedBehavior), (2) rollback occurs, (3) retry with smaller step succeeds, (4) all 8 elements are visited, (5) jumpRecovered >= 1.

#### Scenario: Jump detection triggers on sparse segments
- **WHEN** SparseList_JumpRecovery_AllElementsVisited runs with 30% default step on sparse fixture
- **THEN** ExpectedBehavior validates jumpDetected >= 1 (gap from 0.0 to 0.4 exceeds 30%)

#### Scenario: Jump recovery succeeds with smaller step
- **WHEN** SparseList_JumpRecovery_AllElementsVisited detects a jump
- **THEN** ExpectedBehavior validates jumpRecovered >= 1 (retry with 15% step succeeds)

#### Scenario: All elements visited despite sparse distribution
- **WHEN** SparseList_JumpRecovery_AllElementsVisited completes
- **THEN** VisitedElements.Count >= 8 (all items from sparse segments)

### Requirement: OverlappingList_AdaptiveStep_StepSizeIncreases tests adaptive step optimization

ScrollableBaselineTests.cs SHALL contain a `[Fact]` test method `OverlappingList_AdaptiveStep_StepSizeIncreases` that uses a high-overlap fixture (70%+ overlap between segments) to trigger adaptive step growth. The test SHALL verify: (1) high overlap triggers step increase (adaptiveStepIncreases >= 1 in ExpectedBehavior), (2) scroll count reduces compared to fixed step, (3) all 15 elements are visited.

#### Scenario: High overlap triggers adaptive step increase
- **WHEN** OverlappingList_AdaptiveStep_StepSizeIncreases runs with 70%+ overlap segments
- **THEN** ExpectedBehavior validates adaptiveStepIncreases >= 1

#### Scenario: Adaptive step reduces scroll operations
- **WHEN** OverlappingList_AdaptiveStep_StepSizeIncreases runs
- **THEN** scrollCount is less than a fixed-step traversal (efficiency gain)

#### Scenario: All elements visited with adaptive step
- **WHEN** OverlappingList_AdaptiveStep_StepSizeIncreases completes
- **THEN** VisitedElements.Count >= 15 (all items from overlapping segments)

### Requirement: ExpectedBehavior numericAnchor supports scroll-specific metrics

ExpectedBehavior JSON schema for scroll scenarios SHALL support scroll-specific metrics in the `numericAnchor` object: `scrollCount` (int, downward scroll count), `scrollDistance` (double, 0.0-1.0 total distance), `scrollUpCount` (int, upward scroll count), `jumpDetected` (int, jump detection count), `jumpRecovered` (int, successful recovery count), `finalProgress` (double, 0.0-1.0 final position), `adaptiveStepIncreases` (int, step growth count). Existing `numericAnchor` fields (`totalSteps`, `visitedPagesCount`, `actionHistoryCount`, `elapsedSecondsMax`) SHALL remain unchanged.

#### Scenario: scrollCount field present in scroll scenario ExpectedBehavior
- **WHEN** ExpectedBehavior JSON is loaded for wifi-list-scroll-all-screens scenario
- **THEN** numericAnchor contains "scrollCount" with integer value >= 6

#### Scenario: jumpDetected field present for sparse scenario
- **WHEN** ExpectedBehavior JSON is loaded for sparse-list-jump-recovery scenario
- **THEN** numericAnchor contains "jumpDetected" with integer value >= 1

#### Scenario: Existing numericAnchor fields remain unchanged
- **WHEN** ExpectedBehavior JSON is loaded for any scroll scenario
- **THEN** numericAnchor contains "totalSteps", "visitedPagesCount", "actionHistoryCount", "elapsedSecondsMax" with their original semantics
