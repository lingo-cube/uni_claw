## Context

Phase 2.0–2.3 完成了核心引擎搬运（63 任务，9423 行新增代码），所有任务标记 done，`dotnet build` + `dotnet test` 通过。但 spec-vs-code 对齐审查（详见 `docs/refactor/09-phase2-review-report.md`）发现了 3 条硬约束违反和 16 条中等偏差。

审查基准：设计文档 `docs/refactor/08-phase2-core-engine-design-v2.md`（v2.0，含决策日志）+ OpenSpec 8 个 specs。

核心引擎地基存在结构性缺陷：枚举值数溢出、集合引用泄露、接口归属错误、多项行为缺失。这些问题如果不修正，Phase 3 集成时会导致状态机误转、数据隔离失效、依赖循环等不可恢复的缺陷。

详细设计参考：`docs/refactor/10-phase2.1-fix-design.md`

## Goals / Non-Goals

**Goals:**
- 修正 H-1：TraversalState 移除 DynamicMatch（9→8 值）
- 修正 H-2：VisitedChildren 嵌套集合防 HashSet 引用泄露
- 修正 H-5：ITraversalNode 移入 Graph 层（消除 Graph→StateMachine 双向依赖）
- 修正 H-4：PlanCompiler 补充 scope 合法性校验
- 修正 H-6/H-7：StateRestorer 保存完整 stack + 恢复全部字段
- 修正 H-8：PopupHandler 顶层 try-catch 兜底到 back
- 修正 H-10：PageSnapshotManager.Fingerprint 改用确定性哈希
- 修正 M-4：TraceCoordinator Log-and-Continue 补充实际日志
- 修正 M-9：DynamicMatcher text_pattern 补充 Exact 模式
- 对全部 10 个"值数锁定"enum 加防御性断言测试
- 评估 M-14：GlobalState 在 ITraversalContext 的跨 FSM 依赖

**Non-Goals:**
- 架构重构（TraversalRuntimeContext 拆分、StepOrchestrator 模块化）— Phase 3 前评估
- TraceCoordinator 15 个空方法补充实现 — Phase 2.2
- EntryPolicyExecutor fast/polling 等待模式实现 — Phase 2.2
- ULID 同一毫秒单调排序 — Phase 2.2
- IDynamicMatcher 接口定义 — Phase 2.2
- StaticNodes nullable vs 非空、MatchRuleId 语义、TemplateId vs node.name 等 P2 偏差 — Phase 3 前逐步处理

## Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| D-1 | Phase 2.1 按修正类型分 4 子 Phase（枚举→接口→集合→行为） | 同类型修正互不干扰，每步 dotnet test 验证增量 |
| D-2 | ITraversalNode 移入 Graph.Models（方案 B） | 职责归属：节点描述是 Graph 概念而非 FSM 状态。NodeType 已在 Domain，ChildrenStrategy 已在 Graph — ITraversalNode 依赖的类型都在 Domain/Graph |
| D-3 | INodeStack 保留在 StateMachine 层 | INodeStack 是 FSM 上下文的一部分，被 ITraversalContext 引用。移入 Graph 层会让 ITraversalContext `using Graph.Models`，增加接口跨层依赖 |
| D-4 | VisitedPages/VisitedNodes 保持直接 HashSet 暴露 + 注释标注安全级别 | 当前消费者全是引擎内部，cast-back 风险可控；AI advisor 使用 TraversalContextSnapshot（ImmutableHashSet，完全安全）。Phase 3 改进 |
| D-5 | ReadOnlySetWrapper 用 private sealed class（非 public） | 仅 TraversalRuntimeContext 内部使用，不暴露到外部 |
| D-6 | M-14 仅评估不改 | GlobalState 移出 ITraversalContext 影响大量消费者（FSM、Orchestrator、Handler 都读 GlobalState），风险高 |
| D-7 | Log-and-Continue 用 Console.WriteLine（非 ILogger） | Phase 2 无 DI 注入体系，ILogger 需 Phase 3 引入 |
| D-8 | text_pattern TextMatchMode 默认 Contains | 向后兼容，Python 默认也是 substring 匹配 |
| D-9 | PageSnapshotManager 确定性哈希用逐字符累加方案（`hash * 31 + (int)ch`） | 简单、快速、确定性、无外部依赖。SHA256 过度，string.GetHashCode() 跨进程非确定 |
| D-10 | StateRestorer 保存 List<StackFrame> 替代 int NodeStackDepth | NodeStack 已有 `List<StackFrame>` 内部结构，保存完整列表可精确恢复。仅存 depth 无法重建完整 stack |

### Implementation Sequence

```
Phase 2.1a: 枚举修正（H-1 + 防御性值数守卫） → dotnet test
Phase 2.1b: 接口归属修正（H-5 + M-14 评估） → dotnet test
Phase 2.1c: 集合隔离修正（H-2 + cast-back 阻断） → dotnet test
Phase 2.1d: 行为补充修正（H-4/H-6-8/H-10/M-4/M-9 + 测试） → dotnet test
```

Each sub-phase: implement → `dotnet test` → confirm incremental pass → next phase.

### Architecture Pattern: ReadOnlySetWrapper

ReadOnlySetWrapper wraps `HashSet<string>` as `IReadOnlySet<string>` without exposing the underlying reference. Key properties:
- `sealed class` — no inheritance path to HashSet
- implements all `IReadOnlySet<string>` members by delegating to internal `_set`
- cast-back `(HashSet<string>)wrapper` returns null (InvalidCastException) because the wrapper does not inherit from HashSet

### Architecture Pattern: Interface Relocation

ITraversalNode relocation from StateMachine to Graph.Models follows the same pattern as NodeType relocation (Phase 2.0):
1. Create new file in target layer with correct namespace
2. Copy interface definition to new file
3. Remove from source file
4. Update all using references
5. Verify dependency direction is one-way

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| D-2 ITraversalNode 移入 Graph 可能需要 INodeStack also 移入 | D-3 决策明确 INodeStack 保留 StateMachine，INodeStack 通过 `using Graph.Models` 引用 ITraversalNode（单向） |
| TraversalState.DynamicMatch 移除后如果有引用方 | grep 预检查 + StepOrchestrator 已用 ChildrenStrategy.DynamicMatch 而非 TraversalState.DynamicMatch |
| ReadOnlySetWrapper 性能开销（每次 VisitedChildren 访问创建新 wrapper dict） | 当前已有 lazy rebuild 机制（`_visitedChildrenReadOnly` 缓存），wrapper dict 同样缓存 |
| GlobalState 保留在 ITraversalContext（M-14 不改）可能导致 Phase 3 依赖清理困难 | 评估文档明确记录风险和 Phase 3 修正方向，不做隐瞒 |
| H-6/H-7 StateRestorer 保存完整 List<StackFrame> 增加内存占用 | 弹窗处理是短暂操作，保存/恢复后立即释放，内存增量可忽略 |
