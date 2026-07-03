# Phase 2 核心设计模型参考：Python 架构分析

> **版本**: 1.0
> **日期**: 2026-07-03
> **用途**: Phase 2 C# 改造的 Python 基准参考。本文档不定义 C# 实现方案，只记录 Python 的实际架构模式，供 C# 改造时对照。

---

## 1. 双层状态机模型

Python 遍历系统有两个独立 FSM，在不同海拔运行：

### 1.1 GlobalFSM — 宏观生命周期

**文件**: `src/state_machine/global_fsm.py`

**8 个状态**:

| 状态 | 含义 | 终态? |
|------|------|-------|
| IDLE | 等待启动 | ❌ |
| INITIALIZING | 加载计划、进入应用 | ❌ |
| TRAVERSING | 遍历进行中 | ❌ |
| PAUSED | 手动暂停 | ❌ |
| ERROR | 错误发生 | ❌ |
| RECOVERING | 尝试恢复 | ❌ |
| COMPLETED | 成功完成 | ✅ |
| TERMINATED | 不可恢复终止 | ✅ |

**转换矩阵**:
```
IDLE         → {INITIALIZING}
INITIALIZING → {TRAVERSING, ERROR}
TRAVERSING   → {PAUSED, ERROR, COMPLETED}
PAUSED       → {TRAVERSING, TERMINATED}
ERROR        → {RECOVERING, TERMINATED}
RECOVERING   → {INITIALIZING, TERMINATED}
COMPLETED    → {}  (锁)
TERMINATED   → {}  (锁)
```

**关键约束**:
- ERROR 不能直接回到 TRAVERSING——恢复必须经过 RECOVERING → INITIALIZING → TRAVERSING。INITIALIZING 是状态校验关口（重新验证进入条件、plan 合法性等），不自动重执行引擎 `initialize()` 的完整启动流程
- RECOVERING 成功后回 INITIALIZING（不是 TRAVERSING）
- 回调机制：`register_state_callback(state, callback)`，进入状态时触发，异常吞掉不传播

**C# 改造要点**: GlobalFSM 是独立层，不依赖 Domain。C# 当前已有 `GlobalState` enum 和 `IGlobalStateMachine` 接口骨架。需要实现 `transition_to()` 严格校验 + 回调机制 + `_transition_history`。

---

### 1.2 TraversalFSM — 微观步循环

**文件**: `src/state_machine/traversal_fsm.py`

**8 个状态**（⚠️ 无 DYNAMIC_MATCH 状态——这是 ChildrenStrategyType 值，不是 FSM 状态）:

| 状态 | 含义 | 来源 |
|------|------|------|
| NODE_SELECT | 选择下一节点 | 原始 |
| PRECONDITION_CHECK | 校验前置条件 | 原始 |
| EXECUTE | 执行节点操作 | 原始 |
| RESULT_VERIFY | 验证执行结果 | 原始 |
| BRANCH | 决定下一步 | 原始 |
| FRAME_COMPLETE | 容器帧完成处理 | V6 |
| ERROR_HANDLING | 错误处理 | V6 |
| POPUP_HANDLING | 弹窗处理 | V6 |

**转换矩阵**:
```
NODE_SELECT        → {PRECONDITION_CHECK, BRANCH}
PRECONDITION_CHECK → {EXECUTE, BRANCH, ERROR_HANDLING}
EXECUTE            → {RESULT_VERIFY, BRANCH, ERROR_HANDLING}
RESULT_VERIFY      → {BRANCH, POPUP_HANDLING}
BRANCH             → {NODE_SELECT, PRECONDITION_CHECK, FRAME_COMPLETE, ERROR_HANDLING}
FRAME_COMPLETE     → {NODE_SELECT, ERROR_HANDLING}
ERROR_HANDLING     → {NODE_SELECT, EXECUTE, FRAME_COMPLETE, BRANCH}
POPUP_HANDLING     → {RESULT_VERIFY, ERROR_HANDLING}
```

**step() 方法 — 完整流程**:

