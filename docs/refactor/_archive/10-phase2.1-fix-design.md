# Phase 2.1 设计：硬约束修正 + P1 偏差补充

> **版本**: 1.0
> **日期**: 2026-07-04
> **分支**: `feature/refactor`
> **前置**: Phase 2.0–2.3 已完成（63 任务），Review 报告 `09-phase2-review-report.md`
> **目标**: 修正 3 条硬约束违反 + 6 条 P1 中等偏差，确保核心引擎地基稳固
> **验证基准**: 设计文档 `08-phase2-core-engine-design-v2.md` + Review 报告 §2–§3

---

## 1. 背景

Phase 2.0–2.3 完成了核心引擎搬运（63 任务，9423 行新增代码），所有任务标记 done，`dotnet build` + `dotnet test` 通过。但 spec-vs-code 对齐审查发现了 **3 条硬约束违反** 和 **16 条中等偏差**（详见 `09-phase2-review-report.md` §2–§3）。

Phase 2.1 聚焦修正影响正确性和架构的偏差，不做架构重构（D-Ⅰ到 D-Ⅴ 属于 Phase 3 前评估范畴）。

---

## 2. 目标 / 不目标

**目标**:
- 修正 H-1：TraversalState 移除 DynamicMatch（9→8 值）
- 修正 H-2：VisitedChildren 嵌套集合防 HashSet 引用泄露
- 修正 H-5：ITraversalNode 移入 Graph 层（消除 Graph→StateMachine 双向依赖）
- 修正 H-4：PlanCompiler 补充 scope 合法性校验
- 修正 H-6/H-7：StateRestorer 保存完整 stack + 恢复全部字段
- 修正 H-8：PopupHandler 顶层 try-catch 兜底到 back
- 修正 H-10：PageSnapshotManager.Fingerprint 改用确定性哈希
- 修正 M-4：TraceCoordinator Log-and-Continue 补充实际日志
- 修正 M-9：DynamicMatcher text_pattern 补充 Exact 模式
- 对全部枚举加值数断言测试（防御性守卫）
- 评估 M-14：GlobalState 在 ITraversalContext 的跨 FSM 依赖

**不目标**:
- 架构重构（TraversalRuntimeContext 拆分、StepOrchestrator 模块化）— Phase 3 前评估
- TraceCoordinator 15 个空方法补充实现 — Phase 2.2
- EntryPolicyExecutor fast/polling 等待模式实现 — Phase 2.2
- ULID 同一毫秒单调排序 — Phase 2.2
- IDynamicMatcher 接口定义 — Phase 2.2
- StaticNodes nullable vs 非空、MatchRuleId 语义、TemplateId vs node.name 等 P2 偏差 — Phase 3 前逐步处理

---

## 3. 分解方案：按修正类型分 4 个子 Phase

```
Phase 2.1a: 枚举修正（H-1 + 防御性值数守卫）
Phase 2.1b: 接口归属修正（H-5 + M-14 评估）
Phase 2.1c: 集合隔离修正（H-2 + cast-back 阻断）
Phase 2.1d: 行为补充修正（H-4/H-6-8/H-10/M-4/M-9 + 测试）
```

每个子 Phase 完成后 `dotnet build` + `dotnet test` 确认增量通过。

---

## 4. Phase 2.1a：枚举修正

### 4.1 H-1：TraversalState 移除 DynamicMatch

设计文档 §6.1 明确写：**"8 个状态（不是 9 个——DYNAMIC_MATCH 不是 FSM 状态，是 ChildrenStrategyType 值）"**。

当前 TraversalState 有 9 个成员（含 DynamicMatch），应移除。DynamicMatch 是 ChildrenStrategy 的值，已有 `ChildrenStrategy.DynamicMatch` 在 Graph 层定义。

**修正步骤**：
1. 从 `TraversalState` enum 移除 `DynamicMatch` 成员
2. grep 确认无代码引用 `TraversalState.DynamicMatch`（StepOrchestrator 步骤 9/10 通过 `ChildrenStrategy.DynamicMatch` 检查，不通过 TraversalState）
3. 如有引用，改为 `ChildrenStrategy.DynamicMatch`

### 4.2 防御性值数守卫

对设计文档中所有"值数锁定"的 enum 加断言测试，防止未来意外新增值：

| enum | 设计文档要求 | 断言值 |
|------|-------------|--------|
| TraversalState | §6.1: 8 值 | 8 |
| GlobalState | §6.2: 8 值 | 8 |
| NodeType | §4.2: 8 值 🔴火山级 | 8 |
| ErrorType | §6.3: 6 值 | 6 |
| ErrorStrategy | §6.3: 5 值 | 5 |
| PopupType | §6.4: 5 值 | 5 |
| DismissStrategy | §6.4: 4 值 | 4 |
| UrgencyLevel | §6.4: 4 值 | 4 |
| BlockingType | §6.4: 3 值 | 3 |
| FallbackAction | §6.2: 4 值 | 4 |

