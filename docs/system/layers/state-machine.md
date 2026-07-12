# Layers — State Machine

> **Tier 3 · Layers**: StateMachine 层规格书。改 FSM/Handler/Context 时更新。
> 状态: Phase 2.3 完成 (全部 8 handler 实装 + 603 tests pass)
> 源码: `src/UniClaw.Core/StateMachine/`
> 约束: → constitution C-1, C-4, C-5, C-7

---

## 1. Type Inventory

### Enums (10)

| Enum | 值数 | 级别 | 用途 | Guard test |
|------|------|------|------|-----------|
| `TraversalState` | 8 | 火山 | 微观 FSM 状态 | `TraversalState_Has8Values` |
| `GlobalState` | 8 | 火山 | 宏观 FSM 状态 (含 terminal) | `GlobalState_Has8Values` |
| `PopupType` | 5 | 丘陵 | popup 分类 (Permission/Error/Ad/Dialog/Unknown) | `PopupType_Has5Values` |
| `UrgencyLevel` | 3 | 平原 | popup 紧急度 (→ D-11: 移除不可达 Critical) | `UrgencyLevel_Has3Values` |
| `BlockingType` | 3 | 平原 | popup 阻塞类型 | `BlockingType_Has3Values` |
| `DismissStrategy` | 4 | 丘陵 | popup 关闭策略 | `DismissStrategy_Has4Values` |
| `ErrorType` | 6 | 丘陵 | 错误分类 | `ErrorType_Has6Values` |
| `ErrorStrategy` | 5 | 丘陵 | 错误恢复策略 | `ErrorStrategy_Has5Values` |
| `CompletionReason` | 4 | — | 完成原因 | — |
| `RecoveryOutcome` | 3 | — | 恢复结果 | — |

### Interfaces (6)

| Interface | 所在文件 | 用途 |
|-----------|---------|------|
| `ITraversalStateMachine` | TraversalState.cs | FSM 接口 (CurrentState, Context, TransitionTo, HasUnvisitedChildren, GetNextState) |
| `IGlobalStateMachine` | GlobalState.cs | GlobalFSM 接口 (CurrentState, IsComplete, CanTransitionTo, GetValidTransitions) |
| `ITraversalContext` | TraversalState.cs | 只读上下文接口 (移除 CurrentFrame/GlobalState/LastError setters，mutation 通过 TraversalRuntimeContext.SetXxx() 方法，→ D-29/D-30/D-31) |
| `INodeStack` | TraversalState.cs | DFS stack 接口 (Depth, MaxDepth, Push, Pop, Peek, IsEmpty, Clear) |
| `IGraphTraversalEngine` | TraversalState.cs (空 stub, → D-14) | 最小接口定义，避免循环依赖。完整定义在 Traversal namespace |
| `IVisionProvider` | StepContext.cs | 视觉分析接口 (2 方法: AnalyzeCurrentPageAsync + FindAppEntryAsync) |

### Key Classes

| Class | 用途 | Pattern |
|-------|------|---------|
| `TraversalFSM` | 微观 FSM (8 状态, enum-based switch dispatch) + RuntimeContext 属性 (concrete 类型用于内部 mutation) | → patterns/fsm-design.md, → D-31 |

#### TraversalFSM Handler 实现状态

| Handler | 状态 | 实现度 | 说明 |
|---------|------|--------|------|
| HandleNodeSelect | ✅ 完成 | 100% | 真实逻辑: stack empty→Branch, 有node→PreconditionCheck |
| HandlePreconditionCheck | ✅ Phase 2.3b | 100% | assume pass → Execute + TraceCoordinator.RecordDecision("precondition_assume_pass") (D-23) |
| HandleExecute | ✅ Phase 2.3a | 100% | Operation dispatch (Click/Swipe/Back/InputText/NoAction) via OperationDispatcher → optional RestoreAction → ResultVerify / ErrorHandling; StepContext null → stub fallback |
| HandleResultVerify | ✅ Phase 2.3b | 100% | 3-round retry + PageSnapshotManager.HasChanged + PageAnalysis.IsPopup 检测 → Branch / PopupHandling (D-24) |
| HandleBranch | ✅ Phase 2.3a | 100% | ChildrenStrategy-based decision: STATIC → unvisited check (VisitedChildren); DYNAMIC_MATCH → optimistic NodeSelect; NONE → leaf/container depth logic → NodeSelect / FrameComplete |
| HandleFrameComplete | ✅ 完成 | 100% | 真实逻辑: return NodeSelect |
| HandleErrorHandling | ✅ Phase 2.3c | 100% | 5-strategy RecoveryExecutor delegation: Retry→Execute, Backtrack→NodeSelect, Skip→Branch, Continue→NodeSelect, Abort→FrameComplete + consecutive error tracking (D-25) |
| HandlePopupHandling | ✅ Phase 2.3c | 100% | PopupHandler.HandlePopup() 6-step pipeline delegation: Success→ResultVerify, Failure→ErrorHandling (D-26) |

