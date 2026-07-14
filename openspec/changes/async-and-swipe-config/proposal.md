## Why

遍历管道热路径全部同步但内部调异步 I/O（24 处 `.GetAwaiter().GetResult()`），真机 ADB 截图需要 0.5-5s，同步阻塞会死锁线程池。同时滑动坐标硬编码为 `(0.5, 0.7) → (0.5, 0.3)`，不同 App 的滚动区域位置不同，真机上滑不到目标区域。两个问题不解决，真机运行不可行。

## What Changes

- **BREAKING**: 删除 `TraversalEngine.Run()` 同步包装器，所有调用方改用 `await RunAsync()`
- **BREAKING**: `TraversalFSM.Step()` 改为 `StepAsync()`，返回 `Task<TraversalState>`
- **BREAKING**: `StepOrchestrator.ExecuteStep()` 改为 `ExecuteStepAsync()`，返回 `Task<StepResult>`
- 8 个 FSM Handler 全部改为 async（返回 `Task<TraversalState>`），其中 `HandleExecuteAsync` 和 `HandleResultVerifyAsync` 内部消除 `.GetAwaiter().GetResult()`
- `TryHandleScroll` 改为 `TryHandleScrollAsync`，内部异步调用 `SwipeAsync` 和 `AnalyzeCurrentPageAsync`
- `TraceCoordinator.LogAndContinue` 改为 async，15 个 `Record*` 方法改为 `async Task`
- 新增 `ScrollSwipeConfig` record class（归一化坐标 + 持续时间，默认值 = 当前硬编码值）
- `TraversalEngineConfig` 加 `ScrollSwipeConfig` 字段作为引擎级默认
- `IVisionProvider` 加 `virtual GetScrollSwipeConfig()` 方法支持页面级覆盖
- `SimulatedScreen.WithScrollablePage()` 加可选 `ScrollSwipeConfig` 参数
- `ScrollableMockVisionService` 覆写 `GetScrollSwipeConfig()`
- 删除 `StepOrchestrator` 中 5 个硬编码 `const` 坐标常量

## Capabilities

### New Capabilities
- `scroll-swipe-config`: 滑动坐标从硬编码常量提升为可配置 `ScrollSwipeConfig`，支持引擎级默认 + `IVisionProvider` 页面级覆盖。Mock 通过 `SimulatedScreen.WithScrollablePage()` 按页面适配不同滚动区域。

### Modified Capabilities
- `traversal-fsm`: `Step()` → `StepAsync()`，`DispatchHandler` → `DispatchHandlerAsync`，全部 8 个 Handler 改为 async 返回 `Task<TraversalState>`
- `step-orchestrator`: `ExecuteStep()` → `ExecuteStepAsync()`，`TryHandleScroll()` → `TryHandleScrollAsync()`，删除 5 个硬编码 `const` 坐标常量，改为读取 `ScrollSwipeConfig`
- `traversal-engine`: 删除 `Run()` 同步包装器，`LogAndContinue` → async，15 个 `Record*` → `async Task`
- `scroll-aware-traversal`: 滑动坐标来源从硬编码常量改为 `ctx.Vision.GetScrollSwipeConfig() ?? ctx.ScrollSwipe`

## Impact

- `src/UniClaw.Core/StateMachine/TraversalFSM.cs` — Step/Handler 签名变更
- `src/UniClaw.Core/StateMachine/StepContext.cs` — IVisionProvider +ScrollSwipeConfig, StepContext +字段
- `src/UniClaw.Core/Traversal/StepOrchestrator.cs` — ExecuteStep/TryHandleScroll 异步化 + 删除 consts
- `src/UniClaw.Core/Traversal/TraversalEngine.cs` — Run() 删除, TraceCoordinator 异步化
- `src/UniClaw.Core/Traversal/TraversalEngineConfig.cs` — +ScrollSwipeConfig
- `src/UniClaw.Core/Traversal/ScrollSwipeConfig.cs` — **新文件**
- `src/UniClaw.Core/Simulation/Scroll/SimulatedScreen.cs` — +字典 + WithScrollablePage 参数
- `src/UniClaw.Core/Simulation/Scroll/ScrollableMockVisionService.cs` — 覆写 GetScrollSwipeConfig
- `tests/UniClaw.Core.Tests/` — ~50 测试签名 `void` → `async Task`, `engine.Run()` → `await engine.RunAsync()`
- Constitution: C-4（FSM 独立性）不受影响 — 只改签名不改状态空间
