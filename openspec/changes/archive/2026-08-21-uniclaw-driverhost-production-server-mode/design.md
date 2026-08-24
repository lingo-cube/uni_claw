# Design: uniclaw-driverhost-production-server-mode

> BASELINE design (no code). Source-verified: 2026-08-19.
> Architecture v1 + Protocol v1 FROZEN. This change does NOT modify either.

---

## 0. Explicit non-identity

| Concept | This change? | Reason |
|---|---|---|
| DriverHost production server mode | **YES** | The production transport/runtime ingress |
| UniAgent | NO | No UniAgent abstraction; this is infrastructure for a future UniAgent buyer |
| PhysicalHost | NO | No new PhysicalHost abstraction; reuses existing `PhysicalHostComposition` |
| Architecture layer | NO | DriverHost server mode is transport/runtime ingress, NOT an architecture concept |

DriverHost production server mode ≠ UniAgent ≠ PhysicalHost ≠ architecture layer.
It is the current production transport/runtime ingress needed by a later UniAgent buyer.

---

## 1. Current production entry (source-verified)

```
Program.cs Main(args)
  → PhysicalHostOptions.Parse(args)
  → proof-mode dispatch:
      --scroll    → RunScrollProofAsync
      --multilevel → RunMultiLevelProofAsync
      --corpus    → RunCorpusProofAsync
      --slice2    → RunSlice2ProofAsync
      else        → RunSlice1ProofAsync
  → NO --serve mode exists
```

Each proof mode:
- Calls `BuildEnvironmentAsync` (managed Vision or external)
- Calls `PhysicalHostComposition.BuildRuntimeGraph(environment, options, attach)`
- Runs a scenario directly against the Agent
- Exits

**No proof mode starts the DriverHost server.** The server
(`BuildDriverHostServer`) is only constructed in tests.

---

## 2. Target production server path

```
Program.cs Main(args)
  → PhysicalHostOptions.Parse(args)
  → if --serve:
      → resolve options (serial, vision socket path, port, etc.)
      → PhysicalHostComposition.BuildDriverHostServer(options, serverOptions, ...)
      → server.Start()
      → wait for cancellation (Ctrl+C / SIGTERM / CancellationTokenSource timeout)
      → finally: server.Dispose()
      → exit 0
  → else: existing proof-mode dispatch (unchanged)
```

### 2.1 What the server path reuses (NO new architecture)

| Existing component | Reused as | Evidence |
|---|---|---|
| `PhysicalHostComposition.BuildDriverHostServer()` | Server composition | `PhysicalHostComposition.cs:242` |
| `UniClawDriverHostServer` | TCP listener + JSON-RPC dispatcher | `Transport/UniClawDriverHostServer.cs` |
| `UniClawControlSurface` | Read-only surface (run.list, snapshot, trap, events, evidence, control.support) | `Control/UniClawControlSurface.cs` |
| `RunExecutionCoordinator` | Execution seam (run.start → RunAccepted → async Agent.RunSemanticGoalAsync) | `Execution/RunExecutionCoordinator.cs` |
| `AssistancePendingRegistry` + `AssistanceWireProvider` | Assistance surface (assistance.pending/resolve) | `Assistance/*.cs` |
| `CreateAndroidRunGraphFactory` | Device selector → RunExecutionGraph (RuntimeAgent + Environment + Startup) | `PhysicalHostComposition.cs:212` |
| `VisionRuntimeBootstrap` (optional) | Managed Vision lifecycle (if --vision-managed) | `VisionRuntimeBootstrap.cs` |

**No new server architecture.** The server path is a thin entry branch that
calls the existing composition and lifecycle methods.

---

## 3. Server lifecycle

### 3.1 Start

```
--serve
  → BuildDriverHostServer(options) → UniClawDriverHostServer
  → server.Start()  (binds loopback TCP port; listens)
  → Console.WriteLine($"SERVING port={port}")  (or log)
  → await process-lifetime signal
```

### 3.2 Lifetime / cancellation

The server MUST remain alive until:
- `Ctrl+C` (Console.CancelKeyPress) — graceful cancellation
- `SIGTERM` (Process signal / AppDomain.ProcessExit) — graceful shutdown
- Optional `--timeout <seconds>` — auto-shutdown for testing

A `CancellationTokenSource` links these signals. The server's `Dispose()` is
called in `finally` / `await using`.

### 3.3 Shutdown / disposal

```
finally:
  → server.Dispose()  (stops listener; drains connections; releases resources)
  → exit 0
```

`UniClawDriverHostServer.Dispose()` already exists and stops the TCP listener.
No new disposal logic needed.

