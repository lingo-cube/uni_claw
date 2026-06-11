# PRD V6.16.0: 测试代码硬编码消除

> **版本**: V6.16.0
> **日期**: 2026-06-10
> **依赖**: 无
> **状态**: 设计阶段
> **最后修订**: 2026-06-11 (基于业务原则修正分类标准)

---

## 0. 核心原则

> 本 PRD 遵循以下硬编码消除原则：
>
> 1. **有意义的类型才需要归类** - 只有代表明确业务/技术语义的值才提取为常量
> 2. **业务生成值使用魔法数字** - 坐标、尺寸等依赖被测应用的值应保持原样
> 3. **枚举应来自源代码** - 状态、决策等枚举值应从源码导入，不应在测试中重复定义
> 4. **常量形成设计规范** - 一旦归类为常量，即成为设计规范，修改需评估影响
> 5. **测试数据保留灵活性** - 测试输入数据、边界值应保持直接值，便于测试场景变化

---

## 1. 目的与目标

### 1.1 目的

消除测试代码中**不必要的硬编码**，提高测试的可维护性和可读性，同时保留测试数据和业务生成值的灵活性。

### 1.2 目标

- [ ] 建立测试配置常量集中管理机制（仅限真正的配置常量）
- [ ] 消除重复的配置值（timeout、retry、threshold 语义位置）
- [ ] 规范化测试ID生成规则
- [ ] 提供测试数据工厂方法（而非硬编码常量）
- [ ] 建立枚举值导入规范（从源码导入，不重复定义）

### 1.3 明确不做的

- ❌ 不替换坐标值（业务生成，应保留魔法数字或使用工厂方法生成测试数据）
- ❌ 不替换屏幕尺寸（测试数据，应在工厂方法中定义）
- ❌ 不替换业务阈值（性能要求、验证标准属于业务逻辑）
- ❌ 不过度抽象测试输入数据（load_factor、user_count 等）

---

## 2. 背景与分析

### 2.1 硬编码现状

通过代码扫描，识别出以下硬编码模式：

| 类别 | 严重程度 | 估算数量 | 主要位置 | 是否替换 |
|------|----------|----------|----------|----------|
| 测试节点/元素ID | 低 | 50+ | `test_transition_to.py`, `test_trace_analyzer.py` | ✅ 使用生成器 |
| 滚动位置语义值 | 中 | 40+ | `test_scrollable_vision.py`, `test_models.py` | ⚠️ 部分替换 |
| 配置常量（超时/重试） | 中 | 30+ | 多个测试文件 | ✅ 替换为常量 |
| 枚举/状态字符串 | 中 | 25+ | FSM测试 | ✅ 从源码导入 |
| 坐标值 | 低 | 10+ | 集成测试 | ❌ 保留/使用工厂 |
| 设备尺寸 | 低 | 5+ | 模拟测试 | ❌ 工厂方法管理 |
| 基线值 | 低 | 10+ | `expected_behavior.yaml` | ❌ 测试数据，保留 |

### 2.2 具体问题示例

#### 2.2.1 硬编码测试ID

```python
# test_transition_to.py
fsm._current_node_id = "node123"
node_id="target_node_456"
assert "Current node: node123" in error_message
```

**问题**: 无语义的ID字符串，难以维护和追踪

#### 2.2.2 枚举字符串硬编码

```python
# 测试中硬编码枚举值
assert result.completion_reason == "ALL_VISITED"
assert next_state == TraversalState.NODE_SELECT
assert span.decision == "AUTO_ESCAPE"
```

**问题**:
1. 字符串容易拼写错误
2. 重构时容易遗漏
3. 与源码重复定义

**正确做法**: 从源码导入枚举
```python
from src.state_machine.global_fsm import GlobalState
from src.state_machine.traversal_fsm import TraversalState
from src.state_machine.container_handler import FallbackAction

assert result.completion_reason == "ALL_VISITED"  # 如果是字符串字段
assert next_state == TraversalState.NODE_SELECT   # 如果是枚举
assert span.decision == FallbackAction.AUTO_ESCAPE
```

#### 2.2.3 配置常量分散

```python
# dashboard_performance_v2.py
CONCURRENT_REQUESTS = 20
timeout = 10

# test_error_handler.py
context = {"retry_count": 0, "max_retries": 3}
```

**问题**: 相同配置值在多处定义，不一致风险高

#### 2.2.4 坐标值（保留为测试数据）

