## ADDED Requirements

### Requirement: Hook registration via TraversalEngineConfig.Hooks immutable field
TraversalEngineConfig SHALL expose `Hooks: ImmutableArray<ITraversalHook> { get; init; }` with default `ImmutableArray<ITraversalHook>.Empty`. Hooks SHALL be set at engine construction (init-only), not modified during run. `RegisterHook()` method and `List<ITraversalHook> _hooks` field SHALL be deleted from TraversalEngine. TraversalEngine SHALL read hooks from `_config.Hooks` in Initialize(). Empty Hooks (`_hooks.Length == 0`) SHALL enable zero-overhead skip in FireAsync.

#### Scenario: Empty Hooks list causes zero overhead
- **WHEN** TraversalEngineConfig.Hooks is `ImmutableArray<ITraversalHook>.Empty`
- **THEN** `_hooks.Length == 0` causes FireAsync to return immediately without iterating

#### Scenario: Hooks are immutable after construction
- **WHEN** TraversalEngine is constructed with config containing 2 hooks
- **THEN** hooks cannot be added or removed after construction; `RegisterHook()` method does not exist

#### Scenario: Hook registration order determines execution order
- **WHEN** Hooks is `[HookA, HookB]`
- **THEN** FireAsync iterates HookA first, then HookB

### Requirement: OnBeforeRun fires before step loop
OnBeforeRun SHALL fire at RunAsync entry, before the step loop starts. The hook SHALL receive `TraversalPlan` and `ITraversalContext`. OnBeforeRun SHALL fire outside the `try` block so that hook exceptions (caught by FireAsync Log-and-Continue) are not converted to Done(Error) by the engine's catch handler.

#### Scenario: OnBeforeRun fires before first step
- **WHEN** RunAsync is called with a plan and 1 hook
- **THEN** OnBeforeRunAsync fires before the first step iteration; hook receives the TraversalPlan and ITraversalContext

#### Scenario: OnBeforeRun exception does not produce Done(Error)
- **WHEN** a hook throws in OnBeforeRunAsync
- **THEN** FireAsync catches the exception (Log-and-Continue); the engine proceeds to the step loop normally

### Requirement: OnAfterStep fires before termination checks
OnAfterStep SHALL fire after each step's processing (trace recording + page-visit tracking) and BEFORE termination checks (FrameCompleted at root level, AntiLoop, CompletionPolicy). This ensures the terminating step's OnAfterStep is not skipped. The hook SHALL receive `ITraversalContext`.

#### Scenario: OnAfterStep fires for every step including the terminating step
- **WHEN** RunAsync executes 5 steps and the 5th step results in AllVisited termination
- **THEN** OnAfterStepAsync fires 5 times (steps 1-5); the 5th OnAfterStep fires before the termination check returns

#### Scenario: OnAfterStep fires before FrameCompleted termination check
- **WHEN** stepResult.FrameCompleted && NodeStack.Depth <= 1
- **THEN** OnAfterStepAsync fires first, then the engine checks FrameCompleted and returns Done(AllVisited)

### Requirement: OnAfterRun fires at each Done() call site
OnAfterRun SHALL fire at each `return Done(...)` call site in RunAsync, after TraversalResult construction and before return. Done() SHALL remain synchronous; OnAfterRun SHALL be fired via `await FireAsync(h => h.OnAfterRunAsync(result))` at each call site. The hook SHALL receive the completed `TraversalResult`.

#### Scenario: OnAfterRun fires for AllVisited exit
- **WHEN** traversal completes with AllVisited
- **THEN** Done(AllVisited) constructs TraversalResult; OnAfterRunAsync fires with this result; result is returned to caller

#### Scenario: OnAfterRun fires for Error exit
- **WHEN** traversal fails with engine-level exception
- **THEN** Done(Error) constructs TraversalResult; OnAfterRunAsync fires with this result

#### Scenario: OnAfterRun fires for Cancelled exit
- **WHEN** CancellationToken is signaled
- **THEN** Done(Cancelled) constructs TraversalResult; OnAfterRunAsync fires with this result

### Requirement: OnBeforeStep fires after pause-gate and before vision analysis
OnBeforeStep SHALL fire after the pause-gate check (`await _resumeSignal.Task` + `ct.ThrowIfCancellationRequested()`) and before vision analysis (`_vision.AnalyzeCurrentPageAsync`). The hook SHALL receive `ITraversalContext`.

#### Scenario: OnBeforeStep fires for each step iteration
- **WHEN** RunAsync executes 3 steps with 1 hook
- **THEN** OnBeforeStepAsync fires 3 times, each before the corresponding vision analysis call

### Requirement: OnError (fatal) fires in engine catch block
OnError SHALL fire in `RunAsync` `catch(Exception)` block (when `!_config.ThrowOnError`), before `Done(Reasons.Error)`. `IsRecoverable` SHALL be `false` (engine-level fatal — engine terminates). The hook SHALL receive `TraversalErrorContext(ErrorType, Message, NodeId, IsRecoverable=false)` and `ITraversalContext`.

#### Scenario: Fatal error triggers OnError with IsRecoverable=false
- **WHEN** an unhandled exception occurs during step execution
- **THEN** OnErrorAsync fires with TraversalErrorContext where IsRecoverable=false, then Done(Error) returns

### Requirement: OnError (recoverable) fires at engine-level ErrorHandling intercept
OnError (recoverable) SHALL fire in RunAsync step loop when `stepResult.NextState == ErrorHandling && _ctx.LastError != null`. `IsRecoverable` SHALL be `true` (FSM-level — engine continues). This is an engine-level observation of FSM state, NOT an FSM-internal call. TraversalFSM SHALL NOT access hooks.

#### Scenario: Recoverable error triggers OnError with IsRecoverable=true
- **WHEN** StepOrchestrator.ExecuteStepAsync returns stepResult with NextState=ErrorHandling and _ctx.LastError is non-null
- **THEN** OnErrorAsync fires with TraversalErrorContext(ErrorType: exception type name, Message: exception message, NodeId: current frame NodeId, IsRecoverable=true)

#### Scenario: FSM does not access hooks
- **WHEN** TraversalFSM handles a recoverable error internally
- **THEN** TraversalFSM does not call FireAsync or ITraversalHook methods; the engine observes ErrorHandling state externally

### Requirement: FireAsync uses Log-and-Continue with Console.WriteLine
FireAsync SHALL catch hook exceptions and write a warning to console: `Console.WriteLine($"[Hook Warning] {ex.GetType().Name}: {ex.Message}")`. Hook exceptions SHALL NOT propagate to the engine or interrupt traversal. This is consistent with dispatch-table pattern (TraceCoordinator uses same approach). Empty `_hooks.Length == 0` SHALL return immediately.

#### Scenario: Hook exception is caught and logged, engine continues
- **WHEN** a hook throws InvalidOperationException in OnBeforeStepAsync
- **THEN** `Console.WriteLine` outputs "[Hook Warning] InvalidOperationException: ..."; engine proceeds to next hook and next step

#### Scenario: Empty hooks list causes immediate return
- **WHEN** `_hooks.Length == 0`
- **THEN** FireAsync returns `Task.CompletedTask` immediately without iterating
