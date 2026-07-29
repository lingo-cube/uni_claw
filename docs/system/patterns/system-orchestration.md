# Patterns — System Orchestration

> **Tier 2 · Patterns**: 系统整体架构、执行生命周期与跨层数据流转。
> 新增 FSM state / Step 逻辑 / Handler / 数据链路变更时更新此文档。
> 约束: → constitution C-4, C-5
> 决策: → decisions/log D-5, D-7, D-8, D-14, D-15, D-16, D-17

---

## 1. 自顶向下整体架构

UniClaw.Core 是一个 **UI 自动遍历引擎**——给定一个 App 和遍历意图 (IntentSlots)，引擎自动发现页面结构、处理 popup、恢复错误，直到完成或终止。

### Host 组合根与真实设备短闭环

`UniClaw.Host` 是 Core、Device 与具体 provider 的最外层组合根。Core 不引用
Host、Device 或 provider。Android Settings 的最小闭环采用短计划，不把原始
ADB 命令交给 AI：

```text
versioned scenario + policy
        ↓ validate/hash/snapshot
ScenarioPlanCompiler → TraversalPlan
        ↓
observe → analyze → one-action step plan
        ↓
deterministic safety gate (default deny)
        ↓ allow only
Device IActionExecutor → ADB
        ↓
re-observe → verify → result/issues/trace
```

Host 命令职责：

- `doctor`：只读检查 device、截图、UIAutomator、provider 与输出目录。
- `analyze`：只读产生一次 `PageAnalysis`，设备动作数必须为 0。
- `run`：执行一个已验证场景；每个真实 entry/action 都经过同一 safety gate。

运行资产与 Core trace 是互补关系：Host 保存 run/step/safety/verification
证据，`ITraceRecorder` 继续保存执行语义；二者共享 run/step/page fingerprint
关联字段。详见 `layers/host.md`、`layers/device.md` 与
`layers/observability.md`。

### 五层职责

```
┌─────────────────────────────────────────────────────────────────┐
│ Domain — 纯数据模型                                              │
│ 24+2 不可变类型, 零业务逻辑, 零运行时依赖                          │
│ PageAnalysis (12 fields) = AI 分析后的页面结构化输出               │
│ Operation = 执行动作的描述 (click/type/scroll/wait)               │
│ TypeHint/MenuItemType/ExpectedAction = 元素分类体系               │
├─────────────────────────────────────────────────────────────────┤
│ Graph — 遍历蓝图编译                                             │
│ IntentSlots → PlanCompiler → TraversalPlan (确定性的, 无 AI)     │
│ DynamicMatcher = 运行时匹配 (PageAnalysis → 匹配子节点)           │
│ TraversalNode/MatchCondition = 节点 schema + 匹配规则             │
├─────────────────────────────────────────────────────────────────┤
│ StateMachine — 运行时状态 + Handler                              │
│ TraversalFSM = 微观 8 状态 (每步决策)                             │
│ GlobalFSM = 宏观 8 状态 (整体进度)                                │
│ PopupHandler = 6-step pipeline; Container/Error = 3 子组件各自独立 │
│ TraversalRuntimeContext = 30 可变状态 (26 core + 4 额外)         │
│ NodeStack = DFS 深度栈                                           │
├─────────────────────────────────────────────────────────────────┤
│ Traversal — 14-step 主循环编排                                   │
│ StepOrchestrator = 每步拦截 FSM, 执行 Branch/NodeSelect 逻辑      │
│ DynamicChildManager = 子节点生成 (9-step + dedup)                 │
│ TraceCoordinator = 可观测性 (15 span methods + 2 gates, Log-and-Continue) │
│ EntryPolicyExecutor = 入口策略 (3 strategies + fallback)         │
│ PageSnapshotManager = 确定性指纹 (检测页面变化)                   │
├─────────────────────────────────────────────────────────────────┤
│ AI — 外部 AI 顾问 (Phase 2+ skeleton)                           │
│ IAIStrategyAdvisor = 5 async methods (页面分析 + 策略建议)        │
│ TraversalContextSnapshot = AI 只读上下文 (8 immutable fields)     │
└─────────────────────────────────────────────────────────────────┤
│ Observability — 横切可观测性 (cross-cutting utility)              │
│ ITraceRecorder = 11 async methods (trace 持久化)                  │
│ 被 StateMachine + Traversal 共同消费, 非传统顶层                   │
└─────────────────────────────────────────────────────────────────┘
```

