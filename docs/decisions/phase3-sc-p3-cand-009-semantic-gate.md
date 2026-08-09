# SC-P3-CAND-009 Semantic Gate — Recovery Revalidation for a Discovered Non-Plan Branch

> Date: 2026-08-09 | Status: APPROVED (HUMAN) | Decision: `SEMANTIC_PURCHASE_REQUIRED`
> Human decision: `ACCEPT_OPTION_C_BOUNDARY`
> Scope: approved semantic/architecture boundary and OpenSpec reconciliation only. Runtime implementation, implementation-task generation, Capstone execution, roadmap readiness, Phase 3 completion, and S0 graduation are not authorized.

## Candidate

- ID: `SC-P3-CAND-009`
- Title: **Evidence-Validated Resume for Freshly Discovered Non-Plan Branch Progress After Agent Recovery**
- Evidence confidence: `HIGH`
- Dependencies: frozen SC-P3-CAND-004 progress provenance, SC-P3-CAND-005 post-Recovery three-way effect evaluation, SC-P3-CAND-006 candidate authorization, and SC-P3-CAND-008 bounded discovered-branch inventory.

## Approved Reality Distinction

```text
freshly discovered branch A
+
historical evidence-backed completion for A
+
verified Agent Recovery to the expected parent

!=

A's durable external effect is valid in the recovered world
```

Historical completion, refreshed required-work inventory, parent identity, and `RecoveryResult.Verified` remain evidence with different meanings. None independently proves the current external effect of A.

## Existing Semantic Gap

SC-P3-CAND-005 carries its durable branch-effect criterion on a `PlanStep`. SC-P3-CAND-008 intentionally permits a required branch to be discovered although its concrete target is absent from the immutable Plan. Requiring the discovered branch to become a `PlanStep` would change Plan semantics and would not solve the approved carrier distinction honestly.

The missing semantic is one immutable association between an already-established bounded branch identity and the external-effect criterion that fresh recovered-world evidence must evaluate. It is not a validity state, lifecycle state, route model, or new decision authority.

## Approved Minimum Carrier

Add exactly one optional immutable branch-scoped criterion carrier to Goal scope:

```csharp
public sealed record BranchEffectCriterion(
    string BranchIdentity,
    Func<Observation, bool?> Evaluator);

Goal(..., BranchEffectCriterion? DiscoveredBranchEffectCriterion = null)
```

Semantic meaning:

- `BranchIdentity` names one existing semantic identity within the bounded active parent scope. It does not discover, authorize, select, or complete the branch.
- `Evaluator` represents the durable observable external proposition associated with that branch.
- the evaluator must be deterministic and side-effect-free and may read only the supplied fresh `Observation` plus immutable values already captured by the caller;
- `true` means fresh evidence positively revalidates the effect;
- `false` means fresh evidence positively contradicts the effect;
- `null` means the effect is not observable or otherwise remains unresolved;
- the carrier is a criterion/hypothesis, never proof by itself;
- absence or identity mismatch means unresolved.

Exactly one optional carrier is approved for the bounded Scenario. A collection, map, registry, resolver service, identity service, or generalized predicate framework is not approved.

## Association Boundary

Agent may evaluate the carrier only when all of the following are established independently:

1. SC-P3-CAND-008 inventory evidence contains the same branch identity under the active parent;
2. SC-P3-CAND-004 progress provenance records historical evidence-backed completion for that identity under the same parent;
3. the carrier identity matches that existing identity exactly within the bounded parent scope;
4. exactly one Agent-scope drift and a verified Recovery have occurred;
5. the supplied Observation is fresh and post-dates the Recovery verification boundary.

The carrier cannot establish inventory membership, authorization, parent association, historical completion, or freshness. Ambiguous parent scope, stale evidence, absent carrier, or identity mismatch remains unresolved and cannot be repaired by a new identity authority.

## Three-Way Reconciliation

- **True:** Agent may treat A's historical completion as revalidated for the current recovered-world reconciliation and continue independently unresolved sibling B without redispatching A.
- **False:** A's historical provenance remains observable, but A cannot contribute to current subtree or Goal evaluation. No repair, redispatch, or success is fabricated.
- **Null or absent:** A's validity remains unresolved. Historical provenance contributes nothing, A is not blindly redispatched, and Agent uses existing escalation/non-completion surfaces.

The evaluation result itself must not be stored as a validity, lifecycle, Recovery, or completion status. Existing immutable progress snapshots and Trace/journal may preserve evidence provenance; they may not acquire a new effect-validity field or enum.

## Ownership and Authority

- Agent remains the sole authority for carrier interpretation, retain/invalidate/unresolved, resume/escalation, cross-Container progress, GoalEvidence, and final RunState.
- Recovery remains restore → observe → verify mechanics only. Recovery verification does not imply branch-effect verification.
- Container remains page-local identity/evidence/progress owner.
- Traversal remains deterministic one-step execution and journal owner.
- Environment reports external Observation and dispatch outcomes only.

Ownership delta: `NONE`.

Authority delta: `NONE`.

New mutable-state owners: `0`.

## Purchase Budget

- New semantic carriers: **1** (`BranchEffectCriterion`).
- New production model types: **1**.
- New production fields: **3** total — two immutable carrier fields and one optional immutable Goal field.
- New enums: **0**.
- New interfaces: **0**.
- New components/services: **0**.
- New mutable-state fields: **0**.
- New mutable-state owners: **0**.
- Ownership delta: **NONE**.
- Authority delta: **NONE**.

This physical budget is the smallest representation that keeps identity, criterion, and Goal-held durability explicit without changing Plan, `BranchProgressEvidence`, `BranchInventoryEvidence`, or `GoalEvidence` semantics.

## Explicitly Not Purchased

- Graph, Tree, Stack, Frontier, route registry, persistent route/depth state, checkpoint, or `ResumeToken`;
- `DynamicPlan`, `DynamicPlanner`, generic planner/re-plan, or arbitrary action synthesis;
- `BranchManager`, `ProgressManager`, navigation manager, workflow engine, or Recovery FSM;
- generalized multi-parent routing, generalized branch lifecycle, global semantic identity, or a new semantic identity authority;
- generic effect registry, predicate framework, effect-validity enum, freshness epoch, or stored evaluation result;
- Recovery during an unfinished dynamic-discovery continuation;
- Recovery ownership of branch progress or Recovery dependencies on Container/Traversal;
- reinterpretation of Plan, `BranchProgressEvidence`, `BranchInventoryEvidence`, `CandidateAuthorizationEvidence`, or `GoalEvidence`;
- Runtime implementation, tests, Harness changes, Capstone execution, roadmap readiness, Phase completion, or S0 graduation.

## Reopen Condition

OpenSpec or later implementation must stop and reopen the appropriate Semantic/Architecture/Human Gate if the bounded identity-to-criterion association cannot be expressed without a collection/registry, new identity authority, persistent route/frontier state, new mutable-state owner, ownership movement, or authority movement.

## Next Decision

```text
RECONCILE_SPEC_SC_P3_CAND_009
```

Reconciliation must preserve this exact one-carrier, one-parent, one-Recovery boundary and all explicit exclusions.
