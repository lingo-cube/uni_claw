# Uni-Claw 核心业务模型规范 PRD

> **文档版本**: V1.2
> **创建日期**: 2026-06-01
> **最后更新**: 2026-06-02
> **状态**: 活跃文档
> **类型**: 数据模型规范文档

---

## 文档说明

本文档定义 Uni-Claw 系统**已实现**的核心业务数据模型，涵盖以下七个领域：

1. **页面分析模型** - 屏幕分析的结构化输出
2. **图节点模型** - 遍历操作的节点抽象
3. **内容树模型** - 遍历结果的树形结构
4. **状态机模型** - 全局与遍历状态管理
5. **运行时上下文模型** - 遍历执行过程的上下文
6. **异常处理模型** - 异常上下文与处理结果
7. **AI 能力模型** - AI 服务的数据结构

**本文档仅包含已实现的模型**，未实现的设计模型已移除。

---

## 枚举类型辅助方法

本文档中所有枚举类型均提供统一的辅助方法，便于使用和验证：

### 通用方法

所有枚举类型（继承自 `str` 和 `Enum`）均提供以下三个类方法：

#### `values() -> List[str]`

获取所有枚举值的列表（字符串形式）。

```python
# 示例：获取所有方向值
directions = Direction.values()
# 返回: ["left", "right", "top", "bottom"]

# 示例：获取所有菜单项类型
types = MenuItemType.values()
# 返回: ["menu_item", "tab", "switch", "toggle", "button", ...]
```

#### `from_value(value: str) -> EnumType`

从字符串值创建枚举实例。如果值无效，抛出 `ValueError` 并提示有效值列表。

```python
# 示例：从字符串创建枚举
direction = Direction.from_value("left")
# 返回: Direction.LEFT

# 无效值会抛出异常
try:
    Direction.from_value("invalid")
except ValueError as e:
    # 错误消息: "Invalid Direction value: invalid. Valid values: ['left', 'right', 'top', 'bottom']"
    print(e)
```

#### `is_valid(value: str) -> bool`

验证字符串值是否为有效的枚举值。

```python
# 示例：验证枚举值
Direction.is_valid("left")   # 返回: True
Direction.is_valid("up")     # 返回: False
```

### 使用场景

这些辅助方法特别适用于：

1. **前端集成**：`values()` 方法返回字符串列表，便于前端构建下拉选项
2. **配置验证**：`is_valid()` 方法用于前置条件验证
3. **数据转换**：`from_value()` 方法将字符串配置转换为枚举实例
4. **错误提示**：`from_value()` 提供友好的错误消息，包含所有有效值

### 实现示例

```python
class MenuItemType(str, Enum):
    MENU_ITEM = "menu_item"
    TAB = "tab"
    # ... 其他值

    @classmethod
    def values(cls) -> list[str]:
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "MenuItemType":
        try:
            return cls(value)
        except ValueError as e:
            raise ValueError(
                f"Invalid {cls.__name__} value: {value}. "
                f"Valid values: {cls.values()}"
            ) from e

    @classmethod
    def is_valid(cls, value: str) -> bool:
        return value in cls.values()
```

### 支持的枚举类型

以下枚举类型均支持上述辅助方法：

**页面分析模型**：
- `Direction` - 菜单排列方向
- `MenuItemType` - 菜单项类型
- `ExpectedAction` - 预期操作类型

**图节点模型**：
- `NodeType` - 节点类型
- `ChildrenStrategyType` - 子节点策略类型

**状态机模型**：
- `GlobalState` - 全局状态
- `TraversalState` - 遍历状态

**异常处理模型**：
- `ExceptionSeverity` - 异常严重程度
- `ExceptionAction` - 异常处理动作
- `RecoveryAction` - 恢复动作

**AI 能力模型**：
- `DecisionResult` - AI 决策结果
- `ExecutionStatus` - 执行状态

---

## 测试覆盖

本文档中的所有核心业务模型均配有全面的单元测试，确保实现的正确性和稳定性。

### 测试组织

测试代码位于 `tests/models/` 目录，按模型模块组织：

```
tests/models/
├── test_content_tree.py      # 页面分析模型测试
├── test_graph_nodes.py       # 图节点模型测试
├── test_state_machine.py     # 状态机模型测试
├── test_context.py           # 运行时上下文模型测试
├── test_exception.py         # 异常处理模型测试
├── test_ai_types.py          # AI 能力模型测试
├── test_trace.py             # Trace 模型测试
└── test_enums.py             # 枚举辅助方法统一测试
```

### 测试标准

每个模型的测试覆盖以下方面：

1. **字段验证**：必填字段、类型检查、值范围、默认值
2. **序列化**：`to_dict()` / `to_json()`（如适用）
3. **反序列化**：`from_dict()` / `from_json()`（如适用）
4. **边界条件**：空值、极端值、无效值
5. **枚举专属**：`values()`、`from_value()`、`is_valid()`

### 覆盖率目标

- **核心模型**：80%+ 覆盖率（PageAnalysis、TraversalNode、TraversalContext 等）
- **辅助模型**：60%+ 覆盖率（MenuInfo、Coordinate、枚举等）

