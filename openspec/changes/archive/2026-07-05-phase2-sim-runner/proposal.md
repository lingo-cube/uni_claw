# Proposal: Phase 2.3-sim-runner — SimulationRunner Automation

## Why

Phase 2.3-sim 已交付核心 3 组件（StateFixture + StatefulMockVisionService + StatefulMockActionExecutor），E2E 测试证明了 3 级页面遍历可行（489 tests pass）。但 E2E 测试代码中大量手动管理 FSM 状态转换、节点压栈、visited 标记——每个节点需要 6-8 行样板代码。Python `SimulationRunner`（362 行）通过一个 while 循环自动驱动 `GraphTraversalEngine`，C# 端缺少等价自动编排层。`StepOrchestrator.ExecuteStep(ctx)` 已经封装了所有跨步逻辑（BRANCH 拦截、子节点发现、visited 标记），只需一个轻量 runner 循环调用即可消除手动样板。

## What Changes

- **新增 `SimulationRunner`**: 封装 while 循环，自动调用 `StepOrchestrator.ExecuteStep(ctx)`，根据终止条件（`FrameCompleted + 栈深度 ≤ 1` 或 `AntiLoopTriggered` 或 `MaxSteps`）停止
- **新增 `SimulationConfig`**: 配置 record（MaxSteps、MaxDepth、ThrowOnError、SimulateDelayMs）
- **新增 `SimulationResult`**: 结果 record（Success、CompletionReason、TotalSteps、ElapsedSeconds、ActionHistory、VisitedPages、FinalState、Error）
- **重构 E2E 测试**: 用 Runner 替代手动循环，~120 行 → ~30 行，净减少 ~40 行

## Capabilities

### New Capabilities

- `simulation-runner`: SimulationRunner 自动化仿真驱动层，复用真实 StepOrchestrator 完成端到端遍历。~150 行 C#。

### Modified Capabilities

- `simulation-infra`: 在 StateFixture + StatefulMock* 基础上新增 SimulationConfig / SimulationResult / SimulationRunner 三种类型。

## Impact

| Module | Impact |
|--------|--------|
| `src/UniClaw.Core/Simulation/` | 新增 3 个文件（~155 行） |
| `tests/.../Simulation/SimulationE2ETests.cs` | 重构为 Runner 驱动测试 |
| `docs/system/layers/simulation.md` | 新增类型到清单 |
