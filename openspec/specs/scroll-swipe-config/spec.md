## MODIFIED Requirements

### Requirement: IVisionProvider exposes GetScrollSwipeConfig as page-level override

GetScrollSwipeConfig SHALL move from IVisionProvider to IScreenStateProvider. IVisionProvider SHALL NOT contain GetScrollSwipeConfig method after migration. IScreenStateProvider.GetScrollSwipeConfig() SHALL return `ScrollSwipeConfig?`, default null (use engine default). Implementations MAY override for page-specific config.

#### Scenario: IScreenStateProvider.GetScrollSwipeConfig returns page-level override
- **WHEN** ctx.ScreenState.GetScrollSwipeConfig() is called
- **THEN** returns page-specific ScrollSwipeConfig or null (fallback to engine default)

### Requirement: TryHandleScroll merges engine and page-level config

TryHandleScroll SHALL resolve scroll coordinates by calling `ctx.ScreenState.GetScrollSwipeConfig()` first, falling back to `ctx.ScrollSwipe` when null. This replaces the previous `ctx.Vision.GetScrollSwipeConfig()` call.

#### Scenario: TryHandleScroll resolves config from IScreenStateProvider
- **WHEN** TryHandleScroll executes
- **THEN** calls ctx.ScreenState.GetScrollSwipeConfig() for page-level config
- **THEN** falls back to ctx.ScrollSwipe (engine-level default) when null is returned


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
