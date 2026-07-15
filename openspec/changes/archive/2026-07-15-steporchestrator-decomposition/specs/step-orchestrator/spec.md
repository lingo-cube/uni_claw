## ADDED Requirements

### Requirement: StepOrchestrator SHALL delegate FSM interception to IInterceptionHandler

`StepOrchestrator` SHALL delegate FSM transition interception logic (steps 8-10) to an `IInterceptionHandler` interface rather than implementing it inline. The handler SHALL be injected via constructor (`IInterceptionHandler`) with a default implementation of `new InterceptionHandler()`. The orchestrator SHALL only apply interception overrides when an interception condition matches; when no interception condition matches, the FSM's original `nextState` SHALL be preserved.

#### Scenario: Branch interception delegates to handler
- **WHEN** `ExecuteStepAsync` detects `nextState == TraversalState.Branch` AND `fromState` is in `BranchAllowedSources`
- **THEN** the orchestrator SHALL call `_handler.OnBranch(ctx, fromState)` and apply the returned `InterceptionResult`
- **AND** the orchestrator SHALL NOT contain inline branch/push/scroll/navigation logic

#### Scenario: No interception match preserves FSM state
- **WHEN** `ExecuteStepAsync` detects `nextState` is `ResultVerify` (no interception condition matches)
- **THEN** `nextState` SHALL remain `ResultVerify` (unchanged by any interception)
- **AND** `childPushed`, `frameCompleted`, `frameOverrideTriggered` SHALL remain false

#### Scenario: Default handler is constructed for backward compatibility
- **WHEN** `StepOrchestrator` is constructed without an explicit `IInterceptionHandler` parameter
- **THEN** it SHALL default to `new InterceptionHandler()`

### Requirement: InterceptionResult SHALL be a value type encapsulating FSM override state

The result of FSM interception SHALL be represented as `InterceptionResult`, a `record struct` with four fields: `NextState` (the possibly-overridden next FSM state), `ChildPushed` (whether a child was pushed onto the node stack), `FrameCompleted` (whether the current frame should be marked complete), and `FrameOverrideTriggered` (whether a `FrameComplete` transition was overridden to `NodeSelect`). The struct SHALL be mutable (not `readonly`) to support `ref` mutation by internal helper methods.

#### Scenario: InterceptionResult carries all override state
- **WHEN** `OnBranch` returns an `InterceptionResult`
- **THEN** the caller SHALL extract `NextState`, `ChildPushed`, `FrameCompleted`, and `FrameOverrideTriggered` from it
- **AND** no `ref bool` or `ref TraversalState` parameters SHALL appear on public interface methods

#### Scenario: Default InterceptionResult is a safe no-op
- **WHEN** `default(InterceptionResult)` is evaluated
- **THEN** `NextState` is `default(TraversalState)`, and all three `bool` fields are `false`
- **AND** the orchestrator's `intercepted` flag SHALL prevent this default from being applied to FSM state

### Requirement: InterceptionHandler SHALL own all FSM override logic

`InterceptionHandler` SHALL implement `IInterceptionHandler` and contain all FSM interception/override logic previously inline in `StepOrchestrator`: Branch interception (step 8), DynamicMatch child resolution with navigation/scroll/PressBack (step 9), and FrameComplete override for DynamicMatch nodes with remaining children (step 10). It SHALL also own the helper methods `TryHandleNavigation` (private), `TryHandleScrollAsync` (internal static — direct contract tests retained), `FromFrame`, `GetElementIds` (private static), and the instance field `_lastPushedChildNodeId`.

#### Scenario: InterceptionHandler contains all override logic
- **WHEN** `StepOrchestrator` source is inspected
- **THEN** no branch/dynamic/frame override logic SHALL remain
- **AND** no `TryHandleNavigation`, `TryHandleScrollAsync`, `FromFrame`, or `GetElementIds` methods SHALL remain
- **AND** no `_lastPushedChildNodeId` field SHALL remain

#### Scenario: InterceptionHandler can be mocked for testing
- **WHEN** a test constructs `StepOrchestrator` with a mock `IInterceptionHandler`
- **THEN** interception behavior SHALL be controllable via the mock without executing real scroll/navigation logic

### Requirement: StepOrchestrator SHALL retain lifecycle orchestration but not interception

After decomposition, `StepOrchestrator` SHALL retain only: trace lifecycle calls (steps 2, 4, 5, 7, 14), FSM dispatch (step 3), path change detection (step 4 shared logic), visited node bookkeeping (step 12), and conditional routing to `IInterceptionHandler` (steps 8-10). `BranchAllowedSources` SHALL remain in `StepOrchestrator` as it is an orchestration guard condition, not interception logic.

#### Scenario: StepOrchestrator contains no override logic
- **WHEN** `StepOrchestrator.ExecuteStepAsync` is inspected
- **THEN** it SHALL contain only trace calls, FSM dispatch, visited bookkeeping, and delegation to `IInterceptionHandler`
- **AND** it SHALL NOT directly call `GetNextUnvisitedChild`, `Push`, `Pop`, `PressBackAsync`, `SwipeAsync`, or `AnalyzeCurrentPageAsync`