## ADDED Requirements

### Requirement: StepOrchestrator encapsulates single-step pipeline

The system SHALL provide a `StepOrchestrator` that executes one complete state machine step via a `StepContext` value object. The Engine MUST delegate to `StepOrchestrator.execute_step(ctx)` in its main loop.

#### Scenario: Step completes successfully
- **WHEN** `StepOrchestrator.execute_step(ctx)` is called with valid context
- **THEN** the state machine steps, FRAME_COMPLETE interception runs, BRANCH child push runs, path-change detection runs, and a StepResult is returned

#### Scenario: Engine delegates all step work
- **WHEN** the engine's main loop executes
- **THEN** each iteration calls `StepOrchestrator.execute_step(ctx)` without the engine accessing step internals

### Requirement: StepContext bundles step dependencies

The system SHALL provide a `StepContext` dataclass that bundles all dependencies needed for a single step: stack, context, state_machine, vision, action, child_mgr, node_registry, trace, snapshot_mgr, and path tracking fields.

#### Scenario: StepContext is created by Engine
- **WHEN** the engine starts the main loop
- **THEN** a StepContext is created once with all current dependencies and reused across iterations

### Requirement: Engine is pure orchestrator after extraction

After StepOrchestrator and TraceCoordinator extraction, `GraphTraversalEngine` SHALL be under 900 lines and contain only: `__init__`, `initialize`, `run`, `_should_continue`, `_check_completion_policy`, `_create_result`, and `_NodeStackAdapter`.

#### Scenario: Simulation baseline preserved
- **WHEN** the Settings app simulation is run through the refactored engine
- **THEN** 138 steps COMPLETED with 19 visited nodes, identical to pre-refactor baseline
