## MODIFIED Requirements

### Requirement: BaselineReportCollector shall collect scroll metrics from ActionHistory

The system SHALL calculate scroll metrics from `IActionExecutor.GetHistory()` (the ActionHistory of swipe actions) and `ScrollableMockVisionService`/`SimulatedScreen` viewport state when collecting baseline test results — NOT from `ScrollableMockActionExecutor.ScrollHistory` (removed).

The following metrics SHALL be calculated:
- **ScrollCount**: number of downward swipe actions recorded in ActionHistory
- **ScrollUpCount**: number of upward swipe actions recorded in ActionHistory
- **ScrollDistance**: difference between last swipe's after-progress and first swipe's before-progress (mock viewport; N/A for real services)
- **FinalProgress**: current viewport progress from the vision/screen service

When `executor` or `vision` is null, the system SHALL return 0 for all scroll metrics. `JumpDetected`/`JumpRecovered`/`AdaptiveStepIncreases` SHALL NOT be collected (removed from schema — see expected-behavior delta).

#### Scenario: Calculate scroll metrics from ActionHistory
- **WHEN** `BaselineReportCollector.BuildActualNumeric` is called with `executor` and `vision`
- **THEN** `ScrollCount` equals the count of downward swipe ActionRecords
- **AND** `ScrollUpCount` equals the count of upward swipe ActionRecords
- **AND** `FinalProgress` equals the current viewport progress

#### Scenario: Calculate scroll distance from viewport progress
- **WHEN** ActionHistory contains one or more swipe actions
- **THEN** `ScrollDistance` equals last-swipe after-progress minus first-swipe before-progress

#### Scenario: Handle null executor or vision
- **WHEN** `executor` is null or `vision` is null
- **THEN** all scroll metrics equal 0

### Requirement: JSON expected files shall contain correct numeric anchor values

The system SHALL update the scroll scenario JSON expected files with correct `numericAnchor` values recalibrated against `PagedItemGenerator`-configured scenarios. Expected files SHALL NOT contain `jumpDetected`/`jumpRecovered`/`adaptiveStepIncreases` keys (removed). Scenario fixtures SHALL be expressed as `PagedItemGenerator` parameters (totalCount, pageSize, fillRatio) + `ScrollBehaviorProfile`, not pre-built `ScrollDataStore` segments.

#### Scenario: Update scroll metric expected values
- **WHEN** a JSON expected file is updated
- **THEN** `numericAnchor.scrollCount` reflects expected downward swipe count for the scenario
- **AND** `numericAnchor.finalProgress` reflects expected final progress (1.0 for bottom-reached scenarios)
- **AND** `numericAnchor.scrollUpCount` reflects expected upward swipe count (≥1 for scroll-back scenarios)
- **AND** no `jumpDetected`/`jumpRecovered`/`adaptiveStepIncreases` key is present

### Requirement: Post-implementation verification

After implementation, the system SHALL run baseline tests and verify generated reports contain correct scroll metrics derived from ActionHistory.

#### Scenario: Verify scroll metrics in generated reports
- **WHEN** baseline tests are executed
- **THEN** generated report's `actualNumeric.scrollCount` matches expected value
- **AND** generated report's `actualNumeric.finalProgress` matches expected value (1.0 for bottom-reached scenarios)
- **AND** report's `allPassed` equals true

#### Scenario: Verify scroll-up scenario
- **WHEN** a scroll-back-to-top scenario is executed
- **THEN** report's `actualNumeric.scrollUpCount` is greater than 0

#### Scenario: Verify scroll-through-all-screens scenario
- **WHEN** a scroll-all-screens scenario is executed
- **THEN** report's `actualNumeric.scrollCount` is greater than or equal to the expected page count
- **AND** report's `actualNumeric.finalProgress` equals 1.0

## REMOVED Requirements

### Requirement: JumpDetected and AdaptiveStepIncreases shall be placeholder values

**Reason**: The `JumpDetected`/`JumpRecovered`/`AdaptiveStepIncreases` fields are removed from the `NumericAnchor` schema (C-11 constitution-level change) because the `ScrollHandler`/`JumpDetector`/`JumpRecoveryHandler`/`AdaptiveStepCalculator` pipeline that would have populated them is deleted; there is no data source. Holding them as eternal placeholders contradicts the refactor's removal of jump detection as an engine concept.
**Migration**: Baseline JSON files and any `BuildActualNumeric` code referencing these three keys are removed; verification no longer emits `numeric_anchor:jump_*` RuleResults.
