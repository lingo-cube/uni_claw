## Why

`GraphTraversalEngine` has grown to 1990 lines / 54 methods, mixing state machine scheduling, dynamic matching, entry policy execution, page snapshot management, and trace recording into a single class. This makes it hard to test individual behaviors, error-prone for AI-assisted development, and impossible to swap components independently. The refactoring separates orchestration from component responsibilities.

## What Changes

- Extract `DynamicChildManager` — owns dynamic child generation, caching, invalidation, and `_generated_pairs` deduplication
- Extract `PageSnapshotManager` — pure-function page fingerprint computation
- Extract `StepOrchestrator` — single-step execution pipeline (state machine call, FRAME_COMPLETE interception, child push, page-change detection)
- Extract `EntryPolicyExecutor` — entry strategy chain and wait condition verification
- Extract `TraceCoordinator` — metrics-to-span conversion and trace recording
- Extract `PlanValidator` — plan validation
- Shrink `GraphTraversalEngine` to pure orchestrator: initialization, main loop, completion check, result creation
- **Hard constraint**: simulation tests must pass identically at every migration step — 89 steps COMPLETED, 19 nodes, all menu levels traversed

## Capabilities

### New Capabilities
- `dynamic-child-management`: Dynamic child generation, caching, invalidation, and page-element deduplication
- `step-orchestration`: Single-step execution pipeline coordinating state machine transitions, child pushing, and page-change detection
- `engine-component-extraction`: Separation of entry policy, plan validation, and trace coordination from the engine

### Modified Capabilities
<!-- No existing spec requirements are changing — this is a pure internal refactor. Behavior is preserved exactly. -->

## Impact

- Affected code: `src/traversal/graph_engine.py` (split into 6+ files), `src/state_machine/traversal_fsm.py` (interface consistency with StepOrchestrator)
- No API changes, no dependency changes
- Existing simulation and unit tests serve as regression gate at every migration step