### 层间依赖方向 (→ constitution C-4, C-5)

```
Domain ←──── Graph ←──── StateMachine ←──── Traversal ←──── AI
(被引用)     (被引用)    (被引用)         (被引用)      (被引用)

Cross-cutting:
  Observability ── 被 StateMachine + Traversal 共同消费 (非顶层, 是横切 utility)

严格单向 (无违规):
  Domain 不引用上层 (纯模型) ✅ verified
  Graph 不引用 StateMachine ✅ verified

存在向上引用 (实际依赖, 非设计缺陷):
  StateMachine → Traversal (StepContext 引用 Traversal 类型)
  StateMachine → Observability (TraversalRuntimeContext 引用 ITraceRecorder 等)
  Traversal → Observability (TraversalEngine 定义 TraceCoordinator 包装 ITraceRecorder)

遗留 stub (→ D-6 部分修正, ITraversalNode 已移到 Graph):
  IGraphTraversalEngine 在 StateMachine namespace 有空 stub
  等同名接口在 Traversal namespace 有完整定义
  → decisions/log D-14: 清理 stub, pending layering 决策
```

---

## 2. FSM 执行生命周期

### 2.1 完整遍历运行的时间线

```
                    ┌─ GlobalFSM ──────────────────────────────────────┐
                    │                                                  │
初始化阶段          │  Idle → Initializing → Traversing                │
                    │    │         │            │                       │
                    │    │  EntryPolicy    ┌────┴────┐                 │
                    │    │  Executor       │         │                 │
                    │    │  (3 strategies) │         │                 │
                    │    │                 │         │                 │
遍历循环 ──→→→→→→→ │    │         ┌────────────────────────────┐      │
                    │    │         │  TraversalFSM 8-state loop │      │
                    │    │         │  (StepOrchestrator 14-step)│      │
                    │    │         │  NodeSelect → Branch →     │      │
                    │    │         │  Execute → Verify →        │      │
                    │    │         │  FrameComplete → next node │←←←← │
                    │    │         │                            │      │
                    │    │         │  Handlers:                 │      │
                    │    │         │  popup → container → error │      │
                    │    │         └────────────────────────────┘      │
                    │    │                  │                         │
异常路径            │    │         Error → Recovering → Initializing   │
                    │    │                  │ → Traversing (恢复)     │
                    │    │                  │ → Terminated (放弃)     │
                    │                                  │              │
终止                │                         Completed │ Terminated  │
                    │                           (terminal) (terminal)  │
                    └─────────────────────────────────────────────────┘
```

### 2.2 单步执行流 (StepOrchestrator 14-step)

每次 TraversalFSM.Step() 后，StepOrchestrator 执行 14 个拦截点。
拦截层 (Steps 8-10) 的决策逻辑委托 `IInterceptionHandler` (默认实现 `InterceptionHandler`, → D-80):
orchestrator 只保留调用守卫 (nextState 匹配 + BranchAllowedSources / DynamicMatch 判定),
handler 返回 `InterceptionResult`, 以 `intercepted` flag 守卫应用。

```
Step 1:  NodeStackAdapter 创建 (每步一次)
Step 2:  Trace 记录 step start (no-op when Active=False)
Step 3:  FSM.Step() → 迁移到新 state ←──── FSM 内部 enum-switch dispatch
Step 4:  路径变化检测 → 记录 PageAnalysis (path changed?)
Step 5:  记录 action execution (上次 handler 的 action)
Step 6:  记录 metrics spans (placeholder)
Step 7:  记录 state transition (from → to)
         ┌─── 拦截层 (Steps 8-10) ────────────────────────────┐
Step 8:  BRANCH 拦截 — 只从 Execute/ResultVerify/NodeSelect │
         │ 有 unvisited child → Push child                     │
         │ 无 child → force FrameComplete                      │
Step 9:  NODE_SELECT + DynamicMatch 拦截                      │
         │ 有 child → Push child                               │
         │ 无 child → anti-loop: back + pop + return immediately│
Step 10: FRAME_COMPLETE 拦截 — DynamicMatch 有剩余 child      │
         │ 有 remaining → Override: Push child, state=NodeSelect│
         │ 无 remaining → 正常 FrameComplete                   │
         └────────────────────────────────────────────────────┘
Step 11: 确定最终 nextState (考虑 override)
Step 12: 更新 VisitedNodes
Step 13: 动态子节点缓存失效 (path changed 时)
Step 14: Trace 记录 step end
```

