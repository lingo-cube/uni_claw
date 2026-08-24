# Design: RuntimeAgent Pre-Terminal Cycle Contract

## Context

RuntimeAgent Phase 1-4 provides Directive, ExecutionHypothesis, RuntimeDecision, and HypothesisAdaptation. RuntimeDecision and HypothesisAdaptation are RuntimeAgent-internal reasoning records; they are not Agent-facing lifecycle commands.

Agent remains the sole Run execution authority. It owns checkpoint timing, RunState, cycle sequence, observation acceptance, WorldBelief and DFS progress updates, proposal validation, same-Run continuation, action authorization, recovery, GoalEvidence, and terminal outcome. FSM owns lifecycle transition protocol, Traversal owns concrete execution, and Environment owns Observation acquisition.

The contract adds one optional adapter boundary so RuntimeAgent reasoning can participate before terminal state without moving any frozen authority.

## Goals

- Allow RuntimeAgent to evaluate one immutable, Agent-created snapshot during an active Run.
- Return only a passive continuation proposal to Agent.
- Preserve RuntimeAgent ownership of ExecutionHypothesis, RuntimeDecision, HypothesisAdaptation, and reasoning history.
- Make reasoning updates transactional: rejected evaluation cannot mutate accepted state.
- Reject stale, duplicated, late, cancelled, timed-out, terminal, or malformed results through the fail-closed path.
- Preserve the current DFS engine and all execution/lifecycle authority.
- Provide zero behavioral regression when the optional seam is disabled.

## Non-Goals

- No RuntimeAgent action generation or execution.
- No RuntimeAgent continuation, failure, recovery, completion, FSM, or RunState authority.
- No Agent interpretation of RuntimeDecision or HypothesisAdaptation.
- No outer RuntimeAgent loop, new Run, Multi-Run orchestration, or CycleCoordinator owning execution.
- No Strategy integration, semantic capability, scenario knowledge, routes, selectors, or plans.
- No rewrite of existing Phase 1-4 contracts, Agent DFS behavior, FSM, Traversal, or GoalEvidence.

## Final Ownership Model

| Owner | Exclusive responsibility at this seam |
|---|---|
| RuntimeAgent | ExecutionHypothesis; internal RuntimeDecision; internal HypothesisAdaptation; evaluation of an immutable snapshot; proposed reasoning revision; reasoning history after accepted commit |
| Agent | checkpoint timing; RunId and cycle sequence; accepted observation; WorldBelief and DFS progress; freshness/correlation validation; proposal acceptance/rejection; same-Run continuation; action authorization; recovery; verification; GoalEvidence; RunState; terminal outcome |
| FSM | lifecycle transition protocol |
| Traversal | concrete execution |
| Environment | Observation acquisition |

## Contract Models

### PreTerminalReasoningSnapshot

Agent creates an immutable snapshot only at an eligible checkpoint. The snapshot contains value data and references sufficient for correlation; it exposes no mutable Agent state and no execution callback.

Required fields:

- `RunId`
- `CycleSequence`
- `AcceptedObservationSequence`
- `BeliefRevision`
- `BeliefDigest`
- `DfsProgressRevision`
- `TraceReferences`, including a trace digest or equivalent immutable correlation value
- `AcceptedReasoningRevisionReference`

An implementation may include a bounded immutable projection of accepted evidence needed for reasoning. It must not include DeviceAction authority, mutable WorldBelief/DFS objects, FSM handles, lifecycle callbacks, or completion authority.

### PreTerminalContinuationProposal

RuntimeAgent returns one immutable proposal correlated to the snapshot. Its disposition is exactly one of:

- `ContinuationSupported`
- `ContinuationSupportedAfterRevision`
- `ContinuationNotSupported`

The proposal also carries correlation values copied from the snapshot, the accepted parent reasoning revision N, and the proposed reasoning revision N+1. The proposed revision is opaque to Agent except for identity, parentage, and commit validation.

The proposal MUST NOT contain:

- `DeviceAction`
- target, selector, route, or plan step
- retry or recovery command
- FSM command or lifecycle transition
- RunState mutation or desired RunState
- GoalEvidence mutation
- completion or failure command
- callbacks into Agent, Traversal, FSM, or Environment

`RuntimeDecision` and `HypothesisAdaptation` are not fields of the Agent-facing proposal. They remain internal records used by RuntimeAgent while producing the opaque proposed reasoning revision.

## Checkpoint Boundary

Agent may create a checkpoint only after all of the following are true:

1. A fresh Observation has been accepted.
2. WorldBelief revision for that evidence is complete.
3. DFS progress is updated.
4. RunState is still `Running`.
5. The next action has not been authorized.

One accepted evidence revision creates at most one checkpoint. The cycle sequence is Agent-owned and monotonic within the Run. A checkpoint is not created for action settling, polling, retry, low-level recovery, a provisional/unaccepted observation, or repeated observation without newly accepted evidence.

## Transactional Reasoning Revision

```text
Accepted reasoning revision N
        |
        v
RuntimeAgent evaluates immutable snapshot
without mutating N or accepted history
        |
        v
PreTerminalContinuationProposal
with proposed reasoning revision N+1
        |
        v
Agent compare-and-accept validation
        |
        +-- reject --> discard proposal and N+1
        |              accepted N/history unchanged
        |              zero action; existing fail-closed path
        |
        +-- accept --> authorize RuntimeAgent to atomically commit N+1
                       Agent independently decides continuation
```

