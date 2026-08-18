## Why

CC-04 can preserve a high-level open-world Settings traversal specification,
but the production Runtime cannot yet execute that representation through
verified parent return and sibling continuation. U2 closes this bounded
composition gap so runtime-discovered safe work within `depth <= N` can produce
honest traversal-shaped Goal evidence without pre-enumerating a concrete route.

## What Changes

- Add a bounded execution seam from a resolved open-world
  `IntentSemanticEnvelope` to the existing Agent control plane.
- Extend the existing opt-in branch-discovery path with run-local parent return,
  sibling continuation, depth/scope/safety enforcement, and final existing
  fresh GoalEvidence consumption after verified bounded traversal completion.
- Add deterministic positive, negative, boundary, and replay proof for
  `SC-U2-MUS-001`.
- Preserve the existing closed-world `Agent.RunAsync(Goal, Plan, ...)` behavior
  and all non-traversal Goal completion semantics.

## Capabilities

### New Capabilities

- `u2-open-world-settings-traversal`: Executes a validated open-world type-level
  Settings traversal without a pre-enumerated route and completes only from
  verified bounded traversal evidence consumed by Agent.

### Modified Capabilities

None. CP-04/07/12/14 and the frozen Phase 3 branch inventory, progress,
candidate safety, and local execution semantics remain unchanged and are
composed by this new U2 slice.

## Impact

- Production: one static Planning execution seam and bounded changes to existing
  Agent control flow; existing Goal and evidence models remain unchanged.
- Tests: one L2 short-chain Scenario fixture/formal proof, focused Planning/API
  tests, and an Architecture Guard for the preserved Agent boundary.
- Production delta budget: one added file; one modified file; one public static
  type; no new enum, interface, engine, manager, mutable field, or state owner.
- Ownership, authority, dependency direction, safety semantics, and architecture
  invariants remain unchanged.
- No Planner, Compiler engine, FSM, Graph, route model, generic retry,
  uncertainty framework, new Back action, or non-traversal completion change is
  purchased.
