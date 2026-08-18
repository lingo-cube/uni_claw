# Design: dsh-runtime-agent-subagent-run-entry

> Spec-driven design for the authorized async run-start entry. Source-verified
> baseline: this session inspected the pinned working tree (uni-agent branch,
> 2026-08-17). All referenced types are exact.

## 1. Verified source baseline

| Fact | Source |
|---|---|
| DriverHost dispatch table = 8 read-only methods (`ping`, `run.list`, `run.snapshot.get`, `run.trap.get`, `run.events.after`, `run.events.drain`, `evidence.get`, `control.support`) | `src/UniClaw.Runtime.DriverHost/Transport/UniClawDriverHostServer.cs` `Invoke` |
| Control surface has zero mutating operations | `src/UniClaw.Runtime.DriverHost/Control/UniClawControlSurface.cs` `IUniClawControlSurface` |
| `start` frozen as `DEFERRED_NO_KERNEL_CONTROL_BUYER` | `src/UniClaw.Runtime.DriverHost/Control/ControlSupportAudit.cs` |
| Existing multi-run registry backbone (reused): `DriverHostObservability._runs : Dictionary<string, RegisteredRun>` + `RegisterRun(runId, TraceRun, AgentStateSnapshot, captureBundle)` + `RuntimeEventStore` (per-runId append-only) | `src/UniClaw.Runtime.DriverHost/DriverHostObservability.cs`, `Store/RuntimeEventStore.cs` |
| Live Agent public-read-model snapshot: `AgentStateSnapshot.From(agent)` | `src/UniClaw.Runtime.DriverHost/Projection/AgentStateSnapshot.cs` |
| Per-run trace capture: `RuntimeTraceRecorder` (ActivitySource "UniClaw.Runtime") → frozen `TraceRun` | `src/UniClaw.Runtime.Harness/RuntimeTraceRecorder.cs` |
| Existing semantic entry: `Agent.RunSemanticGoalAsync(SemanticGoalInput, ImmutableArray<SemanticObject>, ImmutableArray<Capability>, string runId, CancellationToken, int maxIterations, viewportExplorationEvaluator, enableDeferredReconciliation)` | `src/UniClaw.Runtime/Agent/Agent.SemanticRun.cs` |
| Agent is single-Run per instance: second call throws "Agent has already executed a Run." | `Agent.SemanticRun.cs` guard |
| Production composition root: `PhysicalHostComposition.ResolveDeviceAsync / BuildRealEnvironment / CreateAttach / BuildRuntimeGraph → HostRuntimeGraph` | `src/UniClaw.Runtime.PhysicalHost/PhysicalHostComposition.cs` |
| Project graph: `UniClaw.Runtime` (zero refs), `DriverHost → Runtime+Harness` (no Adapters), `PhysicalHost → Runtime+Adapters`, `Adapters → Runtime` | `*.csproj` ProjectReference inspection |
| Run state model: `Idle → Initializing → Running → Completed \| Failed` | `src/UniClaw.Runtime/Model/RunState.cs` |
| Event vocabulary: `RunCompleted`/`RunFailed` emittable; no `RunStarted` kind exists | `src/UniClaw.Runtime.DriverHost/Model/RuntimeEventKind.cs` |

## 2. Target flow

```
DSH outer agent
  → uniclaw-run-goal (new DSH command; validate input → adapter.runStart → runId)
  → dsh-plugin-uniclaw (existing Cordis plugin surface, inject: ['commands'])
  → existing loopback TCP newline JSON-RPC (client: adapter.js; server: UniClawDriverHostServer)
  → run.start (new ADDITIVE method; the frozen 8 stay untouched)
  → IUniClawRunExecution.StartRun(RunStartRequest)   [NEW seam, distinct from read-only surface]
  → RunExecutionCoordinator                           [NEW]
       1. validate request (REQUEST_REJECTED on any validation failure)
       2. device factory (injected composition-root) → IEnvironment + semantic wiring
       3. create authoritative runId (DriverHost-owned)
       4. build runtime graph (PhysicalHostComposition.BuildRuntimeGraph shape)
       5. RuntimeTraceRecorder(runId) + AgentStateSnapshot.From(agent) → RegisterRun
          (REUSE DriverHostObservability/RuntimeEventStore; no second lifecycle store)
       6. start Agent.RunSemanticGoalAsync(...) as fire-and-track Task
       7. return RunAccepted { runId, runState }
  → DSH observes with EXISTING surfaces: run.events.after / run.snapshot.get /
    run.trap.get / run.events.drain / evidence.get
```