### 当前状态

截至 2026-06-02，测试套件包含 **252 个测试用例**，全部通过。

```bash
# 运行所有模型测试
pytest tests/models/ -v

# 运行结果
252 passed, 1 warning in 1.34s
```

### 运行测试

```bash
# 运行所有模型测试
pytest tests/models/

# 运行特定模块测试
pytest tests/models/test_content_tree.py

# 运行特定枚举测试
pytest tests/models/test_enums.py::TestDirection

# 生成覆盖率报告
pytest tests/models/ --cov=src --cov-report=term-missing
```

---

# 1. 页面分析模型

页面分析模型描述从屏幕截图到结构化页面分析的数据结构。

## 1.1 基础类型

### Direction (枚举)

菜单排列方向。

```python
class Direction(str, Enum):
    LEFT = "left"      # 左侧菜单
    RIGHT = "right"    # 右侧菜单
    TOP = "top"        # 顶部菜单
    BOTTOM = "bottom"  # 底部菜单
```

**实现位置**: `src/state/content_tree.py:10`

---

### Coordinate

归一化坐标点 (0-1 范围)。

```python
class Coordinate(BaseModel):
    x: float = Field(ge=0.0, le=1.0, description="X coordinate (normalized 0-1)")
    y: float = Field(ge=0.0, le=1.0, description="Y coordinate (normalized 0-1)")
```

**实现位置**: `src/state/content_tree.py:19`

**使用场景**：
- 记录菜单项的点击位置
- 存储滑块、开关的交互坐标
- 作为 ADB 点击操作的输入

---

## 1.2 菜单与元素

### MenuInfo

一级菜单或二级标签页中的一条目。

```python
class MenuInfo(BaseModel):
    name: str              # 菜单名称
    coordinate: Coordinate  # 菜单坐标
    active: bool = False   # 是否为当前激活的菜单项
```

**实现位置**: `src/state/content_tree.py:26`

---

### MenuItemType (枚举)

可交互元素的精确控件类型。

```python
class MenuItemType(str, Enum):
    # Navigation types
    MENU_ITEM = "menu_item"      # Clickable menu item (list item)
    TAB = "tab"                  # Tab button
    BACK_BUTTON = "back_button"  # Back navigation button

    # Action types
    SWITCH = "switch"            # Switch/toggle (changes state)
    TOGGLE = "toggle"            # Toggle button (on/off state)
    BUTTON = "button"            # Generic button (triggers action)

    # Other types
    ICON = "icon"                # Icon
    LINK = "link"                # Link/navigation
    TEXT = "text"                # Plain text
    READONLY = "readonly"        # Read-only element

    # Legacy compatibility
    ITEM = "item"                # Legacy: equivalent to MENU_ITEM
```

**实现位置**: `src/state/content_tree.py:34`

**设计说明**：
- 扩展了基础类型以支持更细粒度的按钮分类
- `BACK_BUTTON` 用于特殊处理返回导航
- `ITEM` 保留用于向后兼容

---

### ExpectedAction (枚举)

元素被点击后的预期行为。

```python
class ExpectedAction(str, Enum):
    NAVIGATE = "navigate"  # Expects page navigation (menu, tab)
    TOGGLE = "toggle"      # Expects state change (switch)
    ACTION = "action"      # Expects action trigger (popup, jump)
    NONE = "none"          # No expected response (read-only)
```

**实现位置**: `src/state/content_tree.py:61`

---

### MenuItem

页面上的一个可交互或可识别的元素。

```python
class MenuItem(BaseModel):
    name: str
    type: MenuItemType = Field(default=MenuItemType.ITEM)
    coordinate: Coordinate
    parent: Optional[str] = None         # 父元素名称
    description: Optional[str] = None

    # Expected behavior fields
    expected_action: ExpectedAction = Field(
        default=ExpectedAction.ACTION,
        description="Expected button behavior (navigate/toggle/action/none)",
    )
    expects_page_change: bool = Field(
        default=False,
        description="Whether clicking should change the current page path",
    )
    expects_state_change: bool = Field(
        default=False,
        description="Whether clicking should change UI state (toggle, etc.)",
    )
```

**实现位置**: `src/state/content_tree.py:73`

**扩展字段说明**：
- `expected_action`: 预期行为，用于确定等待时间和验证策略
- `expects_page_change`: 点击是否应改变当前页面路径
- `expects_state_change`: 点击是否应改变 UI 状态

---

### PopupInfo

弹窗信息。

```python
class PopupInfo(BaseModel):
    title: Optional[str] = None
    content: Optional[str] = None          # 弹窗内容
    close_button: Optional[Coordinate] = None  # 关闭按钮位置
```

**实现位置**: `src/state/content_tree.py:109`

---

## 1.3 完整页面分析

### PageAnalysis

完整的页面结构分析结果。

