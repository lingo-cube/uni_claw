## ADDED Requirements

### Requirement: SimulationConfig defines runner behavior limits

The system SHALL provide a `SimulationConfig` record class with `MaxSteps` (int, default 1000), `MaxDepth` (int, default 10), `ThrowOnError` (bool, default false), and `SimulateDelayMs` (int, default 0).

`MaxSteps` SHALL serve as a safety upper bound to prevent infinite loops. `MaxDepth` SHALL be passed to `TraversalRuntimeContext` constructor. `ThrowOnError` SHALL control whether handler exceptions abort the runner (true) or are caught and routed via ErrorHandling (false). `SimulateDelayMs` SHALL introduce a sleep between steps to simulate real device latency.

#### Scenario: Default config has safe limits

- **WHEN** `new SimulationConfig()` is created
- **THEN** `MaxSteps` SHALL be 1000, `MaxDepth` SHALL be 10, `ThrowOnError` SHALL be false, `SimulateDelayMs` SHALL be 0

#### Scenario: Custom config overrides defaults

- **WHEN** `new SimulationConfig { MaxSteps = 100 }` is created
- **THEN** `MaxSteps` SHALL be 100 and all other fields SHALL retain their defaults

### Requirement: SimulationResult captures complete run outcome

The system SHALL provide a `SimulationResult` record class with `Success` (bool), `CompletionReason` (string), `TotalSteps` (int), `ElapsedSeconds` (double), `ActionHistory` (ImmutableArray of ActionRecord), `VisitedPages` (ImmutableArray of string), `FinalState` (TraversalState), and `Error` (Exception?).

Predefined reason constants SHALL be `"all_visited"`, `"max_steps"`, `"error"`, and `"anti_loop"`.

#### Scenario: Successful completion has Success=true and reason=all_visited

- **WHEN** a simulation completes with all nodes visited
- **THEN** `Success` SHALL be true and `CompletionReason` SHALL be `"all_visited"`

#### Scenario: MaxSteps exceeded has Success=false and reason=max_steps

- **WHEN** a simulation reaches MaxSteps without completing
- **THEN** `Success` SHALL be false and `CompletionReason` SHALL be `"max_steps"`

#### Scenario: Exception has Success=false, reason=error, and Error set

- **WHEN** a simulation throws an unhandled exception
- **THEN** `Success` SHALL be false, `CompletionReason` SHALL be `"error"`, and `Error` SHALL contain the exception

### Requirement: SimulationRunner drives StepOrchestrator in a loop

The system SHALL provide a `SimulationRunner` class that:

1. Constructs with `StateFixture`, `TraversalNode rootNode`, `SimpleNodeRegistry`, and optional `SimulationConfig`.
2. Creates `StatefulMockVisionService` and `StatefulMockActionExecutor` internally.
3. Creates real `TraversalRuntimeContext`, `TraversalFSM`, and `StepOrchestrator`.
4. Assembles a `StepContext` with the real FSM/Context and mock Vision/Action.
5. On `Run()`, calls `StepOrchestrator.ExecuteStep(stepCtx)` in a loop until a termination condition is met.
6. Records page visits by tracking `_vision.CurrentPageId` changes.
7. Returns `SimulationResult` with collected data.

The runner SHALL terminate when `stepResult.FrameCompleted && ctx.NodeStack.Depth <= 1` (all_visited), or `stepResult.AntiLoopTriggered` (anti_loop), or step count reaches `MaxSteps` (max_steps), or an unhandled exception occurs (error).

The runner SHALL NOT manually manage FSM state transitions, node stack pushes, or visited children — StepOrchestrator handles these.

#### Scenario: 2-page traversal completes with all_visited

- **WHEN** `Run()` is called with a 2-page fixture (home → settings → back → home) and a complete node tree
- **THEN** the result SHALL have `Success=true`, `CompletionReason="all_visited"`, and `VisitedPages` SHALL contain home and settings in order

#### Scenario: Empty node tree completes immediately

- **WHEN** `Run()` is called with a root node that has no static children
- **THEN** the result SHALL have `Success=true` and `TotalSteps` SHALL be small (root NoAction execution only)

#### Scenario: MaxSteps exceeded

- **WHEN** `Run()` is called with `MaxSteps=2` on a fixture requiring more steps
- **THEN** the result SHALL have `Success=false` and `CompletionReason="max_steps"`

#### Scenario: Runner does not throw on handler exception by default

- **WHEN** a handler throws during `Run()` with `ThrowOnError=false`
- **THEN** the exception SHALL be caught and the result SHALL NOT have Success=false (FSM routes to ErrorHandling internally)

#### Scenario: Runner throws when ThrowOnError=true

- **WHEN** a handler throws during `Run()` with `ThrowOnError=true`
- **THEN** the runner SHALL rethrow and the result SHALL have `Success=false`, `CompletionReason="error"`
