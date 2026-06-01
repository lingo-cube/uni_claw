# Uni-Claw 核心业务模型规范 PRD

> **文档版本**: V1.0
> **创建日期**: 2026-06-01
> **状态**: 草案
> **类型**: 数据模型规范文档
> **作者**: Uni-Claw Team

---

## 文档说明

本文档定义 Uni-Claw 系统的核心业务数据模型，涵盖以下五个领域：

1. **视觉管道模型** - 屏幕分析输出结构
2. **图节点模型** - 遍历操作的节点抽象
3. **意图槽位与计划编译模型** - 自然语言到遍历计划的转换
4. **状态机模型** - 全局与遍历状态管理
5. **运行时上下文模型** - 遍历执行过程的完整上下文

本文档面向以下读者：
- 系统架构师 - 理解整体数据结构设计
- 开发工程师 - 实现具体业务逻辑
- AI 提示工程师 - 理解如何向 AI 描述数据结构
- 测试工程师 - 设计测试用例和验证逻辑

---

# 1. 视觉管道模型

视觉管道模型描述从屏幕截图到结构化页面分析的完整数据流。该模型由两个阶段组成：

1. **多模态扁平化输出** - 多模态视觉模型的原始输出，仅描述"看到了什么"
2. **完整页面分析** - 文本模型组装后的结构化分析，包含层级和行为推理

## 1.1 基础类型

### BoundingBox

归一化边界框，描述元素在屏幕中的位置和大小。

```python
@dataclass
class BoundingBox:
    x: float  # 左上角 x 坐标，归一化到 0-1
    y: float  # 左上角 y 坐标，归一化到 0-1
    w: float  # 宽度，归一化到 0-1
    h: float  # 高度，归一化到 0-1
```

**使用场景**：
- 记录 UI 元素的屏幕位置
- 计算元素中心点
- 判断元素重叠关系

**归一化说明**：所有坐标值相对于屏幕尺寸，左上角为 (0, 0)，右下角为 (1, 1)。

---

### Coordinate

归一化坐标点，用于简单定位。

```python
@dataclass
class Coordinate:
    x: float
    y: float
```

**使用场景**：
- 记录菜单项的点击位置
- 存储滑块、开关的交互坐标
- 作为 ADB 点击操作的输入

---

## 1.2 多模态扁平化输出

该阶段由多模态视觉模型（如 Claude 4 Vision、MiMo）直接输出，特点是**扁平化、无层级、仅视觉感知**。

### TypeHint (枚举)

多模态模型对元素的粗略视觉类型提示，仅基于视觉特征，不包含行为推理。

```python
class TypeHint(str, Enum):
    CLICKABLE_TEXT = "clickable_text"   # 可点击的文本区域
    SWITCH = "switch"                   # 开关控件
    SLIDER = "slider"                   # 滑块控件
    BUTTON = "button"                   # 按钮控件
    ICON = "icon"                       # 纯图标元素
    INPUT_FIELD = "input_field"         # 输入框
    TEXT = "text"                       # 纯文本元素
    IMAGE = "image"                     # 图片元素
```

**设计原则**：
- 仅描述"看起来像什么"，不描述"做什么"
- 不区分菜单项和按钮（都是可点击文本）
- 不推断导航行为

---

### Region

屏幕区域划分，用于描述布局结构。

```python
@dataclass
class Region:
    id: str                    # 区域唯一标识，如 "left_panel"
    bounds: BoundingBox        # 区域的边界框
    role: str                  # 区域角色：menu / content / tabs / overlay / unknown
```

**区域角色说明**：
- `menu` - 侧边菜单区域
- `content` - 主内容区域
- `tabs` - 标签页区域
- `overlay` - 弹窗/覆盖层
- `unknown` - 未知区域

**使用场景**：
- 帮助区分一级菜单和内容区元素
- 识别弹窗覆盖层
- 辅助理解布局结构

---

### FlattenedElement

扁平化元素，多模态模型输出的单个元素信息。

```python
@dataclass
class FlattenedElement:
    id: int                              # 元素唯一标识（在本次分析内）
    text: str = ""                       # 元素上显示的文本
    type_hint: TypeHint = TypeHint.TEXT  # 粗略视觉类型
    bbox: BoundingBox = field(default_factory=lambda: BoundingBox(0,0,0,0))
    region: Optional[str] = None         # 所属区域 ID
    visual_state: Dict[str, Any] = field(default_factory=dict)
    confidence: float = 1.0              # 识别置信度 (0.0 ~ 1.0)
```

