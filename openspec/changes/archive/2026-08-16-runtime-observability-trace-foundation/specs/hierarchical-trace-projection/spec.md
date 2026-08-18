## ADDED Requirements

### Requirement: Harness-owned immutable versioned trace model
The Harness SHALL define immutable, schema-versioned `TraceRun`, `TraceSpan`, and `ObservabilityEvent` records. Runtime SHALL NOT create, own, mutate, or persist these records.

#### Scenario: Recorder finalizes a run
- **WHEN** the per-run Harness recorder finalizes accepted activities
- **THEN** it SHALL return a new immutable `TraceRun` containing immutable spans and events with an explicit supported schema version

#### Scenario: Published projection is inspected
- **WHEN** a consumer reads a finalized `TraceRun`
- **THEN** the consumer SHALL NOT be able to mutate its spans, events, attributes, hierarchy, timings, or outcome

### Requirement: Single per-run mutable recorder owner
The Harness SHALL use one per-run `RuntimeTraceRecorder` as the sole mutable owner of activity/event buffers, and finalization SHALL freeze that state exactly once without transferring mutable ownership to Runtime, Agent, Container, Traversal, or Environment.

#### Scenario: Concurrent activity callbacks arrive
- **WHEN** callbacks for one accepted trace arrive concurrently or out of completion order
- **THEN** the recorder SHALL collect them safely and final projection SHALL derive relationships from trace/span identifiers rather than callback order

#### Scenario: Finalization is repeated
- **WHEN** a caller attempts to finalize the same recorder more than once
- **THEN** the Harness SHALL fail the repeated finalization without mutating the first immutable `TraceRun`

### Requirement: Span and event separation
A `TraceSpan` SHALL represent a timed operation lifetime and an `ObservabilityEvent` SHALL represent a point occurrence within one containing span. Observability events SHALL NOT be projected as sibling operation spans, and existing semantic `TraceEvent` records SHALL NOT be parsed or converted into observability events.

#### Scenario: Point evidence is emitted within a traversal operation
- **WHEN** an approved stable event is emitted while a Traversal activity is active
- **THEN** projection SHALL attach an `ObservabilityEvent` to that containing span without creating an additional timed operation

#### Scenario: Agent semantic trace contains a reason string
- **WHEN** the existing Agent-owned `TraceEvent` stream contains diagnostic or semantic reason text
- **THEN** hierarchical projection SHALL NOT parse that text to manufacture a span, event, attribution, or outcome

### Requirement: Parent-child hierarchy preservation
Projection from `Activity` records to `TraceRun` SHALL preserve trace, span, and parent identifiers for recorded Runtime spans, SHALL allow the root context to be caller-owned, and SHALL expose duplicate or missing recorded-parent relationships to Harness conformance rather than converting them into semantic evidence.

#### Scenario: Child completes before parent
- **WHEN** an asynchronous child activity stops before its parent and callbacks are recorded out of order
- **THEN** projection SHALL place the child under the parent using identifiers and SHALL preserve one closed hierarchy

#### Scenario: Parent record is missing
- **WHEN** an accepted child record references a parent that is absent from the accepted run
- **THEN** Harness conformance SHALL report the missing recorded parent and SHALL NOT treat the relationship as structurally closed

### Requirement: Monotonic timing projection
`TraceSpan` durations and span/event offsets SHALL be derived from monotonic elapsed-time evidence, SHALL be persisted as non-negative nanoseconds, and SHALL keep every child interval and event within its containing span subject only to documented conversion tolerance.

#### Scenario: Wall clock changes during a run
- **WHEN** system wall time moves forward or backward while activities execute
- **THEN** projected elapsed durations and offsets SHALL remain non-negative and monotonic because wall time is not their duration source

#### Scenario: Invalid negative elapsed value is received
- **WHEN** recorded lifecycle data would project a negative duration or offset
- **THEN** projection SHALL fail validation for that record and SHALL NOT clamp it into apparently valid timing evidence

### Requirement: Explicit projected outcomes
Every projected span SHALL retain the explicit observability outcome emitted at its boundary, and missing or incomplete lifecycle evidence SHALL project as `UNKNOWN` or a validation diagnostic rather than as success.

#### Scenario: Failed child operation is recorded
- **WHEN** a child operation closes with `FAILED` and its parent handles that failure
- **THEN** projection SHALL retain the failed child outcome and the independently emitted parent outcome without rewriting either one

#### Scenario: Stop evidence is missing
- **WHEN** the recorder sees a started activity with no valid closure evidence
- **THEN** the Harness SHALL NOT project that span as `SUCCEEDED`

### Requirement: Recorder and projection failure isolation
Recorder callback, projection, and validation failures SHALL be represented as Harness diagnostics and SHALL NOT escape into, retry, or rewrite an active Runtime invocation.

#### Scenario: Recorder rejects malformed metadata
- **WHEN** malformed activity metadata causes a projection diagnostic after Runtime execution
- **THEN** the Runtime result and semantic trace SHALL remain unchanged and the Harness SHALL report the trace failure separately

### Requirement: Stable structural comparison boundary
Harness conformance SHALL rely on stable span names, hierarchy, layer/component attribution, outcomes, and stable event IDs while excluding wall-clock timestamps, exact durations, private implementation order, CLR names, and diagnostic strings from acceptance.

#### Scenario: Same scenario is replayed twice
- **WHEN** identical deterministic inputs produce equivalent Runtime behavior in two traced replays
- **THEN** their observability acceptance SHALL remain equivalent when wall times and exact elapsed durations differ
