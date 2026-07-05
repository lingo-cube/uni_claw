# Phase 2 核心引擎 Review 报告

> **日期**: 2026-07-04
> **审查范围**: Phase 2.0–2.3 全部实现（63 任务，9423 行新增代码）
> **审查基准**: 设计文档 `08-phase2-core-engine-design-v2.md` + OpenSpec 8 个 specs
> **审查方法**: 4 组并行 spec-vs-code 对齐比对 + 5 层静态验证

---

## 1. 审查方法

本次审查分两个阶段：

**第一阶段（spec-vs-code 对齐）**：4 组并行 agent 逐条比对 OpenSpec spec requirement 与实际 C# 代码，共发现 41 条偏差。

**第二阶段（核心模型逐层验证）**：从 Domain 层向上逐层静态验证核心模型定义和抽象：

```
Layer 0: Domain 层完整性（Phase 1 基础）
Layer 1: 核心枚举（NodeType/TraversalState/GlobalState/Handler enums）
Layer 2: 核心接口（ITraversalContext/ITraversalNode/INodeStack/ITraceRecorder）
Layer 3: 核心数据模型（TraversalRuntimeContext/TraversalContextSnapshot/TraceNode hierarchy）
Layer 4: Graph 基础模型（TraversalNode/TraversalPlan/EntryConfig/MatchCondition）
```

---

## 2. 硬约束违反（需立即修正）

### H-1: TraversalState 包含 DynamicMatch（9 值而非 8 值）

**设计文档原文**（§6.1）：

> TraversalStateMachine 有 **8 个状态**（不是 9 个——DYNAMIC_MATCH 不是 FSM 状态，是 ChildrenStrategyType 值）

**实际代码**（TraversalState.cs 第 9-37 行）：

```csharp
public enum TraversalState
{
    NodeSelect, PreconditionCheck, Execute, ResultVerify,
    Branch, FrameComplete, ErrorHandling, PopupHandling,
    DynamicMatch  // ← 不应存在
}
```

**根因**：Phase 2.0 移 NodeType enum 时只移了值本身，没有清理 StateMachine 层中所有非 FSM 状态的 enum 成员。DynamicMatch 是 ChildrenStrategyType 值，不是 FSM 状态，应从 TraversalState 中移除。

**影响**：
- TraversalFSM 转换矩阵不包含 DynamicMatch 的任何转换（它是不可达状态）
- StepOrchestrator 步骤 9/10 通过 `ChildrenStrategy.DynamicMatch`（而非 `TraversalState.DynamicMatch`）检查子节点策略
- 测试中没有断言 TraversalState 值数量为 8

**修正方案**：
1. 从 `TraversalState` enum 中移除 `DynamicMatch` 成员
2. 添加测试断言：`Enum.GetValues<TraversalState>().Length == 8`
3. 确认 `ChildrenStrategyType.DynamicMatch` 在 Domain/Graph 层有独立定义（已有 `ChildrenStrategy` enum 在 Graph 层）
4. grep 确认无代码引用 `TraversalState.DynamicMatch`

---

### H-2: VisitedChildren 嵌套集合泄露 HashSet 引用

**设计文档原文**（§4.3）：

> `IReadOnlyDictionary<string, IReadOnlySet<string>>` 需确保嵌套的 `IReadOnlySet<string>` 同样不泄露 `HashSet<string>` 引用

**实际代码**（TraversalRuntimeContext.cs 第 161-165 行）：

```csharp
private ReadOnlyDictionary<string, IReadOnlySet<string>> GetVisitedChildrenReadOnly()
{
    var dict = new Dictionary<string, IReadOnlySet<string>>();
    foreach (var (key, set) in _visitedChildren)
        dict[key] = set; // HashSet<string> 直接赋值为 IReadOnlySet<string>
    return new ReadOnlyDictionary<string, IReadOnlySet<string>>(dict);
}
```

