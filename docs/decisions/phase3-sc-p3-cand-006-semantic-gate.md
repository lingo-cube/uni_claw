# SC-P3-CAND-006 Semantic Gate — Bounded Safety Classification of Newly Discovered Settings Candidates

> Date: 2026-08-09 | Status: APPROVED | Decision: `SEMANTIC_PURCHASE_REQUIRED`
> Scope: bounded Scenario registration and Semantic Gate only. OpenSpec, implementation tasks, Runtime code, Runtime tests, Harness changes, Capstone implementation, Runtime refactor, and S1/S2/S3 work are not authorized by this decision.

## Candidate

- ID: `SC-P3-CAND-006`
- Title: **Bounded Safety Classification of Newly Discovered Settings Candidates**
- Evidence confidence: `HIGH`
- Dependency: fresh bounded Settings Observation evidence plus frozen Agent/Container/Traversal/Environment ownership and GoalEvidence completion authority.

## Reality Distinction

```text
candidate observed
!=
candidate semantically authorized
!=
action dispatched
```

Observation can prove that a physically actionable candidate is visible. It cannot prove that the candidate is permitted under the bounded read-only intent. An authorized candidate may be offered to the existing local execution path, but authorization still does not prove dispatch success, world effect, or Goal completion.

Conceptual outcomes are:

- **Authorized:** fresh evidence plus the bounded read-only intent positively permits the candidate;
- **Rejected:** fresh evidence positively proves that the candidate is outside the bounded read-only intent;
- **Unresolved:** available evidence cannot prove safe authorization, so authorization is not granted.

These outcomes do not purchase an enum or stored status.

## Scenario Boundary

The bounded Scenario assumes one fresh Observation already exposes candidates in one Settings Container. It does not discover an arbitrary UI tree or plan a multi-page route.

The Observation contains at least:

1. a safe navigation-like candidate S;
2. a navigation-like candidate D whose text/evidence indicates a destructive operation;
3. a state-changing candidate T whose `SwitchState` proves control state even without dangerous wording;
4. an optional candidate U whose available evidence is insufficient.

The Scenario proves one pre-dispatch classification round and at most one authorized safe-navigation execution through existing Tap mechanics. General candidate discovery, autonomous planning, arbitrary semantic understanding, all-source interception, and Capstone traversal remain outside the boundary.

## Existing Semantic Audit

### Observation and ObservedElement

`Observation.Elements` plus each immutable `ObservedElement.Text`, `SwitchState`, and `Index` can prove that S/D/T/U were freshly observed. `SequenceNumber` provides the deterministic evidence boundary. These values are evidence under I-4 and carry no authorization meaning.

### Plan and PlanStep

The current immutable Plan pre-enumerates exact target/action descriptions. A fixed PlanStep can prove caller preauthorization but cannot express why a newly observed, non-pre-enumerated candidate is eligible, rejected, or unresolved. Adding every visible candidate to Plan would collapse observation into approval and would not satisfy this Scenario.

Plan remains a hypothesis under I-5. The bounded behavior may nominate one freshly authorized candidate to the existing local execution path without purchasing a generalized dynamic planner or rewriting the complete route.

### Goal and GoalEvidence

`Goal.EvidenceEvaluator` evaluates whole-Goal completion and returns `GoalEvidence`. Reusing it for per-candidate authorization would conflate eligibility with final completion and violate I-10. Goal is nevertheless the smallest existing Agent-owned immutable intent surface on which to carry one separate bounded authorization criterion.

### WorldBelief

WorldBelief can describe the current semantic page and its evidence provenance. It cannot decide whether an element is authorized under a read-only intent.

### Traversal

Traversal can select, check, ground, execute, observe, and verify a local step. Its missing-target and unsupported-action failures are mechanical pre-dispatch failures, not semantic safety decisions. Reusing them as authorization denial would distort the journal and create duplicate authority.

### Journal, Trace, Trap, and ActionHistory

- Traversal journal proves what entered the local step protocol and whether an action was dispatched.
- Agent-owned Trace can append a pre-dispatch event with candidate identity/evidence sequence, authorization outcome, and explicit reason while leaving `Action` and `ActionId` absent.
- ActionHistory proves that no denied/unresolved candidate action reached Environment.
- Trap represents a loss of trusted world belief and is not an ordinary authorization-denial receipt; its frozen seven-field meaning remains unchanged.

Existing Trace plus zero matching journal/action entries is sufficient as the denial evidence surface. A new audit log, manager, Trace field, or Trap kind is unnecessary.

## Safety Decision Authority

Agent is the sole semantic authorization authority.

The Agent owns Goal/intent, receives the fresh Observation, evaluates the bounded criterion, records the authorization evidence in Trace, and decides whether an authorized candidate may enter the existing Container/Traversal path.

Traversal does not independently allow or deny semantic safety. It may still reject mechanically when grounding, preconditions, action parsing, or fresh execution protocol checks fail. These local failures do not override an Agent denial and cannot authorize an unresolved candidate.

Environment remains the external Observation/dispatch boundary and receives no candidate authorization authority.

Duplicate semantic authority: **NO**.

## Why Existing Semantics Are Insufficient

Existing semantics can represent candidate observation and actual execution but cannot represent the missing middle distinction:

```text
observed candidate
→ bounded authorization evidence
→ authorized / rejected / unresolved
→ optional execution
```

ActionHistory absence alone does not explain why dispatch did not occur. Existing whole-Goal evidence cannot be reused. Plan presence would preauthorize the candidate and erase the Scenario pressure. Traversal failure would turn semantic rejection into a local execution error. A new bounded authorization semantic is therefore required.

## Minimum Semantic Purchase

### `CandidateAuthorizationEvidence`

One immutable production value with exactly two fields:

