## ADDED Requirements

### Requirement: Verifier accepts ROI-path end-of-list trace decisions
The `ScenarioCompletionVerifier` SHALL accept `scroll_roi_end_reached` and `scroll_roi_content_guard` as valid end-of-list proof in addition to the legacy `scroll_no_new_elements_end_reached`. The `traceEndProof` check SHALL match any of these three trace decision action strings.

#### Scenario: ROI end reached is accepted as end proof
- **WHEN** the trace contains a record with `Action == "scroll_roi_end_reached"` and `RequireEndOfList` is true
- **THEN** `endProven` is true

#### Scenario: ROI content guard is accepted as end proof
- **WHEN** the trace contains a record with `Action == "scroll_roi_content_guard"` and `RequireEndOfList` is true
- **THEN** `endProven` is true

#### Scenario: Legacy seen-set end is still accepted
- **WHEN** the trace contains a record with `Action == "scroll_no_new_elements_end_reached"` and `RequireEndOfList` is true
- **THEN** `endProven` is true (backward compatible)

#### Scenario: No end proof when end is not required
- **WHEN** `RequireEndOfList` is false
- **THEN** `endProven` is true regardless of trace decisions

#### Scenario: No end proof when none of the three signals exist
- **WHEN** the trace contains none of `scroll_roi_end_reached`, `scroll_roi_content_guard`, or `scroll_no_new_elements_end_reached`
- **THEN** `endProven` is false (falls through to `screenEndOfList` check as before)

### Requirement: ScenarioCompletionVerifierTests covers ROI end signals
Unit tests SHALL verify that `traceEndProof` accepts `scroll_roi_end_reached` and `scroll_roi_content_guard` in addition to the existing `scroll_no_new_elements_end_reached` test.

#### Scenario: Test with scroll_roi_end_reached
- **WHEN** a scenario verification test is run with a trace containing `scroll_roi_end_reached`
- **THEN** the test passes and end is proven

#### Scenario: Test with scroll_roi_content_guard
- **WHEN** a scenario verification test is run with a trace containing `scroll_roi_content_guard`
- **THEN** the test passes and end is proven
