# runtime-agent-strategy-execution-loop Specification

## Purpose

Define how one accepted bounded Strategy participates transactionally in the existing Agent-owned Run through scenario-neutral evidence, RuntimeAgent-local hypotheses, and passive pre-terminal continuation proposals without acquiring execution or lifecycle authority.

## Requirements

### Requirement: One Accepted Strategy SHALL Create One Run-Scoped Reasoning Session

After Strategy admission produces one `RuntimeExecutionIntent` and Agent assigns one Run identity, the system SHALL create at most one RuntimeAgent-owned reasoning session for that intent and Run before the first eligible pre-terminal checkpoint. The session SHALL retain the immutable intent reference, accepted adaptation boundary, current `ExecutionHypothesis`, and accepted reasoning revision history. It MUST NOT start, restart, extend, or control the Run.

#### Scenario: Accepted intent is bound once

- **WHEN** one accepted runtime execution intent is assigned to an Agent-owned Run
- **THEN** exactly one pre-terminal reasoning session is bound to that intent reference and Run identity before its first checkpoint evaluation

#### Scenario: Session cannot become an execution controller

- **WHEN** the reasoning session evaluates evidence or records an accepted reasoning revision
- **THEN** it invokes no action, Traversal operation, FSM transition, recovery path, Run start, or terminal operation

### Requirement: Strategy Execution Evidence SHALL Be Immutable, Typed, and Scenario Neutral

Each strategy checkpoint SHALL provide a `StrategyExecutionEvidenceView` containing only immutable typed evidence projections and correlation values. The view SHALL include the execution-intent reference, Run and accepted-observation references, belief and structural-progress revisions, structural progress facts, coverage evidence references, contradiction evidence references, and trace references needed for bounded reasoning.

The view MUST NOT expose mutable World internals or contain free-form scenario strings, actions, selectors, routes, concrete targets, branch ordering, completion flags, GoalEvidence mutation, FSM commands, lifecycle callbacks, or executable delegates.

#### Scenario: Eligible checkpoint projects accepted evidence

- **WHEN** Agent has accepted a fresh observation and completed belief and structural-progress updates
- **THEN** it supplies the reasoning session with an immutable evidence view correlated to the same checkpoint snapshot and accepted revisions

#### Scenario: Mutable or authority-bearing projection fails closed

- **WHEN** a proposed evidence view exposes mutable Runtime state or contains an executable, lifecycle-bearing, target-bearing, or completion-bearing field
- **THEN** the view is rejected before RuntimeAgent reasoning can use it

### Requirement: Evidence Projection SHALL Isolate RuntimeAgent From World Internals

RuntimeAgent strategy reasoning SHALL consume the bounded evidence view rather than directly reading mutable WorldBelief, DFS state, GoalEvidence, Agent state, or Environment state. Evidence projection SHALL preserve only the typed facts and immutable references required to reconcile the current hypothesis.

#### Scenario: World representation changes behind the boundary

- **WHEN** the internal representation of accepted World or structural-progress state changes without changing the evidence-view contract
- **THEN** the strategy reasoning session continues to consume the same bounded typed projection and gains no direct dependency on those internals

### Requirement: Strategy and Execution Hypothesis SHALL Remain Distinct

The accepted Strategy SHALL remain an immutable bounded execution policy containing objective, constraints, evidence expectations, and adaptation permissions. It MUST NOT become an execution plan, action list, route, or traversal ordering. `ExecutionHypothesis` SHALL remain a RuntimeAgent-owned, revisable assumption about current execution progress and expected evidence, and MUST NOT become a user-level Plan or action authority.

#### Scenario: Evidence contradicts the current hypothesis

- **WHEN** accepted evidence contradicts the current execution hypothesis while the Strategy remains valid
- **THEN** RuntimeAgent may propose a new hypothesis revision only within the immutable Strategy adaptation boundary

#### Scenario: Useful revision would replace Strategy

- **WHEN** continued reasoning would require replacing the objective, scope, constraints, evidence expectations, or adaptation permissions of the accepted Strategy
- **THEN** the reasoning session does not replace the Strategy and returns no authority-expanding fallback

### Requirement: Runtime Decision and Adaptation SHALL Remain Internal

For each eligible checkpoint, RuntimeAgent SHALL evaluate the current hypothesis against the evidence view using its internal `RuntimeDecision` and, when permitted, internal `HypothesisAdaptation`. Those records SHALL remain inside the proposed reasoning revision and MUST NOT be returned to Agent as lifecycle, recovery, continuation, or completion commands.

#### Scenario: Internal reasoning supports the current hypothesis

- **WHEN** internal reconciliation finds the current hypothesis consistent with accepted evidence
- **THEN** RuntimeAgent returns a correlated passive `ContinuationSupported` proposal without exposing the internal decision record

#### Scenario: Internal reasoning supports a permitted revision

- **WHEN** reconciliation requires a hypothesis revision allowed by the accepted adaptation boundary
- **THEN** RuntimeAgent stages the internal revision and returns a correlated passive `ContinuationSupportedAfterRevision` proposal

#### Scenario: Internal reasoning cannot support bounded continuation

- **WHEN** reconciliation requires unsupported interpretation or an adaptation outside the accepted boundary
- **THEN** RuntimeAgent returns a correlated passive `ContinuationNotSupported` proposal without deciding that the Run fails or terminates

