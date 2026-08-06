# FSM 矩阵加固与错误生命周期规范化 — 重构设计

> 状态: 设计阶段  
> 来源: fsm-analyzer 双轨分析（静态矩阵审计 + E2E run 运行时诊断）  
> 日期: 2026-08-05

---

## 1. 背景与动机

fsm-analyzer agent 对 TraversalFSM 进行了完整的双轨分析——静态矩阵审计（handler 返回值 × 转移矩阵边可达性 × 门限逻辑交互 × 拦截层覆盖）加运行时行为分析（enumerate-settings-safely E2E run 实测，120 步，FrameComplete=0）。发现 3 个 bug、3 条死边、2 个 handler 级问题。

### 1.1 发现清单

| # | 严重度 | 类型 | 描述 | 位置 |
|---|--------|------|------|------|
| 1 | 🔴 Bug | 异常路由不安全 | StepAsync catch 无条件路由到 ErrorHandling；当 fromState=ErrorHandling 或 NodeSelect 时，矩阵无此边 → DomainValidationException 未捕获传播 → 遍历会话崩溃 | TraversalFSM.cs:125-141 |
| 2 | 🔴 Bug | 连续错误计数入口点分散 | IncrementConsecutiveErrors 在 StepAsync catch、HandlePreconditionCheckAsync、HandleExecuteAsync catch、HandleErrorHandlingAsync 四处调用；异常路径 +2/周期，PopupHandling 路径 +1/周期——门限不一致 | TraversalFSM.cs:130,181,239,592 |
| 3 | 🔴 Bug | LastError 跨恢复残留 | LastError 被设置 3 处，从未清除。成功恢复后旧异常数据残留，可能误导后续 ErrorClassifier 做出错误恢复决策 | TraversalFSM.cs:129,179,237; ErrorContext.cs:42 |
| D1 | 🟡 Dead | Execute→Branch 无人生产 | HandleExecuteAsync 只返回 ResultVerify/ErrorHandling；拦截 Step 8 要求 handler 已返回 Branch（互锁） | TraversalFSM.cs:34 |
| D2 | 🟡 Dead | Branch→PreconditionCheck 无人生产 | HandleBranchAsync 只返回 NodeSelect/FrameComplete；拦截层无 PreconditionCheck；实际路径为两跳 Branch→NodeSelect→PreconditionCheck | TraversalFSM.cs:38 |
| D3 | 🟡 Dead | FrameComplete→ErrorHandling 无人生产 | HandleFrameCompleteAsync 为纯 Task.FromResult(NodeSelect)，无法抛异常 | TraversalFSM.cs:41 |
| H2 | 🟡 Issue | PopupHandling 失败不设 LastError | 弹窗 dismiss 失败返回 ErrorHandling 时，LastError 不设置 → ErrorHandler 无上下文 | TraversalFSM.cs:656-658 |

### 1.2 根因分析

这些问题的共同根因是**转移矩阵承担了两个未明说的职责**：

| 职责 | 检查什么 | 维护者 |
|------|---------|--------|
| Handler 门 | handler 显式返回的下一状态是否合法 | 各 handler 方法 |
| 异常路由门 | StepAsync catch 的 ErrorHandling 路由是否合法 | StepAsync catch 块 |

当同一个矩阵被两个不同机制消费时，死边和缺失边都变成 bug——handler 不生产的边（职责 1 应删）恰好是异常路由需要的边（职责 2 应留），反之亦然。

**本次重构的核心原则**：矩阵只管 Handler 门。异常路由走独立的安全降级通道，不依赖矩阵。

---

## 2. 设计

### 2.1 矩阵瘦身：移除 3 条死边

按 D-1 先例（`PreconditionCheck→Branch` 已因 "handler 从不返回" 移除），从 TransitionMatrix 移除：

