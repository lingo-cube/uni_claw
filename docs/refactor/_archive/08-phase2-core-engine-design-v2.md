# Phase 2 Design：核心引擎搬运 + 架构修正

> **版本**: 2.0
> **日期**: 2026-07-03
> **分支**: `feature/refactor`
> **前置**: Phase 1 + Phase 1.1（已完成，Domain 层 185 测试全绿）
> **验证基准**: Python `main` 分支源码逐行对齐
> **修订说明**: v2.0 基于 Python 架构参考文档（07-phase2-python-architecture-reference.md）的源码验证结果，修正 5 处设计决策（见 §9 决策日志）

---

## 1. 背景

Phase 1/1.1 完成了 Domain 层（Vision 7 类型 + Content 12 类型 + Common 3 类型 + Mappings 1 类型 + DomainValidationException），185 测试全绿。当前 C# 非 Domain 层有 9 个文件定义了 30+ 类型，但只有接口骨架和少量工具类实现（PlaceholderResolver、TemplateValidator、NodeStack），零引擎逻辑，零非 Domain 测试。

Python `main` 分支有 11 个非 Domain 模块待搬运。Phase 2 聚焦核心引擎（Graph + StateMachine + Traversal），不做外围模块（ADB、AI providers、config、safety、simulation、analysis）。

---

## 2. 目标 / 不目标

**目标**:
- 修正架构问题（F-1 统一 PageAnalysis、NodeType 移入 Domain、依赖方向修正）
- 搬运 Graph 基础层（TraversalNode/Plan 补全 + PlanCompiler + DynamicMatcher + TemplateInstantiator）
- 搬运 Trace 基础层（TraceRecorder + TraversalRuntimeContext）
- 搬运 State Machine 核心（TraversalFSM + ContainerHandler + ErrorHandler + PopupHandler）
- 搬运 Traversal Engine 子系统（StepOrchestrator + DynamicChildManager + TraceCoordinator + EntryPolicyExecutor + PageCacheManager + PageSnapshotManager）
- 每个子系统配单元测试，`dotnet test` 增量通过
- Python 源码为搬运基准，逐行对齐关键逻辑

**不目标**:
- 端到端集成仿真测试（Phase 3）
- AI provider 实现（UniBrain、Claude/DeepSeek/Mimo MCP 路由）
- ADB 层搬运（RealADBClient / MockADBClient）
- Config 层搬运（Settings / appsettings.json）
- Safety 模块搬运（SafetyFilter）
- Simulation 模块搬运（SimulationRunner / YAML fixture）
- Analysis 模块搬运（HTTP dashboard / Metrics aggregation）
- JSON → TraversalPlan 反序列化完整管道

---

## 3. 分解方案：架构先行（方案 A）

```
Phase 2.0: 架构修正 + Trace 基础层
Phase 2.1: Graph 基础层 + PlanCompiler + DynamicMatcher
Phase 2.2: State Machine 核心
Phase 2.3: Traversal Engine 子系统
```

每个子 Phase 完成后 `dotnet test` 确认增量通过。

---

## 4. Phase 2.0：架构修正 + Trace 基础层

### 4.1 F-1：统一 PageAnalysis/PopupInfo

删除 `AI/IAIStrategyAdvisor.cs` 内的 AI 简化版 `PageAnalysis`（3 字段：FlattenedScreen, Path, PopupInfo?）和 `PopupInfo`（3 字段：Detected, CloseButton?, Message?）。`IAIStrategyAdvisor` 5 个方法签名改为引用 Domain 版 `PageAnalysis`（`UniClaw.Core.Domain.Models.Content.PageAnalysis`，12 字段）。

**难点**：Domain 版 PageAnalysis 需要 Level1Dir/Level1Menus/Items 等 12 个字段，但 AI 层只产出 FlattenedScreen。解决方案：Phase 2.0 先改签名，构造 PageAnalysis 的具体逻辑留给 Phase 2.2/2.3（需要引擎遍历上下文才能填充 CurrentPath、Items 等）。

### 4.2 NodeType 移入 Domain

将 `NodeType` enum（8 值：Container, LeafSwitch, LeafSlider, LeafAction, LeafInfo, Screen, Action, Target）从 `StateMachine/TraversalState.cs` 移到 `Domain/Models/Content/EnumsAndCoordinate.cs`，与 MenuItemType/ExpectedAction/Direction 同层。添加 `[JsonPropertyName]` 属性和 `NodeTypeExtensions`（Values/FromValue/IsValid，反射模式）。

