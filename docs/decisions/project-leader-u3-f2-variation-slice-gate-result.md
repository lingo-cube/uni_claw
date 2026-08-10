# PROJECT_LEADER_U3_F2_VARIATION_SLICE_GATE_RESULT

> Date: 2026-08-10
> Role: Project Leader
> Parent: `PROJECT_LEADER_U3_TASK_FAMILY_SCOPING_RESULT`
> Prior slice: `SC-U3-F1-001 VALIDATED`
> Task family: `U3-F2 — Bounded Open-World Inspection / Audit`
> Gate mode: Scenario selection + lane classification only
> Runtime changes: NONE

## Decision

`SEMANTIC_GATE_REQUIRED`

The minimum U3-F2 disturbance Scenario is selected, but the current accepted
open-world input cannot express the action authority required to handle it.
Implementation must not begin until that semantic boundary is resolved.

This is not an Architecture Gate. Repository evidence does not require moving
Container/Traversal/Agent ownership, reversing dependencies, or changing an
invariant. The missing item is an immutable caller-authorized meaning for one
bounded local-obstruction action inside an otherwise navigation-only open-world
traversal.

## Minimum Falsifying Scenario Contract

```text
Scenario: SC-U3-F2-001
Title: Popup After Verified Sibling Progress During Bounded Open-World Traversal
Task family: U3-F2 bounded inspection / audit
Primary disturbance: one local Popup obstruction
Evidence level: deterministic production-shaped target (no maturity advance)
```

### Common Given

- authoritative resolved `OPEN_WORLD_TYPE_LEVEL` traversal specification;
- root inventory contains required siblings A and B at depth 1;
- A has been visited, positively proven terminal, returned to the exact fresh
  parent, and recorded complete;
- B remains unresolved and required;
- before B dispatch, a Popup appears while the underlying semantic root
  Container remains logically unchanged;
- the Popup exposes one candidate that could dismiss the obstruction.

The Popup is not a required branch, concrete work inventory member, traversal
completion receipt, page drift, or evidence that A should be replayed.

### Positive Target Behavior

If one bounded dismiss is explicitly authorized and succeeds, then:

1. exactly one dismiss action is dispatched;
2. a fresh Observation is obtained;
3. the same root Container continuity is proven using existing SC-P3-002
   evidence rules;
4. A completion remains associated with the root and is not replayed or reset;
5. B remains pending, is subsequently dispatched exactly once, and is
   independently verified;
6. traversal completes only after no unresolved in-scope work remains;
7. only existing fresh GoalEvidence may complete the Run.

### Negative Target Behavior

If dismiss is rejected, or fresh post-dismiss evidence cannot prove the same
root Container, then:

1. no local handling success is fabricated;
2. A history is not erased or silently promoted to current truth;
3. B is not blindly dispatched through the obstruction;
4. no traversal exhaustion or Goal completion is fabricated;
5. structured Container-scope evidence escalates;
6. Agent retains rebind, Recovery, failure, and final RunState authority.

### Deterministic Replay

Equal Intent, type-level specification, Goal criteria, RunId, world sequence,
and dispatch outcomes must replay equal actions, Observations, journal, Trace,
branch progress, continuity evidence, GoalEvidence, reason, and final RunState.

## Repository Evidence of the Semantic Gap

### Existing U2 Open-World Path

`Agent.RunOpenWorldAsync` consumes only:

- Goal;
- application identity;
- expected semantic entry;
- maximum depth;
- RunId and cancellation.

Its loop immediately interprets accepted Container evidence as branch inventory
and dynamically constructs only required child/parent navigation Tap steps. It
has no caller input for classifying or authorizing a local obstruction action.

### Existing SC-P3-002 Popup Path

The frozen Popup capability is intentionally narrower:

- the dismiss action already exists as a caller-approved concrete `PlanStep`;
- Container determines whether that next approved step can handle the local
  obstruction;
- Traversal dispatches and observes;
- Container verifies continuity;
- Agent retains higher-scope authority.

