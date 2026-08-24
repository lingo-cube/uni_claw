# Spec: dsh-runtime-agent-subagent-run-entry

## Purpose

Define the authorized asynchronous run-start entry for starting an existing Runtime.Agent semantic run as a deterministic execution subagent.

## Requirements

### Requirement: Authorized asynchronous run start

The DriverHost MUST add one additive `run.start` wire method that accepts a
`RunStartRequest`, validates it deterministically, creates a run, starts the existing
`Runtime.Agent` semantic entry asynchronously, and returns `RunAccepted(runId)`
immediately — never blocking until run completion.

#### Scenario: run.start returns immediately with an authoritative runId

Given a valid `RunStartRequest` against a known device,
When `run.start` is invoked,
Then it returns `{ accepted: true, runId, runState }` before the semantic loop
completes,
And the runId is created by the DriverHost (never invented by DSH).

#### Scenario: run.start never blocks on execution

Given an accepted run,
When the semantic loop is still executing,
Then `run.start` has already returned the runId (the run executes in the background).

### Requirement: DriverHost-owned run identity

The DriverHost MUST own creation of the authoritative runId and MUST pass it as the
run identity of the existing `Agent.RunSemanticGoalAsync` entry.

#### Scenario: DSH cannot supply the runId

Given a `run.start` request,
When the request is processed,
Then the runId originates from the DriverHost coordinator,
And the same runId is immediately usable with `run.events.after`, `run.snapshot.get`,
`run.trap.get`, `run.events.drain`, and `evidence.get`.

### Requirement: Existing Runtime.Agent semantic entry reuse

The run entry MUST adapt the request into the existing production semantic entry
`Agent.RunSemanticGoalAsync(SemanticGoalInput, ImmutableArray<SemanticObject>,
ImmutableArray<Capability>, runId, …)`. The request MUST NOT supply
`DeviceAction`, coordinates, `ElementIndex`, `TraversalStep`, or any precompiled
physical action sequence.

#### Scenario: goal adapts to SemanticGoalInput

Given a request `goal { objectIdentity, stateDimension, desiredValue }`,
When the run is started,
Then the Agent receives exactly the corresponding `SemanticGoalInput`.

#### Scenario: no physical authority crosses the boundary

Given any `run.start` request,
When the request is validated,
Then it is rejected if it contains `DeviceAction`, coordinates, `ElementIndex`, or
a precompiled action sequence.

### Requirement: Existing observability reuse

Observation of an accepted run MUST reuse the already-graduated read-only surfaces
(`run.events.after`, `run.snapshot.get`, `run.trap.get`, `run.events.drain`,
`evidence.get`) keyed by the returned runId. No second event/result protocol MAY be
created.

#### Scenario: accepted run is observable through existing surfaces

Given an accepted run and its runId,
When the outer agent reads `run.snapshot.get` and `run.events.after`,
Then it observes Kernel-derived state and RuntimeEvents for that runId.

#### Scenario: no duplicate result transport

Given the accepted run,
When its lifecycle is observed,
Then no new event kind, event stream, or result channel beyond the existing
surfaces is introduced.

### Requirement: Authority preservation

The frozen authority boundaries MUST be preserved: DSH is the control/cognitive
host; `Runtime.Agent` keeps semantic decision authority; the Kernel keeps
authorization/execution/verification; the Environment is the external world
boundary. DSH MUST NOT gain direct physical authority, direct GoalEvidence
authority, direct binding authority, or direct state-belief authority.

#### Scenario: DSH cannot bypass the Kernel

Given an accepted run,
When the run executes,
Then all decisions, authorizations, bindings, state beliefs, and GoalEvidence are
produced by the Kernel, not by DSH.

#### Scenario: Agent has no DSH dependency

Given the runtime graph construction,
When the Agent is built,
Then it receives only its existing injected dependencies (`IEnvironment` + criteria)
and carries no DSH/Cordis reference.

### Requirement: Deterministic start rejection

`run.start` MUST distinguish `REQUEST_REJECTED` (invalid goal, unknown device,
device busy — no run created) from `RUN_ACCEPTED_THEN_FAILED` (runId exists; the
Kernel later reports failure through existing surfaces).

