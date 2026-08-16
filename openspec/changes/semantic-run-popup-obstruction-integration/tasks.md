# Tasks: semantic-run-popup-obstruction-integration

## 1. Audit

- [ ] 1.1 Audit PlanRun local-obstruction implementation
- [ ] 1.2 Identify exact SemanticRun insertion point
- [ ] 1.3 Create OpenSpec

## 2. Implementation

- [ ] 2.1 Add TryHandleLocalObstructionAsync helper to Agent.SemanticRun.cs
- [ ] 2.2 Integrate at loop start
- [ ] 2.3 Ensure fresh Observation after obstruction action
- [ ] 2.4 Ensure stale grounding rejected
- [ ] 2.5 Ensure same Goal preserved

## 3. Tests

- [ ] 3.1 POP-1: popup during active Goal
- [ ] 3.2 POP-2: dismiss succeeds, original page returns
- [ ] 3.3 POP-3: dismiss dispatches but popup remains
- [ ] 3.4 POP-4: dismissal navigates to different known page
- [ ] 3.5 POP-5: post-dismiss state unknown
- [ ] 3.6 POP-6: stale pre-popup grounding rejected
- [ ] 3.7 POP-7: same Goal survives valid recovery
- [ ] 3.8 POP-8: recovery cannot create GoalEvidence
- [ ] 3.9 POP-9: recovery failure escalates to Agent
- [ ] 3.10 POP-10: no popup -> normal path unchanged

## 4. Validation

- [ ] 4.1 Run targeted SemanticRun tests
- [ ] 4.2 Run PlanRun obstruction tests
- [ ] 4.3 Run architecture guards
- [ ] 4.4 Run full regression
- [ ] 4.5 Run consistency check
- [ ] 4.6 Run OpenSpec validation
