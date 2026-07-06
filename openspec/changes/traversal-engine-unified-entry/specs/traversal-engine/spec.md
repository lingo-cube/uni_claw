## ADDED Requirements

### Requirement: TraversalEngine is a sealed class implementing IGraphTraversalEngine as unified entry point
TraversalEngine SHALL be a `sealed class` (not record) implementing `UniClaw.Core.Traversal.IGraphTraversalEngine`. It SHALL have a constructor accepting `TraversalPlan plan`, `IVisionProvider vision`, `IActionExecutor action`, `TraversalEngineConfig? config = null`, `ITraceRecorder? traceRecorder = null`. The constructor SHALL call `Initialize()` which compiles the Plan, creates internal components (TraversalRuntimeContext, TraversalFSM, StepContext, StepOrchestrator), and sets `ctx.GlobalState = Traversing`. If initialization fails, the constructor SHALL throw (fail-fast pattern, not Log-and-Continue).

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

### Requirement: TraversalEngine.RunAsync executes step loop with termination conditions
RunAsync() SHALL implement the core traversal loop: for each step up to MaxSteps, check CancellationToken, apply DelayPerStepMs if configured, call StepOrchestrator.ExecuteStep(), handle leaf-pop (pop stack when ResultVerify + depth>1 + ChildrenStrategyType.None), handle child-push→NodeSelect transition, record TraceRecord if TraceEnabled, track visited pages, and check termination conditions (FrameCompleted + depth≤1 → AllVisited, AntiLoopTriggered → AntiLoop, MaxSteps → MaxSteps). RunAsync() SHALL never throw exceptions to callers — all exceptions SHALL be caught and returned as TraversalResult with Reasons.Error.

#### Scenario: Successful traversal completes all nodes
- **WHEN** RunAsync() runs and StepOrchestrator.ExecuteStep() returns FrameCompleted with NodeStack.Depth≤1
- **THEN** RunAsync() returns TraversalResult with Success=true, CompletionReason="all_visited", GlobalState=Completed

#### Scenario: Anti-loop triggered
- **WHEN** RunAsync() runs and StepOrchestrator.ExecuteStep() returns AntiLoopTriggered=true
- **THEN** RunAsync() returns TraversalResult with Success=true, CompletionReason="anti_loop", GlobalState=Completed

#### Scenario: Max steps exceeded
- **WHEN** RunAsync() reaches config.MaxSteps without completion
- **THEN** RunAsync() returns TraversalResult with Success=false, CompletionReason="max_steps"

#### Scenario: Exception during step execution
- **WHEN** an exception occurs during StepOrchestrator.ExecuteStep()
- **THEN** RunAsync() catches the exception, sets ctx.GlobalState=Error, returns TraversalResult with Success=false, CompletionReason="error", Error=caught exception

#### Scenario: CancellationToken triggered
- **WHEN** CancellationToken is signaled during the loop
- **THEN** RunAsync() catches OperationCanceledException, returns TraversalResult with CompletionReason="cancelled", GlobalState=Terminated

### Requirement: TraversalEngine.Run provides synchronous convenience wrapper
Run() SHALL wrap RunAsync() via `.GetAwaiter().GetResult()`. This method SHALL be documented with a ⚠️ deadlock risk warning for ASP.NET/WinForms/WPF environments (SynchronizationContext). It SHALL be safe for CLI and xUnit test environments (no SynchronizationContext).

#### Scenario: Run() executes in test environment
- **WHEN** Run() is called from an xUnit test or CLI context (no SynchronizationContext)
- **THEN** Run() returns the same TraversalResult as RunAsync() would, without deadlock risk

### Requirement: TraversalEngine implements IGraphTraversalEngine lifecycle methods as Phase 3 stubs
TraversalEngine SHALL implement InitializeAsync() as Task.CompletedTask (constructor already initialized), PauseAsync() setting GlobalState=Paused, ResumeAsync() setting GlobalState=Traversing, StopAsync() setting GlobalState=Terminated, and GetStateAsync() returning ctx.GlobalState. These SHALL be stubs — no precondition validation (Phase 3 completes validation).

#### Scenario: InitializeAsync called after construction
- **WHEN** InitializeAsync() is called on a fully constructed TraversalEngine
- **THEN** it returns Task.CompletedTask immediately (no-op, constructor already initialized)

#### Scenario: StopAsync called during traversal
- **WHEN** StopAsync() is called while engine is Traversing
- **THEN** ctx.GlobalState is set to Terminated (terminal state)

### Requirement: TraversalEngine.Done helper produces TraversalResult with correct GlobalState mapping
Done() SHALL map CompletionReason to GlobalState: AllVisited/AntiLoop → Completed, Cancelled → Terminated, Error → Error. It SHALL create TraversalResult with all fields populated (Success, CompletionReason, TotalSteps, ElapsedSeconds, ActionHistory from IActionExecutor.GetHistory(), VisitedPages, Trace from TraceRecords, TraceId, FinalState from FSM, Error if present).

#### Scenario: Done with AllVisited reason
- **WHEN** Done() is called with reason "all_visited"
- **THEN** GlobalState is set to Completed, TraversalResult.Success=true

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
