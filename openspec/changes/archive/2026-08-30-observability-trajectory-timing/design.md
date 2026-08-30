# Design — observability-trajectory-timing

## Context

See proposal.md — Why. Current state (evidence):

- Emission inventory (post-`observability-emission-expansion`): 11 `StartSpan` sites across coordinator root, Agent (`RunSemanticGoal`, `InvokeCapability`), Container refresh, Traversal (`LoweredAction` — semantic dispatch path only), Recovery (`RecoveryAttempt` ×2), Intent open-world, Environment observe/execute. `TraceRun` schema v1; recorder supports structured events with real monotonic offsets; `CAPABILITY`/`STARTUP` layers exist in the taxonomy but `STARTUP` is unused.
- `Capabilities/` (Perception) has ZERO observability emission — the whole pipeline (capture → vision → fusion → canonicalization → admission) is hidden inside the single `environment.observe` span. Recent Settings/Display failure domain (bounds rounding in `SemanticObservationFactProjector` / Fusion) lives exactly there.
- Plan-run traversal (`ExecuteStepCoreAsync`) has no span — only the Agent semantic dispatch path (`ExecuteLoweredActionAsync`) is instrumented.
- Timeline read model (`RunTimelineProjector`, in-process, derived) already renders timed segments + ordered decision markers + stage duration summaries; richer spans feed it directly, no read-model change needed.

## Goals / Non-Goals

Goals: per-stage Perception timing; Startup bootstrap timing; plan-step traversal timing; per-iteration and settle-round granularity via structured events; decision events with timing anchors on carrying spans. Everything stays fail-open, structural-outcome, schema-v1, zero new packages.

Non-Goals: OTLP export / sampling / Links-Ref taxonomy; schema v2; merging the two timeline tracks by fabricated offsets; renaming frozen wire/DTO surfaces; any change to perception semantics, admission fail-closed behavior, or ownership.

## Decisions

### D1 — Perception stage seam and emitter ownership
**Decision:** one thin span per stage at the existing pipeline seam boundaries in the Perception capability: `capture` (around screenshot acquisition when it runs inside the pipeline), `vision` (around the Vision host inference request), `fusion` (around `SemanticEvidenceFusionPipeline.Fuse`/fusion), `canonicalize` (around canonical occurrence normalization), `admission` (around `SemanticCapabilityEnvironment` admission). Emitters live at the seam methods (fail-open); no stage logic is moved.
**Alternatives:** single opaque `perception.pipeline` span — rejected: the entire point is per-stage attribution (vision inference vs fusion vs canonicalization cost). Emission from `LocalVisionPerceptionSource` only — rejected: that covers vision only and leaves fusion/canonicalization/admission untimed.
**Why:** stage spans give both 耗时归因 and FDP granularity inside the exact domain that produced recent real-device failures, with zero permission/authority change (observability remains non-semantic; admission outcome still drives nothing via spans).

### D2 — plan-step traversal span
**Decision:** `ExecuteStepCoreAsync` emits `traversal.plan-step` per step with `step.id` attribute and structural outcome (Succeeded/Failed/Cancelled); the existing `LoweredAction` span (semantic dispatch path) is unchanged.
**Alternatives:** reuse `LoweredAction` on the plan path — rejected: the plan path executes its own execute+observe+verify and must not masquerade as the semantic lowered dispatch.
**Why:** PlanRun traversal steps become timed without touching traversal authority.

### D3 — iteration/settle granularity via events, not spans
**Decision:** per-iteration `iteration.start` events (`decision.iteration`, `decision.duration_ns`) on the running `RunSemanticGoal` span, and per-round `settle.round` events (`settle.duration_ns`) on the `LoweredAction` span. No per-iteration spans (bounded event count; no new span tree noise).
**Alternatives:** per-iteration spans — rejected: high-cardinality span trees for what is a timing question; events are cheaper and already supported.
**Why:** iteration count and durations are exactly what "耗时" analysis needs; event attributes carry them without schema change.

### D4 — decision events anchored on carrying spans
**Decision:** navigation / viewport / trap decision points emit `decision.navigation` / `decision.viewport` / `decision.trap` events with `decision.reason` on the active span when one exists; the semantic journal (`DecisionRecord`) remains the authoritative record and the events never fabricate a span (spec scenario "no active span → journal only").
**Why:** anchors the decision trajectory in time where timing exists, without duplicating authority.

## Risks / Trade-offs

- [Perception stage spans add emission on the hottest real-device path] → thin seams + fail-open; outcome only, no payload copying; if needed, stage spans can be sampled later (no schema impact).
- [Plan-step span adds span-tree noise] → single thin span per step; anti-fabrication keeps absent trees when path unused.
- [Iteration events could over-emit in long loops] → bounded per-run event count (semantic loop cap already ≤5 iterations; settle cap bounded); document bound in spec.
- [Decision events on spans could be mistaken for authority] → spec scenario pins journal-authority; projection treats them as structural markers.

## Migration Plan

Backward-compatible: new spans/events only; existing traces, wire, and schema unchanged; timeline read model consumes richer data with no API change.

## Open Questions

None that would change specs/approach/task breakdown.