# Patterns — FSM Design

> **Tier 2 · Patterns**: 双 FSM 架构与迁移矩阵。缓慢演变——加新 FSM state 或修改迁移规则时更新。
> 代码来源: `StateMachine/TraversalFSM.cs`, `StateMachine/GlobalFSM.cs`
> 约束来源: → constitution/constraints.md C-4, C-1, C-7
> 决策来源: → decisions/log.md D-5, D-7

---

## 1. 双 FSM 架构概览

两个 FSM 服务不同粒度，各自维护独立的状态空间和迁移规则：

| FSM | 粒度 | 状态数 | 职责 | 所在文件 |
|-----|------|--------|------|---------|
| **TraversalFSM** | 微观 | 8 | 单步遍历决策：节点选择、执行、验证、分支 | `TraversalFSM.cs` |
| **GlobalFSM** | 宏观 | 8 | 整体进度管控：初始化、遍历、暂停、恢复、完成 | `GlobalFSM.cs` |

**独立性原则 (→ constitution C-4)**:
- 两个 FSM 不得共享 state enum / transition method / callback registration
- 协调仅通过 `ITraversalContext.GlobalState` setter/read
- 已知偏差: M-14 — GlobalState setter 在 ITraversalContext 上 (→ decisions/log D-7, Deferred·Phase3)

---

## 2. TraversalFSM 迁移矩阵 (8 状态)

来源: `TraversalFSM.cs` `TransitionMatrix` 字段

| From | Valid Targets | 说明 |
|------|--------------|------|
| **NodeSelect** | PreconditionCheck, Branch | 有 stack → PreconditionCheck; 空 stack → Branch |
| **PreconditionCheck** | Execute, ErrorHandling | precondition pass → Execute; fail → ErrorHandling |
| **Execute** | ResultVerify, Branch, ErrorHandling | 正常 → ResultVerify; 中断 → Branch; 异常 → ErrorHandling |
| **ResultVerify** | Branch, PopupHandling | 验证通过 → Branch; popup 检出 → PopupHandling |
| **Branch** | NodeSelect, PreconditionCheck, FrameComplete, ErrorHandling | 选子节点 → NodeSelect; 选带 stack → PreconditionCheck; 完成 → FrameComplete; 错误 → ErrorHandling |
| **FrameComplete** | NodeSelect, ErrorHandling | 下一 frame → NodeSelect; 异常 → ErrorHandling |
| **ErrorHandling** | NodeSelect, Execute, FrameComplete, Branch | 恢复成功 → 各后续状态 |
| **PopupHandling** | ResultVerify, ErrorHandling | dismiss 后 → ResultVerify; 异常 → ErrorHandling |

**关键设计决策**:
- **D-1 修正**: PreconditionCheck→Branch **已移除** (Python V6.7 handler 从不返回 Branch)
- **H-1 修正**: DynamicMatch **不属于**此矩阵 (它是 ChildrenStrategyType 值, → decisions/log D-5)
- **无自环**: 每个状态不允许迁到自己
- **Step() 异常兜底**: handler 抛异常 → 自动路由到 ErrorHandling, 不阻断 FSM

### Step() 分发逻辑

```csharp
// enum-based switch dispatch (非 if/elif chain)
fromState switch {
    NodeSelect        => HandleNodeSelect(),
    PreconditionCheck => HandlePreconditionCheck(),
    Execute           => HandleExecute(),
    ResultVerify      => HandleResultVerify(),
    Branch            => HandleBranch(),
    FrameComplete     => HandleFrameComplete(),
    ErrorHandling     => HandleErrorHandling(),
    PopupHandling     => HandlePopupHandling(),
    _                 => ErrorHandling  // unknown state = error
};
```

异常被 Step() 的 try-catch 捕获，自动设置 `Context.LastError` 并路由到 ErrorHandling。

### Handler 决策表 (Phase 2.3a: HandleExecute + HandleBranch ✅ implemented)

| Handler | 输入 | 决策逻辑 | 输出 |
|---------|------|---------|------|
| **HandleNodeSelect** ✅ | NodeStack.IsEmpty | empty→Branch; has node→PreconditionCheck | Branch / PreconditionCheck |
| **HandleExecute** ✅ | StepContext.Action + TraversalNode.Operation | NoAction→ResultVerify; Operation dispatch→execute→optional restore→ResultVerify; exception→ErrorHandling; null StepContext→stub ResultVerify | ResultVerify / ErrorHandling |
| **HandleBranch** ✅ | TraversalNode.ChildrenStrategy + VisitedChildren + NodeStack.Depth | STATIC+unvisited→NodeSelect; STATIC+all visited→FrameComplete; DYNAMIC_MATCH→NodeSelect(optimistic); NONE+IsLeaf+depth>1→FrameComplete; NONE+IsLeaf+depth==1→NodeSelect; NONE+container→FrameComplete; null node→FrameComplete(depth>1)/NodeSelect(depth≤1) | NodeSelect / FrameComplete |