```
Execute          → Branch              ← 移除（HandleExecuteAsync 从不返回 Branch）
Branch           → PreconditionCheck   ← 移除（HandleBranchAsync 从不返回 PreconditionCheck）  
FrameComplete    → ErrorHandling       ← 移除（HandleFrameCompleteAsync 无法抛异常）
```

**矩阵：22 边 → 19 边。** 每条剩余边至少有一个 handler 显式返回。

Handler 返回路径验证（移除后）：
- HandleNodeSelectAsync → Branch / PreconditionCheck ✅
- HandlePreconditionCheckAsync → Execute / ErrorHandling ✅
- HandleExecuteAsync → ResultVerify / ErrorHandling ✅
- HandleResultVerifyAsync → Branch / PopupHandling ✅
- HandleBranchAsync → NodeSelect / FrameComplete ✅
- HandleFrameCompleteAsync → NodeSelect ✅
- HandleErrorHandlingAsync → NodeSelect / Execute / FrameComplete / Branch ✅
- HandlePopupHandlingAsync → ResultVerify / ErrorHandling ✅

新增负面测试验证三条死边被拒绝（仿照 D-1 先例测试 `TransitionMatrix_PreconditionCheckToBranch_Rejected`）。

### 2.2 异常路由安全化：CanTransitionTo 守卫 + 降级链

**问题**：移除死边后，3 个状态不含 ErrorHandling 出边（NodeSelect / FrameComplete / ErrorHandling）。StepAsync catch 无守卫路由到非法状态 → `DomainValidationException` 崩溃。单一降级目标（FrameComplete）对 NodeSelect 和 FrameComplete 自身也是非法转移。

**方案**：catch 块不硬编码 ErrorHandling。改为守卫 + 按当前状态选择合法降级目标：

```csharp
// TraversalFSM.StepAsync — catch 块改造
catch (Exception ex)
{
    _logger.LogError(ex, "Step dispatch failed from {FromState}: {ExceptionType} — routing to recovery",
        fromState, ex.GetType().Name);
    RuntimeContext.SetLastError(ex);
    // 守卫: 矩阵是否允许此状态的异常路由?
    // 允许 → ErrorHandling; 不允许 → 按状态选择安全降级目标
    nextState = CanTransitionTo(TraversalState.ErrorHandling)
        ? TraversalState.ErrorHandling
        : fromState switch
        {
            TraversalState.NodeSelect => TraversalState.Branch,
            TraversalState.FrameComplete => TraversalState.NodeSelect,
            TraversalState.ErrorHandling => TraversalState.FrameComplete,
            _ => TraversalState.FrameComplete  // future-proof
        };
}
```

**降级链设计**：每个降级目标都经过矩阵验证（19 边矩阵下全部合法）：
- NodeSelect → Branch ✅（栈空时直接 Branch，不经过需要栈 Peek 的 PreconditionCheck）
- FrameComplete → NodeSelect ✅
- ErrorHandling → FrameComplete ✅

**已知取舍（步数燃烧）**：降级后的 FrameComplete → NodeSelect 不弹栈（FSM 的 FrameComplete handler 恒定返回 NodeSelect，Pop 仅在引擎 leaf-pop / stale-click 熔断 / DynamicMatch 拦截执行）。若 ErrorHandling 反复崩在同一帧内，降级链 ErrorHandling→FrameComplete→NodeSelect→PreconditionCheck→Execute→失败→ErrorHandling→崩→... 会燃烧步数直至 max_steps 终止。此行为优于重构前的会话崩溃（DomainValidationException 未经捕获传播），但不如显式弹栈高效。**不在本次重构范围加 Pop**——引擎 Pop 语义涉及 ChildPushed 状态机，应在拦截层统一处理（见 §3.2）。

