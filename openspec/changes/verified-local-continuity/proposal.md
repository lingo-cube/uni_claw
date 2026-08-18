# Proposal: verified-local-continuity

## Buyer

SCROLLED_CONTAINER_IDENTITY_DRIFT (proven by buyer gate): after a same-Container
action (ScrollForward / SetSwitch) on a scrollable page (Developer options), the
ABSOLUTE page resolver returns null because the page title scrolled out of view.
The Runtime then raised a FALSE SemanticContradiction ("semantic page unresolved")
even though the world never left the Container. Reproducible 5/6 ASU state-change
runs; 6/24 of the real-device distribution corpus.

## Gap (verified repository truth)

- `CreateMultiPageResolver` (absolute) returns null when no unique page match —
  including the "title offscreen on a scrolled same page" case.
- Post-action / post-scroll continuity (`TryVerifyLocalContinuity` /
  `TryVerifyViewportContinuity`) requires the absolute resolver to return the page;
  null → `ReconcileKnownPageTransition` CASE C → SemanticContradiction.
- Developer options page identity anchors are title-only; scrolled-to-bottom frames
  contain page content ("Enable demo mode"/"Show demo mode") but not the title.

## What this change does (APPLY)

Bounded VerifiedLocalContinuity fallback: when the absolute resolver returns null
after a same-Container action, the Agent preserves the previous semantic page ONLY
when fresh continuity evidence independently verifies same-Container continuity
(previous verified identity + compatible foreground + same-Container action scope
+ fresh structural evidence + no other-page match + no navigation/contradictory
evidence). Source = VERIFIED_LOCAL_CONTINUITY. Never resolver==null → previousPage.

## Non-goals

- Perception redesign; binding/state changes; L1/L2; new public taxonomy;
  time-based inference; generalized temporal reasoning.

## Falsifiers

| # | Falsifier | Fails if |
|---|---|---|
| F1 | resolver-null→previousPage | page preserved without fresh continuity evidence |
| F2 | stale carry-forward | previous identity reused as current truth without fresh evidence |
| F3 | other-page override | continuity overrides a positive match to another page |
| F4 | foreground change accepted | continuity accepts a foreground mismatch |
| F5 | insufficient evidence accepted | continuity accepts an empty/bare-text observation |
| F6 | navigation suppressed | a genuine page transition is treated as same-container |
| F7 | L1/L2 coupling | the repair touches Assistance/planning |
| F8 | binding/state freeze violated | binding/state not freshly resolved from the new Observation |