**根因**：实现者对设计文档约束的理解停留在"接口级安全"（IReadOnlySet 不暴露 Add/Remove），但设计文档要求的是"cast-back 级安全"（消费者不应能通过 `(HashSet<string>)visitedChildren["key"]` 修改内部状态）。

**影响**：
- ITraversalContext 消费者可通过 cast-back 修改引擎内部数据
- TraversalContextSnapshot 用了 `ToImmutableHashSet()`（完全安全），但 ITraversalContext 的实时视图不安全
- 设计文档 §4.3 的只读隔离要求有 3 个安全级别：接口级（最低）、cast-back 级（中等）、快照级（最高）。实现只达到了接口级。

**修正方案**：
将嵌套 `HashSet<string>` 包装为不可泄露的只读集合：

```csharp
foreach (var (key, set) in _visitedChildren)
    dict[key] = new ReadOnlySetWrapper(set); // 包装而非直接赋值
```

`ReadOnlySetWrapper` 是一个 private struct/class，实现 `IReadOnlySet<string>` 但内部持有 `HashSet<string>`，不提供 public 转换途径。

---

### H-5: ITraversalNode 定义在 StateMachine 层（Graph→StateMachine 双向依赖）

**设计文档原文**（§4.2）：

> NodeType 是数据枚举（描述节点类型），不是 FSM 状态。Graph/AI/Traversal 引用 NodeType 时不应被迫依赖 StateMachine 层。

设计文档移了 NodeType 到 Domain，但**没有讨论 ITraversalNode 的归属**。ITraversalNode 仍在 TraversalState.cs（StateMachine 层），创建双向依赖：

```
StateMachine (TraversalState.cs) 定义 ITraversalNode → using Graph.Models（引用 ChildrenStrategy）
Graph (TraversalNode.cs) 实现 ITraversalNode → using StateMachine（引用 ITraversalNode）
```

**根因**：§4.2 的架构修正只移了 NodeType enum，没有连带检查"引用 NodeType 的接口（ITraversalNode）是否也需要移走"。这是一个不完整的架构修正。

**影响**：
- Graph 层被迫依赖 StateMachine 层
- TraversalNode.cs 有 `using UniClaw.Core.StateMachine`
- 违反设计文档的依赖方向原则："数据类型应在 Domain，而非在 StateMachine"

**修正方案**：
将 `ITraversalNode` 接口从 `TraversalState.cs` 移入 Domain 或 Graph 层：

**方案 A（移入 Domain）**：ITraversalNode 只引用 `NodeType`（已在 Domain）和 `ChildrenStrategy`（需确认位置）。如果 ChildrenStrategy 也在 Domain 或可移入 Domain，则 ITraversalNode 可移入 Domain。

**方案 B（移入 Graph）**：ITraversalNode 是 TraversalNode 的接口，与 Graph 层关系最密切。移入 Graph.Models 命名空间，与 TraversalNode 同层。

**推荐方案 B**——ITraversalNode 的职责是描述遍历节点（Graph 概念），不是 FSM 状态。NodeType 已在 Domain，ChildrenStrategy 已在 Graph。ITraversalNode 移入 Graph 后，TraversalNode.cs 不再需要 `using StateMachine`。

---

## 3. 中严重性偏差（16 条，修正优先级排序）

### 需在 H-1/H-2/H-5 修正后同步处理的

