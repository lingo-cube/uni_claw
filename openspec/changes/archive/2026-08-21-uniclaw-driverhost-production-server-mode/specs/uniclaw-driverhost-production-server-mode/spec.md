# Spec: uniclaw-driverhost-production-server-mode

> BASELINE spec for a production DriverHost server-mode entry.
> No code in this change. Buyer: `RUNTIME_PROTOCOL_PRODUCTION_SERVABILITY`.
> Architecture v1 + Protocol v1 FROZEN — this change does NOT modify either.
> Owner: `Program.cs` entry + lifecycle (existing `PhysicalHostComposition` composition root).

## ADDED Requirements

### Requirement: Production server mode entry

The production entry point (`Program.cs`) MUST provide a `--serve` mode that
starts the existing DriverHost server via the existing Composition Root
(`PhysicalHostComposition.BuildDriverHostServer`) and remains alive until
process cancellation/termination. It MUST NOT invent a second server
architecture, alter frozen Protocol v1 surfaces, or introduce new architecture
components.

#### Scenario: --serve mode starts the existing DriverHost server

Given the production process is launched with `--serve`,
When the entry point parses options and selects server mode,
Then it calls `PhysicalHostComposition.BuildDriverHostServer(options)` and
`server.Start()`, and the process remains alive listening on the configured port.

#### Scenario: --serve mode does not invent a second server architecture

Given the --serve mode implementation,
When its composition path is inspected,
Then it reuses `BuildDriverHostServer` (the existing composition) and does NOT
construct a parallel or alternative server graph (falsifier F1/F8).

#### Scenario: existing proof modes are unchanged

Given the production process is launched with an existing proof flag
(--slice1, --slice2, --scroll, --multilevel, --corpus),
When the entry point dispatches,
Then the existing proof-mode behavior is unchanged (falsifier F10).

### Requirement: Clean lifecycle and shutdown

The server mode MUST shut down cleanly on process cancellation (Ctrl+C /
SIGTERM) or optional `--timeout`. The existing `UniClawDriverHostServer.Dispose()`
MUST be called in a `finally` block or equivalent deterministic disposal. The
process MUST exit 0 on clean shutdown.

#### Scenario: Ctrl+C triggers clean shutdown

Given the server is running in --serve mode,
When Ctrl+C is received (Console.CancelKeyPress),
Then the cancellation token is signaled, the server is disposed, and the process
exits 0.

#### Scenario: --timeout triggers clean shutdown

Given the server is running with `--timeout N`,
When N seconds elapse,
Then the cancellation token is signaled, the server is disposed, and the process
exits 0.

#### Scenario: no resource leak on shutdown

Given the server was started and then shut down,
When the process exits,
Then `server.Dispose()` was called (no leaked listener, no orphan connections).

### Requirement: Frozen Protocol v1 surfaces unchanged

The --serve mode MUST NOT alter any frozen Protocol v1 surface: Surface A
(`run.start`), Surface B (`run.snapshot.get`, `run.events.after`,
`run.events.drain`, `run.trap.get`, `evidence.get`), Control (`control.support`),
Capability (`assistance.pending`, `assistance.resolve`), Transport (`ping`).
Wire DTO shapes, JSON-RPC method semantics, and the Runtime External Hook
Boundary MUST remain unchanged.

#### Scenario: no wire method added or removed

Given the --serve mode implementation,
When the DriverHost server method table is inspected,
Then the frozen 8 read-only methods + `run.start` + `assistance.pending` +
`assistance.resolve` + `ping` are the complete set; no method is added, removed,
or semantically changed (falsifier F2).

#### Scenario: no DTO shape alteration

Given the --serve mode implementation,
When wire DTO types are inspected,
Then `RunStartRequest`, `RunAccepted`, `RunSnapshot`, `RuntimeEventEnvelope`,
`EvidenceRef`, `AssistanceRequestDigest`, `AssistanceResolveRequest`, and
`AssistanceResolveResult` shapes are unchanged (falsifier F2).

### Requirement: No architecture abstraction introduced

The --serve mode MUST NOT introduce UniAgent, AgentHost, Session contract,
ISwitchStateReader, non-terminal escalation transport, pause/resume/stop/abort
controls, typed hooks, multi-agent, multi-run, or any reserved extension. It
MUST NOT rename Agent → RuntimeAgent in production code.

#### Scenario: no UniAgent or AgentHost abstraction

Given the --serve mode implementation,
When production source is inspected,
Then no UniAgent class/interface and no AgentHost class/interface are introduced
(falsifier F3).

#### Scenario: no reserved extension purchased

Given the --serve mode implementation,
When production source is inspected,
Then no ISwitchStateReader freeze, no non-terminal escalation transport, no
pause/resume/stop/abort controls, no typed hooks, no multi-agent/multi-run
are introduced (falsifier F4).

#### Scenario: no Agent rename

Given the --serve mode implementation,
When production source is inspected,
Then the `Agent` class in `src/UniClaw.Runtime/Agent/` is NOT renamed to
`RuntimeAgent` (falsifier F9).

### Requirement: Production-path integration test

An IntegrationTestHost test MUST prove the production server path end-to-end:
start through the production serve-mode path → accept client connection →
respond to ping → accept run.start → expose RuntimeAgent-owned outcome/event/
snapshot through Surface B → observe truthful terminal outcome → shut down
cleanly. The test MUST use the production composition path
(`BuildDriverHostServer`), NOT a parallel test-only server graph.

#### Scenario: integration test exercises the full production path

Given the IntegrationTestHost test,
When it runs,
Then it starts the production serve-mode path, connects a client, sends ping,
sends run.start with a deterministic goal, polls run.events.after until terminal,
confirms run.snapshot.get shows terminal RunState, and confirms clean shutdown
(falsifier F8).

#### Scenario: existing DriverHost and RuntimeAgent tests pass

Given the full test suite,
When the --serve mode change is applied,
Then existing DriverHost tests (DriverHostRunStartE2ETests,
DriverHostPluginE2ETests, DriverHostAssistanceE2ETests,
UniClawDriverHostServerTests, RunStartWireTests, RunExecutionCoordinatorTests)
and existing RuntimeAgent tests all pass (no regression).