```python
# 这些坐标值是业务生成的，不应替换为常量
coordinate = {'x': 0.5, 'y': 0.5}
Coordinate(x=0.1, y=0.1)
```

**原因**: 这些值代表 UI 元素在屏幕上的位置，由被测应用的界面结构决定，属于测试输入数据，不是框架配置常量。

**正确处理**: 使用工厂方法生成，或在测试中直接指定

#### 2.2.5 滚动位置值（部分替换）

```python
# 场景1：语义明确的常见位置 → 可以使用常量
ScrollSegment(threshold=0.0, elements=[...])  # START
ScrollSegment(threshold=0.5, elements=[...])  # HALF
ScrollSegment(threshold=1.0, elements=[...])  # END

# 场景2：任意测试位置 → 使用魔法数字
ScrollSegment(threshold=0.33, elements=[...])  # 1/3 位置，非常规值
ScrollSegment(threshold=0.73, elements=[...])  # 特定测试场景
```

---

### 2.3 硬编码分类标准（核心规则）

> **关键原则**: 只替换"配置常量"，保留"测试数据"、"业务值"和"业务生成值"

| 分类 | 定义 | 示例 | 是否替换 | 理由 |
|------|------|------|----------|------|
| **框架配置常量** | 控制测试执行行为的参数 | `timeout=10`, `max_retries=3` | ✅ 替换 | 测试框架配置项，应集中管理 |
| **语义位置常量** | 具有明确语义的常见位置值 | ScrollThreshold.START/HALF/END | ⚠️ 可选 | 仅替换常见语义位置，保留任意值能力 |
| **坐标值** | UI元素屏幕坐标 | `{'x': 0.5, 'y': 0.5}` | ❌ 不替换 | 业务生成值，保留魔法数字或工厂 |
| **设备尺寸** | 模拟设备的屏幕尺寸 | `1440, 3168` | ❌ 不替换 | 测试数据，应在工厂方法中管理 |
| **测试数据** | 测试场景的输入数据 | `load_factor=0.5`, `user_count=100` | ❌ 不替换 | 测试输入，需要灵活性 |
| **业务阈值** | 业务逻辑验证标准 | `assert score >= 0.5` | ❌ 不替换 | 业务规则，不应被常量影响 |
| **边界值** | 边界测试的特定值 | `value=0`, `value=-1`, `value=999999` | ❌ 不替换 | 边界测试需要精确控制 |
| **临时值** | 单次测试专用值 | `temp_value=42` | ❌ 不替换 | 无复用价值，不应提取 |
| **枚举字符串** | 源码已定义的枚举 | `"NODE_SELECT"`, `"AUTO_ESCAPE"` | ✅ 从源码导入 | 避免重复定义和拼写错误 |

#### 2.3.1 判断决策树

```
遇到硬编码值时，问自己：
1. 这个值是框架配置吗？(timeout, retry, concurrent)
   → 是 → 替换为常量

2. 这个值是业务生成的吗？(坐标, 设备尺寸)
   → 是 → 不替换，保留为测试数据

3. 这个值是常见的语义位置吗？(START, HALF, END)
   → 是 → 可选使用常量
   → 否（如 0.33）→ 保留魔法数字

4. 这个值是测试输入数据吗？(load factor, test count)
   → 是 → 不替换，保留灵活性

5. 这个值是业务规则判断吗？(assert score >= X)
   → 是 → 不替换，这是业务逻辑

6. 这个值代表源码中定义的枚举吗？
   → 是 → 从源码导入枚举，不使用字符串
```

#### 2.3.2 模糊场景的处理

| 场景 | 判断 | 处理 |
|------|------|------|
| `threshold=0.5` 在 ScrollSegment 中 | 语义位置常量 | 可选使用 `ScrollThreshold.HALF` 或保留 0.5 |
| `threshold=0.33` 在 ScrollSegment 中 | 非常规测试值 | 保留魔法数字 |
| `{'x': 0.5, 'y': 0.5}` 坐标值 | 业务生成值 | 不替换，保留或使用工厂 |
| `1440, 3168` 屏幕尺寸 | 测试数据（设备参数） | 不替换，在工厂方法中定义 |
| `timeout=10` 在所有测试中 | 框架配置常量 | 替换为 `Timeout.LONG` |
| `assert result.status == "COMPLETED"` | 枚举字符串 | 从源码导入 `GlobalState.COMPLETED` |
| `expected_value=0.5` 在断言中 | 测试验证值 | 不替换，这是测试的预期结果 |

