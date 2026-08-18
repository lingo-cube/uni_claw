# Design: Open-World Container Inventory Completeness

## Existing Mechanism

- Container owns accepted `ViewportExplorationObservations`.
- Agent consumes `BranchInventoryEvidence` from `Goal.BranchInventoryEvaluator`.
- `TryAcceptBranchInventory` validates that inventory source sequences are accepted by the current Container.
- `ViewportExplorationEvidence` can represent continue / exhausted / unresolved.
- Identity safety prevents duplicate page traversal and ancestry cycles.
- BranchProgress and parent return provide subtree completion once inventories are truthful.

## Gap

The current `BranchInventoryEvidence` may be supplied from caller knowledge. The Runtime does not require proof that:
1. exploration reached deterministic exhaustion;
2. all accepted viewport observations were considered;
3. all discovered children were normalized to unique canonical semantic identities;
4. no unresolved potential child remains.

An additional proven gap is that `RunOpenWorldAsync` itself does not currently own a deterministic viewport exploration/exhaustion loop. Existing exhaustion semantics live in `RunBoundedCrossPageDiscovery` and PlanRun viewport paths, not in `RunOpenWorldAsync`.

## Required Exhaustion Acquisition

Before completeness can be produced, `RunOpenWorldAsync` must be able to acquire deterministic Container-local viewport exhaustion evidence.

The preferred repair is to reuse/extract the existing generic semantics:

- `EvaluateViewportExploration` — Agent interprets accepted viewport evidence.
- `Container.TryVerifyViewportContinuity` — Container accepts fresh same-page viewport evidence.
- Traversal `ScrollForward` — executes the authorized exploration action.
- `ViewportExplorationEvidence` — represents continue / exhausted / unresolved.

The extraction must preserve ONE semantic definition of viewport progression and exhaustion. It must not create a second Scroll engine or parallel exhaustion definition.

## Minimal Delta

Introduce one narrow, immutable, Agent-owned evidence value:

`ContainerInventoryCompletenessEvidence`

Conceptual minimum fields:

- `ContainerIdentity`
- `SourceObservationSequences`
- `UniqueChildSemanticPageIdentities`
- `ExplorationExhausted`
- `UnresolvedCandidateCount` / unresolved disposition

Every field must have a concrete falsifier. This is a sibling to `BranchInventoryEvidence`, not an overload of it.

## Revised Slice Plan

1. Reuse/extract deterministic viewport exploration + exhaustion semantics.
2. Integrate bounded exploration into `RunOpenWorldAsync`.
3. Prove EXH-1..EXH-10.
4. Implement `ContainerInventoryCompletenessEvidence`.
5. Unique child normalization.
6. Caller inventory validation.
7. Leaf proof.
8. INV-1..INV-16.
9. Full OpenWorld regression.

## Ownership

- Container continues to own accepted local observation evidence.
- Agent owns semantic acceptance of inventory completeness evidence.
- Traversal continues to execute authorized exploration actions.
- CandidateAuthorization continues to govern traversal permission.
- GoalEvidence remains the only completion authority.

## Preserved Boundaries

- DISCOVERED != AUTHORIZED
- AUTHORIZED != VISITED
- VISITED != COMPLETED
- Depth cutoff != leaf
- Safety cutoff != leaf
- Unresolved inventory != leaf
- Viewport exhausted != Container inventory complete unless all completeness conditions hold
