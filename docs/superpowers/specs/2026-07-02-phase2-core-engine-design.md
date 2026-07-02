# Phase 2 Design：核心引擎搬运 + 架构修正

> **版本**: 1.0
> **日期**: 2026-07-02
> **分支**: `feature/refactor`
> **前置**: Phase 1 + Phase 1.1（已完成，Domain 层 185 测试全绿）
> **验证基准**: Python `main` 分支源码逐行对齐

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

### 4.3 其他依赖方向修正

| 问题 | 修正 |
|------|------|
| `NodeData` 在 AI 层定义但语义属于 Graph | `NodeData` record 移到 Graph 层 |
| `ContainerInference` / `PageTypeVerification` 等在 AI 层定义但不含 Domain 依赖 | 保留在 AI 层（纯 AI 决策结果类型，无跨层问题） |
| 两个运行上下文系统 | Python 有 `TraversalContext`（AI 用）和 `TraversalRuntimeContext`（引擎用）。C# 统一为 `TraversalRuntimeContext`（Trace 层定义），AI 通过接口访问所需子集 |
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

**TraversalRuntimeContext**（引擎共享状态容器，25+ 字段）：
- trace_id, node_stack (List<StackFrame>), current_path, current_page_analysis, current_fingerprint, cache_valid
- visited_pages, visited_level1_menus, visited_level2_menus, visited_nodes, visited_children (Dict<string, Set<string>>)
- page_tree, action_history (keep last 5), failed_nodes, consecutive_errors, max_depth, step_count, retry_count
- completion_policy, device_experience, global_state, last_error, exception_chain, ai_provider
- page_cache, wait_after_action_ms
- Helper methods：get_current_depth(), is_at_max_depth(), record_action(), to_readonly()

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
NODE_SELECT → {PRECONDITION_CHECK, BRANCH}
PRECONDITION_CHECK → {EXECUTE, BRANCH, ERROR_HANDLING}
EXECUTE → {RESULT_VERIFY, BRANCH, ERROR_HANDLING}
RESULT_VERIFY → {BRANCH, POPUP_HANDLING}
BRANCH → {NODE_SELECT, PRECONDITION_CHECK, FRAME_COMPLETE, ERROR_HANDLING}
FRAME_COMPLETE → {NODE_SELECT, ERROR_HANDLING}
ERROR_HANDLING → {NODE_SELECT, EXECUTE, FRAME_COMPLETE, BRANCH}
POPUP_HANDLING → {RESULT_VERIFY, ERROR_HANDLING}
```

**step() 方法**：try-catch wrapper，内层按 from_state 分发到 handler 方法。handler 返回下一 TraversalState。异常时路由到 ERROR_HANDLING。

**C# 实现策略**：enum-based switch（与 Python if/elif 链对应）。Phase 1 已验证 enum + construction-throw 在 Domain 层可行。

### 6.2 ContainerHandler

Python `ContainerHandler` 由 3 个子组件组成：

**CompletionDetector.detect_completion()**（优先级链）：
1. Timeout exceeded → is_complete=True, reason=TIMEOUT, suggested_action=BACK, should_backtrack=True
2. Max depth reached → is_complete=True, reason=MAX_DEPTH, suggested_action=BACK, should_backtrack=True
3. No children → is_complete=True, reason=ALL_VISITED, suggested_action=BACK
4. All children visited → is_complete=True, reason=ALL_VISITED, suggested_action=由 exit_condition 决定
5. Still processing → is_complete=False, reason=INCOMPLETE

**FallbackDecider**：Timeout/depth→always BACK, Complete→use suggested_action, Incomplete→SKIP

**ContainerActionExecutor**：映射 action 到 hook：
- BACK → press_back + pop_frame + restore_parent
- AUTO_ESCAPE → try_sibling_menu + fallback_to_back + pop_frame
- SKIP → skip_remaining + pop_frame + mark_complete
- ABORT → abort + stop_traversal + cleanup

注意：CompletionDetector 和 FallbackDecider 都有缓存（composite key：container_id + visited_children + depth）。

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

**ErrorHandler**：orchestrator chaining classifier → selector → executor。维护统计（total_errors, recovered_count, error_statistics）。

### 6.4 PopupHandler

Python `PopupHandler` 由 4 个子组件 + StateRestorer 组成：

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

**StateRestorer**：保存/恢复遍历上下文（current_node_id, node_stack, current_state, execution_result, timestamp）——确保弹窗处理后遍历连续性。

**PopupHandler.handle_popup() 流程**：
1. detector.detect_popup(screen_info) — if no popup, return early
2. classifier.classify_popup(screen_info) — get PopupInfo
3. restorer.preserve_state(context) — save state
4. action_handler.handle_popup(popup_info, context) — execute dismiss
5. restorer.restore_state(state_id, context) — restore state
6. restorer.validate_restored_state(context) — verify

### 6.5 单元测试

- TraversalFSM：8 个状态 × 有效转换矩阵
- CompletionDetector：timeout / max_depth / all_visited / no_children / incomplete 5 种场景
- ErrorHandler：6 种 ErrorType × 策略优先链 + 适用性检查
- PopupHandler：5 种 PopupType × dismiss 策略 + StateRestorer 保存/恢复
- TraversalRuntimeContext：字段更新 + with-独立性

---

## 7. Phase 2.3：Traversal Engine 子系统

### 7.1 StepOrchestrator

Python `StepOrchestrator` 执行一个 FSM step 的完整流程。

**StepContext**（值对象，封装 step 所有依赖）：
- context (TraversalRuntimeContext)
- state_machine (TraversalStateMachine)
- vision / action / child_mgr / node_registry / trace
- last_known_path / last_recorded_path / last_recorded_action
- snapshot_mgr / stack

**execute_step(ctx) 流程**：
1. 创建 NodeStackAdapter（从 context + node_registry）
2. 记录 step start（trace）
3. 调用 state_machine.step(stack, context, vision, action)
4. 记录 page snapshot（path 变化时）
5. 记录 action execution（从 handler metrics）
6. 记录 metrics spans
7. 记录 state transition
8. **BRANCH 处理**：from EXECUTE/RESULT_VERIFY/PRECONDITION_CHECK/NODE_SELECT 时，获取 next unvisited child（via child_mgr），有则 push，无则 force frame completion
9. **NODE_SELECT + DYNAMIC_MATCH**：如果当前节点用 DYNAMIC_MATCH strategy，获取 next child，无则执行 back + pop stack（避免 BRANCH→NODE_SELECT 循环）
10. **FRAME_COMPLETE 拦截**：如果 transitioning to FRAME_COMPLETE 但当前节点是 DYNAMIC_MATCH 有 remaining unvisited children，override → push remaining child
11. Path 变化检测 + cache invalidation（调用 child_mgr.invalidate()）
12. 记录 step end（trace）

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

- StepOrchestrator：step 执行流程各分支（正常/异常/页面无变化/FRAME_COMPLETE 拦截）
- DynamicChildManager：缓存命中/失效/跨失效 dedup 持久
- TraceCoordinator：16+ span 类型方法 + active=no-op 场景
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
| AC-8 | TraversalFSM 8 状态 × 有效转换矩阵对齐 Python | 测试断言 |
| AC-9 | TraversalRuntimeContext 25+ 字段完整 | 测试断言 |
| AC-10 | DynamicChildManager dedup 跨失效持久 | 测试断言 |
