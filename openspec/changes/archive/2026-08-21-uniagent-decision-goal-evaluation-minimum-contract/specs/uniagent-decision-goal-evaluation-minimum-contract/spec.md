## Purpose

Defines the minimum producer, correlation, evidence-reference, append-only, supersession, and authority semantics required to record UniAgent Decisions and Goal Evaluations without creating a second Runtime truth.

## ADDED Requirements

### Requirement: UniAgent Decision is a producer-authored judgment record

A UniAgent Decision MUST represent one evidence-grounded supervisory judgment in Session context. It MUST identify its producer, Decision identity, Session/Primary-Goal correlation, disposition, and basis references. It MUST NOT be represented as a Runtime Action, Runtime Outcome, GoalEvidence, or direct Runtime state mutation.

#### Scenario: failed runtime leads to revised directive decision

- **WHEN** UniAgent reads a truthful Runtime `Failed` outcome and decides to revise the next Directive
- **THEN** it appends a new Decision referencing the failure/evidence basis while preserving the original Runtime outcome and Primary Goal

### Requirement: Decision disposition has a bounded minimum vocabulary

The minimum contract MUST express `Continue`, `ReviseDirective`, `Retry`, `RequestOperator`, `AcceptRuntimeOutcome`, and `Terminate` supervisory dispositions. A disposition MUST describe the UniAgent judgment and MUST NOT encode coordinates, physical actions, Runtime belief mutations, or a precompiled Runtime Plan.

#### Scenario: retry disposition carries no physical authority

- **WHEN** UniAgent records a `Retry` or `ReviseDirective` Decision
- **THEN** the record may reference a candidate bounded Directive but contains no physical action, does not overwrite RunState, WorldBelief, or GoalEvidence, and does not authorize post-terminal dispatch or a second Run

### Requirement: Goal Evaluation has independent completion and satisfaction dimensions

A Goal Evaluation MUST correlate to one Session-level Primary Goal and its relevant Runtime Outcome/evidence basis. It MUST express `Completion` as `Completed`, `Incomplete`, or `Indeterminate` and `Satisfaction` as `Satisfied`, `Unsatisfied`, or `Indeterminate`. It MUST NOT collapse the two dimensions into one boolean or copy them into Runtime RunState.

#### Scenario: completed runtime is unsatisfactory

- **WHEN** the Runtime Outcome is `Completed` but the result violates a Primary-Goal expectation or quality constraint
- **THEN** Goal Evaluation records `Completion = Completed` and `Satisfaction = Unsatisfied` without changing the Runtime Outcome

#### Scenario: evidence is insufficient for evaluation

- **WHEN** UniAgent lacks sufficient evidence to judge completion or satisfaction
- **THEN** the affected dimension is `Indeterminate` rather than a fabricated positive or negative value

### Requirement: Runtime truth remains producer-owned and referenced

Decision and Goal Evaluation records MUST reference Runtime Outcomes, Observations, Facts, Evidence, or prior Decisions without copying or re-originating their truth. RunState, GoalEvidence, WorldBelief, physical execution, and terminal outcome MUST remain RuntimeAgent-owned.

#### Scenario: evaluation references runtime evidence

- **WHEN** UniAgent evaluates a completed or failed Run
- **THEN** it retains references to producer-owned Runtime records and does not create a second mutable Runtime snapshot or evidence store

### Requirement: Records and operator overrides are append-oriented

Decisions and Goal Evaluations MUST be append-oriented. An operator or later UniAgent evaluation MAY supersede the latest Goal Evaluation projection by appending a new producer-authored record with an explicit supersession reference, but MUST NOT mutate or delete the prior evaluation, Decision, Runtime Outcome, or Evidence.

#### Scenario: operator overrides satisfaction judgment

- **WHEN** an operator replaces UniAgent's current Satisfaction judgment
- **THEN** a new operator-authored Goal Evaluation references the superseded evaluation and the latest projection advances without changing either historical record

### Requirement: AssistanceRequired remains distinct from terminal result and evaluation

`AssistanceRequired` MUST remain a non-terminal Escalation disposition. It MUST NOT be encoded as `Completed`, `Failed`, Goal Completion, or Goal Satisfaction. An operator response MUST be recorded as its own Decision or Evaluation input, while RuntimeAgent retains final terminal-outcome authority.

#### Scenario: operator adjudication does not fabricate completion

- **WHEN** RuntimeAgent requires supervisory/operator judgment before it can continue
- **THEN** the escalation and operator Decision are recorded without fabricating a Runtime terminal outcome or Goal Evaluation

### Requirement: Contract is transport- and storage-independent

The minimum contract MUST define semantic obligations without selecting a DTO namespace, database schema, event store, DSH UI, wire method, model provider, or persistence mechanism. Session remains a correlation root rather than a message bus or mutable state store.

#### Scenario: host representation changes without semantic change

- **WHEN** a future authorized host represents Decision and Goal Evaluation records using a different storage or transport technology
- **THEN** producer ownership, append-only history, correlation, evidence references, supersession, and authority semantics remain unchanged