```python
class PageAnalysis(BaseModel):
    # Menu structure
    level1_dir: Direction
    level1_menus: list[MenuInfo]
    level2_dir: Direction
    level2_menus: list[MenuInfo]

    # Current location
    current_path: list[str]

    # Content items
    items: list[MenuItem]

    # Special elements
    is_popup: bool = False
    popup_info: Optional[PopupInfo] = None
    close_button: Optional[Coordinate] = None
    back_button: Optional[Coordinate] = None

    # Navigation hints
    has_scroll: bool = False
    is_end_of_list: bool = False
```

**实现位置**: `src/state/content_tree.py:117`

**current_path 示例**：
```python
["车辆设置", "DiLink", "互联"]
```

---

# 2. 图节点模型

图节点模型描述遍历操作的节点抽象，是遍历图的基本单元。

## 2.1 节点类型

### NodeType (枚举)

遍历节点的类型。

```python
class NodeType(str, Enum):
    CONTAINER = "container"      # Can expand to show children
    LEAF_SWITCH = "leaf_switch"  # Switch/toggle control
    LEAF_SLIDER = "leaf_slider"  # Slider control
    LEAF_ACTION = "leaf_action"  # Action button
    LEAF_INFO = "leaf_info"      # Information display
```

**实现位置**: `src/graph/node.py:13`

---

## 2.2 操作定义

### OperationAction (枚举)

操作动作类型。

```python
# Operation.action 可选值
"click" | "swipe" | "back" | "input_text" | "no_action"
```

**定义位置**: `src/graph/node.py:66` (通过 Operation.__post_init__ 验证)

---

### TargetBy (枚举)

目标定位方式。

```python
# Target.by 可选值
"text" | "coordinate" | "ui_index"
```

**定义位置**: `src/graph/node.py:38` (通过 Target.__post_init__ 验证)

---

### Target

操作目标定位描述。

```python
@dataclass
class Target:
    by: str              # "text", "coordinate", "ui_index"
    value: Any            # 定位值
    meta: Dict[str, Any] = field(default_factory=dict)
```

**实现位置**: `src/graph/node.py:24`

---

### RestoreAction

恢复操作定义。

```python
@dataclass
class RestoreAction:
    action: str                           # 恢复动作类型
    target: Optional[Target] = None
    params: Dict[str, Any] = field(default_factory=dict)
```

**实现位置**: `src/graph/node.py:46`

---

### Operation

描述一次具体的 UI 操作。

```python
@dataclass
class Operation:
    action: str                           # "click", "swipe", "back", "input_text", "no_action"
    target: Optional[Target] = None
    params: Dict[str, Any] = field(default_factory=dict)
    restore: Optional[RestoreAction] = None
```

**实现位置**: `src/graph/node.py:59`

---

## 2.3 节点定义

### Precondition

节点执行前必须满足的页面状态。

```python
@dataclass
class Precondition:
    page_name: Optional[str] = None
    path: Optional[List[str]] = None
    ui_condition: Optional[str] = None
    timeout_seconds: float = 5.0          # 等待条件满足的超时时间
```

**实现位置**: `src/graph/node.py:79`

---

## 2.4 子节点策略

### ChildrenStrategyType (枚举)

```python
class ChildrenStrategyType(str, Enum):
    STATIC = "static"             # 使用预定义的静态子节点列表
    DYNAMIC_MATCH = "dynamic_match"  # 运行时动态匹配
    NONE = "none"                 # 无子节点（叶子节点）
```

**实现位置**: `src/graph/node.py:93`

---

### DynamicRule

一条动态匹配规则。

```python
@dataclass
class DynamicRule:
    rule_id: str
    match_condition: Dict[str, Any]
    child_template: str
    action: str = "generate_child"  # "generate_child", "skip", "execute_inline"
```

**实现位置**: `src/graph/node.py:102`

---

### ChildrenStrategy

子节点生成策略。

```python
@dataclass
class ChildrenStrategy:
    type: ChildrenStrategyType
    static_children: List[str] = field(default_factory=list)
    dynamic_rules: Dict[str, DynamicRule] = field(default_factory=dict)
    max_children: int = 100          # 最大子节点数量（安全限制）
```

**实现位置**: `src/graph/node.py:117`

---

## 2.5 错误策略

### ErrorPolicy

```python
@dataclass
class ErrorPolicy:
    on_error: str                    # "retry", "skip", "abort", "fallback"
    max_retries: int = 1
    fallback_target: Optional[str] = None
    continue_on_error: bool = False
```

**实现位置**: `src/graph/node.py:131`

---

## 2.6 完整节点

### TraversalNode

遍历图中的一个节点。

```python
@dataclass
class TraversalNode:
    node_id: str
    name: str
    node_type: NodeType
    operation: Operation
    precondition: Optional[Precondition] = None
    children_strategy: ChildrenStrategy = field(
        default_factory=lambda: ChildrenStrategy(type=ChildrenStrategyType.NONE)
    )
    error_policy: Optional[ErrorPolicy] = None
    meta: Dict[str, Any] = field(default_factory=dict)
```

**实现位置**: `src/graph/node.py:145`

**辅助方法**：
- `is_container()`: 是否为容器节点
- `is_leaf()`: 是否为叶子节点
- `has_precondition()`: 是否有前置条件
- `needs_restore()`: 是否需要恢复操作

