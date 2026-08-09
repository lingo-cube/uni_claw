# SC-P3-CAND-008 — Bounded Fresh-Evidence Cross-Page Branch Discovery and Route Continuation

> Phase 3 | Semantic Gate: `SEMANTIC_PURCHASE_REQUIRED`
> Approved Production Model Delta: one immutable `BranchInventoryEvidence` value
> Production Fields: `+3` total — two immutable evidence fields plus one optional immutable Goal criterion
> Enums: `+0` | Interfaces: `+0` | Components: `+0` | New Mutable-State Fields: `+0` | New Mutable-State Owners: `+0`
> Ownership Delta: `NONE` | Authority Delta: `NONE`
> Consumer: `specs/bounded-cross-page-discovery/spec.md`

## Goal

Prove that Agent can repeatedly derive the complete required child-branch inventory from fresh bounded evidence in each newly entered semantic Container and nominate one independently authorized branch at a time without encoding the complete concrete route in the initial Plan.

## Given

- Runtime is Running with semantic Container P active at depth 0.
- P owns bounded accepted fresh Observation evidence whose complete required inventory is A.
- Fresh evidence in child A will expose required child C.
- Fresh evidence in child C will positively prove an empty bounded inventory at the approved depth boundary.
- A and C are absent from the initial fixed Plan's concrete targets.
- Goal carries deterministic branch-inventory, candidate-authorization, and GoalEvidence evaluators.
- Existing actions can execute the nominated Tap steps.
- Agent owns inventory interpretation, semantic depth, next-branch selection, active Container changes, GoalEvidence, and final RunState.

## Positive Route Continuation

```text
fresh accepted P evidence
→ inventory = {A}
→ A independently authorized
→ exactly one Tap A
→ fresh Observation reconciles to Container A at depth 1
→ inventory = {C}
→ C independently authorized
→ exactly one Tap C
→ fresh Observation reconciles to Container C at depth 2
→ inventory = empty with positive reason
→ independently satisfied GoalEvidence may complete the Run
```

The initial Plan does not contain concrete Tap targets A or C. Plan remains an immutable hypothesis and no generic planner or persistent route model is created.

## Unresolved Inventory Branch

```text
fresh accepted Container evidence
→ inventory completeness unresolved
→ RequiredBranchEvidence = null
→ zero discovered-branch dispatch
→ no fabricated leaf, local completion, branch completion, or Goal completion
```

## Authorization Boundary

```text
branch A is required
→ authorization false or null
→ zero Tap A
→ explicit unresolved route evidence
→ no fabricated completion
```

An authorized candidate absent from the complete required inventory is also not selected merely because it is executable.

## Depth Boundary

At the approved semantic depth bound, fresh bounded evidence must positively prove an empty required inventory or remain unresolved. No deeper child Tap is dispatched. Additional accepted same-Container viewport evidence under SC-P3-CAND-007 does not increment semantic depth.

## Progress Composition

When fresh P evidence is revisited after SC-P3-CAND-004 has already proven A complete, the refreshed inventory preserves valid A evidence, does not redispatch A, and leaves another required sibling B unresolved. This Scenario does not purchase parent-return mechanics or generic backtracking; it reuses existing visible return affordances and frozen progress semantics.

## Required Assertions

1. Branch inventory membership, candidate authorization, next selection, dispatch, branch completion, and Goal completion remain distinct.
2. A non-null inventory is accepted only from bounded current same-Container evidence and validated source Observation sequences.
3. Empty non-null inventory and null unresolved inventory remain distinct and have deterministic reasons.
4. Agent is the sole consumer and authority for inventory, semantic depth, selection, GoalEvidence, and final RunState.
5. P → A → C is discovered across fresh reconciled Containers although A and C are absent from initial Plan targets.
6. Each positive inventory/authorization decision nominates at most one existing Tap before another fresh Observe/Reconcile cycle.
7. Required rejected/unresolved branches and unresolved inventory produce zero matching dispatches.
8. Candidates beyond the semantic depth bound are not dispatched; same-Container viewport evidence does not consume depth.
9. Parent revisit preserves valid proven progress and does not blindly redispatch completed work.
10. Proven empty inventory does not independently set local completion, branch completion, GoalEvidence, or RunState.
11. Only independently satisfied GoalEvidence consumed by Agent may complete the Run.
12. Equal inputs replay equal inventories/reasons, progress, actions, journal, Trace, GoalEvidence, and final state.

## Ownership and Authority

- Environment reports external Observations and dispatch outcomes only.
- Traversal owns deterministic execution and journal evidence for one nominated local step.
- Container owns semantic-page continuity, current/accepted same-Container evidence, and local progress.
- Agent owns Goal-scoped inventory interpretation, semantic depth, next-branch selection, cross-Container progress, active Container changes, GoalEvidence, and final RunState.
- Recovery ownership remains frozen and receives no branch-discovery authority.

## Completion Boundary

Observed membership, required inventory, authorization, selection, dispatch, world effect, Container transition, positive leaf evidence, branch completion, and Run completion are distinct. A Run may complete only from independently satisfied GoalEvidence.

## Explicitly Deferred

- Generic dynamic planner/re-plan, arbitrary action synthesis, navigation graph/tree/stack, persistent route model, or Container hierarchy.
- BranchManager, NavigationManager, ProgressManager, workflow engine, FSM, new Back action, or generic backtracking policy.
- Fingerprint, Confidence, coordinates, semantic identity algorithm, Vision/VLM/AI semantics, generic retry/uncertainty, or new Recovery behavior.
- Runtime refactor, Capstone implementation, Harness changes, S1/S2/S3 work, or Phase completion.
