# Design — observability-emission-expansion

## Context

See proposal.md — Why. Current state (evidence, not assumption):

- Emission seam: `RuntimeObservability` (`src/UniClaw.Runtime/Observability/RuntimeObservability.cs`) — one BCL `ActivitySource` named `UniClaw.Runtime`, `StartSpan` sets `layer`/`component` tags, `Complete` sets `outcome`, `AddEvent(name)` is name-only.
- Recorder: `src/UniClaw.Runtime.Harness/RuntimeTraceRecorder.cs` — process-global `ActivityListener` on that source; freezes one `TraceRun` (schema v1). Serialization-order constraint: `layer`/`component` are set after `StartActivity` returns, so they are only observable in the stop callback (already corrected).
- Production wiring: `RunExecutionCoordinator` (`RunExecutionCoordinator.cs`) creates `new RuntimeTraceRecorder(runId)` (no trace id) and schedules `ExecuteRunAsync`/`ExecuteStrategyRunAsync`; projected `CorrelationId = trace.TraceId` is null today.
- Multi-run hazard: `ActivityListener` is process-wide; concurrent runs (or parallel test classes) can record each other's spans into one recorder.
- Deferral triggers from the archived foundation spec are all present: DriverHost runs (`runtime.invocation` — caller-owned), `Agent.Recovery` active (`recovery.attempt`), semantic capability selection (`capability.invocation`), StrategyContract compiler + `IntentExecution.RunStrategyOpenWorldAsync` (`intent.execution`).

## Goals / Non-Goals

Goals: activate the four deferred emission boundaries; caller-owned root span with real per-run W3C trace id; run-scoped recorder capture; structured decision events; active-boundary conformance enforcement. Everything stays fail-open, structural-outcome, schema-v1, zero new dependencies.

Non-Goals: `TraceRun` schema v2 or OTLP export; sampling/retention; Links/Ref taxonomy (EvidenceRef etc.); renaming existing attribute keys; changing ownership or semantic outcomes. The frozen wire/DTO surface (`RunSnapshot.cs`, `RuntimeEventEnvelope.cs`, `RuntimeEventKind.cs` — byte-identity guarded by `HarnessSourceShapeGuardTests`) retains its historical `TraceEvent` prose: the DecisionRecord vocabulary applies everywhere else, and touching frozen bytes would require a separate gated contract-surface update.

## Decisions

### D1 — Caller-owned root span location (runtime.invocation)
**Decision:** `RunStartRequest.StartRun` / `StartStrategyRun` open one `RunExecution` root activity (`ORCHESTRATION`/`runtime.invocation`) synchronously after recorder creation and BEFORE scheduling Agent work; the executors close it with a structural outcome in `finally`. The recorder receives the run's trace id implicitly through Activity context.
**Alternatives:** Runtime opens a root at `RunSemanticGoalAsync` — rejected: the archived spec fixes `runtime.invocation` as caller-owned and forbids Runtime-owned root scope. DriverHost opens it at `StartRun` — rejected: rejection paths and async scheduling gaps could fabricate a root for non-runs.
**Why this works:** the coordinator is the caller (authorized root owner), the Activity is sampled by the run's recorder, and the recorder's trace-id derivation picks up this trace id (first recorded activity of the run).

### D2 — recovery.attempt emitter
**Decision:** the `Recovery` mechanism component emits `RecoveryAttempt` spans around `ExecuteNextAsync`/`ExecuteActionAsync` dispatch (mechanism seam).
**Alternatives:** Agent emits around recovery orchestration — rejected: Agent already owns the semantic `TraceEvent` recovery journal (RecoveryId); a span there would duplicate and blur decision vs mechanism. No new component — rejected: over-engineering for one span.
**Why:** 机制归组件、决策归 Agent (裁决 8/10) — the timed operation is the dispatch; the decision stays in Agent trace. Outcome = structural dispatch closure only (never recovery success — 裁决 10).