### Requirement: Reasoning Revision SHALL Commit Only Through Agent Acceptance

Checkpoint evaluation SHALL read accepted reasoning revision N without mutating it and SHALL stage proposed revision N+1. Agent SHALL validate the existing checkpoint correlations, evidence-view correlations, intent reference, session identity, revision parentage, timeout, cancellation, and non-terminal state before authorizing compare-and-accept. Rejection SHALL discard N+1 and leave accepted reasoning history unchanged.

#### Scenario: Fresh reasoning revision is accepted

- **WHEN** proposal N+1 names current revision N and all checkpoint, evidence, intent, session, timing, and terminal validations pass
- **THEN** Agent may authorize the reasoning session to atomically commit N+1 before independently deciding the existing Run path

#### Scenario: Stale evidence view is rejected

- **WHEN** any accepted-observation, belief, structural-progress, trace, intent, session, or reasoning-parent correlation differs at validation time
- **THEN** Agent rejects the proposal, discards N+1, and authorizes zero action from it

### Requirement: Strategy Reasoning SHALL Be a Participant Inside One Agent Run

The lifecycle SHALL be Strategy admission, runtime execution intent, run-scoped reasoning session, initial hypothesis, Agent-owned Run, accepted observation, belief and structural-progress update, checkpoint, RuntimeAgent reasoning, passive continuation proposal, Agent validation, and accepted revision or discard. This sequence MUST NOT create a second execution loop, a successor Run, or Multi-Run orchestration.

#### Scenario: Accepted proposal participates in the same Run

- **WHEN** Agent accepts a fresh continuation proposal during an active Run
- **THEN** only the reasoning revision is committed and Agent independently determines whether the same existing Run follows an already-authorized continuation path

#### Scenario: Run is terminal

- **WHEN** Agent reaches a terminal state
- **THEN** the reasoning session is sealed, accepts no further checkpoint or revision, and cannot reopen, extend, replace, or create a Run

### Requirement: One Run SHALL Use One Reasoning Mode

At Run admission, the system SHALL select at most one reasoning mode for the accepted evidence history. A Run using pre-terminal strategy reasoning MUST NOT perform post-run reconciliation or adaptation over the same evidence. Terminal finalization MAY expose a read-only receipt of already accepted reasoning history but MUST NOT create another decision, adaptation, continuation proposal, or accepted revision.

#### Scenario: Pre-terminal mode reaches terminal finalization

- **WHEN** a Run using pre-terminal strategy reasoning reaches terminal finalization
- **THEN** finalization seals and reports existing accepted reasoning history without executing duplicate post-run reasoning

### Requirement: Abstract Exploration SHALL Reuse Existing Execution Ownership

The capability SHALL support bounded abstract exploration through generic requirements to discover bounded children, maintain structural progress, evaluate coverage evidence, verify continuity, and detect contradictions. RuntimeAgent SHALL own only the associated hypothesis and reconciliation. Agent SHALL retain discovery coordination, action authorization, verification, GoalEvidence evaluation, convergence-to-completion, and terminal decisions. Traversal SHALL retain concrete execution.

Any optional evidence enrichment SHALL occur outside this capability before Agent accepts and projects evidence; the strategy execution loop MUST NOT require or invoke an external semantic capability.

#### Scenario: Structural evidence supports further exploration

- **WHEN** accepted structural-progress evidence indicates unresolved in-scope obligations
- **THEN** RuntimeAgent may support continued reasoning while Agent independently discovers, selects, and authorizes any concrete next work

#### Scenario: Evidence enrichment is absent

- **WHEN** no external evidence enrichment is configured
- **THEN** the strategy execution loop remains valid using only the typed accepted evidence available from the existing Runtime boundary and does not invent missing interpretation

### Requirement: Reasoning Convergence SHALL Not Imply Execution Completion

RuntimeAgent reasoning convergence SHALL mean only that the accepted execution hypothesis is stable against the available evidence. RuntimeAgent MUST NOT produce `Completed`, `GoalSatisfied`, `Terminal`, a completion flag, or an equivalent terminal assertion. Execution completion SHALL require Agent-owned GoalEvidence verification and the existing FSM transition protocol.

#### Scenario: Hypothesis becomes stable

- **WHEN** repeated accepted evidence supports the current execution hypothesis without requiring revision
- **THEN** RuntimeAgent may record reasoning convergence while the Run remains subject to Agent-owned continuation and completion evaluation

#### Scenario: Coverage evidence appears sufficient

- **WHEN** the evidence view references coverage that may satisfy the accepted Strategy expectation
- **THEN** RuntimeAgent does not complete the Run and only Agent may establish completion from GoalEvidence through the existing lifecycle path

### Requirement: Frozen Authority SHALL Be Mechanically Preserved

The strategy execution capability and all of its models SHALL have no dependency path that permits RuntimeAgent to generate an action, select a concrete target, define a route, order DFS branches, invoke Traversal, issue an FSM command, mutate RunState, decide recovery, mutate or satisfy GoalEvidence, replace Strategy, start another Run, or orchestrate multiple Runs.

#### Scenario: Authority surface is inspected

- **WHEN** the strategy execution models and dependencies are mechanically inspected
- **THEN** no forbidden execution, lifecycle, recovery, completion, Strategy-replacement, external-semantic, scenario-knowledge, or Run-creation path is present