---

## 4. Configuration

### 4.1 PhysicalHostOptions additions (minimal)

| Option | Type | Default | Purpose |
|---|---|---|---|
| `--serve` | bool | false | Select server mode |
| `--port` | int | 5177 | DriverHost listen port (existing `DriverHostServerOptions.Port`) |
| `--timeout` | int? | null | Optional auto-shutdown seconds (for testing; null = run forever) |

**No new configuration abstraction.** These map directly to existing
`DriverHostServerOptions` and the new `--serve` flag.

### 4.2 Existing options reused (unchanged)

- `--serial <id>` — device selector
- `--vision-socket <path>` — external Vision endpoint
- `--vision-python <path>` — managed Vision python
- `--adb <path>` — ADB binary path
- `--settings <package>` — Settings package

---

## 5. Testability

### 5.1 Why a minimal refactor MAY be needed

The current `Program.cs Main` is a static method that dispatches proof modes.
To test the `--serve` path without spawning a separate process, the server-mode
selection and lifecycle MAY be extracted into a testable method (e.g.,
`RunServerModeAsync(options, ct)`).

**Principle:** extract ONLY if an actual testability/lifecycle buyer requires
it. Do NOT create abstractions for naming symmetry.

### 5.2 IntegrationTestHost (purchased)

The IntegrationTestHost proves the production server path end-to-end:

```
1. Start: call the production serve-mode path (RunServerModeAsync or equivalent)
   with --port 0 (ephemeral) and --timeout N (auto-shutdown)
2. Connect: raw JSON-RPC client (or Node plugin adapter) to the ephemeral port
3. ping → confirm identity (service name + protocol version)
4. run.start (deterministic ScriptedEnvironment goal) → confirm RunAccepted(runId)
5. run.events.after(runId, cursor=0) → poll until RunCompleted or RunFailed
6. run.snapshot.get(runId) → confirm terminal RunState
7. Shutdown: timeout/cancellation → server.Dispose() → confirm clean exit
```

**The test MUST use the production composition path**
(`BuildDriverHostServer`), NOT a parallel test-only server graph.

---

## 6. Frozen protocol rules (protected)

| Rule | Protected? | How |
|---|---|---|
| Surface A (`run.start`) semantics | YES | `RunStartRequest` 4 fields unchanged; no new field |
| Surface B (snapshot/events/trap/evidence) semantics | YES | Read-only projections unchanged |
| Surface C (Capability Contract) semantics | YES | `IAssistanceProvider` unchanged |
| Surface S (Session) semantics | YES | No Session contract formalized |
| Hook Boundary | YES | `AssistancePendingRegistry` unchanged |
| Wire DTO shapes | YES | No DTO alteration |
| JSON-RPC method semantics | YES | No method added/removed/changed |
| DriverHost/TCP/JSON-RPC NOT architecture | YES | `--serve` is transport ingress, not architecture |

---

## 7. Authority / ownership (unchanged)

| Owner | Owns | Changed? |
|---|---|---|
| RuntimeAgent (Agent) | execution/world-truth/grounding/verification/completion | NO |
| RunExecutionCoordinator | runId creation, device reservation, task scheduling | NO |
| UniClawDriverHostServer | wire transport, request dispatch | NO |
| PhysicalHostComposition | composition root | NO |
| Program.cs | entry + lifecycle (new --serve branch) | ADD branch only |

**AuthorityDelta: NONE.** The `--serve` mode forwards Directive and projects
Outcome; it owns NO semantic authority.

---

## 8. Falsifiers

| # | Falsifier | What it prevents |
|---|---|---|
| F1 | `--serve` mode invents a second server architecture | Reuse existing `BuildDriverHostServer` |
| F2 | `--serve` mode alters Surface A/B DTOs or wire semantics | No DTO/method changes |
| F3 | `--serve` mode implements UniAgent / AgentHost / Session | No new abstractions |
| F4 | `--serve` mode purchases ISwitchStateReader or reserved extensions | No capability purchases |
| F5 | `--serve` mode bypasses RuntimeAgent authority | Server forwards; no authority bypass |
| F6 | `--serve` mode promotes DriverHost/TCP/JSON-RPC to architecture | Transport ingress only |
| F7 | Server does not shut down cleanly on Ctrl+C / cancellation | `finally` Dispose |
| F8 | IntegrationTestHost uses a parallel test-only server graph | Must use production composition |
| F9 | `--serve` mode renames Agent → RuntimeAgent in production code | No rename |
| F10 | Existing proof modes are broken by --serve addition | Proof modes unchanged |