**visual_state 示例**：
```python
{
    "highlighted": True,           # 元素被高亮
    "text_bold": True,            # 文本加粗
    "has_indicator": "filled_circle",  # 有指示器（实心圆）
    "checked": True,              # 开关/复选框状态
    "slider_value": 0.7,          # 滑块位置
}
```

**使用场景**：
- 多模态模型的输出格式
- 作为文本模型组装 PageAnalysis 的输入
- 用于视觉调试和验证

---

### FlattenedScreen

多模态模型输出的扁平化屏幕描述。

```python
@dataclass
class FlattenedScreen:
    elements: List[FlattenedElement] = field(default_factory=list)
    screen_hints: Dict[str, Any] = field(default_factory=dict)
```

**screen_hints 结构**：
```python
{
    "top_bar_text": "车辆设置",           # 顶部标题栏文本
    "layout_type": "split_pane",         # 布局类型
    "regions": [Region(...), ...],       # 屏幕区域划分
    "overlay_detected": True,            # 是否疑似有弹窗
    "scroll_detected": True,             # 页面是否可滚动
}
```

**layout_type 可选值**：
- `split_pane` - 分栏布局（如左侧菜单+右侧内容）
- `tabbed` - 标签页布局
- `single` - 单列布局
- `overlay` - 覆盖层/弹窗
- `unknown` - 未知布局

---

## 1.3 完整页面分析

该阶段由文本模型（或一体化模型）输出，特点是**结构化、含层级、含行为推理**。

### Direction (枚举)

菜单排列方向。

```python
class Direction(str, Enum):
    LEFT = "left"      # 左侧菜单
    RIGHT = "right"    # 右侧菜单
    TOP = "top"        # 顶部菜单
    BOTTOM = "bottom"  # 底部菜单
```

---

### MenuItemType (枚举)

可交互元素的精确控件类型，用于遍历决策。

```python
class MenuItemType(str, Enum):
    MENU_ITEM = "menu_item"    # 可点击的菜单项，预期导航到子页面
    TAB = "tab"                # 标签页按钮，切换顶级视图
    SWITCH = "switch"          # 开关控件（通常带滑动动画）
    TOGGLE = "toggle"          # 状态切换按钮（如收藏按钮）
    BUTTON = "button"          # 通用操作按钮（可能弹窗、跳转）
    LINK = "link"              # 导航链接或超链接
    ICON = "icon"              # 纯图标按钮（无文本标签）
    TEXT = "text"              # 非交互文本元素
    READONLY = "readonly"      # 仅展示元素，不响应点击
    INPUT = "input"            # 文本输入框
    SLIDER = "slider"          # 滑块控件（可拖拽调节值）
```

**与 TypeHint 的区别**：
- TypeHint 是视觉分类（"看起来像开关"）
- MenuItemType 是行为分类（"是开关，点击会切换状态"）

---

### ExpectedAction (枚举)

元素被点击后的预期行为。

```python
class ExpectedAction(str, Enum):
    NAVIGATE = "navigate"  # 预期页面导航
    TOGGLE = "toggle"      # 预期状态切换（如开关、滑块）
    ACTION = "action"      # 预期触发操作（可能弹窗）
    NONE = "none"          # 预期无响应
```

**使用场景**：
- 遍历决策时决定是否需要恢复操作
- 判断点击后是否需要等待页面变化
- 预测操作后的副作用

---

### MenuInfo

一级菜单或二级标签页中的一条目。

```python
@dataclass
class MenuInfo:
    name: str              # 菜单名称
    coordinate: Coordinate # 菜单坐标
    active: bool = False  # 是否为当前激活的菜单项
```

**使用场景**：
- 记录一级菜单列表（左侧/顶部导航）
- 记录二级标签页列表
- 用于 auto_escape 导航策略

---

### MenuItem

页面上的一个可交互或可识别的元素。

