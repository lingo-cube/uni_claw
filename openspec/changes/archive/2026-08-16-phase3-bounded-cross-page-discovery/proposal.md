## Why

The Runtime can authorize one newly observed candidate, retain a bounded approved sibling inventory, and preserve evidence-backed cross-Container completion. It cannot repeatedly establish the complete required branch inventory for each newly entered semantic Container unless the concrete targets were already listed in the initial immutable Plan. Legacy Settings evidence shows that this gap can skip branches, cross a semantic depth bound, revisit already-accounted work, and still claim completion. SC-P3-CAND-008 purchases the minimum missing inventory evidence needed for bounded fresh-evidence route continuation without importing the legacy traversal machinery or creating a generic planner.

## What Changes

- Add the approved SC-P3-CAND-008 contract for deriving a complete required branch inventory from bounded accepted evidence in the active semantic Container.
- Add one immutable reason-bearing inventory evidence value that distinguishes a proven non-empty inventory, a proven empty leaf, and unresolved inventory completeness.
- Add one optional deterministic Goal criterion that consumes bounded accepted same-Container evidence plus evidence-backed semantic depth.
- Require Agent to nominate at most one unresolved required branch only after independent SC-P3-CAND-006 authorization.
- Permit bounded route continuation across newly reconciled Containers without encoding the complete concrete route in the initial Plan.
- Reuse SC-P3-CAND-004 progress ownership and SC-P3-CAND-007 retained evidence rather than adding route state.
- Preserve GoalEvidence as the only final completion authority.

## Capabilities

### New Capabilities

- `bounded-cross-page-discovery`: Defines complete required-branch inventory evidence, bounded fresh-evidence route continuation, independent authorization, depth separation, unresolved handling, progress preservation, and deterministic replay for SC-P3-CAND-008.

### Modified Capabilities

None. SC-P3-CAND-004 continues to own cross-Container progress; SC-P3-CAND-006 continues to own candidate authorization; SC-P3-CAND-007 continues to own same-Container evidence retention and exploration exhaustion.

## Impact

- Expected production surface: one immutable two-field `BranchInventoryEvidence` value, one optional immutable Goal criterion field, and existing Agent control flow/branch-progress state.
- Expected verification surface: deterministic multi-Container fixtures proving P → A → C discovery without a pre-encoded route, positive empty-leaf evidence, unresolved inventory, authorization denial, depth-bound separation, progress preservation, and replay.
- Approved production delta budget: model types +1; fields +3 total; enums +0; interfaces +0; components +0; mutable-state fields +0; mutable-state owners +0.
- Ownership delta: none. Agent remains the sole cross-Container progress and route-decision owner.
- Authority delta: none. Agent remains the sole Goal-scoped inventory, next-branch, GoalEvidence, and final RunState authority.
- No generic dynamic planner/re-plan, graph/tree/stack, persistent route model, manager, workflow engine, FSM, new Back action, Fingerprint, Confidence, Vision/VLM semantics, generic retry/uncertainty, new Recovery behavior, Capstone implementation, Harness change, S1/S2/S3 work, or Runtime refactor is purchased.
