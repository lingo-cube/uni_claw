## MODIFIED Requirements

### Requirement: StepOrchestrator executes_step via 14-step interception layer wrapping TraversalFSM
StepOrchestrator's 14-step interception layer is unchanged in its internal logic. The change is in **usage context**: StepOrchestrator.ExecuteStep() is now invoked by `TraversalEngine.RunAsync()` per step iteration, replacing `SimulationRunner.Run()` as the caller. No spec-level requirement changes to the 14-step process itself — this is purely a caller migration.

#### Scenario: StepOrchestrator called by TraversalEngine
- **WHEN** TraversalEngine.RunAsync() iterates the step loop
- **THEN** each iteration calls StepOrchestrator.ExecuteStep(StepContext) and processes the StepResult (leaf-pop, child-push→NodeSelect, trace recording, termination checks)
