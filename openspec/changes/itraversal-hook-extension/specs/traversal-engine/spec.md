## MODIFIED Requirements

### Requirement: TraversalEngine is a sealed class implementing IGraphTraversalEngine as unified entry point
TraversalEngine SHALL be a `sealed class` (not record) implementing `UniClaw.Core.Traversal.IGraphTraversalEngine`. It SHALL have a constructor accepting `TraversalPlan plan`, `IVisionProvider vision`, `IActionExecutor action`, `TraversalEngineConfig? config = null`, `ITraceRecorder? traceRecorder = null`. The constructor SHALL call `Initialize()` which compiles the Plan, creates internal components (TraversalRuntimeContext, TraversalFSM, StepContext, StepOrchestrator), sets `GlobalState = Traversing`, and reads hooks from `_config.Hooks`. `RegisterHook()` method SHALL NOT exist — hooks are set via config only. `_hooks` SHALL be `ImmutableArray<ITraversalHook>` assigned from `_config.Hooks`, not `List<ITraversalHook>`.

#### Scenario: Constructor succeeds with valid plan and hooks
- **WHEN** TraversalEngine is constructed with a valid TraversalPlan, IVisionProvider, IActionExecutor, and TraversalEngineConfig containing 2 hooks
- **THEN** Initialize() compiles the Plan, creates all internal components, sets GlobalState to Traversing, and `_hooks` is `ImmutableArray<ITraversalHook>` with 2 elements from config

#### Scenario: Constructor uses default config with empty hooks
- **WHEN** TraversalEngine is constructed without config parameter
- **THEN** config defaults to `new TraversalEngineConfig()` with `Hooks = ImmutableArray<ITraversalHook>.Empty`; `_hooks.Length == 0`; engine runs normally with zero hook overhead

#### Scenario: RegisterHook method does not exist
- **WHEN** code previously used `engine.RegisterHook(hook)`
- **THEN** this method SHALL NOT exist; hooks SHALL be passed via `TraversalEngineConfig { Hooks = ImmutableArray.Create(hook) }`