**理由**：NodeType 是数据枚举（描述节点类型），不是 FSM 状态。Graph/AI/Traversal 引用 NodeType 时不应被迫依赖 StateMachine 层。

**引用方改动**：6 处 `using UniClaw.Core.StateMachine` → `using UniClaw.Core.Domain.Models.Content`。

### 4.3 依赖方向修正

| 问题 | 修正 |
|------|------|
| `NodeData` 在 AI 层定义但语义属于 Graph | `NodeData` record 移到 Graph 层 |
| `ContainerInference` / `PageTypeVerification` 等在 AI 层定义但不含 Domain 依赖 | 保留在 AI 层（纯 AI 决策结果类型，无跨层问题） |
| 两个运行上下文系统 | Python 有 `TraversalContext`（AI 用，不可变）和 `TraversalRuntimeContext`（引擎用，可变）。C# 统一为 **`TraversalRuntimeContext`（sealed class + ITraversalContext 只读接口）**。引擎内部用 mutable 字段直接赋值（与 Python dataclass 行为一致），接口暴露 `IReadOnlySet<string>` / `IReadOnlyList<string>` / `IReadOnlyDictionary<string, IReadOnlySet<string>>` 只读视图。AI advisor 通过 `CreateReadOnlySnapshot()` 获得 `TraversalContextSnapshot`（sealed record class，只含 AI 所需 8 字段的不可变快照），与 Python `TraversalContext.to_readonly()` 对齐 |
| `exception → adb` 依赖方向反了 | Phase 2 不搬运 exception 模块（非核心引擎），标注方向给后续 Phase |

### 4.4 Trace 基础层搬运

搬运 Python `trace/models.py` + `trace/recorder.py` + `trace/context.py` 的核心类型：

**TraceNode 层级**（4 类型）：
- `TraceNode`（base：span_id, parent_span_id, timestamp, metadata）
- `SessionNode`（extends TraceNode：session_id, device_info, app_info, status）
- `StepNode`（extends TraceNode：step_type, node_id, action, result）
- `SpanNode`（extends TraceNode：span_type, duration_ms, status）

**ITraceRecorder**（C# 已有骨架，补完整方法签名）：
- Session lifecycle：StartSessionAsync / EndSessionAsync
- Span recording：RecordTransitionAsync / RecordAICallAsync / RecordExecutionAsync / RecordErrorAsync
- Query：GetTransitionsAsync / GetAICallsAsync / GetExecutionsAsync / GetErrorsAsync / ExportTraceAsync

**TraversalRuntimeContext**（引擎共享状态容器，**sealed class + ITraversalContext 只读接口**）：

26 个 mutable 内部字段（对齐 Python `src/trace/context.py`）：
- trace_id, node_stack (List<StackFrame>), current_path, current_page_analysis, current_fingerprint, cache_valid
- visited_pages, visited_level1_menus, visited_level2_menus, visited_nodes, visited_children (Dict<string, Set<string>>)
- page_tree, action_history (keep last 5), failed_nodes, consecutive_errors, max_depth, step_count, retry_count
- completion_policy, device_experience, global_state, last_error, exception_chain, ai_provider
- page_cache, wait_after_action_ms

ITraversalContext 只读接口（强类型集合）：

| 属性 | 接口类型 | 内部实现类型 |
|------|----------|-------------|
| NodeStack | `INodeStack` | `NodeStack`（mutable class） |
| CurrentPath | `IReadOnlyList<string>` | `List<string>` |
| VisitedPages | `IReadOnlySet<string>` | `HashSet<string>` |
| VisitedChildren | `IReadOnlyDictionary<string, IReadOnlySet<string>>` | `Dictionary<string, HashSet<string>>` |
| CurrentFrame | `ITraversalNode? { get; set; }` | mutable setter |
| VisitedNodes | `IReadOnlySet<string>` | `HashSet<string>` |
| StepCount | `int { get; }` | `int`（引擎内部方法更新） |
| GlobalState | `GlobalState { get; set; }` | mutable setter |
| LastError | `Exception? { get; set; }` | mutable setter |

