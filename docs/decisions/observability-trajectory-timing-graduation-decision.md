# Observability Trajectory Timing — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_OBSERVABILITY_TRAJECTORY_TIMING` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-observability-trajectory-timing/`
> Authority: Runtime Architecture Contract I-1..I-14 and Architecture v1 remain the governing baselines; observability remains a fail-open, non-semantic, structural-only channel.

## 1. Buyer and exact claim boundary

**Buyer:** functional-trajectory timing + FDP granularity for real-device analysis — where time went (per stage) and where decisions happened (with timing anchors when a span exists).

This receipt claims only that:

1. Perception pipeline stages are bounded and timed: `perception.capture` / `perception.vision` (PhysicalEnvironment, nested under `environment.observe`), `perception.fusion` (`SemanticEvidenceFusionPipeline`), `perception.canonicalize` + `perception.admission` (`SemanticCapabilityEnvironment`) — structural outcomes only, never admission/decision authority;
2. `startup.bootstrap` (`STARTUP` layer, first activation of the declared layer) times the Startup sequence; `traversal.plan-step` times the deterministic plan-step path (previously untimed);
3. iteration granularity (`iteration.start` events with `decision.iteration` / `decision.duration_ns`, bounded ≤ maxIterations+1) and settle-round granularity (`settle.round` / `settle.duration_ns`, bounded) are emitted as structured events on carrying spans;
4. navigation / viewport / trap decision points emit `decision.*` events with `decision.reason` on the carrying span when a span is active; the semantic journal (`DecisionRecord`) remains authoritative and no span is fabricated for decisions without a span;
5. the read-model taxonomy (component + span name sets) is extended to the 16 components.

No claim is made for: OTLP export, sampling/retention, Links/Ref taxonomy, `TraceRun` schema v2, any change to perception semantics / admission fail-closed behavior / ownership, or the settle-round conformance proof (see Deferred scope).

## 2. Validation evidence

- `dotnet build src/UniClaw.Runtime.sln`: **0 errors**.
- Full solution suite: **2333/2338 passed** (5 failures are pre-existing environment-only: RealEmulator, RealDevice, Vision-host ×3 — present in all runs before/after this change; zero observability-trace failures; the previously archived strict trace tests remain green under the new spans).
- New conformance: adapter capture/vision (minimal fake sources), semantic canonicalize/admission (identity-projector driven), plan-step, iteration events, Startup presence in golden, anti-fabrication extended to all new boundaries (26/26 targeted green).
- Strict OpenSpec validation: **PASS** (`openspec validate observability-trajectory-timing`).
- Consistency: **PASS** (`scripts/check-consistency.sh` ALL PASS; projections verified). Formatting: **PASS** (`git diff --check`).

## 3. Scenario receipts and falsifiers

| Falsifier | Result |
|---|---|
| Two independent root activities are foreign traces to each other (canonicalize/admission as standalone roots) | **Not falsified (design constraint surfaced)**: production nesting under the observe boundary shares one trace; the conformance test composes under an ambient observe span. Patterns documented; recorder run-scoping unchanged. |
| Default fact-projector input contract breaks the pipeline reach (canonicalize→admission flow) | **Not falsified (fixture constraint)**: the semantic-envelope conformance uses an identity projector preserving observation sequence, matching the existing Perception/ test pattern. |
| New emission changes Runtime/wire/detached behavior | **Not falsified**: zero regression in 2333-passed full suite; frozen wire/DTO guards green; schema v1 untouched. |
| Settle-round events conform on fake environments | **Falsified (deferred)**: settle requires a null-switch window + semantic-envelope recognizability that fake environments cannot produce; emission is implemented and bounded, conformance deferred to a settle-capable composition (see Deferred scope). |
| Decision events become authority or fabricate spans | **Not falsified**: events fire only on the carrying span when active; journal remains authoritative; spec scenario "no active span → journal only". |

## 4. Deferred scope

The following remain outside this graduation and require separate authorization:

- Settle-round conformance proof on a settle-capable composition (production real-device or a dedicated null-switch fixture).
- OTLP export / sampling / retention / Links-Ref taxonomy; `TraceRun` schema v2.
- Real-device timing baseline consumption of the new stages through the timeline read model (`GetRunTimeline`).
- Any further trajectory instrumentation beyond the 16-component set.

## 5. Final conclusion

**GRADUATED.** The Perception-stage, Startup, plan-step, iteration/settle, and decision-anchor instrumentation is human-authorized, evidence-verified, and archived; deferred scope remains unauthorized for separate gate.