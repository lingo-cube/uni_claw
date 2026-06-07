# 仿真测试数据模型速查

生成或修改测试用例时，以本文档为唯一数据来源。所有条目与实际代码一致，修改代码后需同步更新。

---

## 一、核心数据模型位置

| 模型 | 文件 | 关键字段 |
|------|------|---------|
| `TraversalPlan` | `src/graph/plan.py` | `entry_app`, `entry_policy`, `root_node`, `static_nodes`, `template_registry`, `mode`, `completion_policy`, `intent_slots`, `meta` |
| `TraversalNode` | `src/graph/node.py` | `node_id`, `name`, `node_type`, `operation`, `precondition`, `children_strategy`, `exit_condition`, `error_policy`, `meta` |
| `Operation` | `src/graph/node.py` | `action`（str）, `target`（Optional[Target]）, `params`（Dict）, `restore`（Optional[RestoreAction]） |
| `Target` | `src/graph/node.py` | `by`（str）, `value`（Any）, `meta`（Dict） |
| `Precondition` | `src/graph/node.py` | `page_name`, `path`, `ui_condition`, `timeout_seconds` |
| `ChildrenStrategy` | `src/graph/node.py` | `type`（ChildrenStrategyType）, `static_children`（List[str]）, `dynamic_rules`（Dict[str, DynamicRule]）, `max_children` |
| `DynamicRule` | `src/graph/node.py` | `rule_id`, `match_condition`（Dict）, `child_template`, `action` |
| `ExitCondition` | `src/graph/node.py` | `type`（ExitConditionType）, `fallback`（FallbackAction）, `max_depth` |
| `EntryPolicy` | `src/graph/node.py` | `strategy`（EntryStrategy）, `fallback`, `wait_condition`, `timeout_seconds` |
| `CompletionPolicy` | `src/graph/node.py` | `type`（CompletionPolicyType）, `target_name`, `match_mode`, `action_on_found`, `timeout_seconds`, `max_steps` |
| `IntentSlots` | `src/graph/node.py` | `target_app`, `scope`, `target`, `depth`, `element_handling`, `navigation`, `restore`, `completion` |
| `PageAnalysis` | `src/state/content_tree.py` | Pydantic BaseModel: `level1_dir`, `level1_menus`, `level2_dir`, `level2_menus`, `current_path`（List[str]）, `items`（List[MenuItem]）, `is_popup`, `popup_info`, `close_button`, `back_button`, `has_scroll`, `is_end_of_list` |
| `MenuItem` | `src/state/content_tree.py` | Pydantic BaseModel: `name`, `type`, `coordinate`（Coordinate）, `parent`, `description`, `expected_action`, `expects_page_change`, `expects_state_change` |
| `SpanNode` | `src/trace/models.py` | dataclass: `span_id`, `trace_id`, `parent_span_id`, `span_type`, `from_state`, `to_state`, `action`, `status`, `severity`, `error_type`, `error_message`, `latency_ms`, `duration_ms`, `page_before`, `page_after`, `target`, `capability`, `provider_id`, `input_tokens`, `output_tokens`, `element_count`, `page_id`, `state_machine`, `node_type`, `step_span_id`, `screenshot_ref`, `stack_trace`, `children`, `success`, `metadata`, `timestamp` |
| `StackFrame` | `src/state_machine/node_stack.py` | dataclass: `node`（TraversalNode）, `child_queue`（List[str]）, `current_child_idx`（int）, `pending_restore`（bool）, `entered_at`（datetime）, `metadata`（Dict） |
| `StackFrame` | `src/trace/context.py` | 另一个 StackFrame: `node_id`（str）, `span_id`（str）, `node_type`（str） |

**注意**：
- `StackFrame` 在 `node_stack.py` 和 `trace/context.py` 各有一份定义，字段不同。引擎侧用 `node_stack.StackFrame`（含 `node` 对象），上下文侧用 `trace.context.StackFrame`（含 `node_id` 字符串）。
- `StackFrame` 没有 `is_leaf()` 或 `mark_completed()` 方法。

---

## 二、仿真组件位置

