## Why

V6.11.0 extracted 5 components from GraphTraversalEngine but deferred two: `StepOrchestrator` (the `_step_once` method still lives in Engine at ~200 lines) and `TraceCoordinator` (15 `_record_*` methods spread across Engine). Extracting them completes the architectural decomposition — Engine becomes a pure orchestrator, `_step_once` becomes independently testable, and trace recording is centralized.

## What Changes

- Extract `TraceCoordinator` — 15 `_record_*` methods into a single class, inject into Engine, DynamicChildManager, and EntryPolicyExecutor
- Extract `StepOrchestrator` — `_step_once` logic with a `StepContext` value object bundling its 10+ dependencies
- Shrink Engine to ~800 lines — main loop, completion check, result creation only
- Update DynamicChildManager to accept `TraceCoordinator` instead of individual callbacks
- Update EntryPolicyExecutor to accept `TraceCoordinator` instead of raw `trace_recorder` + `should_record`
- **Hard constraint**: `test_settings_simulation_run` must produce 138 steps COMPLETED, 19 nodes at every step

## Capabilities

### New Capabilities
- `trace-coordination`: Centralized trace span creation from state machine metrics, page snapshots, and action executions
- `step-orchestration-v2`: Single-step execution pipeline with StepContext injection pattern

### Modified Capabilities
<!-- None — pure internal refactor, behavior preserved exactly -->

## Impact

- Affected code: `src/traversal/graph_engine.py`, `src/traversal/dynamic_child_manager.py`, `src/traversal/entry_policy_executor.py`
- New files: `src/traversal/trace_coordinator.py`, `src/traversal/step_orchestrator.py`
- Existing simulation baseline (138 steps, 19 nodes) serves as regression gate