**实际影响**：
- 当 fromState=ErrorHandling 且 HandleErrorHandlingAsync 内部 trace/日志写入抛异常 → 降级 FrameComplete，步数燃烧 → max_steps 终止（不崩溃）
- 当 fromState=NodeSelect（实践中不可能——handler 是 pure 的）→ 降级 Branch
- 当 fromState=FrameComplete（实践中不可能——handler 是 pure 的）→ 降级 NodeSelect
- 其余 5 个状态 → 行为不变（矩阵中均有 ErrorHandling 出边）

### 2.3 错误计数收敛：单一递增点

**问题**：`IncrementConsecutiveErrors` 在 4 处被调用，路径间不一致（异常路径 +2，PopupHandling 路径 +1）。

**方案**：`ConsecutiveErrors` 的语义不是"出了几次错"，而是"在同一棵子树里执行了几次恢复尝试"。增量应在恢复尝试完成时由 HandleErrorHandlingAsync 统一执行。

- **移除** StepAsync catch（line 130）的 `IncrementConsecutiveErrors()` — 只设 LastError，不管计数
- **移除** HandlePreconditionCheckAsync（line 181）的 `IncrementConsecutiveErrors()` — 同上
- **移除** HandleExecuteAsync catch（line 238）的 `IncrementConsecutiveErrors()` — 同上
- **保留** HandleErrorHandlingAsync（line 592）的 `IncrementConsecutiveErrors()` — 唯一递增点

结果：每次恢复尝试 +1。门限 `>= 3` = 精确的 "3 次恢复尝试后放弃"。所有路径（异常路由 / handler 显式返回 ErrorHandling / PopupHandling 失败）一致。

### 2.4 LastError 生命周期：处置完毕清零

**问题**：LastError 被设置后从不清理。成功恢复后旧异常数据残留。消费者不止 HandleErrorHandlingAsync 自身——`TraversalEngine:345-348` 的 `OnErrorAsync` 钩子在 `NextState==ErrorHandling` 时读取 LastError、`PopupHandler:354/391` 的 preserve/restore 也捕获 LastError（restore 用 `new Exception(msg)` 重建，丢失原始类型）。残留值在跨恢复后的 popup restore 场景尤具误导性。

**方案**：一次错误处置的生命周期 = SetLastError（入口）→ HandleErrorHandlingAsync 读取 + 分类 + 执行恢复 → SetLastError(null)（出口）。

- 在 `HandleErrorHandlingAsync` 的**全部 3 个返回点**前加 `ctx.SetLastError(null)`：
  - page-item 门限路径（line 608 前）
  - consecutive 门限路径（line 621 前）
  - 主返回路径（line 630 前）
- 无论 strategy 结果如何（Retry / Backtrack / Skip / Continue / Abort），处置完毕即清零
- 下游 handler 均不读 LastError；引擎 OnErrorAsync 钩子在入口步触发（早于清零）；popup restore 在清零后 preserve 捕获 null → restore null（安全）

**不在 ResultVerify 清的原因**：ResultVerify 是"操作验证通过"，不是"错误处置完毕"。恢复路径可能不经过 ResultVerify（Retry → Execute → 又失败直接回 ErrorHandling），清除点必须在 HandleErrorHandlingAsync 是必经之路。

### 2.5 PopupHandling 失败：补全错误上下文

**问题**：HandlePopupHandlingAsync 失败返回 ErrorHandling 时，不设 LastError ，也不递增。处理完 Bug #2 收敛后递增问题自动解决，但 ErrorClassifier 仍无上下文。

**方案**：弹窗 dismiss 失败时，构建描述性异常并设置 LastError。注意消息不能内嵌枚举名——`ErrorClassifier`（`ErrorHandler.cs:13-48`，大小写不敏感 substring 匹配）会将消息中的 `"Permission"` / `"Timeout"` 误分类：

```csharp
if (!result.Success)
{
    var detail = result.Classification is { } c
        ? $"Popup dismiss failed: dismiss_action={result.Action}"
        : $"Popup dismiss failed: action={result.Action}";
    ctx.SetLastError(new InvalidOperationException(detail));
}
```

