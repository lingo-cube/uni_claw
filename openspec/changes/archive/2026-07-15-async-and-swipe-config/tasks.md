## 1. New Type: ScrollSwipeConfig

- [x] 1.1 Create `src/UniClaw.Core/Traversal/ScrollSwipeConfig.cs` — sealed record class with 5 fields (StartX=0.5, StartY=0.7, EndX=0.5, EndY=0.3, DurationMs=300)

## 2. Config Plumbing

- [x] 2.1 Add `ScrollSwipeConfig ScrollSwipe { get; init; }` to `TraversalEngineConfig` (default `new()`)
- [x] 2.2 Add `ScrollSwipeConfig ScrollSwipe` (15th field, default `null!`) to `StepContext`
- [x] 2.3 Populate `StepContext.ScrollSwipe` from `_config.ScrollSwipe` in `TraversalEngine.Initialize()`
- [x] 2.4 Add `virtual ScrollSwipeConfig? GetScrollSwipeConfig() => null;` to `IVisionProvider` interface

## 3. FSM Async — Handlers

- [x] 3.1 Rename `HandleExecute` → `HandleExecuteAsync`, return `Task<TraversalState>`, replace 2 `.GetAwaiter().GetResult()` with `await OperationDispatcher.DispatchAsync()`
- [x] 3.2 Rename `HandleResultVerify` → `HandleResultVerifyAsync`, return `Task<TraversalState>`, replace 2 `.GetAwaiter().GetResult()` with `await vision.AnalyzeCurrentPageAsync()`
- [x] 3.3 Rename 6 remaining handlers to `*Async` suffix, return `Task<TraversalState>` (no logic change — wrap with `Task.FromResult()` or add `async` keyword)
- [x] 3.4 Rename `DispatchHandler` → `DispatchHandlerAsync`, return `Task<TraversalState>`, switch expression calls all 8 handlers with `await`

## 4. FSM Async — Step

- [x] 4.1 Rename `Step(StepContext?)` → `StepAsync(StepContext?)`, return `Task<TraversalState>`, replace `DispatchHandler(fromState)` with `await DispatchHandlerAsync(fromState)`
- [x] 4.2 Rename `Step()` (parameterless) → `StepAsync()`, delegate to `StepAsync(null)`

## 5. StepOrchestrator Async

- [x] 5.1 Rename `ExecuteStep` → `ExecuteStepAsync`, return `Task<StepResult>`, replace `ctx.StateMachine.Step(ctx)` with `await ctx.StateMachine.StepAsync(ctx)`, replace `ctx.Action.PressBackAsync().GetAwaiter().GetResult()` with `await ctx.Action.PressBackAsync()`
- [x] 5.2 Rename `TryHandleScroll` → `TryHandleScrollAsync`, return `Task<bool>`, replace `.GetAwaiter().GetResult()` on `SwipeAsync` and `AnalyzeCurrentPageAsync` with `await`
- [x] 5.3 Delete 5 hardcoded `const` swipe fields (`ScrollSwipeStartX/Y`, `ScrollSwipeEndX/Y`, `ScrollSwipeDurationMs`)
- [x] 5.4 Add config resolution in `TryHandleScrollAsync`: `var cfg = ctx.Vision.GetScrollSwipeConfig() ?? ctx.ScrollSwipe;` and pass cfg fields to `SwipeAsync`

## 6. TraversalEngine Async

- [x] 6.1 Delete `Run()` synchronous wrapper method
- [x] 6.2 In `RunAsync()`, replace `_orchestrator.ExecuteStep(_stepCtx)` with `await _orchestrator.ExecuteStepAsync(_stepCtx)`
- [x] 6.3 In `TraceCoordinator.LogAndContinue`, change parameter from `Action` to `Func<Task>`, rename to `LogAndContinueAsync`, use `await func()` internally
- [x] 6.4 Convert all 15 `Record*` methods in `TraceCoordinator` to `async Task`, replace `LogAndContinue(() => { _recorder.Record*Async(...).GetAwaiter().GetResult(); })` with `await LogAndContinueAsync(async () => { await _recorder.Record*Async(...); })`
- [x] 6.5 Update all `Record*` call sites in `StepOrchestrator.ExecuteStepAsync` and `TraversalFSM` handlers to `await ctx.Trace.Record*Async(...)`

## 7. ITraceCoordinator Interface

- [x] 7.1 Update `ITraceCoordinator` interface: change all `void` return types to `Task` for the 15 `Record*` methods

## 8. Mock Adapter — ScrollSwipeConfig

- [x] 8.1 Add `Dictionary<string, ScrollSwipeConfig>` field and `GetScrollSwipeConfig(string pageId)` method to `SimulatedScreen`
- [x] 8.2 Add optional `ScrollSwipeConfig? scrollSwipe = null` parameter to `SimulatedScreen.WithScrollablePage()`
- [x] 8.3 Override `GetScrollSwipeConfig()` in `ScrollableMockVisionService` to delegate to `_screen.GetScrollSwipeConfig(_screen.CurrentPageId)`

## 9. Test Updates — Async Signatures

- [x] 9.1 Replace all `engine.Run()` → `await engine.RunAsync()` across all test files
- [x] 9.2 Change test method signatures from `void` → `async Task` where needed
- [x] 9.3 Verify all 669 tests pass (`dotnet test src/UniClaw.Core.sln`)
- [x] 9.4 Verify baseline `NumericAnchor` values unchanged in hierarchy baseline tests

## 10. Test Updates — ScrollSwipeConfig

- [x] 10.1 Add test: default `ScrollSwipeConfig` produces identical behavior to previous hardcoded values
- [x] 10.2 Add test: `SimulatedScreen.WithScrollablePage(scrollSwipe: custom)` stores and retrieves page-level config
- [x] 10.3 Add test: `TryHandleScrollAsync` uses page-level config when available, falls back to engine default when null

## 11. Build & Verify

- [x] 11.1 Build: `dotnet build src/UniClaw.Core.sln` — zero errors
- [x] 11.2 Test: `dotnet test src/UniClaw.Core.sln` — all 669 tests pass
- [x] 11.3 Verify no `.GetAwaiter().GetResult()` remains in `src/UniClaw.Core/StateMachine/`, `src/UniClaw.Core/Traversal/` (excluding `TraceCoordinator.LogAndContinueAsync` internal wrapper which no longer calls it)
- [x] 11.4 Run ArchitectureGuardTests — all 44 pass (no new enum values, no dependency violations)