---

# 3. 内容树模型

内容树模型描述遍历结果的树形结构存储。

## 3.1 树节点

### ContentNode

内容树中的一个节点。

```python
class ContentNode(BaseModel):
    id: str                           # 节点 ID
    title: str                        # 节点标题
    level: int                        # 层级深度
    parent_id: Optional[str] = None
    children: list[str] = Field(default_factory=list)
    coordinate: Optional[Coordinate] = None
    node_type: str = "item"           # item, popup, jump, no_feedback
    description: Optional[str] = None
    visited: bool = False
```

**实现位置**: `src/state/content_tree.py:143`

---

## 3.2 内容树

### ContentTree

树结构存储已发现的全部内容。

```python
class ContentTree(BaseModel):
    root_title: str = "Root"
    nodes: dict[str, ContentNode] = Field(default_factory=dict)
    level_counters: dict[int, int] = Field(default_factory=dict, alias="_level_counters")
```

**实现位置**: `src/state/content_tree.py:169`

**主要方法**：
- `add_node()`: 添加新节点
- `add_child_node()`: 添加子节点（自动生成层级 ID）
- `mark_visited()`: 标记节点已访问
- `to_markdown()`: 导出为 Markdown 格式

---

## 3.3 访问追踪

### VisitFingerprint

用于追踪已访问元素的指纹。

```python
class VisitFingerprint(BaseModel):
    level1: str
    level2: str
    item_name: str
```

**实现位置**: `src/state/content_tree.py:282`

**指纹格式**: `"{level1}|{level2}|{item_name}"`

---

## 3.4 持久化状态

### TraversalState

完整的遍历状态（用于持久化）。

```python
class TraversalState(BaseModel):
    # Current location
    current_path: list[str] = Field(default_factory=list)

    # Visited tracking
    visited: set[str] = Field(default_factory=set)

    # Caches
    all_level1_menus: dict[str, MenuInfo] = Field(default_factory=dict)
    level2_menus_cache: dict[str, list[MenuInfo]] = Field(default_factory=dict)
    items_cache: dict[str, list[MenuItem]] = Field(default_factory=dict)

    # Content tree
    content_tree: ContentTree = Field(default_factory=ContentTree)

    # Progress tracking
    step_count: int = 0
    current_phase: str = "initialized"

    # Error recovery
    consecutive_errors: int = 0
    last_error: Optional[str] = None

    # Target info
    target_app: Optional[str] = None

    # Exception history
    exception_history_records: list[dict] = Field(default_factory=list, alias="_exception_history_records")

    # Graph mode support
    node_stack: list[dict] = Field(default_factory=list, alias="_node_stack")
    current_node_id: Optional[str] = None
    use_graph_mode: bool = False
```

**实现位置**: `src/state/content_tree.py:302`

**注意**: 此 `TraversalState` 用于持久化，与状态机中的 `TraversalState` 枚举不同。

---

# 4. 状态机模型

状态机模型描述全局和遍历两个层级的状态管理。

## 4.1 全局状态机

### GlobalState (枚举)

全局状态机的状态。

```python
class GlobalState(str, Enum):
    IDLE = "idle"                    # 等待任务开始
    INITIALIZING = "initializing"    # 加载遍历计划和上下文
    TRAVERSING = "traversing"        # 活跃遍历中
    PAUSED = "paused"                # 任务暂停（可恢复）
    ERROR = "error"                  # 发生错误
    RECOVERING = "recovering"        # 尝试恢复中
    COMPLETED = "completed"          # 成功完成
    TERMINATED = "terminated"        # 终止（不可恢复）
```

**实现位置**: `src/state_machine/global_fsm.py:14`

---

### GlobalStateTransition

全局状态转换记录。

```python
@dataclass
class GlobalStateTransition:
    from_state: GlobalState
    to_state: GlobalState
    timestamp: datetime = field(default_factory=datetime.now)
    reason: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)
```

**实现位置**: `src/state_machine/global_fsm.py:28`

---

### GlobalStateMachine

全局状态机实现。

```python
class GlobalStateMachine:
    VALID_TRANSITIONS = {
        GlobalState.IDLE: {GlobalState.INITIALIZING},
        GlobalState.INITIALIZING: {GlobalState.TRAVERSING, GlobalState.ERROR},
        GlobalState.TRAVERSING: {GlobalState.PAUSED, GlobalState.ERROR, GlobalState.COMPLETED},
        GlobalState.PAUSED: {GlobalState.TRAVERSING, GlobalState.TERMINATED},
        GlobalState.ERROR: {GlobalState.RECOVERING, GlobalState.TERMINATED},
        GlobalState.RECOVERING: {GlobalState.INITIALIZING, GlobalState.TERMINATED},
        GlobalState.COMPLETED: set(),
        GlobalState.TERMINATED: set(),
    }
```

**实现位置**: `src/state_machine/global_fsm.py:38`

**属性**：
- `state`: 当前状态
- `is_active`: 是否处于活跃状态
- `is_terminal`: 是否处于终止状态
- `is_paused`: 是否暂停
- `error_context`: 错误上下文

---