消息不含 `PopupType` / `DismissStrategy` 枚举名 → ErrorClassifier 归类为 `ErrorType.Unknown` → 走通用恢复策略。后续若需精确分类弹窗失败，应在 `ErrorClassifier` 中增加 `"popup dismiss failed"` 模式匹配，而非依赖枚举值 substring 碰撞。

### 2.6 测试策略

本次重构改动量小（~25 行源码，核心文件 1 个），但涉及错误恢复门限行为变更。现有 237 个测试**零破坏**——所有测试将继续通过。

新增 6 个测试。每个测试按 Arrange / Act / Assert 规格化。

#### 测试 T1：ErrorHandling_InternalException_SafeDegradeToFrameComplete（P0）

**覆盖**：Bug #1 + ISSUE A/B。HandleErrorHandlingAsync 内部抛异常时不崩溃，按状态降级。

```
Arrange:
  - 创建 TraversalRuntimeContext + TraversalFSM
  - 通过 FsmSimulationHarness.DriveTo 驱动到 ErrorHandling
  - 创建 StepContext，ErrorHandler 的 HandlerTraceWriter 配置为抛 InvalidOperationException
    （模拟 HandleErrorHandlingAsync 内部 trace 写入失败）
  - 设置 LastError 为 "original error"（由前置 handler 写入）

Act:
  - 调用 fsm.StepAsync(stepCtx)  ← HandleErrorHandlingAsync 内部抛异常
  - catch 块 CanTransitionTo(ErrorHandling) → false（自环不在矩阵中）
  - 降级到 FrameComplete（矩阵合法：ErrorHandling→FrameComplete）

Assert:
  - 不抛 DomainValidationException（核心断言——不崩溃）
  - result == TraversalState.FrameComplete
  - LastError == "original error"（catch 块重设为入口异常，非 trace 异常）
  - FSM.CurrentState == TraversalState.FrameComplete（TransitionTo 成功）
```

**变体 T1a — NodeSelect 源异常降级到 Branch**：

```
Arrange:
  - FSM 在 NodeSelect 状态，栈为空
  - StepContext 配置为 HandleNodeSelectAsync 正常（pure handler 不抛），
    但用反射或 mock 强制 StepAsync 的 DispatchHandlerAsync 抛 InvalidOperationException

Act:
  - CanTransitionTo(ErrorHandling) → false（NodeSelect 行无 ErrorHandling）
  - 降级到 Branch（NodeSelect→Branch 合法）

Assert:
  - result == TraversalState.Branch
  - 不抛 DomainValidationException
```

#### 测试 T2：ErrorHandling_SuccessfulRecovery_ClearsLastError（P0）

**覆盖**：Bug #3 + ISSUE E。HandleErrorHandlingAsync 全部 3 条返回路径清零 LastError。

```
Arrange:
  - 创建 ctx + fsm，驱动到 ErrorHandling，SetLastError("test error")
  - 创建 StepContext，ErrorHandler = StrategyForcingHandler(ErrorStrategy.Retry)

Act + Assert（子用例 2a — 主返回路径）:
  - fsm.StepAsync(stepCtx) → Retry → Execute
  - Assert: LastError == null

Arrange（子用例 2b — page-item 门限路径）:
  - ctx.NodeFailedItems = 5（触发 page-item limit gate）
  - ctx.NodeStack.Push(node, depth=2)（depth>1 条件满足）
  - ErrorHandler = StrategyForcingHandler(ErrorStrategy.Skip)
  - FakeActionExecutor(returns: true)（PressBack 需要）

Act:
  - fsm.StepAsync(stepCtx) → Skip → Branch... 但 page-item gate 先触发
  - PressBack → FrameComplete

Assert:
  - LastError == null
  - result == TraversalState.FrameComplete

Arrange（子用例 2c — consecutive 门限路径）:
  - ctx.ConsecutiveErrors 手动设为 2（HandleErrorHandlingAsync 递增后到 3 → >=3 触发）
  - ctx.NodeStack.Push(node, depth=2)
  - ErrorHandler = StrategyForcingHandler(ErrorStrategy.Backtrack)

Act:
  - fsm.StepAsync(stepCtx) → 递增到 3 → gate 触发

Assert:
  - LastError == null
  - result == TraversalState.FrameComplete（PressBack → FrameComplete）
```

