# SC-P3-CAND-006 — Bounded Safety Classification of Newly Discovered Settings Candidates

> Phase 3 | Semantic Gate: `SEMANTIC_PURCHASE_REQUIRED`
> Approved Production Model Delta: one immutable `CandidateAuthorizationEvidence` type
> Production Fields: `+3` total — two immutable value fields plus one optional immutable Goal field
> Enums: `+0` | Interfaces: `+0` | Components: `+0` | New Mutable-State Owners: `+0`
> Ownership Delta: `NONE` | Authority Delta: `NONE`
> Consumer: `specs/bounded-candidate-safety/spec.md`

## Goal

Prove that a physically actionable candidate exposed by fresh bounded Settings Observation evidence does not become executable until Agent's bounded read-only criterion authorizes it, while destructive, state-changing, and unresolved candidates produce explicit pre-dispatch evidence and zero actions.

## Given

- Runtime is Running in one bounded read-only Settings Container.
- Fresh Observation exposes safe navigation candidate S, destructive navigation-like candidate D, state-changing candidate T, and optionally unresolved candidate U.
- S/D/T/U exist as Observation evidence and were not pre-authorized as fixed executable PlanSteps.
- Goal carries the deterministic, side-effect-free bounded `CandidateAuthorizationEvaluator`.
- Agent owns semantic authorization, GoalEvidence, and final RunState.
- Traversal owns only local execution after authorization; Environment owns only external evidence and dispatch outcomes.

## Positive

```text
fresh Observation exposes S
→ evaluator returns Authorized=true with explicit reason
→ Agent may nominate S as one existing Tap step
→ Traversal Selects, dispatches, Observes, and Verifies
→ fresh GoalEvidence decides completion
```

## Destructive

```text
fresh Observation exposes D
→ D appears navigation-like and also carries destructive evidence
→ evaluator returns Authorized=false
→ Agent records rejected Trace evidence before dispatch
→ D has zero journal dispatches and zero Environment actions
```

## State Changing

```text
fresh Observation exposes T with non-null SwitchState
→ no dangerous keyword is required
→ bounded read-only evaluator returns Authorized=false
→ zero dispatch
```

## Unknown

```text
fresh Observation exposes U with insufficient evidence
→ evaluator returns Authorized=null
→ Agent records unresolved non-authorization
→ zero dispatch and no fabricated completion
```

## Required Assertions

1. Observation proves candidate existence only; it never proves authorization.
2. S/D/T/U are not fixed executable PlanSteps before the fresh classification round.
3. The evaluator receives the fresh Observation and a candidate contained in it.
4. Agent is the sole semantic authorization authority; Traversal never reverses its result.
5. `true`, `false`, and `null` remain distinct and have non-empty deterministic reasons.
6. Only `true` may enter the existing local Tap protocol.
7. D and T each produce zero matching journal dispatches and ActionHistory entries.
8. U produces zero dispatch and an explicit unresolved Trace reason.
9. Denial events identify candidate text/index and source Observation sequence and have no Action/ActionId.
10. Rejected/unresolved candidates do not become approved unfinished safe work merely because they were observed.
11. Authorization and local execution do not complete the Run; only satisfied GoalEvidence does.
12. Equal inputs replay equal outcomes/reasons, Trace, journal, actions, Observations, GoalEvidence, and RunState.

## Ownership and Authority

- Agent owns Goal criterion evaluation, semantic authorization, candidate nomination, denial evidence, GoalEvidence consumption, and final RunState.
- Container owns the current page-local Observation and candidates only.
- Traversal owns deterministic local execution and journal evidence only.
- Environment reports Observation and dispatch outcomes only.
- Recovery retains its frozen mechanics and receives no safety authority.

## Plan Boundary

The Scenario permits one bounded Agent nomination of an authorized observed candidate as an existing Tap step. It does not mutate or replace the immutable Plan, construct a route, discover candidates across pages, or purchase a dynamic planner. Existing fixed-Plan behavior remains unchanged when the optional evaluator is absent.

## Completion Boundary

Visible candidate, authorized candidate, required safe work, dispatched action, world effect, and Goal completion are distinct. Rejected/unresolved candidates remain accounted Trace evidence, not required safe branches. Final completion remains Agent consumption of satisfied GoalEvidence.

## Explicitly Deferred

- General candidate discovery, autonomous task planning, arbitrary UI understanding, and multi-page route generation.
- Universal action interception or safety/policy/rule engines.
- SafetyManager, RiskEngine, SafeActionExecutor, authorization manager, RiskLevel, Confidence, policy hash, coordinates, Fingerprint, Vision/VLM judgement, navigation graph/stack, or mutable safety owner.
- New action/Trace/journal/Trap surface, Recovery semantics, Capstone implementation, Harness change, Runtime refactor, S1/S2/S3 work, or Phase completion.
