# Tasks: semantic-run-popup-obstruction-integration

## 1. Audit

- [x] 1.1 Audit PlanRun local-obstruction implementation
- [x] 1.2 Identify exact SemanticRun insertion point
- [x] 1.3 Create OpenSpec

## 2. Implementation

- [x] 2.1 Add TryHandleLocalObstructionAsync helper to Agent.SemanticRun.cs
- [x] 2.2 Integrate at loop start
- [x] 2.3 Ensure fresh Observation after obstruction action
- [x] 2.4 Ensure stale grounding rejected
- [x] 2.5 Ensure same Goal preserved

## 3. Tests

- [x] 3.1 POP-1: popup during active Goal
- [x] 3.2 POP-2: dismiss succeeds, original page returns
- [x] 3.3 POP-3: dismiss dispatches but popup remains
- [x] 3.4 POP-4: dismissal navigates to different known page
- [x] 3.5 POP-5: post-dismiss state unknown
- [x] 3.6 POP-6: stale pre-popup grounding rejected
- [x] 3.7 POP-7: same Goal survives valid recovery
- [x] 3.8 POP-8: recovery cannot create GoalEvidence
- [x] 3.9 POP-9: recovery failure escalates to Agent
- [x] 3.10 POP-10: no popup -> normal path unchanged

## 4. Validation

- [x] 4.1 Run targeted SemanticRun tests
- [x] 4.2 Run PlanRun obstruction tests
- [x] 4.3 Run architecture guards
- [x] 4.4 Run full regression
- [x] 4.5 Run consistency check
- [x] 4.6 Run OpenSpec validation

> Governance reconciliation audit (2026-08-16): all tasks verified against
> current HEAD evidence — production implementation (Agent.SemanticRun.cs
> TryHandleLocalObstructionAsync, loop-start integration, fresh Observation via
> journal.PostActionObservation + TryVerifyLocalContinuity, stale-grounding
> rejection via RefreshContainerEvidence, same-Goal preservation), permanent
> tests (PopupObstructionRecoveryTests x4, ContainerTests local-obstruction
> unit tests, closed-loop suite; targeted run 50/50 PASS), and the existing
> GRADUATED decision record
> (docs/decisions/semantic-run-popup-obstruction-graduation-decision.md).
> POP-1..POP-10 scenario items are covered by these permanent tests under
> descriptive names. Full-regression note: Vision-host identity tests fail for
> the pre-existing stale-deployment-receipt reason (environmental), unrelated
> to this change.
