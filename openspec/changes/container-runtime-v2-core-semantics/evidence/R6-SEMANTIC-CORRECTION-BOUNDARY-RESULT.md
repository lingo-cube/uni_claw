# R6 Semantic Correction Boundary Result

Date: 2026-09-01

## STATUS

`CONTAINER_RUNTIME_V2_READY_FOR_AGENT_INTEGRATION_HUMAN_GATE`

This is not graduation.  It is the completion of the reversible Runtime-side
contract slice immediately before an upper-layer Agent authority change.

## PURCHASED

- A current Slow `Challenge` or `Correct` assessment may be projected into an
  immutable correction fact only when its Observation, evidence revision,
  destination Node, source Node, trigger occurrence, and TransitionOccurrence
  bindings are complete.
- Slow actual/corrected semantics and Agent intended-obligation semantics are
  separate inputs.  Slow does not invent the intended obligation; the Agent
  owner does not inject the observed correction.
- Traversal and directed-entry implications are typed owner context.  The
  output contains pending/visited candidates for owner reevaluation, not
  applied obligation state.
- A checkpoint is only the last sufficiently confirmed node in an explicitly
  ordered, current-revision, correct execution path.  It is a proposal, not
  Graph state, a recovery plan, or an FSM state.

## HYPOTHESIS

- Semantic correction to Agent obligation recomputation remains an
  `ARCHITECTURE_HYPOTHESIS` until a separately authorized Agent consumer and
  fresh Phase 2.6 evidence demonstrate lower wrong-branch, unresolved, and
  repeated-repair rates.
- Checkpoint usefulness remains optional and falsifiable.  No production
  checkpoint lifecycle or recovery behavior was purchased.

## IMPLEMENTED

- `ContainerSemanticCorrectionFact`
- `ContainerObligationContextRef`
- `ContainerObligationContextKind`
- `ContainerObligationContext`
- `ContainerObligationReevaluationInput`
- `ContainerPathConfirmation`
- `ContainerExecutionPath`
- `ContainerCheckpointProposal`
- `ContainerSemanticCorrectionProjector`
- Mechanical `CurrentContainerSnapshot` to `CurrentContainer` rename required
  by the pre-existing OBS-F10 global guard; no behavior or authority changed.

`NEW_SYMBOL_JUSTIFICATION`: existing `BranchProgressEvidence`, `GoalEvidence`,
and `ActiveContainerContext` are Agent-owned obligation/completion/execution
state and cannot own a Runtime semantic assessment projection without changing
authority.  `SlowContainerSemanticAssessment` is immutable raw advisor evidence
and cannot also contain Agent-owned intended obligation context.  The new
records are the minimum immutable join boundary between those responsibilities.
`ContainerExecutionPath` is required to make checkpoint selection depend on
explicit path order instead of arbitrary enumeration order.

## VALIDATED

- Independent V2/Core/Transition/Fast/Slow/Correction regression: 135 passed,
  0 failed.
- Independent Architecture Guard filter: 60 passed, 0 failed.
- `NoContainerSnapshotOrAgentPublicSurfaceExpansion`: passed after the
  mechanical rename; the global guard itself was not modified.
- `dotnet build src/UniClaw.Runtime.sln --no-restore -v:minimal`: 0 warnings,
  0 errors.
- `scripts/check-consistency.sh`: C1-C15 all passed.
- strict OpenSpec validation: passed.
- `git diff --check`: passed.
- Full solution tests were executed and classified:
  - `Semantic.Tests`: 153 passed, 5 failed.  Failures are existing V2/V3
    qualification thresholds for CorrectRecovery and SettingsRoot starvation,
    outside the R6 files.
  - `UniClaw.Runtime.Tests`: 2527 passed, 12 failed.  Seven require an eligible
    ADB device; three are Vision model-identity configuration mismatches; one
    is the dirty ValidationHarness scenario-token guard; one is the existing
    Scroll stability expectation.  No Container Runtime V2 R6 test or guard
    failed after the rename.

## DEFERRED

- Any concrete Slow provider/model/backend, deployment policy, cost policy, or
  mandatory Runtime role.
- Any mutation of `_branchProgress`, exploration ledger, `GoalEvidence`,
  `ActiveContainerContext`, action authorization, return/recovery, or Run
  completion.
- Production checkpoint state and recovery behavior.
- Fresh-device Phase 2.6 acceptance and graduation.

## RISKS

- A read-only correction input has no production benefit until an Agent owner
  consumes it; that consumption changes upper-layer obligation semantics.
- `ObservedVisitedCandidate` remains a candidate.  Treating it as completed or
  visited without Agent-owned evidence would violate I-10 and the purchased
  boundary.
- Full-suite unrelated/environmental failures prevent a repository-wide green
  claim and remain separately owned.

## HUMAN GATE

`REQUIRED_HUMAN_GATE_UPPER_AGENT_AUTHORITY`

The next behavior-changing step must connect
`ContainerObligationReevaluationInput` to Agent-owned obligation/progress
evidence.  That step would decide when traversal C remains pending, when D may
be recorded as visited, and when a directed wrong branch may cause a separately
authorized return/recovery/re-entry decision.  It therefore changes the frozen
Agent/Goal authority boundary and matches Human Gate rule 4.

The bounded option awaiting authorization is:

1. add an Agent-owned consumer that accepts the immutable correction input;
2. update only existing run-local obligation/progress evidence by immutable
   replacement;
3. preserve `GoalEvidence` as the sole completion authority and existing action
   authorization as the sole dispatch authority;
4. add traversal C/D and directed wrong-branch stateful tests;
5. keep Slow, Graph, and checkpoint proposal unable to dispatch or complete.

The alternative is to keep R6 Shadow/read-only and gather correction precision
evidence before changing Agent behavior.

## NEXT_WORKITEM

Blocked pending Human authorization: a narrowly scoped Agent-owned correction
consumer with BEFORE/AFTER authority proof and `NET_NEW_MUTABLE_TRUTH = 0`.