---

### 2.4 枚举值导入规范

> **原则**: 测试代码应从源码导入枚举，避免硬编码字符串或重复定义

#### 2.4.1 常见枚举导入

```python
# 状态枚举
from src.state_machine.global_fsm import GlobalState
from src.state_machine.traversal_fsm import TraversalState

# 决策/动作枚举
from src.state_machine.container_handler import FallbackAction
from src.graph.node import NodeType, Operation

# 其他枚举
from src.models.content_models import Coordinate, Direction
```

#### 2.4.2 使用示例

**Before (硬编码字符串):**
```python
assert result.status == "COMPLETED"
assert next_state == "NODE_SELECT"
assert decision == "AUTO_ESCAPE"
```

**After (导入枚举):**
```python
from src.state_machine.global_fsm import GlobalState
from src.state_machine.traversal_fsm import TraversalState
from src.state_machine.container_handler import FallbackAction

assert result.status == GlobalState.COMPLETED
assert next_state == TraversalState.NODE_SELECT
assert decision == FallbackAction.AUTO_ESCAPE
```

#### 2.4.3 字符串字段 vs 枚举

注意区分字符串字段和枚举类型：

```python
# completion_reason 是字符串字段，但常见值应从常量导入
completion_reason: str  # 字符串字段
# 常见值：ALL_VISITED, ERROR, TIMEOUT等

# state 是枚举类型，应使用枚举
state: TraversalState  # 枚举类型
```

---

### 2.5 ID 依赖扫描策略

> **关键原则**: 替换 ID 前必须扫描所有引用点，确保一次性替换全部引用

#### 2.5.1 问题场景

```python
# 当前代码：ID "node123" 出现在多处
fsm._current_node_id = "node123"              # 赋值
assert "Current node: node123" in error_message  # 断言
logger.info(f"Processing node123")              # 日志
assert span.metadata["node_id"] == "node123"    # 另一个断言
```

**风险**: 如果只替换赋值处的 `node123`，其他三处会断言失败

#### 2.5.2 扫描与替换流程

**步骤 1: 扫描所有引用点**
```bash
# 使用 grep 找出所有包含该 ID 的行
grep -r "node123" tests/state_machine/test_transition_to.py
```

**输出示例**:
```
Line 15: fsm._current_node_id = "node123"
Line 18: assert "Current node: node123" in error_message
Line 42: logger.info(f"Processing node123")
Line 67: assert span.metadata["node_id"] == "node123"
```

**步骤 2: 统一替换**

```python
# Before：分散的硬编码
fsm._current_node_id = "node123"
assert "Current node: node123" in error_message

# After：使用同一变量
node_id = "node123"  # 或 TestIdGenerator.node_id("test", 123)
fsm._current_node_id = node_id
assert f"Current node: {node_id}" in error_message  # 使用 f-string
logger.info(f"Processing {node_id}")
assert span.metadata["node_id"] == node_id
```

---

## 3. 解决方案设计

### 3.1 架构设计

```
tests/
├── config/                    # 新增：测试配置目录
│   ├── __init__.py
│   ├── constants.py          # 框架配置常量（timeout, retry等）
│   └── test_ids.py           # 测试ID生成器
├── factories/                 # 新增/扩展现有：测试数据工厂
│   ├── __init__.py
│   ├── device_factory.py     # 设备/坐标测试数据工厂
│   ├── node_factory.py       # 节点数据工厂（扩展现有）
│   ├── element_factory.py    # 元素数据工厂（扩展现有）
│   └── state_factory.py      # 状态数据工厂
└── ...
```

### 3.2 核心组件

#### 3.2.1 `tests/config/constants.py` - 仅框架配置常量

```python
"""测试框架配置常量 - 仅包含控制测试执行行为的参数"""

# 超时配置
class Timeout:
    """测试超时配置常量"""
    SHORT = 2      # 秒 - 快速操作
    NORMAL = 5     # 秒 - 一般操作
    LONG = 10      # 秒 - 耗时操作
    FLUSH = 5.0    # 秒 - 文件刷新

# 重试配置
class Retry:
    """重试配置常量"""
    MAX_DEFAULT = 3
    MAX_EXTENDED = 5
    COUNT_ZERO = 0
    COUNT_ONE = 1

# 并发配置
class Concurrency:
    """并发配置常量"""
    REQUESTS = 20
    MAX_CHILDREN_DEFAULT = 10
    MAX_CHILDREN_SMALL = 2

# 滚动位置语义常量（可选使用）
class ScrollThreshold:
    """
    滚动位置语义常量

    注意：这是可选的辅助常量。测试可以直接使用数值。
    只有在测试确实表达"起始/中间/结束"语义时才使用这些常量。
    对于任意测试位置（如 0.33），应直接使用数值。
    """
    START = 0.0
    QUARTER = 0.25
    HALF = 0.5
    THREE_QUARTER = 0.75
    END = 1.0
```

