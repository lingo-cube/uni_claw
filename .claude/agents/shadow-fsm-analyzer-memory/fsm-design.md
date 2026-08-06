# Shadow FSM Design — 我独立设计的 FSM 模型

> 🔑 这是 shadow-fsm-analyzer 的核心产物。
> 本模型**完全从需求和测试推导**，刻意不读 C# FSM 源码（TraversalFSM.cs / GlobalFSM.cs / handler 实现）。
> 每次有新证据（需求更新、新测试、运行时 trace、battle 结论）修改我的理解时，更新本文件。
> 设计变更记录在 [battle-log.md](battle-log.md) 中。

## 设计状态

- **版本**: v0.3.2 (Battle #5 指纹去重方案对抗评审 + 2026-08-05 23:2x 刷新)
- **最后更新**: 2026-08-05
- **置信度**: 矩阵结构 SOURCE-VERIFIED（19 边与 TraversalFSM.cs TransitionMatrix 完全一致）；handler 核心逻辑 SOURCE-VERIFIED（8/8 主路径匹配）；T1-T6 重构测试全部落地（spec D-240/242/243/244 已同步）；CPU-FSM 架构 = 设计提案（§10，MEDIUM/LOW 标注）
- **基于**: Constitution 硬约束 + patterns/refactor 设计意图（含 2026-08-05-fsm-matrix-hardening-design.md）+ openspec specs（2026-08-05 22:31 已同步 19 边）+ StateMachine 测试（T1-T6 已落地）+ **Battle #1 源码交叉验证** + Battle #3 CPU 架构设计 + **Battle #5 指纹去重评审（§11）**

---

## 1. 状态定义

### TraversalFSM 状态（Constitution C-1 锁定 8 值）

| 状态 | 语义（我的定义） | 职责边界 | 置信度 | 来源 |
|------|-----------------|---------|--------|------|
| NodeSelect | 选择下一个待处理节点。栈空 → 无 frame 可处理 → Branch；栈顶有节点 → 进入该节点的前置检查 | 只读 NodeStack.IsEmpty | HIGH | C-1 + StateMachineTests.Step_NodeSelect* |
| PreconditionCheck | 前置条件检查。默认 assume pass（无 checker）；有 checker 且返回 false → 失败 | 记录 trace precondition_assume_pass / precondition_failed | HIGH | openspec spec + HandlePreconditionCheckTests + FsmSimulationRegressionTests |
| Execute | 执行当前节点的操作（Click/Swipe/Back/InputText/NoAction），含可选 Restore | OperationDispatcher 分发；NoAction 跳过执行器 | HIGH | HandleExecuteTests (8 场景) |
| ResultVerify | 验证操作是否产生页面变化。首次检查 + 单次重试（2 轮分析）；重试中检出弹窗 → PopupHandling；全部不变仍 → Branch（不阻塞）；stale-click 熔断（≥3 次→Pop+MarkVisited）；验证成功时 ResetConsecutiveErrors | 2 轮分析上限（首次 + 单次 retry）；PageSnapshotManager.HasChanged；PageAnalysis.IsPopup | SOURCE-VERIFIED | HandleResultVerifyTests + Battle #1 源码确认(TraversalFSM.cs:365-438) |
| Branch | 分支决策：按 ChildrenStrategy 决定前进（子节点）/ 完成（父节点） | STATIC→VisitedChildren 检查；DYNAMIC_MATCH→乐观前进；NONE→叶子/容器 + depth 判断 | HIGH | patterns D-20 + HandleBranchTests (6 场景) |
| FrameComplete | 帧完成标记。**handler 只恒定返回 NodeSelect**；栈弹出/帧 teardown 在 StepOrchestrator（拦截层），不在 handler | 纯决策，无副作用 | HIGH | FSMIntegrationTests.D5 注释 |
| ErrorHandling | 错误恢复。ErrorClassifier→ErrorStrategySelector→RecoveryExecutor 5 策略；ConsecutiveErrors 单点递增；双门限熔断 | 5 策略映射；≥3 consecutive / ≥5 page-items → PressBack → FrameComplete | HIGH | HandleErrorHandlingTests + FsmSimulationRegressionTests |
| PopupHandling | 弹窗处理。PopupHandler 6-step pipeline（detect→classify→preserve→handle→restore→validate） | Success→ResultVerify；Failure→ErrorHandling；顶层异常→back_fallback (H-8) | HIGH | HandlePopupHandlingTests + StateMachineTests.PopupHandlerFallbackTests |

### GlobalFSM 状态（Constitution C-7 锁定 8 值）

| 状态 | 语义 | 终端? | 置信度 | 来源 |
|------|------|-------|--------|------|
| Idle | 初始状态，引擎未启动 | No | HIGH | C-7 |
| Initializing | 引擎初始化中 | No | HIGH | C-7 |
| Traversing | 遍历主循环运行中 | No | HIGH | C-7 |
| Paused | 暂停（仅→Traversing 或→Terminated，**不允许 Paused→Error**：暂停不是错误） | No | HIGH | patterns + spec |
| Error | 遍历出错（仅→Recovering 或→Terminated） | No | HIGH | C-7 |
| Recovering | 恢复中（仅→Initializing 或→Terminated；**不能直连 Traversing**——Initializing 是校验检查点） | No | HIGH | patterns + spec + StateMachineTests |
| Completed | 完成（终态，无出边） | **Yes** | HIGH | C-7 + StateMachineTests |
| Terminated | 终止（终态，无出边） | **Yes** | HIGH | C-7 + StateMachineTests |

---

## 2. 转移矩阵

### TraversalFSM 转移矩阵 — 19 边（2026-08-05 加固后）

```
                    ┌──────────────┐
                    ▼              │
  NodeSelect ──► PreconditionCheck ──► Execute ──► ResultVerify
      │                 │               │  ▲           │
      │                 │               ▼  │           │
      │                 │            ErrorHandling     │
      │                 │            ▲    │  │  ▲      │
      │                 │            │    │  │  │      │
      ▼                 ▼            │    ▼  │  │      ▼
   Branch ◄────────────┘            │  FrameComplete  PopupHandling
      │                             │                  ▲
      └─────────────────────────────┘                  │
                                                       │
                          ResultVerify ──► PopupHandling
```

（ASCII 简化——精确边见下表）

| From | Valid Targets | 推导依据 | 生产者 | 置信度 |
|------|--------------|---------|--------|--------|
| NodeSelect | PreconditionCheck, Branch | patterns 矩阵；StateMachineTests: 空栈→Branch、有栈→PreconditionCheck | HandleNodeSelectAsync（显式） | HIGH |
| PreconditionCheck | Execute, ErrorHandling | patterns 矩阵；spec assume pass；FsmSimulationRegressionTests checker=false→ErrorHandling | HandlePreconditionCheckAsync（显式） | HIGH |
| Execute | ResultVerify, ErrorHandling | patterns 矩阵；HandleExecuteTests 成功→ResultVerify、异常→ErrorHandling | HandleExecuteAsync（显式） | HIGH |
| ResultVerify | Branch, PopupHandling, ErrorHandling | patterns 矩阵（含 ErrorHandling）；HandleResultVerifyTests→Branch/PopupHandling；**ErrorHandling 边的显式生产者测试未覆盖（见盲区）** | HandleResultVerifyAsync + 异常路由 | MEDIUM |
| Branch | NodeSelect, FrameComplete, ErrorHandling | patterns 矩阵；HandleBranchTests→NodeSelect/FrameComplete；harness 注释确认 Branch→ErrorHandling 是直接边 | HandleBranchAsync（显式） | HIGH |
| FrameComplete | NodeSelect | patterns 矩阵（19 边版）；FSMIntegrationTests handler 恒定返回 NodeSelect | HandleFrameCompleteAsync（恒定） | HIGH |
| ErrorHandling | NodeSelect, Execute, FrameComplete, Branch | patterns 矩阵；HandleErrorHandlingTests 5 策略映射全部覆盖 | HandleErrorHandlingAsync（显式） | HIGH |
| PopupHandling | ResultVerify, ErrorHandling | patterns 矩阵；HandlePopupHandlingTests 成功/失败两路径 | HandlePopupHandlingAsync（显式） | HIGH |

**被拒绝的边（负面证据）**：

| 边 | 拒绝依据 | 置信度 |
|----|---------|--------|
| NodeSelect→Execute | StateMachineTests.TransitionMatrix_InvalidTransitionsRejected（Assert.Throws DVE） | HIGH |
| PreconditionCheck→Branch | D-1 先例 + StateMachineTests.TransitionMatrix_PreconditionCheckToBranch_Rejected | HIGH |
| Execute→Branch | 死边 D1（重构 T6-1 将加负面测试；HandleExecuteAsync 从不返回 Branch） | HIGH（设计意图 + 重构设计） |
| Branch→PreconditionCheck | 死边 D2（重构 T6-2；实际路径为两跳 Branch→NodeSelect→PreconditionCheck） | HIGH（设计意图 + 重构设计） |
| FrameComplete→ErrorHandling | 死边 D3（重构 T6-3；handler 无法抛异常） | HIGH（设计意图 + 重构设计） |
| ErrorHandling→ErrorHandling | 无自环（C-1 矩阵约束 + 重构 T6-5） | HIGH |
| FrameComplete→FrameComplete | 无自环（重构 T6-6） | HIGH |

**矩阵演化史**（理解为何 19 边）：
- 22 边（旧，含 3 死边 + spec/charter 仍记录此版）→ 19 边（2026-08-05 矩阵加固）
- 死边移除先例 D-1：PreconditionCheck→Branch 因 handler 从不返回而移除
- 每条剩余边至少一个 handler 显式返回（重构设计 §2.1 验证列表）
- **openspec spec 与 charter §3.1 仍是 22 边旧版——文档滞后点（发现）**

### GlobalFSM 转移矩阵

```
Idle ──► Initializing ──► Traversing ──► Completed (terminal)
            │               │  │            │
            ▼               ▼  ▼            ▼
         Error ◄──────── Paused ──► Terminated (terminal)
            │               ▲
            ▼               │
        Recovering ──► Initializing (recovery path, 校验检查点)
```

| From | Valid Targets | 依据 | 置信度 |
|------|--------------|------|--------|
| Idle | Initializing | C-7 + StateMachineTests.IdleOnlyToInitializing | HIGH |
| Initializing | Traversing, Error | C-7 + 测试驱动路径 | HIGH |
| Traversing | Paused, Error, Completed | C-7 + 测试驱动路径 | HIGH |
| Paused | Traversing, Terminated | patterns + spec（不允许 Paused→Error） | HIGH |
| Error | Recovering, Terminated | C-7 + StateMachineTests.RecoveryPath | HIGH |
| Recovering | Initializing, Terminated | spec.RecoveringNotToTraversing（测试断言拒绝） | HIGH |
| Completed | ∅（锁定） | StateMachineTests.CompletedIsTerminal | HIGH |
| Terminated | ∅（锁定） | StateMachineTests.TerminatedIsTerminal | HIGH |

**关键约束**：
- Error→Traversing 拒绝（测试直接断言）：恢复必须走 Recovering→Initializing→Traversing
- Recovering→Traversing 拒绝（测试直接断言）
- 两步终止（D-82）：Traversing→Paused("stopping")→Terminated；矩阵无 Traversing→Terminated 直边
- ForceState 绕过矩阵（仅 PopupHandler/StateRestorer 内部恢复用），记录 "force_restore" 历史、不触发回调、不产生 trace

---

## 3. Handler 决策表

### HandleNodeSelectAsync — 输入: NodeStack.IsEmpty

| 条件 | 输出 | 置信度 | 来源 |
|------|------|--------|------|
| 栈空 | Branch | HIGH | StateMachineTests.Step_NodeSelectWithEmptyStack |
| 栈有节点 | PreconditionCheck | HIGH | StateMachineTests.Step_NodeSelectWithStack |

### HandlePreconditionCheckAsync — 输入: IPreconditionChecker?（StepContext）

| 条件 | 输出 | trace | 置信度 | 来源 |
|------|------|-------|--------|------|
| 无 checker（assume pass） | Execute | precondition_assume_pass | HIGH | HandlePreconditionCheckTests |
| checker 返回 true | Execute | precondition_assume_pass | HIGH | 推断（同 assume pass） |
| checker 返回 false | ErrorHandling | precondition_failed | HIGH | FsmSimulationRegressionTests.PreconditionCheck_CheckerReturnsFalse |

### HandleExecuteAsync — 输入: StepContext.Action + TraversalNode.Operation

| 条件 | 输出 | 置信度 | 来源 |
|------|------|--------|------|
| NoAction | ResultVerify（跳过执行器） | HIGH | HandleExecuteTests.Execute_NoAction |
| Click/Back 成功 | ResultVerify + action 记录 | HIGH | HandleExecuteTests |
| Click+Restore 成功 | ResultVerify（2 次调用 tap+back） | HIGH | HandleExecuteTests.Execute_WithRestore |
| Restore 失败（返回 false） | ResultVerify（非关键） | HIGH | HandleExecuteTests.Execute_WithRestore_Failure |
| 执行器返回 false | ResultVerify（非关键，匹配 Python） | HIGH | HandleExecuteTests.Execute_ActionReturnsFalse |
| 执行器抛异常 | ErrorHandling + SetLastError | HIGH | HandleExecuteTests.Execute_Exception |
| StepContext null | ResultVerify（stub 回退） | HIGH | HandleExecuteTests.Execute_NullStepContext |

**OperationType→executor 映射**（D-19）：Click→TapAsync(x,y)；Swipe→SwipeAsync；Back→PressBackAsync；InputText→InputTextAsync；NoAction→跳过

### HandleResultVerifyAsync — 输入: 前后 PageAnalysis + IsPopup

| 条件 | 输出 | 置信度 | 来源 |
|------|------|--------|------|
| 首次检查页面变化 (HasChanged=true) | Branch + trace verification_passed | SOURCE-VERIFIED | HandleResultVerifyTests + 源码:383-394 |
| 单次 retry: IsPopup=true | PopupHandling | SOURCE-VERIFIED | HandleResultVerifyTests.PopupDetected* + 源码:403-407 |
| 单次 retry: HasChanged=true (第 2 轮成功) | Branch + trace verification_passed_retry | SOURCE-VERIFIED | 源码:409-416 |
| 2 轮均不变 + stale-click≥3 (Click 节点) | Pop+MarkVisited → Branch（不阻塞） | SOURCE-VERIFIED | 源码:420-434 (StaleClickLimit=3) |
| 2 轮均不变（非 Click / 熔断未触发） | Branch + trace verification_page_unchanged | SOURCE-VERIFIED | HandleResultVerifyTests + 源码:436-438 |
| StepContext null | Branch（stub 回退） | SOURCE-VERIFIED | HandleResultVerifyTests.NoStepContext + 源码:369-370 |
| vision 调用抛异常 | ErrorHandling（**异常路由边**——StepAsync catch 自动路由，非 handler 显式返回） | SOURCE-VERIFIED | Battle #1 源码确认: handler 内部无 return ErrorHandling；靠 catch 路由 |

**注**: spec 文档声称 "3-round retry"，但源码实现为 2 轮（首次 + 单次 retry）。文档滞后——以源码为准。
**重试动机**（PRD 2026-08-05-settle-delay D3）：操作后 after 截图无 settle（~50ms 裸奔）→ 截到动画帧 → HasChanged 假阴性 → 重试兜底。
**验证成功副作用**: ResetConsecutiveErrors()（源码:390,412）——确保 page-item gate(≥5) 可达，不被 consecutive gate(≥3) 抢先触发。

### HandleBranchAsync — 输入: ChildrenStrategy + VisitedChildren + NodeStack.Depth（D-20 决策矩阵）

| ChildrenStrategy | 条件 | 输出 | 置信度 | 来源 |
|------------------|------|------|--------|------|
| STATIC | 有未访问子节点 | NodeSelect | HIGH | HandleBranchTests.Branch_StaticUnvisited |
| STATIC | 全部已访问 | FrameComplete | HIGH | HandleBranchTests.Branch_StaticAllVisited |
| DYNAMIC_MATCH | （乐观） | NodeSelect；无未访问子节点时触发 TryHandleScroll（spec D3） | HIGH | HandleBranchTests.Branch_DynamicMatch + spec |
| NONE | 叶子 + depth>1 | FrameComplete（弹回父节点） | HIGH | HandleBranchTests.Branch_LeafNode_DepthMoreThan1 |
| NONE | 叶子 + depth==1 | NodeSelect | HIGH | HandleBranchTests.Branch_LeafNode_Depth1 |
| NONE | 容器 | FrameComplete | HIGH | spec（patterns D-20 表） |
| 任意 | VisitedChildren 无记录 | 全部视为未访问 → NodeSelect | HIGH | HandleBranchTests.Branch_EmptyVisitedChildren |

### HandleFrameCompleteAsync — 恒定返回 NodeSelect

栈弹出/帧 teardown 在 StepOrchestrator step 10（拦截层职责，D5）。**handler 无副作用**。置信度 HIGH（FSMIntegrationTests.HandleFrameComplete_MinimalCorrect）。

### HandleErrorHandlingAsync — 输入: LastError + ConsecutiveErrors + NodeFailedItems + depth（5 策略映射，D-25）

| 策略 | 输出 | ConsecutiveErrors | 置信度 | 来源 |
|------|------|-------------------|--------|------|
| Retry | Execute | +1 | HIGH | HandleErrorHandlingTests.Retry |
| Backtrack | NodeSelect | +1（**不重置**） | HIGH | HandleErrorHandlingTests.Backtrack + 回归测试 |
| Skip | Branch | +1 | HIGH | HandleErrorHandlingTests.Skip |
| Continue | NodeSelect | +1 | HIGH | HandleErrorHandlingTests.Continue |
| Abort | FrameComplete | +1 | HIGH | HandleErrorHandlingTests.Abort |
| RecoveryExecutor 抛异常 | pipeline 内兜底 Abort → FrameComplete | — | HIGH | HandleErrorHandlingTests.RecoveryExecutorFallback |
| StepContext null | NodeSelect（stub 回退） | — | HIGH | HandleErrorHandlingTests.NoStepContext |

**双门限熔断**（在 ErrorHandling 内判定）：
- ConsecutiveErrors ≥ 3 → PressBack → FrameComplete（trace: error_recovery_press_back）
- NodeFailedItems ≥ 5（且 depth>1）→ PressBack → FrameComplete（trace: error_recovery_page_item_limit_5）
- 成功验证（verification_passed）重置 ConsecutiveErrors——consecutive 门限与 item 门限的时序关系：interleaved 场景下 item gate 先于 consecutive gate 触发
- **递增点唯一性（Bug #2 T3 已落地）**：唯一递增点 = HandleErrorHandlingAsync。修复前 4 处（StepAsync catch / PreconditionCheck / Execute catch / ErrorHandling）→ 异常路径 +2/周期；T3/T3a 断言完整周期精确 +1（Execute catch 路径与 StepAsync catch 路径各一）
- **递增先于门限判定**（T2 2c 证明）：ConsecutiveErrors=2 + Backtrack → 递增到 3 才触发 consecutive gate——若先判定 gate，2<3 不会触发
- **page-item gate 判定先于 consecutive gate**（MEDIUM）：设计文档 line 号 608<621 + T2 子用例顺序暗示；无"双 gate 同时触发"测试
- **NodeFailedItems 语义**（MEDIUM）："页内不同失败帧计数"——2b 注释"5 个不同 frame"、回归注释 "distinct frame per iteration"；同一 frame 重复失败只涨 ConsecutiveErrors。递增点在 FSM 外（测试手动调用），FSM 只判定

**策略选择链**（ErrorStrategySelector，非 FSM 层但决定恢复路径）：
- Crash→Abort；Timeout+未达上限→Retry；Timeout+已达上限→Continue；Permission+深度1（Backtrack 不可用）→Abort
- 每节点 ErrorPolicy.MaxRetries 覆盖默认 3（openspec spec）
- 退避 min(2^attempt, 10s)

### HandlePopupHandlingAsync — 输入: PopupHandler.HandlePopup() 结果（6-step pipeline）

| 条件 | 输出 | 置信度 | 来源 |
|------|------|--------|------|
| Success=true | ResultVerify | HIGH | HandlePopupHandlingTests.Success |
| Success=false | ErrorHandling（重构后将 SetLastError——当前 H2 盲区） | HIGH | HandlePopupHandlingTests.Failure |
| StepContext null | ResultVerify（stub 回退） | HIGH | HandlePopupHandlingTests.NoStepContext |
| pipeline 顶层异常 | back_fallback 结果（H-8，Success=false） | HIGH | StateMachineTests.PopupHandlerFallbackTests |

---

## 4. 树结构推理

### 导航树模型（DFS）

- **NodeStack**: DFS 栈。`DefaultMaxDepth = 10`；`Push(node, children)` 在 depth >= MaxDepth 时返回 false（深度硬限制）；`Peek(offset)`: 0=top, 1=parent
- **节点类型**: NodeType（Container / LeafAction / Screen 等）；ChildrenStrategy: Static（显式子列表）/ DynamicMatch（动态匹配生成）/ None（叶子）
- **VisitedChildren**: 每节点子访问记录（Dictionary<string, HashSet<string>>），是 DFS 防环机制；无记录 → 全视为未访问
- **DFS 验证维度**（C-11 ExpectedBehavior）：RootFirst、ParentBeforeChild、BackAfterForward

### 遍历主循环（正常路径）

```
NodeSelect → PreconditionCheck → Execute → ResultVerify → Branch
Branch → NodeSelect（选子，压栈由拦截层做）→ ...
Branch → FrameComplete（完成当前帧）
FrameComplete → NodeSelect（拦截层弹栈后回到父帧）
```

### 回退路径（三种）

| 回退机制 | 触发 | 层 | 来源 |
|---------|------|-----|------|
| FrameComplete（正常回退） | 叶子完成 / 容器全访问 / 滚动耗尽 | FSM handler 决策 | HandleBranchTests |
| PressBack（错误熔断回退） | ConsecutiveErrors≥3 / NodeFailedItems≥5 | ErrorHandling 门限 | FsmSimulationRegressionTests |
| 拦截层回退（leaf-pop / stale-click / DynamicMatch 拦截） | 引擎层判定 | StepOrchestrator/InterceptionHandler | 重构设计 §2.2 取舍说明 |

### 深度语义

- depth==1 = 根节点（叶子 → NodeSelect 不弹回——根不能回退）
- depth>1 叶子完成 → FrameComplete 弹回父节点
- 滚动耗尽在根（depth==1）→ FrameComplete 完成遍历（不是 NodeSelect 死循环——spec）

### 弹窗对状态的干扰

- StateRestorer 保存完整栈内容（H-6，List<IStackFrame> 而非仅 depth）+ 5 字段恢复 + validate（H-7）
- Popup 处理期间 GlobalState 可被 ForceState 撤销（force_restore）

---

## 5. GlobalFSM 生命周期

```
Idle → Initializing → Traversing → Completed          （正常完成: all_visited）
                          │
                          ├→ Paused → Terminated       （两步终止: user_stop / timeout / cancelled）
                          ├→ Error → Recovering → Initializing → Traversing （恢复成功）
                          │            └→ Terminated   （恢复失败）
```

- **激活**: SessionContext 持有 GlobalFSM（D-81）；Context.GlobalState 只读代理
- **正常变更**: SetGlobalState → TransitionTo（矩阵校验 + 历史 + 回调 + trace StateTransition FsmType=GlobalFSM）
- **恢复变更**: ForceState（绕过矩阵，历史 reason="force_restore"，无回调无 trace）
- **Callback**: 迁入后调用；异常 catch+log 不传播（Log-and-Continue）
- **History**: IReadOnlyList<TransitionRecord>；只记录成功迁移；失败（DVE）不记录

---

## 6. 边界条件与熔断

| 门限 | 值 | 触发行为 | 证据强度 |
|------|----|---------|---------|
| ConsecutiveErrors | ≥3 | PressBack → FrameComplete | HIGH（回归测试 + 重构 AC6） |
| NodeFailedItems | ≥5 | PressBack → FrameComplete（depth>1） | HIGH（回归测试 trace action 名） |
| MaxDepth | 10（NodeStack 默认） | Push 返回 false | HIGH（layers + 测试参数） |
| MaxRetries | 3（默认；ErrorPolicy 覆盖） | 策略选择链 | HIGH（spec + ErrorStrategySelector 测试） |
| ResultVerify 重试 | 3 轮 | 全失败 → Branch | HIGH（测试） |
| 退避 | min(2^attempt, 10s) | Retry 前等待 | HIGH（RecoveryExecutor 测试） |
| MaxSteps | 引擎配置（实测 120） | 终止（CompletionReason.MaxSteps） | HIGH（SimulationE2ETests + TraceReplay） |
| advisor 置信度 | 0.7 | 低于门限不采信 advisor | MEDIUM（harness 注释） |
| 弹窗相关 | PopupType 6 值（含 Anr） | ANR: AutoClose(Wait)/Back | HIGH（测试 P1a-c） |

**LastError 生命周期**（重构后语义）：SetLastError（入口）→ HandleErrorHandlingAsync 读取分类 → 3 个返回点前 SetLastError(null)（处置完毕清零）。**当前测试无清零断言——重构 T2 将补**。

---

## 7. 已知盲区

1. **ResultVerify→ErrorHandling 的显式生产者**：~~矩阵有此边~~ **Battle #1 解答**：此边是"异常路由边"——HandleResultVerifyAsync 内部无 `return ErrorHandling`；实际生产者是 StepAsync catch 的 CanTransitionTo 守卫自动路由（TraversalFSM.cs:130-131）。handler 显式返回不生产此边，但运行时可达（vision 异常→catch→路由）。与 3 死边不同：死边连运行时都不可达。
2. **异常路由降级链**（Bug #1 修复，重构 T1/T1a）：当前测试仅覆盖 pipeline 内兜底（ErrorHandling_RecoveryExecutorFallback_Abort），不覆盖 StepAsync catch 降级路径（NodeSelect→Branch / FrameComplete→NodeSelect / ErrorHandling→FrameComplete）。降级后的步数燃烧行为（ISSUE C）无测试。
3. **LastError 清零**（Bug #3 修复，重构 T2）：现有测试断言 LastError 非 null（设置侧），无恢复后为 null 的断言。
4. **ConsecutiveErrors 单点递增**（Bug #2 修复，重构 T3）：**测试已落地（2026-08-05 22:19）**——`ErrorHandling_FullCycle_ConsecutiveErrorsIncrementsOnce`（Execute catch 路径）+ `ErrorHandling_FullCycle_UncaughtException_IncrementsOnce`（StepAsync catch 路径）断言完整周期精确 +1。剩余盲区：**PressBack 后 ConsecutiveErrors 是否重置无测试**；双 gate 同时触发优先级无测试。
5. **Popup 失败 LastError**（H2，重构 T4/T5）：**Battle #1 解答**：源码已修复（TraversalFSM.cs:672-675）——构建 `InvalidOperationException(detail)` 并 SetLastError。测试待补（T4/T5 尚未落地）。
6. **死边拒绝**（重构 T6）：Execute→Branch / Branch→PreconditionCheck / FrameComplete→ErrorHandling 的拒绝测试尚未存在（当前只有 PreconditionCheck→Branch 的 D-1 先例测试）。
7. **DynamicMatch 容器耗尽语义**（R1）：120 步 run 中 FrameComplete=0——DynamicMatch 容器耗尽后应 FrameComplete 还是循环？spec 只有 TryHandleScroll 相关需求；FSM 层无防御（重构设计 §3.1 建议拦截层加 FrameComplete 超时兜底，不在范围）。
8. **stale-click 熔断细节**：在拦截层（InterceptionHandler），我的信息源（需求+测试）不可见其门限值。留给 fsm-analyzer/拦截层分析。
9. **openspec spec 与 charter 的矩阵滞后**：spec（22 边）与 charter §3.1（旧版边如 PreconditionCheck→Branch、ResultVerify→FrameComplete）未同步 2026-08-05 加固。若以 spec 为准会产生错误理解——**以 patterns/fsm-design.md（19 边）为准**。
10. **test_contract_extractor.py 提取局限**：valid_transitions 含驱动路径假阳性（NodeSelect→Execute、Execute→PreconditionCheck 等被列为 valid，实为驱动链或拒绝边）；handler_returns 按 before_context 猜测 handler 名不可靠。输出只能当线索，必须人工校验。
11. **ErrorHandling 步数燃烧路径**：ErrorHandling 反复崩在同一帧 → 降级 FrameComplete→NodeSelect→PreconditionCheck→Execute→失败→… 循环燃烧步数直至 max_steps。设计文档化但无测试。
12. **IPreconditionChecker 的实际来源**：PreconditionCheck 有 checker 机制（回归测试用了 FailingPreconditionChecker），但 HandlePreconditionCheckTests 只说 assume pass——checker 何时注入、默认是否为空，需求未明确。
13. **D-G11（depth≥maxDepth 跳过滚动）无需求锚点 + 无契约测试**（2026-08-06 语义分析）：只存在于 e2e-dedup-vision-quality PRD 指标表 + 引擎实现；仿真 fixture 不可滚动，无法表达"depth=2 可滚动页"场景。我的语义判断：maxDepth 管树下降（Push），滚动管帧内内容揭示，两者正交；D-G11 结果（子页面不滚动）与 enumerate 采样契约（"sample"、"visible menu items"、maxScrolls=12、successCriteria 只查一级条目）一致，但编码理由（机制事实当需求投影）是概念混淆——若需求升级为"子页面记录全"，守卫谓词应改为"childrenNotPushable → 记录可见内容后 FrameComplete"而非 depth 谓词。

---

## 8. Battle #1 结论（2026-08-05 vs TraversalFSM.cs 源码交叉验证）

### 结果: 高度一致 ✅

- **矩阵 19/19 边精确匹配**
- **8/8 handler 核心逻辑匹配**
- **异常路由 CanTransitionTo + 降级链完全一致**
- **双门限 (≥3/≥5) + LastError 生命周期完全一致**
- **3 个表面差异全部解决**（ResultVerify retry 3→2 文档滞后、异常路由边生产者澄清、Popup LastError 源码已修复）
- **1 个遗漏已补充**（stale-click fuse 3 次 + ResetConsecutiveErrors 副作用）

详见 [battle-log.md](battle-log.md) Battle #1。

---

## 9. 设计演进历史

| 日期 | 版本 | 变更 | 触发 |
|------|------|------|------|
| 2026-08-05 | v0.1.0 | 初始骨架（状态定义框架 + GlobalFSM 矩阵 + HandleBranch 决策表片段） | Agent 创建 |
| 2026-08-05 | v0.2.0 | 首次全量 S1+S2：8 状态语义 + 19 边矩阵（含死边证据）+ 8 handler 决策表 + 树结构推理 + 双门限熔断 + 12 项盲区 | 首次全量分析任务 |
| 2026-08-05 | v0.2.1 | Battle #1 交叉验证修正：ResultVerify 重试 3→2 轮 + stale-click fuse 行 + 盲区 #1/#5 解答 + 异常路由边语义澄清 + Handler 决策表全部 SOURCE-VERIFIED | Battle #1 vs TraversalFSM.cs |
| 2026-08-05 | v0.3.0 | §10 CPU-FSM 架构提案（ISA 16 指令 + Memory 5 分区权限矩阵 + Brain 一等公民 + Plan 可执行程序 + Vision 相机模型）——纯设计提案，与 19 边矩阵不冲突 | Battle #3: FSM-as-CPU 架构重新设计 |

---

## 10. CPU-FSM 架构提案（Battle #3，2026-08-05）

> 🔑 从第一性原理推导的重新设计。当前 19 边矩阵是"静态指令邻接表"，本提案在其上叠加 Plan 级控制流 + Memory 分区 + Brain 一等公民。**不改变 C-1 的 8 值锁定**（新指令以 Plan 程序构造/微指令实现，不以 enum 值实现）。
> 详细设计见 Battle #3 输出报告。本节为记忆摘要。

### 10.1 核心隐喻映射

```
FSM        = CPU  — 执行指令、管理控制流；TraversalState = 程序计数器 (PC)
TraversalFSM 矩阵 = 静态指令邻接表（opcode legality table）
异常降级链     = 中断向量表 (IVT)  ← 重构文档 §1.2 的"矩阵双重职责"在 CPU 隐喻下的正解
              （refactor 2026-08-05 §1.2 明确: 矩阵只管 Handler 门，异常路由走独立降级通道 = IVT 分离）
GlobalFSM  = 系统控制器（电源管理：idle/boot/running/suspend/error/completed/terminated）
Plan       = 可执行程序（.code 段：指令 + 子程序 + 循环 + 错误处理块）
AI         = Brain（协处理器，通过 CONSULT_BRAIN 指令介入，任何点）
Vision     = Camera（外部 I/O 设备，写 .device 帧缓冲）
Memory     = RAM（5 分区，CPU/Brain/Vision 按权限矩阵读写）
```

**刷新锚定（2026-08-05 22:00-22:31）**：
- refactor/2026-08-05-fsm-matrix-hardening-design.md §1.2：矩阵双重职责根因分析 = 我的"矩阵=opcode 邻接表、异常路由=独立 IVT"论断的文档化背书（HIGH）
- openspec spec D-240（19 边）、D-242（ConsecutiveErrors 唯一递增点）、D-243（LastError 清零）、D-244（Popup 失败消息安全）——spec 已同步 19 边（knowledge.md 的"spec 滞后"条目已过时）
- T1/T1a/T2/T3/T3a/T4/T5/T6 全部落地（StateMachineTests + HandleErrorHandlingTests + HandlePopupHandlingTests）——盲区 #2/#3/#5/#6 已解决
- openspec/changes/e2e-dedup-vision-quality：verification_passed 时记录 (parentNodeId, destinationFingerprint)，sibling 重复目的地标记 visited——visited 集升级为"内容寻址"（两个 nodeId→同一物理页，R1 根因族）

### 10.2 ISA — 16 条指令（8 条 = 现有状态 1:1，8 条新增）

| 指令 | 对应状态/新增 | 语义 | 操作数 |
|------|------------|------|--------|
| SELECT_NODE | NodeSelect | 读 NodeStack → 写 CurrentFrame | .data.stack |
| CHECK_PRECOND | PreconditionCheck | 前置门 | .data + checker |
| EXECUTE | Execute | 动作分发到设备总线 | .data.action + 执行器 |
| VERIFY | ResultVerify | 读 .device 帧 → 比较 → 有界重试微循环 | .device.fingerprint |
| BRANCH | Branch | 子策略决策 | .data.children + .code.strategy |
| COMPLETE_FRAME | FrameComplete | 弹栈 | .data.stack |
| RECOVER | ErrorHandling | 服务 IRQ#1 | .sysreg.error |
| HANDLE_INTERRUPT | PopupHandling | 服务 IRQ#0（6 步子程序） | .sysreg.popupIRQ |
| **CONSULT_BRAIN** | 新增（一等公民） | 同步咨询 Brain，带安全门+预算 | .brain + .device 快照 |
| **CALL** | 新增（Plan 级） | 压返回地址跳子程序（scroll/popup/recover） | .code 子程序 |
| **RET** | 新增（Plan 级） | 弹返回地址 | — |
| **BRANCH_IF** | 新增（Plan 级） | 条件跳转（循环回边） | .data/.device 条件 |
| **FOR_EACH** | 新增（Plan 级） | 遍历子节点 | .data.children |
| **WAIT** | 新增 | settle 延迟（D3 PRD 的正式化） | ms |
| **NOP** | 新增 | no-op | — |
| **HALT** | 新增 | 终止（→ GlobalFSM 终态） | — |

**关键论断**：8 状态 = 每步原子操作 = 指令；Plan 级控制流（循环/子程序）不扩充矩阵——矩阵保持 19 边静态图，循环在 Plan 程序层显式化（引擎 fetch-execute 循环 = 时钟）。

### 10.3 Memory — 5 分区 + 权限矩阵

| 分区 | 内容（映射现有 5 子系统） | CPU | Brain | Vision | 生命周期 |
|------|--------------------------|-----|-------|--------|---------|
| .code | Plan（只读执行；Brain 可经补丁通道改） | R | R+patch(校验) | — | 编译期→运行期补丁 |
| .data | Navigation+Progress（NodeStack/VisitedChildren/步数等 13 字段） | RW | RO | — | 会话 |
| .device | PageAnalysis/fingerprint/快照（帧缓冲） | RO(frame-id 校验) | R | W | 每帧 |
| .brain | Brain 工作区（决策缓存/reasoning） | R | RW | — | 跨步持久 |
| .sysreg | ErrorContext+Session（PC/LastError/双门限/IRQ 标志） | RW | 部分 R | — | 会话 |

- D-III（ITraversalContext 服务两种消费者）正解：分区权限 = 类型级隔离的推广（ReadOnlySetWrapper C-6 先例扩展为逐分区 wrapper）
- .device 帧语义：帧 id + 时间戳；VERIFY 必须读 post-action 帧（解决"FSM 无环境变化感知"——R1 根因之一）

### 10.4 Brain — 一等公民

- **介入点**（6 处）：编译期（已有 PlanOptimizationAdvisor）、子节点排序（目标导向 DFS）、动作前（安全筛查+改写）、验证时（解释模糊相机结果）、恢复时（策略选择超越固定 5 策略）、弹窗分类
- **通信协议** = 现有 ContextDecisionResult（Decision/Action/Target/Params/Reasoning/Confidence/SafetyVerified）——协议已存在，缺的是介入频率和安全门
- **安全门 6 条**：置信度门限（按指令类型可配，默认 0.7）、动作白名单、目标校验（坐标 [0,1]/文本须在 .device 存在）、决策永不直接改 .data（只返回提案，CPU commit）、预算（时间+token，超时回退确定性路径）、熔断（连续 3 次失败 → 本会话降级确定性模式）
- **升级阶梯**（替代单一 0.7 门限）：确定性 → Brain 辅助（置信门限）→ Brain 权威（仅当确定性路径卡死，如同一节点 2 次失败）——AI 参与度与难度成正比

### 10.5 Plan 可执行程序模型

- 程序结构：main + 子程序（scroll 循环 / popup ISR / 每节点 recover 块）+ 每节点 ErrorPolicy 块（错误处理成为 Plan 数据，非硬编码 5 策略）
- 现有 ChildrenStrategy（STATIC/DYNAMIC_MATCH/NONE）→ 保留为指令操作数；新增 BRANCH_IF/FOR_EACH/LOOP/CALL/RET 程序构造
- **AI 运行时补丁通道**：PlanPatch{NodeId, PatchType(insert/skip/reorder/change_strategy), Value, Reasoning, Confidence} → CPU 校验门（节点存在/补丁类型合法/策略合法/目标合法）→ 应用 + Meta 标注（ai_patched/ai_confidence/ai_source，沿用 ai-plan-optimization-hints.md 标注先例）→ 补丁日志（journal = undo log）
- **防跑飞规则**：补丁/重入同一帧 > N 次（建议 3）→ 拒绝；`.on_stuck` 块（无进展计数器：无新访问节点 + 无 FrameComplete 达 K 次 → 咨询 Brain 或强制 FrameComplete）——直接防御 R1（DynamicMatch 120 步 FrameComplete=0）

### 10.6 Vision 相机模型

- 数据流：Camera（截图）→ OCR 管线（raw RGBA→OCR→elements）→ .device 帧缓冲 → CPU(VERIFY)/Brain(解释)
- 解释分离：Vision 产出原始事实（OCR/元素）；Brain 解释语义（"这是错误页吗？列表到底了吗？"）——VERIFY 低置信度时触发 CONSULT_BRAIN
- 相机寄存器（.sysreg）：current fingerprint、last-frame-change 标志、capture 时间戳——CPU 可不做全量重分析而轮询"世界变了吗"

### 10.7 对现有约束的处置

| 约束 | 处置 | 理由 |
|------|------|------|
| C-1（8 值锁定） | **保留** | 指令以 Plan 构造实现，不新增 enum 值；矩阵=静态指令邻接表不变 |
| C-4（双 FSM 独立） | **保留** | CPU vs 系统控制器隐喻下天然成立；协调字段可演进为小消息通道但独立原则不变 |
| C-7（GlobalState 8 值） | **保留** | 系统生命周期 |
| C-10/C-11 | **保留** | 非法 opcode 检测 + baseline 门槛 |
| C-6（cast-back 阻断） | **扩展** | 先例推广为逐分区权限 wrapper |
| 矩阵"双重职责"（重构 §1.2） | **正解** | 矩阵=指令邻接表；异常路由=独立 IVT（中断向量表）——降级链即 IVT |

### 10.8 迁移路径

- **Stage 1**（第一阶段）：Memory 分区形式化（复用已完成 Phase 5 子上下文分解）+ .device 帧 id + ErrorHandling 内升级阶梯（最小改动：把 0.7 单门限扩为阶梯 + 无进展计数器）
- **Stage 2**：Plan 子程序/循环注解（非破坏：把现有 scroll 循环/重试循环标注为程序构造；plan JSON round-trip 已存在）
- **Stage 3**：Brain 运行时补丁通道（校验门 + journal）
- **Stage 4**：Vision 异步帧生产（frame-id 消费）

### 10.9 置信度

- HIGH：当前局限清单（文档/测试证据：30 字段、0.7 门限、静态 Plan、5 策略硬编码、R1 120 步）
- HIGH：8 状态 1:1 映射为 ISA 基础指令（Battle #1/#2 SOURCE-VERIFIED）
- MEDIUM：新增指令/分区/升级阶梯/补丁通道（设计提案，无既有需求文档）
- HIGH：R1 根因与"环境变化感知缺失"的归因（refactor §3.1 文档化）

---

## 11. Battle #5 结论：指纹去重方案对抗评审（2026-08-05）

> 评审对象：用户方案一（Container 文本去重，P0）+ 方案二（Node ID 归一化，P1）。
> 评审输入：e2e-dedup-vision-quality（D-G12+V1-V4 已落地）+ PageFingerprint 公式（纯类型）+ L6/V1-V4 测试 + CPU 架构 §2。

### 11.1 与 FSM 无关但决定 FSM 行为的去重全景

| 机制 | 层级 | 维度 | 状态 |
|------|------|------|------|
| `_generatedPairs` (DynamicChildManager) | 引擎 | (fingerprint, childName) 对 | 现存 |
| VisitedNodes / VisitedChildren | 引擎/FSM | nodeId | 现存（Branch 消费 VisitedChildren） |
| VisitedPages / VisitedLevel1Menus/2Menus | 引擎 | 文本名 | 现存 |
| D-G12 `_childDestinations` | 引擎 | per-parent 目的地 int 指纹 | **已落地（L6-1/L6-2）** |
| V1-V4 | Vision | 排版/文本质量 | **已落地** |

**关键公式**（PageAnalysis.PageFingerprint，纯类型）：(Type.ToLower, Name) 排序多重集哈希——不含坐标、不含标题、对 Type+Name 全敏感。V4 归一化只解决字符级变体；**语义级变化（On/Off、百分比）仍改指纹**。

### 11.2 我的评审结论（摘要）

- **方案一方向正确但增量被高估**：PageFingerprint 已内建排序归一化；真正增量 = 内容选择（类型过滤）+ 裁决规则。PageTitle 字段无来源（PageAnalysis 无此字段），参与哈希引入新不稳定源。
- **方案二与方案一正交互补**（元素身份 vs 页面身份），但"无文本回退相对位置"应改为"回退视觉指纹"——统一兜底键。
- **三键并存（文本主键+视觉指纹+相对位置）缺裁决矩阵**——dedup 只在高置信度判等时跳过（宁可多访问，不可漏访问，与 D-G12 取舍一致）。
- **落地顺序依赖**：动态值排除依赖跨帧关联（V4 identity）→ 方案一 P0 应先做类型过滤（数据可得），P1 再做动态值排除。
- **最大实现风险**：D-G12 已落地测试绑定旧指纹语义，换键必须迁移 L6-1/L6-2 fixture + _generatedPairs 键类型。
