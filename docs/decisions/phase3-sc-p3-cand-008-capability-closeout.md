# SC-P3-CAND-008 Capability Closeout

> Status: Frozen Capability | Date: 2026-08-09
> Scope: SC-P3-CAND-008 only — this is not a Phase 3 freeze, S0 baseline decision, or Capstone authorization.
> Authority: acceptance receipt for `openspec/changes/phase3-bounded-cross-page-discovery/`; it does not replace the approved Scenario, Spec, Architecture Contract, prior frozen capability closeouts, or remaining roadmap gates.

## Capability

**Bounded Fresh-Evidence Cross-Page Branch Discovery and Route Continuation**

Semantic Gate: `SEMANTIC_PURCHASE_REQUIRED`

Inventory Model Test: `MINIMUM_BRANCH_INVENTORY_EVIDENCE_REQUIRED`

## Proven Behavior

```text
fresh accepted Container P evidence at depth 0
→ complete required inventory {A}
→ independent authorization of required A
→ exactly one existing Tap A
→ fresh Observe + reconcile to Container A at depth 1
→ complete required inventory {C}
→ independent authorization of required C
→ exactly one existing Tap C
→ fresh Observe + reconcile to Container C at depth 2
→ positive empty bounded inventory
→ only independently satisfied GoalEvidence may complete the Run
```

The accepted slice proves:

- Observed candidate, complete required inventory membership, candidate authorization, next selection, dispatch, semantic Container transition, branch progress, positive leaf evidence, GoalEvidence, and Run completion remain distinct.
- `BranchInventoryEvidence.RequiredBranchEvidence` uses a non-null non-empty immutable map for a proven complete required inventory, an empty non-null map for a positively proven bounded leaf, and null for unresolved inventory completeness; every result has a deterministic non-empty reason.
- Each map entry binds one semantic branch identity to an accepted source Observation sequence; invalid, stale, conflicting, or non-current source evidence cannot replace valid progress or authorize dispatch.
- Agent is the only consumer of the Goal-owned branch-inventory criterion and remains the sole authority for inventory acceptance, evidence-backed semantic depth, next selection, cross-Container progress, active Container changes, GoalEvidence, and final RunState.
- Container retains page-local accepted evidence and local progress only; Traversal executes one nominated deterministic step and owns its journal; Environment reports external Observation and dispatch outcome only.
- The formal P → A → C route is discovered although concrete A and C targets are absent from the initial immutable Plan.
- A required branch is nominated only when it is both present in the proven inventory and independently authorized by the frozen SC-P3-CAND-006 criterion. Rejected or unresolved authorization produces zero matching Tap.
- A candidate that is executable but absent from the proven required inventory is not selected merely because it is authorized.
- Every accepted inventory/authorization cycle nominates at most one existing Tap and requires a fresh post-action Observation and semantic Container reconciliation before another inventory decision.
- Null inventory produces zero discovered-branch dispatch and cannot fabricate a leaf, branch completion, GoalEvidence, or Run completion.
- At the semantic depth boundary, visible deeper work remains unresolved and is not dispatched. SC-P3-CAND-007 same-Container viewport movement can extend accepted evidence without incrementing semantic depth.
- Fresh inventory refresh preserves only still-required proven sibling progress; stale/conflicting evidence does not erase valid parent inventory, and proven work is not blindly redispatched.
- Positive empty inventory is not local completion, branch completion, GoalEvidence, or Run completion. Unsatisfied independent GoalEvidence leaves the Run incomplete.
- An absent inventory evaluator preserves frozen fixed-Plan behavior.
- Equal inputs replay equal inventory reasons, progress, ActionHistory, Observations, journal, Trace, GoalEvidence outcome, and final RunState.

## Production Delta

