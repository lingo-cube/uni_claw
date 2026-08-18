# Proposal: Open-World Container Inventory Completeness

| Attribute | Value |
|-----------|-------|
| Change ID | `open-world-container-inventory-completeness` |
| Status | Archived (GRADUATED 2026-08-17 — OPEN_WORLD_CONTAINER_INVENTORY_COMPLETE) |
| Type | Mechanism extension |
| Date | 2026-08-16 |
| Buyer | SETTINGS_FULL_TREE_INVENTORY_COMPLETENESS |
| Gap | RUNTIME_VERIFIED_CONTAINER_INVENTORY_COMPLETENESS_MISSING |
| Target Maturity | OPEN_WORLD_CONTAINER_INVENTORY_COMPLETE |

## Why

The Runtime can collect accepted Container-local viewport observations, perform bounded scroll exploration, and accept caller-provided `BranchInventoryEvidence`. However, it cannot independently prove that the current Container’s complete discoverable child inventory has been enumerated. `Goal.BranchInventoryEvaluator` may declare “complete inventory” from caller knowledge without requiring Runtime-verified exploration exhaustion, unique canonical child normalization, or accounting for unresolved potential children.

## Current Pressure

`RunOpenWorldAsync` currently does NOT own a deterministic viewport exploration/exhaustion loop. It consumes `Goal.BranchInventoryEvaluator` directly and cannot produce Runtime-verified Container inventory completeness evidence without either trusting caller completeness or adding/reusing deterministic exploration mechanics.

The exact pressure is:

`OPEN_WORLD_VIEWPORT_EXHAUSTION_MECHANISM_MISSING`

## What

Add the smallest Agent-owned mechanism that derives and accepts a truthful `CONTAINER_INVENTORY_COMPLETE` claim from deterministic Runtime evidence.

**CALLER_SOURCE_PROVENANCE_CONTRACT slice**: callers (BranchInventoryEvaluator /
test goals) must ground every required branch to an independently discovered
`NavigationSourceOccurrence` via an immutable `NavigationSourceOccurrenceReference`
(`ObservationSequence` + observation-local `OccurrenceLocalIdentity`), carried in
`BranchSourceGroundingEvidence`. The Agent-owned `SourceGroundingValidator` is the
only authority that accepts grounding (run scope, Container scope, accepted
viewport observation, occurrence existence, NAVIGATION_CANDIDATE, resolvability via
`SourceEquivalenceNormalizer`). Callers may only explain where a branch points;
they can never assert equivalence or declare a logical source. Title / count /
destination reconciliation is forbidden.

The conceptual proof is:

- accepted viewport observations
- canonical unique child identity inventory
- verified Container exploration exhaustion
- all potential child candidates accounted for
- no unresolved candidate capable of representing an undiscovered child
- same-Container continuity preserved

→ Agent-owned immutable Container inventory completeness evidence

## Existing Safe Mechanisms Reused

- `Container.ViewportExplorationObservations`
- bounded viewport exploration / exhaustion semantics
- `BranchInventoryEvidence`
- `BranchProgressEvidence`
- `CandidateAuthorizationEvidence`
- open-world parent return
- `OPEN_WORLD_TRAVERSAL_IDENTITY_SAFE`
- existing subtree/full-tree termination semantics GIVEN truthful complete inventories

## Non-Goals

- No global Settings graph
- No page database
- No crawler framework
- No inventory manager subsystem
- No new planner
- No persistent visited-page registry
- No new Container owner
- No new truth authority
- No LLM / VLM
- No claim of full Settings-tree enumeration
- No redesign of existing subtree completion or full-tree termination

## ArchitectureDelta

NONE expected

## AuthorityDelta

NONE


## Graduation note (2026-08-17)

Archived after `PROJECT_LEADER_SINGLE_AGENT_FULL_RUN_CAPSTONE_GRADUATION`:
the change's requirements (Agent-owned completeness evidence, unique child
normalization, deterministic exhaustion, unresolved-fail-closed, truthful leaf,
provenance grounding, authority boundaries) are implemented and proven by
`docs/decisions/open-world-container-inventory-completeness-graduation.md`
(1164/1164 deterministic + the real ONE-Agent/ONE-Run COMPOSE-05 capstone
`STATE=Completed`, `GoalEvidence=True@45`). Claim boundaries and the
SETTINGS_FULL_TREE_ENUMERATION_INTEGRATION next buyer are recorded there.