## 4.2 遍历状态机

### TraversalState (枚举)

遍历状态机的状态。

```python
class TraversalState(str, Enum):
    NODE_SELECT = "node_select"          # 选择下一个待处理节点
    PRECONDITION_CHECK = "precondition_check"  # 验证前置条件
    EXECUTE = "execute"                  # 执行节点操作
    RESULT_VERIFY = "result_verify"      # 验证执行结果
    BRANCH = "branch"                    # 确定下一步动作
```

**实现位置**: `src/state_machine/traversal_fsm.py:14`

**注意**: 与持久化的 `TraversalState` 不同，这是状态机枚举。

---

### TraversalStateTransition

遍历状态转换记录。

```python
@dataclass
class TraversalStateTransition:
    from_state: TraversalState
    to_state: TraversalState
    timestamp: datetime = field(default_factory=datetime.now)
    node_id: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)
```

**实现位置**: `src/state_machine/traversal_fsm.py:25`

---

### TraversalStateMachine

遍历状态机实现。

```python
class TraversalStateMachine:
    VALID_TRANSITIONS = {
        TraversalState.NODE_SELECT: {TraversalState.PRECONDITION_CHECK, TraversalState.BRANCH},
        TraversalState.PRECONDITION_CHECK: {TraversalState.EXECUTE, TraversalState.BRANCH},
        TraversalState.EXECUTE: {TraversalState.RESULT_VERIFY, TraversalState.BRANCH},
        TraversalState.RESULT_VERIFY: {TraversalState.BRANCH},
        TraversalState.BRANCH: {TraversalState.NODE_SELECT, TraversalState.PRECONDITION_CHECK},
    }
```

**实现位置**: `src/state_machine/traversal_fsm.py:35`

---

## 4.3 节点栈

### StackFrame

节点栈中的一帧。

```python
@dataclass
class StackFrame:
    node: TraversalNode
    child_queue: List[str] = field(default_factory=list)
    current_child_idx: int = 0
    pending_restore: bool = False
    entered_at: datetime = field(default_factory=datetime.now)
    metadata: Dict[str, Any] = field(default_factory=dict)
```

**实现位置**: `src/state_machine/node_stack.py:16`

**属性**：
- `node_id`: 节点 ID
- `has_children`: 是否有子节点待处理
- `remaining_children`: 剩余子节点数量
- `is_complete`: 是否所有子节点已处理
- `duration`: 进入该帧后的持续时间

---

### NodeStack

深度优先遍历的节点栈。

```python
class NodeStack:
    DEFAULT_MAX_DEPTH = 10
```

**实现位置**: `src/state_machine/node_stack.py:88`

**属性**：
- `is_empty`: 栈是否为空
- `size`: 当前栈大小
- `depth`: 当前深度
- `max_depth`: 最大允许深度
- `depth_limit_reached`: 是否达到深度限制

**方法**：
- `push()`: 压入新帧
- `pop()`: 弹出顶帧
- `top()`: 获取顶帧（不弹出）
- `peek(offset)`: 偏移查看帧

---

# 5. 运行时上下文模型

运行时上下文模型描述遍历执行过程中的上下文信息。

## 5.1 AI 上下文

### TraversalContext

传递给 AI 顾问的只读运行时状态。

```python
@dataclass(frozen=True)
class TraversalContext:
    node_stack: List[str] = field(default_factory=list)
    current_path: List[str] = field(default_factory=list)
    visited_pages: Set[str] = field(default_factory=set)
    failed_nodes: Dict[str, ErrorRecord] = field(default_factory=dict)
    action_history: List[ActionRecord] = field(default_factory=list)
    inference_history: List["ContainerInference"] = field(default_factory=list)
    goal_attempts: Dict[str, int] = field(default_factory=dict)
```

**实现位置**: `src/context/traversal_context.py:33`

**限制**：
- `action_history`: 最多保留 5 条
- `inference_history`: 最多保留 3 条

---

### ErrorRecord

失败节点记录。

```python
@dataclass(frozen=True)
class ErrorRecord:
    node_id: str
    error_type: str
    timestamp: datetime
    retry_count: int
```

**实现位置**: `src/context/traversal_context.py:13`

---

### ActionRecord

操作记录。

```python
@dataclass(frozen=True)
class ActionRecord:
    action_type: str
    target: Optional[str]
    timestamp: datetime
    result: Optional[str]
```

**实现位置**: `src/context/traversal_context.py:23`

---

# 6. 异常处理模型

异常处理模型描述异常的上下文和处理结果。

## 6.1 异常严重程度

### ExceptionSeverity (枚举)

异常严重程度分级。

```python
class ExceptionSeverity(Enum):
    INFO = "info"          # 正常变化（弹窗、重定向）- 透明处理
    WARNING = "warning"    # 需要注意但不阻塞 - 记录并继续
    ERROR = "error"        # 需要重试的失败 - 尝试恢复
    CRITICAL = "critical"  # 需要干预的严重问题 - 恢复或回退
    FATAL = "fatal"        # 不可恢复的失败 - 终止遍历
```

**实现位置**: `src/exception/exceptions.py:11`