**关键拦截逻辑**:
- **Step 8**: BRANCH 只允许从 Execute/ResultVerify/NodeSelect 进入 (D-1: PreconditionCheck→Branch 已禁止)
- **Step 9**: anti-loop — DynamicMatch 无剩余子节点时强制 back + pop，立即返回 FrameComplete
- **Step 10**: FRAME_COMPLETE override — 如果 DynamicMatch 还有剩余子节点，改写为 NodeSelect + push child

### 2.3 Handler 在 FSM 中的调用位置

```
TraversalFSM.Step() 内部 dispatch:
  HandleNodeSelect()        → 空 stack → Branch; 有 stack → PreconditionCheck
  HandlePreconditionCheck() → Execute (placeholder, real check Phase 2.3)
  HandleExecute()           → ResultVerify (placeholder)
  HandleResultVerify()      → Branch (默认) 或 PopupHandling (如果检测到 popup)
  HandleBranch()            → NodeSelect (placeholder)
  HandleFrameComplete()     → NodeSelect (placeholder)
  HandleErrorHandling()     → NodeSelect (placeholder, real handling Phase 2.3)
  HandlePopupHandling()     → ResultVerify (placeholder)

Handler 实际调用发生在 StepOrchestrator 拦截之后:
  - PopupHandler.HandlePopup() 在 ResultVerify 或 PopupHandling state 时触发 (6-step)
  - Container 3 子组件 (CompletionDetector/FallbackDecider/ContainerActionExecutor) 在 FrameComplete 时触发
  - Error 3 子组件 (ErrorClassifier/ErrorStrategySelector/RecoveryExecutor) 在 ErrorHandling state 时触发
```

---

## 3. 跨层数据流转图

### 3.1 从 IntentSlots 到 TraversalPlan (Graph 层)

```
IntentSlots (scope, target, target_path)
     │
     ▼
PlanCompiler.compile()
     │ 1. validate_slots (scope legality)
     │ 2. build_entry_policy
     │ 3. build_root_node
     │ 4. build_completion_policy
     │ 5. assemble TraversalPlan
     │ 6. build_static_nodes (target_path scope only, 内嵌于 Step 5 构造调用)
     │
     ▼
TraversalPlan (12 fields)
  ├── Root node (TraversalNode)
  ├── EntryPolicy
  ├── CompletionPolicy (ExitConditions + MaxContainers)
  ├── Mode, TraceLevel, etc.
  └────────── NodeData (node registry)
```

### 3.2 遍历循环中的数据流 (核心循环)

```
                    ┌── 外部输入 ──────────────┐
                    │                           │
截图 → AI → PageAnalysis (Domain.Content)       │
                    │                           │
                    ▼                           │
┌── StepOrchestrator 循环 ──────────────────────┤
│                                               │
│  ① FSM.Step() → 确定下一 state               │
│                                               │
│  ② Context 更新:                              │
│     MarkVisited, MarkNodeVisited              │
│     AppendPath/PopPath                        │
│     CurrentFrame = next child                 │
│                                               │
│  ③ DynamicMatcher (如果 DynamicMatch):        │
│     PageAnalysis → MatchableItems             │
│     MatchAll(rules, items) → MatchResults     │
│     TemplateInstantiator → child nodes        │
│                                               │
│  ④ Handler dispatch (按 FSM state):           │
│     PopupHandler → PopupHandlingResult (6-step: detect→classify→preserve→handle→restore→validate) │
│     Container 3 子组件 → ContainerActionResult │
│     Error 3 子组件 → ErrorRecoveryResult      │
│                                               │
│  ⑤ Action 执行 (IActionExecutor):             │
│     Operation → PressBackAsync/ClickAsync     │
│     → 新截图 → 新 PageAnalysis                │
│                                               │
│  ⑥ PageSnapshotManager:                       │
│     Fingerprint(before) vs Fingerprint(after) │
│     → pathChanged flag                        │
│                                               │
│  ⑦ TraceCoordinator:                          │
│     RecordStepStart/End/Transition/etc.       │
│     (no-op when Active=False)                 │
│                                               │
└──→→→→ 循环回到 ① ────────────────────────────┘
                    │
                    │ GlobalFSM: Traversing → Completed
                    ▼
               遍历结束
```