1. 记录 `from_state`，准备 `metadata` dict
2. 按 `from_state` 分发到 8 个 handler 方法
3. 每个 handler 返回下一 `TraversalState`
4. 异常兜底：整个 if/elif 在 `try/except Exception` 中，任何未处理异常 → `context.last_error = e` → `consecutive_errors++` → 强制路由到 `ERROR_HANDLING`
5. 调用 `transition_to(next_state)` 执行状态变更

**Handler 方法逻辑摘要**:

| Handler | 关键逻辑 |
|---------|----------|
| `_handle_node_select` | stack 空 → BRANCH；否则 peek 当前节点 → PRECONDITION_CHECK |
| `_handle_precondition_check` | 无 precondition → EXECUTE（快速路径）；有 precondition → 3 轮重试循环（vision 校验 + classify_relation 智能修正：NAVIGABLE→点击、DEEPER/UNKNOWN→返回） |
| `_handle_execute` | 构造 ExecutionContext → action.execute() → V6.15 restore action → RESULT_VERIFY |
| `_handle_result_verify` | vision.analyze_screenshot() → BRANCH |
| `_handle_branch` | 无子节点 + stack>1 → FRAME_COMPLETE；有未访问子节点 → NODE_SELECT；叶节点 + stack>1 → FRAME_COMPLETE |
| `_handle_frame_complete` | 4 种 exit_condition fallback：BACK（返回+弹出）、AUTO_ESCAPE（尝试兄弟菜单+返回）、SKIP（弹出+继续）、ABORT（终止） |
| `_handle_error` | 三层：node error_policy → ExceptionHandlingChain(commented out) → AI advisor(commented out)。当前只实现第一层 |
| `_handle_popup` | 优先级：安全按钮关键词 → 返回操作 → 异常兜底 |

**C# 改造要点**: TraversalFSM 是引擎的核心驱动器。8 个状态 × 转换矩阵需要严格实现。handler 方法中 `_handle_precondition_check`（3 轮智能修正）和 `_handle_frame_complete`（AUTO_ESCAPE 兄弟菜单搜索）是最复杂的两个。

---

### 1.3 PageRelation — 前置条件智能分类

**独立枚举**: `MATCH` / `NAVIGABLE` / `DEEPER` / `UNKNOWN`

**classify_relation() 纯函数**:
1. `current_path[-1] == expected_page` → MATCH
2. `expected_page in current_path[:-1]` → DEEPER
3. `expected_page in available_menus` → NAVIGABLE
4. 否则 → UNKNOWN

**已知缺陷**: 回退过头时返回 UNKNOWN，可能导致不必要的返回操作。Phase B 可能引入基于深度的恢复。

**C# 改造要点**: 这是前置条件修正的核心决策函数。需要作为独立静态方法实现，单元测试覆盖 4 种 relation + 边界情况。

---

## 2. 三阶段管道模式

Python 的 3 个 handler（Container、Error、Popup）共享同一架构模式：

### 2.1 ContainerHandler — Detect → Decide → Execute

| 阶段 | 组件 | 输出 | 缓存 |
|------|------|------|------|
| Detection | CompletionDetector | FrameCompleteResult | key: `"{container_id}_{visited}_{depth}"` |
| Decision | FallbackDecider | FallbackAction | key: `"{reason}_{depth}_{total}"` |
| Execution | ContainerActionExecutor | state_changes dict | 无 |

**CompletionDetector 优先级链**:
1. TIMEOUT（elapsed > timeout_seconds × 1000）→ BACK + should_backtrack
2. MAX_DEPTH（depth >= max_depth）→ BACK + should_backtrack
3. Empty container → ALL_VISITED
4. All children visited → ALL_VISITED + exit_condition 决定 suggested_action
5. Still processing → INCOMPLETE

**FallbackDecider 决策规则**:
- 安全条件（timeout/depth）→ always BACK
- complete + suggested_action → 用 suggested_action
- !can_continue → BACK
- incomplete + can_continue → SKIP

