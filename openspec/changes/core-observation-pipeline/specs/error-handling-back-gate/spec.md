## ADDED Requirements

### Requirement: Same-page item failure limit triggers automatic back navigation
When consecutive errors occur on the same page and exceed a configurable item-failure count, the FSM SHALL press back to escape the page rather than continuing to cycle through remaining items.

#### Scenario: All items on a sub-page fail
- **WHEN** the FSM is at depth > 1 AND 5 distinct items on the current page have each caused errors
- **THEN** `HandleErrorHandlingAsync` SHALL call `PressBackAsync`
- **AND** SHALL transition to `FrameComplete`
- **AND** the per-page error counter SHALL reset

#### Scenario: Items on the home page fail
- **WHEN** the FSM is at depth 1 (home page)
- **THEN** the same-page item failure limit SHALL NOT trigger PressBack
- **AND** the run SHALL continue or terminate via normal completion logic

### Requirement: TraversalAdvisor participates in error recovery strategy selection
The `IUniBrain.Advisor` (TraversalAdvisor) SHALL be consulted during error handling to recommend a recovery strategy.

#### Scenario: Advisor recommends back
- **WHEN** `ErrorClassifier` has produced a classification AND `Advisor.DecideAsync` returns `{ strategy: "back", confidence: >0.7 }`
- **THEN** the strategy selector SHALL prefer the Advisor's recommendation over the default handler strategy

#### Scenario: Advisor is unavailable
- **WHEN** `IUniBrain.Advisor` is null or `DecideAsync` throws
- **THEN** `HandleErrorHandlingAsync` SHALL fall back to the existing `ErrorHandler` pipeline
- **AND** SHALL record a trace decision "advisor_unavailable"

### Requirement: Consecutive error count is not reset by Backtrack strategy
The consecutive error counter SHALL increment on every error regardless of the recovery strategy chosen.

#### Scenario: Multiple Backtrack strategies
- **WHEN** 3 consecutive errors occur with `ErrorStrategy.Backtrack` each time
- **THEN** `ConsecutiveErrors` SHALL be 3
- **AND** the existing PressBack gate (`≥3 && depth>1`) SHALL trigger
