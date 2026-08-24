# RuntimeAgent Pre-Terminal Cycle Specification

## Purpose

Define an optional Agent-owned checkpoint through which RuntimeAgent may evaluate accepted same-Run evidence and return a passive continuation proposal without gaining execution, lifecycle, recovery, completion, or Multi-Run authority.

## ADDED Requirements

### Requirement: Agent SHALL Own the Pre-Terminal Checkpoint

Agent SHALL be the only component that creates a pre-terminal checkpoint, assigns its cycle sequence, validates its result, and decides whether the current Run continues, completes, or fails.

The checkpoint SHALL occur only after a fresh Observation is accepted, WorldBelief revision is complete, DFS progress is updated, RunState remains `Running`, and no next action has been authorized.

#### Scenario: Eligible checkpoint is created before action authorization

- **WHEN** Agent has accepted fresh evidence, completed WorldBelief and DFS progress updates, remains Running, and has not authorized the next action
- **THEN** Agent MAY create exactly one immutable pre-terminal checkpoint for that accepted evidence revision

#### Scenario: Ineligible operational event creates no checkpoint

- **WHEN** Agent is settling an action, polling, retrying, performing low-level recovery, handling unaccepted evidence, or observing no new accepted evidence
- **THEN** Agent SHALL NOT create a pre-terminal checkpoint

### Requirement: Snapshot SHALL Be Immutable and Correlated

`PreTerminalReasoningSnapshot` SHALL contain RunId, CycleSequence, AcceptedObservationSequence, BeliefRevision, BeliefDigest, DfsProgressRevision, immutable TraceReferences including a digest or equivalent correlation value, and AcceptedReasoningRevisionReference.

The snapshot SHALL NOT expose mutable Agent, WorldBelief, DFS, FSM, Traversal, GoalEvidence, or execution-authority objects.

#### Scenario: RuntimeAgent receives bounded immutable evidence

- **WHEN** Agent dispatches an eligible checkpoint evaluation
- **THEN** RuntimeAgent receives only the immutable correlated snapshot and any bounded immutable evidence projection defined by the contract

### Requirement: Agent-Facing Proposal SHALL Be Passive

RuntimeAgent SHALL return `PreTerminalContinuationProposal` with exactly one disposition: `ContinuationSupported`, `ContinuationSupportedAfterRevision`, or `ContinuationNotSupported`.

The proposal SHALL NOT contain DeviceAction, target, selector, route, plan step, retry, recovery command, FSM command, RunState mutation, GoalEvidence mutation, completion/failure command, or callback into Agent, Traversal, FSM, or Environment.

#### Scenario: Proposal communicates support without commanding execution

- **WHEN** RuntimeAgent completes an eligible snapshot evaluation
- **THEN** it returns only a correlated passive disposition and proposed reasoning revision metadata, and Agent independently determines the lifecycle path

#### Scenario: Executable proposal shape fails closed

- **WHEN** a returned proposal contains an executable or authority-bearing field forbidden by this contract
- **THEN** Agent rejects it, authorizes zero action from it, and follows the existing fail-closed path

### Requirement: Phase 2-4 Reasoning Records SHALL Remain RuntimeAgent Internal

ExecutionHypothesis, RuntimeDecision, and HypothesisAdaptation SHALL remain RuntimeAgent-owned reasoning records. RuntimeDecision and HypothesisAdaptation SHALL NOT be exposed to Agent as continuation, lifecycle, recovery, or completion commands.

#### Scenario: Agent does not consume internal reasoning records

- **WHEN** RuntimeAgent uses RuntimeDecision or HypothesisAdaptation while evaluating a snapshot
- **THEN** those records remain inside the uncommitted reasoning revision and Agent receives only `PreTerminalContinuationProposal`

### Requirement: Evaluation SHALL Be Transactional

RuntimeAgent SHALL evaluate accepted reasoning revision N without mutating N or accepted reasoning history. Each successful evaluation SHALL produce a proposed reasoning revision N+1 whose parent is N.