**ContainerActionExecutor 4 个 hook**:
- BACK: `{press_back, pop_frame, restore_parent}`
- AUTO_ESCAPE: `{try_sibling_menu, fallback_to_back, pop_frame}`
- SKIP: `{skip_remaining, pop_frame, mark_complete}`
- ABORT: `{abort, stop_traversal, cleanup}` — `success=False`

**统计**: processed_count, completed_count, action_statistics, avg_depth, completion_rate

---

### 2.2 ErrorHandler — Classify → Select → Execute

| 阶段 | 组件 | 输出 | 缓存 |
|------|------|------|------|
| Classification | ErrorClassifier | ErrorType (6 值) | 无 |
| Strategy | ErrorStrategySelector | ErrorStrategy (5 值) | key: `"{type}_{retry}_{can_backtrack}"` |
| Execution | RecoveryExecutor | ErrorRecoveryResult | 无 |

**ErrorClassifier 优先级链**（子串匹配，不是正则）:
1. CRASH（crash, fatal, anr, force close）
2. PERMISSION（permission, denied, unauthorized, access, forbidden）
3. TIMEOUT（timeout, timed out, time out）
4. NETWORK（network, connection, unreachable, dns, socket）
5. UI_ELEMENT（element, not found, no such, unable to locate, xpath）
6. Exception type name mapping → fallback
7. UNKNOWN

**ErrorStrategySelector 策略优先链**:
```
NETWORK    → [RETRY, BACKTRACK, ABORT]
UI_ELEMENT → [SKIP, RETRY, BACKTRACK]
TIMEOUT    → [RETRY, CONTINUE, BACKTRACK]
PERMISSION → [ABORT, BACKTRACK]
APP_CRASH  → [ABORT]
UNKNOWN    → [CONTINUE, SKIP, ABORT]
```

**策略适用性检查**:
- RETRY: retry_count < max_retries
- BACKTRACK: can_backtrack AND node_stack_length > 1
- SKIP: can_skip
- CONTINUE: always
- ABORT: always

**RecoveryExecutor 5 个 hook**:
- SKIP: `{success=True, continued=True}`
- RETRY: exponential backoff `min(2^retry_count, 10)` seconds
- BACKTRACK: pop container from stack, return parent
- CONTINUE: `{success=True, continued=True}`
- ABORT: `{success=False, continued=False}`

**统计**: total_errors, recovered_count, error_statistics, recovery_rate

---

### 2.3 PopupHandler — Detect → Classify → Preserve → Handle → Restore

**5 步流程**（比 Container/Error 多了 Preserve/Restore）:

```
detector.detect_popup(screen_info) → detected: bool
  [not detected → return early]
classifier.classify_popup(screen_info) → PopupInfo
restorer.preserve_state(context) → state_id
action_handler.handle_popup(popup_info, context) → handling dict
restorer.restore_state(state_id, context)
restorer.validate_restored_state(context)
  [failure → mark handling_result as failed]
```

**PopupDetector**: regex pattern 匹配，4 类型各 5-6 个 pattern，预编译 `re.IGNORECASE`

**PopupClassifier** 5 个子方法:
1. `_determine_popup_type()` → first matching PopupType
2. `_find_dismiss_target()` → 按类型的优先关闭按钮列表
3. `_determine_dismiss_strategy()` → auto_close / back / wait_timeout / auto_close_or_back
4. `_determine_urgency()` → PERMISSION=HIGH, ERROR=CRITICAL|MEDIUM, AD=LOW
5. `_determine_blocking_type()` → PERMISSION/ERROR=MODAL, AD=NON_MODAL|TOAST

**StateRestorer**: 保存 current_node_id + node_stack + current_state + execution_result → 处理弹窗 → 恢复 → 验证

**统计**: detected_count, handled_count, handling_statistics, handling_rate

---

## 3. Hook Dispatch 表模式

Python 3 个 ActionExecutor 都用同一模式：

```python
class XxxActionExecutor:
    _action_hooks: Dict[EnumType, Callable] = {
        BACK: _execute_back,
        AUTO_ESCAPE: _execute_auto_escape,
        SKIP: _execute_skip,
        ABORT: _execute_abort,
    }

    def execute_fallback(self, action, context):
        hook = self._action_hooks.get(action)
        if hook is None:
            return self._execute_back_action()  # fallback
        try:
            return hook(context)
        except Exception:
            return self._execute_back_action()  # exception fallback
```

