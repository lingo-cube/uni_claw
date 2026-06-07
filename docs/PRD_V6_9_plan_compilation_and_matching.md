# V6.9 遍历执行与计划编译 PRD

**版本**: V6.9
**日期**: 2026-06-07
**依赖**: V6.8 engine-initialization（引擎初始化链路）
**状态**: 设计阶段

---

## 1. 背景

V6.8 完成后，引擎能启动并进入目标应用，但它无法从页面元素生成子节点。引擎在根容器第一次 BRANCH 时 `_get_next_unvisited_child` 对 `DYNAMIC_MATCH` 返回 `None`，立即触发 `FRAME_COMPLETE`——根节点被弹出，遍历结束。这是"能启动但不会走路"。

动态匹配和模板注册表是连接视觉结果与遍历任务的核心桥梁。同时，每份 `TraversalPlan` 都需要手动编写 JSON 过于繁琐，需要一个编译器将 AI 提取的意图槽位映射为可执行计划。

---

## 2. 目标

1. **动态匹配集成**：引擎 BRANCH 时调用 `DynamicMatcher.match_all()` 从 `PageAnalysis.items` 生成子节点
2. **模板注册表真加载**：`_load_template_registry()` 真正初始化 `TemplateRegistry` + `DynamicMatcher`
3. **子节点路径拼接**：实例化时自动生成 `precondition.path = 父路径 + [name]`
4. **页面变化感知**：检测 `current_path` 变化 → 自动失效子节点缓存
5. **计划编译器**：`PlanCompiler` 将 `IntentSlots` 确定性映射为 `TraversalPlan`

---

## 3. 架构总览

```
┌─────────── 编译阶段（新文件）──────────────────────────────────┐
│  NL Task                                                       │
│    │                                                           │
│    ▼                                                           │
│  src/ai/task_parser.py  ── AI 提取 ──→ IntentSlots            │
│                                            │                   │
│                                            ▼                   │
│  src/graph/compiler.py   ── PlanCompiler.compile()             │
│    ├── scope → completion_policy                               │
│    ├── element_handling → dynamic_rules 模板集                  │
│    ├── navigation → exit_condition.fallback                    │
│    └── _validate_slots() → 槽位合理性检查                       │
│    └── → TraversalPlan                                          │
└───────────────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────── 执行阶段（改现有文件）────────────────────────────────┐
│  GraphTraversalEngine.run()                                     │
│    │                                                            │
│    ├── initialize()    [V6.8]                                   │
│    │   └── _load_template_registry()  ← 真正加载               │
│    │                                                            │
│    └── _step_once()    [V6.9 修改]                              │
│        ├── state_machine.step()                                 │
│        ├── BRANCH → _get_next_unvisited_child()                 │
│        │   ├── STATIC: 逐个取 static_children                   │
│        │   └── DYNAMIC_MATCH:                                   │
│        │       ├── 首次 → _generate_dynamic_children()          │
│        │       │   ├── load_rules()                              │
│        │       │   ├── match_all(page_analysis.items)            │
│        │       │   ├── instantiate_match() → path 拼接           │
│        │       │   └── 缓存到 _dynamic_children                  │
│        │       └── 逐个取未访问子节点                            │
│        ├── FRAME_COMPLETE → 拦截过早退出                        │
│        └── 路径变化 → invalidate_children_cache()               │
└───────────────────────────────────────────────────────────────┘
```

---

## 4. 执行阶段详细设计

### 4.1 缓存与子节点生成

**新增字段**：

```python
# graph_engine.py __init__
self.template_registry: Optional[TemplateRegistry] = None
self.dynamic_matcher: Optional[DynamicMatcher] = None
self._dynamic_children: Dict[str, List[TraversalNode]] = {}
self._last_known_path: List[str] = []
```

**`_load_template_registry()` 真加载**：

```python
def _load_template_registry(self) -> None:
    self.template_registry = TemplateRegistry()  # 始终加载 3 个内置模板
    if self.plan.template_registry:
        p = Path(self.plan.template_registry)
        if p.exists():
            self.template_registry.load_from_file(p)
    self.dynamic_matcher = DynamicMatcher(self.template_registry)
```

**`_get_next_unvisited_child` 加入 DYNAMIC_MATCH 分支**：

