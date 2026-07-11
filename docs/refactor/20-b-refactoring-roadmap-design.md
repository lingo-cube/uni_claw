# 20-b — 重构优先级路线图 — 架构健康度驱动

> 状态: Approved
> 日期: 2026-07-11
> 驱动力: 架构健康度 (God Object / 无接口抽象 / StepOrchestrator 单体)
> 决策来源: brainstorming session — 3 个关键决策点

---

## 0. 决策前提

三个 brainstorming 决策锁定此路线图:

| 决策 | 选项 | 理由 |
|------|------|------|
| 核心驱动力 | **架构健康度** | God Object 30 mutable fields 阻碍一切新功能开发 |
| 架构痛点 | **D-I + D-V + D-IV** (不含 Hook B1) | 这三个在实际开发中制造摩擦; Hook 不是当前痛点 |
| 分解策略 | **渐进式** | 每步小变更多, 每步都有测试保护, 前置依赖清晰 |
| Phase 2 收尾顺序 | **先闭环再重构** | FSM 8 handler 全实装 → 稳定测试基线 → 再开始架构重构 |

---

## 1. 优先级排序

| Priority | Change ID | 项目 | 理由 | 前置条件 |
|----------|-----------|------|------|---------|
| **P0** | A1-A5 | 5 Handler 实装 (4 stub + 1 FrameComplete 增强) | FSM 闭环 — 重构前必须有稳定测试基线 | 无 |
| **P1** | D-15 | Subsystem canonical naming | Context 拆分前置 — 无命名边界就不能拆 | A1-A4 完成 |
| **P1** | D-V | Interface extraction | 无前置依赖 + 最简单 + 解除测试覆盖率天花板 | A1-A4 完成, 可与 D-15 并行 |
| **P2** | D-I | Context decomposition | 30 fields → 5 subsystem context — 核心痛点 | D-15 完成 |
| **P2** | D-III | ITraversalContext reform | engine-only ITraversalContext + TraversalContextSnapshot | D-I 完成 |
| **P3** | D-IV | StepOrchestrator decomposition | 14-step → StepScheduler + InterceptionHandler + TraceRecorder | D-I 完成 |
| **P3** | D-28 | Graph 层服务/模型分离 | PlanCompiler/DynamicMatcher/TemplateInstantiator 移入 Services/ + 接口提取 | 无前置 (独立于 SM/Traversal 链路) |
| **P4** | B1 | Engine 扩展点 (Hook/Decorator 待定) | 不是当前痛点 — 形态取决于 D-V/D-IV 完成后的实际需求 | D-V 完成 |
| **P4** | B2 | GlobalFSM 实现 | 依赖 B1 Hook 或类似扩展点 | B1 完成 |
| **P3** | C2 | ExecutionPlanDigest | 数据已够 (TargetType/TargetValue → D-21), 解锁 D-E4 operation_rules + trace_integrity 验证 | 无前置, D-V 完成后可独立先行 |
| **P5** | C1 | FsmAnalysis | 依赖 GlobalFSM trace 数据 | B2 完成 |
| **P5** | C3 | PerformanceProfile | 依赖 DurationMs/Depth 字段 (已有) | 无前置, 优先级低 |
| **P5** | D-II | StateMachine-Graph bidirectional | H-5 已解决核心, 剩余是次要 | P5 |

---

## 2. 执行序列

```
P0 ── A1~A5 (FSM 闭环, 575→575+ tests stable)
       │
P1 ── D-15 + D-V (并行)
       │   subsystem naming   │   interface extraction
       │                      │
P2 ── D-I (Context 拆分)
       │   TraversalRuntimeContext → NavigationContext + ErrorContext + SessionContext + ProgressContext + CacheContext
       │
P2 ── D-III (ITraversalContext reform)
       │   engine-only ITraversalContext + TraversalContextSnapshot for AI advisor
       │
P3 ── D-IV (StepOrchestrator decomposition)
       │   14-step → StepScheduler + InterceptionHandler + TraceCoordinator lifecycle
       │
P3 ── D-28 (Graph 层服务/模型分离 — 独立并行)
       │   PlanCompiler/DynamicMatcher/TemplateInstantiator → Services/ + 接口提取
       │
P3 ── C2 (ExecutionPlanDigest ← 解锁 D-E4 验证维度)
       │
P4 ── B1 (ITraversalHook ← 扩展模型 open question: decorator vs lifecycle hook)
       │   └─→ B2 (GlobalFSM)
       │
P5 ── C1 (FsmAnalysis ← GlobalFSM trace)
       C3 (PerformanceProfile)
```

---