### 3.3 关键数据对象在各层的归属

| 数据对象 | 产生层 | 消费层 | 流经路径 |
|---------|--------|--------|---------|
| `IntentSlots` | Traversal (输入) | Graph | Traversal → PlanCompiler |
| `TraversalPlan` | Graph (编译) | Traversal | PlanCompiler → TraversalEngine 初始化 |
| `PageAnalysis` | Domain (AI 输出) | StateMachine, Graph | AI → Context → DynamicMatcher → StepOrchestrator |
| `Operation` | Domain.Common | Traversal (执行) | TraversalPlan → StepOrchestrator → IActionExecutor |
| `TraversalState` | StateMachine | Traversal | TraversalFSM.Step() → StepOrchestrator 拦截 |
| `GlobalState` | StateMachine | Traversal | GlobalFSM transition → Context.GlobalState |
| `MatchResult` | Graph | Traversal | DynamicMatcher → DynamicChildManager → NodeStack |
| `PopupHandlingResult` | StateMachine | Traversal | PopupHandler → StepOrchestrator |
| `ContainerActionResult` | StateMachine | Traversal | ContainerActionExecutor → StepOrchestrator |
| `ErrorRecoveryResult` | StateMachine | Traversal | RecoveryExecutor → StepOrchestrator |
| `TraversalContextSnapshot` | StateMachine | AI | Context.CreateReadOnlySnapshot() → IAIStrategyAdvisor |

### 3.4 Context 的字段分类

TraversalRuntimeContext 有 **30 可变状态项**：26 core + 4 额外。

**26 core mutable fields** 按职责分为 5 子系统（名称为推导性分类, 非 canonical 定义 → D-15）：

| 子系统 | 字段 | 用途 |
|--------|------|------|
| **DFS 遍历** | nodeStack, currentPath, currentPageAnalysis, currentFingerprint, visitedPages, visitedNodes, visitedChildren, visitedLevel1Menus, visitedLevel2Menus | 栈 + 路径 + 访问记录 |
| **进度控制** | maxDepth, stepCount, retryCount, completionPolicy | 深度限制 + 步数 + 重试 + 完成策略 |
| **错误追踪** | consecutiveErrors, failedNodes, lastError, exceptionChain | 错误计数 + 失败节点 + 异常链 |
| **宏观状态** | globalState, deviceExperience, traceId | GlobalFSM + 设备体验 + trace ID |
| **缓存与配置** | pageTree, actionHistory, aiProvider, pageCache, waitAfterActionMs | 页面树 + 操作历史 + AI + 缓存 + 等待 |

**4 额外可变状态** (不在标注块 26 中):

| 字段 | 类型 | 说明 |
|------|------|------|
| `_scrollHandler` | `object?` | Phase 3 reserved, TODO |
| `_currentSnapshot` | `object?` | Phase 3 reserved, TODO |
| `_visitedChildrenReadOnly` | `ReadOnlyDictionary?` | 惰性重建缓存, null 时重建 |
| `CurrentFrame` | `ITraversalNode?` | ITraversalContext 接口属性, FSM 每步更新 |

(Phase 3 计划: D-I — 将 TraversalRuntimeContext 拆分为 5 subsystem contexts → decisions/log.md)

---

## 4. 组件协作关系图