**设计说明**:
- 移除了 `Coordinate` 类（坐标是业务生成值，不应作为常量）
- 移除了 `ScreenSize` 类（设备尺寸是测试数据，应在工厂中管理）
- `ScrollThreshold` 标注为可选，强调保留使用魔法数字的能力

#### 3.2.2 `tests/config/test_ids.py` - ID生成器

```python
"""测试ID生成器"""

import uuid
from typing import Optional

class TestIdGenerator:
    """统一测试ID生成"""

    @staticmethod
    def node_id(name: str, index: Optional[int] = None) -> str:
        """生成节点ID"""
        suffix = f"_{index}" if index is not None else ""
        return f"{name.lower().replace(' ', '_')}{suffix}"

    @staticmethod
    def span_id(prefix: str, seq: int) -> str:
        """生成span ID"""
        return f"{prefix}_{seq}"

    @staticmethod
    def trace_id() -> str:
        """生成唯一trace ID"""
        return f"trace_{uuid.uuid4().hex[:8]}"

    @staticmethod
    def element_id(type_name: str, text: str) -> str:
        """生成元素ID"""
        return f"{type_name.lower()}_{text.lower().replace(' ', '_')}"
```

#### 3.2.3 `tests/factories/device_factory.py` - 新增：设备/坐标数据工厂

```python
"""设备模拟和坐标测试数据工厂"""

from dataclasses import dataclass
from typing import Dict, Any

@dataclass
class DeviceSpec:
    """设备规格"""
    width: int
    height: int
    name: str

class DeviceFactory:
    """设备测试数据工厂"""

    # 预定义设备规格
    DEFAULT_PHONE = DeviceSpec(width=1440, height=3168, name="default_phone")
    SMALL_PHONE = DeviceSpec(width=1080, height=2340, name="small_phone")
    TABLET = DeviceSpec(width=2048, height=2732, name="tablet")

    @staticmethod
    def create_coordinate(x_ratio: float, y_ratio: float) -> Dict[str, float]:
        """
        创建坐标测试数据

        Args:
            x_ratio: 水平位置比例 (0.0-1.0)
            y_ratio: 垂直位置比例 (0.0-1.0)

        Returns:
            坐标字典
        """
        return {'x': x_ratio, 'y': y_ratio}

    @staticmethod
    def center_coordinate() -> Dict[str, float]:
        """中心位置坐标"""
        return DeviceFactory.create_coordinate(0.5, 0.5)

    @staticmethod
    def top_menu_coordinate() -> Dict[str, float]:
        """顶部菜单位置坐标"""
        return DeviceFactory.create_coordinate(0.5, 0.05)

@dataclass
class Coordinate:
    """坐标数据类（用于需要对象的场景）"""
    x: float
    y: float

class CoordinateFactory:
    """坐标数据工厂（替代原常量方案）"""

    @staticmethod
    def create(x: float, y: float) -> Coordinate:
        """创建坐标对象"""
        return Coordinate(x=x, y=y)

    @staticmethod
    def center() -> Coordinate:
        """中心位置"""
        return Coordinate(x=0.5, y=0.5)

    @staticmethod
    def top_left() -> Coordinate:
        """左上角"""
        return Coordinate(x=0.0, y=0.0)

    @staticmethod
    def top_menu() -> Coordinate:
        """顶部菜单区域"""
        return Coordinate(x=0.5, y=0.05)
```

**设计说明**:
- 使用工厂方法而非常量，保持灵活性
- 支持任意坐标值，不仅限于预定义位置
- 预定义设备规格（ScreenSize）移至此处

#### 3.2.4 扩展现有工厂

扩展 `tests/helpers/factories.py` 或 `tests/factories/node_factory.py`：

