# Layers — Traversal

> **Tier 3 · Layers**: Traversal 层规格书。改 StepOrchestrator/DynamicChildManager/TraceCoordinator 时更新。
> 状态: Phase 2 实现中
> 源码: `src/UniClaw.Core/Traversal/`
> 约束: → constitution C-4 (FSM 独立性), patterns/fsm-design.md (双 FSM)

---

## 1. Type Inventory

### Records

| Record | Fields | 用途 |
|--------|--------|------|
| `TraversalResult` | Success, CompletionReason, TotalSteps, ElapsedSeconds, ActionHistory, VisitedPages, Trace, TraceId, FinalState, Error + nested Reasons class (5 const strings) | 统一引擎执行结果 (替换旧版 + SimulationResult) |
| `TraceRecord` | StepNumber, FromState, ToState, CurrentNodeId, CurrentPageId, ActionExecuted, ActionSuccess, ChildPushed, FrameCompleted, SpanTypes, PageFrom?, PageTo?, PageTransitionType?, StepDurationMs? | 每步 trace 记录 (独立于 ITraceRecorder, SpanTypes 替代单 SpanType? → D-18) |
| `TraversalEngineConfig` | MaxSteps=1000, MaxDepth=10, ThrowOnError=false, TraceEnabled=true, DelayPerStepMs=0 | 引擎配置 (合并 SimulationConfig) |
| `ActionRecord` | Action, Timestamp, Parameters, Success | 操作记录 |

### Interfaces

| Interface | 所在文件 | 用途 |
|-----------|---------|------|
| `IGraphTraversalEngine` | Traversal/IGraphTraversalEngine.cs | 遍历引擎 8 成员 async 接口 (Plan, Context, CurrentState, InitializeAsync, RunAsync, PauseAsync, ResumeAsync, StopAsync, GetStateAsync) |
| `INodeRegistry` | TraversalEngine.cs | 2 方法: GetNode, Register |
| `IActionExecutor` | Traversal/IGraphTraversalEngine.cs | 6 方法 + GetHistory |
| `IDynamicChildManager` | TraversalEngine.cs (nested) | 4 方法: GetNextUnvisitedChild, Generate, Invalidate, GetCachedFingerprint — DynamicChildManager 接口镜像 |
| `ITraceCoordinator` | TraversalEngine.cs (nested) | 18 成员: Active + 16 Record 方法 + ShouldRecordEntryAttempt + ShouldRecordVisionCall + GetStepSnapshot — TraceCoordinator 接口镜像 |
| `IEntryPolicyExecutor` | TraversalEngine.cs (nested) | 2 方法: Execute, BuildChain — EntryPolicyExecutor 接口镜像 |
| `IPageCacheManager` | TraversalEngine.cs (nested) | 2 方法 (ITraversalContext 参数): Update, Restore — PageCacheManager 接口镜像 |
| `IPageSnapshotManager` | TraversalEngine.cs (nested) | 2 instance 方法: Fingerprint, HasChanged — PageSnapshotManager 接口镜像 (static→instance 转换) |
| `INodeStackAdapter` | TraversalEngine.cs (nested) | 3 方法: Push, Pop, Peek — NodeStackAdapter 接口镜像 |

### Classes (9)

| Class | 用途 |
|-------|------|
| `TraversalEngine` | 统一遍历引擎入口 — 实现 IGraphTraversalEngine, 构造器 Initialize() + RunAsync()/Run() 核心循环 |
| `StepOrchestrator` | 14-step interception layer — 遍历主循环 |
| `DynamicChildManager` | 9-step generate pipeline + dedup via _generatedPairs |
| `TraceCoordinator` | 16+ span methods, active gate, Log-and-Continue |
| `EntryPolicyExecutor` | 3 strategies + BIND_CURRENT_SCREEN fallback |
| `PageCacheManager` | update/restore (no TTL/size limits yet) |
| `PageSnapshotManager` | deterministic fingerprint (character-based hash, not string.GetHashCode) |
| `NodeStackAdapter` | wraps NodeStack + INodeRegistry for orchestrator |
| `DictionaryNodeRegistry` | Dictionary-backed INodeRegistry (原 SimpleNodeRegistry, 移到 Traversal namespace) |

### Supporting types

| Type | Fields | 用途 |
|------|--------|------|
| `PageCacheInfo` | Items, Timestamp, ScreenHash | cache metadata |
| `EntryResult` | Success, Strategy, Description | entry policy evaluation result |

---

## 2. StepOrchestrator (14-step)

StepOrchestrator 是遍历引擎的主循环，通过 14 个 interception point 协调 FSM transition、handler 调用和状态更新。

**Anti-loop mechanism**: 重复检测防止无限循环遍历同一节点。