**⚠️ 只读视图隔离要求**：ITraversalContext 接口暴露的只读视图**不得泄露内部可变引用**。具体规则：
- `IReadOnlyList<string>` (CurrentPath) — 通过 `.AsReadOnly()` 包装 `List<string>`，确保返回的是只读包装而非原始 List 引用
- `IReadOnlySet<string>` (VisitedPages/VisitedNodes) — 直接暴露 `HashSet<string>` 是安全的（`IReadOnlySet<T>` 不暴露修改方法），但需验证调用方不能通过强制转换回 `HashSet<string>` 修改。如果需要更强隔离，可考虑返回 `ImmutableHashSet<string>.Builder` 的冻结副本
- `IReadOnlyDictionary<string, IReadOnlySet<string>>` (VisitedChildren) — 内部 `Dictionary<string, HashSet<string>>` 的只读包装，需确保嵌套的 `IReadOnlySet<string>` 同样不泄露 `HashSet<string>` 引用
- `INodeStack` (NodeStack) — NodeStack 是 mutable class（Push/Pop/Clear），ITraversalContext 消费者不应调用修改方法。**实现时需评估是否为 AI advisor 的 TraversalContextSnapshot 提供独立的 INodeStack 只读包装（只允许 Peek/IsEmpty/Depth，禁止 Push/Pop/Clear）**，或者 Snapshot 中 NodeIds 字段已足够替代

引擎内部修改方法（不在 ITraversalContext 接口上）：
- `AppendPath(string page)` / `PopPath()`
- `MarkVisited(string page)` / `MarkNodeVisited(string nodeId)`
- `IncrementStepCount()` / `IncrementRetryCount()` / `IncrementConsecutiveErrors()`
- `ResetConsecutiveErrors()`

`CreateReadOnlySnapshot()` → 返回 `TraversalContextSnapshot`（sealed record class，只含 AI 所需 8 字段）：
- NodeIds (ImmutableArray<string>)
- CurrentPath (ImmutableArray<string>)
- VisitedPages (ImmutableHashSet<string>)
- VisitedNodes (ImmutableHashSet<string>)
- MaxDepth (int)
- StepCount (int)
- ActionHistory (ImmutableArray<ActionRecord>)
- FailedNodes (ImmutableDictionary<string, ErrorRecord>)

**预留扩展位**（TODO 注释，Phase 3 实现）：
- `IScrollHandler? ScrollHandler { get; }` — 滚动策略接口预留
- `IPageSnapshot? CurrentSnapshot { get; }` — 屏幕快照预留

**不搬运**：`storage.py`（Memory/File storage）、`metrics.py`（StepMetrics/SessionMetrics）、`recovery.py`（TraceRecovery）、`analyzer.py`（build_tree/TraceAnalyzer）——Phase 3 内容。

---

## 5. Phase 2.1：Graph 基础层

### 5.1 TraversalNode / TraversalPlan 补全

**TraversalNode**（当前 C# 骨架需补全，对齐 Python 8 字段）：

| 字段 | Python | C# 当前 | 需补 |
|------|--------|---------|------|
| node_id | str | ✅ string | — |
| name | str | ✅ string | — |
| node_type | NodeType | ✅ NodeType | — (移入 Domain 后引用变更) |
| operation | Operation | ✅ Domain.Common.Operation | — |
| precondition | Optional[Precondition] | ✅ Precondition? | — |
| children_strategy | ChildrenStrategy | ✅ ChildrenStrategy | — |
| error_policy | Optional[ErrorPolicy] | ✅ ErrorPolicy? | — |
| exit_condition | Optional[ExitCondition] | ✅ ExitCondition? | — |
| meta | Dict[str, Any] | ✅ Dictionary? | — |

注意：Python TraversalNode **没有独立的 target/restore 字段**——它们内嵌在 Operation.target 和 Operation.restore 中。C# Domain 版 Operation 已有 Target 和 RestoreAction。

**TraversalPlan**（当前 C# 骨架缺 6 字段）：

| 字段 | Python | C# 当前 | 需补 |
|------|--------|---------|------|
| entry_app | str (required) | ❌ | 补 |
| plan_name | str | ❌ | 补 |
| plan_id | str | ❌ | 补 |
| entry_policy | EntryPolicy | ✅ EntryPolicy | — |
| entry_config | Optional[EntryConfig] (V6.8) | ❌ | 补 |
| root_node | Optional[TraversalNode] | ✅ RootNode? | — |
| static_nodes | Dict[str, TraversalNode] | ✅ StaticNodes? | 改名 nodes→static_nodes |
| template_registry | Optional[str] | ✅ TemplateRegistry? | — |
| mode | TraversalMode | ✅ TraversalMode | — |
| completion_policy | CompletionPolicy | ✅ CompletionPolicy | — |
| intent_slots | Optional[IntentSlots] | ✅ IntentSlots? | — |
| meta | Dict[str, Any] | ✅ Dictionary? | — |

