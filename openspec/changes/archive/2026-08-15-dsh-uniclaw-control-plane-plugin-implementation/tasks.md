# Tasks: dsh-uniclaw-control-plane-plugin-implementation

> System of record for implementation progress. Check each box the moment the
> task is complete; final counts are reported in the leader result.

## Slices

- [x] Slice 0 — OpenSpec change scaffolding (proposal/design/spec/README/.openspec.yaml)
- [x] Slice 1 — DSH plugin skeleton + lifecycle (`dsh-plugin-uniclaw/` module,
      pinned-cordis guard, service provide, commands register, session/event
      subscription, dispose cleanup)
- [x] Slice 2 — DriverHost read-only connection boundary (control facade, wire
      contract, codec, TCP JSON-RPC server)
- [x] Slice 3 — RunSnapshot / RuntimeEvent / EvidenceRef mapping (classification
      preserved, cursors preserved, logical-locator evidence)
- [x] Slice 4 — Minimum deterministic human commands (uniclaw-inspect-run,
      uniclaw-inspect-trap, uniclaw-evidence-open, uniclaw-runs-list; deferred
      audit for start/pause/resume/stop/abort)
- [x] Slice 5 — Failure / reconnect semantics (typed errors, bounded reconnect,
      fresh-state, per-connection drain cursor)
- [x] Slice 6 — Architecture guards + e2e deterministic integration tests
- [x] Validation — build, tests, node suite, consistency, openspec validate

## .NET DriverHost additions

- [x] `Control/ControlSupportAudit.cs` — frozen deferred-control audit table
- [x] `Control/UniClawControlSurface.cs` — read-only facade over IReadOnlyObservability
- [x] `Transport/UniClawWireContract.cs` — wire DTOs (Kernel-fact copies)
- [x] `Transport/UniClawWireCodec.cs` — deterministic DTO mapping + JSON codec
- [x] `Transport/DriverHostServerOptions.cs` — loopback server options
- [x] `Transport/UniClawDriverHostServer.cs` — TCP newline-delimited JSON-RPC server

## Node plugin module (dsh-plugin-uniclaw/)

- [x] `package.json` — pins @deepseek-ai/cordis 4.0.1 (peer), baseline constants
- [x] `src/protocol.js` — line codec, RPC envelope, typed error codes
- [x] `src/adapter.js` — TCP client, correlation, reconnect state machine
- [x] `src/commands.js` — zero-model command definitions
- [x] `src/plugin.js` — Cordis plugin (apply/provide/commands/events/dispose)
- [x] `test/lifecycle.test.mjs` — F1/F3/F4/F6/F17
- [x] `test/adapter.test.mjs` — F5/F10/F13/F14/F16
- [x] `test/commands.test.mjs` — F7/F12/F15
- [x] `test/e2e-client.mjs` — spawned by the .NET e2e test (F2)

## Tests (.NET, new files)

- [x] `ControlSurfaceTests.cs` — F12/F13/F15 + facade semantics
- [x] `UniClawWireCodecTests.cs` — F8/F9/F10/F11 round-trips
- [x] `UniClawDriverHostServerTests.cs` — F14 + transport behavior
- [x] `DriverHostPluginE2ETests.cs` — F2 cross-process e2e
- [x] `PluginIntegrationGuardTests.cs` — architecture guards A–F

## Durable real-host regression coverage (graduation-review §16)

> Records the durable protection added for the command-registration race after
> the REPAIR_REQUIRED review: `dsh-plugin-uniclaw/test/real-host.test.mjs`
> boots the REAL pinned DSH host (`boot()` from `@deepseek-ai/dsh-app-boot`,
> vendored `cordis-plugin-loader`, `@deepseek-ai/cordis` 4.0.1, real
> `@deepseek-ai/dsh-commands`) with the commands registry and the plugin as
> SEPARATE parallel loader entries, and asserts the actual registry view holds
> exactly the four commands. Missing `inject: ['commands']` reproduces an empty
> registry under this test (historically proven); the repaired implementation
> passes. No requirements or architecture changed; the protocol baseline is
> untouched.

- [x] `test/real-host.test.mjs` — durable real-host regression test (real boot,
      parallel loader entries, inject dependency, actual registry view, real
      command execution, session lifecycle, clean disposal)
- [x] Included in the normal `npm test` suite (auto-discovered by
      `node --test "test/*.test.mjs"`)

## Validation

- [x] `dotnet build src/UniClaw.Runtime.sln` — 0 errors
- [x] `dotnet test src/UniClaw.Runtime.sln` — all pass
- [x] `node --test` in `dsh-plugin-uniclaw/` — all pass
- [x] `scripts/check-consistency.sh` — all pass
- [x] `openspec validate dsh-uniclaw-control-plane-plugin-implementation --strict --no-interactive` — PASS
- [x] Pinned DSH checkout verified unmodified (git status clean in checkout)
