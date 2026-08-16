# Proposal: Semantic Run Unexpected Navigation Reconciliation

| Attribute | Value |
|-----------|-------|
| Change ID | `semantic-run-unexpected-navigation-reconciliation` |
| Status | Proposed |
| Type | Mechanism generalization |
| Date | 2026-08-15 |
| Buyer | ACTIVE_GOAL_UNEXPECTED_KNOWN_PAGE_TRANSITION |
| Gap | F5_KNOWN_PAGE_RECONCILIATION_SCROLL_COUPLED |

## Why

The existing F5 known-page reconciliation mechanism (`ReconcilePostScrollContinuityFailure`) is coupled to the Scroll path only. When a non-Scroll semantic action (e.g., SetSwitch) results in a fresh Observation that resolves to a different KNOWN page, SemanticRun returns SemanticContradiction instead of reconciling to the new page.

## What

- Extract the generic known-page reconciliation core from `ReconcilePostScrollContinuityFailure`
- Use it for both post-Scroll and post-action continuity mismatches
- Preserve same Goal
- Invalidate stale Container A grounding
- Create/reconcile Container B
- No new architecture

## Non-Goals

- New navigation framework
- New recovery authority
- LLM/VLM
- Broad SemanticRun refactoring
- Treating all SemanticContradictions as page transitions
- Scroll semantics changes