#### 测试 T3：ErrorHandling_FullCycle_ConsecutiveErrorsIncrementsOnce（P1）

**覆盖**：Bug #2。完整错误周期（Execute 抛异常 → StepAsync catch → HandleErrorHandlingAsync）ConsecutiveErrors 精确 +1。

```
Arrange:
  - 创建 ctx + fsm，驱动到 Execute
  - StepContext 的 IActionExecutor 配置为 TapAsync 抛 TimeoutException
  - ErrorHandler = StrategyForcingHandler(ErrorStrategy.Retry)
  - ctx.ConsecutiveErrors 初始 = 0

Act:
  - fsm.StepAsync(stepCtx)
    → HandleExecuteAsync 内部 action.TapAsync 抛异常
    → RuntimeContext.SetLastError + return ErrorHandling（不递增——已移除）
    → StepAsync: TransitionTo(ErrorHandling) → 返回 ErrorHandling（无 catch，handler 显式返回）
  - 下次 StepAsync(stepCtx)
    → HandleErrorHandlingAsync: 递增到 1 → Retry → Execute

Assert:
  - ctx.ConsecutiveErrors == 1（不是 2——修复前为 2）
  - result == TraversalState.Execute

变体 — 异常路由路径:
  // 同上但 handler 抛未被捕获的异常（经 StepAsync catch 路由）
  // StepAsync catch: SetLastError（不递增）
  // TransitionTo(ErrorHandling) → 下次 HandleErrorHandlingAsync → 递增到 1
  // Assert: ConsecutiveErrors == 1
```

#### 测试 T4：PopupHandling_Failure_SetsLastError（P1）

**覆盖**：H2 + ISSUE F。弹窗 dismiss 失败时设置 LastError，消息不含枚举名。

```
Arrange:
  - 创建 ctx + fsm，驱动到 PopupHandling
  - PageAnalysis = PopupPage("Allow access")  // IsPopup=true
  - PopupHandler = 自定义 handler，HandlePopup 返回 success=false, Action="Back"
  - ctx.LastError 初始 = null

Act:
  - fsm.StepAsync(stepCtx)
    → HandlePopupHandlingAsync: HandlePopup 返回 fail → 构建 InvalidOperationException
    → return ErrorHandling

Assert:
  - result == TraversalState.ErrorHandling
  - ctx.LastError != null
  - ctx.LastError is InvalidOperationException
  - ctx.LastError.Message == "Popup dismiss failed: dismiss_action=Back"
  - ctx.LastError.Message 不含 "Permission" / "Error" / "Timeout" / "Ad" / "Dialog" / "Anr"
    （验证 ISSUE F——不触发 ErrorClassifier 误分类）

变体 — 无 Classification:
  - result.Classification == null
  - Assert: ctx.LastError.Message == "Popup dismiss failed: action=Back"
```

#### 测试 T5：PopupHandling_Failure_TriggersOnErrorAsyncHook（P1）

**覆盖**：ISSUE G。弹窗失败后引擎 OnErrorAsync 钩子必然触发。

```
Arrange:
  - 使用 SimulationE2ETests 的引擎基础设施（TraversalEngine + StateFixture）
  - 注册 ITraversalHook，记录 OnErrorAsync 调用
  - PopupHandler 配置为返回 fail

Act:
  - 引擎执行到 PopupHandling 失败 → ErrorHandling
  - 引擎 RunAsync 循环检测到 NextState==ErrorHandling && LastError!=null

Assert:
  - OnErrorAsync 被调用 1 次
  - OnErrorAsync 收到的 Exception 是 InvalidOperationException（非 null / 非残留旧异常）
  - OnErrorAsync 收到的 nodeId 非空
```