| 组件 | 文件 | 关键方法 |
|------|------|---------|
| `GraphTraversalEngine` | `src/traversal/graph_engine.py` | `run()`, `_step_once()`, `_get_next_unvisited_child(node)`, `initialize()`, `_push_node(node_id)`, `_generate_dynamic_children(node)`, `_dynamic_children`, `invalidate_children_cache(node_id)` |
| `TraversalStateMachine` | `src/state_machine/traversal_fsm.py` | `step(stack, context, vision, action)`, `_last_handler_metrics`, `state`, `transition_history` |
| `NodeStack` | `src/state_machine/node_stack.py` | `push(node)`, `pop()`, `top()`, `peek()`, `is_empty()`, `size()`, `clear()`, `to_list()`, `get_current_node_id()`, `get_node_path()`, `get_parent_node_id()`, `contains_node(node_id)`, `get_depth_of_node(node_id)`, `depth_limit_reached()`, `depth()`, `get_summary()` |
| `DynamicMatcher` | `src/graph/matcher.py` | `match_all(menu_items, parent_node) -> List[MatchResult]`, `load_rules(rules)`, `instantiate_match(result) -> TraversalNode`, `get_statistics()` |
| `MatchResult` | `src/graph/matcher.py` | `matched`（bool）, `rule_id`, `template_id`, `action`（MatchAction）, `menu_item`, `context` |
| `MatchCondition` | `src/graph/matcher.py` | `matches(menu_item) -> bool` | 读取 `type`, `expected_action`, `text`, `index`, `min_index`, `max_index` |
| `TemplateRegistry` | `src/graph/template.py` | `instantiate(template_id, context)`, `list_templates()`，内置模板：`menu_container` / `switch_leaf` / `slider_leaf` |
| `TraceRecorder` | `src/trace/recorder.py` | `init(session_node)`, `record_span(span)`, `record_step_start(step)`, `record_step_end(step_span_id, result)`, `finalize(status)` |
| `MemoryStorage` | `src/trace/storage.py` | `write(node)`, `read(trace_id) -> List` |
| `SimulationRunner` | `src/simulation/runner.py` | `__init__(virtual_pages, plan, config)`, `run() -> SimulationResult`, `storage` |
| `SimulationResult` | `src/simulation/runner.py` | `engine_result`（Dict）, `trace`（List[Dict]）, `executed_actions`（List[Dict]）, `visited_tree`（Dict）, `elapsed_seconds`, `completion_reason`, `statistics`（Dict）, `trace_id` |
| `MockVisionService` | `src/simulation/mock_vision.py` | `analyze_screenshot(image_data) -> PageAnalysis`, `set_path_context(path)`, `inject_path(path)`, `call_count`, `reset()` |
| `MockActionExecutor` | `src/simulation/mock_action.py` | `execute(ctx) -> ExecutionResult`, `get_executed_actions()`, `clear_history()`, `history`（属性）, `reset()` |
| `ExecutionContext` | `src/simulation/operation_executor.py` | `node_id`, `node_name`, `operation`（Dict）, `screen_info`, `timestamp` |
| `ExecutionResult` | `src/simulation/operation_executor.py` | `success`（bool）, `action`（str）, `error`（str）, `metadata`（Dict） |

---

## 三、枚举值速查

所有枚举为 `str, Enum`，`.value` 为小写字符串。代码中引用 `EnumClass.MEMBER` 无问题，但 JSON 序列化和 `.value` 比较用小写。