## 3. Wire contract — `run.start` (additive only)

### 3.1 Method

`run.start` is added to the transport dispatch switch. The frozen eight methods keep
their exact semantics and error codes. `run.start` uses the same transport
(newline-delimited JSON-RPC, same codec, same fail-open dispatch envelope).

### 3.2 Request (RunStartRequest — minimum truthful shape)

```json
{
  "goal": { "objectIdentity": "WifiConnectivity", "stateDimension": "Enabled", "desiredValue": true },
  "objects": [ { "identity": "WifiConnectivity", "category": "ConnectivitySetting", "stateDimensions": ["Enabled"] } ],
  "capabilities": [ { "name": "SetEnabled", "applicableToCategory": "ConnectivitySetting", "stateDimension": "Enabled" } ],
  "device": "serial:emulator-5554"
}
```

Field mapping (exact, no invented abstraction):

| Field | Maps to | Why required |
|---|---|---|
| `goal` | `SemanticGoalInput(ObjectIdentity, StateDimension, DesiredValue)` | the existing semantic goal entry |
| `objects` | `ImmutableArray<SemanticObject>` | `RunSemanticGoalAsync` resolves the goal object from this catalog; no catalog exists and scenario catalog is out of scope |
| `capabilities` | `ImmutableArray<Capability>` | capability selection is done by the Agent from this declarative catalog |
| `device` | `DeviceSelector` (string) | explicit selector resolved by the composition root; first slice supports the current Android path only |

Explicitly NOT in the request: `DeviceAction`, coordinates, `ElementIndex`,
`TraversalStep`, precompiled action sequence, `BusinessIntent`, `AgentProfile`,
`ConsultPoints`, `CapabilitySequence`, `RecoveryPolicy` DSL, prompt, LLM model,
intelligence settings, `maxIterations`/deferred-reconciliation toggles (composition-
side defaults in the first slice; a later change may add explicit execution
constraints if a concrete consumer proves the need).

### 3.3 Response

Success:

```json
{ "id": 1, "result": { "accepted": true, "runId": "run-<…>", "runState": "Initializing" } }
```

Rejection (distinct from accepted-then-failed):

```json
{ "id": 1, "error": { "code": "request_rejected", "message": "<deterministic reason>" } }
```

Rejection reasons are deterministic and non-fabricated: malformed request
(`bad_request`, existing code), unknown device selector, device busy (active run on
the same device), invalid goal (unknown object / unknown state dimension / no
matching capability — validated exactly the way `RunSemanticGoalAsync` fails closed).

### 3.4 Wire DTOs

New DTOs in `UniClaw.Runtime.DriverHost` (wire copies, no live references):
`RunStartRequestDto` (goal/objects/capabilities/device), `RunAcceptedDto`
(accepted/runId/runState). `run.start` is the only new method; no new
`RuntimeEventKind`, no new event transport (F4).

## 4. Run identity — DriverHost-owned

