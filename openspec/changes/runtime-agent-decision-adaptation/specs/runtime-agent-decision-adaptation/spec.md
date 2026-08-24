# Spec: runtime-agent-decision-adaptation

> Spec-driven definition of the decision-driven hypothesis adaptation capability. Additive; reuses the
> existing DFS execution, FSM, and ExternalBoundary capability unchanged. Source baseline verified
> 2026-08-22 (uni-agent branch, Phases 1-3 verified clean).

## Purpose

Lets the RuntimeAgent apply a bounded RuntimeDecision to update its run-local execution hypothesis —
closing the decision-to-hypothesis loop — without granting the adaptation any planning, execution,
authorization, recovery, or traversal authority.

## ADDED Requirements

### Requirement: Hypothesis adaptation representation

The Runtime MUST provide an immutable `HypothesisAdaptation` record expressing one bounded modification
of the execution hypothesis: a run identity, an adaptation type, a decision reference, a previous
hypothesis reference, an adapted hypothesis, and an adaptation reason. The record MUST NOT carry a Plan,
a DeviceAction, a Tap instruction, a UI element selection, a Goal modification, a Traversal control,
scenario strings, or any execution authority.

#### Scenario: adaptation carries only a hypothesis update
- **WHEN** a HypothesisAdaptation is constructed with RunId, AdaptationType, DecisionReference,
  PreviousHypothesisReference, AdaptedHypothesis, and AdaptationReason
- **THEN** it exposes exactly those fields
- **AND** it exposes no Plan, no DeviceAction, no Tap, no UI element, no Goal modification, and no
  Traversal control

#### Scenario: adaptation rejects invalid construction
- **WHEN** a HypothesisAdaptation is constructed with a blank RunId or blank AdaptationReason
- **THEN** construction fails with an explicit validation error
- **AND** no adaptation instance is created

### Requirement: Adaptation types

The Runtime MUST define a `HypothesisAdaptationType` enum with exactly Keep, Replace, and Escalate. Keep
means the current hypothesis remains valid. Replace means the current hypothesis no longer explains
reality and is superseded by a new boundary-aware hypothesis. Escalate means the RuntimeAgent cannot
adapt inside its current authority and records its inability.

#### Scenario: adaptation types are exhaustive
- **WHEN** the type enum is inspected
- **THEN** it contains exactly Keep, Replace, and Escalate
- **AND** no other types exist

### Requirement: Stateless hypothesis adaptation

The Runtime MUST provide a `HypothesisAdapter` that is a stateless static pure function mapping a
`RuntimeDecision` and an `ExecutionHypothesis` into exactly one `HypothesisAdaptation`. The adapter MUST
NOT observe the world, MUST NOT authorize an action, MUST NOT execute anything, MUST NOT recover, MUST
NOT modify the Goal or completion, and MUST NOT contain scenario-specific knowledge. It maps a decision
to a bounded hypothesis update; it does not perform the update's execution consequences.

#### Scenario: adapter is deterministic and world-free
- **WHEN** the same (decision, hypothesis) inputs are adapted twice
- **THEN** both adaptations produce structurally identical results
- **AND** neither adaptation performs an observation or dispatches an action

#### Scenario: adapter uses no scenario strings
- **WHEN** an adaptation produces an adaptation reason or adapted hypothesis objective
- **THEN** the text is derived from the decision reason and generic boundary/authority language
- **AND** it contains no application-specific or settings-specific string

### Requirement: Keep adaptation

The adapter MUST produce a Keep adaptation when the decision state is Continue. The adapted hypothesis
MUST be the current hypothesis with Status Confirmed (if not already confirmed). The adaptation MUST NOT
create a new assumption, execute an action, or modify the Goal.

#### Scenario: Continue decision produces Keep
- **WHEN** the decision state is Continue and the hypothesis status is Active
- **THEN** the adapter produces a Keep adaptation
- **AND** the adapted hypothesis is the current hypothesis with Status Confirmed
- **AND** no action is executed and no new assumption is created

### Requirement: Replace adaptation

The adapter MUST produce a Replace adaptation when the decision state is Revise. The current hypothesis
MUST be marked Replaced, and a new hypothesis MUST be created with a boundary-aware objective derived
from the decision's evidence reference (generic language, NOT a scenario string). The new hypothesis
Status MUST be Created. The adaptation MUST NOT execute a SystemBack, a DeviceAction, a Tap, or any
traversal action — the existing ExternalBoundary capability inside the DFS loop remains solely
responsible for boundary handling.

#### Scenario: Revise decision produces Replace without execution
- **WHEN** the decision state is Revise and the hypothesis expected a recursive child
- **THEN** the adapter produces a Replace adaptation
- **AND** the current hypothesis is marked Replaced
- **AND** a new hypothesis is created with Status Created and a generic boundary-aware objective
- **AND** no SystemBack, DeviceAction, or Tap is executed or referenced

### Requirement: Escalate adaptation