**EntryConfig**（V6.8 新增，需搬运）：
- wait_mode: "fast" | "polling"
- wait_timeout_seconds: float
- wait_interval_ms: int
- action_delay_ms: int
- trace_level: "none" | "basic" | "detailed" | "full"

### 5.2 PlanCompiler

Python `src/graph/compiler.py` 的 PlanCompiler 是确定性映射（IntentSlots → TraversalPlan），不依赖 AI。

**TEMPLATE_SETS**（4 值）：
- "full_interaction": ["menu_container", "switch_leaf", "slider_leaf", "leaf_action"]
- "menu_only": ["menu_container"]
- "safe_mode": ["menu_container", "switch_leaf", "slider_leaf", "leaf_action"]
- "read_only": ["leaf_info"]

**compile() 方法结构**：
1. `_validate_slots(slots)` — 校验 target_app、scope/target 组合、depth 合法性
2. `_build_entry_policy(slots)` — 创建默认 EntryPolicy
3. `_build_root_node(slots)` — 根据 scope 决定 ChildrenStrategy（target_path→STATIC，否则→DYNAMIC_MATCH），创建 root TraversalNode
4. `_build_completion_policy(slots)` — 映射 completion override 或 scope 到 CompletionPolicy type
5. 组装 TraversalPlan
6. 如果 scope 是 target_path，调用 `_build_static_nodes()` 构建静态路径

**Match conditions per template**：
- menu_container → {"type": "menu_item"}
- switch_leaf → {"type": "switch"}
- slider_leaf → {"type": "slider"}
- leaf_action → {"type": "button"}
- leaf_info → {} (match anything)

### 5.3 DynamicMatcher

Python `src/graph/matcher.py` 的 DynamicMatcher 匹配页面对象与 DynamicRule。

**MatchCondition 字段**：type, expected_action, text_pattern, min_index, max_index, custom

**匹配逻辑**：
- MenuItemType 匹配（type 字段）
- ExpectedAction 匹配（expected_action 字段）
- 文本模式匹配（text_pattern，支持 Exact 和 Contains 模式）
- 索引范围匹配（min_index / max_index）
- 自定义条件匹配（custom dict）

**MatchResult**：matched(bool), match_rule_id, matched_item, action(GenerateChild/Skip/ExecuteInline)

### 5.4 TemplateInstantiator

当前 C# PlaceholderResolver/TemplateValidator 已有完整实现。需补 TemplateInstantiator：

Python `TemplateInstantiator.instantiate()` 流程：
1. 解析占位符（PlaceholderResolver）
2. 构造 Operation（_create_operation）
3. 构造 Precondition（_create_precondition）
4. 构造 ChildrenStrategy（_create_children_strategy）
5. 构造 ErrorPolicy（_create_error_policy）
6. 组装 TraversalNode
7. V6.9 path concatenation（precondition.path = parent_path + [node.name]）

### 5.5 单元测试

- TraversalNode/TraversalPlan 构造验证 + 字段完整性
- TraversalPlan 缺失 entry_app 时 DomainValidationException
- PlanCompiler：6 种 element_handling 场景
- DynamicMatcher：MatchCondition 匹配/不匹配/多条件组合
- TemplateInstantiator：占位符解析 → TraversalNode 构造

---

## 6. Phase 2.2：State Machine 核心

### 6.1 TraversalFSM

Python `TraversalStateMachine` 有 **8 个状态**（不是 9 个——DYNAMIC_MATCH 不是 FSM 状态，是 ChildrenStrategyType 值）：

```
NODE_SELECT        → {PRECONDITION_CHECK, BRANCH}
PRECONDITION_CHECK → {EXECUTE, ERROR_HANDLING}
EXECUTE            → {RESULT_VERIFY, BRANCH, ERROR_HANDLING}
RESULT_VERIFY      → {BRANCH, POPUP_HANDLING}
BRANCH             → {NODE_SELECT, PRECONDITION_CHECK, FRAME_COMPLETE, ERROR_HANDLING}
FRAME_COMPLETE     → {NODE_SELECT, ERROR_HANDLING}
ERROR_HANDLING     → {NODE_SELECT, EXECUTE, FRAME_COMPLETE, BRANCH}
POPUP_HANDLING     → {RESULT_VERIFY, ERROR_HANDLING}
```

> **设计决策 D-1**：Python `VALID_TRANSITIONS` 定义了 `PRECONDITION_CHECK → {EXECUTE, BRANCH, ERROR_HANDLING}`，但 V6.7 `_handle_precondition_check` handler **从不返回 BRANCH**（只返回 EXECUTE 或 ERROR_HANDLING）。`precondition_failed()` 便利方法是 dead code，C# 不移植。C# 转换矩阵移除此死路径。如果未来需要"前置条件跳过"语义，应通过 ERROR_HANDLING 管道处理（V6.7 已验证此路径）。

