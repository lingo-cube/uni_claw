## Requirements

### Requirement: TraversalResult is a sealed record class capturing unified engine execution outcome
TraversalResult SHALL be a `sealed record class` with fields: `bool Success`, `string CompletionReason` (using Reasons constants: "all_visited", "max_steps", "error", "anti_loop", "cancelled"), `int TotalSteps`, `double ElapsedSeconds`, `ImmutableArray<ActionRecord> ActionHistory`, `ImmutableArray<string> VisitedPages`, `ImmutableArray<TraceRecord> Trace`, `string? TraceId`, `TraversalState FinalState`, `Exception? Error = null`. A static nested class `Reasons` SHALL define 5 const string fields for completion reason values. TraversalResult replaces the old `IGraphTraversalEngine.cs` TraversalResult (which used HashSet + Dictionary, violating P-4/P-5) and `Simulation/SimulationResult.cs`.

#### Scenario: Successful traversal result
- **WHEN** engine completes with AllVisited or AntiLoop reason
- **THEN** TraversalResult has Success=true, CompletionReason="all_visited" or "anti_loop", Error=null

#### Scenario: Error traversal result
- **WHEN** engine encounters an exception
- **THEN** TraversalResult has Success=false, CompletionReason="error", Error=the caught exception, FinalState reflects FSM state at failure point

#### Scenario: Trace records populated when TraceEnabled=true
- **WHEN** TraversalEngineConfig.TraceEnabled is true
- **THEN** TraversalResult.Trace contains ImmutableArray<TraceRecord> with one entry per step executed

#### Scenario: Trace empty when TraceEnabled=false
- **WHEN** TraversalEngineConfig.TraceEnabled is false
- **THEN** TraversalResult.Trace is ImmutableArray<TraceRecord>.Empty

### Requirement: TraceRecord is a sealed record class capturing per-step trace data
TraceRecord SHALL be a `sealed record class` with fields: `int StepNumber`, `TraversalState FromState`, `TraversalState ToState`, `string? CurrentNodeId`, `string? CurrentPageId`, `string? ActionExecuted`, `bool ActionSuccess`, `bool ChildPushed`, `bool FrameCompleted`. TraceRecord is independent from ITraceRecorder — it records in-memory per-step data for TraversalResult, while ITraceRecorder handles external persistence via TraceCoordinator.

#### Scenario: TraceRecord captures FSM state transition
- **WHEN** a step transitions from NodeSelect to PreconditionCheck
- **THEN** TraceRecord.FromState=NodeSelect, TraceRecord.ToState=PreconditionCheck

#### Scenario: TraceRecord captures action execution
- **WHEN** a step executes an action "click_button" successfully
- **THEN** TraceRecord.ActionExecuted="click_button", ActionSuccess=true

#### Scenario: TraceRecord captures child push
- **WHEN** StepOrchestrator returns ChildPushed=true
- **THEN** TraceRecord.ChildPushed=true

### Requirement: TraversalEngineConfig is a sealed record class merging SimulationConfig
TraversalEngineConfig SHALL be a `sealed record class` with init-only properties: `int MaxSteps = 1000`, `int MaxDepth = 10`, `bool ThrowOnError = false`, `bool TraceEnabled = true`, `int DelayPerStepMs = 0`. `DelayPerStepMs` generalizes SimulationConfig.SimulateDelayMs — in simulation mode it models delay, in production mode it waits for UI stabilization. SimulationConfig.cs SHALL be deleted entirely.

#### Scenario: Default config
- **WHEN** TraversalEngine constructed with no config parameter
- **THEN** config defaults to MaxSteps=1000, MaxDepth=10, ThrowOnError=false, TraceEnabled=true, DelayPerStepMs=0

#### Scenario: Simulation delay configured
- **WHEN** TraversalEngineConfig.DelayPerStepMs=200 for simulation
- **THEN** RunAsync() applies 200ms delay per step via Task.Delay

#### Scenario: SimulationConfig no longer exists
- **WHEN** code previously used `SimulationConfig.SimulateDelayMs`
- **THEN** equivalent SHALL use `TraversalEngineConfig.DelayPerStepMs`