```python
def _get_next_unvisited_child(self, node: TraversalNode) -> Optional[str]:
    strategy = node.children_strategy
    if not strategy:
        return None

    visited = self.context.visited_children.get(node.node_id, set())

    if strategy.type == ChildrenStrategyType.STATIC:
        for child_id in strategy.static_children:
            if child_id not in visited:
                visited.add(child_id)
                return child_id
        return None

    if strategy.type == ChildrenStrategyType.DYNAMIC_MATCH:
        if node.node_id not in self._dynamic_children:
            self._generate_dynamic_children(node)
        children = self._dynamic_children.get(node.node_id, [])
        for child in children:
            if child.node_id not in visited:
                visited.add(child.node_id)
                return child.node_id
        return None

    return None  # NONE
```

**`_generate_dynamic_children`**：

```python
def _generate_dynamic_children(self, node: TraversalNode) -> None:
    # 1. 加载规则（DynamicRule 对象 → dict 转换）
    # node.children_strategy.dynamic_rules 是 Dict[str, DynamicRule]
    # DynamicMatcher.load_rules() 期望 Dict[str, Dict]，需要转换
    rules = {}
    for rule_id, rule in node.children_strategy.dynamic_rules.items():
        rules[rule_id] = {
            "match_condition": rule.match_condition,
            "child_template": rule.child_template,
            "action": rule.action,
        }
    self.dynamic_matcher.load_rules(rules)

    # 2. 获取当前页面元素（MenuItem → dict 字段映射）
    # DynamicMatcher 契约：读 "type" / "text"(不是name) / "index" / "coordinate_x/y"
    # MatchCondition.matches()   → type, expected_action, text, index, min_index, max_index
    # _build_context()           → text→item_text, index→item_index, coordinate_x/y, parent_id
    page_analysis = self.context.current_page_analysis
    items = []
    if page_analysis and page_analysis.items:
        for idx, item in enumerate(page_analysis.items):
            items.append({
                "type": item.type.value if hasattr(item.type, 'value') else item.type,
                "text": item.name,  # ← 注意：DynamicMatcher 读 "text"，不是 "name"
                "index": idx,
                "coordinate_x": item.coordinate.x if item.coordinate else 0.5,
                "coordinate_y": item.coordinate.y if item.coordinate else 0.5,
            })

    # 3. 匹配 + 实例化
    results = self.dynamic_matcher.match_all(items, parent_node=node)
    children = []
    for r in results:
        if r.matched and r.action == MatchAction.GENERATE_CHILD:
            child = self.dynamic_matcher.instantiate_match(r)
            if child.precondition:
                child.precondition.path = self.context.current_path + [child.name]
            self._node_registry[child.node_id] = child
            children.append(child)
        else:
            self._record_skip_span(r)

    # 4. 缓存
    self._dynamic_children[node.node_id] = children
```

### 4.2 拦截 FRAME_COMPLETE 过早退出

`_handle_branch` 不改。在 `_step_once()` 中，FRAME_COMPLETE 后检查当前容器是否还有未访问的动态子节点：

```python
# _step_once() — FRAME_COMPLETE 拦截
if transition.to_state == TraversalState.FRAME_COMPLETE:
    current = stack.peek()
    if (current and
        current.children_strategy and
        current.children_strategy.type == ChildrenStrategyType.DYNAMIC_MATCH):
        child_id = self._get_next_unvisited_child(current)
        if child_id:
            self._push_node(child_id)
            next_state = TraversalState.NODE_SELECT  # override
        # else: FRAME_COMPLETE 正确，放行
```

### 4.3 缓存失效

```python
def invalidate_children_cache(self, node_id: str) -> None:
    """失效子节点缓存，供页面变化后调用。"""
    self._dynamic_children.pop(node_id, None)
```

触发点：`_step_once()` 末尾检测 `current_path` 变化。

```python
# _step_once() 末尾
path_now = list(self.context.current_path)
if path_now != self._last_known_path:
    current = stack.peek()
    if current:
        self.invalidate_children_cache(current.node_id)
    self._last_known_path = path_now
```

### 4.4 模板路径拼接

`TemplateInstantiator.instantiate()` 新增 `parent_path` 参数：

```python
def instantiate(
    self,
    template: Template,
    context: Dict[str, Any],
    parent_path: Optional[List[str]] = None,
) -> TraversalNode:
    # ... 现有逻辑 ...

    # 路径拼接
    if parent_path and node.precondition:
        node.precondition.path = parent_path + [node.name]

    return node

# TemplateRegistry.instantiate() 透传
def instantiate(
    self,
    template_id: str,
    context: Dict[str, Any],
    parent_path: Optional[List[str]] = None,
) -> Optional[TraversalNode]:
    template = self.get_template(template_id)
    if not template:
        return None
    return self.instantiator.instantiate(template, context, parent_path)
```

