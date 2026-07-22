## MODIFIED Requirements

### Requirement: TraversalEngine is a sealed class implementing IGraphTraversalEngine as unified entry point

TraversalEngine SHALL accept IUniBrain (replacing IVisionProvider) in its constructor/Initialize parameters. StepContext SHALL carry `IUniBrain Brain` and `IScreenStateProvider ScreenState` properties instead of `IVisionProvider Vision`.

Consumer code migration:
- `ctx.Vision.AnalyzeCurrentPageAsync()` → `ctx.Brain.PageAnalyzer.AnalyzeCurrentPageAsync()`
- `ctx.Vision.HasScroll()` → `ctx.ScreenState.HasScroll()`
- `ctx.Vision.GetScrollProgress()` → `ctx.ScreenState.GetScrollProgress()`
- `ctx.Vision.IsEndOfList()` → `ctx.ScreenState.IsEndOfList()`
- `ctx.Vision.FindAppEntryAsync(app)` → `ctx.Brain.PageAnalyzer.FindAppEntryAsync(app)`
- `ctx.Vision.GetScrollSwipeConfig()` → `ctx.ScreenState.GetScrollSwipeConfig()`

#### Scenario: TraversalEngine initializes with IUniBrain + IScreenStateProvider
- **WHEN** TraversalEngine.Initialize is called
- **THEN** StepContext.Brain is populated with IUniBrain instance
- **THEN** StepContext.ScreenState is populated with IScreenStateProvider instance
- **THEN** StepContext.Vision property no longer exists

#### Scenario: Call site uses ctx.Brain.PageAnalyzer instead of ctx.Vision
- **WHEN** handler code needs page analysis
- **THEN** calls ctx.Brain.PageAnalyzer.AnalyzeCurrentPageAsync() (not ctx.Vision)

#### Scenario: Call site uses ctx.ScreenState instead of ctx.Vision for scroll
- **WHEN** handler code needs scroll state
- **THEN** calls ctx.ScreenState.HasScroll() (not ctx.Vision.HasScroll())
