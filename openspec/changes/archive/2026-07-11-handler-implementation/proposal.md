## Why

TraversalFSM 8 state 中有 4 个 handler 是 stub (HandlePreconditionCheck, HandleResultVerify, HandleErrorHandling, HandlePopupHandling)，另有 HandleFrameComplete 缺少 stack pop/frame teardown 逻辑。这些 stub 阻塞主循环闭环 — 遍历引擎无法跑完整遍历流程。在架构重构 (D-I/D-V/D-IV) 开始前，必须先有稳定的 FSM 闭环测试基线。

## What Changes

- **HandlePreconditionCheck**: 从 stub (always→Execute) 升级为实装 — 检查 precondition 条件，路由到 Execute 或 ErrorHandling
- **HandleResultVerify**: 从 stub (always→Branch) 升级为实装 — 3-round retry + vision correction, popup 检测分流到 PopupHandling
- **HandleErrorHandling**: 从 stub (always→NodeSelect) 升级为实装 — 5-strategy recovery (Retry→Execute, Backtrack→NodeSelect, Skip→Branch, Continue→NodeSelect, Abort→FrameComplete)
- **HandlePopupHandling**: 从 stub (always→ResultVerify) 升级为实装 — popup 检测 + PopupHandler 6-step dismiss pipeline + ResultVerify/ErrorHandling 分流
- **HandleFrameComplete**: 从 minimal (return NodeSelect) 增强 — 加入 stack pop + frame teardown + visited bookkeeping
- 每个 handler 实装伴随单元测试

## Capabilities

### New Capabilities

- `handler-result-verify`: ResultVerify handler 实装 — 3-round retry + vision correction + popup 检测分流
- `handler-error-handling`: ErrorHandling handler 实装 — 5-strategy recovery + consecutive error tracking
- `handler-popup-handling`: PopupHandling handler 实装 — popup 检测 + 6-step dismiss + ErrorHandling fallback

### Modified Capabilities

- `traversal-fsm`: 4 handler 从 stub 升级为实装 + HandleFrameComplete 增强 (FSM transition 行为变更 — 从 unconditional single-state → conditional multi-path)
- `step-orchestrator`: HandleFrameComplete stack pop/teardown 逻辑可能在 StepOrchestrator 而非 FSM handler 中 (待 design 确认)

## Impact

- **代码**: `src/UniClaw.Core/StateMachine/TraversalFSM.cs` (4 handler + 1 增强), 可能涉及 `StepOrchestrator.cs` (FrameComplete 处理)
- **依赖**: RecoveryExecutor (5 ErrorStrategy hooks), PopupHandler (PopupClassifier + PopupActionExecutor), IVisionProvider (re-verification)
- **测试**: 每个实装 handler 需要 2-5 个单元测试 + FSM integration test
- **API**: 无 public API 变更 — handler 是 TraversalFSM 内部方法
- **Guard tests**: 不需要新 Guard — handler 行为是逻辑而非值数锁定