---

## 5. 编译阶段详细设计

### 5.1 文件布局

```
src/graph/compiler.py      # PlanCompiler 类（确定性映射，无 AI 依赖）
src/ai/task_parser.py       # parse_task_to_slots()（AI 解析入口，V6.9 先占位）
```

### 5.2 PlanCompiler

```python
class CompilerError(Exception):
    """编译器异常。"""
    pass


class PlanCompiler:
    """将 IntentSlots 确定性映射为 TraversalPlan。"""

    def compile(self, slots: IntentSlots) -> TraversalPlan:
        self._validate_slots(slots)
        return TraversalPlan(
            entry_app=slots.target_app,
            entry_policy=self._build_entry_policy(slots),
            root_node=self._build_root_node(slots),
            completion_policy=self._build_completion_policy(slots),
            intent_slots=slots,
        )
```

**映射规则**：

| IntentSlot | → Plan 字段 | 映射逻辑 |
|-----------|------------|---------|
| `target_app` | `entry_app` | 直接映射 |
| `scope` | `completion_policy.type` | `"full"`→NONE, `"partial"`→MAX_STEPS, `"target_only"`→TARGET_FOUND, `"target_path"`→NONE + STATIC 路径 |
| `target` | `completion_policy.target_name` | 直接映射 |
| `depth` | `intent_slots.depth` + `exit_condition.max_depth` | 直接映射 |
| `element_handling` | `dynamic_rules` 组成 | 见下表 |
| `navigation` | `exit_condition.fallback` | `"back"`→BACK, 其他/None→AUTO_ESCAPE |
| `completion` | `completion_policy` 覆盖 | `"timeout"`→TIMEOUT, `"steps"`→MAX_STEPS |
| `restore` | `root_node.meta["restore"]` | 直接映射 |

**`element_handling` → 模板集**：

| 值 | 包含模板 | 含义 |
|---|---------|------|
| `"full_interaction"` | menu_container + switch_leaf + slider_leaf + leaf_action | 全元素遍历 |
| `"menu_only"` | menu_container | 仅递归进入菜单 |
| `"safe_mode"` | full_interaction 模板集 + 安全预筛标记写入 meta | 全遍历 + 安全过滤 |
| `"read_only"` | leaf_info | 仅记录元素，不操作 |

**`navigation` → fallback**：

| 值 | exit_condition.fallback |
|---|------------------------|
| `"back"` | BACK |
| 其他 / None | AUTO_ESCAPE |

**`completion` → completion_policy 覆盖**：

当 `completion` 字段有值时，覆盖 `scope` 推导的 `completion_policy`：

| 值 | completion_policy.type |
|---|------------------------|
| `"timeout"` | TIMEOUT + `timeout_seconds`（默认 300s）|
| `"steps"` | MAX_STEPS + `max_steps`（默认 100）|

### 5.3 静态路径处理

当 `scope = "target_path"` 时，编译器生成 STATIC 策略：

```
root_node.children_strategy.type = STATIC
root_node.children_strategy.static_children = [child_1_id]
static_nodes[child_1_id] = node(for path_segment_1)
static_nodes[child_2_id] = node(for path_segment_2)
...
```

`target` 字段使用 `/` 分隔符解析为路径段，每段生成一个容器节点（除最后一段为 leaf_action）。

每个静态节点的 `precondition.path` 需在编译时显式设置为完整路径，规则与模板实例化一致：

```
node_1.precondition.path = [segment_1]
node_2.precondition.path = [segment_1, segment_2]
node_3.precondition.path = [segment_1, segment_2, segment_3]
```

### 5.4 槽位验证

```python
def _validate_slots(self, slots: IntentSlots) -> None:
    """验证槽位组合的合理性。"""
    if not slots.target_app:
        raise CompilerError("target_app is required")
    if slots.scope in ("target_only", "target_path") and not slots.target:
        raise CompilerError(f"target is required when scope is {slots.scope}")
    if slots.depth is not None and (slots.depth <= 0 or slots.depth > 1000):
        raise CompilerError(f"Invalid depth: {slots.depth}")

    # completion 字段覆盖 scope 推导时发出警告
    if slots.completion and slots.scope:
        import logging
        logger = logging.getLogger(__name__)
        logger.warning(
            f"completion='{slots.completion}' overrides scope='{slots.scope}' "
            f"derived completion_policy. Final type will be derived from completion."
        )
```

