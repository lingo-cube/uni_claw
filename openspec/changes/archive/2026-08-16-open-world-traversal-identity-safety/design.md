# Design: Open-World Traversal Identity Safety

## Existing Mechanism

`Agent.RunOpenWorldAsync` and `Agent.RunBoundedCrossPageDiscovery` already maintain:

- a run-local parent Container stack,
- semantic depth,
- per-parent `BranchProgressEvidence`,
- fresh child reconciliation before Container creation,
- unique authorized parent return,
- evidence-gated traversal completion.

## Gap

There is no run-local identity safety layer for cycles or duplicate semantic page identities. Depth bounds prevent infinite loops, but they do not detect A → B → A or same-page-different-branch duplicates.

## Minimal Implementation Delta

Add Agent-owned, run-local identity evidence for the open-world traversal path only:

1. Maintain an immutable run-local ancestry set of semantic page identities for the current parent stack.
2. Maintain an immutable run-local visited set of semantic page identities accepted during this open-world traversal.
3. Before creating a child Container from fresh reconciled evidence:

   - If the child semantic page identity is already in the ancestry set, reject the transition as a cycle.
   - If the child semantic page identity is already in the visited set from a different branch and no explicit merge rule is supplied, fail closed as ambiguous duplicate identity.

4. On successful parent return, remove the child identity from the ancestry set, but keep it in the visited set.
5. On bounded traversal completion, the visited set may be used as Agent-owned evidence that unique page coverage was attempted; it is not GoalEvidence and does not change completion authority.

## Preserved Boundaries

- Container remains the sole owner of local mutable belief.
- Traversal remains the execution kernel owner.
- CandidateAuthorization remains the boundary for safe candidate dispatch.
- GoalEvidence remains the only path to Run completion.
- No global graph, page registry, route planner, or semantic model is introduced.
