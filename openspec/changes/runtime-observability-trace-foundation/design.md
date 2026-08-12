## Context

The Runtime already exposes an Agent-owned, flat `TraceEvent` stream whose purpose is semantic cause/effect evidence. The Harness separately owns `TraceCaptureSession`, environment-call records, physical artifacts, append-only `ITraceCaptureStore` persistence, and scenario catalog/replay assets. Neither contract represents nested execution spans, operation duration, stable layer/component attribution, or a listener-isolated view of one end-to-end invocation.

The approved architecture gate classifies this as `FIT_WITH_BOUNDED_OBSERVABILITY_DELTA`: Runtime components may emit BCL diagnostic activities at their existing responsibility boundaries, while the Harness alone listens, buffers, validates, projects, and persists hierarchical diagnostics. The semantic Runtime spine, public semantic contracts, authority, and mutable-state ownership remain frozen.

## Goals / Non-Goals

**Goals:**

- Emit hierarchical activities for the nine approved boundaries without changing operation inputs, decisions, results, retries, or control flow.
- Attribute every emitted span to a stable layer and stable component identifier that is independent of CLR type/method names.
- Give the Harness one per-run mutable recorder owner that freezes recorded activities into immutable, versioned `TraceRun`, `TraceSpan`, and `ObservabilityEvent` values.
- Preserve parent-child relationships across asynchronous execution and represent elapsed time using monotonic offsets/durations.
- Represent operation outcomes explicitly without redefining semantic success, Goal completion, or recovery authority.
- Isolate listener, recording, validation, projection, and persistence failures from Runtime behavior.
- Persist finalized trace data through the existing append-only Harness capture/store boundary.
- Enable behavior-level scenario assertions over spans, attribution, required events, and failure boundaries.

**Non-Goals:**

- Replacing, extending, or parsing the existing Agent-owned `TraceEvent` semantic trace.
- Merging `TraceRun` with `TraceCaptureSession` or making Runtime own Harness data.
- Changing `Agent`, `Container`, `Traversal`, `Recovery`, `IEnvironment`, GoalEvidence, action authorization, or completion authority.
- Exposing private call order, diagnostic text, exact elapsed times, or CLR implementation names as stable contracts.
- Adding a Provider framework, capability registry, Brain, Planner, FSM, graph, metrics backend, remote exporter, or general tracing subsystem.

## Decisions

### 1. Runtime emits through one BCL ActivitySource seam

Runtime shall use a narrowly scoped helper over `System.Diagnostics.ActivitySource`. It owns only the stable source identity, activity names, attribution tags, event IDs, and no-throw start/stop/outcome operations. It holds no per-run trace buffer and references no Harness type.

Required span boundaries and stable attribution are:

| Boundary | Layer | Stable component ID |
|---|---|---|
| Runtime invocation | `ORCHESTRATION` | `runtime.invocation` |
| Agent execution | `AGENT` | `agent.execution` |
| Intent execution | `ORCHESTRATION` | `intent.execution` |
| Container refresh | `CONTAINER` | `container.refresh` |
| Traversal execution | `TRAVERSAL` | `traversal.execution` |
| Environment observation | `ENVIRONMENT` | `environment.observe` |
| Environment action execution | `ENVIRONMENT` | `environment.execute` |
| Recovery attempt | `RECOVERY` | `recovery.attempt` |
| External capability invocation | `CAPABILITY` | `capability.invocation` |

The complete reserved layer taxonomy is `ORCHESTRATION`, `AGENT`, `STARTUP`, `WORLD`, `CONTAINER`, `TRAVERSAL`, `RECOVERY`, `ENVIRONMENT`, `CAPABILITY`, and `HARNESS`. A span must use a member of this taxonomy. Component IDs are explicit constants and must not be derived from namespaces, CLR type names, method names, or diagnostic strings.

Activities follow `Activity.Current` for parentage across async calls. Call sites that begin the actual operation own the corresponding activity lifetime. Instrumentation wraps existing operations in `try/finally` solely to close the activity and record an observability outcome; it cannot alter exception propagation or returned values.

Alternative considered: inject a recorder or tracer interface into Runtime components. Rejected because it would reverse the Runtime-to-Harness dependency boundary and create a new component dependency in the semantic spine.

### 2. Spans describe timed operations; events describe point evidence

A `TraceSpan` represents one bounded operation with identity, parent identity, stable name, layer, component, monotonic start offset, monotonic duration, explicit outcome, and immutable attributes/events. An `ObservabilityEvent` represents a point occurrence inside one span with a stable event ID, monotonic offset, and immutable structured attributes. Events do not open a nested lifetime and do not replace semantic `TraceEvent` values.

The observability outcome vocabulary is structural diagnostics only: `SUCCEEDED`, `FAILED`, `CANCELLED`, or `UNKNOWN`. It reports how the instrumented operation terminated and must never be interpreted as semantic action success, Goal satisfaction, branch exhaustion, or recovery success. `UNKNOWN` is retained when the listener did not receive sufficient lifecycle evidence; no outcome is inferred from free-form diagnostic text.

Alternative considered: emit every operation as a flat event. Rejected because it cannot prove hierarchy, duration closure, or a failure boundary. Alternative considered: use semantic result enums as span outcomes. Rejected because it would duplicate and potentially redefine existing authorities.

### 3. Harness owns one recorder and the immutable TraceRun projection