- Model types: exactly +1 immutable `BranchInventoryEvidence` value.
- Fields: exactly +3 total — `RequiredBranchEvidence`, `Reason`, and optional immutable `Goal.BranchInventoryEvaluator`.
- Enums: +0.
- Interfaces: +0.
- Components: +0.
- New mutable-state fields: +0.
- New mutable-state owners: +0.
- Behavior: one opt-in bounded Agent control-flow branch that derives semantic depth as Run-local evidence, validates inventory sources, nominates one existing Tap, and reconciles before repeating.

## Ownership and Authority

- Ownership delta: **NONE**.
- Authority delta: **NONE**.
- Environment remains external-world Observation and dispatch-outcome authority only.
- Traversal remains the deterministic one-step Execute → Observe → Verify mechanics and journal owner.
- Container remains the semantic-page continuity, accepted same-Container evidence, and local-progress owner.
- Agent remains the sole inventory/depth/selection/cross-Container progress/active-Container/GoalEvidence/final-RunState authority.
- Recovery remains unchanged and receives no branch-discovery, route, selection, or completion authority.

## Frozen Boundary

| Evidence / decision | Frozen meaning |
|---|---|
| non-null non-empty inventory | Complete bounded required branch membership is proven from accepted source evidence; no authorization or completion is implied. |
| empty non-null inventory | Bounded leaf is positively proven; no local, branch, Goal, or Run completion is implied. |
| null inventory | Inventory completeness remains unresolved; zero discovered-branch dispatch and no fabricated leaf/completion. |
| required + authorized | Agent may nominate at most one existing Tap followed by fresh Observe/Reconcile. |
| required + rejected/unresolved | Zero matching dispatch; route remains explicitly unresolved. |
| authorized but not required | Not selected merely because it is executable. |
| evaluator absent | Existing fixed-Plan behavior remains unchanged. |

## Explicitly Not Purchased

- Generic dynamic planner/re-plan, arbitrary action synthesis, navigation graph/tree/stack, frontier, persistent route/depth model, or Container hierarchy;
- BranchManager, NavigationManager, ProgressManager, workflow engine, FSM, new Back action, generic backtracking, or parent-return framework;
- Fingerprint, Confidence, coordinates, semantic identity algorithm, Vision/VLM/AI semantics, generic retry/uncertainty framework, or new Recovery behavior;
- Runtime refactor, Capstone implementation, Harness changes, S1/S2/S3 work, S0 graduation, Phase freeze, or Phase completion.

## Structural Pressure

Agent now contains another opt-in bounded control-flow branch. The path remains inside existing Agent authority, uses only Run-local derived semantic depth, adds no mutable state field or owner, and is fully expressed within the approved budget. This is non-blocking structural pressure and does not authorize a planner, route abstraction, extraction, compression, or Runtime refactor.

## Acceptance Receipt

- OpenSpec: strict validation passed; proposal/design/specs/tasks complete.
- Tasks: 4/4 complete.
- Independent validation: PASS.
- Build: 0 warnings, 0 errors.
- Tests: 334/334 passed.
- SC-P3-CAND-008 fixture/behavior/formal tests: 29/29 passed.
- `BranchInventoryEvidence` model tests: 5/5 passed.
- Formal Scenario tests: 12/12 passed.
- Architecture Guards: 8/8 passed.
- Consistency checks: C1–C9 ALL PASS.
- Semantic diagnostics: 0 warnings.
- Production delta: exactly one approved immutable type, three approved fields, and one existing-Agent opt-in control-flow adjustment.
- Ownership delta: NONE.
- Authority delta: NONE.
- Semantic drift: NONE.

## State

```text
SC_P3_CAND_008_FROZEN_CAPABILITY
```

This state does **not** mean `PHASE_3_FROZEN`, `S0_BASELINE_READY`, `S0_GRADUATED`, `CAPSTONE READY`, or `PHASE_COMPLETE`. Capstone authorization/execution, OpenSpec archive, any S1/S2/S3 work, and any new Scenario require separate authority.
