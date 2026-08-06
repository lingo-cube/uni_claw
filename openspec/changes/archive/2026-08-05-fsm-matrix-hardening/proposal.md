## Why

TraversalFSM 的转移矩阵承担了两个未明说的职责——Handler 门（handler 返值合法性）和异常路由门（StepAsync catch 的 ErrorHandling 路由）——导致 3 条死边（无 handler 生产方）、错误恢复计数路径间不一致（+1/+2 取决于路径）、LastError 跨恢复残留、异常路由对 ErrorHandling 自身不安全（HandleErrorHandlingAsync 内部 trace 写入失败 → DomainValidationException → 遍历会话崩溃）。fsm-analyzer 双轨分析（静态矩阵审计 + E2E run 诊断）确认了全部缺陷，且修复零现有测试破坏。

## What Changes

- **移除 TransitionMatrix 3 条死边**：Execute→Branch、Branch→PreconditionCheck、FrameComplete→ErrorHandling。矩阵从 22 边缩减到 19 边，每条边均有至少一个 handler 显式返回
- **StepAsync catch 加 CanTransitionTo 守卫 + 按状态降级链**：异常路由不再硬编码 ErrorHandling。对不含 ErrorHandling 出边的 3 个状态（NodeSelect→Branch, FrameComplete→NodeSelect, ErrorHandling→FrameComplete）按合法降级目标安全退出，不再崩溃
- **ConsecutiveErrors 递增收敛到 HandleErrorHandlingAsync 单点**：移除 StepAsync catch、HandlePreconditionCheckAsync、HandleExecuteAsync 的递增调用，语义从"出错次数"修正为"恢复尝试次数"，所有路径一致 +1/周期
- **LastError 在 HandleErrorHandlingAsync 全部 3 条返回路径清零**：错误处置完毕即清场，消除跨恢复残留
- **HandlePopupHandlingAsync 失败时设置 LastError**：弹窗 dismiss 失败不再让 ErrorHandler 无上下文决策；消息不含枚举名以防 ErrorClassifier substring 碰撞
- **新增 6 个测试（T1-T6）**：覆盖降级守卫、3 返回点清零、全周期递增、弹窗失败 LastError、OnErrorAsync 钩子触发、死边拒绝
- **零现有测试破坏**（237 tests pass）

## Capabilities

### New Capabilities
<!-- None — all changes are modifications to existing FSM behavior, no new standalone capability -->

### Modified Capabilities
- `traversal-fsm`: TransitionMatrix 缩至 19 边；异常路由从无条件 ErrorHandling 改为 CanTransitionTo 守卫 + 降级链；错误生命周期的入口/出口契约（递增收敛 + LastError 清零）
- `handler-error-handling`: ConsecutiveErrors 递增从多处收敛到 HandleErrorHandlingAsync 单一入口；LastError 全部 3 条返回路径清零
- `handler-popup-handling`: 弹窗 dismiss 失败时设置描述性 LastError

## Impact

- `src/UniClaw.Core/StateMachine/TraversalFSM.cs` — ~30 行源码变更（矩阵定义 + StepAsync catch + 4 处递增位置变更 + LastError 清零 + Popup LastError 设置）
- `tests/UniClaw.Core.Tests/StateMachine/StateMachineTests.cs` — 新增 T1/T1a/T6（~100 行）
- `tests/UniClaw.Core.Tests/StateMachine/HandleErrorHandlingTests.cs` — 新增 T2/T3（~100 行）
- `tests/UniClaw.Core.Tests/StateMachine/HandlePopupHandlingTests.cs` — 新增 T4/T5（~60 行）
- `docs/system/patterns/fsm-design.md` — 矩阵表更新 + 异常路由降级机制说明（~20 行）
