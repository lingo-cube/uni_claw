## Context

`GlobalFSM` 已完整实现（80 行，8 状态转换矩阵，callback 机制，历史记录），但零次被实例化。引擎绕过它直接写 `SessionContext._globalState` 字段。B2 = 激活已有代码。完整设计见 `docs/refactor/2026-07-15-globalfsm-activation-design.md`。

## Goals / Non-Goals

**Goals:**
- `SessionContext` 持有 `GlobalFSM` 实例，废除 `GlobalState` public setter
- 所有状态变更走 `TransitionTo()`（矩阵校验 + 回调 + 历史）
- GlobalFSM 转换写入 trace（`FsmType = "GlobalFSM"`）
- `ForceState` 内部恢复路径（PopupHandler），不触发 callback

**Non-Goals:**
- 不改 8 状态矩阵
- 不改 TraversalFSM
- 不实现 FsmAnalysis

## Decisions

### 1. SessionContext 持有 GlobalFSM 实例

`_globalState` raw enum → `_globalFsm` GlobalFSM instance。`GlobalState` 变为只读属性。双出口：`GlobalStateMachine`（公开 `IGlobalStateMachine`）和 `InternalGlobalFSM`（internal `GlobalFSM`，供 `ForceState` 访问）。

### 2. ForceState 区分"转换"与"恢复"

PopupHandler 的 preserved state 恢复语义是"撤销到中断前状态"，不是"状态转换"。`ForceState` 不触发 callback（恢复不是消费者应感知的事件），但记录历史（可审计）。`ForceState` 是 `internal`，通过 `GlobalFSM` 具体类访问，不暴露在 `IGlobalStateMachine` 接口上。

### 3. 与 B1 解耦

GlobalFSM 的 `RegisterStateCallback` 已提供观测能力。B2 的 trace callback 在引擎初始化时直接注册，不需要 B1 的 Hook 接口。

## Changes

```
SessionContext:
  _globalState (raw field)        → _globalFsm (GlobalFSM instance)
  GlobalState { get; set; }       → GlobalState => _globalFsm.CurrentState (read-only)
                                    + GlobalStateMachine (IGlobalStateMachine, public)
                                    + InternalGlobalFSM (GlobalFSM, internal)

GlobalFSM:
                                    + ForceState(targetState) (internal)

TraversalRuntimeContext:
  SetGlobalState(value)           → SetGlobalState(value, reason?) → TransitionTo()
                                    + ForceGlobalState(value) → ForceState() (internal)

TraversalEngine:
                                    + RegisterStateCallback for trace

PopupHandler:
  SetGlobalState(preserved)       → ForceGlobalState(preserved)
```

## Coupling

```
SessionContext ──→ GlobalFSM          (同层, 已有类)
TraversalEngine ──→ ITraceRecorder    (已有)
PopupHandler ──→ TraversalRuntimeContext.ForceGlobalState (已有依赖)

零新增跨层依赖。
```

## Risks

| 风险 | 缓解 |
|------|------|
| TransitionTo 抛 DomainValidationException | 已验证 2 调用点矩阵合法；PopupHandler 走 ForceState 不校验 |
| GlobalState setter 删除影响外部 | 仅 PopupHandler + TraversalEngine 调用 SetGlobalState，改用 TransitionTo / ForceState |
| ForceState 被滥用 | `internal` 限制 assembly 内访问；仅 PopupHandler 调用 |