```python
"""节点数据工厂（扩展）"""

from tests.config.test_ids import TestIdGenerator
from tests.factories.device_factory import CoordinateFactory
from src.graph.node import TraversalNode, NodeType, Operation

class NodeFactory:
    """节点数据工厂"""

    @staticmethod
    def create_leaf_action(text: str, index: int = 0) -> TraversalNode:
        """创建叶节点"""
        return TraversalNode(
            node_id=TestIdGenerator.node_id(text, index),
            name=text,
            node_type=NodeType.LEAF_ACTION,
            operation=Operation(action="click"),
            ...
        )

    @staticmethod
    def create_container(name: str, children: list) -> TraversalNode:
        """创建容器节点"""
        return TraversalNode(
            node_id=TestIdGenerator.node_id(name),
            name=name,
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.STATIC,
                static_children=children
            ),
            ...
        )
```

### 3.3 迁移模式

#### 模式1: 框架配置常量替换（必须）

**Before:**
```python
# 分散的魔法数字
context = {"max_retries": 3}
timeout = 10
CONCURRENT_REQUESTS = 20
```

**After:**
```python
from tests.config.constants import Timeout, Retry, Concurrency

context = {"max_retries": Retry.MAX_DEFAULT}
timeout = Timeout.LONG
CONCURRENT_REQUESTS = Concurrency.REQUESTS
```

#### 模式2: 滚动位置使用（可选）

**Before:**
```python
ScrollSegment(threshold=0.5, elements=[...])
```

**After (语义明确时):**
```python
from tests.config.constants import ScrollThreshold

ScrollSegment(threshold=ScrollThreshold.HALF, elements=[...])
```

**或保留魔法数字（测试特定值时）:**
```python
# 任意测试位置，不需要常量
ScrollSegment(threshold=0.33, elements=[...])
```

#### 模式3: 坐标使用工厂方法（新增）

**Before:**
```python
coordinate = {'x': 0.5, 'y': 0.5}
```

**After (使用工厂):**
```python
from tests.factories.device_factory import CoordinateFactory

coordinate = CoordinateFactory.center()
# 或任意位置
coordinate = CoordinateFactory.create(0.3, 0.7)
```

#### 模式4: 设备规格使用工厂

**Before:**
```python
# 硬编码设备尺寸
width, height = 1440, 3168
```

**After:**
```python
from tests.factories.device_factory import DeviceFactory

device = DeviceFactory.DEFAULT_PHONE
width, height = device.width, device.height
```

#### 模式5: 枚举导入（必须）

**Before:**
```python
# 硬编码字符串
assert result.status == "COMPLETED"
assert decision == "AUTO_ESCAPE"
```

**After:**
```python
from src.state_machine.global_fsm import GlobalState
from src.state_machine.container_handler import FallbackAction

assert result.status == GlobalState.COMPLETED
assert decision == FallbackAction.AUTO_ESCAPE
```

#### 模式6: ID生成替换

**Before:**
```python
node_id="child1", node_id="child2"
```

**After:**
```python
from tests.config.test_ids import TestIdGenerator

node_id=TestIdGenerator.node_id("Child", 1),
node_id=TestIdGenerator.node_id("Child", 2),
```

---

## 4. 实施计划

### 4.1 阶段划分

| 阶段 | 任务 | 优先级 | 估计工时 |
|------|------|--------|----------|
| **Phase 1** | 建立基础设施 | P0 | 4h |
| - | 创建 `tests/config/` 目录结构 | | |
| - | 实现 `constants.py`（仅框架配置） | | |
| - | 实现 `test_ids.py` | | |
| - | 实现 `device_factory.py` | | |
| **Phase 2** | 迁移框架配置常量 | P0 | 4h |
| - | 迁移超时/重试配置 | | |
| - | 迁移并发配置 | | |
| - | 添加枚举导入语句 | | |
| **Phase 3** | 迁移测试ID | P1 | 4h |
| - | 迁移 `test_transition_to.py` | | |
| - | 迁移 `test_trace_analyzer.py` | | |
| - | 迁移其他使用硬编码ID的测试 | | |
| **Phase 4** | 引入坐标/设备工厂 | P1 | 3h |
| - | 使用 CoordinateFactory 替代直接坐标构造 | | |
| - | 使用 DeviceFactory 管理设备规格 | | |
| **Phase 5** | 滚动阈值可选迁移 | P2 | 2h |
| - | 评估是否需要 ScrollThreshold 常量 | | |
| - | 选择性迁移语义明确的位置值 | | |

**总工时**: 17h (相比原 PRD 减少 8h)

### 4.2 影响范围详细清单

