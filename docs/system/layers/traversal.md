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
| `TraversalEngineConfig` | MaxSteps=1000, MaxDepth=10, ThrowOnError=false, TraceEnabled=true, DelayPerStepMs=0, ScrollSwipe=default, **Hooks=ImmutableArray<ITraversalHook>.Empty** (D-100) | 引擎配置 (合并 SimulationConfig) |
| `ActionRecord` | Action, Timestamp, Parameters, Success | 操作记录 |

### Interfaces

| Interface | 所在文件 | 用途 |
|-----------|---------|------|
| `IGraphTraversalEngine` | Traversal/IGraphTraversalEngine.cs | 遍历引擎 8 成员 async 接口 (Plan, Context, CurrentState, InitializeAsync, RunAsync, PauseAsync, ResumeAsync, StopAsync, GetStateAsync) |
| `IInterceptionHandler` | Traversal/IInterceptionHandler.cs | 3 方法: OnBranch (async), OnDynamicMatchNodeSelect (async), OnFrameComplete (sync) — FSM 拦截/覆盖逻辑 (StepOrchestrator 步骤 8-10), 可 mock (→ D-80) |
| `INodeRegistry` | TraversalEngine.cs | 2 方法: GetNode, Register |
| `IActionExecutor` | Traversal/IGraphTraversalEngine.cs | 6 方法 + GetHistory |
| `IDynamicChildManager` | TraversalEngine.cs (nested) | 4 方法: GetNextUnvisitedChild, Generate, Invalidate, GetCachedFingerprint — DynamicChildManager 接口镜像 |
| `ITraceCoordinator` | TraversalEngine.cs (nested) | 18 成员: Active + 16 RecordAsync 方法 + ShouldRecordEntryAttempt + ShouldRecordVisionCall + GetStepSnapshot — TraceCoordinator 接口镜像 |
| `IEntryPolicyExecutor` | TraversalEngine.cs (nested) | 2 方法: Execute, BuildChain — EntryPolicyExecutor 接口镜像 |
| `IPageCacheManager` | TraversalEngine.cs (nested) | 2 方法 (ITraversalContext 参数): Update, Restore — PageCacheManager 接口镜像 |
| `IPageSnapshotManager` | TraversalEngine.cs (nested) | 2 instance 方法: Fingerprint, HasChanged — PageSnapshotManager 接口镜像 (static→instance 转换) |
| `INodeStackAdapter` | TraversalEngine.cs (nested) | 3 方法: Push, Pop, Peek — NodeStackAdapter 接口镜像 |
| `ITraversalHook` | Traversal/ITraversalHook.cs | 7 方法: OnBeforeRunAsync, OnAfterRunAsync, OnBeforeStepAsync, OnAfterStepAsync, OnErrorAsync, OnPauseAsync, OnResumeAsync — lifecycle hook interface |

### Classes (10)

| Class | 用途 |
|-------|------|
| `TraversalEngine` | 统一遍历引擎入口 — 实现 IGraphTraversalEngine, 构造器 Initialize() + RunAsync() 核心循环 + 7 lifecycle hook call points via FireAsync dispatch |
| `StepOrchestrator` | 14-step 生命周期编排 (~127 行) — trace + FSM dispatch + visited 记账; 步骤 8-10 委托 IInterceptionHandler (→ D-80) |
| `InterceptionHandler` | FSM 拦截/覆盖逻辑 — OnBranch/OnDynamicMatchNodeSelect/OnFrameComplete + TryHandleNavigation/TryHandleScrollAsync/FromFrame/GetElementIds + _lastPushedChildNodeId (→ D-80) |
| `DynamicChildManager` | 9-step generate pipeline + dedup via _generatedPairs |
| `TraceCoordinator` | 16+ span methods, active gate, Log-and-Continue |
| `EntryPolicyExecutor` | 3 strategies + BIND_CURRENT_SCREEN fallback |
| `PageCacheManager` | update/restore (no TTL/size limits yet) |
| `PageSnapshotManager` | deterministic fingerprint (character-based hash, not string.GetHashCode) |
| `NodeStackAdapter` | wraps NodeStack + INodeRegistry for orchestrator |
| `DictionaryNodeRegistry` | Dictionary-backed INodeRegistry (原 SimpleNodeRegistry, 移到 Traversal namespace) |
| **`TraversalHookBase`** | abstract no-op base class for ITraversalHook — inherit and override selectively |
| **`TraversalErrorContext`** | sealed record: ErrorType, Message, NodeId?, IsRecoverable — lightweight error summary for OnErrorAsync |