**FRAME_COMPLETE override**: 当 TraversalFSM 进入 FrameComplete 状态时，orchestrator 拦截并执行 frame 完成逻辑 (pop stack, update context)。

**BRANCH interception**: 仅允许特定 source state 迁到 Branch (source-state restriction)。

**Scroll Discovery (Step 8/9, 统一 `StepOrchestrator.TryHandleScroll`)**: DYNAMIC_MATCH 子节点耗尽时,
滚动按 **"操作 + 对新截图的判断"** 模型处理 (D-57 supersede — 不再经 ScrollHandler 管线, 不下转 Simulation 具体类型):
1. 不可滚动或已到底 (`!ctx.Vision.HasScroll()` / `ctx.Vision.IsEndOfList()`) → 不 swipe, 由调用方完成帧
2. seed per-frame seen 元素集合 (存 `TraversalRuntimeContext`, 按 NodeId) 为滚动前页面基线
3. **操作**: `ctx.Action.SwipeAsync(...)` (垂直 swipe, mock 与真实服务同路径)
4. **判断**: `ctx.Vision.AnalyzeCurrentPageAsync()` → 新 PageAnalysis; `ctx.ChildMgr.Invalidate(nodeId)` 失效子节点缓存
5. seen-set 差分: 新 PageAnalysis 出现未见元素 → Continue (NodeSelect 重新生成/选择子节点); 全是已见 → 到底 → Stop (根节点 FrameComplete, 非根节点 PressBack + Pop)

循环防护 = seen 集合差分本身 (经验式到底: 滚一下无未见元素 = 到底), 取代旧的 progress-range / 元素计数 / 跳跃恢复管线。
FSM 不再持有滚动职责: `TraversalFSM.HandleBranch` 对耗尽的 DynamicMatch 直接返回 `NodeSelect`, 滚动决策全归 orchestrator。

(→ openspec/specs/scroll-aware-traversal/spec.md — action+judgment 模型 + seen-set 终止)
(→ D-57 supersede: 滚动 = 操作 + 判断, 非 ScrollHandler 管线; D-66 supersede: 删除 9 类冷钝管线 + ScrollAwareNodeSelector)
(→ C-5 strengthened: engine 层 (StateMachine/Traversal/Domain/Graph) 零 `UniClaw.Core.Simulation` 引用 — `EngineLayers_DoNotReferenceSimulation` guard)

**Multi-Branch Navigation (Step 8/9, `TryHandleNavigation`)**: DynamicMatch 父节点有多个导航子节点 (如 hub→listA, hub→listB) 时, 引擎通过行为检测实现全覆盖:

1. **检测**: 非滚动动作 (tap/click) 执行后比较前后页面指纹。指纹变化 → 导航; 指纹未变 → 普通叶子。
2. **子页帧推入**: 指纹变化时推新的 DynamicMatch 子页帧, 归属该导航子节点 NodeId (非 root)。子页帧的子节点从导航目标页生成。
3. **PressBack 还原**: 子页帧耗尽 (depth ≥ 2) → 复用既有 Step 9 PressBack+Pop → 页面还原回父页 → 父帧重新生成 → 剩余兄弟导航子节点出现。
4. **检测优先级**: `TryHandleNavigation` 在 `TryHandleScroll` 之前执行 (导航检测优先于滚动检测)。
5. **指纹自动作废移除**: `GetNextUnvisitedChild` 中不再自动作废指纹缓存 (对滚动冗余, 对导航错误)。指纹变化时返回 null (不返回跨页面 stale 子节点)。

**all_visited 修正**: 仅在所有兄弟导航分支都遍历后才为真; `VisitedNodes` 跨帧去重, 每个导航子节点只算一次。

(→ openspec/specs/scroll-aware-traversal/spec.md §DynamicMatch shall traverse all sibling navigation children)
(→ D-74: DynamicMatch 多分支导航覆盖 —— 行为检测)

---

## 3. DynamicChildManager

**9-step generate pipeline**:
1. Check if generation already cached
2. Determine strategy type (STATIC or DYNAMIC_MATCH)
3. STATIC: iterate static_children list
4. DYNAMIC_MATCH: invoke DynamicMatcher with current page analysis
5. Dedup via `_generatedPairs` (persist across cache invalidation)
6. Register generated nodes in INodeRegistry
7. Cache result for subsequent calls
8. On cache invalidation: preserve dedup pairs (D-3)
9. Return generated children list

**STATIC strategy**: predefined children list from TraversalNode.StaticChildren
**DYNAMIC_MATCH strategy**: DynamicMatcher.MatchAll with current PageAnalysis items

---

## 4. TraceCoordinator

