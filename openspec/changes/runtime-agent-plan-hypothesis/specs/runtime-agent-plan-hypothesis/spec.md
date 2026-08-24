# Spec: runtime-agent-plan-hypothesis

> Spec-driven definition of the run-local, revisable execution hypothesis capability. Additive;
> reuses the existing DFS execution unchanged. Source baseline verified 2026-08-21 (uni-agent branch,
> build clean, 1506 deterministic tests green).

## Purpose

Lets the RuntimeAgent maintain an explicit, run-local execution hypothesis — a first-class record of
its current execution assumption and how observations confirmed or revised it — so the run's
assumptions are observable and lifecycle-tracked without granting the hypothesis any decision,
authorization, completion, or execution authority.

## ADDED Requirements

### Requirement: Execution hypothesis representation

The Runtime MUST provide an immutable `ExecutionHypothesis` record expressing one run-local
execution assumption: a run identity, a directive reference, a current objective, an expected
transition, an expected outcome, a confidence value, an optional revision reason, the observation
sequence at which it was created, and a lifecycle status. The record MUST NOT carry a `Plan`, element
coordinates, a `DeviceAction`, a `TraversalStep`, an element index, scenario strings, authorization
rules, or any completion authority.

#### Scenario: hypothesis carries only an execution assumption
- **WHEN** a hypothesis is constructed with RunId, DirectiveReference, Objective, ExpectedTransition,
  ExpectedOutcome, Confidence, CreatedAtObservation, and Status
- **THEN** it exposes exactly those fields
- **AND** it exposes no Plan, no coordinates, no DeviceAction, no element index, and no authorization
  rule

#### Scenario: hypothesis rejects invalid construction
- **WHEN** a hypothesis is constructed with a blank RunId or blank Objective
- **THEN** construction fails with an explicit validation error
- **AND** no hypothesis instance is created

#### Scenario: confidence is bounded
- **WHEN** a hypothesis is constructed with a confidence outside [0, 1]
- **THEN** construction fails with an explicit validation error

### Requirement: Hypothesis lifecycle

The Runtime MUST define an `ExecutionHypothesisStatus` lifecycle with exactly the states Created,
Active, Confirmed, Revised, and Replaced. A hypothesis begins as Created, becomes Active when
execution begins under it, becomes Confirmed when an observation matches its expected transition, or
becomes Revised when an observation contradicts its expectation (recording a revision reason), and a
revised hypothesis may be Replaced by a new hypothesis for the next execution phase.

#### Scenario: lifecycle states are exhaustive
- **WHEN** the status enum is inspected
- **THEN** it contains exactly Created, Active, Confirmed, Revised, and Replaced
- **AND** no other states exist

#### Scenario: a revised hypothesis records its reason
- **WHEN** a hypothesis is revised due to a contradicting observation
- **THEN** its status becomes Revised
- **AND** its RevisionReason is non-blank and describes the contradiction

### Requirement: Run-local hypothesis ledger

The Runtime MUST provide an `ExecutionHypothesisLedger` that is run-local: it is created for one run,
creates the initial hypothesis from a decomposed directive, revises the hypothesis sequence from
execution evidence, and is discarded when the run method returns. The ledger MUST NOT survive as
global memory, cross-run knowledge, or a navigation model. The ledger MUST NOT be Runtime state owned
by Agent, Container, Traversal, or Environment; it is a transient, method-local derivation.

#### Scenario: ledger creates an initial hypothesis from a directive
- **WHEN** a ledger is created from a decomposed directive and run identity
- **THEN** it produces an initial hypothesis with Status Created
- **AND** the hypothesis objective and expected transition are derived from the directive's declared
  scope and completion requirement
- **AND** the hypothesis carries no scenario-specific knowledge

#### Scenario: ledger revises hypothesis from execution evidence
- **WHEN** the ledger is given the Agent's trace evidence and the run outcome
- **THEN** it produces a revised hypothesis sequence mapping trace inflection points to lifecycle
  transitions