| # | 偏差 | 优先级 | 与硬约束的关联 |
|---|------|--------|---------------|
| M-14 | ITraversalContext 包含 GlobalState 类型（跨 FSM 依赖） | P0 | H-5 修正后需重新评估接口归属 |
| M-1 | TraceNode 是 abstract（非 sealed）— C# 语言约束 | P1 | 不修正，但需在文档中注明设计折衷 |
| M-3 | 预留接口用 `object?`（非 `IScrollHandler?`/`IPageSnapshot?`） | P1 | 接口不存在时的合理折衷，但需在注释中标注偏离原因 |
| M-4 | Log-and-Continue 吞异常但不日志记录 | P1 | 需补充 ILogger 或 Console.WriteLine |
| M-5 | TraceCoordinator 同步调用异步 ITraceRecorder | P2 | sync-over-async 是已知风险，需评估 |
| M-6 | StaticNodes 可空（非非空空 dict） | P2 | Python 对齐 vs C# 习惯，需决定 |
| M-9 | text_pattern 仅 Contains（缺 Exact 模式） | P1 | 设计文档明确要求两种模式 |
| M-10 | MatchRuleId 用 condition.Type（非 rule ID） | P2 | Match() 不接收 rule ID |
| M-11 | TemplateInstantiator 用 TemplateId（非 node.name） | P2 | 先有鸡还是先有蛋问题 |

### 实现不完整（需补充实现）

| # | 偏差 | 优先级 | 说明 |
|---|------|--------|------|
| H-9 | TraceCoordinator 15/16 方法为空 lambda | P1 | 需逐个补充实际 span 记录逻辑 |
| H-11 | EntryPolicyExecutor 无 fast/polling 等待模式 | P1 | 需实现两种等待模式 |
| H-4 | PlanCompiler scope 合法性校验缺失 | P1 | 需在 _validate_slots 中加 scope 校验 |
| H-6/H-7 | StateRestorer 仅保存 depth + 不恢复全部字段 | P1 | 需改为保存完整 stack + 恢复所有字段 |
| H-8 | PopupHandler 无顶层异常兜底到 back | P1 | 需在 HandlePopup 加 try-catch |
| H-10 | PageSnapshotManager.Fingerprint 用 GetHashCode() | P1 | 需改用确定性哈希算法 |
| H-3 | ULID 同一毫秒不保证单调排序 | P2 | 需加计数器或排序机制 |

### 其余中等偏差

| # | 偏差 | 说明 |
|---|------|------|
| M-2 | _nodeStack 类型 NodeStack vs List<StackFrame> | NodeStack 有行为封装，合理折衷 |
| M-7 | PlanCompiler 步骤 5+6 合并 | 功能等价，可接受 |
| M-8 | IDynamicMatcher 接口缺失 | spec 增量要求，需补充 |
| M-12 | ErrorClassifier 异常类型匹配 case-sensitive | 应统一为 case-insensitive |
| M-13 | CompletionDetector ShouldBacktrack 硬编码 false | 应由 exit_condition 决定 |
| M-15 | StepOrchestrator 步骤 4 用路径比较（非 PageSnapshotManager） | 需改用 PageSnapshotManager |
| M-16 | DynamicChildManager STATIC 缺注册表时返回 null | 应继续迭代 |

---

## 4. 低严重性偏差（14 条）

| # | Phase | 偏差 |
|---|-------|------|
| L-1 | 2.0 | ITraceRecorder 额外 CurrentSession 属性 |
| L-2 | 2.0 | INodeStack 通过 ITraversalContext 暴露可变方法（Push/Pop/Clear） |
| L-3 | 2.0 | SessionNode/StepNode/SpanNode 附加字段全 nullable |
| L-4 | 2.1 | TEMPLATE_SETS 字段名 TemplateSets（PascalCase vs UPPER_SNAKE） |
| L-5 | 2.1 | TEMPLATE_SETS 类型 IReadOnlyDictionary+ImmutableArray |
| L-6 | 2.1 | PlanCompiler 方法名 PascalCase vs snake_case |
| L-7 | 2.1 | PlanCompiler NodeType.Screen vs spec "STATIC/DYNAMIC_MATCH" |
| L-8 | 2.1 | DynamicMatcher 先调用 IsValid（spec 只提 FromValue） |
| L-9 | 2.1 | MatchableItem 类型未在 spec 中定义 |
| L-10 | 2.1 | PlaceholderResolver 定义在 Template.cs 中 |
| L-11 | 2.2 | GlobalFSM callback 异常捕获但不日志 |
| L-12 | 2.2 | TraversalFSM consecutive_errors 仅 TraversalRuntimeContext 类型生效 |
| L-13 | 2.3 | StepContext spec 计数 13 但实际 12 |
| L-14 | 2.3 | PageSnapshotManager sealed class 非 static class |

