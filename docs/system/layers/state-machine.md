# Layers — State Machine

> **Tier 3 · Layers**: StateMachine 层规格书。改 FSM/Handler/Context 时更新。
> 状态: Phase 2.3-sim 完成 (IVisionProvider 补全 + Simulation namespace + 488 tests pass)
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
| `ITraversalContext` | TraversalState.cs | 只读上下文接口 (→ patterns/readonly-isolation.md) |
| `INodeStack` | TraversalState.cs | DFS stack 接口 (Depth, MaxDepth, Push, Pop, Peek, IsEmpty, Clear) |
| `IGraphTraversalEngine` | TraversalState.cs (空 stub, → D-14) | 最小接口定义，避免循环依赖。完整定义在 Traversal namespace |
| `IVisionProvider` | StepContext.cs | 视觉分析接口 (2 方法: AnalyzeCurrentPageAsync + FindAppEntryAsync) |

### Key Classes

| Class | 用途 | Pattern |
|-------|------|---------|
| `TraversalFSM` | 微观 FSM (8 状态, enum-based switch dispatch) | → patterns/fsm-design.md |

#### TraversalFSM Handler 实现状态

| Handler | 状态 | 实现度 | 说明 |
|---------|------|--------|------|
| HandleNodeSelect | ✅ 完成 | 100% | 真实逻辑: stack empty→Branch, 有node→PreconditionCheck |
| HandlePreconditionCheck | ⏳ Phase 2.3b | stub | `return Execute` ("assume pass") → 需要 3-round retry + vision correction |
| HandleExecute | ✅ Phase 2.3a | 100% | Operation dispatch (Click/Swipe/Back/InputText/NoAction) via OperationDispatcher → optional RestoreAction → ResultVerify / ErrorHandling; StepContext null → stub fallback |
| HandleResultVerify | ⏳ Phase 2.3b | stub | `return Branch` → 需要 vision verify + page comparison |
| HandleBranch | ✅ Phase 2.3a | 100% | ChildrenStrategy-based decision: STATIC → unvisited check (VisitedChildren); DYNAMIC_MATCH → optimistic NodeSelect; NONE → leaf/container depth logic → NodeSelect / FrameComplete |
| HandleFrameComplete | ✅ 完成 | 100% | 真实逻辑: return NodeSelect |
| HandleErrorHandling | ⏳ Phase 2.3c | stub | `return NodeSelect` → 需要 3-layer recovery policy |
| HandlePopupHandling | ⏳ Phase 2.3c | stub | `return ResultVerify` → 需要 safe button detection + click + verify |

**P1 (Phase 2.3a)**: HandleExecute + HandleBranch → 最小可运行遍历循环
**P2 (Phase 2.3b)**: HandleResultVerify + HandlePreconditionCheck → 验证+纠正
**P3 (Phase 2.3c)**: HandleErrorHandling + HandlePopupHandling → 容错+弹窗
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
| `TraversalRuntimeContext` | 30 可变状态 (26 core + 2 Phase-3 reserved + CurrentFrame + 1 lazy cache), ITraversalContext impl, ReadOnlySetWrapper | → patterns/readonly-isolation.md |

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

---

## 4. NodeStack

- `DefaultMaxDepth = 10`
- `Push(node, children)` → returns false when depth >= MaxDepth (深度限制)
- `Peek(offset)` → 0=top (current), 1=parent, etc.
- Internal `StackFrame` (sealed record, implements IStackFrame)
- `Pop()` returns IStackFrame with NodeId + Node + Children

---

## 5. TraversalRuntimeContext (30 可变状态)

(→ patterns/readonly-isolation.md for collection safety details)

**26 core mutable fields** (aligned with Python src/trace/context.py, 名称非 canonical → D-15):
- traceId, nodeStack, currentPath, currentPageAnalysis, currentFingerprint, cacheValid
- visitedPages, visitedLevel1Menus, visitedLevel2Menus, visitedNodes, visitedChildren
- pageTree, actionHistory (last 5), failedNodes
- consecutiveErrors, maxDepth, stepCount, retryCount
- completionPolicy, deviceExperience, globalState, lastError, exceptionChain
- aiProvider, pageCache, waitAfterActionMs

**4 额外可变状态** (不在标注块 26 中):
- _scrollHandler (object?, Phase 3 reserved)
- _currentSnapshot (object?, Phase 3 reserved)
- _visitedChildrenReadOnly (ReadOnlyDictionary?, lazy cache)
- CurrentFrame (ITraversalNode?, ITraversalContext property, FSM 每步更新)

**Engine-only mutation methods**: AppendPath, PopPath, MarkVisited, MarkNodeVisited, AddVisitedChild, IncrementStepCount, etc.

**CreateReadOnlySnapshot()**: → TraversalContextSnapshot (8 immutable fields, fully isolated from source)

**Design Issues (Phase 3)**:
- D-I: God Object (30 可变状态, 5 subsystems) — needs decomposition
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
