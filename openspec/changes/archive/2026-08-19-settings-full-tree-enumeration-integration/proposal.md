# Proposal: Settings Full-Tree Enumeration Integration

| Attribute | Value |
|-----------|-------|
| Change ID | `settings-full-tree-enumeration-integration` |
| Status | Proposed |
| Type | Integration change (new buyer) |
| Date | 2026-08-18 |
| Buyer | `SETTINGS_FULL_TREE_ENUMERATION_INTEGRATION` |
| Gap | `REAL_SETTINGS_FULL_TREE_COMPLETION_EVIDENCE_MISSING` |
| Target Maturity | `SETTINGS_FULL_TREE_ENUMERATION_INTEGRATED` |
| Prerequisites | `OPEN_WORLD_TRAVERSAL_IDENTITY_SAFE` = GRADUATED; `OPEN_WORLD_CONTAINER_INVENTORY_COMPLETE` = GRADUATED |

## Why

The Runtime has graduated per-Container open-world inventory completeness and
the ONE-Agent/ONE-Run COMPOSE-05 capstone, but no real Android Settings
full-tree traversal has been proven: Root → recursive Containers → per-Container
complete inventory → child traversal → verified parent return → sibling
continuation → subtree completion → full-tree completion evidence. This change
INTEGRATES the graduated mechanisms onto the real Settings root and defines the
full-tree completion contract — it does not re-invent any of them.

## Current Pressure

The exact pressure is:

`REAL_SETTINGS_RECURSIVE_CONTAINER_COMPLETION_EVIDENCE_MISSING`

COMPOSE-05 is a fixed-depth fixture scenario; it is NOT full-tree evidence.
Real Settings root presents: local controls, navigation candidates, Unknowns,
scrollability, external boundaries, and potential destination aliasing — none of
which has been exercised as a real recursive tree.

## What

- Define strictly: `ContainerComplete` ≠ `SubtreeComplete` ≠ `FullTreeComplete`.
- Define the recursion contract (Enter C → prove inventory(C) → for each
  authorized child source S: fresh reach, dispatch, settle child C', recurse,
  prove SubtreeComplete(C'), verified return → SubtreeComplete(C)).
- Define the real Settings root entry (application identity, semantic root
  identity, initial structured sources, local controls, navigation candidates,
  Unknowns, scrollability, foreground ownership).
- Define the Agent-owned run-local completion ledger (bookkeeping only; NOT a
  world-truth authority; no global persistent graph). The ledger records
  ContainerIdentity, ContainerCompletenessEvidence, RequiredChildren,
  CompletedChildren, SubtreeComplete, AND `VerifiedBoundaryDispositions` — each
  EXTERNAL_BOUNDARY source carries its source/provenance reference, verified
  external-boundary evidence, and disposition, so a boundary obligation is
  explicitly discharged and never silently dropped (bookkeeping only — never a
  graph edge, world truth, recursive child completion, or authorization).
- Define the first real integration scenario `SETTINGS-TREE-01` (≥3 semantic
  depths, real emulator) proving recursion genuinely occurs.
- Classify real failures into the eight pressure classes; fix one first real
  production failure per run.

## Non-Goals / NOT CLAIMED (initially)

- arbitrary Android app traversal
- universal Settings completeness
- alias merging across semantic destinations
- persistent navigation graph
- LLM/VLM navigation
- generic popup recovery
- dynamic Settings mutation recovery
- relaxing run-local identity safety
- treating Root inventory complete as full-tree complete

## Reused Prerequisites (not re-purchased)

`RunOpenWorldAsync`; run-local identity safety; Container inventory completeness;
explicit source provenance; positive exhaustion; frozen discovery epoch;
post-completeness consistency; `ScrollBackward` bounded revisit; fresh structured
dispatch; child-transition settle; contextual parent-return control; GoalEvidence
authority.

## ArchitectureDelta

NONE expected (integration/design only; any structure added by approved slices
is recorded, never claimed NONE if added).

## AuthorityDelta

NONE — the completion ledger is bookkeeping, not a new truth authority.
