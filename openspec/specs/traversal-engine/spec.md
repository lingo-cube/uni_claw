## Requirements

### Requirement: TraversalEngine is a sealed class implementing IGraphTraversalEngine as unified entry point
TraversalEngine SHALL be a `sealed class` (not record) implementing `UniClaw.Core.Traversal.IGraphTraversalEngine`. It SHALL have a constructor accepting `TraversalPlan plan`, `IVisionProvider vision`, `IActionExecutor action`, `TraversalEngineConfig? config = null`, `ITraceRecorder? traceRecorder = null`. The constructor SHALL call `Initialize()` which compiles the Plan, creates internal components (TraversalRuntimeContext, TraversalFSM, StepContext, StepOrchestrator), and sets `ctx.GlobalState = Traversing`. If initialization fails, the constructor SHALL throw (fail-fast pattern, not Log-and-Continue). TraversalEngine SHALL NOT depend on Simulation.ExpectedBehavior — verification is a test-layer concern, not an engine concern.

#### Scenario: Constructor succeeds with valid plan
- **WHEN** TraversalEngine is constructed with a valid TraversalPlan, IVisionProvider, and IActionExecutor
- **THEN** Initialize() compiles the Plan, creates all internal components, sets GlobalState to Traversing, and the engine is ready for RunAsync()

#### Scenario: Constructor fails with invalid plan
- **WHEN** TraversalEngine is constructed with a TraversalPlan that produces no root node and StaticNodes is empty
- **THEN** Initialize() SHALL throw an exception (fail-fast), and no engine instance is created

#### Scenario: Constructor uses default config and traceRecorder
- **WHEN** TraversalEngine is constructed without config and traceRecorder parameters
- **THEN** config defaults to `new TraversalEngineConfig()` (MaxSteps=1000, TraceEnabled=true) and traceRecorder defaults to null (no-op trace)

### Requirement: TraversalEngine.Initialize compiles Plan into node tree and internal components
Initialize() SHALL perform 7 steps in order: (1) create TraversalRuntimeContext with trace ID and max depth from config, set GlobalState=Initializing; (2) call CompilePlan() producing (rootNode, registry); (3) push root node onto NodeStack, set CurrentFrame=rootNode; (4) create TraversalFSM with context; (5) assemble StepContext with all 8 dependencies; (6) create StepOrchestrator; (7) set GlobalState=Traversing.

#### Scenario: Initialize creates full internal component chain
- **WHEN** Initialize() runs with a plan containing RootNode and StaticNodes
- **THEN** TraversalRuntimeContext, TraversalFSM, StepContext (with DynamicChildManager, NodeRegistry, TraceCoordinator, PageSnapshotManager, NodeStackAdapter), and StepOrchestrator are all created and wired together

#### Scenario: Initialize sets correct lifecycle state progression
- **WHEN** Initialize() begins execution
- **THEN** GlobalState transitions from Initializing (step 1) to Traversing (step 7)

### Requirement: TraversalEngine.CompilePlan produces root node and node registry from TraversalPlan
CompilePlan() SHALL create a `DictionaryNodeRegistry`, register all StaticNodes from the plan, determine the root node (preferring plan.RootNode, falling back to BuildDefaultRoot from plan.EntryApp), and ensure the root node is registered even if not in StaticNodes.

#### Scenario: Plan has RootNode and StaticNodes
- **WHEN** CompilePlan() processes a TraversalPlan with both RootNode and StaticNodes
- **THEN** all StaticNodes are registered in DictionaryNodeRegistry, RootNode is used as root, and RootNode is registered if not already present

#### Scenario: Plan has no RootNode, only EntryApp
- **WHEN** CompilePlan() processes a TraversalPlan with null RootNode and EntryApp "settings.app"
- **THEN** BuildDefaultRoot("settings.app") creates a minimal Container root node with NodeId "settings.app_root", NodeType Container, Operation NoAction, and StaticChildren from StaticNodes.Keys

### Requirement: TraversalEngine.RunAsync executes step loop with async orchestrator

