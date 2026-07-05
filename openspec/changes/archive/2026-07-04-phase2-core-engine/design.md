## Context

Phase 1/1.1 完成了 Domain 层（24 类型，185 测试全绿）。当前 C# 非 Domain 层只有 30+ 类型骨架（接口定义 + PlaceholderResolver + TemplateValidator + NodeStack），零引擎逻辑。Python `main` 分支的核心引擎需要搬运到 C#，同时修正 3 个架构问题和 5 处设计偏差。

详细设计参考：`docs/refactor/08-phase2-core-engine-design-v2.md`（v2.0，含决策日志）
Python 架构参考：`docs/refactor/07-phase2-python-architecture-reference.md`（源码验证基准）

## Goals / Non-Goals

**Goals:**
- 修正架构问题（F-1 统一 PageAnalysis、NodeType 移入 Domain、ITraversalContext 类型修正）
- 搬运 Trace 基础层、Graph 基础层、State Machine 核心、Traversal Engine 子系统
- 每个子系统配单元测试，`dotnet test` 增量通过
- Python 源码为搬运基准，修正 5 处设计偏差（D-1 到 D-5）

**Non-Goals:**
- 端到端集成仿真测试（Phase 3）
- AI provider 实现、ADB 层、Config 层、Safety/Simulation/Analysis 模块
- JSON → TraversalPlan 反序列化完整管道

## Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| D-1 | TraversalFSM 移除 PRECONDITION_CHECK → BRANCH | Python V6.7 handler 从不返回 BRANCH（只返回 EXECUTE/ERROR_HANDLING），`precondition_failed()` 是 dead code。状态机转换矩阵应只定义实际被执行的路径。未来需要"前置条件跳过"应走 ERROR_HANDLING 管道 |
| D-2 | TraversalRuntimeContext 用 sealed class + ITraversalContext 只读接口 | 引擎每步更新 5-8 字段，record + with 每步复制整个对象（26 字段 × 5-8 次 × 500 步 ≈ 3000 次分配）。Python 用直接赋值（mutable dataclass），C# 应对齐。不可变数据（AI 快照 TraversalContextSnapshot）用 sealed record class |
| D-3 | ContainerHandler 三阶段管道不做缓存 | Python 缓存键包含每步变化的值（visited_children List + depth），缓存命中率极低。CompletionDetector/FallbackDecider 计算量很小，每步纯计算开销可忽略 |
| D-4 | ITraversalContext 用强类型只读集合 | VisitedPages → `IReadOnlySet<string>`（Python 是 Set[str]，不是 Dict）、VisitedChildren → `IReadOnlyDictionary<string, IReadOnlySet<string>>`、CurrentPath → `IReadOnlyList<string>`。接口只暴露只读视图，引擎内部用 mutable 集合 |
| D-5 | TraversalRuntimeContext 预留 scroll/screen 接口位置 | Phase 2 定位是搬运基准，不混入新功能。26 字段对齐 Python，另加 TODO 注释预留 IScrollHandler/IPageSnapshot（Phase 3 实现） |

### Implementation Sequence

```
Phase 2.0: 架构修正 + Trace 基础层
Phase 2.1: Graph 基础层 + PlanCompiler + DynamicMatcher
Phase 2.2: State Machine 核心
Phase 2.3: Traversal Engine 子系统
```

Each sub-phase: implement → `dotnet test` → confirm incremental pass → next phase.

### Architecture Pattern: Three-Stage Pipeline

ContainerHandler, ErrorHandler, PopupHandler all share the same pattern:
1. **Classify/Detect** → output enum (CompletionStatus, ErrorType, PopupType)
2. **Select Strategy** → output strategy enum (FallbackAction, ErrorStrategy, DismissStrategy)
3. **Execute** → Hook Dispatch table (`Dictionary<Enum, Func<Context, Result>>`), exception fallback to safest default

### Architecture Pattern: Interception Layer

StepOrchestrator wraps TraversalFSM:
- FSM produces state transitions
- Orchestrator intercepts specific transitions (BRANCH, NODE_SELECT, FRAME_COMPLETE) to inject engine-level logic
- 3 subsystems bridged: FSM + DynamicChildManager + TraceCoordinator

### Readonly View Isolation

ITraversalContext readonly views MUST NOT leak mutable internal references:
- `IReadOnlyList<string>` via `.AsReadOnly()` wrapper on `List<string>`
- `IReadOnlySet<string>` direct expose of `HashSet<string>` is safe (no mutation methods), but guard against cast-back
- `INodeStack` in TraversalContextSnapshot: evaluate whether to provide read-only wrapper (only Peek/IsEmpty/Depth) or whether NodeIds field is sufficient

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| D-2 sealed class 丧失 record 值语义 | CreateReadOnlySnapshot() 产生 immutable record（TraversalContextSnapshot）给 AI advisor，引擎内部不需要值语义 |
| D-1 移除 PRECONDITION_CHECK → BRANCH 可能限制未来灵活性 | 如果需要"前置条件跳过"，走 ERROR_HANDLING 管道更安全（经过分类+策略选择，不是直接跳转） |
| ITraversalContext IReadOnlySet 泄露 HashSet 引用 | 在 TraversalContextSnapshot 中用 ImmutableHashSet（完全隔离），引擎内部接口用 IReadOnlySet（轻量隔离，防 cast-back） |
| Phase 2.0 架构修正影响现有代码 | 先改签名（Phase 2.0），构造逻辑留给 Phase 2.2/2.3（渐进式） |
| StepOrchestrator 14 步流程复杂度高 | 详尽单元测试：Anti-loop 3 场景、FRAME_COMPLETE override 3 场景、交互组合 |
| 4 个子 Phase 之间有依赖关系 | 每个 Phase 完成后 `dotnet test` 确认增量通过再进下一个 |
