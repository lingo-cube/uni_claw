## MODIFIED Requirements

### Requirement: InterceptionHandler SHALL own all FSM override logic

`InterceptionHandler` SHALL implement `IInterceptionHandler` and contain all FSM interception/override logic previously inline in `StepOrchestrator`: Branch interception (step 8), DynamicMatch child resolution with navigation/scroll/PressBack (step 9), and FrameComplete override for DynamicMatch nodes with remaining children (step 10). It SHALL also own the helper methods `TryHandleNavigation` (private), `TryHandleScrollAsync` (internal static -- direct contract tests retained), `FromFrame` (private static), `GetElementIds` (private static), and the instance field `_lastPushedChildNodeId`.

InterceptionHandler SHALL delegate container completion judgment to `ContainerHandler`. It SHALL NOT directly set `FrameCompleted` — instead, it SHALL call `ContainerHandler.HandleContainer()` and translate the returned `ContainerActionResult` (Back/AutoEscape/Skip → FrameCompleted=true; Abort → no FrameCompleted). InterceptionHandler SHALL retain only event detection (navigation, scroll, child count, fingerprint). ContainerHandler SHALL be the sole authority for container completion.

#### Scenario: InterceptionHandler contains all override logic
- **WHEN** `StepOrchestrator` source is inspected
- **THEN** no branch/dynamic/frame override logic SHALL remain
- **AND** no `TryHandleNavigation`, `TryHandleScrollAsync`, `FromFrame`, or `GetElementIds` methods SHALL remain
- **AND** no `_lastPushedChildNodeId` field SHALL remain

#### Scenario: InterceptionHandler can be mocked for testing
- **WHEN** a test constructs `StepOrchestrator` with a mock `IInterceptionHandler`
- **THEN** interception behavior SHALL be controllable via the mock without executing real scroll/navigation logic

#### Scenario: InterceptionHandler delegates completion judgment to ContainerHandler
- **WHEN** `OnFrameComplete` hook is invoked
- **THEN** `InterceptionHandler` SHALL call `ContainerHandler.HandleContainer()` to determine completion
- **AND** `InterceptionHandler` SHALL NOT directly set `result.FrameCompleted = true`

#### Scenario: InterceptionHandler translates ContainerActionResult to FrameCompleted
- **WHEN** `ContainerHandler.HandleContainer()` returns `ContainerActionResult` with Action = `Back`
- **THEN** `InterceptionHandler` sets `FrameCompleted = true`
- **WHEN** `ContainerHandler.HandleContainer()` returns `ContainerActionResult` with Action = `Abort`
- **THEN** `InterceptionHandler` does NOT set `FrameCompleted`

### Requirement: StepOrchestrator SHALL retain lifecycle orchestration but not interception

After decomposition, `StepOrchestrator` SHALL retain only: trace lifecycle calls (steps 2, 4, 5, 7, 14), FSM dispatch (step 3), path change detection (step 4 shared logic), visited node bookkeeping (step 12), and conditional routing to `IInterceptionHandler` (steps 8-10). `BranchAllowedSources` SHALL remain in `StepOrchestrator` as it is an orchestration guard condition, not interception logic.

StepOrchestrator SHALL also inject `ContainerHandler` and pass it to `InterceptionHandler` (or make it available via `StepContext`), enabling InterceptionHandler to delegate completion judgment.

#### Scenario: StepOrchestrator contains no override logic
- **WHEN** `StepOrchestrator.ExecuteStepAsync` is inspected
- **THEN** it SHALL contain only trace calls, FSM dispatch, visited bookkeeping, and delegation to `IInterceptionHandler`
- **AND** it SHALL NOT directly call `GetNextUnvisitedChild`, `Push`, `Pop`, `PressBackAsync`, `SwipeAsync`, or `AnalyzeCurrentPageAsync`

#### Scenario: StepOrchestrator wires ContainerHandler into StepContext
- **WHEN** `StepContext` is constructed for a traversal step
- **THEN** a `ContainerHandler` instance is available (injected or constructed as default)
