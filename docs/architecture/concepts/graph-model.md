# Graph Model Design

## 概述

Graph Model 是 uni-claw 的核心抽象，定义了遍历计划的声明式表示。它将遍历任务抽象为有向无环图 (DAG)，支持静态路径规划和动态探索。

## 核心组件

### 1. TraversalPlan (遍历计划)

顶层容器，定义完整的遍历策略。

```python
@dataclass
class TraversalPlan:
    entry_app: str                    # 目标应用名称
    entry_policy: EntryPolicy         # 入口策略
    root_node: TraversalNode           # 根节点
    static_nodes: Dict[str, Node]     # 静态节点注册表
    template_registry: Optional[str]   # 模板注册表路径
    mode: TraversalMode                # 遍历模式
    completion_policy: CompletionPolicy  # 完成策略
    intent_slots: IntentSlots          # AI 提取的意图槽位
    meta: Dict[str, Any]               # 元数据
```

**遍历模式**:
- `STATIC` - 仅使用静态节点
- `DYNAMIC` - 仅使用动态匹配
- `HYBRID` - 混合模式（默认）

### 2. TraversalNode (遍历节点)

统一节点抽象，所有遍历操作的原子单位。

```python
@dataclass
class TraversalNode:
    node_id: str                      # 唯一标识
    name: str                         # 显示名称
    node_type: NodeType              # 节点类型
    operation: Operation              # 操作定义
    precondition: Precondition         # 前置条件
    children_strategy: ChildrenStrategy  # 子节点策略
    error_policy: ErrorPolicy         # 错误处理策略
```

#### 节点类型 (NodeType)

| 类型 | 说明 | 示例 |
|------|------|------|
| `CONTAINER` | 容器，可包含子节点 | 设置主页面 |
| `LEAF_SWITCH` | 开关控件 | 自动亮度开关 |
| `LEAF_SLIDER` | 滑块控件 | 音量滑块 |
| `LEAF_ACTION` | 动作按钮 | 返回按钮 |
| `LEAF_INFO` | 信息显示 | 版本信息 |
| `SCREEN` | 屏幕页面 | HomeScreen |
| `ACTION` | 通用动作 | 点击操作 |
| `TARGET` | 目标节点 | 查找目标 |

#### 操作 (Operation)

定义节点执行的具体动作。

```python
@dataclass
class Operation:
    action: str                       # 动作类型 (click/toggle/swipe/wait)
    target: Target                    # 目标定位
    params: Dict[str, Any]            # 参数
    restore_action: Optional[RestoreAction]  # 恢复动作
```

#### 目标定位 (Target)

```python
@dataclass
class Target:
    by: str                          # 定位方式 (text/coordinate/ui_index)
    value: Any                       # 定位值
```

**定位方式**:
- `text` - 按文本定位
- `coordinate` - 按坐标定位
- `ui_index` - 按 UI 索引定位

### 3. ChildrenStrategy (子节点策略)

定义子节点的生成方式。

#### 策略类型 (ChildrenStrategyType)

| 类型 | 说明 |
|------|------|
| `STATIC` | 静态子节点列表 |
| `DYNAMIC_MATCH` | 基于模板动态匹配 |
| `NONE` | 无子节点 |

#### 静态策略

```python
@dataclass
class StaticChildren:
    children: List[TraversalNode]    # 子节点列表
```

#### 动态策略

```python
@dataclass
class DynamicRule:
    rule_id: str                     # 规则标识
    match_mode: MatchMode           # 匹配模式
    template_ref: str                # 模板引用
    selector: Optional[Dict]        # 选择器配置
```

**匹配模式**:
- `SINGLE` - 匹配单个元素
- `MULTIPLE` - 匹配多个元素
- `ALL` - 匹配所有符合条件的元素

### 4. Precondition (前置条件)

定义节点执行前需满足的状态。

```python
@dataclass
class Precondition:
    expected_page: Optional[str]     # 期望页面
    current_path: Optional[List[str]]  # 当前路径
    element_visible: Optional[bool]     # 元素可见性
    custom_conditions: Dict[str, Any]   # 自定义条件
```

### 5. ErrorPolicy (错误处理策略)

定义节点执行失败时的处理方式。

```python
@dataclass
class ErrorPolicy:
    on_error: ErrorAction             # 错误动作
    max_retries: int                 # 最大重试次数
    fallback_action: Optional[str]    # 回退动作
```

### 6. CompletionPolicy (完成策略)

V6 新增，定义全局遍历完成条件。

```python
@dataclass
class CompletionPolicy:
    policy_type: CompletionPolicyType  # 策略类型
    max_steps: Optional[int]          # 最大步数
    timeout: Optional[int]            # 超时时间
    target_ref: Optional[str]         # 目标节点引用
```

**策略类型**:
- `NONE` - 自然完成（栈空）
- `TARGET_FOUND` - 找到目标
- `TIMEOUT` - 超时
- `MAX_STEPS` - 达到最大步数

### 7. ExitCondition (退出条件)

V6 新增，定义容器节点退出条件。

```python
@dataclass
class ExitCondition:
    condition_type: ExitConditionType  # 条件类型
    max_depth: Optional[int]          # 最大深度
    fallback_action: FallbackAction    # 回退动作
```

