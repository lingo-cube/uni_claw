# Graph模块知识库

> **Graph Knowledge Base**
> **用途**: 为多Agent测试验证提供Graph模块的专业知识
> **更新**: 2026-06-08 (V1.0)

---

## 快速导航

| Agent角色 | 需要的信息 | 位置 |
|-----------|-----------|------|
| Agent 1: 代码分析 | 类定义、方法签名、外部依赖、状态变更、不变量 | [核心组件详解](#核心组件详解-agent-1) |
| Agent 2: 文档分析 | 行为规范、参数要求、测试场景 | [行为规范与场景](#行为规范与场景-agent-2) |
| Agent 3: 场景综合 | Given/When/Then场景、Mock映射、验证清单 | [测试场景清单](#测试场景清单-agent-3) |

---

## 核心组件详解 (Agent 1)

### 1. TraversalPlan (遍历计划)

#### 类定义

```python
@dataclass
class TraversalPlan:
    """表示完整的遍历计划"""
    
    entry_app: str                          # 入口应用
    entry_strategy: EntryStrategy          # 入口策略
    root_node: TraversalNode               # 根节点
    mode: TraversalMode = TraversalMode.AUTO  # 遍历模式
    timeout: Optional[float] = None         # 超时时间
    metadata: Dict[str, Any] = field(default_factory=dict)  # 元数据
```

#### 方法签名表

| 方法 | 参数 | 返回类型 | 异常 | 外部依赖 |
|------|------|----------|------|----------|
| `validate()` | 无 | `bool` | `ValidationError` | 无 |
| `to_dict()` | 无 | `Dict[str, Any]` | 无 | 无 |
| `to_json()` | 无 | `str` | `SerializationError` | 无 |
| `from_dict()` | `data: Dict` | `TraversalPlan` | `ValidationError` | 无 |
| `from_json()` | `json_str: str` | `TraversalPlan` | `ValidationError` | 无 |
| `check_circular_refs()` | 无 | `List[str]` | 无 | 无 |

#### 状态变更

- 序列化方法不修改状态
- 验证方法不修改状态

#### 不变量

- `entry_app != ""`
- `root_node != None`
- `mode in VALID_MODES`

---

### 2. TraversalNode (遍历节点)

#### 类定义

```python
@dataclass
class TraversalNode:
    """表示遍历图中的节点"""
    
    node_id: str                           # 节点唯一标识
    node_type: NodeType                   # 节点类型
    children_strategy: ChildrenStrategy  # 子节点策略
    target: Optional[Target] = None       # 目标元素
    operation: Optional[Operation] = None # 执行操作
    restore_action: Optional[Dict] = None # 恢复动作
    completion_policy: CompletionPolicy = CompletionPolicy()  # 完成策略
    exit_condition: ExitCondition = ExitCondition()  # 退出条件
    metadata: Dict[str, Any] = field(default_factory=dict)
```

#### 方法签名表

| 方法 | 参数 | 返回类型 | 异常 | 外部依赖 |
|------|------|----------|------|----------|
| `validate()` | `parent_id: Optional[str]` | `bool` | `ValidationError` | 无 |
| `get_children()` | `context: TraversalContext` | `List[TraversalNode]` | `ResolutionError` | `context.match_results` |
| `should_complete()` | `context: TraversalContext` | `bool` | 无 | `completion_policy` |
| `get_exit_action()` | `context: TraversalContext` | `ExitAction` | 无 | `exit_condition` |

#### 节点类型枚举

| NodeType | 有效ChildrenStrategy | 有效Operation | RestoreAction |
|----------|----------------------|----------------|----------------|
| CONTAINER | STATIC, DYNAMIC_MATCH | 任意 | 可选 |
| LEAF_SWITCH | NONE | click | 必需 |
| LEAF_SLIDER | NONE | swipe | 必需 |
| LEAF_ACTION | NONE | click, input_text | 可选 |
| LEAF_INFO | NONE | no_action | N/A |
| SCREEN | STATIC, DYNAMIC_MATCH | no_action | 可选 |
| ACTION | NONE | 任意 | 可选 |
| TARGET | NONE | no_action | N/A |

#### 不变量

- `node_id != ""`
- `node_type in VALID_NODE_TYPES`
- LEAF节点: `children_strategy == NONE`
- CONTAINER节点: `children_strategy in [STATIC, DYNAMIC_MATCH]`

---

### 3. ChildrenStrategy (子节点策略)

#### 类定义

```python
@dataclass
class ChildrenStrategy:
    """定义如何获取子节点"""
    
    strategy_type: StrategyType           # 策略类型
    static_children: Optional[List[str]] = None  # 静态子节点ID列表
    dynamic_rules: Optional[Dict] = None  # 动态匹配规则
    max_children: int = 100              # 最大子节点数
```

#### 策略类型枚举

| StrategyType | static_children | dynamic_rules | 说明 |
|--------------|-----------------|---------------|------|
| STATIC | 必需 | None | 使用固定子节点列表 |
| DYNAMIC_MATCH | None | 必需 | 使用动态规则匹配 |
| NONE | None | None | 无子节点（叶子节点） |

#### 不变量

- `STATIC`: `static_children != None and len(static_children) <= max_children`
- `DYNAMIC_MATCH`: `dynamic_rules != None`
- `NONE`: `static_children == None and dynamic_rules == None`

---

### 4. Operation (操作)

#### 类定义

```python
@dataclass
class Operation:
    """定义在节点上执行的操作"""
    
    action: str                             # 动作类型
    target: Optional[Target] = None        # 目标元素
    params: Dict[str, Any] = field(default_factory=dict)
```

#### 动作类型

| action | target要求 | params示例 |
|--------|-----------|-----------|
| click | Target必需 | {} |
| swipe | Target(by=coordinate)必需 | {"direction": "up", "distance": 0.5} |
| input_text | Target(by=text)必需 | {"text": "hello"} |
| back | Target=None | {} |
| no_action | Target=None | {} |

---

### 5. Target (目标)

#### 类定义

```python
@dataclass
class Target:
    """定义操作的目标元素"""
    
    by: TargetBy                          # 定位方式
    value: Any                            # 定位值
```

#### TargetBy枚举

| TargetBy | value类型 | 示例 |
|----------|----------|------|
| text | str | "Settings" |
| coordinate | Tuple[float, float] | (0.5, 0.5) |
| ui_index | int | 0 |
| resource_id | str | "com.app:id/button" |
| xpath | str | "//Button[@text='Settings']" |

---

### 6. Template (模板)

#### 类定义

```python
@dataclass
class Template:
    """节点模板，用于快速创建节点"""
    
    template_id: str                      # 模板ID
    node_schema: Dict[str, Any]           # 节点结构
    required_placeholders: List[str]      # 必需的占位符
```

#### 内置模板

| template_id | required_placeholders | 说明 |
|-------------|---------------------|------|
| menu_container | item_text | 菜单项容器 |
| switch_leaf | item_text, item_index | 开关叶子节点 |
| slider_leaf | coordinate | 滑块叶子节点 |
| input_leaf | item_text, hint_text | 输入框叶子节点 |

---

### 7. PlaceholderResolver (占位符解析器)

#### 类定义

```python
class PlaceholderResolver:
    """解析模板中的占位符"""
    
    PLACEHOLDER_PATTERN = r'\{\{(\w+)\}\}'
    
    @staticmethod
    def resolve(template: str, context: Dict[str, Any]) -> str:
        """解析模板中的占位符"""
        
    @staticmethod
    def resolve_dict(data: Dict, context: Dict[str, Any]) -> Dict:
        """递归解析字典中的占位符"""
```

#### 方法签名表

| 方法 | 参数 | 返回类型 | 异常 |
|------|------|----------|------|
| `resolve()` | `template: str, context: Dict` | `str` | `ResolutionError` |
| `resolve_dict()` | `data: Dict, context: Dict` | `Dict` | `ResolutionError` |

---

### 8. DynamicMatcher (动态匹配器)

#### 类定义

```python
class DynamicMatcher:
    """根据规则动态匹配UI元素"""
    
    def __init__(self, rules: Dict[str, Any]):
        self.rules = rules
        
    def match(self, context: TraversalContext) -> List[MatchResult]:
        """执行动态匹配"""
```

#### 匹配规则类型

| 规则类型 | 说明 | 示例 |
|----------|------|------|
| text_match | 文本匹配 | {"text": "Settings"} |
| class_match | 类名匹配 | {"class": "android.widget.Button"} |
| id_match | ID匹配 | {"resource_id": "com.app:id/button"} |
| composite_match | 组合匹配 | {"and": [{"text": "Settings"}, {"class": "Button"}]} |

---

## 外部依赖清单

### Mock映射表

| 组件 | 方法/属性 | Mock要求 | 返回值设置 |
|------|-----------|----------|------------|
| **TraversalContext** | `current_screen` | Mock或真实 | Mock(screen_info) |
| **TraversalContext** | `match_results` | Mock或真实 | [MatchResult(...)] |
| **UIElementFinder** | `find_elements()` | 必须Mock | [MockElement(...)] |
| **ScreenCapturer** | `capture()` | 可选Mock | Mock(screen_image) |
| **ElementMatcher** | `match()` | 可选Mock | bool |

---

## 行为规范与场景 (Agent 2)

### Should 规范列表

| ID | 规范 | 适用组件 |
|----|------|----------|
| **GR-001** | entry_app不能为空 | TraversalPlan |
| **GR-002** | root_node必须存在 | TraversalPlan |
| **GR-003** | LEAF节点不能有子节点 | TraversalNode |
| **GR-004** | CONTAINER节点必须有子节点策略 | TraversalNode |
| **GR-005** | STATIC策略必须有static_children | ChildrenStrategy |
| **GR-006** | DYNAMIC_MATCH策略必须有dynamic_rules | ChildrenStrategy |
| **GR-007** | node_id必须唯一 | TraversalNode |
| **GR-008** | 序列化后能反序列化回原对象 | TraversalPlan |
| **GR-009** | 模板占位符必须全部解析 | Template |
| **GR-010** | 动态匹配结果必须去重 | DynamicMatcher |

### Should_Not 规范列表

| ID | 规范 | 适用组件 |
|----|------|----------|
| **GR-N01** | 不允许循环节点引用 | TraversalNode |
| **GR-N02** | 不允许无效的节点类型组合 | TraversalNode |
| **GR-N03** | 不允许未解析的占位符 | PlaceholderResolver |
| **GR-N04** | 不允许超过max_children限制 | ChildrenStrategy |
| **GR-N05** | 不允许TARGET节点有子节点 | TraversalNode |

---

## 测试场景清单 (Agent 3)

### 核心场景映射表

| 场景ID | 类型 | 组件 | 方法 | 规范ID | 优先级 |
|--------|------|------|------|--------|--------|
| **GR-001** | normal | TraversalPlan | from_dict | GR-001, GR-002 | P1 |
| **GR-002** | boundary | TraversalNode | validate | GR-003, GR-004 | P1 |
| **GR-003** | normal | ChildrenStrategy | validate | GR-005, GR-006 | P1 |
| **GR-004** | error | TraversalPlan | check_circular_refs | GR-N01 | P1 |
| **GR-005** | normal | PlaceholderResolver | resolve | GR-009 | P1 |
| **GR-006** | boundary | DynamicMatcher | match | GR-010 | P2 |
| **GR-007** | error | Template | instantiate | GR-003 | P2 |
| **GR-008** | normal | TraversalPlan | to_json/from_json | GR-008 | P1 |

---

### 结构化场景详情

#### GR-001: 创建最小遍历计划

**类型**: normal  
**组件**: TraversalPlan  
**方法**: from_dict  
**规范**: GR-001, GR-002

**Given**:
```python
plan_data = {
    "entry_app": "com.example.app",
    "entry_strategy": {"type": "COLD_LAUNCH"},
    "root_node": {
        "node_id": "root",
        "node_type": "SCREEN",
        "children_strategy": {"strategy_type": "NONE"}
    }
}
```

**When**:
```python
plan = TraversalPlan.from_dict(plan_data)
```

**Then**:
```python
assert plan.entry_app == "com.example.app"
assert plan.root_node.node_id == "root"
assert plan.root_node.node_type == NodeType.SCREEN
```

**需要的Mock**:
- 无

**验证的副作用**:
- 无副作用（对象创建）

**检查的不变量**:
- `plan.entry_app != ""`
- `plan.root_node != None`

---

#### GR-002: LEAF节点验证

**类型**: boundary  
**组件**: TraversalNode  
**方法**: validate  
**规范**: GR-003

**Given**:
```python
# LEAF_SWITCH节点不应该有子节点
leaf_node = TraversalNode(
    node_id="switch1",
    node_type=NodeType.LEAF_SWITCH,
    children_strategy=ChildrenStrategy(strategy_type=StrategyType.STATIC, static_children=["child1"])
)
```

**When**:
```python
is_valid = leaf_node.validate()
```

**Then**:
```python
assert is_valid == False
# 应该抛出ValidationError: LEAF节点不能有子节点
```

**需要的Mock**:
- 无

**验证的副作用**:
- 无副作用（验证操作）

**检查的不变量**:
- LEAF节点: `children_strategy == NONE`

---

#### GR-003: 循环引用检测

**类型**: error  
**组件**: TraversalPlan  
**方法**: check_circular_refs  
**规范**: GR-N01

**Given**:
```python
# A → B → A (循环引用)
node_a = TraversalNode(node_id="a", node_type=NodeType.CONTAINER, ...)
node_b = TraversalNode(node_id="b", node_type=NodeType.CONTAINER, ...)
# 设置循环引用
plan_data = {
    "root_node": {
        "node_id": "a",
        "static_children": ["b"]
    },
    "static_nodes": {
        "a": {...},
        "b": {
            "node_id": "b",
            "static_children": ["a"]  # 循环回a
        }
    }
}
```

**When**:
```python
plan = TraversalPlan.from_dict(plan_data)
circular_refs = plan.check_circular_refs()
```

**Then**:
```python
assert len(circular_refs) > 0
assert "a" in circular_refs
assert "b" in circular_refs
# 应该在验证时抛出ValidationError
```

**需要的Mock**:
- 无

**验证的副作用**:
- 无副作用（检测操作）

**检查的不变量**:
- 图中无环

---

#### GR-004: 占位符解析

**类型**: normal  
**组件**: PlaceholderResolver  
**方法**: resolve  
**规范**: GR-009

**Given**:
```python
template = "Click on {{item_text}} button at index {{item_index}}"
context = {
    "item_text": "Settings",
    "item_index": 2
}
```

**When**:
```python
resolved = PlaceholderResolver.resolve(template, context)
```

**Then**:
```python
assert resolved == "Click on Settings button at index 2"
assert "{{" not in resolved  # 所有占位符已解析
```

**需要的Mock**:
- 无

**验证的副作用**:
- 无副作用（纯函数）

**检查的不变量**:
- 输出中无未解析占位符

---

#### GR-005: 序列化/反序列化

**类型**: normal  
**组件**: TraversalPlan  
**方法**: to_json/from_json  
**规范**: GR-008

**Given**:
```python
original_plan = TraversalPlan(
    entry_app="com.example.app",
    entry_strategy=EntryStrategy(type=EntryStrategyType.COLD_LAUNCH),
    root_node=TraversalNode(...)
)
```

**When**:
```python
json_str = original_plan.to_json()
restored_plan = TraversalPlan.from_json(json_str)
```

**Then**:
```python
assert restored_plan.entry_app == original_plan.entry_app
assert restored_plan.root_node.node_id == original_plan.root_node.node_id
# 验证所有字段都正确恢复
```

**需要的Mock**:
- 无

**验证的副作用**:
- 无副作用（纯序列化）

**检查的不变量**:
- 反序列化后的对象等于原对象

---

## 边界条件场景

| 场景ID | 类型 | 组件 | 边界条件 |
|--------|------|------|----------|
| **GR-B001** | boundary | TraversalNode | max_depth=100 |
| **GR-B002** | boundary | ChildrenStrategy | max_children=100 |
| **GR-B003** | boundary | PlaceholderResolver | 空context |
| **GR-B004** | boundary | DynamicMatcher | 空匹配结果 |
| **GR-B005** | boundary | Template | 递归深度=10 |

---

## 错误场景

| 场景ID | 类型 | 组件 | 错误条件 |
|--------|------|------|----------|
| **GR-E001** | error | TraversalPlan | entry_app="" |
| **GR-E002** | error | TraversalPlan | root_node=None |
| **GR-E003** | error | TraversalNode | node_type=INVALID |
| **GR-E004** | error | PlaceholderResolver | 缺少必需占位符 |
| **GR-E005** | error | Template | template_id不存在 |

---

## 完整Mock配置模板

```python
@pytest.fixture
def graph_test_setup():
    """
    完整的Graph模块测试Mock配置
    """
    # Mock TraversalContext
    mock_context = Mock(spec=TraversalContext)
    mock_context.current_screen = Mock()
    mock_context.match_results = []
    
    # Mock UIElementFinder
    mock_finder = Mock(spec=UIElementFinder)
    mock_element = Mock()
    mock_element.text = "Settings"
    mock_element.resource_id = "com.app:id/settings"
    mock_finder.find_elements.return_value = [mock_element]
    
    # Mock ScreenCapturer
    mock_capturer = Mock(spec=ScreenCapturer)
    mock_capturer.capture.return_value = Mock()
    
    # Mock ElementMatcher
    mock_matcher = Mock(spec=ElementMatcher)
    mock_matcher.match.return_value = True
    
    # 示例节点
    sample_node = TraversalNode(
        node_id="settings",
        node_type=NodeType.LEAF_ACTION,
        children_strategy=ChildrenStrategy(strategy_type=StrategyType.NONE),
        operation=Operation(action="click", target=Target(by=TargetBy.TEXT, value="Settings"))
    )
    
    return {
        'context': mock_context,
        'finder': mock_finder,
        'capturer': mock_capturer,
        'matcher': mock_matcher,
        'sample_node': sample_node
    }
```

---

## JSON摘要 (供Agent解析)

```json
{
  "module": "graph",
  "version": "1.0",
  "classes": [
    {
      "name": "TraversalPlan",
      "methods": ["validate", "to_dict", "to_json", "from_dict", "from_json", "check_circular_refs"],
      "state_vars": ["entry_app", "entry_strategy", "root_node", "mode", "timeout", "metadata"]
    },
    {
      "name": "TraversalNode",
      "methods": ["validate", "get_children", "should_complete", "get_exit_action"],
      "state_vars": ["node_id", "node_type", "children_strategy", "target", "operation", "restore_action", "completion_policy", "exit_condition"]
    },
    {
      "name": "ChildrenStrategy",
      "methods": ["validate"],
      "state_vars": ["strategy_type", "static_children", "dynamic_rules", "max_children"]
    },
    {
      "name": "Operation",
      "methods": [],
      "state_vars": ["action", "target", "params"]
    },
    {
      "name": "Target",
      "methods": [],
      "state_vars": ["by", "value"]
    },
    {
      "name": "PlaceholderResolver",
      "methods": ["resolve", "resolve_dict"],
      "state_vars": []
    },
    {
      "name": "DynamicMatcher",
      "methods": ["match"],
      "state_vars": ["rules"]
    }
  ],
  "external_dependencies": [
    {"component": "TraversalContext", "attributes": ["current_screen", "match_results"]},
    {"component": "UIElementFinder", "methods": ["find_elements"]},
    {"component": "ScreenCapturer", "methods": ["capture"]},
    {"component": "ElementMatcher", "methods": ["match"]}
  ],
  "scenarios": [
    {"id": "GR-001", "type": "normal", "priority": "P1"},
    {"id": "GR-002", "type": "boundary", "priority": "P1"},
    {"id": "GR-003", "type": "normal", "priority": "P1"},
    {"id": "GR-004", "type": "error", "priority": "P1"},
    {"id": "GR-005", "type": "normal", "priority": "P1"},
    {"id": "GR-006", "type": "boundary", "priority": "P2"}
  ],
  "behaviors": {
    "should": ["GR-001", "GR-002", "GR-003", "GR-004", "GR-005", "GR-006", "GR-007", "GR-008", "GR-009", "GR-010"],
    "should_not": ["GR-N01", "GR-N02", "GR-N03", "GR-N04", "GR-N05"]
  }
}
```

---

## 相关文档

- **设计文档**: `docs/architecture/modules/graph-design.md`
- **测试场景**: `docs/testing/GRAPH_TEST_SCENARIOS.md`

---

**维护者**: Uni-Claw Development Team  
**版本**: V1.0  
**更新频率**: 随模块更新同步更新
