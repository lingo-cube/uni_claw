# UniAgent v1 Implementation Gap Analysis & Next Change Proposal

> Authority: `PROJECT_LEADER_UNIAGENT_V1_IMPLEMENTATION_GAP_AND_NEXT_CHANGE`
> Mode: GAP ANALYSIS + BUYER IDENTIFICATION — NO production code changes, NO implementation
> Date: 2026-08-19
> Head: `203cf83` (uni-agent branch)
> Authoritative baselines:
> - [`uniagent-architecture-v1-core-development-guide.md`](uniagent-architecture-v1-core-development-guide.md) (frozen Architecture v1)
> - [`uniagent-protocol-v1-consolidation-design.md`](uniagent-protocol-v1-consolidation-design.md) (frozen Protocol v1)
> - [`README.md`](README.md) (canonical index)
>
> Constraint: This is NOT architecture design, NOT protocol design, NOT implementation.
> It identifies the next concrete production implementation change using frozen baselines
> as constraints and current repository evidence as ground truth.

---

## 1. Minimum v1 Runtime Path (from frozen topology)

```
Application / Entry
    ↓
Composition Host          (composition + entry + lifecycle)
    ↓
AgentHost                 (Agent lifecycle only)
    ↓
UniAgent                  (supervisory/orchestration agent)
    ↓ Surface A (Directive) / Surface B (Outcome)
RuntimeAgent              (bounded autonomy · execution truth authority)
    ↓
Physical World            (via Environment)
```

With:
- **Session** — correlation root (Surface S)
- **Capabilities** — Brain / Vision / Memory / Operator (Surface C)
- **Runtime External Hook Boundary** — cross-cutting invocation/authority rules
- **Transport Boundary** — replaceable (current: loopback TCP JSON-RPC)

---

## 2. Current Production Implementation Audit

Every classification is based on **production code evidence**, not document assumptions.

### 2.1 Composition Host

| Component | Classification | Evidence |
|---|---|---|
| Composition root | **REALIZED** | `PhysicalHostComposition` — `BuildRealEnvironment`, `BuildRuntimeGraph`, `CreateAttach`, `CreateAndroidRunGraphFactory`, `BuildDriverHostServer` (explicit dependency wiring; "唯一允许用真实 Provider 组合 PhysicalEnvironment 的代码位置") |
| Entry point | **PARTIALLY_REALIZED** | `Program.cs` exists and runs proof/scenario scripts, but has NO production server mode that starts the DriverHost and serves Surface A/B over wire. Only proof-script mode (`RunSlice1ProofAsync`, `RunSlice2ProofAsync`, `RunScrollProofAsync`, etc.) |
| Lifecycle/dispose | **PARTIALLY_REALIZED** | Proof scripts have `finally` dispose; `BuildDriverHostServer` returns a disposable server but is never started in production |

### 2.2 AgentHost

| Component | Classification | Evidence |
|---|---|---|
| AgentHost abstraction | **MISSING** | No `AgentHost` class/interface exists in `src/`. No "Agent lifecycle only" component. |
| Agent lifecycle | **PARTIALLY_REALIZED** | `RunExecutionCoordinator` manages run task lifetime (fire-and-track `Task.Run`), but it is a DriverHost execution coordinator, NOT an AgentHost. It conflates device reservation + runId creation + task scheduling + observability registration. |

### 2.3 UniAgent

| Component | Classification | Evidence |
|---|---|---|
| UniAgent abstraction | **MISSING** | No `UniAgent` class/interface exists in `src/`. |
| Supervisory/orchestration | **MISSING** | No code owns "UniAgent supervision of RuntimeAgent." The DSH plugin's `uniclaw-run-goal` command is a thin wire adapter — it validates JSON, calls `adapter.runStart`, returns runId. It performs NO orchestration, NO goal understanding, NO supervisory decision, NO strategy. |
| What currently performs the role | — | The DSH *human operator* performs the UniAgent role: the human decides the goal, types the command, observes outcomes via `uniclaw-events-after`/`uniclaw-inspect-run`, and decides next steps. The DSH plugin is a read-only command facade, not an orchestration agent. |

### 2.4 RuntimeAgent binding

