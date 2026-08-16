# Design: dsh-uniclaw-control-plane-plugin-implementation

> Implementation design for the DSH↔UniClaw control-plane plugin slice.
> The frozen protocol baseline is authoritative:
> `openspec/changes/archive/2026-08-15-dsh-uniclaw-control-plane-protocol-baseline/`
> (design.md §1–§19, source-evidence-matrix.md S1–S43, integration-matrix.md).
> Nothing in this change re-opens a frozen decision. Where the directive forced
> a previously deferred choice (transport), the choice is recorded here with its
> justification, as the baseline required.

## 1. Scope and gates

Implemented in this slice (requirement G + A–F of the directive):

- A. DSH plugin lifecycle (Cordis plugin on the pinned fork `@deepseek-ai/cordis` 4.0.1).
- B. DriverHost connection boundary (one concrete local transport).
- C. Read-only `RuntimeEvent` consumption (cursor pages).
- D. Read-only `RunSnapshot` consumption (classification-preserving).
- E. `EvidenceRef` inspection (logical locator only).
- F. Deterministic human control seam (zero-model commands; explicit deferred audit).
- G. Source-compatible DSH event/service/command registration
  (`ctx.provide`, `ctx.commands.register`, `ctx.on('session/event')`, `ctx.emit`).

Hard constraints honored (frozen baseline + directive):

| Constraint | Disposition |
|---|---|
| Runtime.Agent refactor / new Container public surface / new Runtime semantic emitter / new GoalEvidence semantics / new physical execution authority / parallel protocol / generic transport framework / generic provider registry / DSH version upgrade | NOT REQUIRED by this implementation → no `IMPLEMENTATION_ARCHITECTURE_PRESSURE` |
| `RuntimeModified` | NO — zero files changed under `src/UniClaw.Runtime/` |
| `RuntimeAgentModified` | NO |
| `RuntimeSemanticModelChanged` | NO |
| `NewRuntimeSemanticEmitters` | NO |
| Model calls from commands / LLM / VLM | 0 everywhere |
| Custom durable session events | NONE — F18–F21 `NOT_APPLICABLE_NO_CUSTOM_DURABLE_EVENTS` |
| Pinned DSH checkout | read-only, never modified |

## 2. Module layout

```
src/UniClaw.Runtime.DriverHost/           (.NET — bounded additions, new files only)
  Control/UniClawControlSurface.cs        control facade over IReadOnlyObservability
  Control/ControlSupportAudit.cs          frozen deferred-control audit table
  Transport/UniClawWireContract.cs        wire DTOs (Kernel-fact copies, never live objects)
  Transport/UniClawWireCodec.cs           deterministic DTO mapping + JSON encode/decode
  Transport/DriverHostServerOptions.cs    server options (loopback, port, size guard)
  Transport/UniClawDriverHostServer.cs    TCP newline-delimited JSON-RPC server
dsh-plugin-uniclaw/                       (Node — DSH plugin module, owns the DSH dependency)
  package.json                            pins @deepseek-ai/cordis 4.0.1 (peer), DSH baseline constants
  README.md                               pin record, transport decision, composition snippet
  src/protocol.js                         line codec, RPC envelope, typed error codes
  src/adapter.js                          TCP client (node:net), correlation, reconnect state machine
  src/commands.js                         zero-model command definitions
  src/plugin.js                           Cordis plugin (apply/provide/commands/session-events/dispose)
  test/lifecycle.test.mjs                 PLUG-F1/F3/F4/F6/F17 (pinned cordis Context)
  test/adapter.test.mjs                   PLUG-F5/F10/F11/F13/F14/F16 (protocol client)
  test/commands.test.mjs                  PLUG-F7/F12/F15 (handler behavior, zero model)
  test/e2e-client.mjs                     client spawned by the .NET e2e test (PLUG-F2)
```

No existing file is modified: `src/UniClaw.Runtime/` untouched, DriverHost additions
are all new files, no new .NET project (no solution edit), tests are new files in
the existing test project (SDK wildcard include).

## 3. Transport decision (directive §9, previously TRANSPORT_DEFERRED)

**Decision: exactly ONE concrete local transport — loopback TCP with
newline-delimited JSON-RPC. DriverHost owns the listening process; the DSH plugin
is the client and CONNECTS.**