Evaluation may construct tentative internal RuntimeDecision and HypothesisAdaptation records, but these belong only to proposed revision N+1. They are not appended to accepted reasoning history until Agent accepts the proposal. The commit updates only RuntimeAgent reasoning history; it does not mutate RunState, WorldBelief, DFS progress, FSM, Traversal, GoalEvidence, or action authorization.

Compare-and-accept succeeds only when accepted reasoning revision N is still the proposal's parent and all snapshot correlations remain current. Agent authorizes acceptance; the RuntimeAgent reasoning layer atomically publishes N+1 into its own history. No separate mutation may occur before that acceptance.

## Lifecycle

```text
Agent Running
    |
    v
Accepted fresh Observation
    |
    v
WorldBelief update + DFS progress update
    |
    v
Agent creates immutable PreTerminalReasoningSnapshot
    |
    v
RuntimeAgent evaluates snapshot using internal Phase 2-4 records
    |
    v
PreTerminalContinuationProposal + proposed revision N+1
    |
    v
Agent validates freshness, correlation, authority, and revision parentage
    |
    +-- reject
    |     discard proposal and N+1
    |     zero action
    |     accepted reasoning state remains N
    |     existing fail-closed path
    |
    +-- accept
          authorize RuntimeAgent to atomically commit reasoning revision N+1
          Agent independently chooses Continue / Complete / Fail
          existing DFS execution continues only if Agent authorizes it
```

The seam does not create a second execution loop. It is an optional checkpoint within Agent's existing `RunOpenWorldAsync` ownership boundary.

## Validation and Rejection

Agent rejects and closes the cycle when any of the following holds:

- RunId mismatch.
- CycleSequence mismatch or duplicate cycle.
- AcceptedObservationSequence changed.
- BeliefRevision or BeliefDigest changed.
- DfsProgressRevision changed.
- Trace digest/reference mismatch.
- Accepted reasoning revision N is no longer current or N+1 does not name N as parent.
- RunState reached terminal before validation.
- Evaluation timed out or was cancelled.
- Proposal disposition is unknown or proposal shape violates the passive contract.

Closing a rejected, timed-out, or cancelled cycle prevents a late result from being accepted. Rejection produces zero action and leaves accepted reasoning state and history unchanged. Agent follows its existing fail-closed behavior; the proposal cannot select that behavior.

No checkpoint may be created after terminal state, and no proposal may reopen or extend a terminal Run.

## Interaction with Existing Capabilities

### Strategy Contract

No Strategy integration is added. A future separately approved change may supply bounded runtime context, but this contract neither depends on Strategy nor changes Strategy ownership.

### ExecutionHypothesis

ExecutionHypothesis remains RuntimeAgent-owned. A proposed revision may contain a tentative hypothesis update internally; Agent sees only the opaque reasoning revision identity and passive disposition.

### RuntimeDecision and HypothesisAdaptation

Both remain internal RuntimeAgent records. Existing contracts are not rewritten or promoted to Agent-facing commands. The pre-terminal adapter may compose these records inside an uncommitted proposed revision.

### Agent.RunOpenWorldAsync

Agent retains its existing Run loop and gains only an optional checkpoint seam at the defined pre-action boundary. Agent performs validation and independently decides the existing continuation/lifecycle path. With the seam disabled or absent, execution follows the current path without new calls, state, or outcome changes.

## Reasoning Mode and Duplication Prevention

A Run uses one configured reasoning mode. Enabling the pre-terminal seam must not cause the same evidence/reasoning revision to be reconciled again through a post-run path. Terminal observability may finalize records, but it cannot create another continuation proposal or mutate an already accepted revision without a distinct contract.

## Risks and Mitigations

- **Risk: tentative reasoning leaks into accepted history.** Mitigation: evaluate against immutable N, stage N+1, and commit only through Agent compare-and-accept.
- **Risk: stale asynchronous results influence execution.** Mitigation: validate all correlation fields and close timed-out/cancelled/rejected cycles against late acceptance.
- **Risk: proposal becomes a hidden command.** Mitigation: closed disposition vocabulary, forbidden executable fields, authority guards, and zero direct action path.
- **Risk: checkpoint fires on noisy observations.** Mitigation: only accepted fresh evidence after belief and DFS revision may create one checkpoint.
- **Risk: optional feature changes baseline behavior.** Mitigation: disabled seam creates no checkpoint and preserves existing execution exactly.

## Migration Plan

1. Add immutable snapshot and passive proposal contract models.
2. Add proposed/accepted reasoning revision transaction support inside RuntimeAgent reasoning ownership.
3. Add the optional Agent checkpoint and validation seam at the defined boundary.
4. Add stale, duplicate, late, timeout, cancellation, terminal, and unknown-proposal rejection.
5. Add authority guards and deterministic tests before enabling the seam in any composition.

No Strategy, semantic capability, scenario knowledge, DFS redesign, FSM change, Traversal change, GoalEvidence change, or production enablement is part of this change.
