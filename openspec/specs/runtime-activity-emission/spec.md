# runtime-activity-emission Specification

## Purpose
TBD - created by archiving change runtime-observability-trace-foundation. Update Purpose after archive.

## Requirements

### Requirement: Stable Runtime activity source
The Runtime SHALL expose one BCL `ActivitySource` emission seam with a stable source name and schema version, SHALL NOT depend on Harness types, and SHALL NOT hold per-run trace recording state.

#### Scenario: Runtime executes without an observability listener
- **WHEN** a Runtime invocation executes with no listener subscribed to the stable source
- **THEN** the Runtime SHALL produce the same semantic actions, observations, results, GoalEvidence, and final state as the uninstrumented path

#### Scenario: Harness subscribes without Runtime dependency injection
- **WHEN** the Harness subscribes to the stable Runtime activity source for one run
- **THEN** Runtime components SHALL emit activities without receiving a recorder, store, Harness model, or callback dependency

### Requirement: Required instrumentation boundary coverage
The Runtime SHALL emit bounded activities for the active Agent execution, Container refresh, Traversal execution, Environment `ObserveAsync`, Environment `ExecuteAsync`, Runtime-invocation root, Recovery attempt, external capability invocation, Intent execution, Perception-stage, Startup bootstrap, and plan-step traversal boundaries. Activation SHALL remain behavior-neutral, fail-open, structural-outcome-only, and SHALL NOT change Agent, Container, Traversal, Environment, Recovery, Capability, Planning, Perception, or Harness ownership.

#### Scenario: Successful active path emits required boundaries
- **WHEN** an end-to-end run exercises Agent, Container, Traversal, observation, and action execution through instrumented production paths
- **THEN** the recorded activities SHALL contain spans for the exercised active boundaries and SHALL contain no requirement to expose private method spans

#### Scenario: Inactive boundary is not fabricated
- **WHEN** a run has no active Perception-stage, Startup, Recovery, external capability, multi-stage Intent, Runtime-invocation owner, or plan-step activity
- **THEN** the Runtime SHALL NOT fabricate spans for the unexercised boundaries merely to satisfy a fixed shape

### Requirement: Deferred instrumentation receipts
Runtime invocation SHALL remain a caller-owned root scope owned by the DriverHost run coordinator (never a Runtime component), Intent execution SHALL emit at the now-active open-world Intent execution seam, Recovery attempt SHALL emit at the now-active Recovery mechanism seam, and external capability invocation SHALL emit at the now-active Agent capability selection/execution seam. Activation SHALL NOT change Agent, Container, Traversal, Environment, Recovery, Capability, Planning, or Harness ownership.

#### Scenario: Foundation closes with deferred boundaries
- **WHEN** a run exercises no Runtime-invocation root, Intent, Recovery, or capability-invocation path
- **THEN** observability conformance SHALL accept the active structure without treating an absent unexercised span as a failure, and SHALL require the corresponding span when that boundary is exercised

### Requirement: Stable layer and component attribution
Every emitted activity SHALL carry one stable layer identifier and one stable component identifier. Layers SHALL be limited to `ORCHESTRATION`, `AGENT`, `STARTUP`, `WORLD`, `CONTAINER`, `TRAVERSAL`, `RECOVERY`, `ENVIRONMENT`, `CAPABILITY`, and `HARNESS`; component identifiers SHALL be explicit contract values and SHALL NOT be derived from CLR names or diagnostic strings. The contract component set SHALL extend to `runtime.invocation`, `agent.execution`, `intent.execution`, `container.refresh`, `traversal.execution`, `traversal.plan-step`, `environment.observe`, `environment.execute`, `recovery.attempt`, `capability.invocation`, `startup.bootstrap`, `perception.capture`, `perception.vision`, `perception.fusion`, `perception.canonicalize`, and `perception.admission`.

#### Scenario: Component implementation is renamed
- **WHEN** an internal CLR type or private method is renamed without changing an approved instrumentation boundary
- **THEN** the emitted layer and component identifiers SHALL remain unchanged

#### Scenario: Activity carries closed attribution
- **WHEN** an approved activity is recorded
- **THEN** its layer SHALL belong to the stable taxonomy and its component identifier SHALL be present, non-blank, and a member of the contract component set

### Requirement: Parent-child activity context
Runtime activities SHALL use the active BCL activity context so nested operations preserve causal parent-child relationships across asynchronous calls.

#### Scenario: Traversal invokes environment observation
- **WHEN** Traversal performs an asynchronous environment observation during one Agent execution
- **THEN** the environment observation activity SHALL be a descendant of that Traversal activity within the same caller-owned trace context

### Requirement: Explicit non-semantic operation outcome
Each closed Runtime activity SHALL record an explicit observability outcome of `SUCCEEDED`, `FAILED`, `CANCELLED`, or `UNKNOWN`, and that outcome SHALL NOT be used as semantic action success, traversal completion, recovery success, or Goal completion evidence.

#### Scenario: Instrumented operation throws
- **WHEN** an approved boundary exits by an exception
- **THEN** its activity SHALL close with `FAILED` while the original exception and Runtime failure behavior remain unchanged

#### Scenario: Instrumented operation is cancelled
- **WHEN** an approved boundary exits due to cancellation
- **THEN** its activity SHALL close with `CANCELLED` without converting cancellation into semantic success or failure

