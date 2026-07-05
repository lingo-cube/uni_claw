# Proposal: Phase 2.3-sim — Simulation Infrastructure Migration

## Why

C# 遍历引擎有完整的 FSM（8 handler）、Context（30 可变状态）、StepOrchestrator（14-step 编排），但无法端到端运行——`IVisionProvider` 只有 placeholder 接口，`IActionExecutor` 无仿真实现。2.3a 的 HandleExecute/HandleBranch 只能做单元测试，2.3b/2.3c 依赖视觉/操作服务却无平台可验证。Python `src/simulation/`（~3,154 行）已证明：用 StatefulMock 服务注入真实引擎，可以在零外部依赖下跑通完整遍历循环。迁移其核心 3 组件可让 C# 端立即获得端到端测试能力。

## What Changes

- **补全 `IVisionProvider` 接口**（2 方法）：`AnalyzeCurrentPageAsync` + `FindAppEntryAsync`，对齐 Python `VisionService` ABC
- **新增 `Simulation/` namespace**（3 组件）:
  - `StateFixture`: 页面状态 + 跳转规则数据模型，JSON 反序列化 + Fluent Builder
  - `StatefulMockVisionService : IVisionProvider`: 状态感知页面模拟，内部维护 `_currentPageId` 状态机
  - `StatefulMockActionExecutor : IActionExecutor`: 联动 vision 的操作模拟，Tap→FindElement→SimulateAction 链路
- **StepOrchestrator 一行修改**: `Step()` → `Step(ctx)`，使 FSM handlers 能通过 StepContext 访问 Vision/Action
- **新增 `SimpleNodeRegistry`**: 测试用 INodeRegistry，字典存储 TraversalNode
- 零新 NuGet 依赖（JSON 反序列化使用已有 `System.Text.Json`）

## Capabilities

### New Capabilities

- `simulation-infra`: StateFixture 数据模型 + StatefulMockVisionService + StatefulMockActionExecutor。提供仿真所需的页面状态定义、状态感知 mock 服务、以及端到端遍历测试能力。对齐 Python `src/simulation/` 核心 3 组件。

### Modified Capabilities

- `traversal-fsm`: IVisionProvider 接口从 1 方法扩展到 2 方法（`AnalyzeCurrentPageAsync` + `FindAppEntryAsync`），新增 `AppEntryPoint` record 类型。**BREAKING**: 接口方法重命名（`GetCurrentPageAnalysisAsync` → `AnalyzeCurrentPageAsync`），需更新所有实现。
- `step-orchestrator`: 第 41 行 `Step()` → `Step(ctx)`，传递 StepContext 给 FSM handlers。

## Impact

| Module | Impact |
|--------|--------|
| `src/UniClaw.Core/StateMachine/StepContext.cs` | IVisionProvider 接口补全 + AppEntryPoint |
| `src/UniClaw.Core/Simulation/` (new) | StateFixture + StatefulMockVision + StatefulMockAction |
| `src/UniClaw.Core/Traversal/StepOrchestrator.cs` | 一行修改 |
| `tests/.../StateMachine/MockVisionProvider.cs` | 方法重命名适配新接口 |
| `tests/.../Simulation/` (new) | 单元测试 + E2E 测试 |
| `docs/system/layers/state-machine.md` | IVisionProvider 文档更新 |
