# Proposal: Settings Navigation Candidate Evidence

| Attribute | Value |
|-----------|-------|
| Change ID | `settings-navigation-candidate-evidence` |
| Status | Proposed |
| Type | Evidence prerequisite |
| Date | 2026-08-16 |
| Buyer | SETTINGS_FULL_TREE_INVENTORY_COMPLETENESS |
| Parent Change | `open-world-container-inventory-completeness` |
| Pressure | PERCEPTION_EVIDENCE_PRESSURE |
| Primary Strategy | STRUCTURED_UI_REQUIRED |
| Target Maturity | SETTINGS_NAVIGATION_CANDIDATE_EVIDENCE_BASELINE |
| Scope | SETTINGS_SCOPED |

## Why

Current `ObservedElement` evidence (`Text`, `SwitchState`, `Index`, `Bounds`, `PerceptionType`) cannot deterministically distinguish Settings navigation rows from local controls such as switches, checkboxes, buttons, and dialog actions. The Runtime therefore cannot independently enumerate potential child navigation sources without using `BranchInventoryEvaluator` as a discovery oracle.

This prerequisite adds enough structured Android UI evidence to support a Settings-scoped, deterministic three-way classification:

- `NAVIGATION_CANDIDATE`
- `LOCAL_CONTROL`
- `UNKNOWN`

Destination semantic page identity is NOT required for discovery.

## What

- Acquire or expose a narrow structured Android UI evidence source.
- Correlate structured evidence to existing `ObservedElement` instances deterministically.
- Add a Runtime semantic value, `InteractionAffordanceEvidence`, that classifies accepted elements as `NAVIGATION_CANDIDATE`, `LOCAL_CONTROL`, or `UNKNOWN`.
- Keep `UNKNOWN` as a first-class truthful outcome.
- Preserve existing Agent, Container, Traversal, and GoalEvidence authority.

## Non-Goals

- No Settings graph
- No page graph
- No route table
- No destination registry
- No crawler state
- No new planner
- No new mutable owner
- No new semantic authority
- No LLM / VLM
- No generic-app navigation classification
- No full-tree completeness claim
- No implementation of parent inventory completeness in this change

## ArchitectureDelta

NONE expected

## AuthorityDelta

NONE