| Component | Classification | Evidence |
|---|---|---|
| RuntimeAgent (Agent) | **REALIZED** | `src/UniClaw.Runtime/Agent/` — `Agent.cs`, `Agent.SemanticRun.cs`, `Agent.PlanRun.cs`, `Agent.Recovery.cs`, `Agent.OpenWorld.cs`. Bounded autonomy, execution truth authority, grounding, verification, local recovery. Frozen. |
| Surface A Directive consumption | **REALIZED** | `RunStartRequest` (4 fields) → `RunExecutionCoordinator.StartRun` → `Agent.RunSemanticGoalAsync`. Wire method `run.start` exists and works (E2E tested). |
| Surface B Outcome observation | **REALIZED** | `run.snapshot.get`, `run.events.after`, `run.events.drain`, `run.trap.get`, `evidence.get` — all wire methods exist and project RuntimeAgent-owned truth (classified snapshots, append-oriented events). E2E tested. |

### 2.5 Session binding/correlation

| Component | Classification | Evidence |
|---|---|---|
| Session (UniClaw-side) | **MISSING_CONTRACT** (frozen classification) | No UniClaw-side Session type. DSH-side `Session` serves command invocation context. Compatible with v1 but not formalized. |
| Session required for minimum v1? | **NOT_REQUIRED_FOR_MINIMUM_V1** | Current DSH Session-compatible implementation is sufficient for the first vertical slice. A UniClaw-side Session contract becomes a buyer only when a non-DSH host exists or a contract-freeze gate authorizes. Do NOT auto-purchase. |

### 2.6 Brain / IAssistanceProvider integration

| Component | Classification | Evidence |
|---|---|---|
| IAssistanceProvider (Brain Capability Contract) | **REALIZED** | `src/UniClaw.Runtime/Capabilities/Brain/IAssistanceProvider.cs` — `ConsultAsync(AssistanceContext, ct) → AssistanceAdvice?`. Advisory-only, worldVersion binding. FROZEN. |
| Assistance wire transport | **REALIZED** | `assistance.pending`/`assistance.resolve` wire methods + `AssistanceWireProvider` + DSH `AssistanceBridge` + `LlmAssistanceConsumer`. Cross-process E2E tested (model-free + real model). |
| Hook Boundary enforcement | **REALIZED** | `AssistancePendingRegistry` (capacity 8, timeout 30s, requestId/worldVersion validation, whitelist re-observe/rebind/dismiss-obstruction, atomic consume+remove). |

### 2.7 Capability registration/composition

| Component | Classification | Evidence |
|---|---|---|
| Capability catalog | **REALIZED** | `Model/Capability.cs` (immutable business semantic descriptor). Injected via `RunStartRequest.Capabilities`. |
| Capability composition at composition root | **REALIZED** | `PhysicalHostComposition.BuildRuntimeGraph` wires `IAssistanceProvider?` (null = zero regression). `BuildDriverHostServer` wires shared `AssistancePendingRegistry`. |
| Vision capability | **DEFERRED_BY_FREEZE** | `ISwitchStateReader` = DEFERRED_CAPABILITY_INSTANCE (UNPURCHASED; 8 preconditions). Do NOT implement. |
| Memory capability | **NOT_REQUIRED_FOR_MINIMUM_V1** | Not purchased; no buyer. |
| Operator capability | **NOT_REQUIRED_FOR_MINIMUM_V1** | Not purchased; no buyer. |

### 2.8 Runtime External Hook integration

| Component | Classification | Evidence |
|---|---|---|
| Hook Boundary (cross-cutting) | **REALIZED** (for Brain) | Safe invocation at belief adjudication (Contradicted/Unresolved). Advisory-only. Fail-closed. Staleness/correlation. RuntimeAgent accept/reject/reconcile. |
| Additional invocation points | **DEFERRED_BY_FREEZE** | Only one safe point exists (belief adjudication). Additional points = Reserved Extension. |

### 2.9 DSH plugin host role