1. `bool? Authorized`;
2. non-empty `string Reason`.

Meaning:

- `true`: the supplied fresh Observation and candidate positively satisfy the bounded read-only authorization criterion;
- `false`: fresh evidence positively rejects the candidate under the bounded read-only intent;
- `null`: the available evidence is insufficient; authorization is not granted;
- `Reason`: deterministic, human-readable evidence explaining the outcome. It is evidence for Agent judgement, not a policy rule engine or universal safety taxonomy.

### `Goal.CandidateAuthorizationEvaluator`

Add exactly one optional immutable field:

```csharp
Func<Observation, ObservedElement, CandidateAuthorizationEvidence>?
    CandidateAuthorizationEvaluator
```

The evaluator is part of the Agent-owned bounded Goal/intent. It must be deterministic, side-effect-free, and depend only on the supplied fresh Observation and a candidate contained in that Observation. It cannot read or mutate Runtime owners, call Environment, perform dispatch, or set RunState.

An absent evaluator grants no authorization to newly discovered, non-preauthorized candidates and preserves existing fixed-Plan behavior.

### Why a smaller representation is insufficient

- A single boolean cannot distinguish positive rejection from insufficient evidence.
- A nullable boolean without a reason cannot honestly explain why no dispatch occurred without hardcoding Scenario rules into Agent.
- Reusing `GoalEvidence` would conflate candidate authorization with final completion.
- Reusing `TraversalStepResult.Failed` would conflate semantic denial with local execution failure.
- A policy string would require an unpurchased parser/interpreter.
- Separate evaluator and reason-provider fields could diverge and would be larger than one two-field immutable result value.

## Purchase Budget

- New production model types: **1** (`CandidateAuthorizationEvidence`).
- New production fields: **3** total — two immutable value fields plus one optional immutable Goal field.
- New enums: **0**.
- New interfaces: **0**.
- New components: **0**.
- New mutable-state owners: **0**.
- Ownership delta: **NONE**.
- Authority delta: **NONE**.

## Formal Scenario

### Positive — safe navigation

```text
fresh bounded Settings Observation
→ S appears
→ Goal criterion evaluates S to Authorized=true with explicit reason
→ Agent may nominate S to existing Container/Traversal Tap path
→ Traversal performs normal Select → Execute → Observe → Verify
→ only fresh post-action GoalEvidence may complete the Run
```

### Destructive — deny overrides navigation-like evidence

```text
fresh Observation
→ D appears navigation-like and also carries destructive text/evidence
→ criterion returns Authorized=false
→ Agent records deterministic pre-dispatch Trace evidence
→ D never enters Traversal and has zero Environment dispatches
```

### State-changing — read-only intent denies mutation

```text
fresh Observation
→ T exposes state-changing evidence through SwitchState
→ no dangerous keyword is required
→ criterion returns Authorized=false
→ zero dispatch
```

### Unknown — insufficient evidence defaults to non-execution

```text
fresh Observation
→ U cannot be safely classified
→ criterion returns Authorized=null
→ Agent records unresolved non-authorization
→ zero dispatch and no fabricated completion
```

### Replay

Equal RunId, fresh Observation, candidate values, bounded Goal evaluator, and world input produce equal authorization outcomes/reasons, Trace denial evidence, Traversal journal, ActionHistory, GoalEvidence, and final RunState.

## Completion Boundary

Observed candidate membership and required-work membership are distinct:

- Observation membership means only “reachable/visible candidate evidence exists”.
- Authorization `true` means the candidate may be considered for execution; it does not by itself make the candidate required work.
- Required safe work remains defined by Agent-owned Goal/approved bounded scope and evidence-backed progress.
- Rejected or unresolved candidates do not enter approved required-work inventory merely because they were visible.
- Authorization, dispatch, and local success never set `RunState.Completed`; only Agent consumption of satisfied GoalEvidence may complete the Run.

## Explicitly Not Purchased

- SafetyManager, RiskEngine, SafetyEngine, PolicyEngine, RuleEngine, SafeActionExecutor, ActionAuthorizationManager, or any new mutable safety owner;
- RiskLevel or other authorization enum;
- Confidence, policy hash, coordinates, Fingerprint, Vision/VLM/AI safety judgement;
- NavigationGraph, NavigationStack, dynamic planner, candidate-discovery framework, or universal all-action interception;
- persistent authorization cache/history, policy registry/parser, audit component, or new Trace/Trap/journal fields;
- Capstone implementation, Harness change, Runtime refactor, S1/S2/S3 work, or Phase completion.

## Architecture Shape

- Agent: `NON_BLOCKING_STRUCTURAL_PRESSURE`. It gains one bounded decision branch inside existing semantic authority; the pressure does not justify extraction or refactor.
- Container: `COHESIVE`; no ownership or behavior authority change.
- Traversal: remains the deterministic local execution kernel and gains no semantic safety authority.
- Architecture review required: **NO**.

## Capstone Impact

- SC-P3-CAND-006: approved at Semantic Gate with an exact one-type/three-field budget; OpenSpec, tasks, implementation, formal proof, validation, and freeze remain absent.
- Autonomous discovered-candidate safety: semantic distinction is approved but not yet proven as a frozen S0 capability.
- SC-S0-CAPSTONE-001: remains `CAPSTONE / PREREQUISITES_MAPPED`; it is not `READY_FOR_S0_RUN`.
- Remaining direct blockers: SC-P3-CAND-006 lifecycle completion/freeze. Legacy simulation classification independently remains required for `S0_BASELINE_READY`.

## Next Decision

```text
RECONCILE_SPEC
```

This decision authorizes only a later repository-approved OpenSpec reconciliation. It does not create OpenSpec, tasks, or Runtime behavior in this step.