注意：`scope="full"` + `completion="timeout"` 时，scope 推导的 `NONE` 策略被覆盖为 `TIMEOUT`。这不是错误——用户意图是用超时限制"全遍历"——但应让用户知道覆盖发生。

### 5.5 AI 解析入口（`src/ai/task_parser.py`）

V6.9 先占位，提供启发式规则兜底。后续 PRD 完善 AI 调用：

```python
def parse_task_to_slots(task: str, provider=None) -> IntentSlots:
    """从自然语言提取意图槽位。V6.9 使用启发式规则兜底。"""

    task_lower = task.lower()

    # 启发式提取 target_app（中英文关键词）
    target_app = None
    app_keywords = [
        "设置", "settings",
        "显示", "display", "屏幕", "screen",
        "声音", "sound", "音频", "audio",
        "网络", "network", "wifi", "蓝牙", "bluetooth",
        "存储", "storage",
        "应用", "apps", "应用程序",
        "微信", "wechat",
        "相册", "gallery", "照片", "photos",
    ]
    for kw in app_keywords:
        if kw in task_lower:
            target_app = kw
            break

    # 启发式提取 scope
    scope = "full"
    if "找到" in task or "查找" in task or "搜索" in task:
        scope = "target_only"
    elif "部分" in task or "一些" in task:
        scope = "partial"

    # 启发式提取 target
    target = None
    for marker in ["找到", "查找", "搜索", "查看"]:
        if marker in task:
            idx = task.index(marker)
            target = task[idx + len(marker):].strip().rstrip("。.！!")
            break

    return IntentSlots(
        target_app=target_app,
        scope=scope,
        target=target,
    )
```

V6.9 用启发式规则覆盖常见输入，`compiler.py` 保持纯映射不依赖 AI。

---

## 6. 修改文件清单

| 文件 | 改动类型 | 改动内容 |
|------|---------|---------|
| `src/graph/template.py` | 修改 | `instantiate()` 增加 `parent_path` 参数，拼接 `precondition.path`；`TemplateRegistry.instantiate()` 透传 |
| `src/traversal/graph_engine.py` | 修改 | `__init__` 新增 3 个字段 + `_load_template_registry()` 真实现；`_get_next_unvisited_child` 增加 DYNAMIC_MATCH 分支；`_step_once` FRAME_COMPLETE 拦截 + 路径变化检测；新增 `_generate_dynamic_children` / `invalidate_children_cache` |
| `src/graph/compiler.py` | **新建** | `PlanCompiler` 类 + `CompilerError` + 完整映射规则 + `_validate_slots()` |
| `src/ai/task_parser.py` | **新建** | `parse_task_to_slots()` 启发式兜底 |

**不改的文件**：`traversal_fsm.py`、`matcher.py`、`node.py`（`IntentSlots` 不动）

---

## 7. 扩展点

| 扩展点 | 位置 | 说明 |
|--------|------|------|
| `invalidate_children_cache(node_id)` | graph_engine.py | AUTO_ESCAPE 切换页面后调用，面向上层开放 |
| `_record_skip_span(result)` | graph_engine.py | 未匹配/跳过的元素记录 Trace Span，供调试 |
| `parse_task_to_slots()` | task_parser.py | V6.10 替换为真实 AI 调用 |
| `_find_app_icon()` 增强 | V6.8 预留 | 多页桌面/文件夹场景 |
| 模板热更新 | template.py | Spec 已定义，代码未实现 |

---

## 8. 测试矩阵

### 8.1 执行阶段

| 场景 | 输入 | 预期 |
|------|------|------|
| DYNAMIC_MATCH 首次生成 | 虚拟页面含 3 个 menu_item | `_dynamic_children[root]` 长度 = 3 |
| MenuItem → dict 字段映射 | mock PageAnalysis.items 含 type/name/coordinate | match_all 正确消费，text 字段非空 |
| 逐个取子节点 | 首次生成后多次 BRANCH | 每次返回不同 child_id，不重复 |
| 全部访问后 FRAME_COMPLETE | 3 个子节点全部访问 | `_get_next_unvisited_child` 返回 None |
| FRAME_COMPLETE 拦截 | 还剩未访问动态子节点 | 拦截成功，推入子节点，继续 NODE_SELECT |
| 路径变化触发失效 | auto_escape 后路径改变 | 缓存清空，下次 BRANCH 重新生成 |
| STATIC 路径不受影响 | STATIC 类型节点 | 行为与当前一致 |
| 路径拼接 | 父路径 `["Settings"]`，子节点 "Display" | `precondition.path = ["Settings", "Display"]` |
| 跳过元素记录 Span | menu_item 不匹配任何规则 | `_record_skip_span` 被调用 |
| page_analysis 为 None | 无 current_page_analysis | 空 items 列表，无崩溃 |
| DynamicRule → dict 转换 | dynamic_rules 含 DynamicRule 对象 | load_rules 正确消费 |

