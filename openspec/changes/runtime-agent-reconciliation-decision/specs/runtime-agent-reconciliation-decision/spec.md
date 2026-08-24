# Spec: runtime-agent-reconciliation-decision

> Spec-driven definition of the runtime reconciliation & decision capability. Additive; reuses the
> existing DFS execution unchanged. Source baseline verified 2026-08-21 (uni-agent branch, Phase 1-2
> verified clean).

## Purpose

Lets the RuntimeAgent explicitly reconcile an ExecutionHypothesis against the observed WorldBelief and
trace evidence and produce a bounded RuntimeDecision (Continue / Revise / Escalate) that classifies
the reconciliation outcome — without granting the decision any execution, authorization, completion,
or traversal authority.

## ADDED Requirements

### Requirement: Runtime decision representation

The Runtime MUST provide an immutable `RuntimeDecision` record expressing one runtime-level decision
after reconciliation: a run identity, a decision state, a hypothesis reference, an evidence reference,
and a decision reason. The record MUST NOT carry an Action, an authorization, a UI element selection, a
Goal modification, a Traversal control, scenario strings, or any execution authority.

#### Scenario: decision carries only a reconciliation outcome
- **WHEN** a RuntimeDecision is constructed with RunId, State, HypothesisReference, EvidenceReference,
  and DecisionReason
- **THEN** it exposes exactly those fields
- **AND** it exposes no Action, no authorization, no UI element, no Goal modification, and no Traversal
  control

#### Scenario: decision rejects invalid construction
- **WHEN** a RuntimeDecision is constructed with a blank RunId or blank DecisionReason
- **THEN** construction fails with an explicit validation error
- **AND** no decision instance is created

### Requirement: Decision states

The Runtime MUST define a `RuntimeDecisionState` enum with exactly Continue, Revise, and Escalate.
Continue means the current hypothesis remains consistent with the observed world. Revise means the
current hypothesis no longer matches world evidence. Escalate means the problem exceeds the current
RuntimeAgent authority.

#### Scenario: decision states are exhaustive
- **WHEN** the state enum is inspected
- **THEN** it contains exactly Continue, Revise, and Escalate
- **AND** no other states exist

### Requirement: Stateless hypothesis reconciliation

The Runtime MUST provide a `HypothesisReconciler` that is a stateless static pure function mapping an
`ExecutionHypothesis`, a `WorldBelief`, and trace evidence into exactly one `RuntimeDecision`. The
reconciler MUST NOT observe the world, MUST NOT authorize an action, MUST NOT execute anything, MUST
NOT modify the Goal or completion, and MUST NOT contain scenario-specific knowledge. It classifies
evidence into a decision state; it does not perform the decision.

#### Scenario: reconciler is deterministic and world-free
- **WHEN** the same (hypothesis, belief, trace) inputs are reconciled twice
- **THEN** both reconciliations produce structurally identical decisions
- **AND** neither reconciliation performs an observation or dispatches an action

#### Scenario: reconciler uses no scenario strings
- **WHEN** a reconciliation produces a decision reason
- **THEN** the reason is derived from generic trace event reasons and belief state
- **AND** it contains no application-specific or settings-specific string

### Requirement: Continue decision classification

The reconciler MUST produce a Continue decision when the hypothesis remains consistent with the
observed world: the hypothesis status is Confirmed or Active (not contradicted), the world belief is
understood (SemanticPage is non-null), and the trace shows in-scope progress without a boundary
contradiction.

#### Scenario: expected child reached produces Continue
- **WHEN** the hypothesis expects a child transition and the trace shows an in-scope inventory complete
  or verified return without a boundary contradiction, and the belief SemanticPage is non-null
- **THEN** the reconciler produces a Continue decision
- **AND** the decision reason references the confirming evidence

### Requirement: Revise decision classification

The reconciler MUST produce a Revise decision when the hypothesis no longer matches the world evidence:
the trace shows an external boundary observation (hypothesis contradicted), or the hypothesis status is
Revised, or the world belief is unknown (SemanticPage is null) but the run is still within RuntimeAgent
authority (not a terminal authority-boundary failure).

#### Scenario: external boundary produces Revise
- **WHEN** the hypothesis expects a recursive child transition and the trace shows an
  EXTERNAL_BOUNDARY_OBSERVED inflection point
- **THEN** the reconciler produces a Revise decision
- **AND** the decision reason references the boundary contradiction

#### Scenario: unknown belief produces Revise
- **WHEN** the hypothesis status is Active but the world belief SemanticPage is null (unknown)
- **THEN** the reconciler produces a Revise decision
- **AND** the decision reason references the unknown world state

