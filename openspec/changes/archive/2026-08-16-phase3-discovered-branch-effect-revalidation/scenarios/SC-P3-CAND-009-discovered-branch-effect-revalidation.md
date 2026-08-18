# SC-P3-CAND-009 — Evidence-Validated Resume for a Discovered Non-Plan Branch

> Phase 3 | Semantic Gate: `SEMANTIC_PURCHASE_REQUIRED` | Human Decision: `ACCEPT_OPTION_C_BOUNDARY`
> Approved Production Model Delta: one immutable `BranchEffectCriterion` carrier
> Production Fields: `+3` total — two immutable carrier fields plus one optional immutable Goal field
> Enums: `+0` | Interfaces: `+0` | Components: `+0` | New Mutable-State Fields: `+0` | New Mutable-State Owners: `+0`
> Ownership Delta: `NONE` | Authority Delta: `NONE`
> Consumer: `specs/discovered-branch-effect-revalidation/spec.md`

## Goal

Prove that Agent can revalidate, contradict, or leave unresolved the retained external effect of one completed freshly discovered non-Plan branch after one verified Agent Recovery, without treating historical completion or Recovery verification as current truth, without blindly redispatching the branch, and without creating navigation or lifecycle infrastructure.

## Given

- Runtime is Running with bounded parent Container P.
- Fresh accepted SC-P3-CAND-008 inventory evidence proves required sibling branches A and B under P.
- A is absent from the initial immutable Plan and is independently authorized under SC-P3-CAND-006.
- Existing Tap mechanics execute A, fresh evidence proves A complete, and Agent-owned SC-P3-CAND-004 progress retains its historical provenance.
- Goal carries exactly one immutable `BranchEffectCriterion` whose identity is A.
- The criterion is deterministic, side-effect-free, and reads only one supplied Observation.
- B remains independently required and unresolved.
- Exactly one external Agent-scope drift occurs after A completes.
- Existing Recovery restores, observes, and verifies P exactly once.
- Agent obtains one fresh recovered-world Observation after `RecoveryResult.Verified`.

## Positive Revalidation

```text
historical A completion under P
→ one external drift
→ Recovery restores / observes / verifies P
→ Agent reconciles fresh recovered-world Observation
→ singular carrier identity exactly matches A
→ A criterion evaluates true
→ A may contribute for the current reconciliation
→ zero duplicate A dispatch
→ Agent may continue independently unresolved B
→ final completion still requires independently satisfied GoalEvidence
```

## Contradicted Effect

```text
historical A completion under P
→ verified Recovery
→ matched A criterion evaluates fresh evidence false
→ historical provenance remains observable
→ A contributes nothing to current subtree / Goal evaluation
→ zero fabricated repair or success
→ zero blind A redispatch
```

## Unresolved Effect

```text
historical A completion under P
→ verified Recovery
→ criterion evaluates null, is absent, or cannot be matched exactly
→ A contributes nothing
→ zero blind A redispatch
→ explicit existing Agent non-completion / escalation
```

## Identity Boundary

The carrier may be used only when its A identity exactly matches independently accepted P inventory and P historical progress. The carrier does not prove that A exists, belongs to the required inventory, is authorized, completed, or is current. Identity mismatch, conflicting parent evidence, or ambiguous association remains unresolved. No registry, fuzzy matching, generated identity, or identity service is available.

## Freshness and Recovery Boundary

The effect criterion is evaluated only after verified Recovery and only against the fresh recovered-world Observation. Historical A evidence, correct P identity, refreshed P inventory, successful local mechanics, and `RecoveryResult.Verified` remain insufficient individually. Recovery performs no branch-progress interpretation.

## Required Assertions

1. A is discovered from accepted evidence and is absent from initial Plan targets.
2. The singular Goal-held carrier does not make A a PlanStep and does not prove inventory, authorization, completion, or validity.
3. Agent matches the carrier only when P inventory, P progress, and carrier identity all identify A exactly.
4. Criterion evaluation occurs only after one verified Recovery and uses only the fresh recovered-world Observation.
5. `true` permits A to contribute and B to continue without a duplicate A dispatch.
6. `false` preserves historical provenance but excludes A from current contribution and fabricates no repair or completion.
7. `null`, absent carrier, identity mismatch, stale evidence, or ambiguous parent scope leaves A unresolved and non-contributing.
8. The nullable result is derived, not persisted as validity, lifecycle, Recovery, or completion state.
9. `BranchProgressEvidence`, `BranchInventoryEvidence`, Plan, and GoalEvidence retain their frozen meanings.
10. Agent remains sole retain/invalidate/unresolved, resume/escalation, progress, GoalEvidence, and RunState authority.
11. Recovery remains restore → observe → verify mechanics and does not imply branch-effect verification.
12. Equal inputs replay equal outcomes, progress contribution, actions, journal, Trace, GoalEvidence, and final state.

## Completion Boundary

Discovered membership, authorization, historical completion, Recovery verification, carrier presence, criterion evaluation, current contribution, subtree completion, GoalEvidence, and Run completion are distinct. Only Agent consumption of independently satisfied GoalEvidence may complete the Run.

## Explicitly Deferred

- Graph, Tree, Stack, Frontier, route registry, persistent route/depth state, checkpoint, or ResumeToken.
- DynamicPlan, DynamicPlanner, generic planner/re-plan, arbitrary action synthesis, manager, workflow, or FSM.
- Generalized multi-parent routing, generalized branch lifecycle, global semantic identity, identity authority, criterion collection, or generic effect registry.
- New mutable-state field/owner, stored validity/lifecycle/Recovery/completion state, or freshness epoch.
- Recovery during an unfinished dynamic-discovery continuation or new Recovery ownership/dependency.
- Runtime refactor, Harness change, Capstone execution, roadmap readiness, Phase completion, or S0 graduation.