SC-P3-002 does not grant Agent authority to invent a dismiss step when no
concrete Plan exists.

### Existing Type-Level Representation

`TypeLevelTraversalSpecification` represents exactly:

- task scope;
- target element categories;
- maximum depth;
- allowed interaction categories;
- completion requirement;
- entry boundary.

Its current category vocabulary is `NavigableContainer` and
`StateChangingControl`. Neither a category label nor the navigation-only U2
specification identifies a concrete Popup dismissal candidate, proves that the
candidate is safe, or grants permission to execute it.

`Goal` likewise has no local-obstruction authorization criterion. Its existing
candidate authorization evaluator is necessary but insufficient: it can assess
a supplied candidate, but nothing in the open-world contract authorizes the
Runtime to classify that candidate as required local handling rather than
ordinary inventory or to manufacture the corresponding execution step.

## Falsified Shortcuts

The following are forbidden resolutions:

1. **Treat Dismiss as a discovered branch.** This conflates obstruction handling
   with task inventory and traversal progress.
2. **Agent manufactures `PlanStep("Dismiss", "Tap")`.** This invents execution
   method and action authority absent from the caller input.
3. **Assume every visible Dismiss/Close text is safe.** Text membership is not
   semantic identity or authorization.
4. **Treat Popup evidence as unresolved inventory and stop permanently.** This
   preserves honesty but does not deliver the selected U3 disturbance behavior.
5. **Reuse `StateChangingControl` by name alone.** A broad category does not
   state which obstruction action is allowed or how bounded handling is proven.

## Semantic Questions Requiring Resolution

The next Semantic Gate must decide only:

1. What immutable caller-authorized evidence makes a visible local obstruction
   candidate eligible for exactly one bounded handling attempt?
2. How is local-obstruction eligibility kept distinct from required branch
   inventory, task progress, GoalEvidence, and traversal exhaustion?
3. How is the approved handling action constrained without pre-enumerating a
   concrete future Popup, target, or route?
4. What does `false` / insufficient / ambiguous handling evidence require before
   dispatch (zero guessed action)?
5. Does existing SC-P3-002 fresh continuity and escalation evidence remain the
   complete post-dispatch protocol? The default answer should be yes unless a
   falsifier proves otherwise.

The Gate must not design PopupManager, Planner, Recovery policy, Graph, FSM,
generic retry, generic uncertainty, or clarification UX.

## Boundary Classification

```text
New semantic pressure: YES — open-world local-obstruction action authority
New Reality Model required: NOT PROVEN
Existing CP/RM contradiction: NONE
Architecture pressure: NONE DETECTED
Ownership change required: NOT PROVEN
Authority transfer required: FORBIDDEN / NOT REQUIRED
Dependency change required: NOT PROVEN
Safety semantic impact: POSSIBLE — must be explicitly checked by the Semantic Gate
Production delta: NOT AUTHORIZED
Runtime implementation: NOT STARTED
```

The semantic question may be representable as a minimal extension or composition
of already accepted CP-07, CP-12, RM-06, RM-10, RM-11, and SC-P3-002. This Gate
does not pre-decide the carrier or public API.

## Exact Non-Actions

- no Runtime, tests, fixture, OpenSpec, or Harness modification;
- no implementation task generation;
- no Popup/timeout/drift/viewport compound Scenario;
- no `U3-F3-CANDIDATE` entry;
- no S1/S2/S3 or emulator/device work;
- no model/type/field/enum/interface/component proposal in this result.

## Recommended Next Task

`PROJECT_LEADER_U3_F2_LOCAL_OBSTRUCTION_AUTHORITY_SEMANTIC_GATE`

Resolve the five bounded semantic questions above, prove minimality against
`SC-U3-F2-001`, audit safety authority explicitly, and return either an approved
semantic envelope, `SAFETY_SEMANTIC_GATE_REQUIRED`, or a genuine Architecture
Gate. Do not implement in that task.

