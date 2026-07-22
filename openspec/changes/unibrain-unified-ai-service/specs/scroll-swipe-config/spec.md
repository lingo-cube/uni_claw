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