| Component | Classification | Evidence |
|---|---|---|
| DSH plugin | **REALIZED** | `dsh-plugin-uniclaw/src/` — `plugin.js` (entry, `inject: ['commands']`), `commands.js` (6 commands), `adapter.js` (wire client), `assistance/` (bridge + consumers), `shadow/` (ephemeral analysis). |
| DSH as Composition Host / AgentHost / UniHost | **PARTIALLY_REALIZED** | DSH *can* implement these roles (v1 invariant 15), but currently the plugin is a read-only command facade + assistance bridge. It does NOT compose RuntimeAgent, manage Agent lifecycle, or perform UniAgent orchestration. |

### 2.10 Lifecycle / start / dispose path

| Component | Classification | Evidence |
|---|---|---|
| Proof-script lifecycle | **REALIZED** | `Program.cs` proof runs: `BuildEnvironmentAsync` (managed Vision start + health) → `BuildRuntimeGraph` → run → `finally` dispose. |
| DriverHost server lifecycle | **PARTIALLY_REALIZED** | `UniClawDriverHostServer` has `Start()`/`Dispose()`. `BuildDriverHostServer` composes it. But it is NEVER started in production `Program.cs` — only in tests. |
| Production server mode | **MISSING** | No `--serve` / `--host` mode in `Program.cs` that starts the DriverHost server and serves Surface A/B over wire for an external UniAgent/DSH to connect to. |

### 2.11 Current tests / TestHosts

| Test type | Classification | Evidence |
|---|---|---|
| RuntimeAgent unit/scenario tests | **REALIZED** | `tests/.../Unit/`, `Scenario/`, `Architecture/` — comprehensive; 1000+ tests |
| DriverHost wire tests | **REALIZED** | `tests/.../DriverHost/` — `UniClawDriverHostServerTests`, `RunStartWireTests`, `UniClawWireCodecTests` |
| Cross-process E2E (DriverHost + Node plugin) | **REALIZED** | `DriverHostPluginE2ETests`, `DriverHostRunStartE2ETests`, `DriverHostAssistanceE2ETests` — real loopback TCP + real Node client |
| Composition tests | **REALIZED** | `tests/.../Composition/PhysicalHostSlice1CompositionTests`, `tests/.../DriverHost/AndroidCompositionTests` |
| UniAgentTestHost | **MISSING** | No test host exercises a UniAgent orchestration layer above RuntimeAgent |
| IntegrationTestHost (production server mode) | **MISSING** | No test exercises a production server that serves Surface A/B to an external client |
| PhysicalHost (real device) | **PARTIALLY_REALIZED** | `Program.cs` proof scripts run on real emulator; `tests/.../PhysicalHost/VisionRuntimeBootstrapTests` |

---

## 3. Frozen Gaps (NOT auto-purchased)

| Gap | Frozen classification | Action |
|---|---|---|
| Non-terminal escalation transport | SEMANTICALLY_FROZEN_NOT_YET_REALIZED | Do NOT implement |
| ISwitchStateReader | DEFERRED_CAPABILITY_INSTANCE / UNPURCHASED | Do NOT implement |
| Session UniClaw-side contract | MISSING_CONTRACT | Do NOT auto-implement. Current DSH Session-compatible impl is sufficient for first vertical slice. |

---

## 4. First Real Implementation Buyer

### 4.1 The critical gap

**There is NO production server mode.** The production entry point (`Program.cs`)
only runs proof/scenario scripts directly against `BuildRuntimeGraph`. The
DriverHost server — which exposes Surface A (Directive) and Surface B (Outcome)
over wire — is only ever started in tests, never in production.

This means:
- The frozen Protocol v1 surfaces (run.start, run.snapshot.get, run.events.after,
  etc.) exist and are E2E tested, but **no production process serves them**.
- A DSH plugin / external UniAgent cannot connect to a running UniClaw production
  process because **no production process listens**.
- The entire v1 path (Application → Composition Host → AgentHost → UniAgent →
  Surface A/B → RuntimeAgent) is **broken at the Composition Host → server
  boundary**: the composition root can build a server, but the entry point never
  starts it.

### 4.2 Why this is the first buyer (not UniAgent abstraction)

A UniAgent abstraction is architecturally valid but **premature** as the first
implementation change because:
1. There is no production server for a UniAgent to connect to.
2. Building a UniAgent abstraction without a serving Composition Host produces
   an untestable orchestration layer.
3. The current DSH human-operator + plugin facade already performs the UniAgent
   role (human decides goal, types command, observes outcomes) — the missing
   piece is the **serving infrastructure**, not the orchestration abstraction.
