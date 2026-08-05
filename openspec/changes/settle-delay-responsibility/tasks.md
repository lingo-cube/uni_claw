## 1. Core: remove production engine delay

- [x] 1.1 `HostCommands.cs`: change `DelayPerStepMs = 300` → `DelayPerStepMs = 0`

## 2. Host: add settle in PageInvalidatingActionExecutor

- [x] 2.1 `InvalidatingPageAnalysisCache.cs` — `PageInvalidatingActionExecutor`: add `_settleDelayMs` field and `settleDelayMs` constructor parameter (default 300)
- [x] 2.2 `InvalidatingPageAnalysisCache.cs` — `ExecuteAsync`: after `_invalidate()` and `onSuccess?.Invoke()`, add `if (_settleDelayMs > 0) await Task.Delay(_settleDelayMs, cancellationToken)`

## 3. Host: wire config

- [x] 3.1 `HostCommands.cs`: read `UNICLAW_SETTLE_DELAY_MS` env var, parse to int, default 300
- [x] 3.2 `HostCommands.cs`: pass `settleDelayMs` to `PageInvalidatingActionExecutor` constructor

## 4. Docs

- [x] 4.1 `integration-config.md`: register `UNICLAW_SETTLE_DELAY_MS` as L4 (done during PRD review)

## 5. Verify

- [x] 5.1 `dotnet build src/UniClaw.Host -c Debug` — build passes (0 warnings, 0 errors)
- [x] 5.2 `dotnet test tests/UniClaw.Core.Tests --filter "FullyQualifiedName~TraversalEngine"` — no regressions (25/25 passed)
- [ ] 5.3 Run scenario-locate integration test (host-test-runner skill) — **needs emulator + vision server running**

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Core/Traversal/` | [docs/system/layers/traversal.md](../../docs/system/layers/traversal.md) |
| `src/UniClaw.Host/` | [docs/system/layers/host.md](../../docs/system/layers/host.md) |
| Config contract | [docs/testing/integration-config.md](../../docs/testing/integration-config.md) |