**P1 (Phase 2.3a)**: HandleExecute + HandleBranch → 最小可运行遍历循环 ✅
**P2 (Phase 2.3b)**: HandleResultVerify + HandlePreconditionCheck → 验证+纠正 ✅
**P3 (Phase 2.3c)**: HandleErrorHandling + HandlePopupHandling → 容错+弹窗 ✅
| `GlobalFSM` | 宏观 FSM (8 状态, callback + history) | → patterns/fsm-design.md |
| `PopupHandler` | popup 6-step pipeline | → patterns/handler-pipeline.md |
| `PopupDetector` | regex pattern matching (4 popup type) | dispatch sub-component |
| `PopupClassifier` | 5 sub-methods (type→dismiss→strategy→urgency→blocking) | pipeline classifier |
| `PopupActionExecutor` | 5 PopupType hooks + exception fallback to back | → patterns/dispatch-table.md |
| `StateRestorer` | preserve/restore/validate lifecycle (6 fields) | pipeline lifecycle |
| `Container` (ContainerHandler.cs 文件) | completion detection + fallback + action (3 独立类, 无统一 wrapper → D-16) | → patterns/dispatch-table.md |
| `CompletionDetector` | 5-priority chain (timeout→maxDepth→noChildren→allVisited→incomplete) | pure computation |
| `FallbackDecider` | priority chain fallback decision | pure computation |
| `ContainerActionExecutor` | 4 FallbackAction hooks + exception fallback to BACK | → patterns/dispatch-table.md |
| `Error` (ErrorHandler.cs 文件) | classification → strategy → recovery (3 独立类, 无统一 wrapper → D-16) | → patterns/dispatch-table.md |
| `ErrorClassifier` | 7-priority chain (substring match, not regex, case-insensitive) | dispatch classifier |
| `ErrorStrategySelector` | applicability-based per-type chains | strategy selector |
| `RecoveryExecutor` | 5 ErrorStrategy hooks + exponential backoff (cap 10s) + exception fallback to abort | → patterns/dispatch-table.md |
| `NodeStack` | DFS stack (DefaultMaxDepth=10, Push returns false at limit) | infrastructure |
| `TraversalRuntimeContext` | 30 可变状态 (26 core + 2 Phase-3 reserved + CurrentFrame + 1 lazy cache), ITraversalContext impl, ReadOnlySetWrapper, D-15 canonical subsystem attribution, SetXxx() 方法 (SetCurrentFrame/SetGlobalState/SetLastError for FSM internal mutation) | → patterns/readonly-isolation.md, → §5 below, → D-30 |

---

## 2. Dual FSM — 概要

(→ patterns/fsm-design.md for full transition matrices)

| FSM | States | Terminal | Exception behavior |
|-----|--------|----------|-------------------|
| TraversalFSM | 8 (NodeSelect→PopupHandling) | None | Invalid transition → DomainValidationException; handler exception → route to ErrorHandling |
| GlobalFSM | 8 (Idle→Terminated) | Completed, Terminated | Invalid/terminal transition → DomainValidationException; callback exception → not propagated |

**Coordination**: 仅通过 `ITraversalContext.GlobalState` (→ decisions/log D-7, M-14)

---

## 3. Handlers — 概要

(→ patterns/handler-pipeline.md for pattern details)