4. A production server mode is the **smallest vertical slice** that proves the
   v1 path exists end-to-end: Composition Host serves → external client sends
   Directive (Surface A) → RuntimeAgent executes → external client observes
   Outcome (Surface B).

### 4.3 What this change proves

This change proves: **UniAgent v1 exists as a servable runtime path** — a
Composition Host that starts the DriverHost server, serves frozen Protocol v1
surfaces, and allows an external client to invoke RuntimeAgent through Surface A
and observe outcomes through Surface B.

It does NOT prove UniAgent orchestration autonomy (that is a later buyer). It
proves the **minimum servable v1 path**.

---

## 5. Does UniAgent Already Exist?

### A. Is there a production UniAgent abstraction?

**NO.** No `UniAgent` class/interface exists in `src/`.

### B. What currently performs the UniAgent responsibilities?

- **Goal understanding:** DSH human operator types the goal JSON.
- **Global decision / supervisory strategy:** DSH human operator decides.
- **RuntimeAgent supervision:** None — RuntimeAgent runs autonomously once started.
- **Capability orchestration:** DSH human operator selects capabilities in the goal JSON.
- **Memory usage:** None.
- **Brain / Operator invocation:** Assistance is wired but human-triggered (via DSH
  `assistance.consumer=llm` config); not UniAgent-orchestrated.

### C. What is the minimum new UniAgent abstraction needed?

**Not yet.** The first buyer is the **serving infrastructure**, not the UniAgent
abstraction. The minimum new UniAgent abstraction becomes a buyer only when:
1. A production server serves Surface A/B (this change).
2. A real orchestration scenario requires automated goal decomposition,
   supervisory adjudication, or capability orchestration beyond what the DSH
   human operator + plugin facade currently does.

**Do NOT rename RuntimeAgent code.** The `Agent` class in `src/UniClaw.Runtime/`
IS the RuntimeAgent. Terminology alignment is documentation-only (already done
in the architecture index). No production rename.

---

## 6. TestHost Buyer Analysis

| TestHost | Needed for this change? | Rationale |
|---|---|---|
| RuntimeTestHost | NO | RuntimeAgent already comprehensively tested |
| RuntimeExtensionTestHost | NO | Extensions (assistance, vision bootstrap) already tested |
| UniAgentTestHost | NO | No UniAgent abstraction in this change |
| IntegrationTestHost | **YES** | This change is about serving the v1 path end-to-end — an integration test that starts the production server mode and drives it through an external client is the minimal TestHost |
| PhysicalHost | NO | Not needed for the minimum servable path (deterministic ScriptedEnvironment is sufficient) |

**Purchase: IntegrationTestHost only.** This is the smallest TestHost that
validates the change: production server starts → external client connects →
Directive → RuntimeAgent → Outcome → terminal.

---

## 7. Proposed Next Change

### Change name

`uniclaw-physicalhost-server-mode`

### Buyer

`PRODUCTION_V1_SERVABLE_PATH` — the minimum capability that proves UniAgent v1
exists as a servable runtime path: a Composition Host production entry that
starts the DriverHost server and serves frozen Protocol v1 surfaces to an
external client.

### Problem

The frozen Protocol v1 surfaces (Surface A: `run.start`; Surface B:
`run.snapshot.get`, `run.events.after`, etc.) exist, are wired, and are E2E
tested — but **no production entry point starts the DriverHost server**. The
production `Program.cs` only runs proof/scenario scripts directly against
`BuildRuntimeGraph`. An external UniAgent / DSH plugin cannot connect to a
running UniClaw production process because no production process listens.

### Current evidence

- `PhysicalHostComposition.BuildDriverHostServer()` composes a complete
  `UniClawDriverHostServer` (read surface + execution seam + assistance surface)
  — **REALIZED** but never called from production.
- `Program.cs` Main has proof modes (`--slice1`, `--slice2`, `--scroll`, etc.)
  but **NO server mode** (`--serve` / `--host`).
- `UniClawDriverHostServer.Start()` / `Dispose()` exist and work.
- Cross-process E2E tests (`DriverHostRunStartE2ETests`, `DriverHostPluginE2ETests`,
  `DriverHostAssistanceE2ETests`) prove the server + wire + coordinator + Agent
  path works over loopback TCP — but only in test harness, not production.