**退出条件类型**:
- `ALL_CHILDREN_VISITED` - 等待所有子节点处理完成
- `DEPTH_LIMITED` - 达到最大深度
- `SINGLE_LEVEL` - 仅处理直接子节点

**回退动作**:
- `BACK` - 按 Back 键
- `AUTO_ESCAPE` - 尝试同级菜单或 Back
- `SKIP` - 跳过，仅弹栈
- `ABORT` - 中止遍历

## 模板系统

### Template (模板定义)

可重用的节点模式，支持占位符。

```python
@dataclass
class Template:
    template_id: str
    node_type: NodeType
    operation: Dict[str, Any]
    precondition: Optional[Dict[str, Any]]
    children_strategy: Optional[Dict[str, Any]]
    error_policy: Optional[Dict[str, Any]]
    meta: Dict[str, Any]
```

### PlaceholderResolver (占位符解析器)

支持的占位符：
- `{{item_text}}` - UI 元素文本
- `{{item_index}}` - UI 元素索引
- `{{coordinate_x}}` - X 坐标
- `{{coordinate_y}}` - Y 坐标
- `{{parent_id}}` - 父节点 ID

### TemplateRegistry (模板注册表)

管理模板的加载和实例化。

```python
class TemplateRegistry:
    def register(self, template: Template)       # 注册模板
    def get(self, template_id: str) -> Template   # 获取模板
    def instantiate(self, template_id: str, context: Dict) -> TraversalNode  # 实例化
```

## 内置模板

### menu_container

菜单容器模板，用于识别菜单项容器。

```json
{
  "template_id": "menu_container",
  "node_type": "container",
  "children_strategy": {
    "type": "dynamic_match",
    "rule": {
      "match_mode": "multiple",
      "template_ref": "menu_item"
    }
  }
}
```

### menu_item

菜单项模板。

```json
{
  "template_id": "menu_item",
  "node_type": "leaf_action",
  "operation": {
    "action": "click",
    "target": {
      "by": "text",
      "value": "{{item_text}}"
    }
  }
}
```

### switch_leaf

开关控件模板。

```json
{
  "template_id": "switch_leaf",
  "node_type": "leaf_switch",
  "operation": {
    "action": "toggle",
    "target": {
      "by": "ui_index",
      "value": "{{item_index}}"
    }
  },
  "restore_action": {
    "action": "toggle",
    "restore": true
  }
}
```

### slider_leaf

滑块控件模板。

```json
{
  "template_id": "slider_leaf",
  "node_type": "leaf_slider",
  "operation": {
    "action": "swipe",
    "target": {
      "by": "ui_index",
      "value": "{{item_index}}"
    },
    "params": {
      "direction": "horizontal"
    }
  }
}
```

## 使用示例

### 静态计划

```json
{
  "entry_app": "Settings",
  "mode": "static",
  "root_node": {
    "node_id": "root",
    "name": "Settings Home",
    "node_type": "screen",
    "children_strategy": {
      "type": "static",
      "children": [
        {
          "node_id": "display",
          "name": "Display",
          "node_type": "container",
          "operation": {
            "action": "click",
            "target": {"by": "text", "value": "Display"}
          }
        }
      ]
    }
  }
}
```

### 动态计划

```json
{
  "entry_app": "Settings",
  "mode": "dynamic",
  "root_node": {
    "node_id": "root",
    "name": "Settings Home",
    "node_type": "screen",
    "children_strategy": {
      "type": "dynamic_match",
      "rule": {
        "match_mode": "multiple",
        "template_ref": "menu_container"
      }
    }
  },
  "template_registry": "builtin"
}
```

### 混合计划

```json
{
  "entry_app": "Settings",
  "mode": "hybrid",
  "root_node": {
    "node_id": "root",
    "name": "Settings Home",
    "node_type": "screen",
    "children_strategy": {
      "type": "static",
      "children": [
        {
          "node_id": "display",
          "name": "Display Settings",
          "node_type": "container",
          "children_strategy": {
            "type": "dynamic_match",
            "rule": {
              "match_mode": "multiple",
              "template_ref": "switch_slider"
            }
          }
        }
      ]
    }
  }
}
```

## 数据流

```
┌─────────────────┐
│  JSON Plan      │
│  (声明式)        │
└────────┬────────┘
         ▼
┌─────────────────┐
│ TraversalPlan   │
│ (解析后)         │
└────────┬────────┘
         ▼
┌─────────────────┐
│ GraphTraversal  │
│     Engine      │
│ (执行)          │
└────────┬────────┘
         ▼
┌─────────────────┐
│  TraversalResult│
│ (结果)          │
└─────────────────┘
```

## 扩展点

1. **自定义模板** - 添加应用特定的节点模板
2. **自定义匹配器** - 扩展动态匹配逻辑
3. **自定义策略** - 实现新的子节点生成策略
4. **自定义策略** - 扩展错误处理方式

## 相关模块

- `src/traversal/graph_engine.py` - 图遍历执行引擎
- `src/state_machine/` - 状态机实现
- `src/simulation/` - 仿真测试

## 测试

单元测试位于 `src/graph/test/`：
- `test_node.py` - 节点数据类测试
- `test_template.py` - 模板系统测试
- `test_plan.py` - 遍历计划测试
