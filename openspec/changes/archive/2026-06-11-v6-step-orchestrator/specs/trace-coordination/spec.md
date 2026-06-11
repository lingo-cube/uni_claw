## ADDED Requirements

### Requirement: Centralized trace span creation

The system SHALL provide a `TraceCoordinator` that owns all trace span creation. Engine, DynamicChildManager, and EntryPolicyExecutor MUST delegate span recording to TraceCoordinator rather than creating spans directly.

#### Scenario: Metrics are converted to spans
- **WHEN** state machine handler metrics are passed to `TraceCoordinator.record_metrics()`
- **THEN** AI call, execution, and error metrics are converted to SpanNode objects and recorded

#### Scenario: State transition is recorded
- **WHEN** a state transition occurs
- **THEN** `TraceCoordinator.record_state_transition(from, to)` creates a state_transition span

#### Scenario: Entry attempt is recorded
- **WHEN** `TraceCoordinator.record_entry_attempt(strategy, success, reason)` is called
- **THEN** a span is created only if trace level is standard or detailed

### Requirement: TraceCoordinator replaces callbacks in DynamicChildManager

DynamicChildManager SHALL accept a `TraceCoordinator` reference instead of individual `record_lifecycle` and `record_skip` callables.

#### Scenario: Lifecycle event is recorded through coordinator
- **WHEN** DynamicChildManager generates a child node
- **THEN** the lifecycle event is recorded via `TraceCoordinator.record_dynamic_lifecycle()`

### Requirement: TraceCoordinator replaces trace_recorder in EntryPolicyExecutor

EntryPolicyExecutor SHALL accept a `TraceCoordinator` reference instead of `trace_recorder` + `should_record`.

#### Scenario: Entry success is recorded through coordinator
- **WHEN** an entry strategy succeeds
- **THEN** the success span is recorded via `TraceCoordinator.record_entry_attempt()`
