# Tasks — observability-trajectory-timing

## 1. Taxonomy extension

- [x] 1.1 Extend `ObservabilityComponent` (Runtime) with `traversal.plan-step`, `startup.bootstrap`, `perception.capture`, `perception.vision`, `perception.fusion`, `perception.canonicalize`, `perception.admission`
- [x] 1.2 Mirror the extended component set in `TraceSpanReadModelVocabulary.Components` (DriverHost read-model validation)

## 2. Perception-stage emission

- [x] 2.1 Emit `perception.capture` around capture acquisition (PhysicalEnvironment, nested under `environment.observe`) — structural outcome
- [x] 2.2 Emit `perception.vision` around perceive + enrich + structured acquisition (PhysicalEnvironment) — structural outcome
- [x] 2.3 Emit `perception.fusion` around `SemanticEvidenceFusionPipeline.ResolveAndFuseAsync` — structural outcome
- [x] 2.4 Emit `perception.canonicalize` around the observation→context projection (SemanticCapabilityEnvironment) — structural outcome
- [x] 2.5 Emit `perception.admission` around `SemanticCapabilityRuntime.EvaluateAsync` (SemanticCapabilityEnvironment) — structural outcome

## 3. Startup + plan-step traversal emission

- [x] 3.1 Emit `startup.bootstrap` (`STARTUP`) around `Startup.StartAsync` — structural outcome
- [x] 3.2 Emit `traversal.plan-step` per step in `ExecuteStepCoreAsync` with `step.id` — structural outcome

## 4. Iteration / settle granularity events

- [x] 4.1 Emit `iteration.start` events (`decision.iteration`, `decision.duration_ns`) per semantic-loop iteration on the Agent span (bounded ≤ maxIterations+1)
- [x] 4.2 Emit `settle.round` events (`settle.duration_ns`) per settle round on the `LoweredAction` span (bounded) — emission implemented; **conformance deferred**: settle requires a null-switch-window + semantic-envelope composition that the fake environments do not provide (deferred note, see 6.4)

## 5. Decision event anchors

- [x] 5.1 Emit `decision.navigation` events with `decision.reason` on the carrying span when active (journal remains authoritative)
- [x] 5.2 Emit `decision.viewport` events with `decision.reason` when a span is active (×2 viewport decision sites)
- [x] 5.3 Emit `decision.trap` events with `decision.reason` when a span is active (Agent.cs viewport escalation + Agent.Recovery drift sites)

## 6. Conformance & tests

- [x] 6.1 Perception conformance: semantic pipeline (`canonicalize` + `admission`) nested under the observe boundary; adapter composition (`capture` + `vision`) via minimal fake sources
- [x] 6.2 Plan-step conformance: deterministic plan-step run asserts `traversal.plan-step` presence
- [x] 6.3 Startup conformance: golden run asserts `startup.bootstrap` presence
- [x] 6.4 Iteration-event conformance: multi-iteration run asserts bounded `iteration.start` events with `decision.iteration` / `decision.duration_ns`; settle-round conformance deferred (see 4.2)
- [x] 6.5 Anti-fabrication: golden run without perception/startup/plan paths asserts absence (perception stages + plan-step)
- [x] 6.6 Timeline tests consume richer span data (existing RunTimeline tests stay green)

## 7. Verification

- [x] 7.1 `dotnet build src/UniClaw.Runtime.sln` (0 errors)
- [x] 7.2 `dotnet test src/UniClaw.Runtime.sln` (all green; guards included)
- [x] 7.3 `scripts/check-consistency.sh` ALL PASS

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime/Capabilities/Perception/` | `openspec/changes/observability-trajectory-timing/design.md` |
| `src/UniClaw.Runtime/Startup/` | `openspec/changes/observability-trajectory-timing/design.md` |
| `src/UniClaw.Runtime/Traversal/` | `openspec/changes/observability-trajectory-timing/design.md` |
| `src/UniClaw.Runtime/Agent/` | `openspec/changes/observability-trajectory-timing/design.md` |
| `src/UniClaw.Runtime/Observability/` | `openspec/changes/observability-trajectory-timing/design.md` |
| `src/UniClaw.Runtime.DriverHost/Model/` | `openspec/changes/observability-trajectory-timing/design.md` |
| `tests/UniClaw.Runtime.Tests/` | `openspec/changes/observability-trajectory-timing/design.md` |