### Supporting types

| Type | Fields | 用途 |
|------|--------|------|
| `PageCacheInfo` | Items, Timestamp, ScreenHash | cache metadata |
| `EntryResult` | Success, Strategy, Description | entry policy evaluation result |
| `InterceptionResult` | NextState, ChildPushed, FrameCompleted, FrameOverrideTriggered | 可变 record struct — FSM override 结果, 替代 3 ref bool + 1 ref TraversalState (→ D-80) |

---

## 2. StepOrchestrator (14-step) + InterceptionHandler

**2 组件架构 (→ D-80)**: StepOrchestrator 保留 14-step 生命周期编排 (trace 生命周期、FSM dispatch、path 变化检测、visited 记账); 步骤 8-10 的 FSM 拦截/覆盖逻辑委托 `IInterceptionHandler` (默认实现 `InterceptionHandler`, 构造器可注入 mock)。handler 返回 `InterceptionResult` (可变 record struct), orchestrator 以 `intercepted` flag 守卫 — 仅当 handler 实际被调用时应用 override, 防止 `default(InterceptionResult)` 污染 FSM 有效 nextState。`nextState` 逐步立即应用 (步骤 8 滚动 → NodeSelect 可级联触发步骤 9, D-74); bool 结果 (childPushed/frameCompleted/frameOverrideTriggered) 在步骤 11 统一从最后一次 interception 结果应用。

**Anti-loop mechanism**: 重复检测防止无限循环遍历同一节点。

**FRAME_COMPLETE override (Step 10, `InterceptionHandler.OnFrameComplete`)**: 当 TraversalFSM 进入 FrameComplete 状态时，若 DynamicMatch 仍有未访问子节点则覆盖为 NodeSelect 并推子节点; 否则放行 FrameComplete。

**BRANCH interception (Step 8, `InterceptionHandler.OnBranch`)**: 仅允许特定 source state 迁到 Branch (source-state restriction)。`BranchAllowedSources` guard 留在 StepOrchestrator (编排条件, 非拦截逻辑)。

**Scroll Discovery (Step 8/9, 统一 `InterceptionHandler.TryHandleScrollAsync`)**: DYNAMIC_MATCH 子节点耗尽时,
滚动按 **"操作 + 对新截图的判断"** 模型处理 (D-57 supersede — 不再经 ScrollHandler 管线, 不下转 Simulation 具体类型):
1. 不可滚动或已到底 (`!ctx.Vision.HasScroll()` / `ctx.Vision.IsEndOfList()`) → 不 swipe, 由调用方完成帧
2. seed per-frame seen 元素集合 (存 `TraversalRuntimeContext`, 按 NodeId) 为滚动前页面基线
3. **操作**: `ctx.Action.SwipeAsync(...)` (垂直 swipe, mock 与真实服务同路径)
4. **判断**: `ctx.Vision.AnalyzeCurrentPageAsync()` → 新 PageAnalysis; `ctx.ChildMgr.Invalidate(nodeId)` 失效子节点缓存
5. seen-set 差分: 新 PageAnalysis 出现未见元素 → Continue (NodeSelect 重新生成/选择子节点); 全是已见 → 到底 → Stop (根节点 FrameComplete, 非根节点 PressBack + Pop)

循环防护 = seen 集合差分本身 (经验式到底: 滚一下无未见元素 = 到底), 取代旧的 progress-range / 元素计数 / 跳跃恢复管线。
FSM 不再持有滚动职责: `TraversalFSM.HandleBranchAsync` 对耗尽的 DynamicMatch 直接返回 `NodeSelect`, 滚动决策全归 orchestrator。

(→ openspec/specs/scroll-aware-traversal/spec.md — action+judgment 模型 + seen-set 终止)
(→ D-57 supersede: 滚动 = 操作 + 判断, 非 ScrollHandler 管线; D-66 supersede: 删除 9 类冷钝管线 + ScrollAwareNodeSelector)
(→ C-5 strengthened: engine 层 (StateMachine/Traversal/Domain/Graph) 零 `UniClaw.Core.Simulation` 引用 — `EngineLayers_DoNotReferenceSimulation` guard)

**Multi-Branch Navigation (Step 8/9, `InterceptionHandler.TryHandleNavigation`)**: DynamicMatch 父节点有多个导航子节点 (如 hub→listA, hub→listB) 时, 引擎通过行为检测实现全覆盖:

1. **检测**: 非滚动动作 (tap/click) 执行后比较前后页面指纹。指纹变化 → 导航; 指纹未变 → 普通叶子。
2. **子页帧推入**: 指纹变化时推新的 DynamicMatch 子页帧, 归属该导航子节点 NodeId (非 root)。子页帧的子节点从导航目标页生成。
3. **PressBack 还原**: 子页帧耗尽 (depth ≥ 2) → 复用既有 Step 9 PressBack+Pop → 页面还原回父页 → 父帧重新生成 → 剩余兄弟导航子节点出现。
4. **检测优先级**: `TryHandleNavigation` 在 `TryHandleScrollAsync` 之前执行 (导航检测优先于滚动检测)。
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

**16+ span methods**: RecordStepStartAsync, RecordStepEndAsync, RecordPageAnalysisAsync, RecordActionExecutionAsync (typed + untyped), RecordSkipSpanAsync, RecordDynamicLifecycleAsync, RecordAICallSpanAsync, RecordErrorSpanAsync, RecordStateTransitionAsync, RecordRootNodePushedAsync, RecordPageTransitionAsync, RecordDecisionAsync, RecordStateDecisionAsync, RecordMetricsAsSpansAsync (stub), RecordExecutionSpanAsync (stub).

**Active gate**: `Active` property — all methods no-op when `_recorder` or `_traceId` is null/empty.

**Log-and-Continue pattern** (→ patterns/dispatch-table.md §TraceCoordinator): 所有 RecordAsync 方法委托 `LogAndContinueAsync(Func<Task>)` — 异静吞, 不传播到 TraversalEngine。

**BuildCorrelation()**: 从 ITraversalContext? ctx 构造 TraceContext (NodeId, StepSpanId, StepNumber, TraceId)。ctx=null → 返回 null。RecordStepStartAsync 用 `with` 表达式覆盖 StepSpanId 为刚生成的 SpanId。

**SpanId 生成**: `_spanCounter` 格式 `"{traceId}-{counter:D6}"`, trace session 内唯一。

**StepSpanId 生命周期**: RecordStepStartAsync → 赋值 _currentStepSpanId=SpanId → BuildCorrelation() 使用 → RecordStepEndAsync → 释放 _currentStepSpanId=null (→ D-20)。

**GetStepSnapshot()**: 返回累积 SpanTypes 为 ImmutableArray + 清空集合, TraversalEngine 每步结束时调用写入 TraceRecord.SpanTypes。

**Method→Record mapping**: 见 patterns/dispatch-table.md §TraceCoordinator Method → Record Mapping 表。

**Implementation status**:
- H-9 resolved: 13/16 方法已实现 (带 BuildCorrelation + TraceContext + typed signatures)
- M-4 deferred: Log-and-Continue 当前静吞异常, Console.WriteLine 日志待 Phase 3
- 2 stubs: RecordMetricsAsSpansAsync, RecordExecutionSpanAsync (no-op, Phase 3)

(→ layers/observability.md for ITraceRecorder/ITraceStorage/ITraceService architecture)

---

## 5. Lifecycle Hook Infrastructure (ITraversalHook)

**Hook registration** (D-100): `TraversalEngineConfig.Hooks: ImmutableArray<ITraversalHook> { get; init; } = Empty`. Hooks set at engine construction (init-only), not modified during run. `RegisterHook()` method removed — replaced by config field. Empty Hooks (`_hooks.Length == 0`) enables zero-overhead skip in FireAsync.

**FireAsync dispatch** (D-104): Selector-based sequential iteration over `ImmutableArray<ITraversalHook>`. Each call site passes a different `Func<ITraversalHook, Task>` selector. Exception handling: `Console.WriteLine("[Hook Warning] ...")` + continue (consistent with TraceCoordinator dispatch-table pattern, → patterns/dispatch-table.md §TraversalEngine.FireAsync).

**Call point mapping** — 7 lifecycle hooks wired in TraversalEngine.RunAsync:

| Hook method | Call point in RunAsync | Position | Design decision |
|-------------|----------------------|----------|-----------------|
| OnBeforeRunAsync | Before `try { for (...) }` | Outside try block | D-103: hook exception caught by FireAsync, not converted to Done(Error) |
| OnBeforeStepAsync | After pause-gate, before vision analysis | Inside for loop, after `ct.ThrowIfCancellationRequested()` | Fires before expensive vision call |
| OnAfterStepAsync | After page-visit recording, before termination checks | Inside for loop, before `if (stepResult.FrameCompleted...)` | Fires for every step including terminating step |
| OnAfterRunAsync | At each `Done()` call site | 7 call sites: AllVisited, AntiLoop, TargetFound, Timeout, MaxSteps(policy), MaxSteps(exhausted), Cancelled, Error | D-102: `var result = Done(...); await FireAsync(h => h.OnAfterRunAsync(result)); return result;` |
| OnErrorAsync (fatal) | In `catch(Exception)` block | Before `Done(Error)` | `IsRecoverable=false` — engine terminates |
| OnErrorAsync (recoverable) | Engine-level intercept after `ExecuteStepAsync` | `stepResult.NextState == ErrorHandling && _ctx.LastError != null` | D-101: IsRecoverable=true, FSM does not access hooks |
| OnPauseAsync | In PauseAsync | After GlobalState=Paused | P4-B2 |
| OnResumeAsync | In ResumeAsync | Before gate opens | P4-B2 |

**TraversalHookBase**: abstract no-op base class — all 7 methods return `Task.CompletedTask`. Inherit and override selectively.

**TraversalErrorContext**: sealed record `(ErrorType, Message, NodeId?, IsRecoverable)` — lightweight error summary for OnErrorAsync. `IsRecoverable=true` = FSM-level error (engine continues), `IsRecoverable=false` = engine-level fatal (engine terminates).

---

## 6. EntryPolicyExecutor

**3 strategies**:
1. WaitForLoad — wait for page stability before entry
2. BindCurrentScreen — always succeeds (terminal fallback)
3. CheckPrecondition — verify precondition conditions

**Fallback chain**: strategy 1 → strategy 2 → BIND_CURRENT_SCREEN (always succeeds as last resort)

---

## 7. PageSnapshotManager

**Pure functions**:
- `Fingerprint(screenElements)` → deterministic character-by-character hash (H-10: NOT string.GetHashCode())
- `HasChanged(current, previous)` → fingerprint comparison

---

## 8. PageCacheManager

- `UpdateCache(items, timestamp, screenHash)` — store cache
- `RestoreCache()` — retrieve cached items (Phase 2, no TTL/size limits yet)

---

## 9. NodeStackAdapter

Wraps `NodeStack` (from StateMachine layer) + `INodeRegistry` for StepOrchestrator. Provides unified interface for stack operations + node lookup.

---

## 10. Dependency

```
Traversal → StateMachine (TraversalFSM, TraversalRuntimeContext, NodeStack, handlers, StepContext)
Traversal → Graph.Models (TraversalPlan, TraversalNode, NodeType, MatchableItem, MatchResult)
Traversal → Graph.Abstractions (IDynamicMatcher, ITemplateInstantiator — D-28 接口注入)
Traversal → Graph.Services (DynamicMatcher, TemplateInstantiator 默认实现 new)
Traversal → Domain (PageAnalysis, MenuItemType, ExpectedAction)
Traversal → Observability (ITraceRecorder interface reference, SpanType, TraceContext, ExecutionRecord)
TraversalEngine implements IGraphTraversalEngine (8-member async interface)
StateMachine → Traversal (IGraphTraversalEngine — acknowledged upward reference, D-14/D-17)
StateMachine → Observability (ITraceRecorder interface reference — acknowledged upward, D-17)
```

---

## 11. Design Issues (Phase 3)

| Issue | Description | Status |
|-------|-----------|--------|
| D-IV | StepOrchestrator 5 responsibility categories in a single 14-step method | **Resolved** — 方案 A (2 组件): StepOrchestrator (生命周期编排) + InterceptionHandler (FSM 拦截/覆盖)。IInterceptionHandler 接口 + InterceptionResult 值类型。→ D-80 |
| D-V | 10+ critical components have no interface abstraction (cannot mock test) | **Resolved** — 6 新 interface 定义: IDynamicChildManager(3), ITraceCoordinator(18), IEntryPolicyExecutor(2), IPageCacheManager(2), IPageSnapshotManager(2), INodeStackAdapter(3)。D-V-1~D-V-7 见 decisions/log.md |
| H-9 | TraceCoordinator 15/16 empty lambdas | **Resolved** — Phase 2.2 实现 BuildCorrelation + TraceContext + typed signatures, 13/16 方法已实现 |
| H-11 | EntryPolicyExecutor has no fast/polling wait modes | Deferred → Phase 2.2 |
