# Tasks: RuntimeAgent Pre-Terminal Cycle Contract

## 1. Passive Contract Models

- [x] 1.1 Add immutable `PreTerminalReasoningSnapshot` with Run, cycle, accepted-observation, belief revision/digest, DFS progress revision, trace correlation, and accepted reasoning revision reference.
- [x] 1.2 Add immutable `PreTerminalContinuationProposal` with the closed passive dispositions `ContinuationSupported`, `ContinuationSupportedAfterRevision`, and `ContinuationNotSupported`.
- [x] 1.3 Prevent snapshot/proposal models from containing DeviceAction, target, selector, route, plan step, retry, recovery, FSM, RunState, GoalEvidence, completion, or execution callbacks.

## 2. Transactional Reasoning Revision

- [x] 2.1 Add accepted/proposed reasoning revision identities and parent linkage for N to N+1 evaluation.
- [x] 2.2 Evaluate snapshots without mutating accepted revision N or accepted reasoning history.
- [x] 2.3 Add Agent-authorized compare-and-accept so only a fresh proposal can be atomically published as N+1 in RuntimeAgent-owned reasoning history.
- [x] 2.4 Discard rejected proposed revisions with no accepted history mutation.
- [x] 2.5 Keep ExecutionHypothesis, RuntimeDecision, and HypothesisAdaptation internal to RuntimeAgent reasoning state.

## 3. Agent-Owned Validation Seam

- [x] 3.1 Add an optional Agent checkpoint seam only after fresh evidence acceptance, WorldBelief update, DFS progress update, Running-state confirmation, and before next-action authorization.
- [x] 3.2 Keep cycle sequence assignment, proposal validation, acceptance/rejection, and same-Run continuation decisions under Agent ownership.
- [x] 3.3 Ensure one accepted evidence revision creates at most one checkpoint and one accepted proposal.
- [x] 3.4 Ensure action settling, polling, retry, low-level recovery, unaccepted evidence, and repeated evidence do not create checkpoints.

## 4. Freshness and Correlation Rejection

- [x] 4.1 Reject RunId, CycleSequence, AcceptedObservationSequence, BeliefRevision, BeliefDigest, DfsProgressRevision, trace correlation, and reasoning-parent mismatches.
- [x] 4.2 Reject duplicate cycles, unknown proposal types, terminal results, and authority-bearing proposal shapes.
- [x] 4.3 Close rejected cycles so late results cannot be accepted.

## 5. Timeout and Cancellation

- [x] 5.1 Add bounded evaluation timeout and cancellation handling without transferring lifecycle or recovery authority to RuntimeAgent.
- [x] 5.2 Reject timeout/cancellation results and any later completion for the closed cycle with zero action and no accepted reasoning mutation.

## 6. Authority Guards

- [x] 6.1 Add guards proving snapshot/proposal contracts cannot reference DeviceAction, Traversal, FSM, RunState, GoalEvidence, completion, recovery commands, another Run, or Multi-Run orchestration.
- [x] 6.2 Add guards proving RuntimeDecision and HypothesisAdaptation are not Agent-facing continuation commands.
- [x] 6.3 Prove Agent remains the sole owner of action authorization, recovery decision, same-Run continuation, GoalEvidence, RunState, and terminal outcome.

## 7. Deterministic Contract Tests

- [x] 7.1 Test a fresh correlated proposal N+1 can be compare-and-accepted over N, after which Agent independently continues the same Run.
- [x] 7.2 Test rejection preserves N and accepted reasoning history and authorizes zero action.
- [x] 7.3 Test stale observation, belief, DFS, trace, cycle, Run, and reasoning-parent mismatches fail closed.
- [x] 7.4 Test duplicate, timeout, cancellation, late, terminal, and unknown proposals fail closed.
- [x] 7.5 Test one accepted evidence revision creates at most one checkpoint.
- [x] 7.6 Test no checkpoint occurs during settle, polling, retry, low-level recovery, unaccepted evidence, repeated evidence, or after terminal state.

## 8. Regression Verification

- [x] 8.1 Verify the disabled/absent seam produces zero behavioral delta in existing OpenWorld DFS execution.
- [x] 8.2 Run RuntimeAgent Phase 1-4 tests and confirm their contracts remain unchanged.
- [ ] 8.3 Run architecture guards, deterministic Runtime scenarios, consistency checks, and the full Runtime solution test suite.
- [x] 8.4 Confirm Agent lifecycle, DFS engine, FSM, Traversal, GoalEvidence, Strategy, Semantic capability, and scenario knowledge remain outside this implementation scope.

## 9. OpenSpec Validation

- [x] 9.1 Run `openspec validate runtime-agent-pre-terminal-cycle-contract --type change --strict --no-interactive`.
- [x] 9.2 Record implementation evidence only after production work is separately authorized and completed.

## Design Docs

- `proposal.md`
- `design.md`
- `specs/runtime-agent-pre-terminal-cycle/spec.md`
- `tasks.md`

## 10. Knowledge Sync

- [x] 10.1 Synchronize the Planning layer ownership documentation with the implemented optional pre-terminal checkpoint seam; no authority or lifecycle projection changes.