#### 4.2.1 新增文件（4个）

| 文件路径 | 类型 | 行数估算 | 说明 |
|---------|------|----------|------|
| `tests/config/__init__.py` | 新增 | ~10 | 配置模块初始化 |
| `tests/config/constants.py` | 新增 | ~60 | 框架配置常量（移除坐标/尺寸） |
| `tests/config/test_ids.py` | 新增 | ~50 | 测试ID生成器 |
| `tests/factories/device_factory.py` | 新增 | ~80 | 设备/坐标工厂（替代常量方案） |

**影响分析**:
- ✅ 新增文件，不影响现有测试
- ✅ 可独立开发验证后再集成

---

#### 4.2.2 Phase 2: 框架配置迁移（8个文件）

| 文件 | 硬编码类型 | 修改次数 | 具体变更 |
|------|------------|----------|----------|
| `tests/v6/test_trace_integration.py` | timeout | 3 | 替换 `timeout=5.0` 为 `Timeout.FLUSH` |
| `tests/v6/test_trace_storage.py` | timeout | 6 | 同上 |
| `tests/dashboard_performance_v2.py` | timeout, concurrent | 8 | 替换 `timeout=10` 为 `Timeout.LONG`，`CONCURRENT_REQUESTS=20` 为 `Concurrency.REQUESTS` |
| `tests/v6/test_error_handler.py` | max_retries | 20 | 替换 `"max_retries": 3` 为 `Retry.MAX_DEFAULT` |
| `tests/v6/test_state_machine_intelligence.py` | max_retries | 5 | 同上 |
| `tests/v6/test_state_machine_error_integration.py` | max_retries | 3 | 同上 |
| `tests/v6/settings/test_target_search.py` | max_retries | 1 | 同上 |
| `tests/v6/test_settings_full_traversal.py` | max_children | 1 | 替换 `max_children=10` 为 `Concurrency.MAX_CHILDREN_DEFAULT` |
| `tests/state_machine/test_branch_handling.py` | max_children | 1 | 替换 `max_children=2` 为 `Concurrency.MAX_CHILDREN_SMALL` |

**影响分析**:
- 🟢 **低风险**: 纯常量替换，逻辑不变
- ✅ **易验证**: 逻辑不变，只是常量引用

---

#### 4.2.3 Phase 3: 测试ID迁移（8个文件）

| 文件 | 硬编码类型 | 修改次数 | 具体变更 |
|------|------------|----------|----------|
| `tests/state_machine/test_transition_to.py` | node_id | 15 | `"node123"` → `TestIdGenerator.node_id("test", 123)` |
| `tests/v6/test_trace_analyzer.py` | span/trace ID | 20 | `"t1"`, `"sp1"` → `TestIdGenerator.trace_id()`, `span_id()` |
| `tests/v6/test_trace_models.py` | span/trace ID | 8 | 同上 |
| `tests/v6/test_trace_recovery.py` | span/trace ID | 6 | 同上 |
| `tests/v6/integration/test_trace_recording.py` | node_id | 5 | `"child1"` → `TestIdGenerator.node_id("Child", 1)` |
| `tests/v6/test_v6_9_dynamic_matching.py` | node_id | 10 | `"child1"`, `"child2"` → `node_id("Child", 1)` |
| `tests/v6/unit/test_problem_detector.py` | node_id | 2 | `"btn1"` → `TestIdGenerator.element_id("button", "1")` |
| `tests/v6/unit/test_behavior_validator.py` | node_id | 1 | 同上 |

---

#### 4.2.4 Phase 4: 坐标/设备工厂迁移（5个文件）

| 文件 | 硬编码类型 | 修改次数 | 具体变更 |
|------|------------|----------|----------|
| `tests/test_vision_service.py` | coordinate | 3 | 使用 `CoordinateFactory.create()` |
| `tests/v6/settings/test_match_result.py` | coordinate | 1 | 使用 `CoordinateFactory.center()` |
| `tests/v6/test_v6_6_trace_handler_metrics.py` | coordinate | 1 | 同上 |
| `tests/integration/test_clicks.py` | screen size | 2 | 使用 `DeviceFactory.DEFAULT_PHONE` |
| `tests/v6/unit/test_stateful_mock_vision.py` | coordinate | 3 | 使用 `CoordinateFactory` |

---

#### 4.2.5 Phase 5: 滚动阈值可选迁移（评估后决定）

