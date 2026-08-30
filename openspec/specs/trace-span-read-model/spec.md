# trace-span-read-model Specification

## Purpose

Defines a bounded read-only projection for inspecting finalized hierarchical trace metadata and spans associated with one explicitly identified registered run.

## Requirements

### Requirement: Explicit registered-run lookup
The read model SHALL accept one explicit registered run identifier and SHALL return either the matching finalized trace summary or a typed unavailable result. It MUST NOT choose a run from a Goal, Scenario, prompt, diagnostic string, latest-run heuristic, or other inferred correlation.

#### Scenario: Finalized trace is available
- **WHEN** a caller requests the trace summary for an explicitly identified registered run whose trace has finalized
- **THEN** the read model SHALL return its schema version, trace identities, available run correlation, span count, and projection diagnostics without changing the registered trace or deriving a run-level outcome from span outcomes

#### Scenario: Run or finalized trace is unavailable
- **WHEN** the requested run is unknown or its finalized trace is unavailable
- **THEN** the read model SHALL return a typed unavailable result with diagnostics and SHALL NOT fabricate an empty successful trace

### Requirement: Stable cursor-paged span projection
The read model SHALL project every span of one finalized trace into a deterministic total order and assign a read-model-local sequence. A cursor SHALL be bound to the run and finalized trace identity, and a page SHALL contain only spans whose sequence is strictly greater than the cursor sequence.

#### Scenario: Caller reads all pages
- **WHEN** a caller follows returned cursors without changing the run, trace identity, or query
- **THEN** each span SHALL appear exactly once in deterministic sequence order and the final page SHALL report no further span

#### Scenario: Cursor belongs to another trace
- **WHEN** a caller supplies a cursor bound to another run or finalized trace identity
- **THEN** the query SHALL fail closed with a typed cursor-mismatch result and SHALL NOT silently restart or cross trace boundaries

### Requirement: Bounded typed span filters
Span queries SHALL support only explicit typed exact-match filters over stable span contract fields. Unsupported fields, free-form expressions, semantic reason parsing, and implementation-name matching MUST be rejected rather than interpreted.

#### Scenario: Exact stable filters are supplied
- **WHEN** a caller filters by supported stable values such as span name, layer, component, outcome, or parent span identity
- **THEN** the page SHALL contain only spans matching every supplied filter while preserving the trace's stable query sequence

#### Scenario: Free-form query is supplied
- **WHEN** a caller supplies a prompt, expression, diagnostic-text predicate, private method name, or unsupported filter field
- **THEN** the read model SHALL reject the request and SHALL NOT convert that text into query or Runtime authority

### Requirement: Hierarchy and observability semantics remain honest
Returned spans SHALL preserve their recorded span identity, parent identity, timing, attribution, outcome, attributes, and events. Structural observability outcome MUST remain diagnostic and MUST NOT be represented as Runtime Result, action success, Goal completion, satisfaction, or recovery success.

#### Scenario: Failed child is queried
- **WHEN** a recorded child span has outcome `FAILED` while its parent and Runtime lifecycle have different outcomes
- **THEN** the query SHALL return the recorded child and parent outcomes independently without rewriting either or inferring a Runtime Result

### Requirement: Query is harmless and transport-independent
Repeated reads SHALL be idempotent and SHALL NOT dispatch actions, transition the FSM, start or continue a Run, invoke recovery, update WorldBelief or GoalEvidence, select a Scenario, or orchestrate another Run. This capability SHALL remain an in-process read model and SHALL NOT add or change a DriverHost wire operation.

#### Scenario: Trace and spans are queried repeatedly
- **WHEN** one or more callers repeat summary and span queries before and after cursor exhaustion
- **THEN** Runtime state, action count, recovery state, GoalEvidence, capture state, and the existing Protocol v1 wire method set SHALL remain unchanged