### Requirement: Escalate decision classification

The reconciler MUST produce an Escalate decision when the problem exceeds the current RuntimeAgent
authority: the run failed (RunState.Failed) with an authority-boundary failure reason (identity safety,
depth cutoff, boundary not handled), or the hypothesis was Revised and the run failed (RuntimeAgent
could not reconcile and continue within its bounded authority). Escalate is a RECORD of the authority
boundary being exceeded — the RuntimeAgent does not perform an escalation action.

#### Scenario: authority-boundary failure produces Escalate
- **WHEN** the run failed with a reason containing an authority-boundary indicator (identity safety,
  depth cutoff, or boundary not handled)
- **THEN** the reconciler produces an Escalate decision
- **AND** the decision reason references the authority boundary

#### Scenario: revised-and-failed produces Escalate
- **WHEN** the hypothesis status is Revised and the run outcome is Failed
- **THEN** the reconciler produces an Escalate decision
- **AND** the decision reason references the unreconciled failure

### Requirement: No authority over execution

The RuntimeDecision and HypothesisReconciler MUST NOT acquire any decision, authorization, completion,
or execution authority. The RuntimeDecision MUST NOT be consulted by the Agent for decisions,
authorization, completion, or execution. The reconciler MUST NOT call any Agent method that mutates run
state, authorizes an action, evaluates GoalEvidence, or dispatches a DeviceAction. The RuntimeAgent
MUST remain the sole run-level semantic and execution authority; the DFS engine MUST be unchanged.

#### Scenario: decision cannot authorize actions
- **WHEN** the decision model and reconciler are inspected
- **THEN** they expose no method that authorizes an action or produces authorization evidence
- **AND** the Agent's authorization path does not reference the decision

#### Scenario: decision cannot bypass the Agent
- **WHEN** a directive is run with a hypothesis ledger
- **THEN** the RunState is produced by the Agent's existing DFS engine, not by the decision or reconciler
- **AND** the Agent does not consult the decision for any decision

#### Scenario: decision cannot alter completion
- **WHEN** the run completes
- **THEN** the GoalEvidence is evaluated by the existing evidence evaluator, not by the decision
- **AND** the decision state reflects the outcome but does not determine it

#### Scenario: decision cannot create recursive authority
- **WHEN** the decision model and reconciler are inspected
- **THEN** they expose no method that dispatches an action, creates a container, or initiates a sub-run
- **AND** Escalate is a record, not an escalation action

### Requirement: Additive integration without DFS modification

The reconciliation MUST integrate into the existing `ExecutionHypothesisLedger` and
`DirectiveExecution` entry additively. The ledger gains a `Reconcile(WorldBelief?)` method and a
`LatestDecision` property. The `DirectiveExecution` entry calls `ledger.Reconcile(agent.Belief)` inside
the existing ContinueWith (when the ledger is non-null). When the ledger is absent (null), the existing
Phase 1-2 behavior MUST be preserved with zero regression. The DFS engine, the `IntentExecution` seam,
`Agent.OpenWorld.cs`, `Agent.cs`, `Container/`, `Traversal/`, `Recovery/`, and `World/` MUST remain
unchanged.

#### Scenario: absent ledger preserves existing behavior
- **WHEN** `DirectiveExecution.RunDirectiveAsync` is called without a hypothesis ledger
- **THEN** it behaves exactly as the Phase 1-2 implementation
- **AND** no decision is created or recorded

#### Scenario: DFS engine is not modified
- **WHEN** the change is implemented
- **THEN** `Agent.OpenWorld.cs`, `Agent.cs`, `Container/`, `Traversal/`, `Recovery/`, and `World/`
  are byte-unchanged
- **AND** the DFS loop does not reference the decision or reconciler

### Requirement: Existing capability regression

The capability MUST NOT change the behavior of the existing open-world execution, bounded candidate
safety, cross-page discovery, the SETTINGS-TREE-01 capstone, the Phase 1 directive capability, or the
Phase 2 execution hypothesis capability. The existing deterministic suites for those capabilities MUST
remain green.

#### Scenario: settings-tree capstone remains green
- **WHEN** the capability is implemented and the full Runtime test suite is run
- **THEN** the SETTINGS-TREE-01 capstone proofs (TREE-1..TREE-20) pass unchanged

#### Scenario: phase 1 and phase 2 tests remain green
- **WHEN** the capability is implemented and the full Runtime test suite is run
- **THEN** all Phase 1 directive decomposition tests and Phase 2 execution hypothesis tests pass
  unchanged
