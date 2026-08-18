# Proposal: dsh-runtime-agent-subagent-run-entry

## Buyer

The DSH outer agent needs an **authorized cross-process execution entry** to start an
existing `Runtime.Agent` semantic run as a deterministic execution subagent. The DSH
agent is the cognitive/control host; `Runtime.Agent` is the execution subagent that
owns semantic decision authority, authorization, execution, and verification.

## Gap

The DriverHost/DSH control plane is **read-only and cannot start a semantic run**:

- The DriverHost transport dispatch table (`UniClawDriverHostServer.Invoke`) contains
  exactly eight methods: `ping`, `run.list`, `run.snapshot.get`, `run.trap.get`,
  `run.events.after`, `run.events.drain`, `evidence.get`, `control.support`. No
  `run.start` (verified at source, `src/UniClaw.Runtime.DriverHost/Transport/UniClawDriverHostServer.cs`).
- `IUniClawControlSurface` exposes zero mutating operations; its doc comment states
  "There is deliberately NO method that mutates Kernel state."
- `ControlSupportAudit` freezes `start` as `DEFERRED_NO_KERNEL_CONTROL_BUYER` with
  evidence "No public Start control on UniClaw.Runtime.Agent ... DSH must never
  fabricate a start."
- Production run execution (`src/UniClaw.Runtime.PhysicalHost/Program.cs`) drives
  `Agent.RunSemanticGoalAsync` in-process with hardcoded run ids; the DriverHost is
  not involved in production execution at all.
- The DSH plugin (`dsh-plugin-uniclaw`) registers six zero-model commands
  (`uniclaw-inspect-run`, `uniclaw-inspect-trap`, `uniclaw-evidence-open`,
  `uniclaw-runs-list`, `uniclaw-events-after`, `uniclaw-shadow-analyze`) — all
  read-only; there is no command that starts a run.
- `run.start` exists only as a future-architecture concept in
  `docs/decisions/outer-intelligence-integration-architecture.md` and as a demo
  fixture event label; no production entry exists.

**Earliest missing system link: `AUTHORIZED_ASYNC_RUN_START_ENTRY`.**

## Desired outcome

```
DSH outer agent
  → uniclaw-run-goal (DSH tool, zero-model)
  → dsh-plugin-uniclaw
  → existing loopback TCP JSON-RPC transport
  → DriverHost
  → run.start (new ADDITIVE wire method)
  → RunExecutionCoordinator (new)
  → existing Runtime.Agent semantic entry (Agent.RunSemanticGoalAsync)
  → existing IEnvironment composition (current Android path only)
  → runId returned immediately (RunAccepted)
```

Observation then reuses the already-graduated surfaces with the returned runId:
`run.events.after`, `run.snapshot.get`, `run.trap.get`, `run.events.drain`,
`evidence.get`. **No second event/result protocol is created.**

## What this change does

1. Adds ONE additive wire method `run.start` to the DriverHost transport, alongside
   the frozen eight read-only methods (no existing method semantics change).
2. Adds the smallest truthful request contract `RunStartRequest
   { Goal, Objects[], Capabilities[], DeviceSelector }` — the exact inputs required
   by the existing `Agent.RunSemanticGoalAsync(goal, objects, capabilities, runId, …)`
   entry. No TaskSpec, no BusinessIntent, no AgentProfile, no capability sequence.
3. Adds a DriverHost `RunExecutionCoordinator` that: validates the request
   (REQUEST_REJECTED vs RUN_ACCEPTED_THEN_FAILED are distinct), owns run identity
   creation, builds the runtime graph through an injected composition-root device
   factory, registers the run into the existing observability store
   (`DriverHostObservability` + `RuntimeEventStore` — reused, no second lifecycle
   store), and starts the Agent execution asynchronously, returning `RunAccepted
   (runId)` immediately.
4. Adds ONE DSH command `uniclaw-run-goal` that validates input, calls `run.start`,
   and returns the runId. Zero model calls in the command/control path.
5. Enforces the explicit first-slice concurrency policy
   `ONE_ACTIVE_RUN_PER_DEVICE` in the DriverHost/composition control layer — never
   inside `Agent`.