**优势**: 避免 if/elif 链，新增 action 只需 enum 值 + 方法 + dict entry。异常兜底到最安全的默认操作。

**C# 改造要点**: 用 `Dictionary<FallbackAction, Func<ContainerContext, Dictionary<string, object>>>` 实现。C# 的 delegate 比 Python callable 更类型安全。

---

## 4. 事件驱动模型

### 4.1 TraceRecorder — Push/Pop 上下文模型

**StepTracker** 是核心机制，用栈自动解析 `parent_span_id`:

```
on_node_enter(span_id)  → push(span_id)  → 后续 span 自动归为此 span 的子级
on_node_exit()           → pop(span_id)   → 恢复上级上下文
get_parent_span_id()     → stack[-1]      → 当前父级 span_id
```

**关键**: StepTracker 栈和遍历引擎的 DFS `node_stack` 是**镜像关系**——每推入一个节点，推入对应 span_id；每弹出节点，弹出 span_id。确保 trace 父子关系反映实际调用栈。

**TraceRecorder 生命周期**:
```
init(session_node)      → push session.span_id → set trace_id → _initialized=True
record_step_start(step) → auto resolve parent → push step.span_id
record_span(span)       → auto resolve parent → 不推入栈（叶子事件）
record_step_end(id)     → write end span → pop tracker
finalize(status)        → write session_end → update session.json → flush → _initialized=False
```

**"Log and Continue" 模式**: `_safe_write(node)` 用 try/except 包裹 storage.write()，失败只警告不中断遍历。

**ULID 生成**: 26 字符 Crockford Base32，前 10 字符编码 48-bit 毫秒时间戳（时间排序），后 16 字符编码 80-bit 随机数（全局唯一）。

---

### 4.2 TraceNode 层级 — 5 种节点类型

| 类型 | 字段 | 父级 | 子级 |
|------|------|------|------|
| TraceNode（base） | trace_id, span_id, parent_span_id, node_type, timestamp | — | — |
| SessionNode | device_id/name/model, os/app version, start/end_time, status, config | None（根） | StepNode, SpanNode |
| StepNode | node_id, step_type, page_path, result | SessionNode 或父 StepNode | SpanNode |
| SpanNode | span_type(9种), metadata | StepNode | — |
| PageTransitionSpan | from_page, to_page, trigger_element/action | StepNode | — |
| DynamicNodeLifecycleSpan | event(5种), node_id, parent_id, match_rule_id, element_id | StepNode | — |
| StateDecisionSpan | current_state, decision, reason, context | StepNode | — |

**SpanNode 的 9 种 span_type**:
| span_type | 用途 |
|-----------|------|
| state_transition | FSM 状态变更 |
| execution | 设备操作（含 restore 标记） |
| ai_call | AI 服务调用 |
| error | 错误事件 |
| step_end | 步完成回填 |
| session_end | 会话完成回填 |
| page_snapshot | 页面分析快照 |
| dynamic_matching | 匹配跳过 |
| decision | 引擎决策 |

**序列化策略**: `to_dict()` 按 span_type 条件序列化（只输出相关字段），`from_dict()` 全量水合。保持 JSON 紧凑。

**parent_span_id 链**: 整个 trace 是树结构，序列化后仅靠 `(trace_id, span_id, parent_span_id)` 三元组重建树，children 列表是运行时内存结构不序列化。

---

### 4.3 TraceCoordinator — 16+ span 类型集中记录

**方法列表**:
- record_state_transition(from, to)
- record_root_node_pushed(node_id)
- record_page_analysis(page_analysis)
- record_action_execution(action, target, success, page_context)
- record_metrics_as_spans(metrics)
- record_skip_span(match_result)
- record_execution_span(ex)
- record_ai_call_span(ai)
- record_error_span(error_type, message, severity, stack_trace)
- record_decision(decision, ctx)
- record_page_transition(from_path, to_path, transition)
- record_dynamic_lifecycle(event, node_id, parent_id, rule_id, element_id)
- record_state_decision(decision, node_id, metadata)
- record_step_start(node_id, page_path)
- record_step_end(step_span_id, result)