```python
@dataclass
class MenuItem:
    id: int                       # 元素唯一标识
    name: str                     # 元素文本/描述
    type: MenuItemType            # 精确控件类型
    coordinate: Coordinate        # 中心坐标
    expected_action: ExpectedAction = ExpectedAction.ACTION
    expects_page_change: bool = False    # 是否预期页面变化
    expects_state_change: bool = False   # 是否预期状态变化
    parent: Optional[str] = None         # 父元素名称
    confidence: float = 1.0              # 识别置信度
    safety_tag: Optional[str] = None     # 安全标记（见 SafetyTag）
```

**使用场景**：
- 动态匹配规则的目标对象
- 安全过滤器检查危险元素
- 构建操作指令的目标定位

**expects_page_change 与 expects_state_change 的关系**：
- `NAVIGATE` → `expects_page_change=True`
- `TOGGLE` → `expects_state_change=True`
- `ACTION` → 两者都可能是 False

---

### PopupInfo

弹窗信息。

```python
@dataclass
class PopupInfo:
    title: Optional[str] = None               # 弹窗标题
    buttons: List[MenuItem] = field(default_factory=list)  # 弹窗中的按钮列表
```

**使用场景**：
- 记录弹窗状态
- 提供弹窗按钮的交互能力
- 支持弹窗关闭操作

---

### PageAnalysis

完整的页面结构分析结果。

```python
@dataclass
class PageAnalysis:
    level1_dir: Optional[Direction] = None     # 一级菜单排列方向
    level1_menus: List[MenuInfo] = field(default_factory=list)
    level2_dir: Optional[Direction] = None     # 二级标签页排列方向
    level2_menus: List[MenuInfo] = field(default_factory=list)
    current_path: List[str] = field(default_factory=list)
    items: List[MenuItem] = field(default_factory=list)
    is_popup: bool = False                     # 当前是否检测到弹窗
    popup_info: Optional[PopupInfo] = None     # 弹窗详情
    has_scroll: bool = False                   # 页面是否可滚动
    is_end_of_list: bool = False               # 是否已滚动到列表底部
```

**current_path 示例**：
```python
["车辆设置", "DiLink", "互联"]
```

**使用场景**：
- 遍历状态机的核心输入
- 动态节点生成的基础
- 页面缓存和比对

---

# 2. 图节点模型

图节点模型描述遍历操作的节点抽象，是遍历图的基本单元。

## 2.1 操作定义

### OperationAction (枚举)

操作动作类型。

```python
class OperationAction(str, Enum):
    CLICK = "click"               # 点击
    SWIPE = "swipe"               # 滑动
    BACK = "back"                 # 返回
    INPUT_TEXT = "input_text"     # 输入文本
    INPUT_CLEAR = "input_clear"   # 清空输入框
    NO_ACTION = "no_action"       # 无操作
    SCROLL_DOWN = "scroll_down"   # 向下滚动
```

---

### TargetBy (枚举)

目标定位方式。

```python
class TargetBy(str, Enum):
    TEXT = "text"                 # 按文本匹配元素
    COORDINATE = "coordinate"     # 直接使用坐标
    UI_INDEX = "ui_index"         # 按元素在列表中的索引
```

---

### Target

操作目标定位描述。

```python
@dataclass
class Target:
    by: TargetBy    # 定位方式
    value: Any      # 定位值（文本内容/坐标元组/索引）
```

**value 类型示例**：
- `by=TEXT` → `value="移动数据"`
- `by=COORDINATE` → `value=(0.2, 0.5)`
- `by=UI_INDEX` → `value=2`

---

### RestoreAction

恢复操作定义，用于操作后恢复原状态。

```python
@dataclass
class RestoreAction:
    needed: bool                  # 是否需要恢复
    action: OperationAction       # 恢复动作类型
    target: Optional[Target] = None
    params: Dict[str, Any] = field(default_factory=dict)
```

**使用场景**：
- 开关操作后恢复到原状态
- 滑块拖动后恢复到原位置
- 临时设置修改后恢复

---

### Operation

描述一次具体的 UI 操作。

```python
@dataclass
class Operation:
    action: OperationAction                # 动作类型
    target: Optional[Target] = None       # 操作目标
    params: Dict[str, Any] = field(default_factory=dict)  # 动作参数
    restore: Optional[RestoreAction] = None
```

**params 示例**：
```python
# 滑块操作
{"value": 0.8, "direction": "right"}

# 输入操作
{"text": "test input", "clear_first": True}
```