**step() 方法**：try-catch wrapper，内层按 from_state 分发到 handler 方法。handler 返回下一 TraversalState。异常时路由到 ERROR_HANDLING。

**C# 实现策略**：enum-based switch（与 Python if/elif 链对应）。Phase 1 已验证 enum + construction-throw 在 Domain 层可行。

### 6.2 ContainerHandler

Python `ContainerHandler` 由 3 个子组件组成：

**CompletionDetector.detect_completion()**（优先级链，**纯计算，无缓存**）：
1. Timeout exceeded → is_complete=True, reason=TIMEOUT, suggested_action=BACK, should_backtrack=True
2. Max depth reached → is_complete=True, reason=MAX_DEPTH, suggested_action=BACK, should_backtrack=True
3. No children → is_complete=True, reason=ALL_VISITED, suggested_action=BACK
4. All children visited → is_complete=True, reason=ALL_VISITED, suggested_action=由 exit_condition 决定
5. Still processing → is_complete=False, reason=INCOMPLETE

**FallbackDecider**（**纯计算，无缓存**）：Timeout/depth→always BACK, Complete→use suggested_action, Incomplete→SKIP

> **设计决策 D-3**：Python 的 CompletionDetector 和 FallbackDecider 各有缓存，但缓存键包含每步变化的值（visited_children 是 List[str]，每步增长；depth 也可能变化），缓存命中率极低。C# 不做缓存——这些组件计算量很小（优先级链 + 条件判断），每步重新计算的开销可忽略。不移植 Python 的无效缓存设计。

**ContainerActionExecutor**：映射 action 到 hook（Hook Dispatch 表模式）：
- BACK → press_back + pop_frame + restore_parent
- AUTO_ESCAPE → try_sibling_menu + fallback_to_back + pop_frame
- SKIP → skip_remaining + pop_frame + mark_complete
- ABORT → abort + stop_traversal + cleanup

Hook Dispatch 表用 `Dictionary<FallbackAction, Func<ContainerContext, ContainerActionResult>>` 实现，异常兜底到最安全的默认操作（BACK）。

**统计**：processed_count, completed_count, action_statistics, avg_depth, completion_rate

### 6.3 ErrorHandler

Python `ErrorHandler` 由 4 个子组件组成（不是简单的 retry→skip→abort 链）：

**ErrorClassifier**：pattern-matching 分类异常 → ErrorType enum（NETWORK/UI_ELEMENT/TIMEOUT/PERMISSION/APP_CRASH/UNKNOWN）

**ErrorStrategySelector**：按 ErrorType 选择策略优先链：
- NETWORK → [RETRY, BACKTRACK, ABORT]
- UI_ELEMENT → [SKIP, RETRY, BACKTRACK]
- TIMEOUT → [RETRY, CONTINUE, BACKTRACK]
- PERMISSION → [ABORT, BACKTRACK]
- APP_CRASH → [ABORT]
- UNKNOWN → [CONTINUE, SKIP, ABORT]

**策略适用性检查**：
- RETRY：retry_count < max_retries
- BACKTRACK：can_backtrack AND node_stack_length > 1
- SKIP：can_skip
- CONTINUE：always
- ABORT：always

**RecoveryExecutor**：
- RETRY：指数退避（min(2^retry_count, 10) 秒）
- BACKTRACK：pop container from stack, return to parent
- SKIP：mark current node as skipped
- CONTINUE：proceed despite error
- ABORT：terminate traversal

Hook Dispatch 表：`Dictionary<ErrorStrategy, Func<ErrorContext, ErrorRecoveryResult>>`，异常兜底到 ABORT。

**ErrorHandler**：orchestrator chaining classifier → selector → executor。维护统计（total_errors, recovered_count, error_statistics）。

### 6.4 PopupHandler

Python `PopupHandler` 由 4 个子组件 + StateRestorer 组成（5 步流程：Detect → Classify → Preserve → Handle → Restore）：

**PopupDetector**：regex pattern matching 检测弹窗
**PopupClassifier**：确定弹窗类型和关闭策略

**PopupType enum**：PERMISSION / ERROR / AD / DIALOG / UNKNOWN
**UrgencyLevel enum**：LOW / MEDIUM / HIGH / CRITICAL
**BlockingType enum**：MODAL / NON_MODAL / TOAST