**active 门控**: 所有方法在 `recorder == null || trace_id == null` 时为 no-op（零开销）。

**Trace level 门控**: `should_record_entry_attempt()` / `should_record_vision_call()` — 从 `plan.entry_config.trace_level` 读取，但当前未被 recording 方法直接调用（是供调用方前置检查的）。

**target 多态处理**: 接收 string / dict(element_id/value) / object(.id) 三种形式。

---

## 5. 生命周期模型

### 5.1 DynamicNode 生命周期 — 5 阶段

| 阶段 | 事件 | 触发条件 | trace 事件 |
|------|------|----------|-----------|
| Created | `generate()` | 页面对象匹配 DynamicRule | DynamicNodeLifecycleSpan(event="created") |
| Matched | `get_next_unvisited_child()` | 已生成子节点被引擎选中 | (由引擎/StepOrchestrator 发) |
| Pushed | `StepOrchestrator._push()` | 子节点推入遍历栈 | (由引擎发) |
| Executed | FSM step | 子节点操作执行完成 | (由引擎发) |
| Popped | `StepOrchestrator` | 子节点弹出遍历栈 | (由引擎发) |

**缓存失效**: `invalidate(node_id)` 在页面变化时触发（由 StepOrchestrator 检测 path 变化）。删除 `_dynamic_children[node_id]`，但 `_generated_pairs` dedup 集 **跨失效持久**——同一 (page_fingerprint, element_name) 不会重新生成，防止循环导航死循环。

---

### 5.2 Session 生命周期

```
Session (业务对象) → SessionNode (trace 对象)
  session_id = generate_id()  (ULID)
  trace_id = session_id       (session_id 即 trace_id)
  → recorder.init(session_node) → push session → set trace_id
  → recorder.finalize(status) → session_end span → update session.json → flush
```

**关键**: `Session` 和 `SessionNode` 是两个独立类型——Session 是引擎的业务对象，SessionNode 是 trace 存储对象。引擎创建 Session，再构造 SessionNode 传给 recorder。

---

### 5.3 TraversalRuntimeContext — 25+ 字段共享状态容器

**字段分类**:

| 类别 | 字段 |
|------|------|
| Identity | trace_id |
| Stack | node_stack (List<StackFrame>) |
| Location | current_path |
| Page | current_page_analysis, current_fingerprint, cache_valid |
| Visited | visited_pages, visited_level1_menus, visited_level2_menus, visited_nodes, visited_children (Dict<string, Set<string>>) |
| Discovery | page_tree |
| History | action_history (keep last 5) |
| Errors | failed_nodes, consecutive_errors |
| Limits | max_depth, step_count, retry_count |
| Completion | completion_policy |
| Device | device_experience |
| Engine-internal | global_state, last_error, exception_chain, ai_provider, page_cache, wait_after_action_ms |

**to_readonly()**: 创建冻结 `TraversalContext` 给 AI advisor，只拷贝 node_stack(node_id only), current_path, visited_pages, visited_nodes, max_depth, step_count, action_history, failed_nodes。不冻结 current_page_analysis, visited_children, page_cache 等引擎内部状态。

**C# 改造要点**: 这是引擎最关键的共享状态。C# 需要决定——纯 `sealed record class` + `with`（每次更新复制整个对象），或 mutable 内部状态 + 不可变公开接口。Python 用的是 mutable dataclass（直接赋值），引擎每步频繁更新。`with` 模式的性能需要 Spike 验证。

---

## 6. StepOrchestrator — 拦截层架构

StepOrchestrator 是 FSM 的**拦截包装层**，不是替代。它：

1. 调用 FSM.step() 获得状态转换结果
2. 拦截特定转换结果（BRANCH / NODE_SELECT / FRAME_COMPLETE）注入引擎级逻辑
3. 桥接 3 个子系统（FSM + DynamicChildManager + TraceCoordinator）