| Question | Answer |
|---|---|
| Why is a transport required at all? | The plugin runtime is Node; the DriverHost is .NET. They cannot share an in-process seam. |
| Existing DriverHost/process seam? | None. `PhysicalHost` is a console proof-runner (runs scenarios, exits), not a long-lived server; `IReadOnlyObservability` is explicitly transport-neutral ("wire format explicitly deferred"). |
| Why not in-process? | Impossible across runtimes (Node ↔ .NET). |
| Why not DSH-as-server (SDK/ACP-style inbound)? | The baseline fixed DSH as the client of the DriverHost surface (plugin CONNECTS); no inbound DSH service seam is defined at this baseline. Reversing direction would invent a parallel protocol. |
| Why TCP loopback, not UDS/stdio/HTTP/WebSocket? | Loopback TCP is the simplest local deterministic mechanism available to both runtimes with zero added dependencies (`node:net` ↔ `System.Net.Sockets`). UDS has no cross-platform parity here; stdio would require the DriverHost to spawn/own the plugin process (wrong ownership); HTTP/WebSocket add framing/abstraction without benefit for one local client. |
| Ownership | DriverHost owns the listener (its process, its lifecycle); the plugin connects and reconnects. |
| Failure behavior | Connection refused/timeout → typed `driverhost_disconnected` client error; observability failure never affects Kernel execution (fail-open). |
| Reconnect | Bounded attempts with backoff; a NEW connection starts with a FRESH cursor (server-side per-connection drain cursor resets); no snapshot/event state is fabricated or cached across reconnect. |
| Generic transport framework? | No — one concrete implementation, no abstraction layer, no registry. |

Wire contract (see `UniClawWireContract.cs` / `protocol.js`): one JSON object per
line; requests `{jsonrpc,id,method,params}`, responses `{jsonrpc,id,result}` or
`{jsonrpc,id,error:{code,message}}`. Error codes: `bad_request`, `unknown_method`,
`internal_error` (server), `driverhost_disconnected` (client-side typed failure).

Methods: `ping`, `run.list`, `run.snapshot.get`, `run.trap.get`,
`run.events.after` (cursor page), `run.events.drain` (per-connection cursor),
`evidence.get` (logical locator), `control.support` (audit).

## 4. Control surface and commands

`IUniClawControlSurface` (DriverHost side) exposes only read operations:
`Ping`, `ListRuns`, `InspectRun`, `InspectTrap`, `OpenEvidence`, `ControlSupport`,
`GetRuntimeEvents`. It never mutates Kernel state.

Command audit (directive §13/§14):

| Command | Status | Truthful buyer |
|---|---|---|
| `uniclaw-inspect-run` | IMPLEMENTED | read-only RunSnapshot projection (Direct/Derived/NotCurrentlyAvailable preserved) |
| `uniclaw-inspect-trap` | IMPLEMENTED | read-only ActiveTrap field (classified) |
| `uniclaw-evidence-open` | IMPLEMENTED | read-only logical EvidenceRef resolution (metadata only) |
| `uniclaw-runs-list` | IMPLEMENTED | read-only RegisteredRunIds |
| `uniclaw.start` | DEFERRED_NO_KERNEL_CONTROL_BUYER | no public Start control on the Agent surface |
| `uniclaw.pause` | DEFERRED_NO_KERNEL_CONTROL_BUYER | no public Pause control on the Agent surface |
| `uniclaw.resume` | DEFERRED_NO_KERNEL_CONTROL_BUYER | no public Resume control on the Agent surface |
| `uniclaw.stop` | DEFERRED_NO_KERNEL_CONTROL_BUYER | no public Stop control on the Agent surface |
| `uniclaw.abort` | DEFERRED_NO_KERNEL_CONTROL_BUYER | no public Abort control on the Agent surface |