**HandleExecute OperationType dispatch** (→ D-19):

| OperationType | IActionExecutor method | Required Target |
|--------------|----------------------|----------------|
| Click | TapAsync(x, y) | TargetType.Coordinate |
| Swipe | SwipeAsync(sx, sy, ex, ey, duration) | TargetType.Coordinate + Params |
| Back | PressBackAsync() | None |
| InputText | InputTextAsync(text) | TargetType.Text |
| NoAction | (skip) → ResultVerify directly | None |

**HandleBranch 决策矩阵** (→ D-20):

| ChildrenStrategy | Has unvisited? | IsLeaf? | Depth>1? | Result |
|-----------------|---------------|---------|----------|--------|
| STATIC | yes | — | — | NodeSelect |
| STATIC | no | — | — | FrameComplete |
| DYNAMIC_MATCH | optimistic | — | — | NodeSelect |
| NONE | — | yes | yes | FrameComplete |
| NONE | — | yes | no | NodeSelect |
| NONE | — | no | — | FrameComplete |

**Step(StepContext) overload** (→ D-18): `Step(StepContext? ctx)` 存 ctx → handlers 可访问 IVisionProvider + IActionExecutor; `Step()` 调 `Step(null)` → stub fallback; 非破坏性。

---

## 3. GlobalFSM 迁移矩阵 (8 状态)

来源: `GlobalFSM.cs` `TransitionMatrix` 字段

| From | Valid Targets | Terminal? |
|------|--------------|-----------|
| **Idle** | Initializing | No |
| **Initializing** | Traversing, Error | No |
| **Traversing** | Paused, Error, Completed | No |
| **Paused** | Traversing, Terminated | No |
| **Error** | Recovering, Terminated | No |
| **Recovering** | Initializing, Terminated | No |
| **Completed** | *(空 — 无出迁)* | **Yes** |
| **Terminated** | *(空 — 无出迁)* | **Yes** |

**关键设计决策**:
- **Completed 和 Terminated 是锁定态**: `ImmutableArray<GlobalState>.Empty` — 无出迁移，FSM 停止
- **Error 不能直接到 Traversing**: 必须走 Recovering → Initializing → Traversing 恢复路径
- **Paused 只能到 Traversing 或 Terminated**: 不允许 Paused → Error (暂停不是错误)

### Recovery 路径示例

```
Error → Recovering → Initializing → Traversing (恢复成功)
Error → Terminated (恢复失败, 用户终止)
```

---

## 4. Callback 机制 (GlobalFSM)

```csharp
fsm.RegisterStateCallback(GlobalState.Initializing, args => { /* 进入 Initializing 时执行 */ });
```

**关键行为**:
- Callback 在状态迁入后调用 (from → to 之后)
- **异常不传播** — callback 抛异常被 catch + log, 不阻断 FSM 迁移
- 这是 **Log-and-Continue 模式** 的又一个实例 (→ patterns/dispatch-table.md)

---

## 5. 转换历史 (GlobalFSM)

`GetTransitionHistory()` 返回 `IReadOnlyList<TransitionRecord>`，每条记录:
- FromState, ToState, Reason (可选), Timestamp

**只记录成功迁迁**: 失败的迁迁 (抛 DVE) 不记录。

---

## 6. TraversalFSM ↔ GlobalFSM 协调

两个 FSM 通过 `ITraversalContext.GlobalState` property 协调：

```
TraversalFSM 运行时:
  - 读 Context.GlobalState → 判断引擎整体状态 (是否 Error/Paused)
  - 写 Context.GlobalState → 标记引擎进入 Error 状态

GlobalFSM 运行时:
  - 不读 TraversalState
  - 不写 TraversalState
```

**当前偏差**: `ITraversalContext.GlobalState { get; set; }` 允许 TraversalFSM **写** GlobalState，创造了类型级跨 FSM 依赖 (→ decisions/log D-7)。Phase 3 计划将 GlobalState setter 从 ITraversalContext 移除，仅在 TraversalRuntimeContext class 上保留。

---

## 7. 迁迁校验机制

两个 FSM 共享相同的迁迁校验逻辑:

```csharp
// 无效迁移 → DomainValidationException
if (!TransitionMatrix.TryGetValue(CurrentState, out var allowedTargets)
    || !allowedTargets.Contains(targetState))
    throw new DomainValidationException("transition", $"{CurrentState}→{targetState}");
```

Terminal 状态特殊处理: GlobalFSM 在 Completed/Terminated 状态时直接拒绝任何迁迁 (代码显式检查，不依赖空矩阵)。TraversalFSM 没有 terminal 状态 (所有状态都有出迁)。
