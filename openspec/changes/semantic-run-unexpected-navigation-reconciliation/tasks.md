# Tasks: semantic-run-unexpected-navigation-reconciliation

## 1. Audit

- [ ] 1.1 Audit exact F5 logic
- [ ] 1.2 Create OpenSpec

## 2. Implementation

- [ ] 2.1 Extract ReconcileKnownPageTransition shared method
- [ ] 2.2 Update ReconcilePostScrollContinuityFailure to use it
- [ ] 2.3 Update post-action continuity mismatch to use it
- [ ] 2.4 Ensure Scroll-specific state stays outside shared method

## 3. Tests

- [ ] 3.1 NAV-1: expected navigation unchanged
- [ ] 3.2 NAV-2: non-Scroll action → different known page B
- [ ] 3.3 NAV-3: page B supports same Goal
- [ ] 3.4 NAV-4: page B known but Goal cannot bind
- [ ] 3.5 NAV-5: post-action page UNKNOWN
- [ ] 3.6 NAV-6: old page A Binding cannot authorize
- [ ] 3.7 NAV-7: old ElementIndex/Bounds cannot be reused
- [ ] 3.8 NAV-8: reconciliation creates no GoalEvidence
- [ ] 3.9 NAV-9: GoalEvidence only from fresh verification
- [ ] 3.10 NAV-10: no page transition unchanged
- [ ] 3.11 NAV-11: SetSwitch → known page B appears
- [ ] 3.12 NAV-12: same-page contradiction still fails

## 4. Validation

- [ ] 4.1 Run targeted unexpected-navigation tests
- [ ] 4.2 Run F5/Scroll regression
- [ ] 4.3 Run architecture guards
- [ ] 4.4 Run full regression
- [ ] 4.5 Run consistency check
- [ ] 4.6 Run OpenSpec validation
