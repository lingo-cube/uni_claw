## Why

Current traversal-related components are scattered across 3 namespaces (StateMachine, Traversal, Simulation). Callers must assemble 5+ types manually to run a traversal, while Python provides a single `GraphTraversalEngine(plan, vision, action).run()` entry point. Additionally, `SimulationRunner` only supports mock execution (not real), `IGraphTraversalEngine` has a dual-definition stub causing dead code (D-14), and there is no structured trace output from the runner.

## What Changes

- **New `TraversalEngine` class** — unified entry point implementing `IGraphTraversalEngine`. Single constructor: `TraversalEngine(plan, vision, action, config?, traceRecorder?)`. `RunAsync()` + `Run()` sync convenience.
- **New `TraversalResult`** — replaces old `TraversalResult` (HashSet violations) and `SimulationResult`. Structured trace, completion reasons, action history.
- **New `TraceRecord`** — per-step trace record (FSM state transitions, actions, page visits).
- **New `TraversalEngineConfig`** — merges `SimulationConfig`. `DelayPerStepMs` generalizes `SimulateDelayMs`.
- **BREAKING: Delete `SimulationRunner`** — logic migrated into `TraversalEngine.RunAsync()`.
- **BREAKING: Delete `SimulationResult`** — merged into `TraversalResult`.
- **BREAKING: Delete `SimulationConfig`** — merged into `TraversalEngineConfig`.
- **Move `SimpleNodeRegistry`** → Traversal namespace as `DictionaryNodeRegistry` (fixes Traversal→Simulation dependency direction).
- **D-14 resolution: Delete `IGraphTraversalEngine` empty stub** in StateMachine namespace. StateMachine→Traversal upward reference explicitly acknowledged (consistent with D-17 for Observability).
- **Architecture guard update** — whitelist StateMachine→Traversal + StateMachine→Observability as acknowledged exceptions to C-5.

## Capabilities

### New Capabilities
- `traversal-engine`: Unified traversal engine entry point — constructor-based initialization, RunAsync/Run execution loop, plan compilation, trace recording, GlobalFSM coordination
- `trace-record`: Structured per-step trace output from traversal execution (TraceRecord type + TraversalResult.Trace field)

### Modified Capabilities
- `step-orchestrator`: TraversalEngine delegates to StepOrchestrator.ExecuteStep() per step; no spec-level requirement changes, only usage context changes (called by TraversalEngine instead of SimulationRunner)
- `traversal-fsm`: TraversalFSM.HasUnvisitedChildren parameter type changes from StateMachine stub to Traversal.IGraphTraversalEngine; TraversalEngine coordinates GlobalFSM state via ctx.GlobalState

## Impact

- **Code**: 3 new files (TraversalEngine.cs, TraversalResult.cs, TraversalEngineConfig.cs), 3 deleted files (SimulationRunner.cs, SimulationResult.cs, SimulationConfig.cs), 1 moved file (SimpleNodeRegistry→DictionaryNodeRegistry), 6 modified files (IGraphTraversalEngine.cs, TraversalState.cs, TraversalFSM.cs, SimulationE2ETests.cs, ArchitectureGuardTests.cs)
- **APIs**: `IGraphTraversalEngine.RunAsync()` return type changes to new `TraversalResult`; `SimulationRunner` public API removed entirely
- **Dependencies**: StateMachine→Traversal upward reference now explicit (was hidden via empty stub)
- **Tests**: SimulationE2ETests migrate from SimulationRunner construction to TraversalEngine construction; ArchitectureGuardTests whitelist new upward references
- **Docs**: layers/traversal.md, layers/simulation.md, constitution/constraints.md, decisions/log.md all updated