### 4.3 任务清单

| # | 任务 | 验证标准 |
|---|------|---------|
| 1 | 从 TraversalState enum 移除 DynamicMatch 成员 | `Enum.GetValues<TraversalState>().Length == 8` |
| 2 | grep 确认无代码引用 `TraversalState.DynamicMatch` | grep 返回 0 结果 |
| 3 | 如有引用，改为 `ChildrenStrategy.DynamicMatch` | 编译通过 |
| 4 | 添加 TraversalState 值数断言测试 | `Assert.Equal(8, Enum.GetValues<TraversalState>().Length)` |
| 5 | 添加 GlobalState 值数断言测试 | `Assert.Equal(8, ...)` |
| 6 | 添加 NodeType 值数断言测试 | `Assert.Equal(8, ...)` |
| 7 | 添加其余 7 个 enum 值数断言测试（ErrorType/ErrorStrategy/PopupType/DismissStrategy/UrgencyLevel/BlockingType/FallbackAction） | 全部断言通过 |
| 8 | `dotnet build` + `dotnet test` 确认增量通过 | 0 错误 + 全绿 |

---

## 5. Phase 2.1b：接口归属修正

### 5.1 H-5：ITraversalNode 移入 Graph 层

设计文档 §4.2 移了 NodeType enum 到 Domain，但未连带移 ITraversalNode 接口。ITraversalNode 当前在 `TraversalState.cs`（StateMachine 层），创建双向依赖：

```
Graph (TraversalNode.cs) → using StateMachine (引用 ITraversalNode)
StateMachine (TraversalState.cs) → using Graph.Models (引用 ChildrenStrategy)
```

**修正方案**（Review 报告 §2 H-5 推荐方案 B）：

将 ITraversalNode 和 IStackFrame 从 `TraversalState.cs` 移入 `Graph/Models/ITraversalNode.cs`，namespace 改为 `UniClaw.Core.Graph.Models`。

理由：
- ITraversalNode 的职责是描述遍历节点（Graph 概念），不是 FSM 状态
- NodeType 已在 Domain，ChildrenStrategy 已在 Graph — ITraversalNode 依赖的类型都在 Domain/Graph
- 移入 Graph 后，TraversalNode.cs 不再需要 `using StateMachine`

**需同步移动的接口**：
- `IStackFrame`（引用 ITraversalNode）— 同文件移动
- `INodeStack`（引用 IStackFrame）— 评估是否也应移入 Graph

**需评估的问题**：
- `INodeStack.Push(ITraversalNode)` — Push 方法参数引用 ITraversalNode，移动后 INodeStack 的依赖方向变化
- `ITraversalContext.NodeStack` 返回 `INodeStack` — ITraversalContext 在 StateMachine 层，INodeStack 如果移到 Graph 层，ITraversalContext 就要 `using Graph.Models`

**修正策略**：
- ITraversalNode 和 IStackFrame 移入 Graph.Models
- INodeStack **保留在 StateMachine 层**（因为它只被 ITraversalContext/TraversalRuntimeContext 使用，属于 FSM 上下文的一部分）
- INodeStack 对 ITraversalNode 的引用改为通过 `using Graph.Models`（单向依赖 StateMachine→Graph，而非双向）

**需更新引用的文件**：
- `TraversalNode.cs` — 移除 `using StateMachine`
- `NodeStack.cs` — 添加 `using Graph.Models`
- `StepContext.cs` — 评估 using 变化
- `StepOrchestrator.cs` — 评估 using 变化
- 测试文件 — 评估 using 变化

### 5.2 M-14 评估：GlobalState 在 ITraversalContext

设计文档/spec 说"TraversalFSM MUST NOT import GlobalFSM types"，但 ITraversalContext 上有 `GlobalState { get; set; }`。TraversalFSM 通过 ITraversalContext 使用 GlobalState，创建了类型级依赖。

**此任务仅做评估**，不做修改。评估结论写入文档，供 Phase 3 决策。

评估维度：
- GlobalState 移出 ITraversalContext 的影响范围
- 是否可将 GlobalState 改为 engine-only 属性（仅 TraversalRuntimeContext 有，ITraversalContext 没有）
- FSM 如何读取 GlobalState（通过 TraversalRuntimeContext 直接引用而非接口）

### 5.3 任务清单

