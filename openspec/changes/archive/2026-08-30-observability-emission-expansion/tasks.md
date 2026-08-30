# Tasks — observability-emission-expansion

## 0. Decision-journal rename (vocabulary)

- [x] 0.1 Rename `Model/TraceEvent.cs` → `Model/DecisionRecord.cs` (type + file) and update its doc comment with the Trace/Event/DecisionRecord vocabulary
- [x] 0.2 Replace `TraceEvent` references across `src/` and `tests/` (Agent journal, `AgentStateSnapshot.Trace`, `RuntimeEventProjector`, Planning/Directive consumers, fixtures); keep `TraceEventAsset`/`TraceEventId`/`TraceEventType` (replay corpus, unrelated) untouched
- [x] 0.3 Verify behavior-identical: build green + affected test scopes pass (Agent / Planning / Scenario / Observability)

## 1. Emission seam

- [x] 1.1 Add an `AddEvent` overload with structured attributes to `RuntimeObservability` (fail-open; existing name-only overload unchanged)
- [x] 1.2 Add `decision.*` event-attribute usage site(s) when the first decision event ships (e.g., Agent capability selection decision event with `decision.reason`) — `capability.selected` event on the `InvokeCapability` span

## 2. Caller-owned root span (runtime.invocation)

- [x] 2.1 Open `RunExecution` root activity (`ORCHESTRATION`/`runtime.invocation`) at `ExecuteRunAsync` entry and close with structural outcome in `finally`
- [x] 2.2 Mirror the root activity in `ExecuteStrategyRunAsync` (strategy path)
- [x] 2.3 Keep rejection paths free of fabricated roots (no root for `StartRun`/`StartStrategyRun` failures) — rejection paths never reach the background executors

## 3. Deferred boundary activation

- [x] 3.1 Emit `RecoveryAttempt` (`RECOVERY`/`recovery.attempt`) around `Recovery.ExecuteNextAsync` and `ExecuteActionAsync` dispatch; outcome = structural dispatch closure only
- [x] 3.2 Emit `InvokeCapability` (`CAPABILITY`/`capability.invocation`) at the Agent capability selection/execution boundary
- [x] 3.3 Emit `RunIntentOpenWorld` (`AGENT`/`intent.execution`) around the open-world step in `IntentExecution.RunOpenWorldCoreAsync` (shared seam for `RunOpenWorldAsync` and `RunStrategyOpenWorldAsync` — one emitter, no double spans, per design D4)

## 4. Run-scoped recorder capture

- [x] 4.1 Scope `RuntimeTraceRecorder` capture to the run's W3C trace id (first recorded activity); skip foreign-trace activities with a `Diagnostics` entry
- [x] 4.2 Preserve caller-supplied `TraceRun.TraceId` correlation value; derive the run trace id only when the caller omitted it (already landed; conformance proof in `Recorder_TraceId_*`)
- [x] 4.3 Preserve event attributes and real event monotonic offsets in projection (recorder corrections already landed; covered by conformance)

## 5. Conformance & tests

- [x] 5.1 Golden end-to-end run asserts exercised active boundaries present (Agent + Container) and unexercised deferred boundaries absent
- [x] 5.2 Action-exercising run asserts Traversal boundary present
- [x] 5.3 Recovery exercise asserts `recovery.attempt` present only when recovery actually ran (+ golden absence when it did not)
- [x] 5.4 DriverHost coordinator test asserts one `runtime.invocation` root per accepted run and `TraceId`/`CorrelationId` driver non-null through the public read surface
- [x] 5.5 Concurrent-recorder test asserts run-scoped isolation (two trace ids → no cross-recording, skips reported in Diagnostics)
- [x] 5.6 Event timestamp/attribute conformance (explicit timestamps, order preserved, in-span)

## 6. Verification

- [x] 6.1 `dotnet build src/UniClaw.Runtime.sln` (0 errors)
- [x] 6.2 `dotnet test src/UniClaw.Runtime.sln` (all green; guards included)
- [x] 6.3 `scripts/check-consistency.sh` ALL PASS

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime/Observability/` | `openspec/changes/observability-emission-expansion/design.md` |
| `src/UniClaw.Runtime/Recovery/` | `openspec/changes/observability-emission-expansion/design.md` |
| `src/UniClaw.Runtime/Agent/` | `openspec/changes/observability-emission-expansion/design.md` |
| `src/UniClaw.Runtime/Planning/` | `openspec/changes/observability-emission-expansion/design.md` |
| `src/UniClaw.Runtime.DriverHost/Execution/` | `openspec/changes/observability-emission-expansion/design.md` |
| `src/UniClaw.Runtime.Harness/` | `openspec/changes/observability-emission-expansion/design.md` |
| `tests/UniClaw.Runtime.Tests/` | `openspec/changes/observability-emission-expansion/design.md` |