#### 测试 T6：TransitionMatrix_DeadEdges_Rejected（P1）

**覆盖**：死边移除 + ISSUE B。仿 D-1 先例，验证 6 条非法边被拒绝。

```
Arrange:
  - 创建 TraversalRuntimeContext + TraversalFSM
  - 驱动到各源状态

Act + Assert（逐边）:
  1. fsm 在 Execute → TransitionTo(Branch) → DomainValidationException
  2. fsm 在 Branch → TransitionTo(PreconditionCheck) → DomainValidationException
  3. fsm 在 FrameComplete → TransitionTo(ErrorHandling) → DomainValidationException
  4. fsm 在 NodeSelect → TransitionTo(ErrorHandling) → DomainValidationException
  5. fsm 在 ErrorHandling → TransitionTo(ErrorHandling) → DomainValidationException
  6. fsm 在 FrameComplete → TransitionTo(FrameComplete) → DomainValidationException
  7. 正向验证：fsm 在 ErrorHandling → TransitionTo(FrameComplete) → 通过（降级目标合法）
  8. 正向验证：fsm 在 NodeSelect → TransitionTo(Branch) → 通过（降级目标合法）
```

### 2.7 验收标准

以下标准全部满足方可合并：

| # | 标准 | 验证方式 |
|---|------|---------|
| AC1 | 237 现有测试全部通过（零回归） | `dotnet test --filter "FullyQualifiedName~StateMachine"` |
| AC2 | 6 个新测试全部通过 | 同上 |
| AC3 | 矩阵 19 边结构有效（无自环、全部可达、无死状态） | `matrix_from_source.py --json` → issues: [] |
| AC4 | 源码矩阵与 fsm-design.md 文档一致 | `matrix_from_source.py --diff-docs` → exit 0 |
| AC5 | HandleErrorHandlingAsync 内部异常不导致 DomainValidationException | T1 + 手动 E2E（注入 trace 写入失败） |
| AC6 | 连续 3 次错误恢复后 PressBack（所有路径一致） | T3 + 现有 ErrorHandling_ThreeBacktracks 仍通过 |
| AC7 | 错误恢复后 LastError 为空（3 条返回路径全覆盖） | T2 三个子用例 |
| AC8 | 弹窗失败时 ErrorHandler 收到有效 LastError（不含枚举名） | T4 + T5 |
| AC9 | 死边 + 自环被 DomainValidationException 拒绝 | T6 全部 6 条边 + 2 条正向 |
| AC10 | E2E enumerate-settings-safely 回归（max_steps 行为不变） | 实际 run 或仿真回归 |

### 2.8 遗漏检查清单

交叉引用 fsm-analyzer 全部 11 项发现与测试覆盖：

