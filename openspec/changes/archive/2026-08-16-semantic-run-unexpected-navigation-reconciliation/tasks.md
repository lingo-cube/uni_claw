# Tasks: semantic-run-unexpected-navigation-reconciliation

> Governance audit (2026-08-16): status = GRADUATED.
> All tasks verified against current HEAD production code and permanent tests.
> Shared helper is de-scrolled; NAV/deferred coverage is permanent; full
> targeted regression is green; Vision/Python failures are environmental.

## 1. Audit

- [x] 1.1 Audit exact F5 logic
- [x] 1.2 Create OpenSpec

## 2. Implementation

- [x] 2.1 Extract ReconcileKnownPageTransition shared method
- [x] 2.2 Update ReconcilePostScrollContinuityFailure to use it
- [x] 2.3 Update post-action continuity mismatch to use it
- [x] 2.4 Ensure Scroll-specific state stays outside shared method

## 3. Tests

- [x] 3.1 NAV-1: expected navigation unchanged
- [x] 3.2 NAV-2: non-Scroll action → different known page B
- [x] 3.3 NAV-3: page B supports same Goal
- [x] 3.4 NAV-4: page B known but Goal cannot bind
- [x] 3.5 NAV-5: post-action page UNKNOWN
- [x] 3.6 NAV-6: old page A Binding cannot authorize
- [x] 3.7 NAV-7: old ElementIndex/Bounds cannot be reused
- [x] 3.8 NAV-8: reconciliation creates no GoalEvidence
- [x] 3.9 NAV-9: GoalEvidence only from fresh verification
- [x] 3.10 NAV-10: no page transition unchanged
- [x] 3.11 NAV-11: SetSwitch → known page B appears
- [x] 3.12 NAV-12: same-page contradiction still fails

## 4. Validation

- [x] 4.1 Run targeted unexpected-navigation tests
- [x] 4.2 Run F5/Scroll regression
- [x] 4.3 Run architecture guards
- [x] 4.4 Run full regression
- [x] 4.5 Run consistency check
- [x] 4.6 Run OpenSpec validation
