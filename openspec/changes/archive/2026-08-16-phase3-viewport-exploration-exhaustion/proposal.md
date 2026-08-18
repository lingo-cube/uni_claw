## Why

The Runtime can prove one bounded viewport movement and same-Container continuity, but it cannot decide from fresh evidence whether another bounded movement is justified, forward exploration is positively exhausted, or the result remains unresolved. SC-P3-CAND-007 purchases the minimum evidence semantics needed to prevent both premature Container exhaustion and blind repeated scrolling inside one semantic Container.

## What Changes

- Add the approved SC-P3-CAND-007 contract for bounded repeated forward exploration within one active semantic Container.
- Preserve the distinction between visible-work exhaustion, movement dispatch, changed evidence, exploration progress, semantic exhaustion, and movement-budget exhaustion.
- Add one immutable, reason-bearing three-valued exploration-decision evidence value: positive continuation, positive exhaustion, or unresolved.
- Add one optional deterministic Goal criterion through which Agent interprets bounded same-Container evidence under the active Goal scope.
- Add one Container-owned bounded retained-evidence field so accepted fresh Observations can be compared without treating sequence number, text equality, or element order as stable identity.
- Require exactly one fresh Observation and SC-P3-003 continuity verification after each authorized movement before another exploration decision.
- Require the maximum exploration bound to stop looping without fabricating semantic exhaustion.
- Preserve GoalEvidence as the only Run-completion authority.

## Capabilities

### New Capabilities

- `viewport-exploration-exhaustion`: Defines evidence-based repeated viewport continuation, positive exhaustion, unresolved handling, boundedness, same-Container evidence retention, completion separation, and deterministic replay for SC-P3-CAND-007.

### Modified Capabilities

None. SC-P3-003 continues to own one bounded `ScrollForward` plus fresh continuity verification; SC-P3-CAND-004 continues to own cross-Container branch progress; SC-P3-CAND-006 continues to own one-Observation candidate authorization.

## Impact

- Expected production surface: one immutable two-field exploration-decision evidence value, one optional immutable Goal criterion field, one Container-owned bounded retained-evidence field, and existing Agent/Container control flow.
- Expected verification surface: deterministic V1 → V2 → V3 exploration fixtures with continue, positive exhaustion, unresolved, bound-reached, continuity-failure, and replay proofs.
- Approved production delta budget: model types +1; fields +4 total; enums +0; interfaces +0; components +0; mutable-state owners +0.
- Ownership delta: none. Container remains the sole owner of page-local retained exploration evidence.
- Authority delta: none. Agent remains the sole authority for Goal relevance, continue/stop/escalate decisions, GoalEvidence consumption, and final RunState.
- No production `Viewport`, `ViewportId`, viewport identity, hierarchy, graph, stack, manager, Fingerprint authority, generic scroll/retry/uncertainty framework, dynamic planner, multi-Container exploration state, new Recovery semantics, FSM, Capstone implementation, S1/S2/S3 work, Harness change, or Runtime refactor is purchased.