| # | 发现 | 严重度 | 测试覆盖 | 处理 |
|---|------|--------|---------|------|
| 1 | Bug #1 异常路由崩溃 | 🔴 | T1 + T1a | §2.2 修复 |
| 2 | Bug #2 双倍递增 | 🔴 | T3 | §2.3 修复 |
| 3 | Bug #3 LastError 残留 | 🔴 | T2 (3 子用例) | §2.4 修复 |
| 4 | D1 Execute→Branch 死边 | 🟡 | T6-1 | §2.1 移除 |
| 5 | D2 Branch→PreconditionCheck 死边 | 🟡 | T6-2 | §2.1 移除 |
| 6 | D3 FrameComplete→ErrorHandling 死边 | 🟡 | T6-3 | §2.1 移除 |
| 7 | H2 Popup 失败无 LastError | 🟡 | T4 + T5 | §2.5 修复 + ISSUE G |
| 8 | ISSUE A NodeSelect→FrameComplete 非法 | 🟡 | T1a + T6-7 | §2.2 降级链 |
| 9 | ISSUE B FrameComplete 新崩溃向量 | 🟡 | T6-3, T6-6 | §2.2 降级链 + 死边移除 |
| 10 | ISSUE C 步数燃烧 | 🔵 | — | §2.2 已知取舍（设计文档化） |
| 11 | ISSUE E 3 返回点清零 | 🟡 | T2 (2b+2c) | §2.4 修复 |
| 12 | ISSUE F ErrorClassifier 碰撞 | 🟡 | T4 (消息不含枚举名) | §2.5 修复 |
| 13 | ISSUE G OnErrorAsync 钩子 | 🟡 | T5 | §4.2 行为变更表 |
| 14 | ISSUE D LastError 消费者声明错误 | 🔵 | — | §2.4 文档修正 |
| 15 | R1 DynamicMatch FrameComplete=0 | 🔵 | — | 不在范围（可观测性，非功能缺陷） |
| 16 | R2 deny.default on wait | 🔵 | — | Host 层，不在范围 |
| 17 | R3 行动效率 ~18% | 🔵 | — | 性能指标，不在范围 |
| 18 | line 239→238 行号修正 | 🔵 | — | §2.3 已修正 |

**无遗漏**：18 项发现中，10 项由 6 个新测试直接覆盖，4 项设计文档化或不在范围，2 项文档修正已在设计本体中，2 项（R1/R2）确认非 FSM 层范围。

---

## 3. 跨层交互附录

### 3.1 Vision→FSM 传导路径

E2E run 诊断发现一条 vision 层→FSM 层的传导路径：

```
OCR 文本变体 ("App security" vs "Appsecurity")
  → PageSnapshotManager.Fingerprint 变化（sorted (type,name) 全量 hash）
  → DynamicChildManager.GetNextUnvisitedChild 找不到匹配条目 → 返回 null
  → InterceptionHandler.OnDynamicMatchNodeSelect 走 press_back_parent_frame_differs
  → FSM 回到 NodeSelect，不进 FrameComplete
  → 在 DynamicMatch 容器中循环直至 max_steps
```

根因在 vision 层（OCR 文本不一致），但 FSM 层应加防御：

1. **fingerprint 稳定性 guard**（引擎层，不在本次重构范围）：对同一页面在时间窗口内允许多个相似 fingerprint 视为同一页面
2. **FrameComplete 超时兜底**（拦截层）：若 DynamicMatch 容器超过 N 次 NodeSelect 重进仍未 FrameComplete，强制 FrameComplete

### 3.2 拦截层接管范围

`InterceptionHandler` 对 DynamicMatch 容器的 3 个拦截点（OnBranch / OnDynamicMatchNodeSelect / OnFrameComplete）产生的 NextState 均不经过 FSM 矩阵校验。当前正确行为依赖拦截层与 FSM handler 的一致性——若拦截层返回了 handler 不生产的 NextState（如 PreconditionCheck），错误仅在 trace 中可见，FSM 不崩溃但行为不可预测。**建议**：在拦截层的 NextState 赋值点加 `Debug.Assert(fsm.CanTransitionTo(nextState))` 验证覆盖的 NextState 在矩阵内。

---

## 4. 影响范围

### 4.1 文件变更