**Dismiss strategies**：
- auto_close：find and click dismiss button
- back：press back key
- wait_timeout：wait for popup to expire
- auto_close_or_back：try auto_close, fallback to back

**Dismiss button priorities by type**：
- PERMISSION: ["allow", "accept", "continue", "grant", "ok"]
- ERROR: ["ok", "close", "dismiss", "acknowledge"]
- AD: ["close", "skip", "x", "dismiss"]
- DIALOG: ["ok", "cancel", "close", "yes", "no"]

**StateRestorer**：保存/恢复遍历上下文（current_node_id, node_stack, current_state, execution_result, timestamp）——确保弹窗处理后遍历连续性。恢复后验证状态一致性，失败则标记 handling_result 为 failed。

**PopupHandler.handle_popup() 流程**：
1. detector.detect_popup(screen_info) — if no popup, return early
2. classifier.classify_popup(screen_info) — get PopupInfo
3. restorer.preserve_state(context) — save state
4. action_handler.handle_popup(popup_info, context) — execute dismiss
5. restorer.restore_state(state_id, context) — restore state
6. restorer.validate_restored_state(context) — verify

Hook Dispatch 表：`Dictionary<PopupType, Func<PopupContext, PopupHandlingResult>>`，异常兜底到最安全操作（back）。

**统计**：detected_count, handled_count, handling_statistics, handling_rate

### 6.5 单元测试

- TraversalFSM：转换矩阵对齐修正定义 × 禁止转换校验（PRECONDITION_CHECK → BRANCH 应拒绝）
- CompletionDetector：timeout / max_depth / all_visited / no_children / incomplete 5 种场景（纯计算，无缓存）
- FallbackDecider：5 种 completion_reason × 决策规则（纯计算，无缓存）
- ContainerActionExecutor：4 个 FallbackAction hook + 异常兜底到 BACK
- ErrorHandler：6 种 ErrorType × 策略优先链 + 适用性检查
- PopupHandler：5 种 PopupType × dismiss 策略 + StateRestorer 保存/恢复/验证
- TraversalRuntimeContext：字段更新 + CreateReadOnlySnapshot 隔离验证（快照创建后引擎修改不影响快照）

---

## 7. Phase 2.3：Traversal Engine 子系统

### 7.1 StepOrchestrator

Python `StepOrchestrator` 执行一个 FSM step 的完整流程。

**StepContext**（值对象，封装 step 所有依赖）：
- context (TraversalRuntimeContext) — sealed class, 引擎内部直接赋值更新
- state_machine (TraversalStateMachine)
- vision / action / child_mgr / node_registry / trace
- last_known_path / last_recorded_path / last_recorded_action
- snapshot_mgr / stack

**execute_step(ctx) 流程**（14 步）：
1. 创建 NodeStackAdapter（从 context + node_registry）
2. 记录 step start（trace）
3. 调用 state_machine.step(stack, context, vision, action)
4. 记录 page snapshot（path 变化时）
5. 记录 action execution（从 handler metrics）
6. 记录 metrics spans
7. 记录 state transition
8. **BRANCH 处理**：from EXECUTE/RESULT_VERIFY/NODE_SELECT 时，获取 next unvisited child（via child_mgr），有则 push，无则 force frame completion
9. **NODE_SELECT + DYNAMIC_MATCH**：如果当前节点用 DYNAMIC_MATCH strategy，获取 next child，无则执行 back + pop stack（避免 BRANCH→NODE_SELECT 循环）
10. **FRAME_COMPLETE 拦截**：如果 transitioning to FRAME_COMPLETE 但当前节点是 DYNAMIC_MATCH 有 remaining unvisited children，override → push remaining child
11. 确定下一状态（should_complete_frame / child_pushed override）
12. 更新 visited_nodes
13. Path 变化检测 + cache invalidation（调用 child_mgr.invalidate()）
14. 记录 step end（trace）

> **修正说明**：Step 8 BRANCH 拦截来源从"EXECUTE/RESULT_VERIFY/PRECONDITION_CHECK/NODE_SELECT"改为"EXECUTE/RESULT_VERIFY/NODE_SELECT"。PRECONDITION_CHECK 不再转换到 BRANCH，因此不会作为 BRANCH 的来源状态。

### 7.2 DynamicChildManager