---

## 2.2 节点定义

### Precondition

节点执行前必须满足的页面状态。

```python
@dataclass
class Precondition:
    page_name: Optional[str] = None     # 要求页面名称匹配
    path: Optional[List[str]] = None    # 要求完整路径匹配
    ui_condition: Optional[str] = None  # 自定义 UI 条件
```

**ui_condition 示例**：
```python
"screen_contains('亮度')"
"element_visible('设置', '成功')"
```

---

## 2.3 子节点策略

### StrategyType (枚举)

子节点生成策略类型。

```python
class StrategyType(str, Enum):
    STATIC = "static"             # 使用预定义的静态子节点列表
    DYNAMIC_MATCH = "dynamic_match"  # 运行时根据屏幕元素动态匹配
    NONE = "none"                 # 无子节点（叶子节点）
```

---

### DynamicRuleAction (枚举)

动态匹配后对元素的处理动作。

```python
class DynamicRuleAction(str, Enum):
    GENERATE_CHILD = "generate_child"   # 生成子节点并压入节点栈
    SKIP = "skip"                       # 跳过该元素
    EXECUTE_INLINE = "execute_inline"   # 立即执行操作，但不生成子节点
```

---

### DynamicRule

一条动态匹配规则。

```python
@dataclass
class DynamicRule:
    match_condition: Dict[str, Any]     # 匹配条件
    child_template: str                 # 匹配后使用的模板 ID
    action: DynamicRuleAction = DynamicRuleAction.GENERATE_CHILD
```

**match_condition 示例**：
```python
{
    "type": "menu_item",
    "expected_action": "navigate"
}
```

---

### ChildrenStrategy

子节点生成策略。

```python
@dataclass
class ChildrenStrategy:
    type: StrategyType
    static_children: Optional[List[str]] = None
    dynamic_rules: Optional[Dict[str, DynamicRule]] = None
```

---

## 2.4 退出条件

### ExitConditionType (枚举)

```python
class ExitConditionType(str, Enum):
    ALL_CHILDREN_VISITED = "all_children_visited"
    DEPTH_LIMITED = "depth_limited"
    SINGLE_LEVEL = "single_level"
```

---

### ExitCondition

```python
@dataclass
class ExitCondition:
    type: ExitConditionType
    fallback: Optional[Dict[str, Any]] = None
    max_depth: Optional[int] = None
```

---

## 2.5 错误策略

### ErrorPolicy

```python
@dataclass
class ErrorPolicy:
    max_retries: int = 1
    fallback_action: str = "skip"
    recovery_steps: List[Operation] = field(default_factory=list)
```

---

### TraversalNode

遍历图中的一个节点。

```python
@dataclass
class TraversalNode:
    node_id: str
    name: str
    node_type: str  # container, leaf_switch, leaf_slider, leaf_action, leaf_input
    operation: Operation
    precondition: Optional[Precondition] = None
    children_strategy: ChildrenStrategy = field(default_factory=lambda: ChildrenStrategy(type=StrategyType.NONE))
    exit_condition: Optional[ExitCondition] = None
    error_policy: Optional[ErrorPolicy] = None
    meta: Dict[str, Any] = field(default_factory=dict)
```

**node_type 说明**：
- `container` - 容器节点，可以进入子页面
- `leaf_switch` - 开关叶子节点
- `leaf_slider` - 滑块叶子节点
- `leaf_action` - 动作按钮叶子节点
- `leaf_input` - 输入框叶子节点

---

# 3. 意图槽位与计划编译模型

## 3.1 意图槽位

### IntentSlots

AI 从自然语言指令中提取的意图槽位。

```python
@dataclass
class IntentSlots:
    target_app: str = "设置"
    scope: str = "all_menus"
    target: Optional[str] = None
    depth: str = "unlimited"
    element_handling: str = "full_interaction"
    navigation: str = "adaptive"
    restore: str = "restore"
    completion: Optional[str] = None
```

**字段说明**：

