## MODIFIED Requirements

### Requirement: StepOrchestrator passes StepContext to TraversalFSM.Step

`StepOrchestrator.ExecuteStep(StepContext ctx)` SHALL call `ctx.StateMachine.Step(ctx)` instead of `ctx.StateMachine.Step()`. This enables FSM handlers to access `IVisionProvider` and `IActionExecutor` through `_currentStepContext`.

The change SHALL be non-breaking: `TraversalFSM.Step(null)` delegates to `Step()` behavior (stub fallback for handlers without StepContext).

#### Scenario: StepOrchestrator passes ctx to FSM

- **WHEN** `StepOrchestrator.ExecuteStep(ctx)` is called
- **THEN** `ctx.StateMachine.Step(ctx)` SHALL be invoked (not `Step()` without arguments)
- **AND** FSM handlers SHALL receive `_currentStepContext` set to `ctx`

#### Scenario: Existing Step() behavior unchanged

- **WHEN** `TraversalFSM.Step()` is called directly (without StepContext)
- **THEN** handlers SHALL operate in stub fallback mode (returning default states)
- **AND** no exception SHALL be thrown

#### Scenario: E2E traversal via StepOrchestrator with mock services

- **WHEN** `StepOrchestrator.ExecuteStep(ctx)` is called with `ctx.Vision` = `StatefulMockVisionService` and `ctx.Action` = `StatefulMockActionExecutor`
- **THEN** `HandleExecute` SHALL dispatch operations via the mock action executor
- **AND** `HandleExecute` SHALL return the correct next state based on execution outcome
