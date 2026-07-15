## Why

`StepOrchestrator.ExecuteStepAsync` 是 197 行的单体方法，14 个步骤中混杂了两类截然不同的职责：编排（trace 生命周期、FSM 调度、visited 记账 ~90 行）和拦截（branch/dynamic/frame override + 导航检测 + 滚动判断 ~107 行 + 109 行私有方法）。拦截逻辑通过 `ref` 参数修改调用方的局部变量，无法独立测试。`ITraceCoordinator` 已通过接口完全解耦、`StateUpdater` 仅 1 行，机械地拆 4 组件违反 YAGNI。D-IV 应聚焦唯一痛点：提取 `InterceptionHandler`。

## What Changes

- **新建 `IInterceptionHandler` 接口**：3 方法 — `OnBranch(StepContext, TraversalState)`、`OnDynamicMatchNodeSelect(StepContext)`、`OnFrameComplete(StepContext)` — 各返回 `InterceptionResult`
- **新建 `InterceptionResult` record struct**：4 字段 — `NextState`、`ChildPushed`、`FrameCompleted`、`FrameOverrideTriggered` — 值类型替代 3 个 `ref bool` + 1 个 `ref TraversalState`
- **新建 `InterceptionHandler` 类**：实现 `IInterceptionHandler`，搬入 216 行拦截逻辑（步骤 8-10 + `TryHandleNavigation` + `TryHandleScrollAsync` + `FromFrame` + `GetElementIds` + `_lastPushedChildNodeId`）
- **`StepOrchestrator` 简化**：366 → ~120 行，删除所有 override 逻辑；步骤 8-10 改为委托 `_handler.OnXxx()`；保留 14-step 生命周期（trace + FSM + path + visited）
- **BREAKING — 无**：对外接口（`IGraphTraversalEngine` 等）不变；`StepOrchestrator` 公共 API `ExecuteStepAsync` 签名不变；无新 enum/接口方法

## Capabilities

### New Capabilities
_(无 — 纯架构重构，不新增功能)_

### Modified Capabilities
- `step-orchestrator`: StepOrchestrator 从单体 14-step 方法拆分为 StepOrchestrator（生命周期编排） + InterceptionHandler（FSM 拦截/覆盖）。新增 `IInterceptionHandler` 接口和 `InterceptionResult` 值类型。拦截逻辑可通过接口 mock 独立测试。

## Impact

- **新建**: `IInterceptionHandler.cs` (3 方法 interface + `InterceptionResult` struct)、`InterceptionHandler.cs` (~250 行，从 StepOrchestrator 搬入)
- **修改**: `StepOrchestrator.cs` (删除 216 行拦截逻辑 + 私有方法 + `_lastPushedChildNodeId`；添加 `_handler` 字段 + 3 个委托调用 → ~120 行)、`TraversalEngine.cs` (构造器注入 `new InterceptionHandler()`)、`ArchitectureGuardTests.cs` (新 guard)
- **依赖**: 无新增外部依赖。`InterceptionHandler` 所有依赖来自 `StepContext`（已有），不引入新耦合。依赖方向：`StepOrchestrator → IInterceptionHandler → StepContext`（单向，零循环）
- **风险**: 纯机械搬移，每步 `dotnet build` 验证。`ref` 参数 → `InterceptionResult` 语义等效。`TryHandleScrollAsync` 从 `internal static` → `private`（已确认仅步骤 8/9 内联使用）
- **详细设计**: 见 `docs/refactor/2026-07-15-steporchestrator-decomposition-design.md`