RunAsync() SHALL implement the core traversal loop: for each step up to MaxSteps, check CancellationToken, apply DelayPerStepMs if configured, call `await StepOrchestrator.ExecuteStepAsync()`, handle leaf-pop, handle child-push→NodeSelect transition, record TraceRecord if TraceEnabled, track visited pages, and check termination conditions. RunAsync() SHALL await all async operations without `.GetAwaiter().GetResult()`. RunAsync() SHALL never throw exceptions to callers — all exceptions SHALL be caught and returned as TraversalResult with Reasons.Error.

The engine SHALL use `Exhaustive` (formerly `None`) as the completion policy type check for exhaustive traversal. The engine SHALL derive `effective_depth = min(config.MaxDepth, plan.IntentSlots.Depth ?? int.MaxValue)` and pass it to `CompletionContext.MaxDepth` for ContainerHandler consumption.

#### Scenario: Successful traversal completes all nodes
- **WHEN** RunAsync() runs and StepOrchestrator.ExecuteStep() returns FrameCompleted with NodeStack.Depth≤1
- **THEN** RunAsync() returns TraversalResult with Success=true, CompletionReason="all_visited", GlobalState=Completed

#### Scenario: Anti-loop triggered
- **WHEN** RunAsync() runs and StepOrchestrator.ExecuteStep() returns AntiLoopTriggered=true
- **THEN** RunAsync() returns TraversalResult with Success=true, CompletionReason="anti_loop", GlobalState=Completed

#### Scenario: CompletionPolicy TargetFound triggered
- **WHEN** RunAsync() runs and CompletionPolicy TargetFound check matches the current node
- **THEN** RunAsync() returns TraversalResult with Success=true, CompletionReason="target_found", GlobalState=Completed

#### Scenario: CompletionPolicy Timeout triggered
- **WHEN** RunAsync() runs and elapsed time exceeds CompletionPolicy.TimeoutSeconds
- **THEN** RunAsync() returns TraversalResult with Success=false, CompletionReason="timeout", GlobalState=Terminated

#### Scenario: CompletionPolicy MaxSteps triggered
- **WHEN** RunAsync() runs and step count reaches CompletionPolicy.MaxSteps before engine hard limit
- **THEN** RunAsync() returns TraversalResult with CompletionReason="max_steps", TotalSteps <= CompletionPolicy.MaxSteps

#### Scenario: Max steps exceeded (engine hard limit)
- **WHEN** RunAsync() reaches config.MaxSteps without completion
- **THEN** RunAsync() returns TraversalResult with Success=false, CompletionReason="max_steps"

#### Scenario: Exception during step execution
- **WHEN** an exception occurs during StepOrchestrator.ExecuteStep()
- **THEN** RunAsync() catches the exception, sets ctx.GlobalState=Error, returns TraversalResult with Success=false, CompletionReason="error", Error=caught exception

#### Scenario: CancellationToken triggered
- **WHEN** CancellationToken is signaled during the loop
- **THEN** RunAsync() catches OperationCanceledException, returns TraversalResult with CompletionReason="cancelled", GlobalState=Terminated

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

#### Scenario: Exhaustive policy check uses renamed enum value
- **WHEN** RunAsync() checks completion policy type for exhaustive traversal
- **THEN** the condition SHALL be `policy.Type != CompletionPolicyType.Exhaustive` (formerly `None`)

#### Scenario: Depth flows from IntentSlots via priority min
- **WHEN** RunAsync() constructs CompletionContext for ContainerHandler
- **THEN** `CompletionContext.MaxDepth` SHALL be `min(config.MaxDepth, plan.IntentSlots.Depth ?? int.MaxValue)`
- **AND** when `IntentSlots.Depth` is null, MaxDepth is governed solely by `config.MaxDepth`

<!-- Requirement removed: TraversalEngine.Run synchronous convenience wrapper deleted.
     Reason: The synchronous Run() wrapper with .GetAwaiter().GetResult() is a deadlock risk
     for any environment with a SynchronizationContext. With the full async pipeline, all
     callers SHALL use await RunAsync() directly. xUnit test methods change from void to
     async Task. IGraphTraversalEngine already exposes RunAsync() — no interface change needed.
-->

