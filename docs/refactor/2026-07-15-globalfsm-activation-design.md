# Design: GlobalFSM 激活 (B2)

> **创建时间**: 2026-07-15
> **状态**: 设计阶段
> **路线图**: P4, 前置 B1（搁置中，但 B2 对 B1 的依赖经分析为可解耦）
> **原则**: 激活已有代码，非新增功能

## 1. 现状

### 矛盾

`GlobalFSM` 类已完整实现 80 行（转换矩阵、`TransitionTo()`、回调注册、历史记录），但 **整个代码库中零次被实例化**。引擎绕过它，直接写 `SessionContext._globalState` 字段：

```csharp
// SessionContext.cs:31-35 — public setter, 任何人都可以绕过 FSM
public GlobalState GlobalState
{
    get => _globalState;
    set => _globalState = value;  // ❌ 无矩阵校验, 无回调, 无历史
}

// TraversalRuntimeContext.cs:244-245 — 薄封装
public void SetGlobalState(GlobalState value) =>
    _session.GlobalState = value;  // ❌ 同上
```

### 调用点

只有 2 处：

| 位置 | 代码 | 场景 |
|------|------|------|
| `TraversalEngine.cs:371` | `_ctx.SetGlobalState(reason is AllVisited ? Completed : Error)` | 遍历完成 |
| `PopupHandler.cs:366` | `rtc.SetGlobalState(preserved.CurrentState)` | 弹窗处理后恢复 |

### 后果

- 8 状态转换矩阵完全被绕过，`Idle → Completed`（跳过 Traversing）也能执行
- `RegisterStateCallback` 零消费者，`_callbacks` 永远空
- `TransitionRecord` 历史永远空，`GetTransitionHistory()` 永远返回空列表
- `StateTransition.FsmType` 永远只记录 `"TraversalFSM"`，GlobalFSM 转换无 trace

## 2. 设计目标

### Goals

- 激活 `GlobalFSM` 实例 — `TraversalEngine` 构造时创建
- 所有状态变更走 `TransitionTo()` — 矩阵校验 + 回调 + 历史
- 废除 `GlobalState` 的 public setter — 防止绕过
- GlobalFSM 转换写入 trace — `StateTransition.FsmType = "GlobalFSM"`
- 保留内部恢复路径 — `PopupHandler` 的 preserved state 恢复不抛异常

### Non-Goals

- 不改 8 状态转换矩阵（已锁定）
- 不改 TraversalFSM（独立于 GlobalFSM，本次不动）
- 不实现 FsmAnalysis（C1，Phase 3）
- 不新增 GlobalFSM 功能（类已完整）

## 3. 核心改动

### 3.1 SessionContext — 废除 public setter

```csharp
// BEFORE: raw field + public setter
private GlobalState _globalState;
public GlobalState GlobalState
{
    get => _globalState;
    set => _globalState = value;  // ❌ 任何人可绕过
}

// AFTER: 通过 GlobalFSM 实例间接访问
private readonly GlobalFSM _globalFsm = new();
public GlobalState GlobalState => _globalFsm.CurrentState;  // ✅ 只读
// public setter 删除 — 所有修改必须走 TransitionTo() 或 ForceState()

// 公开接口 (回调注册等)
public IGlobalStateMachine GlobalStateMachine => _globalFsm;

// 内部访问具体类型 (ForceState 是 internal, 不暴露给外部)
internal GlobalFSM InternalGlobalFSM => _globalFsm;
```

### 3.2 TraversalRuntimeContext — 双层 API: TransitionTo + ForceState

```csharp
// 正常转换 (矩阵校验 + 回调 + 历史)
public void SetGlobalState(GlobalState value, string? reason = null)
{
    _session.GlobalStateMachine.TransitionTo(value, reason);
}

// 内部恢复 (绕过矩阵, 仅 engine 内部, 不触发 callback)
internal void ForceGlobalState(GlobalState value)
{
    _session.InternalGlobalFSM.ForceState(value);
}
```

**分层**: `SetGlobalState` 走公开接口 `IGlobalStateMachine`（矩阵校验），`ForceGlobalState` 通过 `internal GlobalFSM` 访问 `ForceState`（不暴露到接口，不触发回调）。

### 3.3 GlobalFSM — 加 ForceState（仅恢复用）

```csharp
// 新增 internal 方法 — 仅 engine 内部用于状态恢复
internal void ForceState(GlobalState targetState)
{
    var fromState = CurrentState;
    CurrentState = targetState;
    _transitionHistory.Add(new TransitionRecord(fromState, targetState, "force_restore", DateTimeOffset.UtcNow));
    // 不触发 callback — 恢复不是状态转换，消费者不应感知
}
```