| 字段 | 可选值 | 说明 |
|------|--------|------|
| target_app | 应用名称 | "设置"、"电话"、"音乐" 等 |
| scope | all_menus / current_page / until_target / target_path | 探索范围 |
| target | 节点名称 | "序列号"、"关于本机"、"亮度" |
| depth | unlimited / max_N | 递归深度限制 |
| element_handling | full_interaction / menu_only / safe_mode / read_only | 控件处理策略 |
| navigation | adaptive / strict_back | 页面间导航方式 |
| restore | restore / keep_changes | 是否恢复有状态控件 |
| completion | None / timeout:N / max_steps:N | 附加终止条件 |

---

## 3.2 遍历计划

### TraversalMode (枚举)

```python
class TraversalMode(str, Enum):
    HYBRID = "hybrid"     # 混合模式：静态节点+动态探索
    CONCRETE = "concrete" # 具体模式：严格按静态路径执行
    ABSTRACT = "abstract" # 抽象模式：完全依赖动态规则探索
```

---

### EntryStrategy (枚举)

```python
class EntryStrategy(str, Enum):
    COLD_LAUNCH = "cold_launch"
    DIRECT_DEEPLINK = "direct_deeplink"
    BIND_CURRENT_SCREEN = "bind_current_screen"
```

---

### CompletionPolicyType (枚举)

```python
class CompletionPolicyType(str, Enum):
    NONE = "none"
    TARGET_FOUND = "target_found"
    TIMEOUT = "timeout"
    MAX_STEPS = "max_steps"
```

---

### TraversalPlan

```python
@dataclass
class TraversalPlan:
    intent_slots: Optional[IntentSlots] = None
    entry_app: Optional[str] = None
    entry_policy: EntryPolicy = field(default_factory=EntryPolicy)
    root_node: Optional[TraversalNode] = None
    static_nodes: List[TraversalNode] = field(default_factory=list)
    template_registry: str = "default"
    mode: TraversalMode = TraversalMode.HYBRID
    completion_policy: CompletionPolicy = field(default_factory=CompletionPolicy)
```

---

# 4. 状态机模型

## 4.1 全局状态机

### GlobalState (枚举)

```python
class GlobalState(str, Enum):
    IDLE = "idle"
    INITIALIZING = "initializing"
    TRAVERSING = "traversing"
    PAUSED = "paused"
    ERROR = "error"
    RECOVERING = "recovering"
    COMPLETED = "completed"
    TERMINATED = "terminated"
```

---

## 4.2 遍历状态机

### TraversalState (枚举)

```python
class TraversalState(str, Enum):
    NODE_SELECT = "node_select"
    PRECONDITION_CHECK = "precondition_check"
    EXECUTE = "execute"
    RESULT_VERIFY = "result_verify"
    BRANCH = "branch"
    FRAME_COMPLETE = "frame_complete"
```

---

### TraversalEvent (枚举)

```python
class TraversalEvent(str, Enum):
    NODE_READY = "node_ready"
    PRECONDITION_MET = "precondition_met"
    PRECONDITION_FAILED = "precondition_failed"
    EXECUTION_DONE = "execution_done"
    EXECUTION_FAILED = "execution_failed"
    RESULT_VERIFIED = "result_verified"
    POPUP_DETECTED = "popup_detected"
    BRANCH_COMPLETE = "branch_complete"
    FRAME_DONE = "frame_done"
    BACK_COMPLETE = "back_complete"
```

---

# 5. 运行时上下文模型

## 5.1 安全与异常

### SafetyTag (枚举)

```python
class SafetyTag(str, Enum):
    SAFE = "safe"
    CAUTION = "caution"
    SKIP = "skip"
    UNKNOWN = "unknown"
```

---

### ErrorSeverity (枚举)

```python
class ErrorSeverity(str, Enum):
    INFO = "info"
    WARNING = "warning"
    ERROR = "error"
    CRITICAL = "critical"
    FATAL = "fatal"
```

---

### ExceptionAction (枚举)

```python
class ExceptionAction(str, Enum):
    RETRY = "retry"
    SKIP = "skip"
    BACKTRACK = "backtrack"
    RECOVER = "recover"
    TERMINATE = "terminate"
    IGNORE = "ignore"
```

---

## 5.2 节点栈

### StackFrame

节点栈中的一帧。

```python
@dataclass
class StackFrame:
    node: TraversalNode
    page_name: str
    children: List[str] = field(default_factory=list)
    current_child_idx: int = 0
    pending_restore: bool = False
```

---

## 5.3 页面缓存

### PageNode

