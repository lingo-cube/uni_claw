## Requirements

### Requirement: BaselineReportCollector shall collect scroll metrics from ScrollHistory

The system SHALL calculate scroll metrics from `ScrollableMockActionExecutor.ScrollHistory` and `ScrollableMockVisionService` state when collecting baseline test results.

The following metrics SHALL be calculated:
- **ScrollCount**: Number of `ScrollDown` actions in `ScrollHistory`
- **ScrollUpCount**: Number of `ScrollUp` actions in `ScrollHistory`
- **ScrollDistance**: Difference between last scroll's `AfterProgress` and first scroll's `BeforeProgress`
- **FinalProgress**: Current scroll progress from `ScrollableMockVisionService.GetScrollProgress(CurrentPageId)`

When `executor` or `vision` is null, the system SHALL return 0 for all scroll metrics.

#### Scenario: Calculate scroll metrics from ScrollHistory
- **WHEN** `BaselineReportCollector.BuildActualNumeric` is called with `executor` and `vision`
- **THEN** `ScrollCount` equals count of `ScrollActionType.ScrollDown` in `ScrollHistory`
- **AND** `ScrollUpCount` equals count of `ScrollActionType.ScrollUp` in `ScrollHistory`
- **AND** `FinalProgress` equals `vision.GetScrollProgress(vision.CurrentPageId)`

#### Scenario: Calculate scroll distance
- **WHEN** `ScrollHistory` contains one or more scroll actions
- **THEN** `ScrollDistance` equals `lastScroll.AfterProgress - firstScroll.BeforeProgress`

#### Scenario: Handle null executor or vision
- **WHEN** `executor` is null or `vision` is null
- **THEN** all scroll metrics equal 0

### Requirement: JSON expected files shall contain correct numeric anchor values

The system SHALL update all 6 scroll scenario JSON files with correct `numericAnchor` values for scroll metrics.

The following files SHALL be updated:
- `wifi-list-scroll-all-screens.json`
- `wifi-list-scroll-back-to-top.json`
- `wifi-list-element-deduplication.json`
- `wifi-list-boundary-conditions.json`
- `sparse-list-jump-recovery.json`
- `overlapping-list-adaptive-step.json`

#### Scenario: Update scroll metric expected values
- **WHEN** JSON expected file is updated
- **THEN** `numericAnchor.scrollCount` reflects expected scroll count for the scenario
- **AND** `numericAnchor.finalProgress` reflects expected final progress (1.0 for bottom-reached scenarios)
- **AND** `numericAnchor.scrollUpCount` reflects expected scroll up count (≥1 for scroll-back scenarios)

### Requirement: JumpDetected and AdaptiveStepIncreases shall be placeholder values

The system SHALL set `JumpDetected`, `JumpRecovered`, and `AdaptiveStepIncreases` to 0 in this phase.

These metrics SHALL be marked as Phase 3 Future Work, requiring integration with `ScrollHandler.Statistics` and `AdaptiveStepCalculator`.

#### Scenario: Placeholder values for advanced metrics
- **WHEN** `BuildActualNumeric` constructs `NumericAnchor`
- **THEN** `JumpDetected` equals 0
- **AND** `JumpRecovered` equals 0
- **AND** `AdaptiveStepIncreases` equals 0

### Requirement: Post-implementation verification

After implementation, the system SHALL run baseline tests and verify generated reports contain correct scroll metrics.

#### Scenario: Verify scroll metrics in generated reports
- **WHEN** baseline tests are executed
- **THEN** generated report's `actualNumeric.scrollCount` matches expected value
- **AND** generated report's `actualNumeric.finalProgress` matches expected value (1.0 for bottom-reached scenarios)
- **AND** report's `allPassed` equals true

#### Scenario: Verify scroll-up scenario
- **WHEN** `wifi-list-scroll-back-to-top` scenario is executed
- **THEN** report's `actualNumeric.scrollUpCount` is greater than 0

#### Scenario: Verify scroll-through-all-screens scenario
- **WHEN** `wifi-list-scroll-all-screens` scenario is executed
- **THEN** report's `actualNumeric.scrollCount` is greater than or equal to 5
- **AND** report's `actualNumeric.finalProgress` equals 1.0