For each run, the Harness creates a `RuntimeTraceRecorder` before invoking Runtime and disposes/finalizes it afterward. The recorder is the sole owner of its concurrent mutable activity/event buffers. It listens only to the approved ActivitySource and accepted trace ID, records activity lifecycle data, then freezes exactly once into:

- `TraceRun`: schema version, run/trace correlation, explicit run observability outcome, immutable root-span collection, and projection diagnostics;
- `TraceSpan`: immutable span identity/parentage, stable attribution, timing, outcome, attributes, and events;
- `ObservabilityEvent`: immutable stable event ID, containing span identity, timing offset, and structured attributes.

All persistent records carry an explicit schema version. Collections are immutable and materialization produces a new snapshot; published records are never edited. `TraceRun` is not `TraceCaptureSession`: the former is finalized hierarchical diagnostic data, while the latter owns the capture/environment-asset lifecycle. Runtime never constructs, retains, or persists either Harness model.

Alternative considered: extend Agent's `List<TraceEvent>` with spans. Rejected because semantic trace ownership and observability recording have different responsibilities and failure modes.

### 4. Projection preserves hierarchy and monotonic elapsed time

Projection keys relationships with BCL trace/span/parent identifiers, not list order. A child may finish before its parent; finalization builds the hierarchy after recording and validates one root invocation, parent existence within the accepted run, absence of cycles, and unique span identities. Out-of-source and out-of-run activities are ignored.

Durations and event/start offsets are converted from monotonic elapsed values to non-negative nanoseconds using overflow-safe conversion. Every child interval must be contained by its parent interval within conversion tolerance, and events must fall within their containing span. Wall-clock start metadata may be retained for human correlation but is not the duration source and is excluded from deterministic equality.

Malformed, orphaned, cyclic, negative, or unclosed records are preserved only as projection diagnostics and cannot be silently represented as a valid closed hierarchy. Projection itself does not rewrite Runtime outcomes.

Alternative considered: reconstruct hierarchy from callback or collection order. Rejected because asynchronous operations and concurrent callbacks make that order non-semantic.

### 5. Listener and observability failures are fail-open for Runtime

With no listener, activity creation is a no-op and Runtime behavior remains identical. Runtime emission helpers and Harness callbacks must not allow listener exceptions to escape into an instrumented operation. Recorder faults are latched as Harness diagnostics; subsequent Runtime dispatch, retry, observation, verification, recovery, GoalEvidence, and final result remain untouched.

Finalization or persistence failure is reported separately from the Runtime result. A caller that requires a trace may fail its Harness/tooling operation after Runtime returns, but it cannot retroactively change Runtime state or cause redispatch.

### 6. Persistence composes with the existing capture/store boundary

A finalized `TraceRun` is attached as an optional typed, immutable member of the Harness capture bundle and serialized as a versioned trace artifact by the existing `ITraceCaptureStore` operation. Existing capture IDs remain the append-only publication key; existing published captures cannot be overwritten. Captures without a hierarchical trace remain readable, and older capture data is not upgraded by inventing spans.

No new Runtime persistence port or generic repository is introduced. Persistence errors use the existing Harness persistence result/failure path.

Alternative considered: add an independent Runtime trace store. Rejected because persistence is Harness ownership and a second store would duplicate append-only lifecycle authority.

### 7. Scenario conformance asserts stable structure, not incidental implementation

Harness test utilities shall support assertions for:

- existence and closure of named stable spans;
- valid layer and component attribution;
- parent-child ancestry required by a Scenario;
- required stable events and structured fields;
- the span at which failure/cancellation was observed and closure of its ancestors.

They shall not expose exact duration equality, private-method ordering, CLR names, free-form diagnostic strings, or collection callback order as acceptance contracts. Structural replay comparison normalizes generated IDs and excludes wall-clock timestamps and exact elapsed values while retaining span names, hierarchy, attribution, outcomes, and required event IDs.

## Risks / Trade-offs

- [Global `ActivityListener` callbacks can observe unrelated sources or throw] → Filter by the stable source and trace ID, scope the listener per Harness run, make callbacks no-throw, and dispose deterministically.
- [Instrumentation volume increases allocation and latency when enabled] → Emit only the nine approved operation boundaries, rely on `ActivitySource.HasListeners` behavior, and avoid payload copies or diagnostic strings.
- [Async callbacks can arrive out of order] → Materialize from stable identifiers at finalization rather than callback order.
- [Observability outcomes could be mistaken for semantic results] → Use a separate narrow vocabulary and mechanically forbid it from driving dispatch, retry, GoalEvidence, or completion.
- [Persistence schema evolution can corrupt replay assumptions] → Version every trace record, fail closed on unsupported versions, and never synthesize missing historical spans.
- [Instrumentation can drift toward private implementation details] → Freeze public activity/event names, layers, and component IDs; scenario tests assert only those stable contracts.

## Migration Plan

1. Add the Runtime ActivitySource helper and no-op/listener-isolation tests.
2. Instrument the approved boundaries without changing signatures or semantic results.
3. Add Harness immutable models, recorder, projection validation, and structural normalization tests.
4. Compose optional `TraceRun` persistence through the existing capture bundle/store and prove backward readability plus append-only behavior.
5. Add scenario conformance assertions and one end-to-end traced Scenario covering success and failure boundaries.
6. Run architecture guards, full regression, consistency, and strict OpenSpec validation. Rollback removes instrumentation and optional trace attachment; existing captures and semantic Runtime behavior remain valid.

## Open Questions

None. Any request for new span boundaries, semantic payload interpretation, remote export, metrics aggregation, or a general capability/provider registry requires a separate change and gate.
