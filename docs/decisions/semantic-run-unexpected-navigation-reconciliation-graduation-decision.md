# Semantic Run Unexpected Navigation Reconciliation — Graduation Decision

| Attribute | Value |
|-----------|-------|
| Change | `semantic-run-unexpected-navigation-reconciliation` |
| Decision | **GRADUATED** |
| Maturity | `SEMANTIC_RUN_UNEXPECTED_NAVIGATION_RECONCILED` |
| Record date | 2026-08-16 |
| Review | `PROJECT_LEADER_SEMANTIC_RUN_UNEXPECTED_NAVIGATION_GRADUATION_REVIEW_V2` |

## Buyer

ACTIVE_GOAL_UNEXPECTED_KNOWN_PAGE_TRANSITION

## Gap

F5_KNOWN_PAGE_RECONCILIATION_SCROLL_COUPLED

## Original Mechanism

`ReconcilePostScrollContinuityFailure`

## Shared Mechanism

`ReconcileKnownPageTransition`

## F5 Delegation Result

PASS — the strict Scroll F5 path delegates through `ReconcilePostScrollContinuityFailure` to `ReconcileKnownPageTransition`; Scroll-specific bookkeeping is owned by the Scroll wrapper/caller, not the shared helper.

## Non-Scroll Insertion Point

`src/UniClaw.Runtime/Agent/Agent.SemanticRun.cs` — inside `case SemanticActionResult.Dispatched`, after `journal.PostActionObservation` and `Reconcile.FromObservation`, when `TryVerifyLocalContinuity` returns `false`.

## Known-Page Behavior

A fresh different KNOWN page B is accepted as current semantic reality only when:

1. fresh Observation exists;
2. fresh page is KNOWN;
3. fresh page differs from current page;
4. foreground ownership matches `StartupResult.Ready.Anchor.ApplicationIdentity`;
5. old Container no longer claims the fresh Observation.

After acceptance: Container B is created, bound from fresh Observation, refreshed, and the Agent run-level `observation` and `_belief` are set to B.

## Unknown-Page Fail-Closed Behavior

Unknown / unresolved fresh page returns `SemanticContradiction`; no guessed Container is created.

## Same-Page Contradiction Boundary

Same-page continuity failure remains `SemanticContradiction`; it is not converted into a Container transition.

## Container Transition Semantics

Container ownership remains unchanged: Agent creates the new Container through the existing injected factory; the old Container is no longer current; no new global state owner is introduced.

## Stale Binding / Grounding Rules

After reconciliation, action grounding is derived from the fresh page-B Observation and Container B bindings. Permanent tests verify that a subsequent SetSwitch uses page-B element index/bounds rather than page-A index/bounds.

## Same Goal Preservation

The same `SemanticGoalInput` continues after page transition; no navigation-specific Goal replacement or Goal authority change occurs.

## GoalEvidence Boundary

`ReconcileKnownPageTransition` creates zero GoalEvidence. Later fresh page-B evidence may create GoalEvidence through the normal verification path, and tests assert the GoalEvidence source sequence is the fresh B observation.

## SetSwitch Buyer Result

PASS — production-path `RunSemanticGoalAsync` tests cover non-Scroll SetSwitch → different known page B → same Goal continuation.

## Scroll Regression Result

PASS — strict F5 and deferred checkpoint paths keep Container/Observation/Belief aligned; deferred NAV tests cover A→B checkpoint, stale-grounding rejection, B-sourced GoalEvidence, and unknown-page fail-closed.

## ArchitectureDelta

NONE

## AuthorityDelta

NONE

## Remaining Scenario Limitations

- Unknown navigation remains fail-closed; it is not “solved” as a general navigation capability.
- Foreground-app drift is not absorbed into known-page reconciliation; it remains independent fail-closed territory.
- Live Android E2E is not part of this deterministic maturity.
- All navigation recovery, trap recovery, and general reconciliation beyond different KNOWN pages remain outside this maturity.
