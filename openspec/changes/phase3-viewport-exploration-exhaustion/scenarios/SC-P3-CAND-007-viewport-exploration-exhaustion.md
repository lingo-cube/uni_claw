# SC-P3-CAND-007 — Evidence-Based Repeated Viewport Exploration and Honest Exhaustion

> Phase 3 | Semantic Gate: `SEMANTIC_PURCHASE_REQUIRED`
> Viewport Model Test: `MINIMUM_VIEWPORT_EVIDENCE_REQUIRED`
> Approved Production Model Delta: one immutable `ViewportExplorationEvidence` value
> Production Fields: `+4` maximum — two evidence fields, one optional immutable Goal criterion, one Container-owned retained-evidence field
> Enums: `+0` | Interfaces: `+0` | Components: `+0` | New Mutable-State Owners: `+0`
> Ownership Delta: `NONE` | Authority Delta: `NONE`
> Consumer: `specs/viewport-exploration-exhaustion/spec.md`

## Goal

Prove that one semantic Container can perform bounded repeated forward exploration from fresh retained evidence, continue only when another movement is positively justified, stop when exhaustion is positively proven, and remain honestly unresolved when neither conclusion is supported.

## Given

- Runtime is Running with one valid active semantic Container.
- The Container is bound to fresh Observation V1 exposing A/B/C and has existing local progress.
- The finite Plan contains multiple already-approved `ScrollForward` steps that define the maximum exploration bound.
- Goal carries deterministic, side-effect-free viewport exploration and GoalEvidence evaluators.
- SC-P3-003 owns each individual movement, fresh Observation, and semantic continuity verification.
- Agent owns Goal relevance, continue/stop/escalate decisions, GoalEvidence, and final RunState.

## Positive Exploration and Exhaustion

```text
V1: A B C + positive evidence that bounded exploration should continue
→ exploration evidence = true
→ exactly one ScrollForward
→ fresh continuous V2: B C D
→ D is newly relevant evidence
→ exploration evidence = true
→ exactly one further ScrollForward
→ fresh continuous V3: C D E + positive end/boundary evidence
→ exploration evidence = false
→ no third ScrollForward
→ independently satisfied GoalEvidence may complete the Run
```

Movement dispatch itself is not progress. Changed visible evidence is not automatically relevant work. The final positive boundary evidence, not Plan exhaustion or snapshot equality, proves bounded forward exhaustion.

## Unresolved Branch

```text
fresh continuous Observation repeats or changes visible evidence
→ no positive continuation evidence
→ no positive exhaustion evidence
→ exploration evidence = null
→ no further ScrollForward
→ explicit unresolved/non-completion behavior
```

## Bound-Reached Branch

```text
final approved ScrollForward is consumed
→ fresh continuous evidence still returns true
→ movement budget exhausted
→ semantic exhaustion remains unproven
→ unresolved/incomplete, not exhausted or Completed
```

## Dispatch / Continuity Failure Branch

If a movement is rejected, times out without accepted post-action proof, returns stale evidence, or fails existing same-Container continuity:

- the failed movement does not prove semantic exhaustion;
- contradictory evidence is not appended to retained exploration evidence;
- no blind repeat occurs;
- SC-P3-001/SC-P3-003 failure or escalation behavior remains authoritative;
- prior Container-local evidence and progress are not silently reset.

## Required Assertions

1. Container retains accepted Observation evidence in deterministic V1/V2/V3 order and remains its sole mutable owner.
2. Observation sequence proves freshness/order only and never content identity.
3. Agent is the sole authority consuming the exploration criterion.
4. `true`, `false`, and `null` remain distinct and carry non-empty deterministic reasons.
5. Each `true` authorizes at most one already-approved `ScrollForward`.
6. Every dispatched movement is followed by fresh Observation and existing continuity verification before another decision.
7. Positive branch dispatches exactly two `ScrollForward` actions and no third action after exhaustion evidence.
8. Same visible elements, rejected dispatch, no visible authorized candidate, no new text, and bound consumption do not independently prove exhaustion.
9. `null` and bound-reached branches perform no blind additional movement and produce no fabricated completion.
10. Positive exhaustion does not set local completion, branch completion, GoalEvidence, or RunState.
11. Only independently satisfied GoalEvidence may complete the Run.
12. Equal RunId, evaluators, Plan, Environment inputs, and bound replay equal retained evidence, outcomes/reasons, actions, journal, Trace, GoalEvidence, and final state.

## Ownership and Authority

- Environment reports external Observations and dispatch outcomes only.
- Traversal owns one bounded movement's Execute → Observe → mechanical verification protocol.
- Container owns one semantic page scope, continuity, local progress, and bounded retained cross-viewport Observation evidence.
- Agent owns Goal relevance, continue/stop/escalate decisions, active Container changes, GoalEvidence consumption, and final RunState.
- Recovery ownership remains frozen and receives no viewport exploration authority.

## Completion Boundary

Visible-work exhaustion, viewport movement, accepted continuity, new evidence, positive forward exhaustion, Container completion, branch completion, and Run completion are distinct. Positive viewport exhaustion stops movement only. `RunState.Completed` still requires independently satisfied GoalEvidence consumed by Agent.

## Explicitly Deferred

- Production `Viewport`, `ViewportId`, stable viewport/content identity, hierarchy, graph, stack, manager, or progress framework.
- Fingerprint authority, geometry, reverse scrolling, generic ScrollPolicy, retry or uncertainty framework.
- Dynamic planning, arbitrary multi-viewport candidate discovery, multi-Container exploration state, or generalized enumeration.
- New Recovery semantics, FSM, Runtime refactor, Capstone implementation, Harness changes, S1/S2/S3 work, or Phase completion.
