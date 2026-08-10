# PROJECT_LEADER_U3_TASK_FAMILY_SCOPING_RESULT

> Date: 2026-08-10
> Role: Project Leader
> Scope: U3 task-family definition and prioritization only
> Runtime changes: NONE
> Status: `SCOPED_DEFINITION_ONLY`

## Decision

`U3_TASK_FAMILY_PORTFOLIO_SCOPED`

U3 is a cross-family usability and reality-pressure portfolio. A task family is
defined by stable user Goal and completion meaning, not by a page name, fixed
route, fixture topology, error type, or Runtime mechanism. UI variation,
observation noise, alternate routes, ambiguity, Popup obstruction, external
drift, dispatch timeout, action uncertainty, and longer-horizon Recovery are
cross-cutting disturbance axes; they are not independent user task families.

This decision scopes U3 without defining implementation architecture, creating
a Candidate, authorizing Runtime changes, or advancing evidence maturity.

## Repository Baseline

| Family | Proven baseline | Current evidence |
|---|---|---|
| `U3-F1` — Single-target desired-state assurance | U1: `确保 WiFi 已开启` | Already satisfied means zero mutation; otherwise minimum safe action plus fresh satisfied `GoalEvidence` |
| `U3-F2` — Bounded open-world inspection / audit | U2: depth-bounded safe Settings traversal | Runtime-discovered inventory, verified parent continuation, retained progress, honest bounded completion, fresh `GoalEvidence` |

These are two distinct task families because their Goal and completion shapes
are different. U1 completes from a desired external world state. U2 completes
from verified bounded traversal plus existing fresh Goal evidence. Their use of
closed-world and open-world execution representations remains a representation
choice, not a task-family identity.

## Scoped Task-Family Portfolio

### `U3-F1` — Single-Target Desired-State Assurance

Goal shape: ensure one identified setting has a declared desired state.

Required preserved behavior:

- already satisfied world causes zero unnecessary mutation;
- an unsatisfied world permits only the minimum independently authorized action;
- target ambiguity or insufficient evidence causes no guessed action;
- completion requires fresh world-state `GoalEvidence`.

U1 is the frozen baseline. U3 adds reality variation and disturbance evidence;
it does not redefine the Goal.

### `U3-F2` — Bounded Open-World Inspection / Audit

Goal shape: exhaustively inspect every runtime-discovered in-scope node within
declared depth, scope, category, safety, completion, and entry boundaries.

Required preserved behavior:

- concrete pages, targets, route, and inventory remain unknown before execution;
- intermediate progress, local exhaustion, failure, ambiguity, and boundary
  cutoff remain distinct from global completion;
- parent return and sibling continuation preserve evidence-backed progress;
- completion requires verified bounded traversal plus existing fresh
  `GoalEvidence`.

U2 is the frozen baseline. U3 adds variation, disturbance, and longer-horizon
composition evidence; it does not replace the validated type-level contract.

### `U3-F3-CANDIDATE` — Bounded Open-World Conditional Remediation

Potential Goal shape: discover in-scope configurable items at runtime and
change only items for which a declared desired-state rule and independent safe
action authority are proven.

Disposition: `SEMANTIC_ENTRY_REVIEW_REQUIRED_BEFORE_USE`.

This candidate is not required to begin U3 and is not authorized by this
scoping result. It may require new decisions about per-item desired-state
meaning, required-work membership, multi-item mutation authority, and final
completion evidence. No Scenario, model, API, Candidate implementation, or
production delta is purchased here.

## Cross-Cutting Disturbance Matrix

The following are evidence dimensions applied across task families, not a
Cartesian-product implementation mandate:

| Axis | Minimum falsification question | Existing semantic owner |
|---|---|---|
| UI structural variation | Does the same Goal survive reordered, renamed, or differently grouped visible candidates without fixture identity becoming truth? | CP-11/12/13 |
| Observation noise or failure | Does missing, stale, contradictory, or unavailable evidence remain unresolved instead of becoming exhaustion or success? | CP-08; RM-07 |
| Alternate safe routes | Can execution continue when the concrete route differs while Intent, Goal, and constraints remain unchanged? | CP-14; RM-11 |
| Target ambiguity | Does insufficient grounding produce zero guessed dispatch? | CP-12; RM-10 |
| Local Popup obstruction | Is continuity freshly proven after bounded handling without progress reset or fabricated completion? | SC-P3-002 |
| External drift | Is historical progress revalidated after verified Recovery before it contributes? | SC-P3-CAND-005/009 |
| Dispatch timeout / uncertain effect | Is fresh world evidence consulted with no blind redispatch? | SC-P3-001 |
| Longer horizon / Recovery composition | Do sibling work, return, recovery, and remaining work compose without double count or premature completion? | SC-P3-CAND-004/005/008/009 and Capstone |

Across the eventual U3 portfolio every axis must have at least one positive and
one honest non-completion falsifier. An individual vertical slice should buy
only the smallest subset needed to expose its binding usability blocker.

## Priority

1. **P0 — `U3-F1` variation slice.** Start with the short, externally visible
   desired-state oracle. Vary candidate layout/route and grounding ambiguity;
   retain both already-satisfied zero-mutation and unsatisfied fresh-verification
   branches. Add at most one disturbance class per bounded Scenario.
2. **P1 — `U3-F2` variation and longer-horizon slice.** Apply viewport,
   alternate-route, observation-failure, Popup, drift, and progress-resume
   pressure to the bounded audit family incrementally.
3. **P2 — cross-family compound disturbance validation.** Compose only
   disturbances independently proven in P0/P1; verify that family-specific Goal
   completion semantics remain distinct.
4. **P3 — `U3-F3-CANDIDATE` semantic entry review.** Enter only when product
   pressure requires state-changing open-world remediation. Do not infer this
   purchase from U1 + U2 composition alone.

## U3 Portfolio Exit Definition

U3 may be claimed only when:

- at least `U3-F1` and `U3-F2` have executable production-shaped coverage;
- the disturbance matrix is covered across the portfolio without requiring the
  full Cartesian product;
- each covered disturbance has a positive continuation/recovery branch and an
  honest no-success/no-completion branch where applicable;
- equal deterministic inputs replay equal actions, Observations, journals,
  progress, GoalEvidence, Trace, and final state;
- family-specific completion authority remains Agent-owned and evidence-based;
- no evidence-maturity label beyond the actual artifacts is claimed.

`U3-F3-CANDIDATE` is not an exit requirement unless separately admitted by a
future product/semantic decision.

## Architecture and Governance Boundaries

```text
Architecture impact: NONE
Ownership delta: NONE
Authority delta: NONE
Dependency-direction delta: NONE
Runtime changes: NONE
OpenSpec change: NONE
New Candidate authorized: NO
S1/S2/S3 authorization: NONE
State-machine pressure: NO_STATE_PRESSURE
```

The existing `Agent → Container → Traversal → Environment` spine, Recovery
authority, Intent/Goal/execution-representation separation, and both
`CLOSED_WORLD_CONCRETE` and `OPEN_WORLD_TYPE_LEVEL` modes remain frozen.

## Recommended Next Task

`PROJECT_LEADER_U3_F1_VARIATION_SLICE_GATE`

The next task should select one minimum falsifying `U3-F1` Scenario from current
evidence, classify whether it is accepted-semantics Fast Lane work or requires a
Semantic/Evidence-Maturity gate, and stop before implementation unless the exact
delta is already authorized.

