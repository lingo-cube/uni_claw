# R5 Slow Disabled/Shadow Result

Date: 2026-09-01

## STATUS

`CONTAINER_RUNTIME_V2_CORE_PURCHASE_IN_PROGRESS`

The provider-neutral Slow Advisor seam passed the Leader Gate for Disabled, Shadow and advisory-only consumption. No concrete provider, model, backend, deployment mode or mandatory Runtime capability is purchased.

## PURCHASED

- `ISlowContainerSemanticAdvisor` as an async assessment-only port with exact evidence correlation.
- Disabled, Shadow and AsyncAdvisory modes.
- Confirm, Challenge, Correct and Insufficient assessment kinds; bounded scene, usefulness and suggested-disposition evidence.
- Two-phase raw acquisition followed by latest-revision projection.
- Original Slow assessment retention with derived Available, Stale, Rejected, Disabled or Unavailable views.

## HYPOTHESIS

- Slow remains an architecture experiment. It graduates only if correction precision and blocker reduction outweigh false correction, latency, cost and lifecycle complexity.

## IMPLEMENTED

- Request/result bind ObservationRef, evidence revision, NodeRef, SourceNodeRef, TriggerOccurrenceRef and TransitionOccurrenceRef as applicable.
- Optional Fast assessment must match the same revision and current/candidate nodes.
- `SlowContainerSemanticConsumer.AcquireAsync` invokes no advisor in Disabled mode.
- `Project` reads the latest accepted revision only after async completion; stale or rejected raw evidence remains visible but never current.
- Shadow and AsyncAdvisory projections expose `HasRuntimeEffect = false` as a derived constant.

## VALIDATED

- Leader rejected the initial one-phase API because it accepted the "current" revision before awaiting Slow and discarded rejected raw evidence. The final two-phase seam validates freshness at consumption time and preserves diagnostic history.
- Slow/Fast/V2 core/existing Fast semantic/legacy transition targeted suite: 116 passed, 0 failed.
- Runtime full Rebuild: 0 errors; no warning originates from `SlowContainerSemanticAdvisor.cs`; existing repository warnings remain.
- Stateful async tests prove Fast-first/Slow-later, revision advancement while Slow is pending, stale visibility, mismatch/future rejection, Fast/Slow conflict visibility, Disabled zero invocation and Shadow zero behavior effect.
- `scripts/check-consistency.sh`: all C1-C15 passed.
- strict OpenSpec validation: passed.
- `AuthorityDelta = NONE`; `BehaviorDelta = NONE`; `NET_NEW_MUTABLE_TRUTH = 0`.

## DEFERRED

- Concrete Slow provider/model/backend, product dependency, credentials, deployment, retry/scheduling, cost and latency policy.
- Production Agent/Graph/CurrentContainer/action/recovery/completion/Goal consumption.
- Mandatory Slow capability and any stronger authority.

## RISKS

- A real provider may return low-quality or systematically biased correction; Shadow metrics and false-correction review remain mandatory.
- The interface adds async lifecycle complexity even though no mutable state owner is added.
- Raw rejected assessments are diagnostic evidence only; future projections must never reinterpret them as current truth.

## NEXT_WORKITEM

Create immutable semantic-correction facts and a read-only Runtime-to-UniAgent obligation input projection. Prove traversal wrong-child and directed-entry wrong-branch examples without modifying Agent, Goal, recovery or dispatch behavior. Stop at the upper-layer boundary if actual obligation consumption would change frozen authority.
