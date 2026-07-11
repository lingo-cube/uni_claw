## MODIFIED Requirements

### Requirement: StepContext is a sealed record class encapsulating step dependencies

`StepContext` SHALL be a `sealed record class` that bundles all dependencies required for a single FSM step execution. It SHALL contain: `context` (TraversalRuntimeContext), `state_machine` (TraversalFSM), `vision` (IVisionProvider), `action` (IActionExecutor), `child_mgr` (IDynamicChildManager), `node_registry` (INodeRegistry), `trace` (ITraceCoordinator), `snapshot_mgr` (IPageSnapshotManager), `stack` (INodeStackAdapter), `last_known_path` (string?), `last_recorded_path` (string?), and `last_recorded_action` (string?). `StepContext` SHALL be constructed once per step and SHALL NOT be mutated after construction (record immutability).

#### Scenario: StepContext contains all 13 dependency fields with interface types
- **WHEN** `StepContext` is inspected for field declarations
- **THEN** it contains exactly: `context` (TraversalRuntimeContext), `state_machine` (TraversalFSM), `vision` (IVisionProvider), `action` (IActionExecutor), `child_mgr` (IDynamicChildManager), `node_registry` (INodeRegistry), `trace` (ITraceCoordinator), `snapshot_mgr` (IPageSnapshotManager), `stack` (INodeStackAdapter), `last_known_path` (string?), `last_recorded_path` (string?), `last_recorded_action` (string?)

#### Scenario: StepContext 4 fields use interface types not concrete types
- **WHEN** `StepContext` field types for `child_mgr`, `trace`, `snapshot_mgr`, `stack` are inspected
- **THEN** `child_mgr` SHALL be `IDynamicChildManager` (not `DynamicChildManager`)
- **THEN** `trace` SHALL be `ITraceCoordinator` (not `TraceCoordinator`)
- **THEN** `snapshot_mgr` SHALL be `IPageSnapshotManager` (not `PageSnapshotManager`)
- **THEN** `stack` SHALL be `INodeStackAdapter` (not `NodeStackAdapter`)

#### Scenario: StepContext is sealed record class
- **WHEN** the type declaration of `StepContext` is inspected
- **THEN** it is `sealed record class` (not mutable class)

#### Scenario: StepContext is immutable after construction
- **WHEN** a `StepContext` instance is created and an attempt is made to reassign one of its fields
- **THEN** the compiler rejects the assignment (record fields are init-only)