---

## 6.2 异常动作

### ExceptionAction (枚举)

异常处理时可采取的动作。

```python
class ExceptionAction(str, Enum):
    RETRY = "retry"          # 重试操作（增加重试计数）
    SKIP = "skip"            # 跳过当前操作，继续下一项
    BACKTRACK = "backtrack"  # 返回上一节点，标记当前为失败
    RECOVER = "recover"      # 执行恢复动作，然后重试
    TERMINATE = "terminate"  # 停止遍历，重新抛出异常
    IGNORE = "ignore"        # 记录异常但正常继续
```

**实现位置**: `src/exception/context.py:18`

---

### RecoveryAction (枚举)

具体的恢复动作。

```python
class RecoveryAction(str, Enum):
    RECONNECT_ADB = "reconnect_adb"
    RESTART_APP = "restart_app"
    CLOSE_POPUP = "close_popup"
    NAVIGATE_BACK = "navigate_back"
    WAIT_AND_RETRY = "wait_and_retry"
    IGNORE_UI_CHANGE = "ignore_ui_change"
```

**实现位置**: `src/exception/context.py:37`

---

## 6.3 异常上下文

### ExceptionContext

传递给异常处理器的上下文信息。

```python
@dataclass
class ExceptionContext:
    exception: "TraversalException"
    severity: "ExceptionSeverity"
    state: "TraversalState"
    node: Optional["ContentNode"]
    operation: str
    timestamp: datetime
    retry_count: int
```

**实现位置**: `src/exception/context.py:57`

---

## 6.4 异常处理结果

### ExceptionHandlingResult

异常处理器返回的结果。

```python
@dataclass
class ExceptionHandlingResult:
    action: ExceptionAction
    message: str
    new_state: Optional[str] = None
    recovery_action: Optional[RecoveryAction] = None
```

**实现位置**: `src/exception/context.py:92`

**工厂方法**：
- `retry()`: 创建 RETRY 结果
- `skip()`: 创建 SKIP 结果
- `backtrack()`: 创建 BACKTRACK 结果
- `recover()`: 创建 RECOVER 结果
- `terminate()`: 创建 TERMINATE 结果
- `ignore()`: 创建 IGNORE 结果

---

# 7. AI 能力模型

AI 能力模型描述 AI 服务的数据结构。

## 7.1 基础 AI 类型

### DecisionResult (枚举)

AI 决策结果。

```python
class DecisionResult(str, Enum):
    SUCCESS = "success"
    UNSURE = "unsure"
    GIVE_UP = "give_up"
```

**实现位置**: `src/ai/types.py:8`

---

### ContainerInference

容器类型推断结果。

```python
@dataclass(frozen=True)
class ContainerInference:
    container_type: str
    confidence: float
    matched_template: Optional[str] = None
```

**实现位置**: `src/ai/types.py:17`

**验证**: `confidence` 必须在 [0, 1] 范围内。

---

## 7.2 自然语言解析

### TraversalPlan

自然语言解析后的遍历计划。

```python
@dataclass
class TraversalPlan:
    entry_app: Optional[str]
    root_node: TraversalNode
    static_nodes: List[TraversalNode] = field(default_factory=list)
    template_registry: str = "default"
    mode: Literal["hybrid", "concrete", "dynamic"] = "hybrid"
    reasoning: Optional[str] = None
    confidence: float = 1.0
```

**实现位置**: `src/ai/capabilities/types.py:40`

**注意**: 此 `TraversalPlan` 结构与 PRD V5 提案略有简化，使用 AI 模块内部的定义。

---

### NodeOperation

节点操作定义（AI 模块内部）。

```python
@dataclass
class NodeOperation:
    action: str
    target: Optional[Dict[str, Any]] = None
    params: Optional[Dict[str, Any]] = None
    restore: Optional[Dict[str, Any]] = None
```

**实现位置**: `src/ai/capabilities/types.py:11`

---

### NodeStrategy

节点策略（AI 模块内部）。

```python
@dataclass
class NodeStrategy:
    type: str
    dynamic_rules: Optional[Dict[str, Any]] = None
    static_children: Optional[List[str]] = None
```

**实现位置**: `src/ai/capabilities/types.py:20`

---

## 7.3 页面类型验证

### PageTypeVerification

页面类型验证结果。

```python
@dataclass
class PageTypeVerification:
    is_match: bool
    confidence: float
    actual_type: Literal["menu_list", "settings_group", "dialog", "home_desktop", "leaf_page", "unknown"]
    reasoning: str = ""
    mismatch_details: Optional[MismatchDetails] = None
    suggestion: Optional[Suggestion] = None
```

**实现位置**: `src/ai/capabilities/types.py:70`

---

### MismatchDetails

页面类型不匹配详情。

```python
@dataclass
class MismatchDetails:
    missing_items: List[str] = field(default_factory=list)
    unexpected_items: List[str] = field(default_factory=list)
    type_conflict: Optional[str] = None
```

**实现位置**: `src/ai/capabilities/types.py:54`

---

### Suggestion

处理不匹配的建议。

