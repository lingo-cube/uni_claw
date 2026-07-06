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
| `TraceRecord` | StepNumber, FromState, ToState, CurrentNodeId, CurrentPageId, ActionExecuted, ActionSuccess, ChildPushed, FrameCompleted | 每步 trace 记录 (独立于 ITraceRecorder) |
| `TraversalEngineConfig` | MaxSteps=1000, MaxDepth=10, ThrowOnError=false, TraceEnabled=true, DelayPerStepMs=0 | 引擎配置 (合并 SimulationConfig) |
| `ActionRecord` | Action, Timestamp, Parameters, Success | 操作记录 |

### Interfaces

| Interface | 所在文件 | 用途 |
|-----------|---------|------|
| `IGraphTraversalEngine` | Traversal/IGraphTraversalEngine.cs | 遍历引擎 8 成员 async 接口 (Plan, Context, CurrentState, InitializeAsync, RunAsync, PauseAsync, ResumeAsync, StopAsync, GetStateAsync) |
| `INodeRegistry` | TraversalEngine.cs | 2 方法: GetNode, Register |
| `IActionExecutor` | Traversal/IGraphTraversalEngine.cs | 6 方法 + GetHistory |

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

(→ openspec/specs/step-orchestrator/spec.md for full 14-step detail)

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

**16+ span methods**: BeginStep, EndStep, BeginSpan, EndSpan, RecordAction, RecordError, etc.

**Active gate**: `Active` flag — all methods are no-op when Active=false.

**Log-and-Continue pattern** (→ patterns/dispatch-table.md): exceptions in span methods are caught and logged, never propagated to caller.

**Implementation status** (→ decisions/log, deferred to Phase 2.2):
- H-9: 15/16 methods are currently empty lambdas (no actual logging implementation)
- M-4: Log-and-Continue should add Console.WriteLine logging (currently silently swallows)

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
Traversal → Common (UlidGenerator)
Traversal → Observability (ITraceRecorder interface reference)
TraversalEngine implements IGraphTraversalEngine (8-member async interface)
StateMachine → Traversal (IGraphTraversalEngine — acknowledged upward reference, D-14/D-17)
```

---

## 10. Design Issues (Phase 3)

| Issue | Description | Status |
|-------|-----------|--------|
| D-IV | StepOrchestrator 5 responsibility categories in a single 14-step method | Phase 3 evaluation |
| D-V | 10+ critical components have no interface abstraction (cannot mock test) | Phase 3 evaluation |
| H-9 | TraceCoordinator 15/16 methods are empty lambdas | Deferred → Phase 2.2 |
| H-11 | EntryPolicyExecutor has no fast/polling wait modes | Deferred → Phase 2.2 |
