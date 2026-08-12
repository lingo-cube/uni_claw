## Why

The graduated Runtime and Harness can capture semantic results and environment assets, but they cannot yet reconstruct one hierarchical execution with stable layer/component attribution and isolated timing/outcome evidence. A bounded observability foundation is needed so scenarios can prove cross-layer closure and failure boundaries without changing Runtime semantics or coupling Runtime to Harness state.

## What Changes

- Add a BCL `ActivitySource` emission seam at the active Agent, Container, Traversal, and Environment boundaries. Runtime invocation, Intent execution, Recovery attempt, and external capability invocation remain explicit deferred extension points until their owning paths supply executable pressure.
- Add Harness-owned, immutable, versioned `TraceRun`, `TraceSpan`, and `ObservabilityEvent` values plus per-run listener/recording state.
- Project recorded `Activity` data into a parent/child `TraceRun` with monotonic elapsed-time values, explicit outcomes, stable layer/component attribution, and listener-failure isolation.
- Persist the immutable `TraceRun` through the existing Harness append-only capture/store boundary without making Runtime own persistence.
- Add scenario conformance assertions for required spans, layer/component closure, required events, and failure boundaries while excluding exact-duration, private-method-order, and diagnostic-string assertions.
- Preserve the existing Agent-owned flat `TraceEvent` semantic trace and the existing Harness `TraceCaptureSession` capture lifecycle as distinct contracts.

## Capabilities

### New Capabilities

- `runtime-activity-emission`: Stable, behavior-neutral hierarchical observability emission at approved Runtime and external-call boundaries.
- `hierarchical-trace-projection`: Harness-owned immutable trace models and deterministic projection from recorded activities into one hierarchical run.
- `trace-run-persistence`: Append-only persistence integration for versioned `TraceRun` data through the existing Harness storage lifecycle.
- `scenario-observability-conformance`: Stable scenario assertions over observable hierarchy, attribution, events, outcomes, and failure boundaries.

### Modified Capabilities

None.

## Impact

- `src/UniClaw.Runtime/Observability/` and bounded call sites in Agent, Container, Traversal, and Environment adapters: `ActivitySource` emission only.
- `src/UniClaw.Runtime.Harness/`: Harness-owned listener/recorder, immutable trace projection values, validation, and persistence composition.
- `tests/UniClaw.Runtime.Tests/`: component, projection, isolation, scenario-conformance, architecture-guard, and regression coverage.
- Existing public semantic contracts, authority, ownership, execution results, `TraceEvent`, `TraceCaptureSession`, and `IEnvironment` remain unchanged.
- No new external package or service is required; emission uses BCL diagnostics APIs.