### D3 — capability.invocation emitter
**Decision:** the Agent emits `InvokeCapability` (`CAPABILITY`/`capability.invocation`) at the boundary where it selects and executes a semantic/external capability (the semantic capability selected event site). The span is structural; selection decision is unchanged (Agent-owned, I-3).
**Alternatives:** Capabilities/Brain or Operator emit — rejected: capability components have no orchestration authority and must not self-trace decisions; Agent is the invocation boundary owner.
**Why:** the boundary the deferral named is the *invocation* from the Agent's executing loop into the capability — that is where FDP-relevant timing lives.

### D4 — intent.execution emitter
**Decision:** `IntentExecution.RunStrategyOpenWorldAsync` (Planning/) wraps its open-world step in `RunIntentOpenWorld` (`AGENT`/`intent.execution`).
**Alternatives:** StrategyContract compiler — rejected: compilation is not the multi-stage *execution* pressure. DirectiveExecution — rejected: it delegates to the same intent seam; one emitter avoids double spans.
**Why:** the foundation named "future multi-stage compiler pressure" — the compiler exists and drives this open-world execution; this is the seam.

### D5 — Run-scoped recorder capture
**Decision:** recorder captures only activities whose W3C `TraceId` equals the run's trace id (first recorded activity's trace id; the caller-supplied `TraceRun.TraceId` correlation value is preserved separately and never used as the filter). Foreign-trace activities are skipped and reported in `Diagnostics` at finalization.
**Alternatives:** per-span runId tag from the emission side — rejected: puts run context in the Runtime seam and couples emission to run identity. Recorder captures everything (status quo) — rejected: concurrent multi-device runs and parallel tests pollute each other, violating the single-per-run-owner contract.
**Race closure:** the caller-owned root is opened in `StartRun` synchronously, adjacent to recorder creation and BEFORE any Agent work is scheduled — the root is the recorder's first observed activity, so the claim never crosses a scheduling boundary. The residual window is a few instructions; the failure mode is a foreign-trace skip reported as a Harness diagnostic, never silent corruption. Test classes holding live recorders (conformance, perception diagnostics, coordinator-driven DriverHost tests) are serialized in one non-parallel xunit collection (`ObservabilityTraceEmitters`) so parallel test runs cannot pre-claim each other's scope.

### D6 — Structured events & vocabulary
**Decision:** `RuntimeObservability.AddEvent` gains an attributes overload (positional/name-value params, fail-open); new decision events use `decision.*` keys (start with `decision.reason`); recorded events carry attributes and their own monotonic offset (recorder wall→monotonic epoch mapping with documented conversion tolerance, clamped into the containing span).
**Alternatives:** rename existing keys — rejected: schema stays v1, no **BREAKING**. New key family outside `decision.*` — rejected: GAP-07 wants vocabulary convergence, not expansion.

## Risks / Trade-offs

- [Concurrent first-span trace race] → closed by the root's synchronous open in `StartRun` (adjacent to recorder creation, before any Agent scheduling); residual instruction-scale window fails as a Diagnostics skip, never silent; emitter test classes serialized in one non-parallel xunit collection.
- [Attribution read at stop means never-stopped spans have blank layer/component] → already the "missing closure evidence" semantics; conformance requires attribution only for asserted/closed spans.
- [Wall→monotonic event mapping under clock jumps] → single epoch capture at recorder start + cap at current monotonic + clamp into containing span; tolerance documented in code.
- [New emission fails open unexpectedly (no spans)] → same fail-open contract as the foundation; conformance can only require spans for exercised boundaries.

## Migration Plan

- Landing the recorder corrections (event timing/attributes, trace-id derivation, layer/component at stop) is backward-compatible: schema v1 unchanged, existing captures untouched (immutable, append-only). No rollback surface beyond reverting commits; no persisted data migration.
- Activating emission boundaries is additive; a run that never hits a boundary emits nothing for it (anti-fabrication preserved), so no existing scenario acceptance breaks.

## Open Questions

None that would change specs/approach/task breakdown.