| 文件 | 硬编码类型 | 修改次数 | 具体变更 |
|------|------------|----------|----------|
| `tests/simulation/scroll/test_scrollable_vision.py` | threshold | 36 | 评估是否替换常见值 |
| `tests/simulation/scroll/test_models.py` | threshold | 22 | 评估是否替换常见值 |
| `tests/simulation/scroll/test_scrollable_action.py` | threshold | 7 | 评估是否替换常见值 |
| `tests/simulation/scroll/test_scenarios.py` | threshold | 27 | 评估是否替换常见值 |
| `tests/simulation/scroll/test_data_store.py` | threshold | 16 | 评估是否替换常见值 |

**说明**: 此阶段为 P2，先评估价值再决定是否实施。如果 0.0/0.5/1.0 使用频率高且语义清晰，可选择性替换。

---

#### 4.2.6 枚举导入（多个文件）

| 文件 | 修改 | 说明 |
|------|------|------|
| 多个 FSM 测试 | 添加枚举导入 | 从源码导入 `TraversalState`, `GlobalState`, `FallbackAction` |
| 多个 trace 测试 | 添加枚举导入 | 导入相关枚举类型 |

---

### 4.3 文件修改汇总表

| Phase | 文件数 | 修改行数估算 | 新增代码 | 风险 |
|-------|--------|-------------|----------|------|
| Phase 1 | 4 (新增) | 0 | ~200行 | 🟢 无 |
| Phase 2 | 9 | ~50 | ~60行 | 🟢 低 |
| Phase 3 | 8 | ~80 | ~50行 | 🟡 中 |
| Phase 4 | 5 | ~15 | ~0行 | 🟢 低 |
| Phase 5 | 5 (可选) | 待评估 | 待评估 | 🟢 低 |
| **总计** | **31** | **~145** | **~310行** | **🟢 低** |

相比原 PRD (39个文件, ~330行修改)，新版大幅减少了影响范围。

---

### 4.4 回滚方案

| 回滚场景 | 回滚方式 | 恢复时间 |
|---------|---------|----------|
| Phase 1 失败 | 删除新增目录 | 1分钟 |
| Phase 2/3 失败 | Git revert commit | 5分钟 |
| 全部失败 | Git reset 到分支起点 | 10分钟 |

---

## 5. 验证标准

### 5.1 完成标准

- [ ] 框架配置常量（timeout、retry、concurrent）已替换
- [ ] 测试数据和业务生成值未错误替换
- [ ] 测试ID生成器覆盖所有ID创建场景
- [ ] 坐标和设备尺寸通过工厂方法管理（非常量）
- [ ] 枚举值从源码导入，无硬编码字符串
- [ ] 所有测试通过（无回归）
- [ ] 代码覆盖率不降低

### 5.2 质量标准

| 指标 | 目标 | 测量方法 |
|------|------|----------|
| 配置常量替换率 | 100% | grep timeout/retry/concurrent，应无裸值 |
| 测试数据保留率 | 100% | 坐标、尺寸保持为数据/工厂 |
| 枚举导入率 | 100% | grep 硬编码枚举字符串，应为0 |
| ID引用点覆盖率 | 100% | grep旧ID应返回空 |
| 测试可读性 | 提升 | Code Review |
| 维护成本 | 降低 | 修改常量影响范围可控 |

### 5.3 验证命令

**验证配置常量已替换：**
```bash
# 应该找不到这些模式
grep -r "timeout.*=.*[0-9]" tests/ | grep -v "Timeout\."
grep -r "max_retries.*=.*[0-9]" tests/ | grep -v "Retry\."
grep -r "CONCURRENT_REQUESTS.*=.*[0-9]" tests/

# 应该找到常量引用
grep -r "Timeout\." tests/ | wc -l
grep -r "Retry\." tests/ | wc -l
```

**验证坐标/尺寸未被错误替换为常量：**
```bash
# 应该没有 Coordinate. 常量引用（应该用工厂或魔法数字）
grep -r "Coordinate\.CENTER\|Coordinate\.TOP" tests/
# 应返回空或仅工厂方法调用

# 设备尺寸应该通过工厂或直接值
grep -r "ScreenSize\." tests/
# 应返回空
```

**验证枚举已导入：**
```bash
# 检查是否从源码导入了枚举
grep -r "from src.state_machine.*import.*State" tests/ | wc -l
# 应该 > 0

# 检查是否还有硬编码枚举字符串
grep -r '== "NODE_SELECT"\|== "AUTO_ESCAPE"' tests/
# 应该很少或没有
```

