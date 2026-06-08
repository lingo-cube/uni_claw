## ADDED Requirements

### Requirement: Engine as pure orchestrator

After extraction, `GraphTraversalEngine` SHALL only: hold component instances, run the main traversal loop, check completion policy and depth limits, and create `TraversalResult`. It MUST NOT contain dynamic child generation logic, page snapshot computation, entry policy execution, or trace span creation.

#### Scenario: Engine delegates all step work
- **WHEN** the engine's main loop executes
- **THEN** each step is delegated to `StepOrchestrator.execute_step()` without the engine accessing dynamic child caches or page fingerprints directly

#### Scenario: Engine workflow remains unchanged
- **WHEN** a Settings app traversal plan is executed through the refactored engine
- **THEN** the traversal produces 89 steps COMPLETED with 19 visited nodes, identical to pre-refactor behavior

### Requirement: Entry policy extraction

Entry policy execution SHALL be handled by a dedicated `EntryPolicyExecutor` component. The component MUST attempt strategies in the configured fallback chain and raise `EntryPolicyError` if all strategies fail. The engine MUST call this component during initialization and SHALL NOT contain strategy execution logic.

#### Scenario: Fallback chain is followed
- **WHEN** the primary entry strategy fails
- **THEN** the executor tries the configured fallback strategy, then BIND_CURRENT_SCREEN as final fallback

#### Scenario: All strategies fail
- **WHEN** all strategies in the chain fail
- **THEN** `EntryPolicyError` is raised with the list of failed strategies

### Requirement: Trace coordination extraction

Trace span creation SHALL be handled by a dedicated `TraceCoordinator` component. The component MUST convert state machine metrics to Span nodes and record them via the trace recorder. The engine and StepOrchestrator MUST call TraceCoordinator rather than creating spans directly.

#### Scenario: Metrics are recorded as spans
- **WHEN** the state machine produces handler metrics
- **THEN** `TraceCoordinator.record_metrics()` converts them to Span objects and records them

### Requirement: Page fingerprint as pure function

Page fingerprint computation SHALL be a pure static function with no side effects. It MUST produce the same hash for the same page analysis (sorted type-name tuples), and produce different hashes for different element sets.

#### Scenario: Same page produces same fingerprint
- **WHEN** `fingerprint(page_a)` and `fingerprint(page_b)` are called with identical element sets (same names and types)
- **THEN** both calls return the same integer hash

#### Scenario: Different pages produce different fingerprints
- **WHEN** two pages have different element sets
- **THEN** their fingerprints SHALL be different

### Requirement: Plan validation extraction

Plan validation SHALL be handled by a dedicated `PlanValidator` component. The validator MUST check root_node existence, type, and operation correctness, raising `ConfigurationError` on failure.

#### Scenario: Valid plan passes validation
- **WHEN** a plan with CONTAINER root and no_action operation is validated
- **THEN** validation passes without exception

#### Scenario: Invalid root type raises error
- **WHEN** a plan with LEAF_ACTION root node is validated
- **THEN** `ConfigurationError` is raised