#### Scenario: invalid request is rejected without a fake run

Given a request with an unknown device selector, an invalid goal, or a busy device,
When `run.start` is invoked,
Then it returns a typed rejection (`request_rejected`) with a deterministic reason,
And no runId, observability entry, or execution is created.

#### Scenario: accepted run failure is observable

Given an accepted run that later fails in the Kernel,
When its snapshot and events are read,
Then the runId exists and reports Kernel `Failed` state / `RunFailed` events.

### Requirement: Device selection and composition boundary

Device selection MUST be an explicit composition-root mapping. The first slice MUST
support only the current Android path. No reflection discovery, MEF, dynamic
provider registry, or arbitrary assembly loading MAY be introduced.

#### Scenario: known device selector resolves the current Android composition

Given a device selector for the current Android path (e.g. `serial:<serial>`),
When the run is started,
Then the composition root resolves the existing `IEnvironment` + criteria wiring
(the same composition `PhysicalHostComposition` builds today),
And the Agent receives only `IEnvironment`.

#### Scenario: unknown device selector is rejected

Given a device selector with no composition mapping,
When `run.start` is invoked,
Then it is rejected with `request_rejected` and no run is created.

#### Scenario: contract allows a later second adapter without redesign

Given the device factory boundary,
When a second device kind is later added,
Then it requires only a new explicit composition mapping, without changing the
`run.start` request shape or the Agent contract.

### Requirement: DSH disconnect and process isolation behavior

A DSH transport failure or DSH/plugin restart MUST NOT fabricate or reset Kernel
truth. The DriverHost owns its own process; the plugin CONNECTS and MUST NOT
launch, supervise, or restart it.

#### Scenario: plugin disconnect does not corrupt the run

Given a plugin that loses its connection after receiving a runId,
When the run is still executing,
Then the Kernel run continues independently,
And a reconnecting DSH rediscovers it through `run.list` / `run.snapshot.get`.

#### Scenario: DriverHost process is plugin-independent

Given the DriverHost process lifecycle,
When the plugin activates or disposes,
Then it neither launches, supervises, nor restarts the DriverHost process.

### Requirement: Zero-model control command

The DSH command `uniclaw-run-goal` and the DriverHost `run.start` handler MUST be
deterministic control infrastructure requiring zero model calls.

#### Scenario: run start performs no model call

Given an invocation of `uniclaw-run-goal` / `run.start`,
When the control path executes,
Then it makes zero LLM/VLM calls.

#### Scenario: command returns only the runId

Given a valid invocation of `uniclaw-run-goal`,
When the handler runs,
Then it validates input, calls `run.start`, and returns the runId + runState,
And it does not poll, run shadow cognition, issue semantic actions, or translate to
ADB.

### Requirement: Backward compatibility with existing read-only wire methods

The eight frozen read-only methods (`ping`, `run.list`, `run.snapshot.get`,
`run.trap.get`, `run.events.after`, `run.events.drain`, `evidence.get`,
`control.support`) MUST retain their exact semantics, DTOs, and error codes.
`run.start` MUST be additive.

#### Scenario: existing read-only methods are unchanged

Given the DriverHost dispatch table,
When the frozen read-only methods are invoked,
Then they behave exactly as before this change (same method names, parameters,
DTOs, and error codes).

#### Scenario: control.support audit stays truthful

Given `control.support("start")` after this change,
When the audit is read,
Then it reflects the new authorized start entry (the `pause`/`resume`/`stop`/`abort`
rows remain deferred).

### Requirement: Same-device concurrency policy

The DriverHost/composition control layer MUST enforce the explicit first-slice
policy `ONE_ACTIVE_RUN_PER_DEVICE`. Device locking MUST NOT be placed inside
`Agent`. Distinct devices MAY run concurrently.

#### Scenario: second run on the same device is rejected

Given one active accepted run on device D,
When a second `run.start` for device D arrives,
Then it is rejected with `request_rejected` (device busy),
And no second execution starts on device D.

#### Scenario: distinct devices run concurrently

Given active runs on different device selectors,
When each is started,
Then they are accepted independently without identity or state aliasing.
