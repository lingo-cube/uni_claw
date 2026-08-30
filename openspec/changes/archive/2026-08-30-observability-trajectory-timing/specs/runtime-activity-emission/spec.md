## MODIFIED Requirements

### Requirement: Required instrumentation boundary coverage
The Runtime SHALL emit bounded activities for the active Agent execution, Container refresh, Traversal execution, Environment `ObserveAsync`, Environment `ExecuteAsync`, Runtime-invocation root, Recovery attempt, external capability invocation, Intent execution, Perception-stage, Startup bootstrap, and plan-step traversal boundaries. Activation SHALL remain behavior-neutral, fail-open, structural-outcome-only, and SHALL NOT change Agent, Container, Traversal, Environment, Recovery, Capability, Planning, Perception, or Harness ownership.

#### Scenario: Successful active path emits required boundaries
- **WHEN** an end-to-end run exercises Agent, Container, Traversal, observation, and action execution through instrumented production paths
- **THEN** the recorded activities SHALL contain spans for the exercised active boundaries and SHALL contain no requirement to expose private method spans

#### Scenario: Inactive boundary is not fabricated
- **WHEN** a run has no active Perception-stage, Startup, Recovery, external capability, multi-stage Intent, Runtime-invocation owner, or plan-step activity
- **THEN** the Runtime SHALL NOT fabricate spans for the unexercised boundaries merely to satisfy a fixed shape

### Requirement: Stable layer and component attribution
Every emitted activity SHALL carry one stable layer identifier and one stable component identifier. Layers SHALL be limited to `ORCHESTRATION`, `AGENT`, `STARTUP`, `WORLD`, `CONTAINER`, `TRAVERSAL`, `RECOVERY`, `ENVIRONMENT`, `CAPABILITY`, and `HARNESS`; component identifiers SHALL be explicit contract values and SHALL NOT be derived from CLR names or diagnostic strings. The contract component set SHALL extend to `runtime.invocation`, `agent.execution`, `intent.execution`, `container.refresh`, `traversal.execution`, `traversal.plan-step`, `environment.observe`, `environment.execute`, `recovery.attempt`, `capability.invocation`, `startup.bootstrap`, `perception.capture`, `perception.vision`, `perception.fusion`, `perception.canonicalize`, and `perception.admission`.

#### Scenario: Component implementation is renamed
- **WHEN** an internal CLR type or private method is renamed without changing an approved instrumentation boundary
- **THEN** the emitted layer and component identifiers SHALL remain unchanged

#### Scenario: Activity carries closed attribution
- **WHEN** an approved activity is recorded
- **THEN** its layer SHALL belong to the stable taxonomy and its component identifier SHALL be present, non-blank, and a member of the contract component set

## ADDED Requirements

### Requirement: Perception-stage boundary coverage
The Perception capability pipeline SHALL emit one bounded activity per stage — capture, Vision inference, Fusion, Canonicalization, and Semantic admission — with structural outcomes only, so the per-stage timing of a real-device observation is attributable without exposing private internals as semantics.

#### Scenario: Real-device observation passes through Perception
- **WHEN** an Environment observe produces a Perception frame through capture, Vision, Fusion, Canonicalization, and admission
- **THEN** the recorded activities SHALL include the exercised stage spans (`perception.capture`, `perception.vision`, `perception.fusion`, `perception.canonicalize`, `perception.admission`) under the enclosing `environment.observe` span

#### Scenario: Perception stage fails fail-open
- **WHEN** a Perception stage exits by exception or empty evidence
- **THEN** the stage activity SHALL close with `FAILED` (or the truthful structural outcome) while the Runtime observation and fail-closed admission behavior remain unchanged

#### Scenario: Perception is not exercised
- **WHEN** a run never reaches the Perception pipeline
- **THEN** the run SHALL NOT contain fabricated Perception-stage spans

### Requirement: Startup bootstrap boundary
The Startup bootstrap SHALL emit one `startup.bootstrap` activity (`STARTUP` layer) around `Startup.StartAsync`, with a structural outcome, so the attach→launch→observe→resolve→initial-container sequence is timed.

#### Scenario: Run starts through Startup
- **WHEN** a run executes Startup bootstrap
- **THEN** the recorded activity SHALL carry the `STARTUP` layer and `startup.bootstrap` component with a structural outcome

### Requirement: Plan-step traversal boundary
The deterministic plan-step execution path (`ExecuteStepCoreAsync`) SHALL emit a `traversal.plan-step` activity per executed plan step so the PlanRun traversal path is timed like the semantic dispatch path, with step-id and outcome as attributes, never as semantic authority.

#### Scenario: Plan step executes
- **WHEN** a deterministic plan step is executed through the plan-step path
- **THEN** the recorded activity SHALL carry the `TRAVERSAL` layer and `traversal.plan-step` component and SHALL close with a structural outcome

#### Scenario: Plan path is not exercised
- **WHEN** a run never executes deterministic plan steps
- **THEN** the run SHALL NOT contain a fabricated `traversal.plan-step` span

### Requirement: Iteration and settle granularity events
The Agent loop and Traversal settle SHALL emit structured point events (`decision.iteration`, `decision.duration_ns`, `settle.round`, `settle.duration_ns`) on their carrying spans per iteration / per settle round, reusing the structured event capability; event count SHALL be bounded per run.

#### Scenario: Multi-iteration semantic loop
- **WHEN** a semantic run iterates more than once
- **THEN** the Agent span SHALL carry one `iteration.start` event per iteration with `decision.iteration` and `decision.duration_ns` attributes

#### Scenario: Post-action settle occurs
- **WHEN** post-action state settle performs one or more re-observation rounds
- **THEN** the `LoweredAction` span SHALL carry one `settle.round` event per round with `settle.duration_ns` attributes

### Requirement: Decision events gain timing anchors
Navigation, viewport-exploration, and Trap decision points SHALL emit `decision.*` events (`decision.navigation`, `decision.viewport`, `decision.trap`) with `decision.reason` attributes on their carrying spans, so the decision trajectory is anchored in time when a span exists.

#### Scenario: Navigation decision recorded
- **WHEN** the Agent records a navigation decision while an Agent or Traversal span is active
- **THEN** the active span SHALL carry the navigation decision event with its `decision.reason`

#### Scenario: No active span at decision time
- **WHEN** a decision occurs outside any active approved span
- **THEN** the decision SHALL still be recorded on the semantic journal (DecisionRecord) and SHALL NOT be fabricated onto a span