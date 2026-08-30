## Purpose

Defines a generic, bounded UniAgent-to-RuntimeAgent strategy input that can be interpreted and adapted against runtime reality without transferring user-level planning, action, lifecycle, or completion authority.

## ADDED Requirements

### Requirement: UniAgent-authored bounded strategy

The system SHALL accept a `StrategyDirective` only as a UniAgent-authored, start-time declaration containing an objective, scope, exploration intent, constraints, completion criteria, and allowed adaptation boundary. The declaration MUST be bounded, typed, and free of concrete device actions or routes.

#### Scenario: UniAgent submits an abstract exploration strategy

- **WHEN** UniAgent submits a typed strategy to explore a declared semantic scope exhaustively
- **THEN** RuntimeAgent receives that strategy as an immutable external execution contract rather than as a concrete action plan

#### Scenario: Concrete route is not a strategy

- **WHEN** a submitted strategy contains a sequence of taps, coordinates, gestures, or precompiled traversal steps
- **THEN** RuntimeAgent rejects the request as outside the Strategy Contract

### Requirement: Strategy and Directive remain distinct

The system SHALL distinguish a `StrategyDirective` from both the existing four-field Directive and UniAgent's private Supervisory Plan. The existing Directive declares one bounded execution request without an abstract exploration approach; the StrategyDirective additionally declares the bounded approach, constraints, completion semantics, and adaptation permissions. Neither message transfers the Supervisory Plan or user-level planning authority to RuntimeAgent.

#### Scenario: Existing Directive remains valid

- **WHEN** a client submits the existing `run.start` request
- **THEN** the request retains its current semantics and does not require a StrategyDirective

#### Scenario: Supervisory Plan remains private

- **WHEN** UniAgent derives a bounded StrategyDirective from a larger user-level plan
- **THEN** only the bounded StrategyDirective crosses Surface A and the larger Supervisory Plan remains UniAgent-owned

### Requirement: Start-time admission and deterministic rejection

RuntimeAgent SHALL validate a StrategyDirective before creating its Run execution context. It MUST reject malformed, unbounded, contradictory, unsupported, or unverifiable strategy semantics deterministically and MUST NOT guess missing meaning. Rejection MUST NOT start a fallback Run.

#### Scenario: Supported strategy is admitted

- **WHEN** all strategy fields are internally consistent and every required semantic criterion and completion condition is supported
- **THEN** RuntimeAgent accepts one bounded Run and establishes the immutable accepted strategy boundary for that Run

#### Scenario: Unsupported semantic criterion is rejected

- **WHEN** a strategy references a semantic criterion for which no compatible runtime capability binding is available
- **THEN** RuntimeAgent returns a deterministic unsupported-strategy rejection and creates no fallback execution

#### Scenario: User language is not interpreted by RuntimeAgent

- **WHEN** a request supplies unresolved user-language intent where a typed strategy field or semantic criterion reference is required
- **THEN** RuntimeAgent rejects the request rather than becoming the user-level planner

### Requirement: Runtime-local interpretation only

For an accepted strategy, RuntimeAgent SHALL interpret the immutable strategy into a runtime-local execution intent and SHALL use current WorldBelief to create or revise runtime-local execution hypotheses. Interpretation MUST stay within the declared objective, scope, constraints, completion criteria, and adaptation boundary. A runtime-local execution intent MUST NOT be a `DeviceAction` or constitute action authorization.

#### Scenario: Strategy becomes a runtime-local hypothesis

- **WHEN** an accepted exhaustive exploration strategy enters a world whose current semantic root has discoverable children
- **THEN** RuntimeAgent may form a hypothesis that the children require bounded discovery without prescribing or authorizing a concrete device action

#### Scenario: Reality differs from the current hypothesis

- **WHEN** fresh observation contradicts the current execution hypothesis
- **THEN** RuntimeAgent may reconcile and revise the hypothesis only through adaptation classes allowed by the accepted strategy

### Requirement: Immutable authority-bearing strategy fields