```
┌──────────────────────────────────────────────────────────────────┐
│                    TraversalEngine (顶层入口)                     │
│                                                                  │
│  初始化:                                                          │
│    IntentSlots → PlanCompiler → TraversalPlan                    │
│    EntryPolicyExecutor → 成功进入 → GlobalFSM.Initializing       │
│    → GlobalFSM.Traversing → 开始遍历循环                          │
│                                                                  │
│  遍历循环 (每步):                                                  │
│    StepOrchestrator.ExecuteStep(StepContext)                      │
│      │                                                           │
│      ├── TraversalFSM.Step()                                     │
│      │     └── enum-switch dispatch → HandleXxx()                │
│      │                                                           │
│      ├── DynamicChildManager.GetNextUnvisitedChild()              │
│      │     ├── STATIC: iterate StaticChildren                    │
│      │     └── DYNAMIC_MATCH: DynamicMatcher.MatchAll()          │
│      │           └── MatchableItem ← PageAnalysis                │
│      │           └── TemplateInstantiator → child nodes          │
│      │                                                           │
│      ├── PopupHandler (FSM=PopupHandling) — 6-step pipeline        │
│      │     detect → classify → preserve → handle → restore →       │
│      │     validate (StateRestorer.ValidateRestoredState, H-7)      │
│      │                                                           │
│      ├── Container sub-components (FSM=FrameComplete)              │
│      │     CompletionDetector → FallbackDecider →                  │
│      │     ContainerActionExecutor (3 独立类, 无统一 wrapper)       │
│      │                                                           │
│      ├── Error sub-components (FSM=ErrorHandling)                  │
│      │     ErrorClassifier → ErrorStrategySelector →               │
│      │     RecoveryExecutor (3 独立类, 无统一 wrapper)              │
│      │                                                           │
│      ├── IActionExecutor → 操作执行 → 新截图                     │
│      │                                                           │
│      ├── TraceCoordinator → 可观测性记录                          │
│      │     └── Log-and-Continue (异常不传播)                      │
│      │                                                           │
│      └── Context mutation (engine-only methods)                  │
│            MarkVisited, AppendPath, IncrementStepCount, etc.     │
│                                                                  │
│  终止:                                                            │
│    GlobalFSM → Completed (正常完成)                               │
│    GlobalFSM → Terminated (错误/超时放弃)                         │
└──────────────────────────────────────────────────────────────────┘
```

---

## 5. AI 分析模式 (→ layers/domain.md §数据流)

两种 AI 分析模式决定 PageAnalysis 如何产生：

| 模式 | 链路 | Domain.Vision 参与? | ElementTypeMapper 参与? |
|------|------|---------------------|------------------------|
| A (直接) | 截图 → 多模态AI → PageAnalysis | ❌ 不参与 | ❌ 不参与 |
| B (两步) | 截图 → AI → FlattenedScreen → 规则/文本模型 → PageAnalysis | ✅ 核心链路第一步 | ✅ 规则引擎路径 |

Phase 2 设计决策: 统一使用 Domain 的 PageAnalysis (12 fields), 删除 AI 层的简化版 PageAnalysis (3 fields)。

AI advisor 通过 `TraversalContextSnapshot` (8 immutable fields) 获取只读上下文，不直接引用 `TraversalRuntimeContext` (→ patterns/readonly-isolation.md)。

---

## 6. Relationship to Other Patterns

| Pattern | System Orchestration 中的角色 |
|---------|-----------------------------|
| [fsm-design.md](fsm-design.md) | 两个 FSM 的静态迁移矩阵 — 本文档 §2.1 展示它们的运行时生命周期 |
| [handler-pipeline.md](handler-pipeline.md) | 通用管道模式 — PopupHandler 遵循 6-step pipeline; Container/Error 为 3 独立子组件无统一 wrapper (→ D-16) |
| [dispatch-table.md](dispatch-table.md) | Handler 内部的 dispatch + fallback — 本文档 §2.3 展示 dispatch 触发时机 |
| [readonly-isolation.md](readonly-isolation.md) | Context 的集合安全 — 本文档 §3.3 展示数据对象在各层的流转 |