Every implemented command handler performs 0 model calls (no LLM/VLM import or
invocation anywhere in the plugin module — verified by static scan and by
construction: handlers only call the adapter's read methods). `control.support`
returns the frozen audit entry for any operation, so the DSH side surfaces
`DEFERRED_NO_KERNEL_CONTROL_BUYER` explicitly instead of a missing method.

## 5. Read-only consumption semantics

- **RunSnapshot**: every field carries `{value, classification, truthSource, isPartial}`
  across the wire; `NotCurrentlyAvailable` fields stay visibly unavailable
  (never collapsed to null-as-truth). Unknown run → `RunSnapshot.Unknown` with a
  truthful diagnostic.
- **RuntimeEvent**: pages preserve `eventId` (stable), `sequence` (projection
  order only), `observationSequence` (Kernel anchor), `correlationId`/`causationId`,
  `evidenceRefs`, and the kind-specific payload as structured data. `GetAfter`
  cursor semantics are preserved (run-scoped `EventCursor`, `hasMore`, `nextCursor`).
  The DSH side never interprets payloads semantically (no cognition).
- **EvidenceRef**: resolution by LOGICAL locator only (`capture:{session}:record:{n}` /
  `capture:{session}:artifact:{id}`); never path-based, never embeds content.
  Persistent/lazy resolution stays DEFERRED.

## 6. Durability and events

- No custom durable session events are declared or written. `CustomDurableSessionEvents`
  = NONE; durability policy = RuntimeEvent stays live/read-model; only the sparse
  control-plane facts a future buyer would request are candidates for persistence.
- Plugin-owned live events reuse the DSH-native fanout: `uniclaw/connection` is
  emitted through `ctx.emit` (Cordis event bus), and DSH lifecycle events are
  consumed through the single `session/event` emit (`session/created` →
  lazy `ensureConnected`). No UniClawEventBus, no custom WebSocket, no browser push.

## 7. DSH baseline pin

- Pinned: commit `47f943859bef60e4160492346772ded9b24f765a` (0.1.0-rc.5),
  `SESSION_FORMAT_VERSION = 0`, 44 known session event types. Read-only.
- The plugin performs a runtime guard: it resolves the loaded
  `@deepseek-ai/cordis` version (the modified fork, 4.0.1) and refuses to activate
  on any other version. `dsh-plugin-uniclaw/package.json` pins the peer dependency
  and exports the baseline constants.

## 8. PLUG-F gate matrix (results to be reported by the leader)

| Gate | Check | Result |
|---|---|---|
| F1 | Plugin registers against pinned DSH/Cordis lifecycle (service + commands + events) | lifecycle test |
| F2 | Read surface reachable end-to-end across processes | .NET e2e (spawns node client) |
| F3 | Dispose cleans up (commands unregistered, socket closed, pending rejected) | lifecycle test |
| F4 | Connection lifecycle observable (states + `uniclaw/connection`) | lifecycle/adapter tests |
| F5 | Connection failure surfaces typed `driverhost_disconnected` | adapter test |
| F6 | DSH lifecycle event subscription (`session/event`) registered | lifecycle test |
| F7 | Commands execute deterministically, zero model calls | commands test |
| F8 | RunSnapshot classification preserved over the wire | codec test |
| F9 | Unavailable fields stay visibly unavailable | codec test |
| F10 | RuntimeEvent cursor semantics preserved | codec/adapter tests |
| F11 | Event identity stable (eventId, sequence, observationSequence) | codec test |
| F12 | EvidenceRef logical-locator-only | control surface test |
| F13 | Unknown run → Unknown snapshot with diagnostic | control surface test |
| F14 | Protocol errors typed (`bad_request`/`unknown_method`/`internal_error`) | server test |
| F15 | Unsupported Kernel control → explicit deferred result | control surface test |
| F16 | Reconnect obtains fresh state, no fabrication | adapter test |
| F17 | Static guard: zero model/LLM/VLM references in plugin module | Node static scan test |
| F18–F21 | Custom durable events (declare/known-set/ignore-safety/round-trip) | NOT_APPLICABLE_NO_CUSTOM_DURABLE_EVENTS |

## 9. Failure and reconnect semantics (directive §21–§24)

- Fail-open observability: plugin/projection/subscriber failure never equals
  Kernel execution failure; diagnostics travel as data.
- Typed deterministic errors: `run_not_found` is represented as `Unknown`
  snapshot data (not an exception); protocol-level errors carry stable string codes.
- Reconnect: bounded attempts, exponential backoff, connection state transitions
  emitted to listeners; after reconnect the consumer must re-fetch snapshot and
  cursors (adapter caches nothing; server per-connection drain cursors reset).

## 10. Validation (directive §37)

- `dotnet build src/UniClaw.Runtime.sln` — 0 errors.
- `dotnet test src/UniClaw.Runtime.sln` — new + existing tests pass.
- `node --test` in `dsh-plugin-uniclaw/` — lifecycle/adapter/commands pass.
- Architecture guards (new file) — A: Runtime zero DSH dep; B: Runtime.Agent zero
  DSH dep; C: DriverHost no DSH cognition/model dep; D: DSH packages confined to
  plugin/adapter boundary; E: no ADB/PhysicalEnvironment dep in plugin; F: no
  Container mutation path from plugin.
- `scripts/check-consistency.sh` — all pass.
- `openspec validate dsh-uniclaw-control-plane-plugin-implementation --strict --no-interactive`.
- Pinned DSH checkout never modified.

## 11. OpenSpec lifecycle

- Change is NOT archived during Apply (graduation review is the next gate:
  `PROJECT_LEADER_DSH_UNICLAW_CONTROL_PLANE_PLUGIN_GRADUATION_REVIEW`).
- Maturity claim: `DSH_UNICLAW_CONTROL_PLANE_PLUGIN_IMPLEMENTED` only
  (NOT INTEGRATED).
