## Why

The graduated observability surface (9 approved components) covers 8 functional segments with real durations, but the full functional trajectory still has timing black holes where problems actually show up on real devices: the entire Perception chain (capture → vision inference → fusion → canonicalization → semantic admission) is zero-instrumented, Startup and Reconcile have no spans, the Traversal plan-step path (`ExecuteStepCoreAsync`) bypasses the only traversal span, navigation / viewport / trap / recovery-orchestration decisions have semantic records but no timing, and per-iteration / post-action settle granularity is hidden inside bigger spans. As a result the timeline read model (a) can answer "where time went" only at coarse boundaries, and FDP localization inside perception — the domain that produced the recent Settings/Display bounds failures — is still a replay-and-guess exercise.

## What Changes

- Add Perception-internal stage spans: `capture`, `vision.infer` (vision host request), `fusion`, `canonicalize`, `semantic.admission` — the highest-value timing + FDP granularity (no semantics moved; all structural outcomes, fail-open).
- Activate the declared-but-unused `STARTUP` layer: one `startup.bootstrap` span around `Startup.StartAsync`.
- Add `traversal.plan-step` instrumentation on the plan-step execution path (`ExecuteStepCoreAsync`) so deterministic PlanRun traversal steps are timed like the semantic dispatch path.
- Add per-iteration and post-action-settle granularity: iteration decision events (`iteration.start` with `decision.iteration` / `decision.duration_ns`) on the Agent span, and settle-round events (`settle.round` / `settle.duration_ns`) on the `LoweredAction` span — reuses the already-landed structured event capability.
- Attach navigation / viewport / trap decision events with `decision.*` attributes to the carrying spans (Agent / Traversal) so the decision trajectory gains timing anchors.
- No **BREAKING** changes: `TraceRun` schema v1, existing 9 components, wire surface, and ownership all unchanged; the new component ids extend the closed taxonomy.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `runtime-activity-emission`: add the Perception-stage, Startup, and plan-step traversal boundaries; extend the component taxonomy with the new contract values.
- `scenario-observability-conformance`: exercised Perception / Startup / plan-step boundaries become assertable (and required when exercised); the unexercised-anti-fabrication principle is preserved.

## Impact

- `src/UniClaw.Runtime/Capabilities/Perception/*` — first observability emission in this layer (stage spans around capture, vision request, fusion, canonicalization, admission; structural only).
- `src/UniClaw.Runtime/Startup/Startup.cs` — `startup.bootstrap` span (`STARTUP` layer).
- `src/UniClaw.Runtime/Traversal/Traversal.cs` — plan-step span + settle-round events.
- `src/UniClaw.Runtime/Agent/Agent.SemanticRun.cs` / `Agent.OpenWorld.cs` — iteration events + navigation/viewport/trap decision events.
- `src/UniClaw.Runtime/Observability/RuntimeObservability.cs` — no API change (event attributes already landed); taxonomy constants extended.
- `src/UniClaw.Runtime.DriverHost/Model/TraceSpanReadModel.cs` — component vocabulary extended (read-model validation set).
- `tests/UniClaw.Runtime.Tests/` — conformance for the new boundaries; timeline tests consume the richer span data.
- No new external package or service; zero OTLP/sampling/Links.