| # | 任务 | 验证标准 |
|---|------|---------|
| 9 | 将 ITraversalNode 接口从 TraversalState.cs 移入新文件 `Graph/Models/ITraversalNode.cs`（namespace: Graph.Models） | ITraversalNode 在 `UniClaw.Core.Graph.Models` |
| 10 | 将 IStackFrame 接口从 TraversalState.cs 移入同文件 | IStackFrame 在 `UniClaw.Core.Graph.Models` |
| 11 | 更新 TraversalNode.cs：移除 `using UniClaw.Core.StateMachine` | TraversalNode.cs 无 StateMachine using |
| 12 | 更新 NodeStack.cs：添加 `using UniClaw.Core.Graph.Models`（引用 ITraversalNode/IStackFrame） | 编译通过 |
| 13 | 更新所有引用 ITraversalNode/IStackFrame 的文件的 using | grep 确认全部更新 |
| 14 | 确认 TraversalState.cs 不再包含 ITraversalNode/IStackFrame | 仅含 enum + ITraversalContext + ITraversalStateMachine + INodeStack + IGraphTraversalEngine |
| 15 | 检查 StateMachine 层对 Graph.Models 的 using（确认是单向而非双向） | Graph 层无 StateMachine using，StateMachine 层有 Graph using（单向） |
| 16 | 添加依赖方向断言测试（TraversalNode.cs 不应引用 StateMachine namespace） | 测试检查 using 列表 |
| 17 | 评估 M-14：产出 GlobalState 在 ITraversalContext 的评估结论文档 | 评估文档写入 docs/refactor/ |
| 18 | `dotnet build` + `dotnet test` 确认增量通过 | 0 错误 + 全绿 |

---

## 6. Phase 2.1c：集合隔离修正

### 6.1 H-2：VisitedChildren 嵌套 HashSet 引用泄露

设计文档 §4.3 要求"确保嵌套 IReadOnlySet 不泄露 HashSet 引用"，但当前实现 `dict[key] = set` 直接将 `HashSet<string>` 赋值为 `IReadOnlySet<string>`。

消费者可通过 cast-back `(HashSet<string>)visitedChildren["key"]` 修改引擎内部数据。

**修正方案**：实现 `ReadOnlySetWrapper<T>` — private struct/class 包装 `HashSet<T>`：

```csharp
// TraversalRuntimeContext.cs 内部
private sealed class ReadOnlySetWrapper : IReadOnlySet<string>
{
    private readonly HashSet<string> _set;
    public ReadOnlySetWrapper(HashSet<string> set) => _set = set;
    // 实现 IReadOnlySet<string> 所有成员，委托到 _set
    public int Count => _set.Count;
    public bool Contains(string item) => _set.Contains(item);
    // ... 其他 IReadOnlySet 成员
}
```

关键：`ReadOnlySetWrapper` 是 `sealed class`，不继承 `HashSet<string>`，cast-back `(HashSet<string>)wrapper` 会返回 null/抛异常。

### 6.2 VisitedPages/VisitedNodes 评估

设计文档 §4.3 说"IReadOnlySet<string> 直接暴露 HashSet<string> 是安全的（不暴露修改方法）"。但 H-2 的修正逻辑表明 cast-back 级也不安全。

**此任务做评估**：是否需要同样用 ReadOnlySetWrapper 包装 VisitedPages/VisitedNodes？

评估结论：
- 如果消费者全是引擎内部代码 → cast-back 风险可控，可接受直接暴露
- 如果消费者包含 AI advisor 等外部代码 → 应包装，防 cast-back
- 当前阶段 AI advisor 使用 TraversalContextSnapshot（ImmutableHashSet，完全安全），不使用 ITraversalContext 的实时视图
- **结论**：VisitedPages/VisitedNodes 的直接暴露在当前阶段可接受，但应加注释标注安全级别

### 6.3 任务清单

| # | 任务 | 验证标准 |
|---|------|---------|
| 19 | 实现 `ReadOnlySetWrapper<T>`（TraversalRuntimeContext.cs 内 private sealed class） | 包装 HashSet 为 IReadOnlySet，cast-back 失败 |
| 20 | 修改 `GetVisitedChildrenReadOnly()`：嵌套 HashSet 用 ReadOnlySetWrapper 包装 | `ITraversalContext.VisitedChildren["key"]` 的 runtime type 是 ReadOnlySetWrapper |
| 21 | 添加 VisitedChildren cast-back 阻断测试 | `(HashSet<string>)wrapper` 返回 null 或抛 InvalidCastException |
| 22 | 添加 VisitedChildren 修改隔离测试：通过 ITraversalContext 无法修改内部数据 | 通过接口修改不反映到引擎内部 |
| 23 | 评估 VisitedPages/VisitedNodes 直接 HashSet 暴露是否需同样包装 → 加注释标注安全级别 | 注释标注 "接口级安全，cast-back 级需 Phase 3 改进" |
| 24 | 确认 TraversalContextSnapshot 创建后引擎修改不影响快照（已有测试，确认覆盖） | dotnet test 通过 |
| 25 | `dotnet build` + `dotnet test` 确认增量通过 | 0 错误 + 全绿 |

