## Context

After V6.11.0, Engine is 1293 lines. Two large blocks remain:
- `_step_once` (~200 lines) — the core single-step pipeline
- 15 `_record_*` methods (~300 lines) — trace span creation

These are the last remaining "god methods" in Engine. Extracting them completes the decomposition.

**Hard constraint**: Simulation tests are the sole success criterion — 138 steps COMPLETED, 19 nodes.

## Goals / Non-Goals

**Goals:**
- Extract TraceCoordinator — reduce Engine ~300 lines, centralize span creation
- Extract StepOrchestrator — reduce Engine ~200 lines, make step pipeline independently testable
- Engine finalized at ~800 lines as pure orchestrator

**Non-Goals:**
- Changing any traversal behavior
- Modifying state machine or template logic
- Adding new features

## Decisions

### Decision 1: TraceCoordinator first, then StepOrchestrator

TraceCoordinator is a pure extraction — 15 methods that only depend on `trace_recorder`. Extract it first so StepOrchestrator can use it cleanly.

### Decision 2: StepContext value object for StepOrchestrator

`_step_once` accesses 10+ attributes on `self`. Instead of passing 10 parameters, bundle them:

```python
@dataclass
class StepContext:
    stack: _NodeStackAdapter
    context: TraversalRuntimeContext
    state_machine: TraversalStateMachine
    vision: VisionService
    action: ActionExecutor
    child_mgr: DynamicChildManager
    node_registry: Dict[str, TraversalNode]
    trace: TraceCoordinator
    snapshot_mgr: PageSnapshotManager
    last_known_path: List[str]
    last_recorded_path: List[str]
    last_recorded_action: Optional[str]
```

StepOrchestrator.execute_step(ctx) → StepResult.

### Decision 3: Replace callbacks with TraceCoordinator

DynamicChildManager currently takes `record_lifecycle` + `record_skip` callbacks. Replace with a single `trace: TraceCoordinator` reference.

EntryPolicyExecutor currently takes `trace_recorder` + `should_record`. Replace with `trace: TraceCoordinator`.

### Decision 4: Migration order

1. TraceCoordinator — extract + inject into Engine, DynamicChildManager, EntryPolicyExecutor
2. StepOrchestrator — extract _step_once with StepContext
3. Engine cleanup — remove delegated methods
4. Verify — simulation baseline unchanged

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| StepContext has too many fields | Start with all 10; refactor further in future |
| TraceCoordinator changes break EntryPolicyExecutor | Entry tests cover span recording behavior |
| Circular imports | TraceCoordinator is a leaf dependency — no imports from traversal module |

## Migration Plan

| Step | Action | Gate |
|------|--------|------|
| 1 | Extract TraceCoordinator | 79 tests pass, simulation 138 steps |
| 2 | Replace callbacks with TraceCoordinator | Same |
| 3 | Extract StepOrchestrator | Same |
| 4 | Engine cleanup | Engine < 900 lines, all tests pass |

**Rollback**: Each step is a separate commit.