### Requirement: Listener failure isolation
Activity creation, annotation, event emission, and closure SHALL be fail-open for Runtime behavior; a listener or recorder failure MUST NOT alter dispatch, retry, observation, verification, recovery, GoalEvidence, or the Runtime result.

#### Scenario: Listener callback fails before action dispatch
- **WHEN** a subscribed listener throws while an activity is started or annotated before an authorized action
- **THEN** the authorized Runtime operation SHALL continue according to its existing semantics and SHALL NOT be suppressed, repeated, or replaced by observability behavior

#### Scenario: Listener callback fails during activity closure
- **WHEN** a listener fails while an activity is stopping
- **THEN** the Runtime SHALL return or propagate its original result independently of the listener failure

### Requirement: Caller-owned Runtime-invocation root scope
The DriverHost run coordinator SHALL open one `runtime.invocation` root activity per accepted run before scheduling Agent work, SHALL close it at the terminal execution path, and SHALL NOT require the Runtime to open or own a root scope.

#### Scenario: One accepted run opens exactly one root
- **WHEN** the coordinator accepts a run and schedules Agent execution
- **THEN** the run's recorded activities SHALL include one `ORCHESTRATION`/`runtime.invocation` root spanning the Agent work, and recorded child activities SHALL descend from it

#### Scenario: Run fails before schedule
- **WHEN** run acceptance fails before Agent execution is scheduled
- **THEN** the coordinator SHALL NOT fabricate a root activity for the rejected run

### Requirement: Recovery attempt emission at the mechanism seam
The Recovery mechanism component SHALL emit a `recovery.attempt` activity around each recovery action dispatch it performs, with a structural outcome only. The recovery decision (when to recover, where to, when to resume) SHALL remain Agent-owned and SHALL NOT be derived from the span outcome.

#### Scenario: Recovery dispatches a recipe action
- **WHEN** Recovery dispatches one recipe action through the Environment port
- **THEN** the emitted activity SHALL carry the `RECOVERY` layer and `recovery.attempt` component, and its outcome SHALL reflect dispatch/mechanism closure only, never recovery success

#### Scenario: No active recovery path in a run
- **WHEN** a run never enters recovery
- **THEN** the run SHALL NOT contain a fabricated `recovery.attempt` span and conformance SHALL accept its absence

### Requirement: Capability invocation emission at the Agent seam
The Agent SHALL emit a `capability.invocation` activity at the boundary where it selects and executes a semantic/external capability, keeping the selection decision Agent-owned and the span outcome structural.

#### Scenario: Agent selects and executes a capability
- **WHEN** the Agent selects an admissible capability for the current goal and executes it
- **THEN** the recorded activity SHALL carry the `CAPABILITY` layer and `capability.invocation` component with a structural outcome

#### Scenario: Capability is not exercisable
- **WHEN** no capability selection occurs during a run
- **THEN** the run SHALL NOT contain a fabricated `capability.invocation` span

### Requirement: Intent execution emission at the multi-stage seam
Intent execution SHALL emit an `intent.execution` activity around an open-world Intent execution path (the multi-stage intent trigger identified by the foundation deferral is present), with outcome limited to execution closure.

#### Scenario: Open-world intent executes
- **WHEN** an accepted Strategy/Intent reaches the open-world execution seam
- **THEN** the recorded activity SHALL carry the `AGENT` layer and `intent.execution` component and SHALL close with a structural outcome

#### Scenario: No multi-stage intent path in a run
- **WHEN** a run contains no open-world Intent execution
- **THEN** the run SHALL NOT fabricate an `intent.execution` span

### Requirement: Structured point-event emission
The observability emission seam SHALL support emitting point events with structured attributes (`decision.*` vocabulary for decision events; existing attribute keys unchanged), and recorded `ObservabilityEvent` values SHALL preserve those attributes and their real monotonic offsets within the containing span.

#### Scenario: Decision event carries a reason
- **WHEN** an approved boundary emits a decision event with a `decision.reason` attribute
- **THEN** the projected `ObservabilityEvent` SHALL contain the attribute and SHALL NOT rewrite or drop it

#### Scenario: Event emission API remains behavior-neutral
- **WHEN** a listener is absent or fails during event emission
- **THEN** the Runtime operation SHALL continue unchanged (fail-open, unchanged from the foundation)

### Requirement: Run-scoped recorder capture
The per-run recorder SHALL capture only activities belonging to its run's W3C trace id (derived from the first recorded activity when the caller did not supply a trace id), SHALL skip activities of foreign traces with a Harness diagnostic, and SHALL retain the caller-supplied `TraceRun.TraceId` when one is provided.

#### Scenario: Concurrent runs on separate devices
- **WHEN** two agreed runs record concurrently on distinct trace ids
- **THEN** each recorder's finalized `TraceRun` SHALL contain only its own run's spans and SHALL report the foreign-trace skips in its Diagnostics

#### Scenario: Caller supplied a correlation id
- **WHEN** the caller provides a `TraceRun.TraceId` correlation value at recorder creation
- **THEN** the finalized `TraceRun` SHALL preserve that value while capture isolation still follows the run's W3C trace id

#### Scenario: Run emits spans across async boundaries
- **WHEN** one run's activities span asynchronous continuations and complete out of order
- **THEN** capture SHALL remain complete for that run's trace id and hierarchy SHALL be derived from identifiers (unchanged from the foundation)

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