已访问页面的缓存快照。

```python
@dataclass
class PageNode:
    page_name: str
    path: List[str]
    items: List[MenuItem]
    explored_children: List[str] = field(default_factory=list)
    timestamp: float = 0.0
    fingerprint: Optional[str] = None
```

---

## 5.4 错误与历史

### ErrorRecord

```python
@dataclass
class ErrorRecord:
    error_type: str
    message: str
    node_id: Optional[str] = None
    retry_count: int = 0
    timestamp: float = 0.0
    severity: ErrorSeverity = ErrorSeverity.ERROR
```

---

### ActionRecord

```python
@dataclass
class ActionRecord:
    action: OperationAction
    target: Optional[str] = None
    result: str = "success"
    page_before: Optional[str] = None
    page_after: Optional[str] = None
```

---

## 5.5 完整上下文

### TraversalContext

```python
@dataclass
class TraversalContext:
    node_stack: List[StackFrame] = field(default_factory=list)
    current_path: List[str] = field(default_factory=list)
    visited_pages: Set[str] = field(default_factory=set)
    page_tree: Dict[str, PageNode] = field(default_factory=dict)
    current_page_analysis: Optional[PageAnalysis] = None
    cache_valid: bool = False
    failed_nodes: Dict[str, ErrorRecord] = field(default_factory=dict)
    action_history: List[ActionRecord] = field(default_factory=list)
    current_fingerprint: Optional[str] = None
    visited_level1_menus: Set[str] = field(default_factory=set)
    visited_level2_menus: Set[str] = field(default_factory=set)
```

---

# 附录 A：模型关系图

```
┌─────────────────────────────────────────────────────────────┐
│                        视觉管道                              │
├─────────────────────────────────────────────────────────────┤
│  截图 → 多模态模型 → FlattenedScreen → 文本模型 → PageAnalysis │
│                                                            │
│  FlattenedScreen (扁平、视觉)                                │
│      ├─ elements: FlattenedElement[]                       │
│      └─ screen_hints: {layout, regions, ...}               │
│                                                            │
│  PageAnalysis (结构化、行为)                                │
│      ├─ level1/level2_menus: MenuInfo[]                    │
│      ├─ current_path: str[]                                │
│      └─ items: MenuItem[]                                  │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                        图节点模型                            │
├─────────────────────────────────────────────────────────────┤
│  TraversalNode                                              │
│      ├─ operation: Operation                               │
│      ├─ precondition: Precondition                         │
│      ├─ children_strategy: ChildrenStrategy                │
│      └─ error_policy: ErrorPolicy                          │
│                                                            │
│  Operation                                                  │
│      ├─ action: OperationAction                            │
│      ├─ target: Target                                     │
│      └─ restore: RestoreAction                            │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                        状态机模型                            │
├─────────────────────────────────────────────────────────────┤
│  GlobalFSM: IDLE → INITIALIZING → TRAVERSING → COMPLETED    │
│                                                            │
│  TraversalFSM: SELECT → CHECK → EXECUTE → VERIFY → BRANCH   │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                      运行时上下文                             │
├─────────────────────────────────────────────────────────────┤
│  TraversalContext                                           │
│      ├─ node_stack: StackFrame[]                           │
│      ├─ current_path: str[]                                │
│      ├─ visited_pages: Set[str]                           │
│      ├─ page_tree: Dict[str, PageNode]                     │
│      └─ action_history: ActionRecord[]                     │
└─────────────────────────────────────────────────────────────┘
```

---

# 附录 B：术语对照表

| 中文 | 英文 | 说明 |
|------|------|------|
| 归一化坐标 | Normalized Coordinate | 相对于屏幕尺寸的 0-1 坐标 |
| 扁平化输出 | Flattened Output | 无层级的元素列表 |
| 容器节点 | Container Node | 可展开进入子页面的节点 |
| 叶子节点 | Leaf Node | 执行操作后不深入 |
| 动态匹配 | Dynamic Match | 运行时根据 UI 元素生成节点 |
| 模板注册表 | Template Registry | 预定义节点模板集合 |
| 节点栈 | Node Stack | 深度优先遍历的状态栈 |
| 页面指纹 | Page Fingerprint | 用于快速比对的页面标识 |

---

*本文档随系统演进持续更新*
