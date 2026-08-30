## MODIFIED Requirements

### Requirement: Required instrumentation boundary coverage
The Runtime SHALL emit bounded activities for the active Agent execution, Container refresh, Traversal execution, Environment `ObserveAsync`, and Environment `ExecuteAsync` boundaries, and the activated Runtime-invocation root, Recovery attempt, external capability invocation, and Intent execution boundaries. Activation SHALL remain behavior-neutral, fail-open, structural-outcome-only, and SHALL NOT change Agent, Container, Traversal, Environment, Recovery, Capability, Planning, or Harness ownership.

#### Scenario: Successful active path emits required boundaries
- **WHEN** an end-to-end run exercises Agent, Container, Traversal, observation, and action execution through instrumented production paths
- **THEN** the recorded activities SHALL contain spans for the exercised active boundaries and SHALL contain no requirement to expose private method spans

#### Scenario: Inactive boundary is not fabricated
- **WHEN** a run has no active Recovery, external capability, multi-stage Intent, or Runtime-invocation owner activity
- **THEN** the Runtime SHALL NOT fabricate spans for the unexercised boundaries merely to satisfy a fixed shape

### Requirement: Deferred instrumentation receipts
Runtime invocation SHALL remain a caller-owned root scope owned by the DriverHost run coordinator (never a Runtime component), Intent execution SHALL emit at the now-active open-world Intent execution seam, Recovery attempt SHALL emit at the now-active Recovery mechanism seam, and external capability invocation SHALL emit at the now-active Agent capability selection/execution seam. Activation SHALL NOT change Agent, Container, Traversal, Environment, Recovery, Capability, Planning, or Harness ownership.

#### Scenario: Foundation closes with deferred boundaries
- **WHEN** a run exercises no Runtime-invocation root, Intent, Recovery, or capability-invocation path
- **THEN** observability conformance SHALL accept the active structure without treating an absent unexercised span as a failure, and SHALL require the corresponding span when that boundary is exercised

## ADDED Requirements

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