| Handler | 结构 | Pipeline | Dispatch | Fallback | Stats |
|---------|------|----------|----------|----------|-------|
| PopupHandler | 统一编排类 | detect→classify→preserve→handle→restore→validate (6-step) | 5 PopupType hooks | exception → back_fallback | detected/handled + HandlingRate |
| Container 3 子组件 | 3 独立类, 无 wrapper (→ D-16) | CompletionDetector→FallbackDecider→ContainerActionExecutor | 4 FallbackAction hooks | exception → BACK | none |
| Error 3 子组件 | 3 独立类, 无 wrapper (→ D-16) | ErrorClassifier→ErrorStrategySelector→RecoveryExecutor | 5 ErrorStrategy hooks | exception → abort | none |
| ScrollHandler | 统一编排类 | detect→classify→decide→execute→verify→recover→statistics (7-step) | 3 ScrollActionType hooks (ScrollDown/ScrollUp/None) | exception → DefaultNone | scrolled/skipped + jump stats |

#### ScrollHandler 7 步流程详解

| 步骤 | 组件 | 职责 |
|------|------|------|
| 1. Detect | ScrollabilityDetector | 检测滚动能力 (NotScrollable/CanScrollDown/AtBottom/CanScrollUp) |
| 2. Classify | ScrollClassifier | 计算进度、最大阈值、推荐步长 |
| 3. Decide | ScrollDecider | 映射到动作类型 (None/ScrollDown/ScrollUp) |
| 4. Execute | ScrollActionExecutor | 通过 Hook Dispatch Table 执行滚动 |
| 5. Verify | JumpDetector | 检测跳跃 (元素集合比较) |
| 6. Recover | JumpRecoveryHandler | 回滚并重试 (跳跃恢复) |
| 7. Statistics | ScrollStatisticsCollector | 收集统计指标 |

#### ScrollHandler 子组件

| 子组件 | 职责 |
|--------|------|
| ScrollabilityDetector | 滚动能力检测 (基于 HasScrollData/IsEndOfList/CurrentProgress) |
| ScrollClassifier | 滚动分类 (计算 CurrentProgress/MaxProgress/RecommendedStep) |
| ScrollDecider | 滚动决策 (Scrollability → ScrollActionType 映射) |
| ScrollActionExecutor | 滚动执行 (Hook Dispatch Table + 异常处理) |
| JumpDetector | 跳跃检测 (BeforeElementIds ∩ AfterElementIds 比较) |
| JumpRecoveryHandler | 跳跃恢复 (回滚 + 减小步长重试) |
| AdaptiveStepCalculator | 自适应步长 (纯函数，基于重复比例) |
| ScrollStatisticsCollector | 统计收集 (ScrolledCount/SkippedCount/JumpDetectedCount 等) |

#### ScrollHandler 集成点 (✅ 已完成 FSM Integration)

**集成位置**: `TraversalFSM.HandleBranch()` 和 `TraversalFSM.TryHandleScroll()`

**触发条件**:
- `ChildrenStrategyType.Static`: 所有静态子节点已访问 (`HasUnvisitedStaticChildren == false`)
- `ChildrenStrategyType.DynamicMatch`: 无未访问子节点时检查滚动 (→ D-40)

**滚动循环防护** (→ D-38, D-39, D-42):
1. **D5 - IsEndOfList 早期退出**: 在创建 ScrollHandler 前检查 `RuntimeContext.IsEndOfList`，已到底则返回 `FrameComplete`
2. **D1 - 进度检查**: 滚动后检查 `progressDelta = newProgress - currentProgress`，若 `<= Config.ProgressEpsilon` 则返回 `FrameComplete`
3. **D2 - 元素计数检查**: 比较滚动前后去重元素数 `uniqueBefore` vs `uniqueAfter`，若 `uniqueAfter <= uniqueBefore` 则返回 `FrameComplete`
4. **根节点耗尽处理**: depth=1 且滚动耗尽时返回 `FrameComplete`（而非 `NodeSelect`，避免无限循环）

**滚动成功** (进度前进 + 元素增加): 重置 VisitedChildren → NodeSelect 流程
**滚动失败** (进度未前进 / 元素未增加 / 已到底): FrameComplete 流程

