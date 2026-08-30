## Purpose

Define one minimal Agent-owned, Run-local execution context that replaces scattered active-Container state while keeping fresh observed world location separate from execution obligation and excluding navigation-graph semantics.

## ADDED Requirements

### Requirement: Observed location and execution obligation are distinct
The Runtime SHALL represent `CurrentObservedLocation` as the semantic location in its fresh accepted WorldBelief and SHALL represent `ActiveExecutionContainer` as the Container whose traversal/completeness obligation the Agent still owns. The Runtime MUST permit these values to differ without treating the difference itself as a state conflict, and SHALL NOT introduce a mutable `CurrentContainer` value that merges them.

#### Scenario: Premature parent return keeps the child obligation
- **WHEN** fresh accepted evidence grounds the current observed location as parent `SettingsRoot` while child `Display` remains incomplete
- **THEN** `CurrentObservedLocation` SHALL be `SettingsRoot`, `ActiveExecutionContainer` SHALL remain `Display`, and no child completion or re-entry SHALL be inferred

#### Scenario: Normal same-Container execution remains aligned
- **WHEN** fresh accepted evidence remains in the active execution Container
- **THEN** observed location and active execution Container SHALL remain equal without storing a duplicate observed location in the execution context

### Requirement: Minimal ActiveContainerContext replaces scattered state
The Runtime SHALL use one immutable Agent-owned `ActiveContainerContext` value containing exactly `ActiveExecutionContainer` and `ActiveAncestorPath` as its mutable execution-context truth. It SHALL replace the existing active-Container slot and method-local active parent stack, SHALL derive current ancestry membership from the path plus the active execution Container, and SHALL NOT retain old mutable state in parallel with the context.

#### Scenario: State replacement completes
- **WHEN** ownership consolidation is complete
- **THEN** the old active-Container field, method-local active parent stack, and separately maintained current-ancestry set SHALL be absent, and their consumers SHALL read the context or its derived views

#### Scenario: Mutable truth budget is enforced
- **WHEN** the before/after state inventory is evaluated
- **THEN** semantic mutable container-location/execution facts SHALL decrease from four to three, mutable storage slots SHALL decrease from four to two, and the number of mutable owners SHALL remain one

### Requirement: ActiveAncestorPath is only the active recursive execution chain
`ActiveAncestorPath` SHALL be an ordered, Run-local, bounded sequence of the existing parent execution Container and entered-child obligation values needed for verified return. It SHALL be appended only on an already-authorized child entry, popped only on a verified return, and discarded with the Run. It SHALL NOT support arbitrary lookup, route search, cross-session reuse, graph exhaustion, topology persistence, navigation completion, or recovery authorization.

#### Scenario: Authorized child entry extends the active path
- **WHEN** existing action authorization and fresh transition evidence admit child `Display` from parent `SettingsRoot`
- **THEN** the path SHALL contain the parent execution entry and the active execution Container SHALL become `Display`

#### Scenario: Verified return contracts the active path
- **WHEN** existing completeness evidence and fresh exact-parent continuity prove a verified return from `Display` to `SettingsRoot`
- **THEN** the path SHALL remove the parent entry used for that return and the active execution Container SHALL resume as `SettingsRoot`

#### Scenario: Path cannot answer a route query
- **WHEN** a consumer asks how to navigate to an arbitrary Container or whether the world topology is exhausted
- **THEN** `ActiveAncestorPath` SHALL provide no route, edge, search, topology, or navigation-completion answer

### Requirement: Existing non-location evidence stays with its current owner
Container `CurrentObservation` and local completeness SHALL remain Container-owned evidence; cross-Container branch progress, verified-return bookkeeping, and external `BoundaryRelation` evidence SHALL remain in the existing Agent-owned progress ledger; run-local visited identity evidence SHALL remain historical identity-safety evidence. `ActiveContainerContext` SHALL NOT copy or acquire any of these values.

#### Scenario: Completeness is referenced rather than copied
- **WHEN** a transition requires completeness evidence
- **THEN** the transition/context flow SHALL reference the existing Container/progress evidence and SHALL NOT store a completeness boolean or subtree state in `ActiveContainerContext`

#### Scenario: Historical visited evidence is not current truth
- **WHEN** a previously visited Container is not on the active execution chain
- **THEN** visited evidence MAY retain its historical record but SHALL NOT affect `CurrentObservedLocation`, `ActiveExecutionContainer`, or `ActiveAncestorPath`