| 文件 | 变更 | 估计行数 |
|------|------|---------|
| `src/UniClaw.Core/StateMachine/TraversalFSM.cs` | 矩阵去 3 边 + catch 守卫降级链 + 递增收敛（-3）+ LastError 清零（3 返回点）+ Popup LastError 设置 | ~30 |
| `tests/UniClaw.Core.Tests/StateMachine/StateMachineTests.cs` | T1 + T1a（异常路由守卫降级）+ T6（死边拒绝，8 条断言） | ~100 |
| `tests/UniClaw.Core.Tests/StateMachine/HandleErrorHandlingTests.cs` | T2（3 子用例）+ T3（2 变体） | ~100 |
| `tests/UniClaw.Core.Tests/StateMachine/HandlePopupHandlingTests.cs` | T4（2 变体）+ T5（钩子触发） | ~60 |
| `docs/system/patterns/fsm-design.md` | 矩阵表更新（22→19 边）+ 异常路由降级机制一节 | ~20 |

### 4.2 行为变更

| 场景 | 之前 | 之后 |
|------|------|------|
| HandleErrorHandlingAsync 内部抛异常 | DomainValidationException 崩溃 → 遍历会话终止 | 降级链（按状态选择合法目标），步数燃烧 → max_steps 终止 |
| HandleErrorHandlingAsync 内部抛异常 / fromState=NodeSelect | DomainValidationException 崩溃 | 降级 Branch |
| 连续 3 次错误恢复后 PressBack | 异常路径 2 次即触发，PopupHandling 路径 3 次触发 | 全部路径精确 3 次触发 |
| 错误恢复成功后 | LastError 残留旧值 → 可能误导后续 ErrorClassifier + popup restore 复活类型退化异常 | LastError 清零（3 返回点全覆盖）；popup restore 捕获 null → restore null（安全） |
| 弹窗 dismiss 失败 | ErrorHandler 收到 null LastError → 无上下文决策；引擎 OnErrorAsync 钩子不触发（无 LastError） | ErrorHandler 收到 `InvalidOperationException("Popup dismiss failed: dismiss_action=...")` |
| 弹窗 dismiss 失败 → 引擎 OnErrorAsync 钩子 | 仅在 popup restore 恰好残留旧 LastError 时触发（不可靠） | 必然触发（LastError 已设置） |
| 矩阵边数 | 22 | 19 |

---

## 5. 验证

1. `dotnet test --filter "FullyQualifiedName~StateMachine"` — 237 + 6 = 243 tests pass（先跑基线确认 237 → 再加新测试确认 243）
2. `matrix_from_source.py --diff-docs` — 文档矩阵与源码一致（更新 fsm-design.md 后，**一并补齐已知遗漏的 ResultVerify→ErrorHandling 行**）
3. `matrix_from_source.py --json` — 结构验证通过（19 边，无自环，全部可达，无死状态）
4. E2E run — enumerate-settings-safely 回归（验证 max_steps 行为不变，错误场景触发降级不崩溃）

## 6. 验收标准

| # | 标准 | 验证方式 |
|---|------|---------|
| AC1 | 237 现有测试全部通过（零回归） | `dotnet test --filter "FullyQualifiedName~StateMachine"` |
| AC2 | 6 个新测试全部通过 | 同上 |
| AC3 | 矩阵 19 边结构有效（无自环、全部可达、无死状态） | `matrix_from_source.py --json` → issues: [] |
| AC4 | 源码矩阵与 fsm-design.md 文档一致（exit 0） | `matrix_from_source.py --diff-docs` |
| AC5 | HandleErrorHandlingAsync 内部异常不导致 DomainValidationException | T1 + T1a |
| AC6 | 连续 3 次错误恢复后 PressBack（所有路径一致） | T3 + 现有 `ErrorHandling_ThreeBacktracks` 仍通过 |
| AC7 | 错误恢复后 LastError 为空（3 条返回路径全覆盖） | T2 三个子用例 |
| AC8 | 弹窗失败时 ErrorHandler 收到有效 LastError（不含枚举名） | T4 + T5 |
| AC9 | 死边 + 自环被 DomainValidationException 拒绝 | T6 全部 6 条边 + 2 条正向 |
| AC10 | E2E enumerate-settings-safely 回归 | 实际 run 或仿真回归：行为不变，错误场景不崩溃 |