**选择性重置** (→ D-41): 
- 因元素名称 (PageAnalysis) 与节点 ID (VisitedChildren) 不匹配，当前使用完全重置
- 未来可通过 TraversalEngine.StaticNodes 建立精确名称到 ID 映射实现选择性重置

**Phase 状态**: ✅ Phase 2.4 完成 (D1/D2/D3/D5 实装，D4 延期)

---

---

## 4. NodeStack

- `DefaultMaxDepth = 10`
- `Push(node, children)` → returns false when depth >= MaxDepth (深度限制)
- `Peek(offset)` → 0=top (current), 1=parent, etc.
- Internal `StackFrame` (sealed record, implements IStackFrame)
- `Pop()` returns IStackFrame with NodeId + Node + Children

---

## 5. TraversalRuntimeContext — Canonical Subsystem Attribution (D-15)

(→ patterns/readonly-isolation.md for collection safety details)

### 5-Subsystem Canonical Definition

| # | Canonical Name | Responsibility |
|---|----------------|----------------|
| 1 | NavigationContext | DFS traversal — node selection, visited tracking, page identity, stack management |
| 2 | ErrorContext | Error tracking — error recording, retry counting, failure tracking, recovery state |
| 3 | SessionContext | Macro state — global FSM state, trace identity, device/AI configuration |
| 4 | ProgressContext | Progress control — step counting, completion policy, action audit, timing config |
| 5 | CacheContext | Cache & config — page cache, cache validity, screen snapshots (Phase 3 reserved) |

### Canonical Field Ownership Table

| Field | Type | Subsystem | Rationale |
|-------|------|-----------|-----------|
| `_traceId` | string | SessionContext | Identifies traversal session; set once at start |
| `_nodeStack` | NodeStack | NavigationContext | DFS stack for frame push/pop |
| `_currentPath` | List<string> | NavigationContext | DFS path tracking |
| `_currentPageAnalysis` | PageAnalysis? | NavigationContext | Current page interpretation for DFS child selection |
| `_currentFingerprint` | VisitFingerprint? | NavigationContext | Page identity for DFS revisit detection (cache invalidation is downstream side-effect) |
| `_cacheValid` | bool | CacheContext | Cache validity flag controlling _pageCache reuse lifecycle |
| `_visitedPages` | HashSet<string> | NavigationContext | DFS visited page set |
| `_visitedLevel1Menus` | HashSet<string> | NavigationContext | DFS traversal decision — DynamicChildManager checks to skip revisited menus |
| `_visitedLevel2Menus` | HashSet<string> | NavigationContext | Same pattern as L1 — DFS traversal decision, not cache dedup |
| `_visitedNodes` | HashSet<string> | NavigationContext | DFS visited node set |
| `_visitedChildren` | Dictionary<string, HashSet<string>> | NavigationContext | Per-node child visited map — DFS anti-loop mechanism |
| `_pageTree` | ContentNode? | NavigationContext | DynamicChildManager uses for child enumeration — DFS navigation data structure |
| `_actionHistory` | List<ActionRecord> | ProgressContext | Audit trail of recent actions — navigation decisions don't query it |
| `_failedNodes` | Dictionary<string, ErrorRecord> | ErrorContext | Failed node registry with failure reasons |
| `_consecutiveErrors` | int | ErrorContext | Error streak counter for recovery decisions |
| `_maxDepth` | int | ProgressContext | Maximum traversal depth constraint |
| `_stepCount` | int | ProgressContext | Step counter for progress tracking |
| `_retryCount` | int | ErrorContext | Retry counter for current node error recovery |
| `_completionPolicy` | CompletionPolicy? | ProgressContext | Answers "when should traversal end?" — termination question |
| `_deviceExperience` | string? | SessionContext | Set once per session, never changes — session-level metadata |
| `_globalState` | GlobalState | SessionContext | Macro session lifecycle managed by GlobalFSM (D-7: ✅ Fixed — ITraversalContext now read-only via setter removal) |
| `_lastError` | Exception? | ErrorContext | Most recent exception |
| `_exceptionChain` | List<Exception>? | ErrorContext | Error accumulation chain |
| `_aiProvider` | string? | SessionContext | Set once — session-level configuration |
| `_pageCache` | Dictionary<string, object> | CacheContext | Cached page data managed by PageCacheManager |
| `_waitAfterActionMs` | int | ProgressContext | Post-action delay — timing configuration for progress pacing |
| `_scrollHandler` (Phase 3) | object? | CacheContext | Scroll state manager — reserved for Phase 3 |
| `_currentSnapshot` (Phase 3) | object? | CacheContext | Page snapshot — reserved for Phase 3 |
| `CurrentFrame` | ITraversalNode? | NavigationContext | Current navigation position (alias for stack top) |
| `_visitedChildrenReadOnly` | ReadOnlyDictionary? | NavigationContext | Read-only projection of _visitedChildren — same subsystem as source |