### Exact production scope

1. Add a `--serve` mode to `Program.cs` (or a separate minimal server entry)
   that:
   - Resolves device/options (existing `PhysicalHostOptions.Parse`)
   - Calls `PhysicalHostComposition.BuildDriverHostServer(options)`
   - Starts the server (`server.Start()`)
   - Blocks until cancellation/termination signal
   - Disposes the server on exit (`finally`)
2. No new architecture component. No UniAgent abstraction. No AgentHost.
   No Session contract. No new wire methods. No DTOs.
3. The server mode reuses the EXISTING `BuildDriverHostServer` composition
   (which already wires: read surface + `RunExecutionCoordinator` +
   `AssistancePendingRegistry` + `AssistanceWireProvider` + Android run graph
   factory + Vision bootstrap).

### Exact test scope

1. **IntegrationTestHost** (new): a test that:
   - Starts the production server mode (or `BuildDriverHostServer` + `Start`)
   - Connects an external client (Node plugin adapter or raw JSON-RPC client)
   - Sends `ping` → confirms identity
   - Sends `run.start` with a deterministic ScriptedEnvironment goal → confirms
     `RunAccepted(runId)`
   - Polls `run.events.after` → confirms `RunCompleted` or `RunFailed`
   - Confirms `run.snapshot.get` shows terminal `RunState`
   - Disposes the server
2. Reuse existing `DriverHostRunStartE2ETests` patterns (loopback TCP + Node
   client) but drive through the production server entry, not a test-only
   server construction.

### Files/components likely affected

| File | Change type | Scope |
|---|---|---|
| `src/UniClaw.Runtime.PhysicalHost/Program.cs` | ADD `--serve` mode | New branch in `Main` that starts and serves `BuildDriverHostServer` |
| `src/UniClaw.Runtime.PhysicalHost/PhysicalHostOptions.cs` | ADD `--serve` flag | Parse a `Serve` boolean option (minimal) |
| `tests/UniClaw.Runtime.Tests/Integration/` (new dir) | ADD IntegrationTestHost | New test class exercising production server mode end-to-end |
| `tests/UniClaw.Runtime.Tests/Integration/ServerModeIntegrationTests.cs` (new) | ADD test | Start server → connect → run.start → observe → terminal → dispose |

**NOT affected:**
- `src/UniClaw.Runtime/` (RuntimeAgent — unchanged)
- `src/UniClaw.Runtime.DriverHost/` (wire surface — unchanged)
- `dsh-plugin-uniclaw/` (DSH plugin — unchanged)
- No new wire methods, no new DTOs, no protocol changes

### Frozen invariants exercised

| Invariant | How exercised |
|---|---|
| v1-2 RuntimeAgent = bounded specialist execution agent | RuntimeAgent executes via `RunSemanticGoalAsync` unchanged |
| v1-4 RuntimeAgent retains execution/world-truth authority | Server mode only forwards Directive; no authority bypass |
| v1-10 Composition Host owns composition and entry only | `--serve` mode is entry + lifecycle only; no goal/agent/runtime decision |
| v1-15 DSH is implementation framework, not architecture | Server mode is transport-agnostic; DSH plugin is one possible client |
| v1-17 External capabilities cannot acquire Runtime authority | Server exposes only frozen read + run.start surfaces; no mutation |
| v1-18 1 Session / 1 Goal / 1 Run default | `ONE_ACTIVE_RUN_PER_DEVICE` enforced (existing) |
| PI-1 Directive carries only task-level declarations | `RunStartRequest` 4 fields unchanged |
| PI-2 RuntimeAgent owns runId creation | `RunExecutionCoordinator` creates runId (existing) |
| PI-3 Acceptance is asynchronous | `RunAccepted` returns immediately (existing) |
| PI-5 All outcomes are producer-derived | Server projects RuntimeAgent-owned truth (existing) |

### Explicit exclusions

