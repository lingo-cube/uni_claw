## ADDED Requirements

### Requirement: ScrollableMockActionExecutor provides GetScrollUpCount method

`ScrollableMockActionExecutor` SHALL provide a `GetScrollUpCount()` method that returns the count of scroll-up operations in `ScrollHistory`. The method SHALL count `ScrollAction` records where `IsScrollUp` is true.

#### Scenario: GetScrollUpCount returns up-scroll count
- **WHEN** `GetScrollUpCount()` is called after scroll operations
- **THEN** it returns the number of scroll-up actions performed
- **AND** the count is derived from `ScrollHistory.Count(s => s.IsScrollUp)`

#### Scenario: GetScrollUpCount returns zero when no up-scrolls
- **WHEN** `GetScrollUpCount()` is called with no scroll-up history
- **THEN** it returns 0

### Requirement: ScrollableMockVisionService provides GetScrollDistance method

`ScrollableMockVisionService` SHALL provide a `GetScrollDistance()` method that returns the current scroll progress (0.0-1.0) for the current page. The method SHALL delegate to the existing `GetScrollProgress(CurrentPageId)` method.

#### Scenario: GetScrollDistance returns current progress
- **WHEN** `GetScrollDistance()` is called during scrolling
- **THEN** it returns the current scroll progress value
- **AND** the value is between 0.0 and 1.0

#### Scenario: GetScrollDistance returns zero when no scrolling
- **WHEN** `GetScrollDistance()` is called before any scroll operations
- **THEN** it returns 0.0

### Requirement: Collector extracts scroll metrics from mock services

`BaselineReportCollector` SHALL extract scroll metrics from optional mock service parameters when constructing `actualNumeric`. The collector SHALL call `GetScrollCount()`, `GetScrollUpCount()`, and `GetScrollDistance()` on the provided services. When services are null (non-scroll tests), metrics SHALL default to 0.

#### Scenario: Scroll metrics extracted when services provided
- **WHEN** `Collector.Add()` is called with executor and vision parameters
- **THEN** `actualNumeric.ScrollCount` equals `executor.GetScrollCount()`
- **AND** `actualNumeric.ScrollUpCount` equals `executor.GetScrollUpCount()`
- **AND** `actualNumeric.ScrollDistance` equals `vision.GetScrollDistance()`
- **AND** `actualNumeric.FinalProgress` equals `vision.GetScrollProgress(vision.CurrentPageId)`

#### Scenario: Scroll metrics default to zero when services omitted
- **WHEN** `Collector.Add()` is called without executor and vision parameters
- **THEN** `actualNumeric.ScrollCount` is 0
- **AND** `actualNumeric.ScrollUpCount` is 0
- **AND** `actualNumeric.ScrollDistance` is 0.0
- **AND** `actualNumeric.FinalProgress` is 0.0

### Requirement: Advanced scroll metrics return zero in Phase 1

Jump detection, jump recovery, and adaptive step increase metrics SHALL return 0 in the current implementation. These metrics are informational placeholders for future Phase 3 implementation.

#### Scenario: JumpDetected returns zero
- **WHEN** `actualNumeric` is constructed in Phase 1
- **THEN** `JumpDetected` is always 0
- **AND** this is a documented limitation for Phase 1

#### Scenario: JumpRecovered returns zero
- **WHEN** `actualNumeric` is constructed in Phase 1
- **THEN** `JumpRecovered` is always 0
- **AND** this is a documented limitation for Phase 1

#### Scenario: AdaptiveStepIncreases returns zero
- **WHEN** `actualNumeric` is constructed in Phase 1
- **THEN** `AdaptiveStepIncreases` is always 0
- **AND** this is a documented limitation for Phase 1

### Requirement: Scroll metrics are derived from existing data structures

Scroll metric extraction SHALL use existing `ScrollHistory` and `ScrollState` data without adding new state tracking. This follows YAGNI principles by deferring complex state until Phase 3 verification requirements.

#### Scenario: ScrollCount from ScrollHistory
- **WHEN** `GetScrollCount()` is called
- **THEN** it returns the count of all `ScrollAction` records in `ScrollHistory`

#### Scenario: ScrollDistance from ScrollState progress
- **WHEN** `GetScrollDistance()` is called
- **THEN** it returns the current progress value from `ScrollState`
- **AND** no additional distance tracking state is maintained
