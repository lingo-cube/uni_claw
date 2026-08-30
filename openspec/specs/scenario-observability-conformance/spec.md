# scenario-observability-conformance Specification

## Purpose
TBD - created by archiving change runtime-observability-trace-foundation. Update Purpose after archive.

## Requirements

### Requirement: Scenario span-existence assertions
Harness scenario utilities SHALL allow a Scenario to require the existence and closure of an approved stable span without coupling the Scenario to a CLR type, private method, or incidental nested implementation. The set of required boundary spans SHALL follow the exercised active boundaries: an accepted run over an instrumented path that exercises Agent execution, Container refresh, Traversal execution, Runtime invocation, Recovery attempt, capability invocation, Intent execution, Perception stages, Startup bootstrap, or plan-step traversal SHALL be capable of requiring those spans, while boundaries the run never exercised SHALL remain assertable as absent (never fabricated).

#### Scenario: Scenario requires environment execution evidence
- **WHEN** a Scenario declares that an action must cross the Environment execution boundary
- **THEN** its conformance assertion SHALL be able to require a closed `environment.execute` span under the expected stable ancestor

#### Scenario: Exercised active boundaries are enforced
- **WHEN** an end-to-end run exercises Agent execution and Container refresh over an instrumented path
- **THEN** scenario conformance SHALL fail when either exercised boundary span is absent, and SHALL accept the run when both are present with valid attribution

#### Scenario: Exercised Perception stage is enforced
- **WHEN** a real-device observation exercised the Perception cap stage and Vision stage
- **THEN** scenario conformance SHALL be able to require `perception.capture` and `perception.vision` spans under the enclosing `environment.observe` span

#### Scenario: Unexercised boundary is not fabricated
- **WHEN** a run never enters Recovery, capability selection, Intent execution, a root-scoped invocation, Perception, Startup, or plan-step traversal
- **THEN** conformance SHALL accept the run without any fabricated span for the unexercised boundary (the anti-fabrication principle remains)

### Requirement: Layer and component closure assertions
Harness scenario utilities SHALL validate that every asserted span has a recognized stable layer, a non-blank stable component identifier, a valid parent when non-root, and a closed lifetime with an explicit outcome.

#### Scenario: Span uses unknown layer
- **WHEN** a captured Scenario trace contains an asserted span with a layer outside the approved taxonomy
- **THEN** layer/component closure validation SHALL fail with structured validation evidence

#### Scenario: Required ancestor is unclosed
- **WHEN** a required child span is present but one required ancestor has no valid closure
- **THEN** closure validation SHALL fail rather than treating span existence as sufficient

### Requirement: Required stable event assertions
Harness scenario utilities SHALL allow a Scenario to assert the presence of stable event IDs and required structured fields within an expected span while forbidding diagnostic message text as a contract. Events SHALL carry their own monotonic offsets and attributes.

#### Scenario: Required event is absent
- **WHEN** a Scenario requires an approved stable event in a traversal span and the event is absent
- **THEN** observability conformance SHALL fail even if the traversal span exists

#### Scenario: Diagnostic wording changes
- **WHEN** free-form diagnostic wording changes but stable event ID, structured fields, hierarchy, and outcome remain equivalent
- **THEN** the Scenario observability assertion SHALL remain satisfied

### Requirement: Failure-boundary assertions
Harness scenario utilities SHALL allow a Scenario to validate explicit span outcomes and the recorded parent boundary without using observability outcome as semantic completion evidence.

#### Scenario: Environment execution fails
- **WHEN** Environment `ExecuteAsync` fails during a traced Scenario
- **THEN** conformance SHALL be able to validate `FAILED` at `environment.execute` and recorded parent closure independently of the Runtime's semantic result

#### Scenario: Listener fails during a failing Runtime operation
- **WHEN** both a Runtime operation and the observability listener fail
- **THEN** Scenario evidence SHALL preserve the original Runtime failure boundary when available and SHALL report listener loss separately without fabricating semantic success

### Requirement: Incidental assertions are forbidden
Scenario observability conformance SHALL NOT support acceptance assertions over exact duration values, callback order, private method order, CLR implementation names, or free-form diagnostic strings.

#### Scenario: Test attempts exact duration equality
- **WHEN** a Scenario test attempts to make an exact elapsed duration part of acceptance
- **THEN** the observability assertion surface SHALL reject or omit that assertion in favor of non-negative monotonic closure

#### Scenario: Test attempts private method order assertion
- **WHEN** a Scenario test attempts to assert the order of private Runtime methods
- **THEN** the observability assertion surface SHALL not expose that order as a supported contract

### Requirement: Scenario observability remains non-authoritative
Observability assertions SHALL validate recorded evidence after or alongside a run but SHALL NOT authorize actions, choose retries, interpret world truth, decide recovery, or produce Goal completion.

#### Scenario: Required observability span is missing
- **WHEN** Runtime behavior completes but an observability-required Scenario is missing a span
- **THEN** the Harness SHALL fail observability conformance separately and SHALL NOT retroactively change the Runtime result or execute compensating work
