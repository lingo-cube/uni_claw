# Capability: Traversal Engine — Delta

## MODIFIED Requirements

### Requirement: TraversalEngine.RunAsync executes step loop with async orchestrator

RunAsync() SHALL implement the core traversal loop: for each step up to MaxSteps, check CancellationToken, apply DelayPerStepMs if configured, call `await StepOrchestrator.ExecuteStepAsync()`, handle leaf-pop, handle child-push→NodeSelect transition, record TraceRecord if TraceEnabled, track visited pages, and check termination conditions. RunAsync() SHALL await all async operations without `.GetAwaiter().GetResult()`.

#### Scenario: RunAsync calls ExecuteStepAsync with await
- **WHEN** `RunAsync()` executes a step iteration
- **THEN** `await _orchestrator.ExecuteStepAsync(_stepCtx)` is called
- **AND** no `.GetAwaiter().GetResult()` is present in the step loop body

#### Scenario: Trace records are recorded asynchronously
- **WHEN** `RunAsync()` records trace events (page visits, state decisions, etc.)
- **THEN** trace coordinator methods are awaited

#### Scenario: RunAsync passes ScrollSwipe to StepContext
- **WHEN** `RunAsync()` constructs `StepContext`
- **THEN** `ScrollSwipe` is set to `_config.ScrollSwipe`

## REMOVED Requirements

### Requirement: TraversalEngine.Run provides synchronous convenience wrapper

**Reason**: The synchronous `Run()` wrapper with `.GetAwaiter().GetResult()` is a deadlock risk for any environment with a `SynchronizationContext` (ASP.NET, WinForms, WPF). With the full async pipeline, all callers SHALL use `await RunAsync()` directly.

**Migration**: Replace all `engine.Run()` calls with `await engine.RunAsync()`. xUnit test methods change from `void` to `async Task`. The `IGraphTraversalEngine` interface already exposes `RunAsync()` — no interface change needed.

## ADDED Requirements

### Requirement: TraceCoordinator LogAndContinue supports async operations

`TraceCoordinator.LogAndContinue` SHALL accept `Func<Task>` instead of `Action`. All 15 `Record*` methods SHALL be changed to `async Task` and use `await LogAndContinueAsync(async () => { await _recorder.Record*Async(...); })`. Trace write failures SHALL be caught and logged but MUST NOT interrupt traversal.

#### Scenario: LogAndContinueAsync awaits the async function
- **WHEN** `LogAndContinueAsync` is called with an async function and `Active` is true
- **THEN** the async function is awaited
- **AND** no `.GetAwaiter().GetResult()` is used internally

#### Scenario: LogAndContinueAsync is no-op when Active is false
- **WHEN** `LogAndContinueAsync` is called with `Active` false
- **THEN** the async function is NOT invoked and the method returns immediately

#### Scenario: Async trace failure triggers Log-and-Continue
- **WHEN** an async trace write method throws during execution
- **THEN** the exception is caught, a warning is logged, and the traversal step continues
