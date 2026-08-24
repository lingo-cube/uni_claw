## Purpose

Defines the minimum Agent ownership, lifecycle, result/evaluation, evidence, and trace semantics required for UniAgent and RuntimeAgent to collaborate without duplicating Runtime truth or opening reserved orchestration scope.

## Requirements

### Requirement: UniAgent and RuntimeAgent authority separation

The Agent concept model MUST define UniAgent as the supervisory, orchestration, and Primary-Goal-evaluation Agent and RuntimeAgent as the bounded specialist execution Agent. UniAgent MUST NOT directly operate devices or mutate RuntimeAgent belief, grounding, execution state, GoalEvidence, or terminal outcome. RuntimeAgent MUST NOT redefine the Primary Goal or claim final user satisfaction.

#### Scenario: supervisory strategy preserves Runtime authority

- **WHEN** UniAgent changes its supervisory strategy after reading a Runtime outcome
- **THEN** it issues a bounded Directive or records a supervisory Decision without directly changing RuntimeAgent-owned truth or physical state

### Requirement: Goal levels are distinct

The model MUST distinguish the Session-level Primary Goal, which UniAgent interprets and evaluates, from the bounded Execution Goal carried by a Directive and handled by RuntimeAgent. The existing Runtime `SemanticGoalInput` MUST be interpreted as an Execution Goal and MUST NOT automatically be treated as the complete Primary Goal.

#### Scenario: bounded execution goal does not replace primary goal

- **WHEN** UniAgent delegates one bounded Runtime execution target
- **THEN** the Primary Goal remains unchanged in Session context and is evaluated separately after the Runtime outcome

### Requirement: Directive, Plan, Action, Decision, and Trace remain distinct

The model MUST distinguish Directive, UniAgent Supervisory Plan, Runtime-local Plan, concrete Runtime Action, Agent Decision, and Trace. A Plan MUST remain a revisable hypothesis rather than truth. The current v1 Runtime Protocol MUST continue to carry a bounded Directive without adding a Plan, PlanRef, physical action, or mid-run redirection field.

#### Scenario: Runtime dynamically expands a directive

- **WHEN** RuntimeAgent receives a Directive to traverse a menu and process matching items
- **THEN** it may discover, skip, scroll, act, verify, and stop through Runtime-local Decisions without receiving a precompiled physical action list

#### Scenario: supervisory plan does not silently change the wire contract

- **WHEN** UniAgent maintains or revises an Abstract Plan
- **THEN** the Plan remains UniAgent-side unless a future separately authorized Protocol gate adds a bounded representation

### Requirement: Runtime Outcome and Goal Evaluation are layered

The model MUST distinguish Directive acceptance, RuntimeAgent-produced Runtime Outcome, and UniAgent-produced Goal Evaluation. RuntimeAgent MUST remain the sole owner of Run completion and failure from GoalEvidence. Goal Evaluation MUST express `Completion` and `Satisfaction` independently and MUST NOT rewrite the Runtime Outcome.

#### Scenario: completed runtime is unsatisfactory at the primary-goal level

- **WHEN** RuntimeAgent truthfully produces `Completed` but the resulting state violates a Primary-Goal quality constraint
- **THEN** UniAgent records `Completion = Completed` and `Satisfaction = Unsatisfied` while preserving the Runtime Outcome as `Completed`

#### Scenario: rejected directive creates no failed run

- **WHEN** RuntimeAgent rejects a Directive before creating a Run
- **THEN** the record remains a request rejection and MUST NOT be represented as a failed Runtime lifecycle

### Requirement: AssistanceRequired is not fabricated terminal completion

The model MUST preserve Protocol v1's distinction between non-terminal Escalation and TerminalOutcome. `AssistanceRequired` MUST be interpreted as a non-terminal supervisory disposition in v1 and MUST NOT be reported as `Completed` or `Failed` unless RuntimeAgent independently reaches that truthful terminal outcome.

#### Scenario: human adjudication remains non-terminal

- **WHEN** RuntimeAgent cannot close an uncertainty locally and requests supervisory or operator judgment
- **THEN** the request remains a non-terminal escalation, RuntimeAgent retains execution authority, and no terminal outcome is fabricated

### Requirement: Observation, Fact, Evidence, belief, and GoalEvidence remain distinct

Observation MUST remain Environment/perception-produced evidence rather than truth. Fact records MUST be append-oriented. WorldBelief and latest-fact summaries MUST be treated as revisable projections rather than mutable historical facts. GoalEvidence MUST remain a specialized RuntimeAgent-kernel completion basis and MUST NOT become a synonym for all Evidence.

#### Scenario: later fact does not mutate earlier history

- **WHEN** evidence first supports `wifi_enabled = false` and later supports `wifi_enabled = true`
- **THEN** both fact records remain available while the current projection may select the later supported assertion

#### Scenario: new observation revises belief

- **WHEN** a fresh Observation contradicts the current WorldBelief
- **THEN** RuntimeAgent reconciles a revised belief without rewriting the earlier Observation or Fact records

### Requirement: Trace domains are correlated without shared truth

The model MUST define Trace as a correlated reasoning or execution process, Span as a bounded local activity, and Event as an instantaneous occurrence. RuntimeAgent and UniAgent traces MAY reference the same Session, Directive, Run, Decision, Fact, or Evidence identities, but MUST NOT become one shared mutable truth object.

#### Scenario: evaluation trace links to runtime evidence

- **WHEN** UniAgent evaluates a Runtime outcome
- **THEN** its Decision/Evaluation trace may reference the Runtime run and evidence while preserving RuntimeAgent as the producer and owner of those records

### Requirement: Lifecycle continuity does not require permanent Agent objects

Session MUST remain the continuity and correlation root across conversational turns and Agent loop activations. Agent lifecycle MUST remain independent from Session lifecycle, and Agent instances MAY be destroyed after their bounded lifecycle. RuntimeAgent MAY be described as UniAgent's fixed specialist SubAgent, but this MUST NOT authorize generic multi-agent graphs, multiple Primary Runs, SubRuns, BranchRuns, or multi-run scheduling in v1.

#### Scenario: failed runtime is followed by a revised directive

- **WHEN** RuntimeAgent produces a truthful `Failed` outcome and UniAgent decides to revise its Directive
- **THEN** the failure record remains append-only, the Primary Goal remains unchanged, and UniAgent may record a Retry Decision/candidate Directive but MUST NOT dispatch it as a second Run unless a future gate authorizes another Run model

#### Scenario: agent instance ends while session continues

- **WHEN** a bounded UniAgent or RuntimeAgent lifecycle ends
- **THEN** its in-memory object may be disposed while Session context and correlated records continue to provide continuity