The coordinator creates the authoritative `runId` at acceptance (e.g.
`run-<monotonic>-<short-token>`), passes it as the `runId` argument to
`Agent.RunSemanticGoalAsync` (the existing entry's run identity), and registers the
observability store under the same id. DSH never invents or supplies the runId (F3).
The returned runId is immediately usable with `run.events.after`,
`run.snapshot.get`, `run.trap.get`, `run.events.drain`, `evidence.get` (T2).

## 5. RunExecutionCoordinator (NEW, DriverHost side)

New seam: `IUniClawRunExecution { RunAccepted StartRun(RunStartRequest request); }`
— deliberately separate from the frozen read-only `IUniClawControlSurface`, so the
read-only surface and its guarantee remain untouched (R10).

`RunExecutionCoordinator` is constructed with:
- the existing `DriverHostObservability` (reuse; no second store),
- an injected device factory `Func<DeviceSelector, RunComposition>` (composition
  root, see §6),
- optional `Func<string, string>` runId factory (deterministic default).

It owns (per accepted run): the runtime graph (`HostRuntimeGraph` shape), the
`RuntimeTraceRecorder`, the live `Agent` reference, the execution `Task`, and the
device-exclusivity registration. **It is the execution-side lifecycle owner; the
observability store remains the identity/lifecycle backbone for reads.**

Live observability refresh (pull, no new emitters): the coordinator registers the
run at accept with a truthful initial snapshot (`AgentStateSnapshot.From(agent)`,
State = Idle — the Agent's state at acceptance) and an empty trace, so the
returned runId is IMMEDIATELY legitimate for `run.list` / `run.snapshot.get` /
`run.events.after` (no race — registration happens synchronously BEFORE the Agent
task is scheduled). At termination the coordinator finalizes `RuntimeTraceRecorder`
and calls `DriverHostObservability.ReplaceRunProjection` (additive method backed by
`RuntimeEventStore.ReplaceRunEvents`) to replace the empty accept-time stream with
the final full projection (terminal state + complete event stream, stamped from
sequence 1). `RegisterRun`/`Append` frozen idempotency semantics are untouched for
all existing callers; no second store, no new event kinds, no new emitters.

## 6. Device factory boundary (explicit composition-root mapping)

No reflection/MEF/discovery (F10-adjacent). The composition root injects:

```csharp
// DriverHost side (narrow execution seam):
public delegate RunExecutionGraph RunGraphFactory(DeviceSelector selector);
public sealed record RunExecutionGraph(RuntimeAgent Agent, IEnvironment Environment);
```

The production mapping lives beside the existing `PhysicalHostComposition`
(`BuildRealEnvironment` + `BuildRuntimeGraph`): `CreateAndroidRunGraphFactory`
resolves the current Android path (`serial:<serial>` — explicit, no silent
fallback), exactly as the PhysicalHost Program does today. Unknown selector →
`DeviceSelectorUnsupportedException` → REQUEST_REJECTED. The `Agent` continues to
receive only `IEnvironment` + criteria (its existing contract); it never learns
about device transports, serials, or device plugins (F8, Guard 2, Guard 10a/10d).

`RunExecutionGraph` is the composition-root product for a device (fully wired
Agent graph over the device's environment). Task-level declarations
(`goal/objects/capabilities`) travel in the request. If a requested object has no
binding anchor in the device composition, the run fails closed through the
existing `BindingUnresolved` semantics — truthful, no fabricated progress.

## 7. Concurrency — ONE_ACTIVE_RUN_PER_DEVICE

- Storage (`DriverHostObservability` + `RuntimeEventStore`) is already multi-run
  capable (per-runId keys) — multiple devices may run concurrently.
- `Agent` is single-Run per instance, and a physical device is a shared world: two
  concurrent `Agent` loops driving the same ADB device would corrupt world
  ownership. First-slice policy: **`ONE_ACTIVE_RUN_PER_DEVICE`**, enforced
  explicitly in the coordinator's device-key → active-run mapping
  (`request_rejected: device busy` for the second concurrent accept on the same
  device). **Never inside `Agent`** (F9).
- No global serialization: distinct devices accept concurrently.

## 8. Observability reuse and completion model

- Reuse: `run.events.after` / `run.events.drain` / `run.snapshot.get` /
  `run.trap.get` / `evidence.get` with the returned runId (R4).
- Completion comes through existing Kernel truth: `RunCompleted` / `RunFailed`
  events, `RunState` in the snapshot, `GoalEvidence` reason. `DSHCompletionAuthority
  = NONE`; the DSH command never synthesizes completion (F4, F5-adjacent).
- `run.start` only starts. No `run.wait`/`run.result`/`run.trace` commands are added
  unless a concrete gap proves existing observability insufficient (none does).

## 9. Failure semantics

- `REQUEST_REJECTED` — validation/unknown-device/device-busy: no run created, no
  runId, no observability entry.
- `RUN_ACCEPTED_THEN_FAILED` — the runId exists; the Kernel later terminates with
  `Failed`; observable through existing snapshot/events (`RunFailed`). The two are
  never collapsed into a generic exception.
- DSH transport failure after acceptance does not fabricate or reset Kernel truth:
  the run continues in the DriverHost/Kernel process; reconnecting DSH rediscovers it
  through `run.list` / `run.snapshot.get` (existing surfaces). DSH restart does not
  couple to run durability in this slice (existing in-process store semantics).

## 10. DSH command — `uniclaw-run-goal`

One new command in `dsh-plugin-uniclaw` (registered through the existing
`commands.register` path, `inject: ['commands']`):

- Input: JSON payload `{ goal, objects, capabilities, device }` (same shape as the
  wire request); strict validation before any transport call.
- Behavior: call `adapter.runStart(...)` → return `runId` + `runState`.
- MUST NOT: poll until completion, auto-run shadow cognition, issue subsequent
  semantic actions, translate to ADB, write Runtime state.
- Zero model calls (F7). The outer DSH agent may have used a model to decide to
  invoke the tool; the command itself is deterministic control infrastructure.
- After start, the outer agent observes with the existing commands
  (`uniclaw-events-after`, `uniclaw-inspect-run`, `uniclaw-inspect-trap`,
  `uniclaw-evidence-open`).

## 11. ControlSupportAudit truthfulness amendment

`control.support("start")` currently returns `DEFERRED_NO_KERNEL_CONTROL_BUYER`.
After this change that statement would be false: a truthful authorized start entry
exists. This slice amends ONLY the `start` row (reason → supported-with-entry,
evidence cites `run.start` + `IUniClawRunExecution`); `pause`/`resume`/`stop`/`abort`
stay deferred. `control.support` semantics (a read-only audit lookup) are unchanged
(R10); this is a data truthfulness update, not a wire semantic change.

## 12. Backward compatibility (R10)

- The eight frozen read-only methods: exact semantics, exact DTOs, exact error codes
  — untouched.
- `IUniClawControlSurface`/`UniClawControlSurface`: untouched.
- `UniClaw.Runtime` (Kernel): untouched by this slice (Guard 2/3 unaffected).
- Existing DSH commands: untouched; `uniclaw-run-goal` is additive.

## 13. First-slice implementation scope

- DriverHost: `run.start` wire method + `IUniClawRunExecution` + coordinator +
  wire DTOs + request validation; all new files, no modification of the frozen
  read-only files except the `ControlSupportAudit.start` row (§11) and the
  server's additive `case "run.start"`.
- Composition: device factory mapping for the current Android path (beside
  `PhysicalHostComposition`); production DriverHost host composition wires
  surface + execution + factory (DriverHost owns its own process — §F6).
- Plugin: `uniclaw-run-goal` + `adapter.runStart` (new client method).
- Tests (later Apply gate): T1–T12 in the gate's test plan.
- NO iOS/Web/Desktop adapters, no device discovery, no TaskSpec, no IntelligenceSeam.

## 14. Deferred (explicitly NOT this change)

- `NEEDS_INTELLIGENCE` semantic baseline (next buyer after this entry is integrated).
- TaskSpec / intent compilation; perception domains; escalation protocol.
- Explicit execution constraints in the request (`maxIterations` etc. stay
  composition-side defaults until a concrete consumer proves the need).
- Viewport-exploration evaluator over the wire (a `Func`, cannot cross the wire;
  composition-side per device/app profile in the first slice).