Agent SHALL authorize acceptance of N+1 only through compare-and-accept after validating that N and every snapshot correlation remain current. RuntimeAgent SHALL atomically publish an accepted N+1 into its own reasoning history.

#### Scenario: Fresh proposal commits atomically

- **WHEN** proposal N+1 names the current accepted revision N and all freshness, correlation, authority, and terminal checks pass
- **THEN** Agent MAY accept the proposal and RuntimeAgent SHALL atomically commit N+1 before Agent independently decides continuation

#### Scenario: Rejected proposal leaves no reasoning mutation

- **WHEN** Agent rejects a proposal for any reason
- **THEN** proposed revision N+1 is discarded and accepted revision N and accepted reasoning history remain unchanged

### Requirement: Agent SHALL Reject Stale or Invalid Results

Agent SHALL reject a proposal when RunId, CycleSequence, AcceptedObservationSequence, BeliefRevision, BeliefDigest, DfsProgressRevision, TraceReferences/digest, or accepted reasoning parent does not match current state; when the cycle is duplicate; when RunState is terminal; when evaluation timed out or was cancelled; or when the proposal type is unknown.

A rejected, timed-out, or cancelled cycle SHALL be closed so that any later result for that cycle is rejected. Rejection SHALL produce zero action and no accepted reasoning-history mutation.

#### Scenario: Changed evidence rejects a stale proposal

- **WHEN** accepted observation, belief, DFS progress, trace correlation, or reasoning parent changes before proposal validation
- **THEN** Agent rejects the proposal, closes the cycle, and leaves accepted reasoning state unchanged

#### Scenario: Timeout or cancellation rejects late completion

- **WHEN** evaluation times out or is cancelled and a result later arrives for the closed cycle
- **THEN** Agent rejects the late result and authorizes zero action from it

#### Scenario: Duplicate cycle is rejected

- **WHEN** Agent has already accepted or closed a cycle sequence and receives another result for it
- **THEN** Agent rejects the duplicate without mutating accepted reasoning history

### Requirement: Terminal State SHALL Be Protected

Agent SHALL NOT create a checkpoint after terminal state and SHALL reject any proposal whose Run reached terminal before validation. No proposal SHALL reopen, extend, complete, or fail a Run.

#### Scenario: Terminal Run cannot be extended

- **WHEN** the Run reaches Completed or Failed before checkpoint creation or proposal acceptance
- **THEN** no checkpoint is created or accepted and the terminal state remains unchanged

### Requirement: Accepted Evidence SHALL Be Deduplicated

One accepted evidence revision SHALL create at most one checkpoint and at most one accepted proposal. Repeated observations without newly accepted evidence SHALL NOT create additional cycle sequences.

#### Scenario: Repeated evidence does not multiply checkpoints

- **WHEN** Agent observes the same evidence without accepting a new evidence revision
- **THEN** it creates no additional pre-terminal checkpoint for that evidence revision

### Requirement: Disabled Seam SHALL Have Zero Regression

The pre-terminal seam SHALL be optional. When absent or disabled, Agent SHALL execute its existing Run, DFS, FSM, Traversal, recovery, verification, GoalEvidence, and terminal behavior without additional checkpoints, reasoning mutations, or outcome changes.

#### Scenario: Existing execution remains unchanged when disabled

- **WHEN** no pre-terminal reasoning capability is configured
- **THEN** Agent follows the existing execution path with no checkpoint call and no pre-terminal reasoning state

### Requirement: Contract SHALL Preserve Frozen Authority

RuntimeAgent and `PreTerminalContinuationProposal` SHALL NOT call or invoke DeviceAction, Traversal, FSM transition, RunState mutation, recovery, GoalEvidence completion, another Run, or Multi-Run orchestration.

#### Scenario: Proposal cannot cross execution authority boundary

- **WHEN** RuntimeAgent returns a valid proposal
- **THEN** only Agent may independently authorize an existing DFS/Traversal action or choose an existing lifecycle outcome
