# Runtime Observability Trace Graduation

> Date: 2026-08-12
> Decision: `RUNTIME_OBSERVABILITY_TRACE = GRADUATED`
> Status: `READY_FOR_GRADUATION` accepted
> Implementation baseline: `6319460`
> OpenSpec: `openspec/changes/runtime-observability-trace-foundation/`

## 1. Graduation decision

The Runtime Observability Trace foundation is graduated as a bounded observational capability around the unchanged semantic Runtime spine.

Delivered implementation:

- BCL `ActivitySource` emission seam is complete.
- Harness-owned per-run `RuntimeTraceRecorder` is complete.
- Immutable, versioned `TraceRun`, `TraceSpan`, and `ObservabilityEvent` projection is complete for the accepted foundation.
- Optional TraceRun persistence is integrated through the existing Harness append-only capture/store boundary.
- Golden replay recording composes Runtime execution, TraceRun capture, `TraceCaptureBundle`, and persistence.
- Scenario observability assertions validate stable structure, attribution, parent closure, required events, and outcomes.
- Existing Agent-owned semantic `TraceEvent` remains unchanged.

## 2. Instrumentation boundary decision

Accepted active production boundaries:

| Boundary | Layer | Stable component |
|---|---|---|
| Agent execution | `AGENT` | `agent.execution` |
| Container refresh | `CONTAINER` | `container.refresh` |
| Traversal execution | `TRAVERSAL` | `traversal.execution` |
| Environment `ObserveAsync` | `ENVIRONMENT` | `environment.observe` |
| Environment `ExecuteAsync` | `ENVIRONMENT` | `environment.execute` |

Explicit deferred receipts:

| Boundary | Graduation classification |
|---|---|
| Runtime invocation | `DEFERRED_CALLER_OWNED_ROOT_SCOPE` |
| Intent execution | `DEFERRED_FUTURE_MULTI_STAGE_COMPILER_PRESSURE` |
| Recovery attempt | `DEFERRED_NO_ACTIVE_PATH` |
| Capability invocation | `DEFERRED_FUTURE_CAPABILITY_EXPANSION` |

Deferred boundaries are not failed or silently omitted requirements. They require new executable pressure before activation and do not block this bounded graduation.

## 3. Ownership and authority freeze

- Runtime components emit operation facts only; they own no TraceRun buffers, projection, or persistence.
- Harness owns the complete TraceRun listener, mutable recording buffer, immutable projection, validation, and persistence lifecycle.
- `TraceRun` remains distinct from the Harness `TraceCaptureSession` lifecycle.
- Agent remains semantic/run authority, Container remains page-local mutable owner, Traversal remains execution/verification authority, and Environment remains the external-world boundary.
- Trace listener, recording, assertion, projection, or persistence failure cannot change Runtime actions, retries, recovery, GoalEvidence, completion, or final result.
- Observability span outcomes are diagnostic facts and never become semantic action success, world truth, recovery success, or Goal completion evidence.
- Existing semantic `TraceEvent` contracts and ownership remain unchanged.

## 4. Scenario assertion freeze

Accepted Scenario observability assertions may validate:

- stable span existence;
- approved layer and component attribution;
- recorded parent closure and unique span identity;
- required stable events;
- explicit observability outcomes.

They must not assert:

- exact duration values;
- callback or private implementation order;
- CLR type or method names;
- free-form diagnostic strings.

Scenario observability failure remains a Harness conformance result and cannot retroactively rewrite the Runtime result.

## 5. Validation evidence

- Full Runtime regression: 819/819 PASS.
- Architecture Guards: 16/16 PASS.
- Consistency: C1-C10 PASS.
- Target OpenSpec strict validation: PASS.
- Production call-site audit: all five accepted active boundaries present; all four deferred boundaries absent as intended.

## 6. Frozen invariants

1. Runtime emits facts only.
2. Harness owns the TraceRun lifecycle.
3. Trace never affects Runtime outcome.
4. Trace never becomes semantic authority.
5. Scenario assertions validate stable structure, not implementation order.

This graduation does not authorize semantic Runtime redesign, a Provider framework, Brain, Planner, capability registry, exact-timing contracts, or activation of a deferred boundary without new evidence and governance.

`RUNTIME_OBSERVABILITY_TRACE_GRADUATED`

STOP. No automatic next capability is authorized by this receipt.
