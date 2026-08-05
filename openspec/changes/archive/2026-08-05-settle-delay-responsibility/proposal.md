## Why

`DelayPerStepMs = 300ms` fires unconditionally at the engine loop head every step — including non-action steps (decision, verification retry, cached reads). The delay is intended for UI settle after device actions, but placed at the loop head it also misses ResultVerify's first after-screenshot (which has zero settle window, relying on a ~4.5s retry as implicit bailout). The engine should not manage UI timing — the component that executes device actions should.

详见 [docs/prd/2026-08-05-settle-delay-responsibility-prd.md](../../docs/prd/2026-08-05-settle-delay-responsibility-prd.md)。

## What Changes

- **Remove** production engine delay: `TraversalEngineConfig.DelayPerStepMs = 300` → `0`
- **Add** settle wait in `PageInvalidatingActionExecutor.ExecuteAsync`: after successful ADB operation and cache invalidation, `Task.Delay(settleDelayMs)` before returning
- **Configure** via `UNICLAW_SETTLE_DELAY_MS` env var (default 300ms, `0` to disable)
- No **BREAKING** changes — `DelayPerStepMs` property preserved, engine guard `if > 0` preserved, all tests set their own config values

## Capabilities

### New Capabilities
- `action-settle-delay`: `PageInvalidatingActionExecutor` waits for UI settle after successful device operations (tap/swipe/back/input_text/long_press), controlled by constructor parameter `settleDelayMs` and `UNICLAW_SETTLE_DELAY_MS` env var

### Modified Capabilities
<!-- None — existing spec-level behavior unchanged -->

## Impact

| Module | File | Change |
|--------|------|--------|
| Host Runner | `src/UniClaw.Host/Runner/InvalidatingPageAnalysisCache.cs` | `PageInvalidatingActionExecutor`: new `settleDelayMs` ctor param, `Task.Delay` in `ExecuteAsync` |
| Host Commands | `src/UniClaw.Host/Commands/HostCommands.cs` | `DelayPerStepMs: 0`; read `UNICLAW_SETTLE_DELAY_MS`; pass to executor ctor |
| Core Engine | `src/UniClaw.Core/Traversal/TraversalEngine.cs` | Unchanged (guard `> 0` retained) |
| Core Config | `src/UniClaw.Core/Traversal/TraversalEngineConfig.cs` | Unchanged |
| Core Interface | `src/UniClaw.Core/Traversal/IGraphTraversalEngine.cs` | Unchanged (`WaitAsync` exists) |
| Config Doc | `docs/testing/integration-config.md` | Register `UNICLAW_SETTLE_DELAY_MS` as L4 |
