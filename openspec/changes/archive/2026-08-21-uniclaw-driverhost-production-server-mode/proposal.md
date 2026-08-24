# Proposal: uniclaw-driverhost-production-server-mode

## Buyer

`RUNTIME_PROTOCOL_PRODUCTION_SERVABILITY`

The frozen Protocol v1 surfaces (Surface A: `run.start`; Surface B:
`run.snapshot.get`, `run.events.after`, `run.events.drain`, `run.trap.get`,
`evidence.get`; Control: `control.support`; Capability: `assistance.pending`,
`assistance.resolve`; Transport: `ping`) exist, are wired through
`PhysicalHostComposition.BuildDriverHostServer()`, and are E2E tested
(`DriverHostRunStartE2ETests`, `DriverHostPluginE2ETests`,
`DriverHostAssistanceE2ETests`). However, the production entry point
(`Program.cs`) does NOT start the DriverHost server — it only runs direct
proof/scenario scripts against `BuildRuntimeGraph`.

Therefore the frozen semantic protocol has a current transport realization,
but that realization is **not reachable as a long-lived production server mode**.
An external UniAgent / DSH plugin cannot connect to a running UniClaw production
process because no production process listens.

This change purchases ONLY the production server-mode entry that makes the
already-existing DriverHost / Protocol v1 transport surfaces available from the
real production process entrypoint. It is infrastructure required by the future
UniAgent orchestration vertical slice.

## What this change IS

- A `--serve` mode in `Program.cs` that starts the existing DriverHost server
  and remains alive until process cancellation/termination.
- Reuse of the existing `PhysicalHostComposition.BuildDriverHostServer()`
  composition (read surface + `RunExecutionCoordinator` +
  `AssistancePendingRegistry` + `AssistanceWireProvider` + Android run graph
  factory + Vision bootstrap).
- Clean shutdown/disposal on Ctrl+C / SIGTERM / cancellation.
- Minimum IntegrationTestHost coverage proving the production server path.

## What this change is NOT (explicit)

- NOT UniAgent implementation — no UniAgent abstraction, class, or interface.
- NOT AgentHost implementation — no AgentHost abstraction.
- NOT PhysicalHost completion — no new PhysicalHost abstraction for naming symmetry.
- NOT an architecture layer — DriverHost production server mode is the current
  production transport/runtime ingress, NOT an architecture concept.
- NOT Session contract formalization — current DSH Session-compatible impl
  remains sufficient.
- NOT non-terminal escalation transport — SEMANTICALLY_FROZEN_NOT_YET_REALIZED.
- NOT ISwitchStateReader purchase — DEFERRED_CAPABILITY_INSTANCE.
- NOT pause/resume/stop/abort controls — Reserved Extension.
- NOT RuntimeAgent / Runtime FSM / Surface A/B DTO alteration.
- NOT a second server architecture — reuses the existing one.
- NOT a DSH plugin redesign.
- NOT multi-agent / multi-run / sub-run / branch-run.
- NOT Agent → RuntimeAgent production rename.

## Gap (verified repository truth)

- `PhysicalHostComposition.BuildDriverHostServer()` — REALIZED (composes
  `UniClawDriverHostServer` with read surface + execution seam + assistance
  surface; `src/UniClaw.Runtime.PhysicalHost/PhysicalHostComposition.cs:242`).
- `UniClawDriverHostServer.Start()` / `Dispose()` — REALIZED
  (`src/UniClaw.Runtime.DriverHost/Transport/UniClawDriverHostServer.cs:76`).
- `Program.cs` Main — has proof modes (`--slice1`, `--slice2`, `--scroll`,
  `--multilevel`, `--corpus`) but **NO `--serve` mode**
  (`src/UniClaw.Runtime.PhysicalHost/Program.cs:40-75`).
- `BuildDriverHostServer` is called ONLY from tests
  (`tests/.../DriverHost/AndroidCompositionTests.cs:58`), NEVER from production.
- Cross-process E2E tests prove the server + wire + coordinator + Agent path
  works over loopback TCP — but only in test harness.

## Frozen baseline constraints

- Architecture v1: [`docs/architecture/uniagent-architecture-v1-core-development-guide.md`]
- Protocol v1: [`docs/architecture/uniagent-protocol-v1-consolidation-design.md`]
- Canonical index: [`docs/architecture/README.md`]
- All 19 Architecture v1 invariants and 23 Protocol v1 invariants are protected.
- No frozen/reserved capability is accidentally purchased.

## Scope discipline

This change proves ONLY that the production process can serve frozen Protocol
v1 surfaces. It does NOT prove UniAgent v1 exists as an orchestration layer,
does NOT complete PhysicalHost, and does NOT claim architecture-layer status for
the DriverHost server mode.
