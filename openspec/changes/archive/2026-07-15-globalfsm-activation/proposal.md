## Why

`GlobalFSM` 类已完整实现 80 行（8 状态转换矩阵、`TransitionTo()`、`RegisterStateCallback`、`GetTransitionHistory`），但整个代码库中 `new GlobalFSM()` 零次被调用。引擎绕过它，通过 `SessionContext.GlobalState` 的 public setter 直接写字段——无矩阵校验、无回调触发、无历史记录。`RegisterStateCallback` 零消费者，`TransitionRecord` 历史永远空，`StateTransition.FsmType` 永远只是 `"TraversalFSM"`。B2 的核心工作是**激活已有代码**，不是新增功能。

## What Changes

- **`SessionContext` 废除 `GlobalState` 的 public setter**：raw `GlobalState` 字段替换为 `GlobalFSM` 实例；`GlobalState` 属性变为只读（`=> _globalFsm.CurrentState`）
- **`GlobalFSM` 新增 `internal ForceState()`**：PopupHandler 状态恢复场景下绕过矩阵（语义是"撤销"，不是"转换"），不触发 callback 但记录历史
- **`SetGlobalState` 路由到 `TransitionTo`**：正常状态变更走矩阵校验 + 回调 + 历史；`ForceGlobalState` 为内部恢复保留
- **`TraversalEngine` 注册 trace callback**：GlobalFSM 转换通过 `RegisterStateCallback` 写入 `ITraceRecorder`，`FsmType = "GlobalFSM"`
- **BREAKING — 无**：对外接口不变；`SetGlobalState(GlobalState)` → `SetGlobalState(GlobalState, string?)` 是加可选参数，旧调用点兼容

## Capabilities

### New Capabilities
_(无 — 激活已有代码，不新增功能)_

### Modified Capabilities
- `traversal-fsm`: `GlobalFSM` 从"零实例化"变为"被 `SessionContext` 持有并激活"；全局状态变更走矩阵校验的 `TransitionTo()` 替代直写字段；新增 `ForceState` 语义区分正常转换与状态恢复

## Impact

- **修改**: `SessionContext.cs`（GlobalState setter 删除 + GlobalFSM 实例）、`GlobalFSM.cs`（+ForceState）、`TraversalRuntimeContext.cs`（SetGlobalState 双层 API）、`TraversalEngine.cs`（注册 trace callback）、`PopupHandler.cs`（改用 ForceGlobalState）
- **依赖**: 零新增。所有改动在 StateMachine 层内或 Traversal→StateMachine（已有依赖）
- **风险**: `TransitionTo` 抛 `DomainValidationException` 若矩阵不匹配 — 已验证 2 个调用点（Traversing→Completed：✅，Traversing→Error：✅），PopupHandler 走 ForceState 不受影响
- **详细设计**: 见 `docs/refactor/2026-07-15-globalfsm-activation-design.md`
