# Tasks: open-world-traversal-identity-safety

## 1. Audit

- [x] 1.1 Audit existing open-world traversal identity and cycle behavior
- [x] 1.2 Define minimal Agent-owned identity evidence boundary

## 2. Implementation

- [x] 2.1 Add Agent-owned run-local ancestry identity evidence for open-world traversal
- [x] 2.2 Add Agent-owned run-local visited identity evidence for open-world traversal
- [x] 2.3 Reject child entry when child identity is in current ancestry
- [x] 2.4 Reject duplicate semantic page identity across branches by default; allow explicit merge rule only if separately approved
- [x] 2.5 Preserve parent-return and branch-completion evidence behavior

## 3. Tests

- [x] 3.1 OWI-1: A → B → A cycle rejected
- [x] 3.2 OWI-2: duplicate semantic page identity across branches rejected
- [x] 3.3 OWI-3: unique page traversal completes
- [x] 3.4 OWI-4: parent return after rejected cycle remains valid
- [x] 3.5 OWI-5: Goal completion evidence unaffected
- [x] 3.6 Closed-world PlanRun regression unchanged

## 4. Validation

- [x] 4.1 Run targeted open-world traversal identity tests
- [x] 4.2 Run U2 / bounded cross-page discovery regression
- [x] 4.3 Run architecture guards
- [x] 4.4 Run full regression
- [x] 4.5 Run consistency check
- [x] 4.6 Run OpenSpec validation