### 3.4 TraversalEngine — 注册 trace callback

```csharp
// 初始化时:
var fsm = _ctx.Session.InternalGlobalFSM;  // 或通过公开接口注册

// 注册 trace callback — GlobalFSM 转换写入 ITraceRecorder
fsm.RegisterStateCallback(GlobalState.Completed, args =>
{
    _traceRecorder?.RecordTransitionAsync(new StateTransition(
        FromState: args.FromState.ToString(),
        ToState: args.ToState.ToString(),
        FsmType: "GlobalFSM",  // ← 区分于 TraversalFSM
        Reason: args.Reason,
        Timestamp: args.Timestamp));
});

// 同理注册 Error, Traversing 等关键状态 callback

### 3.5 PopupHandler — 改用 ForceGlobalState

```csharp
// BEFORE
rtc.SetGlobalState(preserved.CurrentState);  // 可能违反矩阵

// AFTER
rtc.ForceGlobalState(preserved.CurrentState);  // 明确语义: 状态恢复, 非正常转换
```

## 4. 内聚与耦合验证

### 内聚

| 组件 | 职责 | 验证 |
|------|------|------|
| **GlobalFSM** | 全局状态转换的校验 + 回调 + 历史 | ✅ 不变（类已存在，本次只是激活） |
| **SessionContext** | 持有 GlobalFSM 实例，暴露只读 GlobalState | ✅ 新增 `StateMachine` 属性 |
| **TraversalEngine** | 构造 GlobalFSM，注册 trace callback | ✅ 仅初始化代码 |

### 耦合

```
BEFORE:
  TraversalEngine → SetGlobalState() → SessionContext.GlobalState = value  (直写字段)
  PopupHandler → SetGlobalState() → SessionContext.GlobalState = value      (直写字段)

AFTER:
  TraversalEngine → SetGlobalState() → TransitionTo()  → GlobalFSM (矩阵+回调+历史)
  PopupHandler    → ForceGlobalState() → ForceState()  → GlobalFSM (仅历史, 不触发回调)
```

**GlobalFSM 所有依赖都在 StateMachine 层内，零跨层新增耦合。**

### 关键决策：`ForceState` 分离正常转换与恢复

`PopupHandler.RestoreState` 的语义是"中断后恢复"，不是"状态转换"。例如弹窗处理可能临时改为 Error，处理完恢复到 Traversing。`Error → Traversing` 在矩阵中不存在（正确路径是 Error → Recovering → Initializing → Traversing）。但恢复不是"从 Error 出发"，而是"撤销到之前的状态"。

`ForceState` 明确表达此语义：不触发 callback（消费者不应看到内部恢复操作），但记录历史（可审计）。

## 5. 改动清单

| # | 文件 | 操作 |
|---|------|------|
| 1 | `SessionContext.cs` | 删除 `_globalState` 字段 + public setter; 新增 `_globalFsm` 字段 + `StateMachine` property; `GlobalState` getter 改为 `=> _globalFsm.CurrentState` |
| 2 | `GlobalFSM.cs` | 新增 `internal ForceState(targetState)` |
| 3 | `TraversalRuntimeContext.cs` | `SetGlobalState` 改为调用 `TransitionTo`; 新增 `internal ForceGlobalState` |
| 4 | `TraversalEngine.cs` | 初始化时注册 GlobalFSM → ITraceRecorder callback |
| 5 | `PopupHandler.cs` | `RestoreState` 中 `SetGlobalState` → `ForceGlobalState` |

## 6. 验证

- `dotnet build` 0 错误
- `dotnet test` 670+ 全绿
- 手动验证: `GetTransitionHistory()` 在遍历结束时非空
- 手动验证: StateTransition trace 中出现 `FsmType = "GlobalFSM"` 记录

## 7. 与 B1 的关系

原路线图说 B2 依赖 B1（ITraversalHook），但经分析这是**假性依赖**。B2 只需激活已有的 `GlobalFSM` 并注册 trace callback。Callback 注册不需要 B1 的 Hook 接口——它在引擎初始化时直接注册，走 `RegisterStateCallback` API。

B1 的 `ITraversalObserver` 是另一条线：为第三方消费者提供观测点。GlobalFSM 的 callback 机制已经为 B2 提供了所需的观测能力，B1 是独立需求。
