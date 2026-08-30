## Why

The graduated observability foundation (`runtime-observability-trace-foundation`) deliberately deferred four instrumentation boundaries — Runtime invocation root, Intent execution, Recovery attempt, and external capability invocation — until their owning paths became active. Those paths are now active in production (DriverHost run coordinator, Strategy/Intent open-world execution, Agent Recovery, semantic capability selection), so FDP-level decision provenance stays invisible exactly where real runs need it, and production `TraceRun.TraceId` / projected `CorrelationId` remain null. An audit also found that per-run recorder capture is not run-scoped: because `ActivityListener` is process-global, concurrent runs (multiple devices) and parallel test classes pollute each other's traces.

## What Changes

- Activate the caller-owned `runtime.invocation` root span in the DriverHost run coordinator: the whole run (including the Agent) becomes one closed trace, and the recorder carries the run's real W3C trace id, making `TraceRun.TraceId` and projected `CorrelationId` non-null in production.
- Activate the `recovery.attempt` boundary at the Recovery mechanism seam (structural outcome only — dispatch outcome never becomes recovery-success evidence; decision authority stays with the Agent).
- Activate the `capability.invocation` boundary at the Agent's semantic capability selection/execution seam (Agent keeps sole selection authority; the span is structural).
- Activate the `intent.execution` boundary at the Strategy/Intent open-world execution seam (the multi-stage intent trigger identified by the foundation spec is present).
- Add structured point-event emission (attributes on events) and a minimal `decision.*` vocabulary for new decision events; existing keys and the `TraceRun` schema v1 stay unchanged (no **BREAKING** changes).
- Rename the Agent-owned semantic journal type `TraceEvent` to `DecisionRecord` (vocabulary-only, zero behavior change): `Trace` = OTel-style causal-chain protocol carrier, `Event` = point occurrence attached to a trace, `DecisionRecord` = Agent-internal semantic decision journal entry. The replay-corpus `TraceEventAsset` type is unrelated and unchanged.
- Scope per-run recorder capture to one W3C trace id: activities outside the run's trace are skipped with a Harness diagnostic. Fixes multi-run/parallel-test pollution and matches the "single per-run recorder owner" intent.
- Update conformance: exercised active boundaries SHALL be present in captured runs; unexercised or un-activated boundaries SHALL NOT be fabricated (existing anti-fabrication principle unchanged).

## Capabilities

### New Capabilities

None (all behavior folds into the three modified capabilities below).

### Modified Capabilities

- `runtime-activity-emission`: replace the five-boundary required set and the four deferral receipts with the activated boundary set (runtime.invocation root, recovery.attempt, capability.invocation, intent.execution) and structured decision-event emission.
- `hierarchical-trace-projection`: extend the single per-run recorder contract to run-scoped capture (one W3C trace id per run; foreign traces excluded with diagnostics).
- `scenario-observability-conformance`: exercised active boundaries become required span evidence; incidental/absent deferred boundaries remain forbidden to fabricate.

## Impact

- `src/UniClaw.Runtime/Observability/` — emission seam: `AddEvent` overload with attributes; no API removal.
- `src/UniClaw.Runtime/Model/DecisionRecord.cs` (renamed from `TraceEvent.cs`): vocabulary-only rename of the Agent semantic journal type.
- `src/UniClaw.Runtime/Recovery/` — `recovery.attempt` emission (mechanism-only, no decision authority change).
- `src/UniClaw.Runtime/Agent/` — `capability.invocation` emission at capability selection/execution.
- `src/UniClaw.Runtime/Planning/IntentExecution.cs` — `intent.execution` emission at the open-world intent seam.
- `src/UniClaw.Runtime.DriverHost/Execution/RunExecutionCoordinator.cs` — caller-owned `runtime.invocation` root span; recorder now observes a real trace id (projected `CorrelationId` becomes non-null).
- `src/UniClaw.Runtime.Harness/RuntimeTraceRecorder.cs` — run-scoped capture by W3C trace id; trace-id derivation from first recorded span (already landed as an in-model correction, see design.md).
- `src/UniClaw.Runtime.DriverHost/Projection/RuntimeEventProjector.cs` — consumes the now-non-null `CorrelationId` (no contract change).
- `tests/UniClaw.Runtime.Tests/` — conformance additions for the activated boundaries and run-scoped capture.
- No new external package or service; the OTel-aligned model stays schema-v1, and OTLP export / sampling / Links remain out of scope.