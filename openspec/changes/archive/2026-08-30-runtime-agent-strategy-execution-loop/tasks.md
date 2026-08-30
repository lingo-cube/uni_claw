## 1. Apply Gate and Dependency Verification

- [x] 1.1 Obtain explicit human approval for production apply; record Luna as implementation owner and Sol as independent architecture reviewer/final verifier.
- [x] 1.2 Verify `uniagent-runtimeagent-strategy-contract` and `runtime-agent-pre-terminal-cycle-contract` implementation and architecture guards before changing production code.
  - Evidence (2026-08-24): previously blocking scenario-knowledge guard finding no longer reproduces; full deterministic suite green including `StrategyContractTests`, `StrategyRunWireTests`, `StrategyContractAuthorityTests`, `StrategyExecutionLoopContractTests`, and architecture guards; zero scenario literals remain under `src/UniClaw.Runtime`.
- [x] 1.3 Freeze `PreTerminalStrategy` as the single reasoning mode for an accepted Strategy Run and confirm no post-run reconciliation will process the same evidence history.

## 2. Immutable Strategy Evidence Contract

- [x] 2.1 Add immutable `StrategyExecutionEvidenceView` with contract version, Run and intent references, accepted-observation and belief correlations, structural-progress revision/facts, coverage and contradiction evidence references, trace correlation, and evidence-view digest.
- [x] 2.2 Add a closed structural-progress fact vocabulary whose entries carry only kind, revision, and opaque evidence reference.
- [x] 2.3 Reject evidence views containing mutable World/DFS objects, free-form scenario content, actions, selectors, routes, actionable targets, branch ordering, completion flags, GoalEvidence mutation, FSM commands, callbacks, or executable delegates.
- [x] 2.4 Attach exactly one evidence view to eligible strategy-mode pre-terminal snapshots without changing the existing passive proposal vocabulary.

## 3. Run-Scoped Reasoning Session

- [x] 3.1 Add `StrategyExecutionReasoningSession` bound once to Agent-assigned RunId, accepted runtime execution intent reference, immutable adaptation boundary, initial hypothesis H0, and initial accepted revision N0.
- [x] 3.2 Keep accepted reasoning history immutable and stage RuntimeDecision, optional HypothesisAdaptation, hypothesis N+1, and evidence correlation outside accepted history until commit.
- [x] 3.3 Implement passive mapping from internal support, permitted revision, and unsupported bounded continuation to the existing three `PreTerminalContinuationProposal` dispositions.
- [x] 3.4 Seal the session on Agent-owned terminal finalization; reject later evaluation or commit and expose at most a read-only receipt of accepted history.

## 4. Agent-Owned Checkpoint Integration

- [x] 4.1 Create and bind one reasoning session after Strategy admission and Agent RunId assignment but before the first eligible checkpoint, without granting the session Run-start or lifecycle access.
- [x] 4.2 Project the evidence view only after Agent accepts fresh Observation evidence and completes belief and structural-progress updates, and before any next action is authorized.
- [x] 4.3 Extend Agent validation to compare snapshot and evidence-view Run, intent, observation, belief, structural-progress, trace, digest, session, and reasoning-parent correlations.
- [x] 4.4 Commit proposed reasoning revision N+1 only through existing Agent-authorized compare-and-accept; discard rejected, stale, duplicate, timed-out, cancelled, terminal, or malformed proposals with zero action and no accepted history mutation.
- [x] 4.5 Ensure Agent independently chooses every existing continuation, recovery, completion, or fail-closed path after proposal validation.

## 5. Single Reasoning Mode Migration

- [x] 5.1 Route accepted Strategy Run Phase 2-4 reasoning through the run-scoped pre-terminal session.
- [x] 5.2 Suppress post-run reconciliation and adaptation for evidence already processed in `PreTerminalStrategy` mode.
- [x] 5.3 Limit terminal handling to sealing and read-only receipt generation; do not create a new RuntimeDecision, HypothesisAdaptation, proposal, or reasoning revision after terminal state.
- [x] 5.4 Preserve the prior non-pre-terminal behavior for Runs not configured with the Strategy execution loop.

## 6. Authority and Neutrality Guards

- [x] 6.1 Add guards proving the reasoning session and evidence view cannot generate DeviceAction, select a concrete target, define a route, order DFS branches, invoke Traversal, issue an FSM command, mutate RunState, decide recovery, mutate GoalEvidence, or assert completion.
- [x] 6.2 Add guards proving Strategy remains immutable during an active Run and RuntimeAgent cannot replace it, start another Run, or orchestrate multiple Runs.
- [x] 6.3 Add guards proving strategy reasoning consumes only the immutable evidence view and has no direct dependency on mutable World/DFS internals or outbound evidence-enrichment capability calls.
- [x] 6.4 Add source and dependency guards proving Runtime strategy execution contains no scenario-specific knowledge or external semantic dependency.

