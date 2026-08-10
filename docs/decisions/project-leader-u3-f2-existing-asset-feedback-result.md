# PROJECT_LEADER_U3_F2_EXISTING_ASSET_FEEDBACK_RESULT

> Date: 2026-08-10
> Role: Project Leader
> Mode: read-only asset feedback and semantic-purchase rebasing
> Scenario: `SC-U3-F2-001`
> Previous result: `PROJECT_LEADER_U3_F2_VARIATION_SLICE_GATE_RESULT`
> Runtime changes: NONE

## Decision

`REBASE_SEMANTIC_GATE_TO_EXISTING_METHOD_CONSTRAINT_COMPOSITION`

The previous `SEMANTIC_GATE_REQUIRED` remains correct, but its stated gap was
too broad. Repository assets already provide both the local Popup protocol and
an immutable concrete carrier for caller-approved dismiss method intent:
existing `PlanStep` / `Plan` semantics.

The remaining gap is narrower:

```text
OPEN_WORLD_TYPE_LEVEL traversal specification
+ finite caller-supplied local contingency method constraint
→ one truthful executable input
```

The contingency must remain distinct from a concrete future route, discovered
branch inventory, progress, GoalEvidence, and generic Popup policy. The next
Semantic Gate must decide only whether and how these already-valid meanings can
coexist at the upstream execution-representation boundary.

## Asset Feedback Matrix

| Asset | What it proves | Reuse decision | What it does not prove |
|---|---|---|---|
| U2 fixture and formal Scenario | Dynamic A/B inventory, A retained while B pending, exact parent return, honest completion, replay | **REUSE_BASE_WORLD_AND_PROGRESS_ORACLE** | Popup handling or local contingency authority |
| SC-P3-002 Popup fixture/formal Scenario | Concrete approved Dismiss step, positive/rejected/page-changed branches, fresh same-Container continuity, progress preservation, escalation, replay | **REUSE_HANDLING_AND_CONTINUITY_ORACLE** | Open-world representation composition |
| S0 Capstone harness/formal proof | Dynamic discovered branch and a caller-supplied concrete Dismiss `PlanStep` compose under existing Agent/Container/Traversal ownership; one Popup is handled with fresh continuity | **REUSE_ARCHITECTURE_AND_COMPOSITION_EVIDENCE** | A truthful `OPEN_WORLD_TYPE_LEVEL` input; most of its 31-step route is concrete and predeclared |
| Capstone stop-extract edge | A previously dispatched/suspended Dismiss step is not replayed after Recovery when the recovered world has no current Dismiss candidate; run stops explicitly | **REUSE_NEGATIVE_GROUNDING_ORACLE** | Generic resumption or automatic redispatch authority |
| Legacy enumerate scenario + read-only policy | Declared scope, action classes, depth and safety vocabulary existed; broad click/back permission coexisted with dangerous button/permission semantics | **CONTEXT_ONLY** | Safe authorization of a specific Dismiss candidate |
| Legacy FSM popup tests | Popup detection pressure, bounded attempts, failure handling pressure | **EVIDENCE_CONTEXT_ONLY** | External-world effect, current architecture fit, or production semantics |
| Legacy PopupHandler/FSM specs | Regex priorities, auto-close/back fallbacks, state snapshots, handler pipeline, mutable statistics | **REJECT_LEGACY_MECHANISM** | Any authority to recreate PopupManager, classifier, FSM, fallback Back, or mutable handler state |

## Original Purchase Review

### Retained

- `SC-U3-F2-001` remains the minimum falsifying U3-F2 disturbance Scenario.
- Popup handling must be explicitly authorized before dispatch.
- Local handling remains distinct from branch inventory and progress.
- Existing SC-P3-002 post-dispatch fresh continuity and escalation semantics are
  sufficient and remain frozen.
- Agent must not invent a Dismiss action or silently treat text membership as
  authority.

### Superseded

The following earlier implication is superseded:

> A new Popup-specific authorization criterion or semantic carrier is presumed
> necessary.

Existing `PlanStep` already expresses a bounded concrete target/action method
constraint, and the Capstone proves that such a step can drive SC-P3-002 without
moving ownership or authority. No Popup-specific criterion, manager, policy,
enum, classifier, engine, or Recovery framework should be purchased unless a
future falsifier proves `PlanStep` insufficient.

### Still Missing

Current CC-04 representation is an exclusive union:

```text
CLOSED_WORLD_CONCRETE → Plan
OPEN_WORLD_TYPE_LEVEL → TypeLevelTraversalSpecification
```

U2's production execution seam accepts only the type-level specification and
passes no caller-supplied contingency step into `Agent.RunOpenWorldAsync`.
Conversely, the Capstone's `Agent.RunAsync(Goal, Plan, ...)` path can carry the
Dismiss step but does not truthfully represent the U2 type-level traversal as
its primary execution representation.

Using the Capstone path as-is would therefore prove the wrong contract. It would
replace the validated U2 boundary with a mostly concrete route rather than show
that one bounded contingency constraint can coexist with open-world discovery.

## Rebased SC-U3-F2-001 Input Contract

The Scenario branches remain unchanged except for one clarified authoritative
input requirement:

```text
authoritative Intent + Goal
+ exact existing OPEN_WORLD_TYPE_LEVEL specification
+ exact caller-supplied local Dismiss/Tap method constraint
```

The method constraint:

