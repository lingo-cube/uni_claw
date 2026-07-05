# M-14 评估：GlobalState 在 ITraversalContext 的跨 FSM 依赖

> **评估日期**: 2026-07-04
> **评估人**: Phase 2.1b review
> **结论**: 当前不改，Phase 3 前评估是否移除

---

## 问题描述

设计文档/spec 要求"TraversalFSM MUST NOT import GlobalFSM types"，但 ITraversalContext 接口上有 `GlobalState { get; set; }` 属性。TraversalFSM 通过 ITraversalContext 使用 GlobalState，创建了类型级依赖。

虽然 TraversalFSM 不直接 `using GlobalFSM`（GlobalState enum 在同一 namespace `UniClaw.Core.StateMachine`），但语义上 TraversalFSM（微步骤层）和 GlobalFSM（宏会话层）应保持独立。

---

## 当前影响分析

### 直接影响
- TraversalFSM.step() 中的 try-catch 会设置 `context.GlobalState = GlobalState.Error`
- TraversalFSM.step() 中的 handler 方法通过 ITraversalContext.GlobalState 读取当前全局状态
- ErrorHandler 恢复时通过 ITraversalContext.GlobalState 设置恢复后状态
- StateRestorer 恢复时通过 ITraversalContext.GlobalState 设置恢复后状态

### 受影响的消费者
- TraversalFSM.cs — 通过 ITraversalContext 读/写 GlobalState
- StepOrchestrator.cs — 通过 TraversalRuntimeContext（不通过接口）读 GlobalState
- ContainerHandler.cs — 通过 ITraversalContext 读 GlobalState（ContainerHandler.ContainerContext 引用 ITraversalContext）
- ErrorHandler.cs — 通过 StrategySelectionContext 读（不通过 ITraversalContext）
- PopupHandler.cs — 通过 ITraversalContext 读/写 GlobalState（preserve/restore）
- TraversalRuntimeContext.cs — GlobalState 是 26 字段之一

### 如果移除的影响
- 需要修改 ITraversalContext 签名（breaking change）
- TraversalFSM 需要从 TraversalRuntimeContext（具体类）而非接口读取 GlobalState
- 所有 handler 测试需要用 TraversalRuntimeContext 而非 ITraversalContext mock
- PopupHandler 的 preserve/restore 无法通过接口操作 GlobalState

---

## 评估结论

| 选项 | 优点 | 缺点 | 推荐度 |
|------|------|------|--------|
| A: 保持现状 | 最小改动，当前可工作 | 违反 spec 的"两个 FSM 不共享类型"原则 | ❌ 不推荐长期 |
| B: GlobalState 改为 engine-only 属性（仅 TraversalRuntimeContext 有） | 消除 ITraversalContext 上的跨 FSM 类型 | Breaking change，FSM/handler 不能通过接口读 GlobalState | ⭐ 推荐 Phase 3 |
| C: GlobalState 从 ITraversalContext 移到独立 IGlobalStateContext | 类型隔离，FSM 通过专用接口访问 | 新增接口，增加复杂度 | ⭐ 可考虑 Phase 3 |
| D: 引入 GlobalFSM 回调机制 | FSM 通过回调而非直接赋值 | 大幅改变 FSM 交互模式 | ❌ 过度设计 |

**结论**：当前不改。Phase 3 时评估方案 B（GlobalState 改为 engine-only 属性），配合 TraversalFSM 使用 TraversalRuntimeContext 直接引用而非接口。

**风险**：当前状态虽然违反 spec 原则，但实际运行无问题（GlobalState enum 在同一 namespace，不需要额外 using）。这是一个**设计意图违反**而非**运行时缺陷**，优先级低于硬约束修正。