- NO UniAgent abstraction / class / interface
- NO AgentHost abstraction
- NO Session UniClaw-side contract
- NO non-terminal escalation transport
- NO ISwitchStateReader / Vision capability purchase
- NO multi-agent / multi-run / sub-run / branch-run
- NO typed hooks
- NO RuntimeAgent code changes
- NO Agent → RuntimeAgent rename
- NO new wire methods / DTOs / protocol changes
- NO DSH plugin changes
- NO Brain capability changes (assistance wiring is already in `BuildDriverHostServer`)

### Graduation criteria

1. `dotnet build` — 0 errors, 0 warnings
2. `scripts/check-consistency.sh` — ALL PASS (C1-C10)
3. Architecture guards — ALL PASS
4. `ServerModeIntegrationTests` — PASS (start → ping → run.start → observe events → terminal snapshot → dispose)
5. Existing `DriverHostRunStartE2ETests` + `DriverHostPluginE2ETests` + `DriverHostAssistanceE2ETests` — still PASS (no regression)
6. Existing full test suite — no new failures (pre-existing environmental failures unchanged)
7. Manual verification: `dotnet run --project src/UniClaw.Runtime.PhysicalHost -- --serve` starts, listens, and serves (a client can connect and `ping`)
8. OpenSpec change created (`uniclaw-physicalhost-server-mode`) with proposal/design/specs/tasks

### Dependencies

- None blocking. All infrastructure (`BuildDriverHostServer`, `RunExecutionCoordinator`,
  wire surface, E2E test patterns) already exists and is graduated.
- Architecture v1 + Protocol v1 frozen (constraints, not dependencies).

### Why this change comes before other candidates

| Candidate | Why not first |
|---|---|
| UniAgent abstraction | Premature — no production server for UniAgent to connect to. Building UniAgent without a serving Composition Host produces an untestable layer. |
| Session UniClaw-side contract | MISSING_CONTRACT but current DSH Session-compatible impl is sufficient for first slice. Auto-purchasing is forbidden. |
| Non-terminal escalation transport | SEMANTICALLY_FROZEN_NOT_YET_REALIZED — Reserved Extension, no buyer. |
| ISwitchStateReader | DEFERRED_CAPABILITY_INSTANCE — UNPURCHASED, 8 preconditions. |
| Additional Hook invocation points | Reserved Extension, no buyer. |
| Push-based event delivery | Transport optimization, no buyer. |

**This change is the smallest vertical slice that proves the v1 path is servable
end-to-end.** It requires no new architecture, no new protocol, no new abstraction
— it connects already-realized components into a production entry point.

---

## 8. Ordered Backlog (NOT opened — no design, no OpenSpec)

| Rank | Candidate buyer | Condition to open |
|---|---|---|
| 1 | `uniclaw-physicalhost-server-mode` (THIS CHANGE) | Approved now |
| 2 | UniAgent minimum orchestration abstraction | After #1: when a real scenario requires automated goal dispatch + outcome observation + supervisory decision beyond the DSH human-operator facade. Proves UniAgent exists as an orchestration layer above the serving path. |
| 3 | Session UniClaw-side contract formalization | After #2: when a non-DSH host buyer exists or when UniAgent orchestration requires correlation references that DSH Session cannot provide. |
| 4 | Non-terminal escalation transport realization | When a real supervisory-adjudication buyer requires RuntimeAgent to signal non-terminal decision-required to UniAgent. Reserved Extension. |

---

## FINAL STATUS

```
UNIAGENT_V1_NEXT_IMPLEMENTATION_CHANGE_READY
```

**Recommended next change:** `uniclaw-physicalhost-server-mode`

**Buyer:** `PRODUCTION_V1_SERVABLE_PATH`

**Graduation criteria:** build 0/0, consistency ALL PASS, guards ALL PASS,
ServerModeIntegrationTests PASS, existing E2E tests no regression, full suite
no new failures, manual `--serve` verification, OpenSpec change created.

**Remaining implementation gaps (frozen, NOT auto-purchased):**
1. Session UniClaw-side contract — MISSING_CONTRACT (sufficient for first slice)
2. Non-terminal escalation transport — SEMANTICALLY_FROZEN_NOT_YET_REALIZED
3. ISwitchStateReader — DEFERRED_CAPABILITY_INSTANCE (UNPURCHASED)

STOP. Do not implement. Do not create the OpenSpec change automatically.