**关键行为**：
- `get_next_unvisited_child(node, context)` — STATIC: iterate static_children；DYNAMIC_MATCH: generate if not cached, then iterate cached
- `generate(node, context)` — 核心生成管道：
  1. Compute page fingerprint (PageSnapshotManager.fingerprint())
  2. Convert DynamicRules → matcher rules
  3. Extract items from context.current_page_analysis
  4. Call DynamicMatcher.match_all()
  5. For GENERATE_CHILD action: instantiate child nodes
  6. **Dedup**：`(page_fingerprint, child.name)` pair stored in `_generated_pairs` set
  7. Set child.precondition.path = list(context.current_path) + [child.name]
  8. Register child in _node_registry
  9. Record dynamic lifecycle trace events

**缓存失效规则**：`invalidate(node_id)` removes from `_dynamic_children` dict。但 `_generated_pairs` dedup set **跨失效持久**——同一页面指纹+元素名不会重新生成。

### 7.3 TraceCoordinator

Python TraceCoordinator 有 16+ 方法，按 span 类型细分：

- `record_state_transition(from, to)` — span_type="state_transition"
- `record_root_node_pushed(node_id)` — INITIALIZING→TRAVERSING transition
- `record_page_analysis(page_analysis)` — span_type="page_snapshot"
- `record_action_execution(action, target, success)` — span_type="execution"
- `record_metrics_as_spans(metrics)` — dispatches to ai_call/execution/restore/error sub-recorders
- `record_skip_span(match_result)` — span_type="dynamic_matching", action="skip_element"
- `record_execution_span(ex)` — includes is_restore flag
- `record_ai_call_span(ai)` — capability, latency, tokens
- `record_error_span(error_type, message, severity)` — span_type="error"
- `record_decision(decision, ctx)` — stack_depth, current_path
- `record_page_transition(from, to, transition)` — PageTransitionSpan
- `record_dynamic_lifecycle(event, node_id, parent_id, rule_id, element_id)` — DynamicNodeLifecycleSpan
- `record_state_decision(decision, node_id, metadata)` — StateDecisionSpan
- `record_step_start/end(node_id, result)` — step boundaries

所有方法在 `active=False`（recorder null 或无 trace_id）时为 no-op。

**Trace level gates**：`should_record_entry_attempt()` / `should_record_vision_call()` — 从 plan.entry_config.trace_level 或 plan.meta 读取。

**"Log and Continue" 模式**：所有 write 操作用 try-catch 包裹，失败只警告不中断遍历。

**ULID 生成**：26 字符 Crockford Base32，前 10 字符编码 48-bit 毫秒时间戳，后 16 字符编码 80-bit 随机数。

### 7.4 EntryPolicyExecutor

Python `EntryPolicyExecutor` 执行进入策略。

**策略链**（_build_chain）：
1. Primary strategy from policy.strategy
2. Fallback strategy from policy.fallback（if different）
3. Always append BIND_CURRENT_SCREEN as final fallback

**策略执行**：
- DIRECT_DEEPLINK：send deeplink, wait action_delay_ms
- COLD_LAUNCH：press_home → find_app_icon → click → wait
- BIND_CURRENT_SCREEN：wait action_delay_ms (assume already on target)

**Wait condition verification**：
- "fast" mode：single check
- "polling" mode：repeated checks until timeout

### 7.5 PageCacheManager + PageSnapshotManager

**PageCacheManager**（极简）：
- `update(path, page_info)` — store PageCacheInfo (items, timestamp, screen_hash) in context.page_cache[path]
- `restore(path)` — return cached items or None

**PageSnapshotManager**（纯函数，无状态）：
- `fingerprint(page_analysis) → int` — hash of sorted (type, name) tuples from page_analysis.items. Returns 0 for None/empty.
- `has_changed(before, after) → bool` — simple inequality check

### 7.6 单元测试

**StepOrchestrator 核心拦截逻辑**（14 步流程中最复杂，需详尽测试）：
- BRANCH 拦截仅来自 EXECUTE/RESULT_VERIFY/NODE_SELECT（不含 PRECONDITION_CHECK）
- **Anti-loop 机制**：DYNAMIC_MATCH 无剩余子节点 → back + pop stack + return immediately——防止 BRANCH→NODE_SELECT 无限循环。需测试：正常循环（有子节点时正常 push）、死循环触发（无子节点时强制 back+pop）、连续多次触发场景
- **FRAME_COMPLETE 拦截 override**：FRAME_COMPLETE 但 DYNAMIC_MATCH 有剩余子节点时 override → push remaining child。需测试：无剩余子节点时正常 FRAME_COMPLETE、有剩余子节点时 override 成功、override 后子节点栈状态一致性
- 拦截逻辑组合：Anti-loop + FRAME_COMPLETE override 交互场景（先 anti-loop 再 override 的顺序依赖）

