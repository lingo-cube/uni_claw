# hierarchical-trace-projection Specification

## Purpose
TBD - created by archiving change runtime-observability-trace-foundation. Update Purpose after archive.

## Requirements

### Requirement: Harness-owned immutable versioned trace model
The Harness SHALL define immutable, schema-versioned `TraceRun`, `TraceSpan`, and `ObservabilityEvent` records. Runtime SHALL NOT create, own, mutate, or persist these records.

#### Scenario: Recorder finalizes a run
- **WHEN** the per-run Harness recorder finalizes accepted activities
- **THEN** it SHALL return a new immutable `TraceRun` containing immutable spans and events with an explicit supported schema version

#### Scenario: Published projection is inspected
- **WHEN** a consumer reads a finalized `TraceRun`
- **THEN** the consumer SHALL NOT be able to mutate its spans, events, attributes, hierarchy, timings, or outcome

### Requirement: Single per-run mutable recorder owner
The Harness SHALL use one per-run `RuntimeTraceRecorder` as the sole mutable owner of activity/event buffers, and finalization SHALL freeze that state exactly once without transferring mutable ownership to Runtime, Agent, Container, Traversal, or Environment. The recorder SHALL scope its capture to one W3C trace id per run: activities outside that trace id SHALL be skipped and reported as Harness diagnostics rather than recorded into the run's `TraceRun`.

#### Scenario: Concurrent activity callbacks arrive
- **WHEN** callbacks for one accepted trace arrive concurrently or out of completion order
- **THEN** the recorder SHALL collect them safely and final projection SHALL derive relationships from trace/span identifiers rather than callback order

#### Scenario: Foreign trace activity is observed
- **WHEN** a process-global listener observes an activity whose trace id differs from the run's trace id
- **THEN** the recorder SHALL NOT include that activity in the run's `TraceRun` and SHALL append a diagnostic entry

#### Scenario: Finalization is repeated
- **WHEN** a caller attempts to finalize the same recorder more than once
- **THEN** the Harness SHALL fail the repeated finalization without mutating the first immutable `TraceRun`

### Requirement: Span and event separation
A `TraceSpan` SHALL represent a timed operation lifetime and an `ObservabilityEvent` SHALL represent a point occurrence within one containing span. Observability events SHALL NOT be projected as sibling operation spans, and existing Agent-owned semantic `DecisionRecord` journal records SHALL NOT be parsed or converted into observability events.

#### Scenario: Point evidence is emitted within a traversal operation
- **WHEN** an approved stable event is emitted while a Traversal activity is active
- **THEN** projection SHALL attach an `ObservabilityEvent` to that containing span without creating an additional timed operation

#### Scenario: Agent semantic trace contains a reason string
- **WHEN** the existing Agent-owned `DecisionRecord` journal contains diagnostic or semantic reason text
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
`TraceSpan` durations and span/event offsets SHALL be derived from monotonic elapsed-time evidence, SHALL be persisted as non-negative nanoseconds, and SHALL keep every child interval and event within its containing span subject only to documented conversion tolerance. Event offsets SHALL reflect the event's own point in time (mapped through the recorder's wall-to-monotonic epoch with documented conversion tolerance), not the containing span's start.

#### Scenario: Wall clock changes during a run
- **WHEN** system wall time moves forward or backward while activities execute
- **THEN** projected elapsed durations and offsets SHALL remain non-negative and monotonic because wall time is not their duration source

#### Scenario: Invalid negative elapsed value is received
- **WHEN** recorded lifecycle data would project a negative duration or offset
- **THEN** projection SHALL fail validation for that record and SHALL NOT clamp it into apparently valid timing evidence

#### Scenario: Two point events on one span
- **WHEN** one span carries two point events with distinct wall timestamps
- **THEN** their projected offsets SHALL preserve that order and SHALL each remain inside the containing span interval

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

### Requirement: Decision-journal naming vocabulary
The Agent-owned semantic journal type previously named `TraceEvent` SHALL be named `DecisionRecord`. Vocabulary: `Trace` SHALL denote the OTel-style causal-chain protocol carrier (`TraceRun` + Activity spans); `Event` SHALL denote a point occurrence attached to a trace (projected `RuntimeEventEnvelope` or a span `ObservabilityEvent`); `DecisionRecord` SHALL denote an Agent-internal semantic decision journal entry that is never itself an observability trace event. The rename SHALL be vocabulary-only: no schema, wire, persisted, or behavior change, and the replay-corpus `TraceEventAsset` type is unrelated and SHALL remain named as is.

#### Scenario: Agent journal is renamed
- **WHEN** the Agent records a semantic decision in its journal
- **THEN** the type is `DecisionRecord` and its records SHALL NOT appear as OTel trace events or as replay `TraceEventAsset` values

#### Scenario: Existing consumers still compile and behave identically
- **WHEN** a run executes after the rename with unchanged decision content
- **THEN** projected `RuntimeEventEnvelope` values and reconciliation behavior SHALL be byte-identical to the pre-rename run

### Requirement: Trace identity preserved from recorded evidence
When the caller does not supply a `TraceRun.TraceId`, the finalized `TraceRun` SHALL expose the run's actual W3C trace id derived from its recorded activities; the projected `CorrelationId` consumable by downstream projections SHALL NOT remain empty for a run that recorded span evidence.

#### Scenario: Coordinator omits the trace id
- **WHEN** a run records activities and the coordinator supplied no trace-id correlation value
- **THEN** the finalized `TraceRun` SHALL carry the recorded run's W3C trace id and downstream correlation SHALL be non-null
