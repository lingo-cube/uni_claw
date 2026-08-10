# Planning Layer

## 1. Responsibility

Planning preserves caller-authoritative execution hypotheses and semantic
execution representations. It does not own external-world truth, runtime work
inventory, target selection, action dispatch, progress, or Goal completion.

The two supported representation modes remain distinct:

```text
CLOSED_WORLD_CONCRETE
→ existing Plan

OPEN_WORLD_TYPE_LEVEL
→ TypeLevelTraversalSpecification
```

`Plan` is a hypothesis, not reality. A `TypeLevelTraversalSpecification` is a
scope/category/depth/safety/completion/entry boundary, not a concrete future
route or work inventory.

## 2. Intent Semantic Envelope

`IntentSemanticEnvelope` projects already-authoritative structured caller input
into either:

- `Resolved`, containing a Goal and exactly one truthful execution
  representation; or
- `Insufficient`, containing no executable Goal or representation.

Projection does not parse natural language, invent desired state or authority,
observe the world, or generate a route.

## 3. U2 Open-World Execution Seam

`IntentSemanticEnvelopeExecution.RunOpenWorldAsync` is the sole bounded U2
execution seam. It accepts a resolved open-world envelope, validates that the
supplied specification is exhaustive, navigation-only, and has matching scope
and entry boundaries, then forwards only the already-authoritative primitive
and Model values to Agent.

The seam does not:

- discover inventory;
- select or ground targets;
- construct a Plan or route;
- observe or mutate the world;
- evaluate progress or decide completion.

Agent remains independent of the Planning namespace. Existing
`Agent.RunAsync(Goal, Plan, ...)` remains the closed-world execution boundary.

## 4. Ownership and Dependency Boundary

- Planning owns no mutable Runtime state.
- Agent owns semantic inventory/progress interpretation and final RunState.
- Container owns page-local state and accepted local evidence.
- Traversal owns local Select → Check → Execute → fresh Observe → Verify.
- Environment owns external Observation and dispatch outcomes only.

No Planner engine, Compiler engine, FSM, Graph, route registry, or new state
owner is introduced by the U2 seam.