## 7. Deterministic Contract Tests

- [x] 7.1 Test one accepted intent and Agent-assigned RunId create exactly one session with H0 and N0 before the first eligible checkpoint.
- [x] 7.2 Test supported evidence produces a passive continuation-supported proposal and commits N+1 only after Agent validation.
- [x] 7.3 Test a permitted hypothesis revision remains tentative until acceptance and then produces a passive supported-after-revision proposal.
- [x] 7.4 Test an out-of-bound or unsupported revision produces a passive not-supported proposal without failing, completing, recovering, or authorizing action.
- [x] 7.5 Test every snapshot/evidence/session/intent/revision correlation mismatch, duplicate, timeout, cancellation, late result, malformed view, and terminal result fails closed without accepted reasoning mutation.
- [x] 7.6 Test one Run cannot activate both pre-terminal and post-run reasoning over the same accepted evidence.
- [x] 7.7 Test reasoning convergence never creates a completion fact and terminal completion still requires Agent-owned GoalEvidence and FSM transition.
- [x] 7.8 Test the loop remains valid without optional evidence enrichment and never invents unavailable interpretation.
- [x] 7.9 Test sealed sessions reject later checkpoints and cannot reopen, extend, replace, or create a Run.

## 8. Regression and Independent Verification

- [x] 8.1 Run Strategy Contract tests and prove existing Strategy admission and external operations remain unchanged.
- [x] 8.2 Run RuntimeAgent Directive, ExecutionHypothesis, RuntimeDecision, HypothesisAdaptation, and pre-terminal cycle tests.
- [x] 8.3 Run existing deterministic OpenWorld, DFS, lifecycle, recovery, verification, GoalEvidence, FSM, Traversal, and Environment regression suites.
- [x] 8.4 Run architecture guards and confirm zero authority delta and zero scenario/external-semantic dependency.
  - Evidence (2026-08-24): `ArchitectureGuardTests`, `ExternalSemanticCapabilityBoundaryGuardTests`, and strategy authority/neutrality guards (6.1–6.4) pass in the full deterministic run; no scenario-specific or external-semantic dependency findings.
- [x] 8.5 Run the full Runtime solution build and test suite plus repository consistency checks.
  - Evidence (2026-08-24): build 0 warnings / 0 errors; deterministic suite 1971/1971 Runtime + 32/32 Semantic green; 7 RealDevice/RealEmulator tests fail-closed on absent ADB device (hardware availability, by design); `scripts/check-consistency.sh` ALL PASS; `git diff --check` PASS; strict OpenSpec validation 60/60.
- [x] 8.6 Run strict OpenSpec validation and record evidence only after the approved implementation is complete.
- [x] 8.7 Have Sol independently review the production diff and regression evidence before any graduation decision; task completion alone does not authorize graduation or archive.

## Apply Evidence

- Runtime solution build: PASS, 0 warnings and 0 errors.
- Strategy execution loop contract tests: PASS, 19/19.
- Combined Strategy and pre-terminal targeted tests: PASS, 71/71.
- Deterministic Runtime suite excluding seven unavailable-device tests and one pre-existing scenario-neutrality guard blocker: PASS, 1849/1849.
- Semantic test project in the full solution run: PASS, 32/32.
- Full Runtime solution test attempt: 1850 passed, 8 failed. Seven failures require unavailable configured devices; one failure is the pre-existing Runtime scenario-knowledge guard finding outside this change. Task 8.5 remains open.
- Architecture/neutrality guards added by this change: PASS. 2026-08-24 re-run: the previously reported repository-wide scenario-knowledge guard blocker no longer reproduces; full deterministic suite 1971/1971 green, so tasks 1.2, 8.4, and 8.5 are closed with evidence.
- `scripts/check-consistency.sh`: PASS (re-verified 2026-08-24, C1–C12 ALL PASS).
- `git diff --check`: PASS.
- Strict OpenSpec validation: PASS.
- Graduation and archive: NOT CLAIMED.

## Design Docs

> Implementation agents must read these artifacts before starting.

| Area | Design Doc |
|---|---|
| Change scope and dependencies | `proposal.md` |
| Strategy execution architecture | `design.md` |
| Normative behavior | `specs/runtime-agent-strategy-execution-loop/spec.md` |
