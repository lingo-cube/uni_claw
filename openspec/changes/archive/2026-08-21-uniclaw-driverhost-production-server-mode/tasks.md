# Tasks: uniclaw-driverhost-production-server-mode

> System of record. IMPLEMENTED (APPLY gate executed 2026-08-19).
> **State**: production --serve mode, options, and IntegrationTestHost implemented;
> targeted + DriverHost + RuntimeAgent tests green; manual serve-mode validated.
> Pending graduation review (no self-archive).

## Slices (this gate)

- [x] Slice 0 — OpenSpec change scaffolding (proposal/design/spec/README/.openspec.yaml)
- [x] Slice 1 — Verified source baseline (BuildDriverHostServer exists;
      UniClawDriverHostServer.Start/Dispose exist; Program.cs has NO --serve;
      BuildDriverHostServer called only from tests; E2E tests prove the path)
- [x] Slice 2 — Server-mode lifecycle design (start, cancellation, shutdown,
      disposal; Ctrl+C / SIGTERM / --timeout; finally Dispose)
- [x] Slice 3 — PhysicalHostOptions additions (--serve, --port, --timeout;
      minimal; no new config abstraction)
- [x] Slice 4 — Production-path testability design (RunServerModeAsync
      extraction for testability; no naming abstractions)
- [x] Slice 5 — IntegrationTestHost design (production composition path;
      NOT parallel test-only server)
- [x] Slice 6 — Frozen protocol protection clauses (Surface A/B/C/S, Hook
      Boundary, wire DTOs, JSON-RPC methods unchanged; no architecture promotion)
- [x] Slice 7 — Falsifier mapping F1–F10
- [x] Slice 8 — Explicit exclusions list (no UniAgent/AgentHost/Session/
      ISwitchStateReader/escalation/controls/multi-agent/rename)
- [x] Validation — openspec validate --strict, check-consistency.sh, buyer-doc
      cross-check

## Implementation plan (APPLY gate — EXECUTED 2026-08-19)

- [x] A1 — `PhysicalHostOptions`: add `Serve` (bool), `Port` (int, default 5177),
      `TimeoutSeconds` (int?, default null) options to `Parse` (minimal; no new
      abstraction)
      (`src/UniClaw.Runtime.PhysicalHost/PhysicalHostOptions.cs`)
- [x] A2 — `Program.cs`: add `--serve` branch in `Main` that calls
      `PhysicalHostComposition.BuildDriverHostServer(options, serverOptions)`,
      `server.Start()`, awaits cancellation, `finally server.Dispose()`
      (`src/UniClaw.Runtime.PhysicalHost/Program.cs` — RunServerModeAsync)
- [x] A3 — `Program.cs`: wire `Console.CancelKeyPress` + optional
      `--timeout` CancellationTokenSource to the server lifetime; exit 0 on clean
      shutdown
      (`src/UniClaw.Runtime.PhysicalHost/Program.cs` — RunServerModeAsync
      linked CTS + Ctrl+C handler)
- [x] A4 — Minimal extraction for testability: `RunServerModeAsync(
      PhysicalHostOptions, CancellationToken)` extracted from the `--serve`
      branch (local + minimal; no naming abstractions)
      (`src/UniClaw.Runtime.PhysicalHost/Program.cs`)
- [x] A5 — `tests/UniClaw.Runtime.Tests/Integration/ServerModeIntegrationTests.cs`
      (NEW): start production serve path (build --port 0, ephemeral) → connect raw
      JSON-RPC client → ping → run.start → read-only surface reachability → clean
      shutdown (uses `PhysicalHostComposition.BuildDriverHostServer` — the
      production composition; NOT a parallel test-only graph)
- [x] A6 — Verify existing proof modes unchanged (RunSlice1ProofAsync etc. still
      compile and run)
- [x] A7 — Full regression: build 0 errors; consistency ALL PASS; architecture
      guards ALL PASS; existing DriverHost + RuntimeAgent tests pass; no new
      failures attributable to this change

## Falsifier mapping

- [x] F1 — second server architecture → reuse BuildDriverHostServer (spec:
      server mode entry scenario) — PASS: `RunServerModeAsync` calls
      `PhysicalHostComposition.BuildDriverHostServer`
- [x] F2 — wire DTO/method alteration → no DTO/method changes (spec: frozen
      protocol surfaces unchanged) — PASS: no DriverHost/Runtime change
- [x] F3 — UniAgent/AgentHost/Session introduced → no new abstractions (spec:
      no architecture abstraction introduced) — PASS: only `RunServerModeAsync`
      (local private method); no new types
- [x] F4 — reserved extension purchased → no ISwitchStateReader/escalation/
      controls/multi-agent (spec: no architecture abstraction introduced) — PASS
- [x] F5 — RuntimeAgent authority bypass → server forwards only (spec: frozen
      protocol surfaces unchanged) — PASS: ServerModeIntegrationTests confirms
      run.start round-trip + read surfaces; RuntimeAgent untouched
- [x] F6 — DriverHost promoted to architecture → transport ingress only (design
      §0 explicit non-identity) — PASS
- [x] F7 — unclean shutdown → finally Dispose (spec: clean lifecycle scenario) —
      PASS: manual `--serve --timeout 2` → SHUTDOWN → stopped → exit 0
- [x] F8 — parallel test-only server graph → must use production composition
      (spec: production-path integration test scenario) — PASS: test uses
      `BuildDriverHostServer`
- [x] F9 — Agent rename → no rename (spec: no architecture abstraction scenario) —
      PASS
- [x] F10 — proof modes broken → proof modes unchanged (spec: existing proof
      modes scenario) — PASS: proof modes unchanged; --serve is an additive branch

## Manual production-path validation (--serve)

```
$ dotnet src/UniClaw.Runtime.PhysicalHost/bin/Debug/net10.0/uniclaw-physical-host.dll --serve --port 0 --timeout 2
DRIVERHOST DriverHost transport listening on 127.0.0.1:58542
SERVING port=58542
SHUTDOWN signal received; disposing server.
DRIVERHOST DriverHost transport stopped
exit code: 0
```

- `--serve` starts the existing DriverHost server (BuildDriverHostServer → Start).
- `--timeout 2` auto-shutdown triggers clean cancellation.
- Server disposed exactly once (`DRIVERHOST DriverHost transport stopped`).
- Process exits 0.
- Ctrl+C path uses the same cancellation (validated via code inspection; the
  `--timeout` path exercises the identical linked-CTS lifecycle).
