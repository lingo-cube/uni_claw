# Capability: Step Orchestrator — Delta

## MODIFIED Requirements

### Requirement: StepContext is a sealed record class encapsulating step dependencies

`StepContext` SHALL be a `sealed record class` that bundles all dependencies required for a single FSM step execution. It SHALL contain 15 fields: `context` (TraversalRuntimeContext), `state_machine` (TraversalFSM), `vision` (IVisionProvider), `action` (IActionExecutor), `child_mgr` (IDynamicChildManager), `node_registry` (INodeRegistry), `trace` (ITraceCoordinator), `snapshot_mgr` (IPageSnapshotManager), `stack` (INodeStackAdapter), `error_handler` (ErrorHandler?), `popup_handler` (PopupHandler?), `last_known_path` (string?), `last_recorded_path` (string?), `last_recorded_action` (string?), and `scroll_swipe` (ScrollSwipeConfig). `StepContext` SHALL be constructed once per step and SHALL NOT be mutated after construction (record immutability).

#### Scenario: StepContext contains all 15 dependency fields
- **WHEN** `StepContext` is inspected for field declarations
- **THEN** it contains exactly: `context`, `state_machine`, `vision`, `action`, `child_mgr`, `node_registry`, `trace`, `snapshot_mgr`, `stack`, `error_handler`, `popup_handler`, `last_known_path`, `last_recorded_path`, `last_recorded_action`, `scroll_swipe`

#### Scenario: StepContext is sealed record class
- **WHEN** the type declaration of `StepContext` is inspected
- **THEN** it is `sealed record class` (not mutable class)

#### Scenario: StepContext is immutable after construction
- **WHEN** a `StepContext` instance is created and an attempt is made to reassign one of its fields
- **THEN** the compiler rejects the assignment (record fields are init-only)

### Requirement: StepOrchestrator executes_step via 14-step interception layer wrapping TraversalFSM

`StepOrchestrator` SHALL be a sealed class that wraps `TraversalFSM.StepAsync()` with a 14-step interception layer. The `ExecuteStepAsync(ctx)` method SHALL execute steps 1 through 14 in strict sequential order, using `await` for async operations. No step SHALL be skipped unless its precondition is explicitly not met. The orchestrator SHALL NOT short-circuit the FSM transition; steps 8-10 are interception overlays on top of the FSM result. StepOrchestrator.ExecuteStepAsync() is invoked by `TraversalEngine.RunAsync()` per step iteration.

#### Scenario: Step 3 calls state_machine.StepAsync and captures transition result
- **WHEN** step 3 executes
- **THEN** `await ctx.state_machine.StepAsync(ctx)` is invoked and the transition result is captured for subsequent interception steps
- **AND** no `.GetAwaiter().GetResult()` is present in the ExecuteStepAsync method

#### Scenario: Step 8 BRANCH interception calls TryHandleScrollAsync when needed
- **WHEN** step 8 BRANCH interception reaches the scroll decision point
- **THEN** `await TryHandleScrollAsync(ctx, currentFrame, ...)` is called

#### Scenario: Step 9 NODE_SELECT calls PressBackAsync with await
- **WHEN** step 9 triggers back navigation (DYNAMIC_MATCH exhausted, depth > 1)
- **THEN** `await ctx.Action.PressBackAsync()` is called
- **AND** no `.GetAwaiter().GetResult()` is present

#### Scenario: ExecuteStepAsync returns Task<StepResult>
- **WHEN** `ExecuteStepAsync` is invoked
- **THEN** it returns `Task<StepResult>` containing the 6 outcome fields

### Requirement: TryHandleScroll executes scroll as async operation+judgment

`TryHandleScrollAsync` SHALL be an `internal static async Task<bool>` method. It SHALL NOT use `.GetAwaiter().GetResult()`. Instead:
1. Check `ctx.Vision.HasScroll()` and `ctx.Vision.IsEndOfList()` (sync, no change)
2. Resolve swipe config via `ctx.Vision.GetScrollSwipeConfig() ?? ctx.ScrollSwipe`
3. Execute swipe: `await ctx.Action.SwipeAsync(cfg.StartX, cfg.StartY, cfg.EndX, cfg.EndY, cfg.DurationMs)`
4. Re-analyze: `var after = await ctx.Vision.AnalyzeCurrentPageAsync()`
5. Judge: seen-set diff to determine if new elements were revealed

#### Scenario: TryHandleScrollAsync awaits SwipeAsync
- **WHEN** `TryHandleScrollAsync` executes the scroll operation
- **THEN** `await ctx.Action.SwipeAsync(...)` is called with coordinates from the resolved config
- **AND** no `.GetAwaiter().GetResult()` is present

#### Scenario: TryHandleScrollAsync awaits AnalyzeCurrentPageAsync
- **WHEN** `TryHandleScrollAsync` re-analyzes the page after swipe
- **THEN** `await ctx.Vision.AnalyzeCurrentPageAsync()` is called

#### Scenario: TryHandleScrollAsync uses config coordinates not consts
- **WHEN** `TryHandleScrollAsync` resolves swipe coordinates
- **THEN** the source is `ScrollSwipeConfig`, not hardcoded `const` fields

## REMOVED Requirements

### Requirement: Hardcoded swipe coordinate constants

**Reason**: Replaced by `ScrollSwipeConfig` — configurable engine-level default with page-level override via `IVisionProvider.GetScrollSwipeConfig()`.

**Migration**: `const ScrollSwipeStartX/Y`, `ScrollSwipeEndX/Y`, `ScrollSwipeDurationMs` are deleted. All coordinate references use `ctx.Vision.GetScrollSwipeConfig() ?? ctx.ScrollSwipe`. Default `ScrollSwipeConfig()` produces identical values — zero behavioral change for existing tests.