**Subsystem field counts**: NavigationContext=12, ErrorContext=5, SessionContext=4, ProgressContext=5, CacheContext=2 (core) + 2 (Phase 3 reserved)

**Guard test**: `SubsystemBoundaryGuardTests.TraversalRuntimeContext_FieldCountsPerSubsystem` — CI-blocking, verifies canonical counts via source annotation parsing

**Phase 5 Status (2026-07-12)**: ✅ **Context Decomposition Complete**

All 5 sub-contexts have been extracted from `TraversalRuntimeContext` per the Container pattern (D-I):
- `NavigationContext` (12 fields) — DFS traversal state
- `ErrorContext` (5 fields) — Error tracking and recovery
- `SessionContext` (4 fields) — Macro session state
- `ProgressContext` (5 fields) — Progress control and pacing
- `CacheContext` (2 core + 2 Phase 3 reserved fields) — Cache and configuration

`TraversalRuntimeContext` now serves as a pure Container holding 5 sub-contexts with immutable references. All ITraversalContext properties delegate to appropriate sub-contexts. 617 CI tests passing.

### Engine-only mutation methods

AppendPath, PopPath, MarkVisited, MarkNodeVisited, AddVisitedChild, IncrementStepCount, etc.

### CreateReadOnlySnapshot()

→ TraversalContextSnapshot (8 immutable fields, fully isolated from source)

### Design Issues (Phase 3)

- ~~D-I: God Object (30 mutable states, 5 subsystems) — needs decomposition per canonical table above~~ ✅ **RESOLVED (Phase 5)**
- ~~D-7: GlobalState 暂留 ITraversalContext — Phase 3 待修~~ ✅ **RESOLVED (Phase 2.3)** — ITraversalContext 现在是纯只读接口（setter 已移除），mutation 通过 TraversalRuntimeContext.SetXxx() 方法
- D-III: ITraversalContext serves both engine and AI advisor with different safety needs

---

## 6. Dependency

```
StateMachine → Domain (DomainValidationException, Domain.Models.Content for MenuItemType/ExpectedAction)
StateMachine → Graph.Models (NodeType, ITraversalNode, IStackFrame for TraversalState/NodeStack)
StateMachine → Observability (TraceCoordinator, ITraceRecorder — via TraversalRuntimeContext/StepContext)  ← 向上引用 (D-17)
StateMachine → Traversal (DynamicChildManager, NodeStackAdapter, PageSnapshotManager — via StepContext) ← 向上引用 (D-17)

Simulation → StateMachine (IVisionProvider, AppEntryPoint)
Simulation → Domain.Models.Content (PageAnalysis, MenuItem, Coordinate — via BuildPageAnalysis)
Simulation → Domain.Models.Common (Operation, Target — via StatefulMockActionExecutor)
Simulation → Graph.Models (TraversalNode, ChildrenStrategy — via SimpleNodeRegistry)
Simulation → Traversal (IActionExecutor, ActionRecord)

NOTE: ITraversalNode is in Graph.Models namespace (→ constitution C-5)
      TraversalNode.cs does NOT using StateMachine (Guard verified ✅)
      Domain does NOT reference upper layers (Guard verified ✅)
      Observability is cross-cutting utility, not traditional top layer (→ D-17)
      Simulation is a new namespace (Phase 2.3-sim), zero new NuGet dependencies
```

---

## 7. Locked Enums (→ constitution locked-enums.md)

All 10 Phase 2.1 enums have Guard tests in `ArchitectureGuardTests.cs`.
