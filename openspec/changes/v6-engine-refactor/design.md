## Context

`GraphTraversalEngine` (1990 lines, 54 methods) has grown beyond its original scope. It currently handles: initialization, entry policy execution, dynamic child generation/caching/deduplication, page snapshot fingerprinting, trace recording (12+ `_record_*` methods), state machine stepping, FRAME_COMPLETE interception, child node pushing, path change detection, and page-change detection. This is a single-class monolith that resists unit testing and complicates AI-assisted development.

The PRD at `docs/prd/PRD_V6_11_0_engine_refactor.md` defines the target architecture with 7 components. This design document captures the technical decisions for the decomposition.

**Hard constraint**: Simulation tests are the sole success criterion. Every migration step must preserve exact behavior — 89 steps COMPLETED, 19 nodes, all menu levels traversed.

## Goals / Non-Goals

**Goals:**
- Separate orchestration (Engine) from component responsibilities
- Make each component independently testable (50-300 lines each)
- Preserve exact external behavior — this is a pure internal refactor
- Follow existing patterns: `DynamicChildManager` already has a natural boundary at `_generate_dynamic_children` / `_get_next_unvisited_child` / `invalidate_children_cache` / `_generated_pairs`

**Non-Goals:**
- Adding new features or changing traversal behavior
- Changing the state machine interface
- Modifying template or fixture data
- Real-device testing (simulation-only validation for each step)

## Decisions

### Decision 1: Component decomposition — 7 components, no size threshold

Extract all 7 components regardless of method count. `PlanValidator` (1 method) and `PageCacheManager` (2 methods) are small but have distinct responsibilities that may evolve independently (e.g., fingerprint algorithm optimization in PageCacheManager).

**Alternatives considered**: Only extract components above a size threshold (e.g., >5 methods). Rejected — inconsistent, and small components still benefit from isolated testing.

### Decision 2: StepOrchestrator owns state override, not the state machine

When the state machine produces `FRAME_COMPLETE` or `BRANCH` transitions but `DynamicChildManager` still has unvisited children, the `StepOrchestrator` overrides the state to `NODE_SELECT` and pushes the child. The state machine does not know about dynamic children.

**Rationale**: The state machine manages node lifecycle. Dynamic child generation is an engine-layer concern. Introducing dynamic child awareness into the state machine would couple two unrelated concerns.

**Alternatives considered**: Have the state machine call back into DynamicChildManager. Rejected — would violate state machine's single responsibility and complicate its state transition table.

### Decision 3: `_generated_pairs` owned by DynamicChildManager

The `(page_fingerprint, element_name)` deduplication set lives entirely inside `DynamicChildManager`. `PageSnapshotManager` is a pure-function utility that computes fingerprints — it does not store state or participate in deduplication logic.

**Rationale**: Deduplication is a strategy of child generation, not of snapshot comparison. Testing deduplication requires only a mock PageSnapshotManager returning fixed fingerprints.

### Decision 4: Migration order by independence

1. `DynamicChildManager` + `PageSnapshotManager` — most independent, clear boundaries already exist in code
2. `StepOrchestrator` — depends on DynamicChildManager and PageSnapshotManager
3. `EntryPolicyExecutor` + `TraceCoordinator` — independent of each other, can be done in parallel
4. Engine cleanup — remove extracted methods, verify orchestration

**Rationale**: Extract the most independent components first to build confidence and minimize risk. Each step is validated by the simulation test before proceeding.

### Decision 5: File layout

New components go in `src/traversal/` alongside the existing `graph_engine.py`:
```
src/traversal/
├── graph_engine.py              # Shrunk to orchestrator (~300 lines)
├── step_orchestrator.py         # New: StepOrchestrator
├── dynamic_child_manager.py     # New: DynamicChildManager
├── page_snapshot_manager.py     # New: PageSnapshotManager
├── entry_policy_executor.py     # New: EntryPolicyExecutor
├── trace_coordinator.py         # New: TraceCoordinator
├── plan_validator.py            # New: PlanValidator
└── page_cache_manager.py        # New: PageCacheManager
```

All internal to `src/traversal/` — no public API changes.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Component boundaries misaligned, requiring rework | Extract smallest components first; simulation test gates every step |
| Increased file count makes navigation harder | Co-locate in `src/traversal/`; Engine remains the single entry point |
| Circular imports between components | StepOrchestrator injects dependencies via constructor; no component imports another directly |
| Performance regression from indirection | Pure delegation, no extra allocations; fingerprint computation is cached |

## Migration Plan

Each step has a hard gate: `tests/v6/settings/test_settings_simulation.py::test_settings_simulation_run` must produce 89 steps, COMPLETED, 19 nodes.

| Step | Extract | Validation |
|------|---------|------------|
| 1 | DynamicChildManager + PageSnapshotManager | Simulation: 89 steps, 19 nodes |
| 2 | StepOrchestrator | Simulation + `test_branch_handling` 12/12 |
| 3 | EntryPolicyExecutor + TraceCoordinator | Simulation + `test_engine_initialization` |
| 4 | PlanValidator + PageCacheManager + Engine cleanup | Full V6 test suite |

**Rollback**: Each step is a separate commit. If any step fails simulation validation, revert that commit and re-assess boundary.

## Open Questions

<!-- None — design decisions are resolved through the PRD review -->