- is not a route prefix or suffix;
- is not required branch inventory;
- does not state that a Popup will occur;
- does not itself identify a current candidate;
- does not authorize every Dismiss/Close/Allow-like element;
- may be consumed only when current obstruction evidence and independent
  candidate safety authorization make the exact step eligible;
- is bounded to at most one attempt in `SC-U3-F2-001`;
- creates no progress or completion evidence.

## Rebased Falsifiers

### Positive

After A is verified and returned, one Popup appears before B. The exact supplied
Dismiss/Tap constraint is eligible against current evidence, receives an
independent positive safety receipt, dispatches exactly once, obtains fresh
same-root continuity, preserves A, and permits B to continue exactly once.

### Absent Constraint

If no local contingency method constraint was supplied, the Runtime must not
invent Dismiss/Tap. It stops or escalates explicitly with zero handling dispatch.

### Candidate Missing / Ambiguous / Unsafe

An exact constraint with no unique current eligible candidate, or without an
independent positive safety receipt, causes zero dispatch and no progress,
exhaustion, or completion claim.

### Dispatch or Continuity Failure

Rejected dismiss or contradictory post-dismiss evidence follows the existing
SC-P3-002 structured escalation path. A remains historical evidence; B is not
blindly dispatched through the obstruction.

### Recovery Edge

If the Popup disappears before a suspended handling step can be grounded again,
the Capstone stop-extract oracle applies: no blind redispatch, explicit
non-completion, and no invented replacement action.

## Safety Feedback

Legacy assets strengthen the need for a narrow safety boundary:

- a generic `click` allowance is not target authorization;
- legacy policy classifies buttons, permission grants, and text such as
  `allow`/`grant` as dangerous;
- legacy PopupHandler proposed auto-close and fallback Back behavior, which is
  mechanism-coupled and unsafe to inherit;
- a visible `Dismiss`-like label cannot by itself prove benign semantics.

For `SC-U3-F2-001`, the existing independent
`CandidateAuthorizationEvaluator` must still issue a positive receipt for the
current exact candidate. The Semantic Gate may approve only the neutral
test-world Dismiss/Tap constraint; it must not generalize to permission dialogs,
destructive confirmation, auto-close, or fallback Back.

No standalone `SAFETY_SEMANTIC_GATE_REQUIRED` is established yet because the
selected slice can remain inside existing per-candidate safety authority. If
the next Gate cannot preserve that boundary, it must stop at the Safety Gate.

## Architecture Feedback

The Capstone is executable evidence that existing ownership can carry the
behavior when a concrete Dismiss step is supplied:

- caller/Plan supplies the method constraint;
- Agent sequences and retains higher-scope authority;
- Container classifies bounded local obstruction and verifies continuity;
- Traversal selects, dispatches, observes, and verifies freshness;
- Environment owns visible Popup state, transitions, and dispatch outcomes.

Therefore:

```text
Architecture pressure: NONE DETECTED
Ownership delta: NONE
Authority delta: NONE
Dependency-direction delta: NONE
Invariant delta: NONE
State-machine pressure: NO_STATE_PRESSURE
```

The final representation/API carrier remains a semantic decision. Naming that
carrier does not justify a new layer or component.

## Evidence Maturity

- current U2, SC-P3-002, and Capstone proofs are deterministic executable
  evidence in the current repository;
- legacy FSM popup evidence is synthetic and mechanism-specific;
- legacy scenario/policy and archived PopupHandler specifications are context,
  not current Runtime authority;
- no E0/E1 asset is promoted to S1/S2/S3 or external-world truth by this review.

## Revised Semantic Gate Questions

The next Gate must answer only:

1. Can one existing concrete `PlanStep` method constraint coexist with
   `OPEN_WORLD_TYPE_LEVEL` without converting the specification into a concrete
   future route?
2. Is the constraint part of the execution representation, or a separate
   immutable caller authority input that must remain structurally associated
   with it?
3. How is eligibility bound to current local-obstruction evidence while
   existing `CandidateAuthorizationEvaluator` remains the safety authority and
   Traversal remains the target selector?
4. How is absence/ambiguity/unsafe evidence structurally prevented from
   producing an executable handling step?
5. Can the minimum carrier remain specific to the selected neutral Dismiss/Tap
   slice without creating generic Popup semantics?

The Gate must prefer reuse of existing `PlanStep`, SC-P3-002 evidence, and U2
progress models. A new semantic type is allowed only if the dual-mode
representation cannot honestly preserve the association otherwise.

## Result

```text
Scenario: SC-U3-F2-001 RETAINED
Prior Semantic Gate: RETAINED_BUT_REBASED
New Popup semantics: NOT REQUIRED
Existing PlanStep reuse: REQUIRED_FIRST_OPTION
Existing SC-P3-002 protocol: REUSE_UNCHANGED
Existing U2 progress protocol: REUSE_UNCHANGED
Capstone: REUSE_AS_COMPOSITION_ORACLE, NOT AS U3 EXECUTION INPUT
Legacy mechanism migration: REJECTED
Runtime changes: NONE
Tests changed: NONE
OpenSpec changes: NONE
```

## Recommended Next Task

`PROJECT_LEADER_U3_F2_LOCAL_OBSTRUCTION_AUTHORITY_SEMANTIC_GATE_REBASED`

Resolve the five revised questions above. Do not design a Popup subsystem or
implement Runtime. If the minimum truthful representation requires a material
public semantic/API purchase, return the exact Human Gate packet; otherwise
return the approved bounded semantic envelope and next Architecture Fit Check.