---

## 7. Phase 2.1d：行为补充修正

### 7.1 H-4：PlanCompiler scope 合法性校验

设计文档 §5.2 要求 `_validate_slots(slots)` 校验"target_app、scope/target 组合、depth 合法性"。当前代码只校验了 target_app 非空和 depth ≥ 0，**漏掉了 scope/target 组合合法性**。

非法 scope 值传入时 BuildDynamicRules 返回 null，静默失败而非 fail-fast（DomainValidationException）。

**修正**：在 `_validate_slots` 中添加 scope 合法性校验：
- scope 必须是 TEMPLATE_SETS 中存在的键（"full_interaction"/"menu_only"/"safe_mode"/"read_only"/"target_path"）
- 如果 scope 是 "target_path"，target 字段必须非空
- 否则抛 DomainValidationException

### 7.2 H-6/H-7：StateRestorer 保存完整 stack + 恢复全部字段

当前 StateRestorer：
- **preserve_state**: 保存 `NodeStackDepth`（int），而非完整 stack 内容
- **restore_state**: 仅恢复 GlobalState 和 LastError，不恢复 CurrentFrame、NodeStack、ExecutionResult

设计文档 §6.4 要求"保存/恢复遍历上下文（current_node_id, **node_stack**, current_state, execution_result, timestamp）"。

**修正**：
- PreservedState 改为保存完整 NodeStack 内容（List<StackFrame> 或 equivalent）
- RestoreState 恢复所有 5 个字段：CurrentFrame、NodeStack、GlobalState、LastError、ExecutionResult
- ValidateRestoredState 比对恢复值与保存值（而非仅做结构检查）

### 7.3 H-8：PopupHandler 顶层 try-catch 兜底

当前 HandlePopup 方法无顶层异常兜底。只有 PopupActionExecutor（步骤 4）有内部异常处理。如果 detect/classify/preserve/restore/validate 步骤抛异常，异常直接传播到调用者。

设计文档 §6.4 要求"Hook Dispatch 表异常兜底到最安全操作（back）"。

**修正**：在 HandlePopup 方法外层加 try-catch，异常兜底返回 `new PopupHandlingResult(false, "back_fallback", "Unhandled exception during popup handling")`。

### 7.4 H-10：PageSnapshotManager 确定性哈希

当前 Fingerprint 用 `string.GetHashCode()`，.NET 中 GetHashCode() 跨进程非确定性（每次运行随机化 hash 种子）。

设计文档 §7.5/spec 要求"deterministic"。

**修正**：改用确定性哈希算法：
- 方案 A：逐字符累加哈希（`hash = hash * 31 + (int)ch`）
- 方案 B：用 `System.Security.Cryptography.SHA256` 简化版
- 推荐方案 A — 简单、快速、确定性、无外部依赖

### 7.5 M-4：Log-and-Continue 补充日志

当前 TraceCoordinator 的 `LogAndContinue` catch block 为空注释 `/* Log warning, do NOT propagate */`，异常被静默吞掉。

设计文档 §7.3/spec 要求"failures logged as warning"。

**修正**：catch block 加 `Console.WriteLine($"[TraceCoordinator Warning] {ex.GetType().Name}: {ex.Message}")`（Phase 2 无 ILogger 注入，Console.WriteLine 是最简方案）。

### 7.6 M-9：DynamicMatcher text_pattern Exact 模式

设计文档 §5.3 要求"支持 Exact 和 Contains 模式"。当前仅实现 Contains。

**修正**：
- MatchCondition 添加 `TextMatchMode` 字段（enum: Exact/Contains，默认 Contains）
- DynamicMatcher.match 检查 TextMatchMode，Exact 用字符串相等，Contains 用 substring
- 保持向后兼容：TextMatchMode 默认 Contains

### 7.7 任务清单

