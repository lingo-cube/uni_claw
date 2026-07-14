## Context

遍历管道的热路径全部同步但内部调异步 I/O：`IActionExecutor` 和 `IVisionProvider` 从设计之初就是 `Task` 返回，但上层的 FSM（`Step()`）和 Orchestrator（`ExecuteStep()`）没有跟上，全部用 `.GetAwaiter().GetResult()` 同步阻塞。24 处阻塞分布在 `TraversalFSM.cs`、`StepOrchestrator.cs`、`TraversalEngine.cs` 三文件。真机 ADB 截图延迟 0.5-5s，同步阻塞会死锁线程池。

滑动坐标硬编码为 5 个 `const`（`0.5, 0.7 → 0.5, 0.3, 300ms`），注释标注为"v1 默认值，延后项 §13"。Mock 只看方向不读坐标值，真机不同 App 滚动区域位置不同。

当前 575 测试全绿，baseline 测试通过 `ExpectedBehavior` 验证遍历行为。

## Goals / Non-Goals

**Goals:**
- 消除全部 24 处 `.GetAwaiter().GetResult()`，热路径全链路 async/await
- 滑动坐标从硬编码 `const` 变为可配置，引擎级默认 + 页面级覆盖，默认值不变
- Mock（`SimulatedScreen` + `ScrollableMockVisionService`）按页面适配不同滚动区域
- 所有 575 现有测试通过，baseline `NumericAnchor` 不重新标定

**Non-Goals:**
- 不修改 `IActionExecutor` / `IVisionProvider` / `IGraphTraversalEngine` 接口契约
- 不涉及 FSM 状态空间或迁移矩阵变更（C-4 不受影响）
- 不加 `ScrollSwipeConfig` 到 `ChildrenStrategy` 实现节点级覆盖（延后到真机需要时）
- 不加时序等待模型（已延后）
- 不修改 `PageAnalysis` 或 Domain 层

## Decisions

### D1: 全链路 async/await，不保留同步包装

**选择**: `Step()` → `StepAsync()`，`ExecuteStep()` → `ExecuteStepAsync()`，`Run()` 删除。所有 Handler 改为 `async Task<TraversalState>`。
**替代方案**: 保留 `Run()` 作为 `RunAsync().GetAwaiter().GetResult()` 包装 → 拒绝，因为保留死锁风险入口且制造源码真值歧义（有时 async，有时 sync wrapper）。
**理由**: 消除歧义源。调用方直接 `await RunAsync()` 语义清晰，线程模型正确。

### D2: 6 个纯同步 Handler 也改 async 签名

**选择**: 全部 8 个 Handler 统一为 `async Task<TraversalState>`。
**替代方案**: 只改有 I/O 的 `HandleExecute` 和 `HandleResultVerify`，其他保持同步 → 拒绝，因为 DispatchHandlerAsync 需要统一返回类型。
**理由**: `DispatchHandlerAsync` 的 switch 表达式需要同一返回类型。纯同步 Handler 内部不 await，但签名统一。

### D3: ScrollSwipeConfig 配置层级：引擎默认 → Vision 页面覆盖

**选择**: `TraversalEngineConfig.ScrollSwipe` 作为引擎默认，`IVisionProvider.GetScrollSwipeConfig()` 作为页面级覆盖（virtual, 默认 null = 用引擎配置）。
**替代方案**: 只在 `TraversalEngineConfig` 配 → 拒绝，因为不同页面可能不同滚动区域。
**替代方案**: 从 `PageAnalysis` AI 推导 → 拒绝，因为过度设计，95% 默认值够用。
**理由**: 两层够用，不改 AI 接口。Mock 和真机都覆写 `GetScrollSwipeConfig()`。

### D4: ScrollSwipeConfig 放在 Traversal 命名空间

**选择**: `UniClaw.Core.Traversal.ScrollSwipeConfig`。
**替代方案**: `Domain.Models.Common` → 拒绝，因为这是 Traversal 层配置，不是 Domain 模型。
**理由**: 与 `TraversalEngineConfig` 同层。StateMachine → Traversal 向上引用已被 D-14/D-17 承认，加一个类型引用不改变依赖图。

## Risks / Trade-offs

- **[Risk] ~50 测试签名变更可能引入编译错误** → 机械性修改，逐个替换 `engine.Run()` → `await engine.RunAsync()`，`void` → `async Task`，编译器会指出遗漏点
- **[Risk] async 异常传播路径与同步不同** → `StepAsync()` 的 try-catch 已经包装了 `await DispatchHandlerAsync()`，异常路由到 `ErrorHandling` 逻辑不变
- **[Trade-off] StepContext 加第 15 个字段** → 构造点仅 `TraversalEngine.RunAsync` 一处，影响可控
- **[Trade-off] TraceCoordinator 改 async 但 trace 路径测试覆盖率偏低** → `LogAndContinue` 的 try-catch 保持异常安全，trace 失败不影响遍历
