# Semantic Run Unexpected Navigation Reconciliation — Review Record (REPAIR_REQUIRED)

| Attribute | Value |
|-----------|-------|
| Change | `semantic-run-unexpected-navigation-reconciliation` |
| Decision | **REPAIR_REQUIRED** |
| Canonical status | `PRODUCTION_IMPLEMENTED_REPAIR_REQUIRED` |
| State | ACTIVE — NOT_ARCHIVED, NOT_GRADUATED |
| Record date | 2026-08-16 (canonical repository governance pass) |
| Review posture | No behavior repair performed in this gate; production code untouched |

## Context

A production implementation of generic known-page reconciliation exists at HEAD
(`src/UniClaw.Runtime/Agent/Agent.SemanticRun.cs`:
`ReconcileKnownPageTransition`, with `ReconcilePostScrollContinuityFailure`
delegating to it — added in commit `088421a`). The independent graduation
review of the change proved real defects; graduation is therefore denied and
the change remains ACTIVE with status REPAIR_REQUIRED.

## Defects proven by the independent review

At minimum (recorded findings; structural claims verified against the current
production source):

1. **Shared helper contains Scroll-specific state** — the shared
   `ReconcileKnownPageTransition` mutates `_postScrollContinuityUnverified`
   and `_deferredScrollCount` (Scroll/DEFERRED_BOUNDED policy state) for the
   non-Scroll post-action path, coupling the generic mechanism to scroll
   semantics.
2. **Fresh page-B observation is not adopted** — in the different-known-page
   case the helper replaces `_activeContainer` and calls
   `RefreshContainerEvidence(_activeContainer, freshObs)`, but the fresh
   observation/WorldBelief is not adopted into the Agent's running
   observation state for the reconciled page.
3. **Agent `_belief` remains page A** — `_belief` is not updated to the fresh
   page-B belief; the Agent's WorldBelief keeps pointing at the previous page.
4. **Container becomes B while Agent belief remains A** — `_activeContainer`
   is replaced with page B while Agent `_belief` still reflects page A,
   leaving the Agent/Container belief pair inconsistent.
5. **Stale A element grounding can survive** — pre-transition element
   indices/bounds/grounding from page A are not invalidated against the
   fresh page-B observation in all paths; stale grounding can survive into
   subsequent semantic actions.
6. **GoalEvidence may use stale A observation sequence** — evidence minting
   paths can reference the stale page-A observation sequence instead of the
   fresh page-B verification.
7. **Reconciliation entry condition is too broad** — the post-action path
   enters reconciliation on any continuity mismatch, not only on genuine
   known-page transitions, widening the surface for incorrect adoption.
8. **Foreground ownership boundary is missing** — the helper does not verify
   the foreground application identity for the reconciled page before
   adopting it (the existing continuity checks are not applied on the
   adoption path).
9. **F5/deferred paths inherit the stale-world defect** —
   `ReconcilePostScrollContinuityFailure` (F5) delegates to the same shared
   helper, so the stale-world defect propagates to the scroll/deferred paths.

## Canonical status

- `semantic-run-unexpected-navigation-reconciliation` = **ACTIVE**
- = **PRODUCTION_IMPLEMENTED_REPAIR_REQUIRED**
- = **NOT_ARCHIVED**
- = **NOT_GRADUATED**

Behavior repair of `ReconcileKnownPageTransition`, `Agent.SemanticRun` control
flow, F5, DEFERRED_BOUNDED, and GoalEvidence production belongs to the NEXT
gate (`PROJECT_LEADER_REPAIR_SEMANTIC_RUN_UNEXPECTED_NAVIGATION_RECONCILIATION`).
