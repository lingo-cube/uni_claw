## MODIFIED Requirements

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