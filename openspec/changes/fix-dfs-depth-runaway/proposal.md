## Why

enumerate_first_level 场景在 local-vision provider 下 DFS 遍历深度失控。场景规定 `maxDepth=2`，但引擎实际进入 depth=4 的子页面（Settings → Network & internet → Internet → Wi‑Fi → Advanced），导致 maxSteps=120 耗尽，`settings_home_not_restored`。

根因：`TraversalEngine.Initialize()` 中 `TraversalRuntimeContext` 用硬编码 `_config.MaxDepth=10` 构造 `NodeStack`，而 `plan.IntentSlots.Depth=2` 仅在后续 `StepContext.EffectiveMaxDepth` 中使用。NodeStack 允许 Push 到深度 10，ContainerHandler 在深度 3 才喊停——但子帧已被推入栈、访问、记录。

## What Changes

- **P1: NodeStack 尊重 EffectiveMaxDepth** — `TraversalEngine.Initialize()` 中 `effectiveMaxDepth` 计算提前到 `TraversalRuntimeContext` 构造之前，传给 `maxDepth` 参数。`NodeStack` 在 `depth >= effectiveMaxDepth` 时拒绝 Push，从源头阻止深度失控。
- **P2: (附带) ContainerHandler 使用同一值** — 复用提前计算的 `effectiveMaxDepth`，去掉重复计算。

## Capabilities

### New Capabilities

（无。此变更系 bug 修复，不引入新功能能力。）

### Modified Capabilities

（无。修复实现层使引擎遵守 plan 的 depth 约束。已有的 `ContainerHandler.MaxDepth` 检查保持不变作为 defense-in-depth。）

## Impact

- `src/UniClaw.Core/Traversal/TraversalEngine.cs` — `Initialize()` 方法，~5 行调整
- `tests/UniClaw.Core.Tests/Simulation/TraceReplay/` — 验证修复的仿真测试（已创建，当前 FAIL → 修复后 PASS）
- `tests/UniClaw.Core.Tests/Simulation/TraceReplay/FixVerificationTests.cs` — L2 DepthConstraint 测试
- `tests/UniClaw.Core.Tests/Simulation/TraceReplay/SettingsEnumerateRegression.cs` — 永久基线

上层 PRD: `docs/prd/2026-08-05-verify-evidence-chain-fix-prd.md`（P0 视觉层修复已独立完成）