### 8.2 编译阶段

| 场景 | slots 输入 | 预期 |
|------|-----------|------|
| `full` → NONE | `scope="full"` | `completion_policy.type == NONE` |
| `partial` → MAX_STEPS | `scope="partial"` | `completion_policy.type == MAX_STEPS` |
| `target_only` + target | `scope="target_only", target="版本号"` | `type == TARGET_FOUND, target_name="版本号"` |
| `target_only` 缺少 target | `scope="target_only", target=None` | `CompilerError` |
| `target_path` 静态路径 | `scope="target_path", target="设置/显示/亮度"` | STATIC 类型 + 3 个 static_nodes，precondition.path 层层拼接 |
| `target_path` 路径拼接 | 3 段路径 | node_1.path=["设置"], node_2.path=["设置","显示"], node_3.path=["设置","显示","亮度"] |
| `full_interaction` | `element_handling="full_interaction"` | dynamic_rules 含 4 个规则 |
| `menu_only` | `element_handling="menu_only"` | dynamic_rules 仅含 menu_container |
| `safe_mode` | `element_handling="safe_mode"` | dynamic_rules 含 4 个 + `meta["safe_mode"]=True` |
| `read_only` | `element_handling="read_only"` | dynamic_rules 仅含 leaf_info |
| `navigation="back"` | `navigation="back"` | `exit_condition.fallback == BACK` |
| `navigation` 缺失 | 不传 navigation | `exit_condition.fallback == AUTO_ESCAPE` |
| `completion="timeout"` | `completion="timeout"` | `completion_policy.type == TIMEOUT` |
| `completion="steps"` | `completion="steps"` | `completion_policy.type == MAX_STEPS` |
| 缺少 target_app | `target_app=None` | `CompilerError` |
| `parse_task_to_slots` "遍历设置找到版本号" | 中文 task | `scope="target_only", target_app="设置", target="版本号"` |

---

## 9. 实施步骤

| Phase | 内容 | 文件 | 可独立验证 |
|-------|------|------|-----------|
| A | `_load_template_registry()` 真加载 | graph_engine.py | 检查 `template_registry.list_templates()` = 3 |
| B | `_get_next_unvisited_child` + `_generate_dynamic_children` | graph_engine.py | 仿真测试递归深度 > 1 |
| C | FRAME_COMPLETE 拦截 | graph_engine.py | 验证容器不会过早弹出 |
| D | 路径拼接（`instantiate` 加 `parent_path`） | template.py | 验证子节点 `precondition.path` 正确 |
| E | `invalidate_children_cache` + 路径变化检测 | graph_engine.py | 模拟 auto_escape 后缓存失效 |
| F | `PlanCompiler` 类 | compiler.py（新） | 所有映射场景单元测试 |
| G | `parse_task_to_slots` 骨架 | task_parser.py（新） | 常见中文输入提取测试 |
| H | 全链路仿真测试 | tests/v6/ | 编译 → 初始化 → 遍历全流程 |

---

## 10. 修订记录

| 日期 | 版本 | 修订内容 |
|------|------|----------|
| 2026-06-07 | V6.9.0 | 初始版本：执行阶段动态匹配集成 + 编译阶段 PlanCompiler |

---

## 11. 已知限制

| 限制 | 影响 | 后续版本 |
|------|------|----------|
| 动态子节点不支持元素滚动翻页 | 长列表只处理首屏可见项 | V6.10+ 滚动触发重新生成 |
| 启发式 `parse_task_to_slots` 精度有限 | 复杂自然语言可能提取失败 | V6.10 接入 AI provider |
| 静态路径仅支持精确匹配名称 | 需要提前知道页面名称 | V6.10+ 模糊匹配 |
| `safe_mode` 安全预筛标记无实际过滤逻辑 | `safe_mode` 行为等同于 `full_interaction` | V6.10+ 安全过滤集成 |
