## ADDED Requirements

### Requirement: ContainerHandler.HandleContainer() unified pipeline entry

ContainerHandler SHALL provide a `sealed class ContainerHandler` with a `HandleContainer(CompletionContext completionCtx, bool canContinue, string nodeId, ITraversalContext traversalContext)` method that executes a 3-step pipeline: detect → decide → execute. The pipeline SHALL wrap all 3 steps in a try/catch that returns `ContainerActionResult(Back, false, "Unhandled exception...")` on any unhandled exception.

#### Scenario: Normal pipeline execution — detect→decide→execute
- **WHEN** HandleContainer() is called with valid CompletionContext, canContinue=true, nodeId, and ITraversalContext
- **THEN** CompletionDetector.DetectCompletion() SHALL be called first
- **THEN** FallbackDecider.DecideFallback() SHALL be called with the completion result and canContinue
- **THEN** ContainerActionExecutor.Execute() SHALL be called with the fallback action and a ContainerContext built from nodeId, completionCtx.CurrentDepth, and traversalContext
- **THEN** the ContainerActionResult from the executor SHALL be returned

#### Scenario: Pipeline-level fallback on any step exception
- **WHEN** any step in the HandleContainer pipeline throws an Exception
- **THEN** the exception SHALL NOT propagate to the caller
- **THEN** the method SHALL return `ContainerActionResult(FallbackAction.Back, false, "Unhandled exception during container handling: {ex.GetType().Name}: {ex.Message}")`

#### Scenario: Pipeline fallback Success=false vs executor fallback Success=true
- **WHEN** pipeline-level try/catch catches an exception
- **THEN** Success MUST be false (pipeline crashed, BACK is safest guess)
- **WHEN** ContainerActionExecutor catches an exception internally
- **THEN** Success MUST be true (DefaultBack is a known-working action)
- **THEN** this difference is intentional and documented in D-G4

#### Scenario: Constructor injection with optional sub-components
- **WHEN** ContainerHandler is constructed with no arguments
- **THEN** it SHALL create default instances of CompletionDetector, FallbackDecider, and ContainerActionExecutor
- **WHEN** custom sub-component instances are passed via constructor
- **THEN** they SHALL be used instead of defaults (dependency injection for testability)