- **AND** each revision records a non-blank RevisionReason derived from the trace evidence

#### Scenario: ledger is run-local and discarded
- **WHEN** the run method returns
- **THEN** the ledger is not retained in any Agent, Container, Traversal, or Environment field
- **AND** no hypothesis from the run survives as global or cross-run state

### Requirement: No authority over execution

The hypothesis and ledger MUST NOT acquire any decision, authorization, completion, or execution
authority. The hypothesis MUST NOT be consulted by the Agent for decisions, authorization, completion,
or execution. The ledger MUST NOT call any Agent method that mutates run state, authorizes an action,
evaluates GoalEvidence, or dispatches a DeviceAction. The RuntimeAgent MUST remain the sole run-level
semantic and execution authority; the DFS engine MUST be unchanged.

#### Scenario: hypothesis cannot authorize actions
- **WHEN** the hypothesis model and ledger are inspected
- **THEN** they expose no method that authorizes an action or produces authorization evidence
- **AND** the Agent's authorization path does not reference the hypothesis

#### Scenario: hypothesis cannot bypass the Agent
- **WHEN** a directive is run with a hypothesis ledger
- **THEN** the RunState is produced by the Agent's existing DFS engine, not by the hypothesis or ledger
- **AND** the Agent does not consult the hypothesis for any decision

#### Scenario: hypothesis cannot modify completion
- **WHEN** the run completes
- **THEN** the GoalEvidence is evaluated by the existing evidence evaluator, not by the hypothesis
- **AND** the hypothesis status reflects the outcome but does not determine it

#### Scenario: hypothesis cannot create recursive authority
- **WHEN** the hypothesis and ledger are inspected
- **THEN** they expose no method that dispatches an action, creates a container, or initiates a
  sub-run
- **AND** they introduce no new authority ownership

### Requirement: Additive integration without DFS modification

The hypothesis ledger MUST integrate into the existing `DirectiveExecution` entry as an optional,
nullable parameter. When the parameter is absent (null), the existing Phase 1 behavior MUST be
preserved with zero regression. The DFS engine (`Agent.RunOpenWorldAsync`), the `IntentExecution`
seam, `Agent.OpenWorld.cs`, `Agent.cs`, `Container/`, `Traversal/`, `Recovery/`, and `World/` MUST
remain unchanged.

#### Scenario: absent ledger preserves existing behavior
- **WHEN** `DirectiveExecution.RunDirectiveAsync` is called without a hypothesis ledger
- **THEN** it behaves exactly as the Phase 1 implementation
- **AND** no hypothesis is created or recorded

#### Scenario: DFS engine is not modified
- **WHEN** the change is implemented
- **THEN** `Agent.OpenWorld.cs`, `Agent.cs`, `Container/`, `Traversal/`, `Recovery/`, and `World/`
  are byte-unchanged
- **AND** the DFS loop does not reference the hypothesis or ledger

### Requirement: Existing capability regression

The capability MUST NOT change the behavior of the existing open-world execution, bounded candidate
safety, cross-page discovery, the SETTINGS-TREE-01 capstone, or the Phase 1 directive capability.
The existing deterministic suites for those capabilities MUST remain green.

#### Scenario: settings-tree capstone remains green
- **WHEN** the capability is implemented and the full Runtime test suite is run
- **THEN** the SETTINGS-TREE-01 capstone proofs (TREE-1..TREE-20) pass unchanged

#### Scenario: phase 1 directive tests remain green
- **WHEN** the capability is implemented and the full Runtime test suite is run
- **THEN** all Phase 1 directive decomposition tests pass unchanged

#### Scenario: open-world suites remain green
- **WHEN** the capability is implemented and the full Runtime test suite is run
- **THEN** the SC-U2-MUS-001 and SC-OW-TD-001 open-world suites pass unchanged