低严重性偏差基本是命名约定、类型细节、spec 计数差异等，不影响正确性，可在后续迭代中统一。

---

## 5. 设计层面问题（Phase 3 前需评估）

以下问题不涉及硬约束违反，但影响 Phase 3 的架构方向。

### D-Ⅰ: TraversalRuntimeContext 是 God Object

26 个可变字段服务于 5 个子系统（FSM/Orchestrator/Error/Popup/ChildMgr），子系统之间通过共享字段隐式耦合。

**Phase 3 建议**：将 TraversalRuntimeContext 拆分为职责单一的子 context：
- `NavigationContext`（path, node_stack, visited_pages/nodes/children）
- `ErrorContext`（consecutive_errors, retry_count, last_error, exception_chain）
- `SessionContext`（trace_id, ai_provider, device_experience, completion_policy）
- `CacheContext`（page_cache, cache_valid, current_fingerprint）

各子系统只读写自己需要的子 context，减少隐式耦合。

### D-Ⅱ: StateMachine ↔ Graph 双向依赖

ITraversalNode 在 StateMachine 层定义，TraversalNode 在 Graph 层实现，创建双向依赖。

**Phase 3 建议**：H-5 修正将 ITraversalNode 移入 Graph 层，消除双向依赖。

### D-Ⅲ: ITraversalContext 两套只读机制 + GlobalState 跨 FSM

ITraversalContext 同时服务于引擎内部（需要实时值）和 AI advisor（需要不可变快照），但两者安全需求不同。GlobalState 在 ITraversalContext 上暴露，违反"两个 FSM 不共享类型"的约束。

**Phase 3 建议**：
- ITraversalContext 仅服务于引擎内部，明确标注"引擎专用"
- AI advisor 仅使用 TraversalContextSnapshot
- GlobalState 从 ITraversalContext 移到 TraversalRuntimeContext 的 engine-only 属性

### D-Ⅳ: StepOrchestrator 5 类职责混在 14 步单体流程

FSM 调度、Child 管理、Trace 记录、State 更新、Cache 管理 5 类职责在一个方法中交织。

**Phase 3 建议**：将 StepOrchestrator 拆分为：
- `StepScheduler`（调用 FSM + 确定最终状态）
- `InterceptionHandler`（BRANCH/Anti-loop/FRAME_COMPLETE override）
- `TraceRecorder`（步骤边界 trace）

Orchestrator 只做调度协调，不直接实现任何一类职责。

### D-Ⅴ: 10+ 关键组件无接口抽象

DynamicChildManager、TraceCoordinator、EntryPolicyExecutor 等全是 sealed class 无接口，无法 mock 测试。

**Phase 3 建议**：为每个子系统提取接口（IDynamicChildManager、ITraceCoordinator、IEntryPolicyExecutor 等），StepContext 通过接口引用而非直接依赖实现类。

---

## 6. 根因分析

### 6.1 偏差根因分类

| 根因类别 | 高 | 中 | 低 | 代表偏差 |
|----------|---|---|----|---------|
| **实现遗漏**（设计要求但代码没做） | 4 | 4 | 1 | H-4 scope 校验、M-9 Exact 模式 |
| **实现不完整**（骨架/占位符而非完整逻辑） | 3 | 1 | 0 | H-9 空 lambda、H-11 等待模式 |
| **不完整架构修正**（修正了一部分，残留依赖） | 2 | 1 | 0 | H-1 DynamicMatch 残留、H-5 ITraversalNode 未移 |
| **技术知识盲区**（C# 特性/行为不了解） | 1 | 2 | 0 | H-10 GetHashCode 非确定性、M-5 sync-over-async |
| **实现简化**（功能等价但步骤/结构不同） | 1 | 3 | 3 | H-6 NodeStackDepth vs full stack |
| **语义偏差**（字段值/行为不符合设计意图） | 0 | 4 | 3 | M-10 MatchRuleId、M-11 TemplateId |