**execute_step() 14 步完整流程**:

```
1.  Setup: NodeStackAdapter + peek current node
2.  Record step start (trace)
3.  Call state_machine.step() → transition result
4.  Record page snapshot (if path changed)
5.  Record action execution (from metrics)
6.  Record metrics spans (trace)
7.  Record state transition (trace)
8.  BRANCH interception: get next unvisited child → push or force frame completion
9.  NODE_SELECT + DYNAMIC_MATCH interception: push child or back+pop (anti-loop)
10. FRAME_COMPLETE interception: override if DYNAMIC_MATCH has remaining children
11. Determine next state (should_complete_frame / child_pushed override)
12. Update visited_nodes
13. Path change detection → invalidate dynamic children cache
14. Record step end (trace)
```

**关键拦截**:
- **Step 9 anti-loop**: 当 DYNAMIC_MATCH 节点无剩余子节点时，直接执行 back + pop stack + return immediately——防止 BRANCH→NODE_SELECT 无限循环
- **Step 10 override**: 当 FRAME_COMPLETE 但 DYNAMIC_MATCH 有剩余子节点时，override → push remaining child
- **Step 13 cache invalidation**: path 变化时调用 `child_mgr.invalidate(current.node_id)`

---

## 7. GraphTraversalEngine — 编排器

**初始化流程** (initialize):
```
1. global_state = INITIALIZING
2. PlanValidator.validate(plan)
3. Create Session → context.trace_id = session_id
4. recorder.init(session_node) → push session
5. Record IDLE→INITIALIZING transition
6. EntryPolicyExecutor.execute() → launch app
7. EntryPolicyExecutor.wait_for_condition() → verify entry
8. Validate root_node
9. Record step start for root node
10. Push root node onto context.node_stack
11. Record root pushed
12. INITIALIZING→TRAVERSING transition
```

**主循环** (run):
```
1. _start_time = time.time()
2. initialize()
3. Create StepOrchestrator + StepContext
4. while _should_continue():
   - step_count > 500 → break (safety)
   - execute_step(step_ctx)
   - step_count++
5. return _create_result(COMPLETED)
6. on exception → _create_result(ERROR)
7. finally → _end_time = time.time()
```

**_should_continue()**: node_stack 非空 + completion_policy 未触发

**_check_completion_policy()**: NONE(never) / TARGET_FOUND(exact/contains) / TIMEOUT / MAX_STEPS

---

## 8. 跨组件交互模式总结

| # | 模式 | 实例 | C# 改造要点 |
|---|------|------|-------------|
| 1 | 三阶段管道 | ContainerHandler / ErrorHandler / PopupHandler | 每阶段独立类 + 缓存 + facade orchestrator |
| 2 | Hook Dispatch 表 | 3 个 ActionExecutor | Dictionary<Enum, Func> + exception fallback |
| 3 | 安全优先链 | CompletionDetector / ErrorClassifier / ExceptionHandlingChain | 优先级排序：crash > permission > timeout > network > element |
| 4 | 统计累积+率 | 4 个 handler 各自统计 | total/recovered/rate dict snapshot |
| 5 | 状态保存/恢复/验证 | PopupHandler StateRestorer | preserve → act → restore → validate |
| 6 | 拦截层 | StepOrchestrator wraps FSM | FSM 产生转换 → orchestrator 拦截注入引擎逻辑 |
| 7 | 集中事件 Sink | TraceCoordinator | 16+ span 类型方法 + active 门控 |
| 8 | Push/Pop 双栈 | StepTracker mirrors node_stack | trace parentage 自动反映遍历栈 |
| 9 | "Log and Continue" | _safe_write | trace 写失败不中断遍历 |
| 10 | Dedup 跨失效 | DynamicChildManager._generated_pairs | 防循环导航死循环 |
| 11 | Read-Only Snapshot | TraversalRuntimeContext.to_readonly() | AI advisor 只读视图 |
| 12 | 条件序列化 | SpanNode.to_dict() | 按 span_type 只输出相关字段 |
