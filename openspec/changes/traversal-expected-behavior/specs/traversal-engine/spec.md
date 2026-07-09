## MODIFIED Requirements

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