6. Keeps the frozen authority boundaries: DSH supplies intent only (goal + object +
   capability declarations + device selector); DSH never supplies
   `DeviceAction`/coordinates/`ElementIndex`/precompiled action sequences; the Kernel
   keeps semantic decision, authorization, execution, verification, and GoalEvidence
   authority.

## Non-goals (explicitly out of scope)

- IntelligenceSeam (`IIntelligenceProvider`, `IntelligenceAdvice`,
  `intelligence.consult`, `perception.ask`, `escalation.resolve`,
  `NEEDS_INTELLIGENCE` runtime state) — the NEXT buyer, after this entry is integrated.
- TaskSpec / business intent compilation (Phase 6 intent → capability sequence).
- Multi-device registry, device discovery (`device.list`), reflection/MEF/assembly
  loading, plugin marketplace.
- iOS / Web / Desktop adapters — current supported Android path only, with a contract
  that permits a later second adapter without redesign.
- Run pause / resume / abort / stop controls.
- New Runtime semantics: no change to Runtime cognition, failure, recovery, or
  completion semantics; no new `RuntimeEventKind`; no new Runtime emitters.
- DSH process supervision of DriverHost (DriverHost keeps owning its own process;
  the plugin CONNECTS).
- Shadow cognition automatically on run start; scenario catalog; control-plane UI.

## Required output

`PROJECT_LEADER_DSH_RUNTIME_AGENT_SUBAGENT_RUN_ENTRY_BASELINE_RESULT` with
Decision `BUYER_CONFIRMED` (verified gap), `EarliestMissingSystemLink =
AUTHORIZED_ASYNC_RUN_START_ENTRY`, and this OpenSpec change (proposal/design/spec/
tasks) created and validated. No production or test files are changed in this gate.

## Authority

- `DirectDSHPhysicalAuthority = MUST_BE_NO` — `run.start` means "start this semantic
  task"; it never means "execute these coordinates/actions".
- `DirectDSHGoalEvidenceAuthority = MUST_BE_NO` — completion is projected from the
  Kernel (`GoalEvidence`, `RunCompleted`/`RunFailed`); DSH never synthesizes completion.
- `AgentDependsOnDSH = MUST_BE_NO` — the Agent receives only its existing injected
  dependencies (`IEnvironment` + criteria); no DSH/Cordis reference (Guard 2).
- `DriverHostProcessOwnedByPlugin = MUST_BE_NO` — frozen process-lifecycle decision
  preserved; the plugin CONNECTS, never launches/supervises/restarts.
- `ModelCallsForRunStart = MUST_BE_0` — the DSH command and the DriverHost
  `run.start` handler are deterministic control infrastructure.
- `ExistingReadOnlyWireCompatibility = PRESERVED` — the eight frozen read-only
  methods keep their exact semantics; `run.start` is additive.

## Falsifiers

| # | Falsifier | Fails if |
|---|---|---|
| F1 | DSH direct ADB/device action | the new entry accepts coordinates / concrete `DeviceAction` / element index as semantic authority |
| F2 | run.start blocks | `run.start` waits for run completion instead of returning `RunAccepted(runId)` immediately |
| F3 | DSH-runId | DSH creates the authoritative run id (DriverHost owns it) |
| F4 | duplicate protocol | a new event/result transport duplicates `RuntimeEvent`/snapshot surfaces |
| F5 | Agent←DSH dependency | `Agent` or `UniClaw.Runtime` acquires a DSH/plugin dependency |
| F6 | plugin-owned DriverHost | the plugin launches/supervises/restarts the DriverHost process |
| F7 | LLM in control path | `run.start` or `uniclaw-run-goal` requires a model call |
| F8 | physical bypass | the request carries `DeviceAction`/coordinates/`ElementIndex` as semantic authority |
| F9 | same-device concurrency | two runs on the same device execute without the explicit ownership rule |
| F10 | scope creep | the slice introduces TaskSpec/IntelligenceSeam only because future architecture needs them |

## Validation

- `openspec validate dsh-runtime-agent-subagent-run-entry --strict --no-interactive`
- `scripts/check-consistency.sh`
- Source inspection only in this gate; no production/test/wire/plugin implementation.