```python
@dataclass
class Suggestion:
    action: Literal["back", "retry", "skip", "close_popup", "renavigate"]
    target: Optional[str] = None
    reason: str = ""
```

**实现位置**: `src/ai/capabilities/types.py:63`

---

## 7.4 安全筛选

### SafetyEvaluation

单个元素的安全评估。

```python
@dataclass
class SafetyEvaluation:
    name: str
    safety_tag: Literal["safe", "caution", "skip", "unknown"]
    confidence: float
    reason: str
    context_dependency: Optional[str] = None
    task_relevance: Optional[str] = None
```

**实现位置**: `src/ai/capabilities/types.py:83`

---

### PageLevelGuidance

页面级别的安全指导。

```python
@dataclass
class PageLevelGuidance:
    overall_safe_to_proceed: bool
    recommended_max_parallel: int = 3
    special_precautions: List[str] = field(default_factory=list)
    task_suitability: Optional[str] = None
```

**实现位置**: `src/ai/capabilities/types.py:94`

---

### SafetyScreeningResult

元素安全筛选结果。

```python
@dataclass
class SafetyScreeningResult:
    evaluations: List[SafetyEvaluation]
    page_level_guidance: Optional[PageLevelGuidance] = None
```

**实现位置**: `src/ai/capabilities/types.py:103`

---

## 7.5 上下文决策

### ContextDecisionResult

上下文决策结果。

```python
@dataclass
class ContextDecisionResult:
    result: Literal["success", "unsure", "give_up", "wait", "safe_mode"]
    action: Literal["click", "back", "swipe", "scroll_down", "wait", "skip", "no_action"]
    target: Optional[Dict[str, Any]] = None
    params: Optional[Dict[str, Any]] = None
    reasoning: str = ""
    confidence: float = 1.0
    safety_verified: bool = True
```

**实现位置**: `src/ai/capabilities/types.py:112`

---

# 8. Trace 模型

Trace 模型描述遍历过程的录制和回放数据结构。

## 8.1 执行状态

### ExecutionStatus (枚举)

```python
class ExecutionStatus(str, Enum):
    SUCCESS = "success"
    FAILED = "failed"
    SKIPPED = "skipped"
    TIMEOUT = "timeout"
```

**实现位置**: `src/trace/models.py:15`

---

## 8.2 Trace 步骤

### TraceDecision

遍历过程中的决策记录。

```python
@dataclass
class TraceDecision:
    node_id: str
    node_type: str
    operation_action: str
    target_description: Optional[str] = None
    reasoning: Optional[str] = None
    confidence: float = 1.0
```

**实现位置**: `src/trace/models.py:25`

---

### TraceExecution

操作执行结果。

```python
@dataclass
class TraceExecution:
    status: ExecutionStatus
    duration_ms: float
    screenshot_ref: Optional[str] = None
    error_message: Optional[str] = None
    error_type: Optional[str] = None
    stack_trace: Optional[str] = None
```

**实现位置**: `src/trace/models.py:37`

---

### TraceStep

单个遍历步骤记录。

```python
@dataclass
class TraceStep:
    step_id: int
    timestamp: datetime
    global_state: str
    traversal_state: str
    page_analysis_summary: Optional[str] = None
    decision: Optional[TraceDecision] = None
    execution: Optional[TraceExecution] = None
    stack_snapshot: List[str] = field(default_factory=list)
    path_before: List[str] = field(default_factory=list)
    path_after: List[str] = field(default_factory=list)
    screenshot_ref: Optional[str] = None
    error: Optional[Dict[str, Any]] = None
    metadata: Dict[str, Any] = field(default_factory=dict)
```

**实现位置**: `src/trace/models.py:49`

---

## 8.3 状态快照

### StateSnapshot

完整状态快照。

```python
@dataclass
class StateSnapshot:
    snapshot_id: str
    timestamp: datetime
    step_id: int
    full_state: Dict[str, Any]
    node_stack: List[Dict[str, Any]]
    visited_nodes: Dict[str, str]
    current_path: List[str]
    metadata: Dict[str, Any] = field(default_factory=dict)
```

**实现位置**: `src/trace/models.py:137`

---

## 8.4 会话信息

### SessionInfo

Trace 会话信息。

```python
@dataclass
class SessionInfo:
    device_id: Optional[str] = None
    device_name: Optional[str] = None
    app_version: Optional[str] = None
    app_package: Optional[str] = None
    start_time: datetime = field(default_factory=datetime.now)
    end_time: Optional[datetime] = None
    traversal_mode: str = "graph"
    config: Dict[str, Any] = field(default_factory=dict)
```

**实现位置**: `src/trace/models.py:182`

---

## 8.5 Trace 摘要

### TraceSummary

遍历 Trace 的统计摘要。

```python
@dataclass
class TraceSummary:
    total_steps: int
    successful_operations: int
    failed_operations: int
    skipped_operations: int
    total_duration_ms: float
    visited_pages_count: int
    visited_nodes_count: int
    screenshots_count: int
    errors_count: int
    errors_by_type: Dict[str, int] = field(default_factory=dict)
    max_stack_depth: int = 0
    unique_nodes_visited: int = 0
```