| 枚举 | 文件 | 值 |
|------|------|----|
| `GlobalState` | `src/state_machine/global_fsm.py` | `idle`, `initializing`, `traversing`, `paused`, `error`, `recovering`, `completed`, `terminated` |
| `TraversalState` | `src/state_machine/traversal_fsm.py` | `node_select`, `precondition_check`, `execute`, `result_verify`, `branch`, `frame_complete`, `error_handling`, `popup_handling` |
| `NodeType` | `src/graph/node.py` | `container`, `leaf_switch`, `leaf_slider`, `leaf_action`, `leaf_info`, `screen`, `action`, `target` |
| `ChildrenStrategyType` | `src/graph/node.py` | `static`, `dynamic_match`, `none` |
| `FallbackAction` | `src/graph/node.py` | `back`, `auto_escape`, `skip`, `abort` |
| `CompletionPolicyType` | `src/graph/node.py` | `none`, `target_found`, `timeout`, `max_steps` |
| `EntryStrategy` | `src/graph/node.py` | `cold_launch`, `direct_deeplink`, `bind_current_screen` |
| `ExitConditionType` | `src/graph/node.py` | `all_children_visited`, `depth_limited`, `single_level` |
| `MatchAction` | `src/graph/matcher.py` | `generate_child`, `skip`, `execute_inline` |
| `RecoveryStrategy` | `src/trace/recovery.py` | `full`, `replay`, `minimal` |
| `ErrorPolicy.on_error` | `src/graph/node.py` | `retry`, `skip`, `abort`, `fallback`, `backtrack` |

---

## 四、虚拟页面 JSON 格式

MockVisionService 从虚拟页面 JSON 构建 PageAnalysis 的映射规则：

```json
{
  "/path/to/page": {
    "path": "/path/to/page",
    "elements": [
      {
        "id": "element_id",
        "text": "Element Name",
        "type": "menu_item",
        "clickable": true,
        "bounds": [0, 100, 500, 180],
        "description": "Optional description"
      }
    ],
    "is_popup": false,
    "has_scroll": false,
    "is_end_of_list": false
  }
}
```

### MockVisionService 映射规则

| JSON 输入 | PageAnalysis / MenuItem 输出 |
|-----------|----------------------------|
| `path` | `current_path = path.split("/")` |
| `elements[].text` 或 `elements[].name` | `MenuItem.name` |
| `elements[].type` 或自动推断 | `MenuItem.type` |
| `elements[].bounds` 或 `elements[].coordinate` | `MenuItem.coordinate` = Coordinate(中点) |
| `elements[].clickable` + `type` | 自动推断 `expected_action = "navigate"` |
| `expected_action = "navigate"` | `expects_page_change = True` |
| `is_popup = true` | `popup_info` 填充 |

**注意**：MockVisionService **没有** `set_response_sequence()` 方法，通过 `set_path_context()` 和 `inject_path()` 控制返回的页面。

---

## 五、现有测试资产

| 资产 | 位置 |
|------|------|
| 虚拟页面 fixture（单页） | `tests/assets/fixtures/virtual_pages_simple.json` |
| 虚拟页面 fixture（7 页设置） | `tests/assets/fixtures/pages_all.json` |
| 虚拟页面 fixture（目标搜索） | `tests/assets/fixtures/pages_find.json` |
| 计划 fixture（全遍历） | `tests/assets/fixtures/plan_all.json` |
| 计划 fixture（目标搜索） | `tests/assets/fixtures/plan_find_version.json` |
| 计划 fixture（静态路径） | `tests/assets/fixtures/plan_static.json` |
| 工厂函数 | `tests/assets/utils/model_helpers.py` |
| Fixture 加载 | `tests/assets/__init__.py`（`load_virtual_pages()`, `load_plan()`） |

---

## 六、测试目录结构

```
tests/
├── REFERENCE.md                    # 本文档
├── conftest.py                     # pytest 配置（markers, path, asyncio）
├── assets/
│   ├── __init__.py                 # load_virtual_pages(), load_plan()
│   ├── fixtures/                   # JSON fixtures
│   └── utils/
│       └── model_helpers.py        # 工厂函数
├── helpers/                        # 共享测试辅助（待建）
│   ├── mock_factories.py           # FailingMockVisionService 等
│   ├── engine_factories.py         # quick_simulation_runner() 等
│   └── trace_asserter.py           # assert_completed() 等断言工具
├── v6/
│   ├── test_simulation_base.py     # Mock 服务基础测试
│   ├── test_v6_9_dynamic_match.py  # 动态匹配集成测试
│   ├── test_simulation_sm.py       # 状态机智能纠正测试
│   ├── test_engine_initialization.py # 入口策略测试
│   ├── test_trace_simulation.py    # Trace 仿真测试
│   ├── unit/                       # 单元测试
│   └── ...
└── integration/
    └── test_simulation_e2e.py      # 端到端测试
```