## 3. 渐进式约束

每步重构必须满足:

| 约束 | 说明 | 验证方式 |
|------|------|---------|
| **测试全绿** | 每步完成后 dotnet test 0 错误 | CI + 手动验证 |
| **单子系统边界** | 每步变更 ≤ 1 个 subsystem boundary (例外: interface extraction 跨多 subsystem 但只改类型签名不改行为) | code review |
| **D-15 先行** | 没有 subsystem naming, Context 拆分无方向 | design doc dependency |
| **D-V 可并行** | 提取接口不改 Context 结构 | 独立 change |
| **每步 OpenSpec change** | 每个优先级步骤走 propose → apply → archive 流程 | openspec/changes/ |

---

## 4. P0 详细: Handler 实装

| Handle | FSM transition path | 核心逻辑 | 实装复杂度 |
|---------|--------------------|---------|----------|
| HandleResultVerify | Execute → ResultVerify → Branch/PopupHandling | 3-round retry + vision correction, popup 检测分流 | 中 |
| HandlePreconditionCheck | NodeSelect → PreconditionCheck → Execute/ErrorHandling | precondition 检查 → 路径选择 | 低 |
| HandleErrorHandling | 多源 → ErrorHandling → NodeSelect/Execute/FrameComplete/Branch | 5-strategy recovery: Retry→Execute, Backtrack→NodeSelect, Skip→Branch, Continue→NodeSelect (proceed as if error didn't occur, try next node), Abort→FrameComplete | 高 |
| HandlePopupHandling | 多源 → PopupHandling → ResultVerify/ErrorHandling | popup 检测 + dismiss pipeline (PopupHandler 6-step) | 高 |
| HandleFrameComplete (增强) | 多源 → FrameComplete → NodeSelect/ErrorHandling | 当前只 return NodeSelect; 需加 stack pop + frame teardown | 低-中 |

5 handler 完成后:
- TraversalFSM 8 state 全有实装 handler
- 主循环可跑完整遍历
- 575 测试 + 新 handler tests → 稳定基线

---

## 5. P1 详细: D-15 Subsystem Naming + D-V Interface Extraction

### D-15: 5 Subsystem Canonical Definition ✅ COMPLETED

D-15 已完成并锁定 (openspec:subsystem-canonical-naming)。
Canonical 字段归属表见 `docs/system/layers/state-machine.md §5` 和 `docs/system/decisions/log.md D-15`。
Guard test: `SubsystemBoundaryGuardTests.TraversalRuntimeContext_FieldCountsPerSubsystem` (CI-blocking)。

**Canonical subsystem names and field counts (D-15-1, D-15-2)**:

| Subsystem | Canonical Name | Field Count | Core Fields |
|-----------|---------------|-------------|-------------|
| DFS 遍历 | NavigationContext | 12 | _nodeStack, _currentPath, _currentPageAnalysis, _currentFingerprint, _visitedPages, _visitedLevel1Menus, _visitedLevel2Menus, _visitedNodes, _visitedChildren, _pageTree, CurrentFrame, _visitedChildrenReadOnly |
| 错误追踪 | ErrorContext | 5 | _failedNodes, _consecutiveErrors, _retryCount, _lastError, _exceptionChain |
| 宏观状态 | SessionContext | 4 | _traceId, _globalState, _deviceExperience, _aiProvider |
| 进度控制 | ProgressContext | 5 | _stepCount, _maxDepth, _completionPolicy, _actionHistory, _waitAfterActionMs |
| 缓存与配置 | CacheContext | 2+2 reserved | _pageCache, _cacheValid, + _scrollHandler(Phase3), _currentSnapshot(Phase3) |

**10 ambiguity resolutions (D-15-3)**: 全部已判定, 见 `docs/system/decisions/log.md D-15`。

### D-V: Interface Extraction (10+ components)

| 当前 class | 提取 interface | 用途 |
|------------|--------------|------|
| DynamicChildManager | IDynamicChildManager | 可 mock 子节点生成 |
| TraceCoordinator | ITraceCoordinator | 可 mock trace recording |
| EntryPolicyExecutor | IEntryPolicyExecutor | 可 mock 入口策略 |
| PageCacheManager | IPageCacheManager | 可 mock 缓存 |
| PageSnapshotManager | IPageSnapshotManager | 可 mock fingerprint |
| NodeStackAdapter | INodeStackAdapter | 可 mock stack |
| DictionaryNodeRegistry | INodeRegistry (已有) | 已有接口 |

D-V 产出: 6+ 新 interface, 每个 sealed class 实现对应 interface, TraversalEngine 构造器改用 interface 类型注入。

**⚠️ Ripple effect**: StepContext (sealed record, 12 positional init-only 参数) 当前引用 concrete types
(DynamicChildManager, TraceCoordinator, PageSnapshotManager 等)。
D-V 提取 interface 后, StepContext 的 positional 参数类型需同步改为 interface 类型。
这是 D-V 的必要连带变更, 不是额外 task。

---

## 6. P2 详细: D-I Context Decomposition

**设计原则说明**: Domain 层用 sealed record class + ImmutableArray (不可变 by design)。
TraversalRuntimeContext 是 **engine runtime state** — mutable by design (FSM handler 需要实时修改导航/错误/进度状态)。
拆分后 5 个 sub-context 保持 mutable, 但通过 **engine-only mutation methods** 封装变更入口
(当前 TraversalRuntimeContext 已有此模式: StepCount+1 通过 IncrementStepCount() 而非直接赋值)。

**拆分方案** (依赖 D-15 canonical naming):

**⚠️ Namespace 归属待定**: 当前 TraversalRuntimeContext 在 StateMachine namespace。
拆分后 5 个 sub-context 应留在 StateMachine 还是部分迁移到 Traversal?
(NavigationContext 被 StepOrchestrator 消费 → 可迁 Traversal;
ErrorContext 被 ErrorHandler 消费 → 留 StateMachine;
SessionContext 被 GlobalFSM 消费 → 留 StateMachine)
这是 D-I 实施时的 namespace 分配决策, 不在 D-15 scope。

```
TraversalRuntimeContext (30 fields, God Object)
    ↓ 拆为
NavigationContext     — DFS 遍历: nodeId, pageId, stack, visited
ErrorContext          — 错误追踪: lastError, strategy, retry
SessionContext        — 宏观状态: globalState, traceId, fsm
ProgressContext       — 进度控制: stepCount, maxSteps, policy
CacheContext          — 缓存配置: pageCache, screenHash, config
```

**拆分后 Engine 消费方式**:

```
TraversalEngine
    ├── _navigation (NavigationContext)   ← StepOrchestrator + DynamicChildManager
    ├── _error (ErrorContext)             ← ErrorHandler + RecoveryExecutor
    ├── _session (SessionContext)         ← GlobalFSM + TraceCoordinator
    ├── _progress (ProgressContext)       ← CompletionDetector + StepCounter
    └── _cache (CacheContext)             ← PageCacheManager + PageSnapshotManager(? — fingerprint 是导航检测而非缓存)
```

**D-III 后续**: ITraversalContext 当前已是 read-only interface (只有 property getters, 无 mutation methods)。
D-III 的核心问题是: (1) GlobalState 不应在接口上 (→ D-7/M-14, 移到 engine-only property);
(2) AI advisor 消费 ITraversalContext 时缺少 immutable snapshot 保障 —
TraversalRuntimeContext 内部可变, 外部通过 ITraversalContext 看到的是 "live view" 而非 snapshot。
修复: ITraversalContext 排除 GlobalState; 新增 TraversalContextSnapshot (immutable sealed record)
供 AI advisor 消费; Engine 持有 TraversalRuntimeContext 可直接 mutation。

---

## 7. P3 详细: D-IV StepOrchestrator Decomposition

当前 StepOrchestrator 是 14-step 单方法, 7 责任交织 (代码验证):

| 责任 | 拆分目标 | 说明 | 对应 step |
|------|---------|------|----------|
| Trace/Observability | TraceCoordinator lifecycle | RecordStepStart/End + ActionExecution + StateTransition (steps 2,5,6,7,14) | 5 steps |
| FSM Dispatch | StepScheduler | ctx.StateMachine.Step(ctx) → raw next state (step 3) | 1 step |
| Path Change Detection | PathChangeDetector | 比较当前路径 vs LastKnownPath, 触发 page analysis (step 4) | 1 step |
| Branch Interception | InterceptionHandler | Branch 源状态限制 → push child 或 force FrameComplete (step 8) | 1 step |
| DynamicMatch Child Resolution | InterceptionHandler (同) | NodeSelect + DynamicMatch → push/pop/anti-loop (step 9) | 1 step |
| Frame Completion Override | InterceptionHandler (同) | FrameComplete + DynamicMatch remaining → override NodeSelect (step 10) | 1 step |
| Visited Bookkeeping | StateUpdater | 标记当前 frame node 为 visited (step 12) | 1 step |

D-I Context 拆分后, StepOrchestrator 可按 subsystem 分流:

| 原责任 | 归宿子组件 | 理由 |
|--------|----------|------|
| FSM Dispatch (step 3) | StepScheduler | 核心调度职责 |
| Branch Interception (step 8) | InterceptionHandler | 拦截/override FSM transition |
| DynamicMatch Child Resolution (step 9) | InterceptionHandler | 同类拦截逻辑 |
| Frame Completion Override (step 10) | InterceptionHandler | 同类拦截逻辑 |
| Trace/Observability (5 steps) | TraceCoordinator lifecycle (已有实现, 不需新建) | BuildCorrelation + Record 方法集 |
| Path Change Detection (step 4) | StepScheduler | 步骤调度前的页面变化检测, 影响后续调度决策 |
| Visited Bookkeeping (step 12) | StateUpdater | 状态更新职责的一部分 |

```
StepScheduler.OnStep(navigationCtx)       → dispatch + path change detection
InterceptionHandler.OnIntercept(ctx)       → branch/dynamic/frame override
TraceCoordinator.OnLifecycle(traceCtx)     → BuildCorrelation + Record (已有实现)
StateUpdater.OnResult(progressCtx)         → visited bookkeeping + state update
```

---

## 8. P4 降级理由: Hook 架构 (B1)

ITraversalHook 不是当前痛点, 因为:

1. **D-V 完成后自然有扩展空间** — 提取 interface 后, 装饰器模式可实现"校验菜单名":
   ```csharp
   ValidatingActionExecutor(IActionExecutor inner, ITraversalHook hook)
   ```
2. **不急着在架构不健康时加新抽象** — 当前 StepOrchestrator 是 14-step 单体, 加 Hook lifecycle 调用点会让单体更复杂
3. **D-IV 完成后 Hook 调用点自然清晰** — InterceptionHandler.StepStart/StepEnd 就是 Hook 的理想调用点

**D-V → D-IV 完成后, B1 的形态是 open question, 不需要现在预设计。**

两种可能的扩展模型:
- **Decorator pattern**: ValidatingActionExecutor(IActionExecutor inner) — 精准拦截单个组件, 不加新抽象
- **Lifecycle Hook**: ITraversalHook 5 方法 — 跨切面观测, 与 TraceCoordinator 调用模式一致

哪种更合适取决于 D-V/D-IV 完成后的实际扩展需求。当前不决定。

---

## 9. 与已有 Decisions 的关联

| Decision | 与本路线图的关系 |
|----------|---------------|
| D-7 (M-14 GlobalState) | P2 D-III 包含此修复 — GlobalState 从 ITraversalContext 移到 engine-only property |
| D-15 (subsystem naming) | P1 ✅ COMPLETED — canonical naming + guard tests + docs |
| D-I (God Object) | P2 — 核心痛点, 30 mutable fields → 5 sub-context |
| D-III (ITraversalContext dual safety) | P2 — ITraversalContext 排除 GlobalState + TraversalContextSnapshot |
| D-IV (StepOrchestrator monolith) | P3 — 7 责任 → 4 子组件 (代码验证: 实际 7 责任而非原估 5) |
| D-V (interface extraction) | P1 — 6+ 新 interface, 解除测试覆盖率天花板 |
| D-17 (Observability cross-cutting) | 不受影响 — Observability 定位不变 |
| D-18~D-22 (Trace pipeline) | 不受影响 — Trace 三层架构已完成 |
| D-E4 (5 可验证维度 + 2 TODO) | C2 (ExecutionPlanDigest) 解锁 operation_rules + trace_integrity 验证 |

---

## 10. OpenSpec Change 规划

每个优先级步骤走独立 OpenSpec change:

| Change Name | Priority | 大致 task 数 |
|-------------|----------|------------|
| `handler-implementation` | P0 | 5 (4 stub handler 实装 + 1 FrameComplete 增强) |
| `subsystem-canonical-naming` | P1 | 3-5 (naming + guard + doc) |
| `interface-extraction` | P1 | 6+ (每个 interface 1-2 tasks) |
| `context-decomposition` | P2 | 10+ (拆分 + 重写 consumer + tests) |
| `itraversal-context-reform` | P2 | 5-8 (GlobalState 排除 + snapshot + consumer updates) |
| `execution-plan-digest` | P3 | 3-5 (query implementation + verification integration) |
| `step-orchestrator-decomposition` | P3 | 8-10 (4 子组件 + 重写 orchestrator + tests) |
| `graph-service-model-separation` | P3 | 5-7 (移动文件 + 提取 3 interface + 改 TraversalEngine 引用 + tests) |

每个 change 完成后走 `/opsx:archive` — **必须执行 Step 5 Decisions Extract + Step 6 Four-Layer Documentation Confirmation**。