**其余子系统**：
- DynamicChildManager：缓存命中/失效/跨失效 dedup 持久
- TraceCoordinator：16+ span 类型方法 + active=no-op 场景 + "Log and Continue"
- EntryPolicyExecutor：3 策略 × fallback chain + wait condition
- PageCacheManager：TTL + update/restore
- PageSnapshotManager：fingerprint 确定性 + has_changed

---

## 8. 验证标准

| # | 标准 | 验证方式 |
|---|------|----------|
| AC-1 | `dotnet build` 0 错误 0 警告 | CI |
| AC-2 | `dotnet test` 增量通过 | CI |
| AC-3 | AI 简化版 PageAnalysis/PopupInfo 已删除 | grep 确认 |
| AC-4 | NodeType 在 Domain.Models.Content | grep 确认 |
| AC-5 | TraversalNode 8 字段完整对齐 Python | 测试断言 |
| AC-6 | TraversalPlan 12 字段完整对齐 Python | 测试断言 |
| AC-7 | PlanCompiler TEMPLATE_SETS 对齐 Python 4 值 | 测试断言 |
| AC-8 | TraversalFSM 8 状态 × 转换矩阵对齐修正定义（PRECONDITION_CHECK → BRANCH 禁止） | 测试断言 |
| AC-9 | TraversalRuntimeContext 26 字段完整 + ITraversalContext 只读接口暴露强类型集合 + CreateReadOnlySnapshot 返回不可变快照 | 测试断言 |
| AC-10 | DynamicChildManager dedup 跨失效持久 | 测试断言 |
| AC-11 | ContainerHandler CompletionDetector/FallbackDecider 无缓存（纯计算） | 代码审查 + 测试无缓存逻辑 |
| AC-12 | TraversalContextSnapshot 创建后引擎修改不影响快照 | 测试断言 |

---

## 9. 决策日志

基于 Python 架构参考文档（07-phase2-python-architecture-reference.md）的源码验证结果，v2.0 修正 5 处设计决策：

| # | 决策 | Python 基准 | C# 修正 | 理由 |
|---|------|-------------|---------|------|
| D-1 | TraversalFSM 转换矩阵移除 PRECONDITION_CHECK → BRANCH | `VALID_TRANSITIONS` 定义了此路径，但 V6.7 handler 从不返回 BRANCH（只返回 EXECUTE/ERROR_HANDLING），`precondition_failed()` 是 dead code | 移除死路径，矩阵改为 PRECONDITION_CHECK → {EXECUTE, ERROR_HANDLING} | 设计为先：状态机转换矩阵应只定义实际被执行的路径。死路径违反最小惊讶原则。未来需要"前置条件跳过"应走 ERROR_HANDLING 管道 |
| D-2 | TraversalRuntimeContext 用 sealed class + ITraversalContext 只读接口 | Python 用 mutable dataclass + `to_readonly()` 创建不可变快照给 AI advisor | sealed class + mutable 内部字段 + ITraversalContext 只读接口 + CreateReadOnlySnapshot() → TraversalContextSnapshot (sealed record) | 引擎每步更新 5-8 字段，record + with 每步复制整个对象（26 字段 × 5-8 次 × 500 步 ≈ 3000 次分配）。Python 用直接赋值，C# 应对齐此行为。不可变数据（AI 快照）用 record |
| D-3 | ContainerHandler 三阶段管道不做缓存 | CompletionDetector/FallbackDecider 各有缓存，但缓存键包含每步变化的值（visited_children List + depth），命中率极低 | 不做缓存，每步纯计算 | 计算量很小（优先级链 + 条件判断），缓存几乎无效。不移植无效缓存设计 |
| D-4 | ITraversalContext 接口用强类型只读集合 | Python 用 Set[str] / Dict[str, Set[str]] / List[str] | VisitedPages → IReadOnlySet<string>, VisitedChildren → IReadOnlyDictionary<string, IReadOnlySet<string>>, CurrentPath → IReadOnlyList<string> | 类型语义对齐 Python（visited_pages 是集合不是字典）。接口只暴露只读视图，引擎内部用 mutable 集合 |
| D-5 | TraversalRuntimeContext 预留 scroll/screen 接口位置 | Python 代码库不存在 scroll_handlers/scroll_contexts/screen_snapshots 字段 | 26 字段对齐 Python，另加 TODO 注释预留 IScrollHandler 和 IPageSnapshot 接口位置（Phase 3 实现） | Phase 2 定位是搬运基准，不混入新功能。预留位置为后续 Phase 留路标 |