The adapter MUST produce an Escalate adaptation when the decision state is Escalate. The adapted
hypothesis MUST be the current hypothesis with Status Revised and an escalation-marked revision reason
recording the inability. The adaptation MUST NOT recover, retry, dispatch an action, or automatically
continue — it records the authority boundary being exceeded.

#### Scenario: Escalate decision produces Escalate without recovery
- **WHEN** the decision state is Escalate
- **THEN** the adapter produces an Escalate adaptation
- **AND** the adapted hypothesis is the current hypothesis with Status Revised and an escalation reason
- **AND** no recovery, retry, or action dispatch is performed or referenced

### Requirement: No authority over execution

The HypothesisAdaptation and HypothesisAdapter MUST NOT acquire any decision, authorization, completion,
execution, recovery, or traversal authority. The adaptation MUST NOT be consulted by the Agent for
decisions, authorization, completion, or execution. The adapter MUST NOT call any Agent method, dispatch
a DeviceAction, create a container, or initiate a sub-run. The RuntimeAgent MUST remain the sole
run-level semantic authority; the Agent MUST remain the sole execution authority; the FSM MUST remain the
sole lifecycle owner; the Traversal MUST remain the sole action performer. The DFS engine MUST be
unchanged.

#### Scenario: adaptation cannot authorize actions
- **WHEN** the adaptation model and adapter are inspected
- **THEN** they expose no method that authorizes an action or produces authorization evidence
- **AND** the Agent's authorization path does not reference the adaptation

#### Scenario: adaptation cannot bypass the Agent
- **WHEN** a directive is run with a hypothesis ledger
- **THEN** the RunState is produced by the Agent's existing DFS engine, not by the adaptation or adapter
- **AND** the Agent does not consult the adaptation for any decision

#### Scenario: adaptation cannot modify completion
- **WHEN** the run completes
- **THEN** the GoalEvidence is evaluated by the existing evidence evaluator, not by the adaptation
- **AND** the adaptation type reflects the outcome but does not determine it

#### Scenario: adaptation cannot create traversal authority
- **WHEN** the adaptation model and adapter are inspected
- **THEN** they expose no method that dispatches a DeviceAction, creates a container, or initiates a
  sub-run
- **AND** Replace does not execute SystemBack and Escalate does not recover

### Requirement: Additive integration without DFS or FSM modification

The adaptation MUST integrate into the existing `ExecutionHypothesisLedger` and `DirectiveExecution`
entry additively. The ledger gains an `Adapt()` method and a `LatestAdaptation` property. The
`DirectiveExecution` entry calls `ledger.Adapt()` inside the existing ContinueWith (when the ledger is
non-null), after `Reconcile` (Phase 3). When the ledger is absent (null), the existing Phase 1-3
behavior MUST be preserved with zero regression. The DFS engine, the FSM (RunState), the
`IntentExecution` seam, `Agent.OpenWorld.cs`, `Agent.cs`, `Container/`, `Traversal/`, `Recovery/`,
`World/`, and `HypothesisReconciler.cs` MUST remain unchanged.

#### Scenario: absent ledger preserves existing behavior
- **WHEN** `DirectiveExecution.RunDirectiveAsync` is called without a hypothesis ledger
- **THEN** it behaves exactly as the Phase 1-3 implementation
- **AND** no adaptation is created or recorded

#### Scenario: DFS engine and FSM are not modified
- **WHEN** the change is implemented
- **THEN** `Agent.OpenWorld.cs`, `Agent.cs`, `Container/`, `Traversal/`, `Recovery/`, `World/`, and
  `HypothesisReconciler.cs` are byte-unchanged
- **AND** the DFS loop and RunState transitions do not reference the adaptation or adapter

### Requirement: Immutable history preservation

The ledger's `Adapt()` MUST append the adapted hypothesis to the immutable history without rewriting or
deleting prior entries. The full hypothesis sequence (initial → revised → replaced → adapted) MUST remain
observable via the ledger's History property.

#### Scenario: adaptation appends without rewriting history
- **WHEN** `Adapt()` is called after `Reconcile()`
- **THEN** the adapted hypothesis is appended to the history
- **AND** all prior hypotheses remain in the history unchanged

### Requirement: Existing capability regression

The capability MUST NOT change the behavior of the existing open-world execution, bounded candidate
safety, cross-page discovery, the SETTINGS-TREE-01 capstone, the Phase 1 directive capability, the Phase
2 execution hypothesis capability, or the Phase 3 runtime decision capability. The existing deterministic
suites for those capabilities MUST remain green.

#### Scenario: settings-tree capstone remains green
- **WHEN** the capability is implemented and the full Runtime test suite is run
- **THEN** the SETTINGS-TREE-01 capstone proofs (TREE-1..TREE-20) pass unchanged

#### Scenario: phase 1-3 tests remain green
- **WHEN** the capability is implemented and the full Runtime test suite is run
- **THEN** all Phase 1 directive, Phase 2 execution hypothesis, and Phase 3 runtime decision tests pass
  unchanged
