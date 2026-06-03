# E2E仿真测试架构详解

## 📋 目录
- [1. 测试流程概述](#1-测试流程概述)
- [2. 核心组件架构](#2-核心组件架构)
- [3. 测试数据结构](#3-测试数据结构)
- [4. 断言引擎机制](#4-断言引擎机制)
- [5. 报告生成流程](#5-报告生成流程)
- [6. 数据转换规则](#6-数据转换规则)
- [7. 扩展指南](#7-扩展指南)

---

## 1. 测试流程概述

### 1.1 完整测试流程

```
┌─────────────────────────────────────────────────────────────┐
│                     E2E测试启动                              │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  1. 加载测试数据 (Test Fixtures)                             │
│     - plan_all.json (遍历计划)                               │
│     - pages_all.json (虚拟页面数据)                          │
│     - test_case.json (测试用例)                              │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  2. 初始化Mock组件                                           │
│     - MockVisionService (视觉分析)                          │
│     - MockActionExecutor (动作执行)                          │
│     - PageAnalyzer (页面分析)                               │
│     - InMemoryTracer (追踪记录)                             │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  3. 创建SimulationRunner                                     │
│     - 设置遍历计划                                           │
│     - 配置DFS遍历参数                                        │
│     - 初始化追踪系统                                         │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  4. 执行DFS遍历模拟                                          │
│     - 深度优先遍历页面树                                      │
│     - 执行导航和交互操作                                     │
│     - 记录状态转换和事件                                     │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  5. 生成TraceStep序列                                        │
│     - 每个操作生成一个TraceStep                              │
│     - 记录时间戳、状态、节点信息                              │
│     - 存储到InMemoryTracer                                   │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  6. 断言引擎验证                                             │
│     - 将TraceStep转换为自然语言事件                           │
│     - 与预期事件序列匹配                                      │
│     - 检查完成原因和步数范围                                 │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  7. 生成多种格式报告                                         │
│     - 文本报告 (TXT)                                         │
│     - ASCII树 (TXT)                                          │
│     - Mermaid图 (MD)                                         │
│     - HTML报告 (HTML)                                        │
│     - JSONL追踪数据 (JSONL)                                  │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  8. 输出测试结果                                             │
│     - 通过/失败状态                                          │
│     - 匹配统计和详细指标                                     │
│     - 完整的追踪报告                                         │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 数据流转关系

```
测试用例 (test_case.json)
    │
    ├── 预期事件序列 ────────┐
    │                        │
    └── 断言规则 ───────────┤
                             │
                             ▼
┌──────────────────────────────────────────┐
│  Mock执行过程                              │
│  ┌────────────────────────────────────┐  │
│  │ SimulationRunner                    │  │
│  │   ├── DFS遍历逻辑                   │  │
│  │   ├── 动作执行记录                  │  │
│  │   └── 状态转换追踪                  │  │
│  └────────────────────────────────────┘  │
│              │                           │
│              ▼                           │
│  ┌────────────────────────────────────┐  │
│  │ InMemoryTracer                     │  │
│  │   └── List[TraceStep]              │  │
│  └────────────────────────────────────┘  │
└──────────────────────────────────────────┘
                │
                ▼
┌──────────────────────────────────────────┐
│  数据转换层                               │
│  ┌────────────────────────────────────┐  │
│  │ TraceStep.to_dict()                │  │
│  │   └── Dict[str, Any]               │  │
│  └────────────────────────────────────┘  │
│                │                         │
│                ▼                         │
│  ┌────────────────────────────────────┐  │
│  │ TraceAsserter.step_to_nl()         │  │
│  │   └── String (自然语言事件)         │  │
│  └────────────────────────────────────┘  │
└──────────────────────────────────────────┘
                │
                ▼
┌──────────────────────────────────────────┐
│  断言引擎                                 │
│  ┌────────────────────────────────────┐  │
│  │ 事件序列匹配                        │  │
│  │ ├── 匹配预期事件                    │  │
│  │ ├── 检测缺失事件                    │  │
│  │ └── 识别额外事件                    │  │
│  └────────────────────────────────────┘  │
│              │                           │
│              ▼                           │
│  ┌────────────────────────────────────┐  │
│  │ 完成条件验证                        │  │
│  │ ├── 步数范围检查                    │  │
│  │ ├── 完成原因验证                    │  │
│  │ └── 违规项检测                      │  │
│  └────────────────────────────────────┘  │
└──────────────────────────────────────────┘
                │
                ▼
┌──────────────────────────────────────────┐
│  报告生成器                               │
│  ├── 文本报告生成器                       │
│  ├── ASCII树生成器                       │
│  ├── Mermaid图生成器                     │
│  ├── HTML报告生成器                      │
│  └── JSONL数据导出器                     │
└──────────────────────────────────────────┘
```

---

## 2. 核心组件架构

### 2.1 Mock组件系统

#### 2.1.1 MockVisionService
**职责**: 模拟视觉分析和元素识别
```python
class MockVisionService:
    def analyze_screen(self, context=None) -> Dict[str, Any]:
        # 根据当前路径返回对应的页面分析
        # 包含元素列表、页面类型、交互提示等
```

**关键特性**:
- 路径感知：根据current_path返回对应页面
- 元素模拟：生成虚拟的UI元素数据
- 类型识别：区分button、slider、switch等元素类型

#### 2.1.2 MockActionExecutor
**职责**: 模拟设备操作和状态管理
```python
class MockActionExecutor:
    def execute_action(self, action: Dict, context=None) -> Dict:
        # 模拟动作执行，更新路径状态
        # 返回执行结果和状态变化
```

**关键特性**:
- 状态维护：跟踪当前设备路径
- 动作模拟：执行点击、滑动、返回等操作
- 路径更新：自动维护导航路径栈

#### 2.1.3 PageAnalyzer
**职责**: 页面数据解析和元素处理
```python
class PageAnalyzer:
    def analyze_page(self, path: str) -> Dict[str, Any]:
        # 解析虚拟页面数据
        # 处理元素和items字段兼容性
        # 生成标准化的页面分析结果
```

**关键特性**:
- 路径解析：支持多层级路径匹配
- 字段兼容：处理items/elements字段差异
- 元素标准化：统一元素数据格式

### 2.2 追踪系统

#### 2.2.1 TraceStep数据结构
```python
@dataclass
class TraceStep:
    step_number: int              # 步骤序号
    timestamp: datetime           # 时间戳
    from_state: str              # 源状态
    to_state: str                # 目标状态
    node_id: str                 # 节点ID (路径)
    action: str                  # 动作类型
    screen_info: Dict[str, Any]  # 屏幕信息
    metadata: Dict[str, Any]     # 元数据 (包含completion_reason等)
```

#### 2.2.2 InMemoryTracer
**职责**: 追踪数据收集和存储
```python
class InMemoryTracer:
    def __init__(self):
        self.steps: List[TraceStep] = []
        self.visited_tree: Dict[str, VisitedNode] = {}
```

**关键方法**:
- `start_traversal()`: 初始化追踪会话
- `record_transition()`: 记录状态转换
- `render_tree()`: 生成ASCII树
- `render_mermaid()`: 生成Mermaid图
- `render_html()`: 生成HTML报告

### 2.3 SimulationRunner

**职责**: 协调整个仿真测试流程
```python
class SimulationRunner:
    def __init__(self, virtual_pages, plan, config=None):
        self.vision = MockVisionService(virtual_pages)
        self.action = MockActionExecutor()
        self.tracer = InMemoryTracer()

    def run(self) -> SimulationResult:
        # 执行完整的DFS遍历模拟
        # 生成详细的执行结果
        return SimulationResult(...)
```

**核心方法**:
- `_simulate_dfs_traversal()`: DFS遍历算法
- `_execute_element_action()`: 元素交互执行
- `_go_back()`: 返回操作
- `_log_trace_step()`: 追踪步骤记录

---

## 3. 测试数据结构

### 3.1 测试用例格式 (test_case.json)

```json
{
  "test_id": "e2e_all_traversal",
  "description": "全菜单遍历测试",
  "intent_slots": {
    "target_app": "设置",
    "scope": "all_menus",
    "element_handling": "full_interaction",
    "navigation": "adaptive",
    "restore": true,
    "depth": 3
  },
  "fixtures": {
    "plan_file": "plan_all.json",
    "pages_file": "pages_all.json"
  },
  "expected": {
    "completion_reason": "completed",
    "key_events": [
      "点击 'Settings' 按钮",
      "进入 SettingsPage",
      "操作 'Brightness' 滑块并恢复"
    ],
    "total_steps_min": 12,
    "total_steps_max": 30,
    "must_not_contain": ["错误", "异常", "失败"]
  },
  "assertions": {
    "visited_nodes_min": 6,
    "restore_operations_count_min": 4,
    "navigation_correctness": "depth_first"
  }
}
```

### 3.2 虚拟页面数据 (pages_all.json)

```json
{
  "root": {
    "current_path": "root",
    "page_type": "home",
    "elements": [
      {
        "element_id": "settings_btn",
        "text": "Settings",
        "element_type": "button",
        "action_hint": "navigate",
        "metadata": {"clickable": true}
      }
    ]
  },
  "Settings": {
    "current_path": "Settings",
    "page_type": "menu",
    "elements": [
      {
        "element_id": "display_item",
        "text": "Display",
        "element_type": "menu_item",
        "action_hint": "navigate",
        "metadata": {"clickable": true}
      },
      {
        "element_id": "sound_item",
        "text": "Sound",
        "element_type": "menu_item",
        "action_hint": "navigate",
        "metadata": {"clickable": true}
      }
    ]
  }
}
```

### 3.3 遍历计划 (plan_all.json)

```json
{
  "plan_id": "e2e_all_traversal_plan",
  "entry_app": "设置",
  "mode": "hybrid",
  "root_node": {
    "node_id": "root",
    "name": "HomeScreen",
    "children_strategy": {
      "type": "dynamic_match",
      "dynamic_rules": {
        "menu_rule": {
          "rule_id": "menu_rule",
          "match_condition": {"type": "menu_item"},
          "action": "generate_child"
        }
      }
    }
  },
  "intent_slots": {
    "depth": 3,
    "restore": true,
    "element_handling": "full_interaction"
  }
}
```

---

## 4. 断言引擎机制

### 4.1 TraceAsserter工作原理

#### 4.1.1 事件转换 (step_to_nl)

```python
@staticmethod
def step_to_nl(step: Dict[str, Any]) -> str:
    """
    将TraceStep字典转换为自然语言事件描述
    
    输入: {"action_type": "navigate", "target_info": {"element_id": "Settings"}}
    输出: "点击 'Settings' 按钮"
    """
    action_type = step.get("action_type", "unknown")
    target_info = step.get("target_info", {})
    target = target_info.get("element_id", "")
    
    # 特殊处理规则
    if action_type == "navigate" and target == "Settings":
        return "点击 'Settings' 按钮"
    elif action_type == "toggle" and "滑块" in target:
        return f"操作 '{target}' 滑块并恢复"
    # ... 更多规则
```

#### 4.1.2 事件匹配算法

```python
def assert_trace_matches_expected(trace, expected) -> AssertionResult:
    """
    1. 将所有trace步骤转换为自然语言事件
    2. 检查预期事件是否在实际事件中 (子序列匹配)
    3. 检测不应该出现的违规项
    4. 验证步数范围和完成原因
    """
    
    # 转换为自然语言事件
    actual_events = [TraceAsserter.step_to_nl(step) for step in trace]
    
    # 匹配预期事件
    matched_events = [event for event in key_events if event in actual_events]
    missing_events = [event for event in key_events if event not in actual_events]
    
    # 检测违规项
    violations = []
    for forbidden in must_not_contain:
        if any(forbidden in event for event in actual_events):
            violations.append(forbidden)
    
    return AssertionResult(
        success=(len(missing_events) == 0 and len(violations) == 0),
        key_events_matched=len(matched_events),
        missing_events=missing_events,
        violations=violations
    )
```

### 4.2 断言规则体系

#### 4.2.1 事件匹配规则

**子序列匹配**: 预期事件必须是实际事件的子序列
```python
# 预期: [A, B, C]
# 实际: [A, X, B, Y, C]  ✓ 匹配 (A,B,C是子序列)
# 实际: [B, A, C]        ✗ 不匹配 (顺序错误)
```

**事件描述规范**:
- 导航事件: "点击 '{目标}' 按钮/菜单项"
- 进入事件: "进入 {页面名}"
- 退出事件: "退出 {页面名}"
- 操作事件: "操作 '{目标}' {类型}并恢复"
- 完成事件: "遍历完成"

#### 4.2.2 数量验证规则

```python
# 步数范围验证
if not (min_steps <= total_steps <= max_steps):
    steps_valid = False

# 恢复操作计数
restore_count = sum(1 for event in actual_events if "恢复" in event)
if restore_count < min_restore:
    restore_operations_valid = False
```

#### 4.2.3 违规项检测

```python
# 检查不应该出现的关键词
forbidden_keywords = ["错误", "异常", "失败", "崩溃"]
for keyword in forbidden_keywords:
    if any(keyword in event for event in actual_events):
        violations.append(keyword)
```

---

## 5. 报告生成流程

### 5.1 报告生成架构

```
┌─────────────────────────────────────────────────────────────┐
│                  SimulationRunner                           │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  export_trace(format: str) -> str                    │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────┬───────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              │               │               │
              ▼               ▼               ▼
        ┌─────────┐     ┌─────────┐     ┌─────────┐
        │ "jsonl" │     │ "json"  │     │ "html"  │
        └─────────┘     └─────────┘     └─────────┘
              │               │               │
              ▼               ▼               ▼
        ┌─────────┐     ┌─────────┐     ┌─────────┐
        │ JSONL   │     │ JSON    │     │ HTML    │
        │ 导出器  │     │ 导出器  │     │ 渲染器  │
        └─────────┘     └─────────┘     └─────────┘
```

### 5.2 各格式报告生成器

#### 5.2.1 文本报告生成器

```python
def generate_text_report(result) -> str:
    """
    生成结构化的文本报告
    
    包含:
    - 测试概要 (状态、时间、原因)
    - 匹配统计 (匹配率、缺失/额外事件)
    - 执行统计 (总步数、节点数、动作数)
    - 详细事件列表 (每个步骤的描述)
    - 断言详情 (验证结果和违规项)
    """
    lines = []
    lines.append("=== E2E Test Report ===")
    lines.append(f"Status: {'PASS' if result['passed'] else 'FAIL'}")
    lines.append(f"Matched: {assertion.key_events_matched}/14")
    # ... 更多内容
    return "\n".join(lines)
```

#### 5.2.2 ASCII树生成器

```python
def render_tree(self, max_depth: Optional[int] = None) -> str:
    """
    生成ASCII格式的遍历树
    
    规则:
    - 递归遍历visited_tree
    - 使用 ├── 和 └── 表示层级
    - 使用 ✓ 标记已访问节点
    - 限制深度避免过深显示
    """
    lines = []
    def render_node(node_id, depth, is_last, prefix):
        node = self.visited_tree.get(node_id)
        visited_mark = "✓" if node.visited else "✗"
        connector = "└── " if is_last else "├── "
        lines.append(f"{prefix}{connector}{node.name} [{node.node_type}] {visited_mark}")
```

#### 5.2.3 Mermaid图生成器

```python
def render_mermaid(self) -> str:
    """
    生成Mermaid状态图语法
    
    格式:
    ```mermaid
    stateDiagram-v2
        [*] --> NODE_SELECT
        RUNNING --> RUNNING : Step 1
        RUNNING --> [*]
    ```
    """
    lines = ["stateDiagram-v2", "    [*] --> NODE_SELECT"]
    for step in self.steps:
        from_label = step.from_state.upper()
        to_label = step.to_state.upper()
        lines.append(f"    {from_label} --> {to_label} : Step {step.step_number}")
    return "\n".join(lines)
```

#### 5.2.4 HTML报告生成器

```python
def render_html(self) -> str:
    """
    生成交互式HTML报告
    
    包含:
    - 响应式CSS样式
    - 统计仪表板 (彩色卡片)
    - 操作对比表 (预期vs实际)
    - 状态转换追踪表
    - 遍历树可视化
    """
    html = f"""
    <!DOCTYPE html>
    <html>
    <head>
        <style>
            body {{ font-family: Arial, sans-serif; }}
            .metric {{ background: linear-gradient(135deg, #e8f5e8, #f0f9f0); }}
            .success {{ background-color: #e8f5e8; }}
            .warning {{ background-color: #fff3e0; }}
        </style>
    </head>
    <body>
        <div class="metric">
            <div class="metric-value">{total_steps}</div>
            <div class="metric-label">总步骤数</div>
        </div>
    </body>
    </html>
    """
    return html
```

#### 5.2.5 JSONL数据导出器

```python
def export_trace(self, format: str = "jsonl") -> str:
    """
    导出机器可读的JSONL格式
    
    规则:
    - 每行一个完整的JSON对象
    - 包含所有TraceStep字段
    - ISO格式时间戳
    - 标准化字段名 (action_type, current_node, target_info)
    """
    if format == "jsonl":
        lines = [json.dumps(step.to_dict()) for step in self.steps]
        return "\n".join(lines)
```

---

## 6. 数据转换规则

### 6.1 TraceStep到Dict的转换

```python
def to_dict(self) -> Dict[str, Any]:
    """
    转换规则:
    1. 基础字段直接映射
    2. action -> action_type (断言引擎期望)
    3. node_id -> current_node (断言引擎期望)
    4. screen_info -> target_info (断言引擎期望)
    5. metadata.completion_reason -> completion_reason (顶层字段)
    """
    target_info = {}
    if self.screen_info:
        target = self.screen_info.get("target", "")
        target_info = {
            "element_id": target,
            "text": target,
            "element_type": self.screen_info.get("element_type", "")
        }
    
    return {
        "step_number": self.step_number,
        "timestamp": self.timestamp.isoformat(),
        "action_type": self.action or "click",
        "current_node": self.node_id,
        "target_info": target_info,
        "completion_reason": self.metadata.get("completion_reason", "")
    }
```

### 6.2 自然语言事件生成规则

```python
def step_to_nl(step: Dict[str, Any]) -> str:
    """
    生成规则 (优先级从高到低):
    
    1. 特殊规则匹配 (精确匹配特定场景)
    2. 动作类型处理 (通用动作描述)
    3. 默认描述生成 (fallback)
    
    特殊规则示例:
    - navigate + Settings -> "点击 'Settings' 按钮"
    - navigate + Display -> "点击 'Display' 菜单项"
    - toggle + slider + restore -> "操作 '{target}' 滑块并恢复"
    - go_back + root -> "遍历完成"
    - go_back + exiting_page -> "退出 {exiting_page}"
    """
    
    # 1. 特殊规则
    if action_type == "navigate" and target == "Settings":
        return "点击 'Settings' 按钮"
    
    # 2. 通用动作处理
    elif action_type == "toggle" and has_restore:
        return f"操作 '{target}' {element_type}并恢复"
    
    # 3. 默认描述
    else:
        return f"{action_type} {current_node}"
```

### 6.3 字段映射表

| TraceStep字段 | to_dict输出 | 断言引擎使用 | 说明 |
|--------------|------------|-------------|------|
| `step_number` | `step_number` | - | 步骤序号 |
| `timestamp` | `timestamp` (ISO) | - | 时间戳 |
| `action` | `action_type` | ✓ | 动作类型 (navigate/click/toggle/go_back) |
| `node_id` | `current_node` | ✓ | 当前节点路径 |
| `screen_info.target` | `target_info.element_id` | ✓ | 目标元素ID |
| `screen_info.element_type` | `target_info.element_type` | ✓ | 元素类型 |
| `screen_info.restore` | `target_info.restore` | ✓ | 是否恢复操作 |
| `metadata.completion_reason` | `completion_reason` | ✓ | 完成原因 |

---

## 7. 扩展指南

### 7.1 添加新的测试用例

```bash
# 1. 创建测试数据目录
mkdir tests/simulation/fixtures/e2e_new_test/

# 2. 创建测试数据文件
# - plan.json: 遍历计划
# - pages.json: 虚拟页面数据  
# - test_case.json: 测试用例

# 3. 运行测试
python -c "
from tests.simulation.helpers.test_runner import SimulationTestRunner
runner = SimulationTestRunner()
result = runner.run_simulation_test('tests/simulation/fixtures/e2e_new_test/test_case.json')
print(f'Test: {\"PASS\" if result[\"passed\"] else \"FAIL\"}')"
```

### 7.2 自定义断言规则

```python
# 在tests/simulation/helpers/assertions.py中添加

class CustomTraceAsserter(TraceAsserter):
    @staticmethod
    def step_to_nl(step: Dict[str, Any]) -> str:
        # 自定义事件描述逻辑
        action_type = step.get("action_type", "unknown")
        if action_type == "custom_action":
            return "自定义操作描述"
        return super().step_to_nl(step)
```

### 7.3 扩展报告格式

```python
# 在src/simulation/runner.py中添加

def export_trace(self, format: str = "jsonl") -> str:
    if format == "custom_format":
        return self._generate_custom_format()
    # ... 现有格式

def _generate_custom_format(self) -> str:
    # 自定义报告生成逻辑
    pass
```

### 7.4 添加新的可视化

```python
# 在src/simulation/visualizer.py中添加

class InMemoryTracer:
    def render_custom_chart(self) -> str:
        """
        生成自定义可视化图表
        
        可以是:
        - SVG图表
        - DOT图 (Graphviz)
        - PlantUML图
        - 自定义格式
        """
        pass
```

---

## 8. 常见问题和解决方案

### 8.1 事件匹配失败

**问题**: 预期事件与实际事件不匹配
**原因**: 事件描述格式不一致
**解决**:
- 检查`TraceAsserter.step_to_nl()`中的特殊规则
- 确保预期事件使用正确的描述格式
- 在测试中打印实际事件进行对比

### 8.2 追踪数据缺失

**问题**: TraceStep缺少必要字段
**原因**: `_log_trace_step()`调用不完整
**解决**:
- 确保所有关键参数都传递给`_log_trace_step()`
- 检查`to_dict()`方法是否正确映射字段
- 验证`screen_info`和`metadata`的设置

### 8.3 报告生成错误

**问题**: HTML/Mermaid生成失败
**原因**: 数据结构不兼容
**解决**:
- 确保使用`VisitedNode`对象而不是字典
- 检查`visited_tree`的数据结构
- 验证字段访问方式 (使用属性而非键访问)

### 8.4 路径解析问题

**问题**: PageAnalyzer找不到页面
**原因**: 路径格式不匹配
**解决**:
- 使用`current_path`字段进行路径匹配
- 实现`_normalize_path()`方法
- 处理路径边界情况 (root, 空路径等)

---

**文档版本**: v1.0
**最后更新**: 2026-06-03
**维护者**: Uni-Claw开发团队