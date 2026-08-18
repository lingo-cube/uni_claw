## Context

CC-04 and `TypeLevelTraversalSpecification` already preserve an authoritative
open-world traversal input without inventing a concrete `Plan`. Existing
production `Agent.RunAsync(Goal, Plan, ...)` remains the closed-world boundary.
The opt-in bounded branch-discovery path can enter fresh child Containers but
cannot verify a parent return, continue a sibling, or gate existing fresh Goal
evaluation on verified bounded traversal completion. The S0 Capstone demonstrates the
composition only in a test-side concrete orchestrator and cannot serve as the
production U2 path.

The evidence-backed reconciliation at
`docs/decisions/u2-minimum-usable-agent-slice-gate.md` freezes the revised
completion semantic and production budget. The earlier Human receipt is
superseded; implementation remains pending renewed Human authorization.

## Goals / Non-Goals

**Goals:**

- Execute a resolved open-world type-level Settings specification without a
  pre-enumerated concrete route or work inventory.
- Discover and authorize one in-scope child at a time from fresh accepted
  evidence.
- Verify bounded child terminal evidence, a unique authorized parent return,
  exact fresh parent reconciliation, and sibling continuation.
- Preserve completed sibling evidence while unresolved siblings remain.
- Let Agent invoke the existing `Goal.EvidenceEvaluator` on fresh root evidence
  only after it has derived verified bounded traversal completion.
- Preserve deterministic replay and all existing closed-world behavior.

**Non-Goals:**

- Change `Agent.RunAsync(Goal, Plan, ...)` or non-traversal Goal completion.
- Add a Planner, Compiler engine, route/frontier model, graph, stack type,
  navigation manager, FSM, generic retry, or uncertainty framework.
- Add a new Back action, target identity algorithm, or safety semantic.
- Treat empty inventory, a local leaf, a depth/safety cutoff, action dispatch,
  observation failure, ambiguity, or visited known nodes as global completion.
- Implement viewport discovery, Recovery, Popup handling, or a general Settings
  crawler inside this slice.

## Decisions

### Add one bounded upstream execution seam

Add `Planning/IntentSemanticEnvelopeExecution.cs` with one public static type and
one `RunOpenWorldAsync` entry. It accepts an `IntentSemanticEnvelope.Resolved`,
requires its representation to be `OpenWorldTypeLevel`, validates the already
authoritative specification as the supported navigation-only exhaustive
boundary, and forwards only primitive/model inputs to an internal Agent entry.

The seam does not accept `Insufficient`, parse text, infer a desired state,
select work, generate a route, observe, dispatch, or decide completion. Agent
continues to have no dependency on `UniClaw.Runtime.Planning`.

Alternative rejected: extend `Agent.RunAsync` with a Planning union. That would
move the upstream intent boundary into Agent and violate the frozen CP-14 guard.

Alternative rejected: translate the type-level specification into a concrete
`Plan`. It would pre-enumerate or manufacture future work and erase the
closed/open representation distinction.

### Reuse the existing Goal evidence boundary

Do not add `Goal.BranchProgressEvidenceEvaluator` or modify `Goal.cs`. Agent
derives `VerifiedBoundedTraversalCompletion` from its existing immutable
`BranchProgressEvidence`, accepted inventories, verified returns, and unresolved
work checks. Only after that condition is true does Agent invoke the existing
`Goal.EvidenceEvaluator` on the current fresh root Observation.

The completion rule is the conjunction:

```text
VerifiedBoundedTraversalCompletion
+ existing fresh GoalEvidence.Satisfied
= traversal-shaped Goal completion
```

The evaluator never receives partial progress through a closure, and Agent does
not fabricate GoalEvidence. A satisfied evaluator before bounded traversal
completion is not consulted by the opt-in open-world path.

Alternative rejected: add `VerifiedBoundedTraversalCompletion` as a type. The
Human decision froze it as a semantic condition, and the minimum Scenario does
not require another production model.

### Use run-local parent frames inside Agent

The new internal Agent path performs Startup and initial reconciliation through
the existing owners, then executes depth-first bounded traversal. Parent frames
are a method-local stack containing only an existing Container reference and the
selected child identity. Semantic depth is derived from stack count. Frames are
not a new type, field, model, graph, route, or state owner.

At each Container Agent:

1. evaluates the existing complete branch-inventory criterion over accepted
   local evidence and validates its source sequences;
2. preserves existing completed-sibling evidence when refreshing the inventory;
3. if below the depth bound, selects at most one pending required branch only
   after the existing candidate-authorization criterion returns positive;
4. nominates that semantic target through an existing transient Tap `PlanStep`;
5. delegates selection, dispatch, fresh Observe, and first local verification
   to Container/Traversal;
6. reconciles the fresh child semantic Container and pushes one run-local parent
   frame.

No future sibling, page, target, coordinate, route, or inventory is stored in
the specification or a Plan.

### Verify return before recording branch completion

When the current subtree is terminal within the declared bound, Agent expects
the parent semantic identity from the run-local frame. The current fresh
Observation must contain exactly one matching candidate, and the existing
authorization evaluator must return positive. Otherwise no return action is
dispatched.

Traversal performs the nominated Tap and fresh verification. Agent then
requires a strictly fresh Observation that reconciles exactly to the expected
parent and is accepted by the retained parent Container. Only after this proof
does Agent update the parent's `BranchProgressEvidence` and continue with a
sibling. A wrong/stale/ambiguous/rejected return never records completion and is
never blindly repeated.

Alternative rejected: introduce a generic Back action. The minimum Scenario has
an explicit, observable, authorized parent target and does not purchase device
Back semantics.

### Derive verified bounded traversal completion before Goal evaluation

At the root, Agent may derive the frozen semantic condition only when:

- the current complete inventory is accepted from fresh root evidence;
- every required in-scope child is present in completed-sibling evidence;
- every entered child returned through the verified protocol;
- the method-local parent frame stack is empty;
- no inventory, authorization, observation, continuity, or return ambiguity is
  unresolved;
- the declared application, entry, maximum depth, and navigation-only safety
  boundary have remained satisfied.

Only then does Agent call the existing `Goal.EvidenceEvaluator` on the current
fresh root Observation. Satisfied `GoalEvidence` completes; unsatisfied evidence
fails explicitly. A depth cutoff can make deeper visible candidates out of
scope, but Trace language records a bounded cutoff and never claims
discovered-world or whole-world exhaustion.

### Preserve existing behavior by opt-in entry

The new control flow is reachable only through the Planning seam. Existing
`RunAsync`, `Goal`, and the Phase 1–3/CP12 closed-world branches remain
unchanged.

## Risks / Trade-offs

- [Risk] Text identity is only parent-local and may be ambiguous. → Require
  exactly one current parent-return match; ambiguity stops before dispatch.
- [Risk] A criterion could incorrectly claim complete inventory. → Validate all
  cited branches against accepted Observation sequences and retain the criterion
  as Goal-scoped evidence rather than world truth.
- [Risk] Depth cutoff could be described as exhaustion. → Use explicit bounded
  scope/cutoff Trace evidence and negative tests that forbid exhaustion claims.
- [Risk] Agent grows another bounded protocol. → Keep it behind one opt-in seam,
  reuse existing models and execution mechanics, add no state field/type, and
  preserve the architecture guard that forbids Agent → Planning.
- [Risk] Parent return by visible target does not generalize to all apps. → This
  minimum slice intentionally proves only the explicit-target Settings world;
  generic navigation is not purchased.