RuntimeAgent MUST NOT revise the accepted objective, scope, safety constraints, completion criteria, or forbidden-effects boundary. The allowed adaptation boundary MAY permit only runtime-local operations such as re-grounding semantic targets, reordering pending discovery, reconciling WorldBelief, or revising the execution hypothesis without expanding authority.

#### Scenario: Allowed runtime-local adaptation

- **WHEN** observation changes candidate ordering and the strategy permits pending-work reordering
- **THEN** RuntimeAgent may revise its hypothesis ordering while preserving every immutable strategy field

#### Scenario: Adaptation would expand scope

- **WHEN** a useful next candidate lies outside the accepted strategy scope
- **THEN** RuntimeAgent does not pursue it and produces a bounded revision or escalation result through an already-authorized seam

### Requirement: Generic semantic capability binding

Strategy interpretation SHALL resolve typed semantic criterion references through generic runtime capability bindings supplied by composition. Runtime core MUST NOT contain application-, screen-, or scenario-specific strategy knowledge, and wire messages MUST NOT carry executable predicates, callbacks, or code.

#### Scenario: Generic criterion binding is available

- **WHEN** a typed criterion reference and compatible version resolve to a configured generic semantic capability
- **THEN** RuntimeAgent may use the capability output as advisory input to runtime-local reconciliation

#### Scenario: Settings-specific knowledge is absent

- **WHEN** the Strategy Contract and Runtime core are inspected
- **THEN** they contain no Android Settings labels, routes, selectors, or other scenario-specific rules

### Requirement: Agent-owned execution and terminal lifecycle

Every concrete action derived downstream from a runtime-local execution intent MUST pass through the existing Agent authorization path. Agent SHALL remain owner of RunState, execution authorization, GoalEvidence evaluation, verification, and terminal outcome; FSM SHALL remain the sole lifecycle transition authority; Traversal SHALL remain the concrete execution owner.

#### Scenario: Runtime intent suggests executable work

- **WHEN** RuntimeAgent produces a bounded runtime-local execution intent from the accepted strategy and current belief
- **THEN** Agent independently decides whether execution is authorized before Traversal can perform any concrete action

#### Scenario: Strategy completion criterion appears satisfied

- **WHEN** RuntimeAgent reconciliation indicates that the strategy's completion criterion may be satisfied
- **THEN** the Run reaches `Completed` only if Agent-owned GoalEvidence verification and the existing FSM transition path authorize that terminal state

### Requirement: One StrategyDirective creates at most one Run

The Strategy Contract SHALL be start-time only: one accepted StrategyDirective creates at most one Agent Run. RuntimeAgent MUST NOT use the contract to start another Run, replace an active Run's strategy, continue after terminal state, or implement a RuntimeAgent-owned outer loop.

#### Scenario: Accepted strategy executes within one Run

- **WHEN** RuntimeAgent accepts a StrategyDirective
- **THEN** every observation, reconciliation, hypothesis revision, authorized action, verification, and terminal transition belongs to the same Agent-owned Run

#### Scenario: Run is terminal

- **WHEN** the Agent-owned Run reaches `Completed` or `Failed`
- **THEN** RuntimeAgent cannot use the Strategy Contract to continue execution or create a successor Run

### Requirement: Bounded escalation without authority transfer

When the accepted strategy cannot continue without changing an immutable boundary or using unsupported semantics, RuntimeAgent SHALL stop proposing in-bound execution and produce a bounded reason suitable for an authorized revision or escalation seam. The result MUST NOT itself revise the strategy, invoke UniAgent, transition FSM state, authorize an action, or determine completion.

#### Scenario: Strategy revision is required

- **WHEN** progress requires changing the objective, scope, safety constraints, or completion criteria
- **THEN** RuntimeAgent reports that revision is required and performs no authority-expanding fallback

#### Scenario: No escalation seam is currently authorized

- **WHEN** a bounded reason is produced but the current Run contract has no authorized non-terminal escalation transport
- **THEN** RuntimeAgent preserves the reason as an internal result and follows the existing Agent-owned failure or terminal path without inventing a new protocol loop