### 6.2 核心流程问题

| 流程缺陷 | 改进措施 | 优先级 |
|----------|---------|--------|
| **值数锁定无自动验证** | 加 `Enum.GetValues<X>().Length == N` 测试断言 | P0 |
| **架构修正无依赖追踪** | 移类型前列出所有引用方，逐一确认位置 | P0 |
| **AC 验证仅结构级** | 加"设计文档语义 vs 代码行为"对齐验证步骤 | P0 |
| **偏离设计未标注原因** | 代码注释标注偏离原因和设计文档原文 | P1 |
| **只读隔离安全级别模糊** | 设计文档标注安全级别（接口级/cast-back级/快照级） | P1 |
| **实现者自写测试** | 对齐测试应独立于实现者 | P1 |
| **占位符无明确标记** | 空 lambda 应标注 `// TODO: implement` | P1 |

---

## 7. 修正计划

### 7.1 第一阶段：硬约束修正（H-1/H-2/H-5）

| 步骤 | 内容 | 验证 |
|------|------|------|
| H-1-a | 从 TraversalState 移除 DynamicMatch | `Enum.GetValues<TraversalState>().Length == 8` |
| H-1-b | 添加 TraversalState 值数断言测试 | dotnet test 通过 |
| H-1-c | grep 确认无代码引用 `TraversalState.DynamicMatch` | grep 无结果 |
| H-2-a | 实现 ReadOnlySetWrapper（private，包装 HashSet 不泄露引用） | cast-back 测试：`(HashSet<string>)wrapper` 返回 null |
| H-2-b | 修改 GetVisitedChildrenReadOnly() 使用 ReadOnlySetWrapper | ITraversalContext.VisitedChildren["key"] cast-back 失败 |
| H-2-c | 添加 VisitedChildren cast-back 阻断测试 | dotnet test 通过 |
| H-5-a | 将 ITraversalNode 接口从 TraversalState.cs 移入 Graph.Models | ITraversalNode 在 Graph 层 |
| H-5-b | 更新 TraversalNode.cs 的 using（移除 `using StateMachine`） | TraversalNode.cs 无 StateMachine using |
| H-5-c | 更新引用方 using | grep 确认所有引用更新 |
| H-5-d | 添加 TraversalNode 依赖方向测试/断言 | dotnet test 通过 |
| 全部 | `dotnet build` + `dotnet test` | 0 错误 + 测试全绿 |

### 7.2 第二阶段：中等偏差修正（按优先级）

P1 级修正需在第一阶段完成后同步推进：
- M-4: Log-and-Continue 补充日志输出
- M-9: text_pattern 补充 Exact 模式
- M-14: GlobalState 从 ITraversalContext 评估移除

P2 级修正可在 Phase 3 前逐步处理：
- H-9: TraceCoordinator 补充实际 span 记录逻辑
- H-11: EntryPolicyExecutor 实现 fast/polling 等待
- H-10: PageSnapshotManager 用确定性哈希
- H-3: ULID 单调排序保证

### 7.3 第三阶段：架构重构评估（Phase 3 前）

在硬约束和中偏差修正完成后，评估 D-Ⅰ 到 D-Ⅴ 的架构重构：
- D-Ⅰ: TraversalRuntimeContext 拆分为子 context
- D-Ⅳ: StepOrchestrator 拆分为调度+拦截+trace
- D-Ⅴ: 关键组件提取接口抽象

架构重构的决策应在 Phase 3 设计文档中明确，不在 Phase 2 修正阶段做。
