# scroll-swipe-config delta Specification

## MODIFIED Requirements

### Requirement: ScrollSwipeConfig includes MaxEmptyScrollRetries field

`ScrollSwipeConfig` SHALL add a new `int MaxEmptyScrollRetries = 1` field. This field controls how many consecutive empty-scroll-diff observations are required before `IsEndOfList` is confirmed in `InterceptionHandler.TryHandleScrollAsync`. A value of 0 SHALL restore the current behavior (immediate conclusion after one empty diff). The existing 5 fields (`StartX`, `StartY`, `EndX`, `EndY`, `DurationMs`) SHALL remain unchanged.

#### Scenario: Default value preserves current effective behavior

- **WHEN** `ScrollSwipeConfig` is constructed with defaults
- **THEN** `MaxEmptyScrollRetries` is 1, meaning 2 consecutive empty diffs are required (current behavior = 1 confirmation after initial swipe)

#### Scenario: Zero restores immediate conclusion

- **WHEN** `MaxEmptyScrollRetries` is set to 0
- **THEN** `InterceptionHandler.TryHandleScrollAsync` confirms end-of-list after a single empty diff (immediate conclusion)

#### Scenario: Custom N requires N+1 confirmations

- **WHEN** `MaxEmptyScrollRetries` is set to 3
- **THEN** 4 consecutive empty diffs are required before end-of-list is confirmed