**实现位置**: `src/trace/models.py:223`

---

### TraversalTrace

完整的遍历 Trace。

```python
@dataclass
class TraversalTrace:
    session_info: SessionInfo
    steps: List[TraceStep] = field(default_factory=list)
    state_snapshots: List[StateSnapshot] = field(default_factory=list)
    summary: Optional[TraceSummary] = None
    trace_id: str = field(default_factory=lambda: datetime.now().strftime("%Y%m%d_%H%M%S"))
```

**实现位置**: `src/trace/models.py:280`

---

# 附录 A：模型关系图

```
┌─────────────────────────────────────────────────────────────┐
│                    页面分析模型                                │
├─────────────────────────────────────────────────────────────┤
│  截图 → 视觉分析 → PageAnalysis                              │
│                                                            │
│  PageAnalysis                                                │
│      ├─ level1/level2_menus: MenuInfo[]                     │
│      ├─ current_path: str[]                                 │
│      └─ items: MenuItem[]                                   │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                    图节点模型                                  │
├─────────────────────────────────────────────────────────────┤
│  TraversalNode                                              │
│      ├─ operation: Operation                               │
│      ├─ precondition: Precondition                         │
│      ├─ children_strategy: ChildrenStrategy                │
│      └─ error_policy: ErrorPolicy                          │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                    内容树模型                                   │
├─────────────────────────────────────────────────────────────┤
│  ContentTree                                                │
│      └─ nodes: Dict[str, ContentNode]                      │
│                                                            │
│  TraversalState (持久化)                                     │
│      ├─ current_path: str[]                                │
│      ├─ content_tree: ContentTree                          │
│      └─ node_stack: dict[]                                 │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                    状态机模型                                   │
├─────────────────────────────────────────────────────────────┤
│  GlobalFSM                                                  │
│      IDLE → INITIALIZING → TRAVERSING → COMPLETED            │
│                                                            │
│  TraversalFSM                                              │
│      SELECT → CHECK → EXECUTE → VERIFY → BRANCH               │
│                                                            │
│  NodeStack                                                 │
│      └─ frames: StackFrame[]                               │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                    运行时上下文                                 │
├─────────────────────────────────────────────────────────────┤
│  TraversalContext (AI)                                      │
│      ├─ node_stack: str[]                                  │
│      ├─ current_path: str[]                                │
│      └─ action_history: ActionRecord[]                     │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                    异常处理模型                                 │
├─────────────────────────────────────────────────────────────┤
│  ExceptionContext                                           │
│      ├─ exception: TraversalException                      │
│      ├─ severity: ExceptionSeverity                        │
│      └─ retry_count: int                                   │
│                                                            │
│  ExceptionHandlingResult                                   │
│      ├─ action: ExceptionAction                            │
│      └─ recovery_action: RecoveryAction                    │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                    AI 能力模型                                   │
├─────────────────────────────────────────────────────────────┤
│  TraversalPlan (AI解析)                                     │
│  ContainerInference (容器推断)                              │
│  PageTypeVerification (页面验证)                            │
│  SafetyScreeningResult (安全筛选)                           │
│  ContextDecisionResult (上下文决策)                         │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                    Trace 模型                                   │
├─────────────────────────────────────────────────────────────┤
│  TraversalTrace                                             │
│      ├─ session_info: SessionInfo                          │
│      ├─ steps: TraceStep[]                                 │
│      ├─ state_snapshots: StateSnapshot[]                    │
│      └─ summary: TraceSummary                              │
└─────────────────────────────────────────────────────────────┘
```

---

# 附录 B：命名冲突说明

## TraversalState 冲突

代码中存在两个同名但用途不同的 `TraversalState`：

| 用途 | 类型 | 位置 |
|------|------|------|
| 状态机枚举 | `enum` | `src/state_machine/traversal_fsm.py` |
| 持久化状态 | `BaseModel` | `src/state/content_tree.py` |

使用时需注意上下文：
- 状态机上下文中使用枚举版本
- 持久化/缓存上下文中使用 BaseModel 版本

---

# 附录 C：术语对照表

| 中文 | 英文 | 说明 |
|------|------|------|
| 归一化坐标 | Normalized Coordinate | 相对于屏幕尺寸的 0-1 坐标 |
| 容器节点 | Container Node | 可展开进入子页面的节点 |
| 叶子节点 | Leaf Node | 执行操作后不深入 |
| 动态匹配 | Dynamic Match | 运行时根据 UI 元素生成节点 |
| 模板注册表 | Template Registry | 预定义节点模板集合 |
| 节点栈 | Node Stack | 深度优先遍历的状态栈 |
| 异常严重程度 | Exception Severity | 异常的严重级别（INFO/WARNING/ERROR/CRITICAL/FATAL） |
| 恢复动作 | Recovery Action | 异常恢复时的具体操作 |
| 内容树 | Content Tree | 遍历结果的树形存储 |
| 访问指纹 | Visit Fingerprint | 用于追踪已访问元素的唯一标识 |

---

*本文档随系统演进持续更新，仅记录已实现的模型*
