## 1. Qwen API Key Loading

- [x] 1.1 Add `LoadQwenApiKey()` method (env var + secrets.json fallback) to `HostCommands.cs`
- [x] 1.2 Verify key loads correctly from both env and secrets.json

## 2. Qwen Provider Registration

- [x] 2.1 Add `--provider qwen` branch in `CreateProviders()` with model default priority (`--model` > `UNICLAW_MODEL` > `QWEN_MODEL` > `"qwen3.7-plus"`)
- [x] 2.2 Add qwen to `ProviderReady()` without Model non-null constraint
- [x] 2.3 Add `"qwen"` to CLI usage help text
- [x] 2.4 Build and verify: `dotnet build src/UniClaw.Host` — 0 errors

## 3. Two-Stage Mode Support

- [x] 3.1 Read `UNICLAW_VISION_MODE` env var in qwen branch; if `"two_stage"`, register additional `"deepseek"` provider with `DEEPSEEK_MODEL` (default `deepseek-v4-flash-0731`)
- [x] 3.2 Add `UseTwoStagePageAnalyzer` flag to `UniBrainConfig` (default false)

## 4. Tests

- [x] 4.1 Run `dotnet test tests/UniClaw.Host.Tests --filter "HooksTests|EnginePathTests"` — pass
- [x] 4.2 Run `dotnet test tests/UniClaw.Core.Tests` — no regressions (992 tests)

## 5. Documentation

- [x] 5.1 Document Qwen env vars in help text or project README