**16+ span methods**: RecordStepStart, RecordStepEnd, RecordPageAnalysis, RecordActionExecution (typed + untyped), RecordSkipSpan, RecordDynamicLifecycle, RecordAICallSpan, RecordErrorSpan, RecordStateTransition, RecordRootNodePushed, RecordPageTransition, RecordDecision, RecordStateDecision, RecordMetricsAsSpans (stub), RecordExecutionSpan (stub).

**Active gate**: `Active` property — all methods no-op when `_recorder` or `_traceId` is null/empty.

**Log-and-Continue pattern** (→ patterns/dispatch-table.md §TraceCoordinator): 所有 Record 方法委托 `LogAndContinue(Action)` — 异静吞, 不传播到 TraversalEngine。

**BuildCorrelation()**: 从 ITraversalContext? ctx 构造 TraceContext (NodeId, StepSpanId, StepNumber, TraceId)。ctx=null → 返回 null。RecordStepStart 用 `with` 表达式覆盖 StepSpanId 为刚生成的 SpanId。

**SpanId 生成**: `_spanCounter` 格式 `"{traceId}-{counter:D6}"`, trace session 内唯一。

**StepSpanId 生命周期**: RecordStepStart → 赋值 _currentStepSpanId=SpanId → BuildCorrelation() 使用 → RecordStepEnd → 释放 _currentStepSpanId=null (→ D-20)。

**GetStepSnapshot()**: 返回累积 SpanTypes 为 ImmutableArray + 清空集合, TraversalEngine 每步结束时调用写入 TraceRecord.SpanTypes。

**Method→Record mapping**: 见 patterns/dispatch-table.md §TraceCoordinator Method → Record Mapping 表。

**Implementation status**:
- H-9 resolved: 13/16 方法已实现 (带 BuildCorrelation + TraceContext + typed signatures)
- M-4 deferred: Log-and-Continue 当前静吞异常, Console.WriteLine 日志待 Phase 3
- 2 stubs: RecordMetricsAsSpans, RecordExecutionSpan (no-op, Phase 3)

(→ layers/observability.md for ITraceRecorder/ITraceStorage/ITraceService architecture)

---

## 5. EntryPolicyExecutor

**3 strategies**:
1. WaitForLoad — wait for page stability before entry
2. BindCurrentScreen — always succeeds (terminal fallback)
3. CheckPrecondition — verify precondition conditions

**Fallback chain**: strategy 1 → strategy 2 → BIND_CURRENT_SCREEN (always succeeds as last resort)

---

## 6. PageSnapshotManager

**Pure functions**:
- `Fingerprint(screenElements)` → deterministic character-by-character hash (H-10: NOT string.GetHashCode())
- `HasChanged(current, previous)` → fingerprint comparison

---

## 7. PageCacheManager

- `UpdateCache(items, timestamp, screenHash)` — store cache
- `RestoreCache()` — retrieve cached items (Phase 2, no TTL/size limits yet)

---

## 8. NodeStackAdapter

Wraps `NodeStack` (from StateMachine layer) + `INodeRegistry` for StepOrchestrator. Provides unified interface for stack operations + node lookup.

---

## 9. Dependency

```
Traversal → StateMachine (TraversalFSM, TraversalRuntimeContext, NodeStack, handlers, StepContext)
Traversal → Graph.Models (TraversalPlan, TraversalNode, NodeType, DynamicMatcher, PlanCompiler)
Traversal → Domain (PageAnalysis, MenuItemType, ExpectedAction)
Traversal → Observability (ITraceRecorder interface reference, SpanType, TraceContext, ExecutionRecord)
TraversalEngine implements IGraphTraversalEngine (8-member async interface)
StateMachine → Traversal (IGraphTraversalEngine — acknowledged upward reference, D-14/D-17)
StateMachine → Observability (ITraceRecorder interface reference — acknowledged upward, D-17)
```

---

## 10. Design Issues (Phase 3)

| Issue | Description | Status |
|-------|-----------|--------|
| D-IV | StepOrchestrator 5 responsibility categories in a single 14-step method | Phase 3 evaluation |
| D-V | 10+ critical components have no interface abstraction (cannot mock test) | **Resolved** — 6 新 interface 定义: IDynamicChildManager(3), ITraceCoordinator(18), IEntryPolicyExecutor(2), IPageCacheManager(2), IPageSnapshotManager(2), INodeStackAdapter(3)。D-V-1~D-V-7 见 decisions/log.md |
| H-9 | TraceCoordinator 15/16 empty lambdas | **Resolved** — Phase 2.2 实现 BuildCorrelation + TraceContext + typed signatures, 13/16 方法已实现 |
| H-11 | EntryPolicyExecutor has no fast/polling wait modes | Deferred → Phase 2.2 |
