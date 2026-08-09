## Context

Frozen SC-P3-CAND-004 records parent-scoped inventory and historical completion provenance in Agent-owned immutable `BranchProgressEvidence`. Frozen SC-P3-CAND-005 evaluates a durable PlanStep-carried branch effect against fresh evidence after verified Recovery. Frozen SC-P3-CAND-008 permits a required branch to be discovered and executed without appearing in the initial Plan.

The composition gap is criterion carriage, not branch discovery, authorization, progress ownership, Recovery mechanics, or navigation. The approved Scenario contains one parent, one discovered completed branch A, one remaining sibling B, one external drift, one verified Recovery, and one fresh recovered-world evaluation.

## Goals / Non-Goals

**Goals:**

- Associate one already-defined bounded semantic branch identity with one durable external-effect criterion without making the branch a PlanStep.
- Evaluate only fresh post-verified-Recovery Observation evidence.
- Distinguish positive, contradicted, and unresolved outcomes without storing validity/lifecycle state.
- Preserve historical progress provenance independently from current-world effect validity.
- Let Agent continue B only when A is freshly revalidated, without blindly redispatching A.
- Preserve existing ownership, authority, dependency direction, completion semantics, and deterministic replay.

**Non-Goals:**

- Define a global branch identity, identity service, registry, collection of criteria, route model, graph/tree/stack/frontier, dynamic Plan, planner, manager, workflow, or FSM.
- Resume an unfinished dynamic-discovery loop after Recovery.
- Add persistent validity, lifecycle, Recovery, freshness, epoch, or completion status.
- Change Plan, `BranchProgressEvidence`, `BranchInventoryEvidence`, authorization, or GoalEvidence semantics.
- Implement Capstone, alter roadmap readiness, complete Phase 3, or graduate S0.

## Decisions

### Use one singular immutable Goal-held carrier

Add one immutable value:

```csharp
public sealed record BranchEffectCriterion(
    string BranchIdentity,
    Func<Observation, bool?> Evaluator);
```

Add one optional immutable Goal field:

```csharp
BranchEffectCriterion? DiscoveredBranchEffectCriterion = null
```

Goal is already Agent-owned durable hypothesis input across Recovery. A singular optional carrier survives the Recovery boundary without requiring Plan mutation, progress-state duplication, a registry, or a new mutable owner. The carrier does not change `Goal.EvidenceEvaluator` or `GoalEvidence` meaning.

Alternative rejected: add the discovered branch to Plan or create a required PlanStep. The approved boundary explicitly preserves Plan semantics and non-Plan discovery.

Alternative rejected: attach the criterion to `BranchProgressEvidence`. That would mix historical provenance with an external-effect proposition and change frozen progress meaning.

Alternative rejected: attach it to `BranchInventoryEvidence`. Inventory membership is required-work evidence only, not effect meaning or validity.

Alternative rejected: reuse `Goal.EvidenceEvaluator`. Whole-Goal completion evidence cannot represent a branch-scoped three-way effect judgement.

Alternative rejected: use a dictionary, registry, resolver service, or identity authority. The bounded Scenario needs exactly one association; generalized lookup is not purchased.

### Keep identity association bounded and independently proven

`BranchIdentity` is not an identity authority. It must exactly match the existing parent-scoped identity already present in both accepted `BranchInventoryEvidence` and historical `BranchProgressEvidence`. Agent performs the comparison because Agent already owns discovered-branch interpretation and cross-Container progress.

The carrier cannot create inventory membership, completion, authorization, parent continuity, or freshness. A missing carrier, identity mismatch, conflicting parent scope, or ambiguous association yields unresolved. No fallback parsing, fuzzy matching, route registry, or generated identity is allowed.

### Evaluate only after verified Recovery using fresh Observation

The required ordering is:

```text
A historical completion exists
→ external Agent-scope drift is observed
→ Recovery restores, observes, and verifies position
→ Agent reconciles one fresh recovered-world Observation
→ Agent matches the bounded carrier to A
→ Agent evaluates A's criterion on that Observation
→ true / false / null is derived
```

The evaluator is deterministic, side-effect-free, and Observation-only. It cannot read or mutate Agent, Recovery, Container, Traversal, Environment, progress, journal, Trace, or RunState. `RecoveryResult.Verified`, parent identity, refreshed inventory, dispatch history, and pre-Recovery evidence are insufficient inputs by themselves.

### Derive outcomes without persistent status

- `true`: A may contribute to the current recovered-world reconciliation; Agent may continue B without redispatching A.
- `false`: A cannot contribute; historical provenance remains observable and no success/repair is fabricated.
- `null` or absent/mismatch: A remains unresolved, contributes nothing, and is not blindly redispatched.

The nullable result is consumed immediately. No boolean field, enum, lifecycle state, Recovery state, completion status, freshness epoch, or new mutable dictionary is added. Existing progress snapshots and Trace/journal may record evidence provenance only; they do not become an effect-validity store.

### Preserve ownership and authority

Agent remains the only interpreter of the association and fresh result and the only authority for retain/invalidate/unresolved, resume/escalation, cross-Container progress, GoalEvidence, and RunState. Recovery remains restore/observe/verify. Container and Traversal receive no new dependency or authority.

### Preserve deterministic replay

Equal RunId, Goal/carrier, initial Plan, accepted inventory/progress evidence, disturbance schedule, Recovery results, fresh Observations, authorization results, and Environment transitions must replay equal criterion outcomes, contributing progress, actions, journal, Trace, GoalEvidence, and final RunState.

## Risks / Trade-offs

- [Risk] One Goal-held association could be mistaken for inventory membership. → Require independent CAND-008 inventory proof before matching; the carrier cannot discover a branch.
- [Risk] String identity may be ambiguous outside one parent. → Scope exact matching to the approved parent and return unresolved on ambiguity; global identity is deferred.
- [Risk] A future Scenario may need many discovered criteria. → Stop and reopen the Semantic/Architecture Gate; do not widen the singular carrier into a registry.
- [Risk] A true result could become stored validity state. → Consume the result immediately and preserve only existing evidence provenance surfaces.
- [Risk] Runtime flow may pressure recovery during unfinished discovery. → Explicitly exclude discovery-continuation Recovery; this Scenario recovers only after A already completed.

## Reopen Conditions

Stop before task generation or implementation if reconciliation requires a criterion collection, route/frontier state, global identity authority, new mutable-state field/owner, ownership or authority movement, Recovery progress ownership, or semantic reinterpretation of a frozen value.