---

## 6. 风险与缓解

### 6.1 总体风险评估

| 维度 | 评估 | 说明 |
|------|------|------|
| **技术复杂度** | 🟢 低 | 主要是字符串/数字替换，工厂方法简单 |
| **影响范围** | 🟢 低 | 31个测试文件，相比原方案减少 |
| **回归风险** | 🟢 低 | 常量值不变，工厂方法行为一致 |
| **回滚难度** | 🟢 低 | 纯测试代码，易于回滚 |
| **总体风险** | 🟢 低 | 风险可控，范围合理 |

### 6.2 关键原则符合性检查

| 原则 | 检查 | 符合度 |
|------|------|--------|
| 有意义的类型才归类 | 仅常量化框架配置和语义位置 | ✅ 符合 |
| 业务生成值用魔法数字 | 坐标、尺寸保留为测试数据/工厂 | ✅ 符合 |
| 枚举形成设计规范 | 从源码导入，不重复定义 | ✅ 符合 |
| 常量不可随意修改 | 设计规范文档化 | ✅ 符合 |
| 测试数据保留灵活性 | 支持工厂和直接值 | ✅ 符合 |

### 6.3 具体风险与缓解

| 风险项 | 风险等级 | 缓解措施 |
|--------|----------|----------|
| **配置常量遗漏** | 🟢 低 | 按 grep 扫描结果逐个替换 |
| **坐标错误常量化** | 🟢 低 | 已移除 Coordinate 常量类，改用工厂 |
| **枚举导入路径错误** | 🟢 低 | IDE 自动导入，编译验证 |
| **工厂方法过度设计** | 🟢 低 | 保持工厂简单，仅封装常用值 |
| **ID断言依赖遗漏** | 🟡 中 | 按 2.5 节扫描流程执行 |

---

## 7. 与原 PRD 的主要差异

| 方面 | 原 PRD | 修正后 | 原因 |
|------|--------|--------|------|
| **坐标处理** | 替换为 Coordinate 常量类 | 使用工厂方法或保留魔法数字 | 坐标是业务生成值 |
| **屏幕尺寸** | 替换为 ScreenSize 常量 | 移至 DeviceFactory | 设备尺寸是测试数据 |
| **滚动阈值** | 强制替换为 ScrollThreshold | 可选替换，保留魔法数字能力 | 需要支持任意测试位置 |
| **枚举值** | 作为硬编码问题处理 | 从源码导入 | 避免重复定义 |
| **影响范围** | 39个文件，~330行修改 | 31个文件，~145行修改 | 聚焦真正的配置常量 |
| **工时估算** | 24h | 17h | 范围缩小 |
| **风险等级** | 中偏低 | 低 | 减少不必要的变更 |

---

## 8. 后续优化

### 8.1 V6.17+ 计划

- 引入测试数据生成器（随机化测试）
- 建立测试配置热更新机制
- 集成测试参数化框架

### 8.2 长期目标

- 实现测试数据与生产数据同构
- 建立测试常量版本管理
- 支持多环境测试配置

---

## 9. 参考资料

### 9.1 相关文档

- [CLAUDE_CONVENTIONS.md](../../CLAUDE_CONVENTIONS.md) - 代码规范
- [CLAUDE_WORKFLOW.md](../../CLAUDE_WORKFLOW.md) - 工作流程
- [docs/testing/README.md](../../testing/README.md) - 测试指南

### 9.2 相关PRD

- [PRD_V6.2.0](./PRD_V6_2_test_architecture_standardization_prd.md) - 测试架构标准化
- [PRD_V6.15.0](./PRD_V6_15_0_State_Machine_Test_Migration.md) - 状态机测试迁移

---

**Change Log:**

| 日期 | 版本 | 变更 |
|------|------|------|
| 2026-06-10 | V1.0 | 初始版本 |
| 2026-06-10 | V1.1 | 新增硬编码分类标准和ID依赖扫描策略 |
| 2026-06-11 | V2.0 | **重大修正**: 基于业务原则重新分类<br>- 移除 Coordinate 常量类（坐标是业务生成值）<br>- 移除 ScreenSize 常量（移至工厂）<br>- ScrollThreshold 改为可选（保留魔法数字能力）<br>- 新增枚举导入规范<br>- 缩小影响范围（39→31个文件）<br>- 降低工时估算（24→17小时）<br>- 降低风险等级（中→低） |