### Requirement: TraversalEngine implements IGraphTraversalEngine lifecycle methods
TraversalEngine SHALL implement InitializeAsync() as Task.CompletedTask (constructor already initialized), GetStateAsync() returning ctx.GlobalState, and StopAsync() using two-step termination (Traversing→Paused→Terminated).

#### Scenario: InitializeAsync called after construction
- **WHEN** InitializeAsync() is called on a fully constructed TraversalEngine
- **THEN** it returns Task.CompletedTask immediately (no-op, constructor already initialized)

#### Scenario: StopAsync called during traversal
- **WHEN** StopAsync() is called while engine is Traversing
- **THEN** ctx.GlobalState first transitions to Paused("stopping"), then to Terminated("user_stop")
- **AND** this two-step path is required because Traversing→Terminated has no direct edge in the GlobalFSM transition matrix

### Requirement: PauseAsync/ResumeAsync with TaskCompletionSource gate (Phase 3/4)
PauseAsync SHALL suspend the RunAsync step loop using a TaskCompletionSource gate pattern, and ResumeAsync SHALL release the gate to continue the step loop. Both SHALL validate preconditions and fire B1 lifecycle hooks.

#### Scenario: PauseAsync preconditions
- **WHEN** PauseAsync() is called and GlobalState != Traversing
- **THEN** it SHALL throw DomainValidationException("GlobalState", "Cannot pause when not Traversing")

#### Scenario: PauseAsync suspends step loop
- **WHEN** PauseAsync() is called during Traversing
- **THEN** it SHALL create a new uncompleted TaskCompletionSource (close gate)
- **AND** set GlobalState to Paused via GlobalFSM
- **AND** fire `OnPauseAsync` B1 lifecycle hook
- **AND** the step loop SHALL block at the next iteration's pause check (`await _resumeSignal.Task`)

#### Scenario: ResumeAsync preconditions
- **WHEN** ResumeAsync() is called and GlobalState != Paused
- **THEN** it SHALL throw DomainValidationException("GlobalState", "Cannot resume when not Paused")

#### Scenario: ResumeAsync restores step loop
- **WHEN** ResumeAsync() is called during Paused
- **THEN** it SHALL set GlobalState to Traversing via GlobalFSM
- **AND** fire `OnResumeAsync` B1 lifecycle hook (while gate is still closed)
- **AND** call TrySetResult on the current TaskCompletionSource AFTER all hooks complete (open gate)
- **AND** the step loop SHALL unblock and continue with the next step

#### Scenario: RunAsync step loop pause check
- **WHEN** each step iteration begins in RunAsync
- **THEN** before executing the step, the loop SHALL `await _resumeSignal.Task`
- **AND** SHALL check CancellationToken after resume (`ct.ThrowIfCancellationRequested()`)

#### Scenario: PauseAsync mid-step (graceful)
- **WHEN** PauseAsync is called while a step is executing (step passed the pause check)
- **THEN** the current step SHALL complete normally
- **AND** the pause SHALL take effect at the next iteration's pause check

#### Scenario: _resumeSignal field is volatile
- The `_resumeSignal` TaskCompletionSource field SHALL be declared `volatile` to prevent JIT register caching across threads (written by external PauseAsync caller, read by RunAsync step loop)

### Requirement: TraversalEngine.Done helper produces TraversalResult with correct GlobalState mapping
Done() SHALL map CompletionReason to GlobalState: AllVisited/AntiLoop/TargetFound → Completed, Cancelled/Timeout → Terminated, Error → Error. Success SHALL be true when reason is AllVisited, AntiLoop, or TargetFound. It SHALL create TraversalResult with all fields populated (Success, CompletionReason, TotalSteps, ElapsedSeconds, ActionHistory from IActionExecutor.GetHistory(), VisitedPages, Trace from TraceRecords, TraceId, FinalState from FSM, Error if present).

#### Scenario: Done with AllVisited reason
- **WHEN** Done() is called with reason "all_visited"
- **THEN** GlobalState is set to Completed, TraversalResult.Success=true

#### Scenario: Done with TargetFound reason
- **WHEN** Done() is called with reason "target_found"
- **THEN** GlobalState is set to Completed, TraversalResult.Success=true

#### Scenario: Done with Timeout reason
- **WHEN** Done() is called with reason "timeout"
- **THEN** GlobalState is set to Terminated, TraversalResult.Success=false