| # | 任务 | 验证标准 |
|---|------|---------|
| 26 | H-4: PlanCompiler._validate_slots 补充 scope 合法性校验 | 非法 scope 抛 DomainValidationException |
| 27 | M-9: DynamicMatcher MatchCondition 添加 TextMatchMode 字段 + Exact/Contains 逻辑 | Exact 模式测试通过 |
| 28 | M-4: TraceCoordinator.LogAndContinue catch block 加 Console.WriteLine | catch block 不再空吞 |
| 29 | H-10: PageSnapshotManager.Fingerprint 改用逐字符确定性哈希 | 相同输入跨调用哈希值一致 |
| 30 | H-6/H-7: PreservedState 保存完整 NodeStack 内容 + RestoreState 恢复全部 5 字段 + ValidateRestoredState 比对保存值 | 保存/恢复/比对全部通过 |
| 31 | H-8: PopupHandler.HandlePopup 外层加 try-catch 兜底到 back | 步骤 1-6 任何异常 → 返回 back_fallback |
| 32 | 添加 PlanCompiler scope 校验测试 | 非法 scope 抛异常 |
| 33 | 添加 DynamicMatcher Exact 模式测试 | Exact 精确匹配 + Contains 子串匹配 |
| 34 | 添加 PageSnapshotManager 确定性哈希测试 | 相同输入两次调用结果一致 |
| 35 | 添加 StateRestorer 完整保存/恢复/比对测试 | 保存后恢复全部字段，比对通过 |
| 36 | 添加 PopupHandler 顶层异常兜底测试 | 任何步骤异常 → back_fallback |
| 37 | `dotnet build` + `dotnet test` 确认全部通过 | 0 错误 + 全绿 |

---

## 8. 验证标准

| # | 标准 | 验证方式 |
|---|------|----------|
| AC-1 | `dotnet build` 0 错误 | CI |
| AC-2 | `dotnet test` 增量通过 | CI |
| AC-3 | TraversalState 恰好 8 值（不含 DynamicMatch） | `Enum.GetValues<TraversalState>().Length == 8` |
| AC-4 | ITraversalNode 在 Graph.Models namespace（不在 StateMachine） | grep 确认 |
| AC-5 | TraversalNode.cs 无 `using UniClaw.Core.StateMachine` | grep 确认 |
| AC-6 | VisitedChildren 嵌套集合 cast-back 阻断 | 测试断言 |
| AC-7 | PageSnapshotManager.Fingerprint 跨调用确定性 | 测试断言 |
| AC-8 | PlanCompiler 非法 scope 抛 DomainValidationException | 测试断言 |
| AC-9 | StateRestorer 保存完整 stack + 恢复全部字段 + 比对 | 测试断言 |
| AC-10 | PopupHandler 顶层异常兜底到 back | 测试断言 |
| AC-11 | 全部 10 个 enum 值数守卫断言 | 测试断言 |

---

## 9. 延续到 Phase 2.2 的偏差

以下偏差不在 Phase 2.1 范围内，延到 Phase 2.2 处理：

| # | 偏差 | 原因 |
|---|------|------|
| H-9 | TraceCoordinator 15/16 空 lambda | 实现量大（15 个 span 方法），独立 Phase 更合适 |
| H-11 | EntryPolicyExecutor fast/polling 等待模式 | 需 async/await 模式，独立 Phase 更合适 |
| H-3 | ULID 同一毫秒单调排序 | 需加计数器机制，非核心引擎偏差 |
| M-8 | IDynamicMatcher 接口缺失 | spec 增量要求，可与 Phase 2.2 接口抽象一起做 |

---

## 10. 决策日志

| # | 决策 | 理由 |
|---|------|------|
| D-1 | Phase 2.1 按修正类型分 4 子 Phase | 同类型修正互不干扰，每步 dotnet test 验证增量 |
| D-2 | ITraversalNode 移入 Graph.Models（方案 B） | 职责归属：节点描述是 Graph 概念而非 FSM 状态 |
| D-3 | INodeStack 保留在 StateMachine 层 | INodeStack 是 FSM 上下文的一部分，被 ITraversalContext 引用 |
| D-4 | VisitedPages/VisitedNodes 保持直接 HashSet 暴露 + 注释标注 | 当前消费者全是引擎内部，cast-back 风险可控；Phase 3 改进 |
| D-5 | ReadOnlySetWrapper 用 private sealed class（非 public） | 仅 TraversalRuntimeContext 内部使用，不暴露到外部 |
| D-6 | M-14 仅评估不改 | GlobalState 移出 ITraversalContext 影响大量消费者，风险高 |
| D-7 | Log-and-Continue 用 Console.WriteLine（非 ILogger） | Phase 2 无 DI 注入体系，ILogger 需 Phase 3 引入 |
| D-8 | text_pattern TextMatchMode 默认 Contains | 向后兼容，Python 默认也是 substring 匹配 |