#### Scenario: Done with Error reason
- **WHEN** Done() is called with reason "error" and an Exception
- **THEN** GlobalState is set to Error, TraversalResult.Success=false, TraversalResult.Error=the exception

### Requirement: SimulationRunner, SimulationResult, and SimulationConfig are deleted
SimulationRunner.cs, SimulationResult.cs, and SimulationConfig.cs SHALL be deleted. All logic from SimulationRunner.Run() is migrated into TraversalEngine.RunAsync(). SimulationResult fields merge into TraversalResult. SimulationConfig fields merge into TraversalEngineConfig. StateFixture, StatefulMockVisionService, and StatefulMockActionExecutor SHALL remain in Simulation namespace (still needed for test construction).

#### Scenario: SimulationRunner no longer exists as public API
- **WHEN** code previously used `new SimulationRunner(fixture, root, registry).Run()`
- **THEN** equivalent code SHALL use `new TraversalEngine(plan, vision, action).Run()` with plan constructed from same nodes

#### Scenario: StateFixture and mock services still available
- **WHEN** constructing TraversalEngine for simulation mode
- **THEN** StateFixture, StatefulMockVisionService, and StatefulMockActionExecutor remain in Simulation namespace for caller construction

### Requirement: SimpleNodeRegistry moves to Traversal namespace as DictionaryNodeRegistry
`SimpleNodeRegistry` SHALL be renamed to `DictionaryNodeRegistry` and moved from Simulation namespace to Traversal namespace. This fixes Traversal→Simulation dependency direction. DictionaryNodeRegistry SHALL implement INodeRegistry and be used internally by TraversalEngine.CompilePlan() and DynamicChildManager. Tests SHALL not directly reference DictionaryNodeRegistry — they construct TraversalPlan with Dictionary<string, TraversalNode> for staticNodes.

#### Scenario: TraversalEngine uses DictionaryNodeRegistry internally
- **WHEN** TraversalEngine.CompilePlan() creates a registry
- **THEN** it creates DictionaryNodeRegistry (Traversal namespace), registers all StaticNodes, and root node

#### Scenario: Tests no longer manually create SimpleNodeRegistry
- **WHEN** a test previously created `new SimpleNodeRegistry()` and called `Register(node)` per node
- **THEN** it SHALL instead construct `Dictionary<string, TraversalNode>` and pass it to TraversalPlan.staticNodes

### Requirement: TraversalResult.Reasons includes TargetFound and Timeout constants

TraversalResult.Reasons SHALL define `TargetFound = "target_found"` and `Timeout = "timeout"` as additional const string fields alongside existing AllVisited, AntiLoop, MaxSteps, Cancelled, and Error.

#### Scenario: Reasons.TargetFound constant exists
- **WHEN** `TraversalResult.Reasons.TargetFound` is referenced
- **THEN** its value is `"target_found"`

#### Scenario: Reasons.Timeout constant exists
- **WHEN** `TraversalResult.Reasons.Timeout` is referenced
- **THEN** its value is `"timeout"`

### Requirement: TraversalResult.Reason SHALL use four-tier classification

`TraversalResult.Reason` SHALL classify completion reasons into four tiers: **Achieved** (AllVisited, TargetFound — normal completeness proof, Success=true), **Constraint-pruned** (MaxSteps, Timeout — scoped: over-cap/budget elements out-of-scope, Success=false), **Anomaly** (AntiLoop, Error — hard failure, completeness not claimed, Success=false), and **External** (Cancelled — user abort, Success=false). The invariant SHALL be: anomaly-tier reasons MUST NEVER masquerade as AllVisited or any achieved-tier reason.

#### Scenario: Anomaly never masquerades as AllVisited
- **WHEN** traversal completes with an anomaly-tier reason (AntiLoop or Error)
- **THEN** `TraversalResult.Success` MUST be `false` and `TraversalResult.Reason` MUST NOT be `"all_visited"`

#### Scenario: Cancelled reason is classified as External tier
- **WHEN** traversal is cancelled by the user (CancellationToken)
- **THEN** `TraversalResult.Reason = "cancelled"` is classified in the External tier, not Anomaly

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
