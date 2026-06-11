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
> ### 分类原则
> 1. **有意义的类型才需要归类** - 只有代表明确业务/技术语义的值才提取为常量
> 2. **业务生成值使用魔法数字** - 坐标、尺寸等依赖被测应用的值应保持原样
> 3. **枚举应来自源代码** - 状态、决策等枚举值应从源码导入，不应在测试中重复定义
>
> ### 设计原则
> 4. **常量形成设计规范** - 一旦归类为常量，即成为设计规范，修改需评估影响
> 5. **测试数据保留灵活性** - 测试输入数据、边界值应保持直接值，便于测试场景变化
>
> ### 协作原则
> 6. **源码修改需同步检查测试** - 修改源代码中的常量/枚举/字段时，必须扫描并同步修改相关测试中的硬编码引用
>
> ### 实施原则
> 7. **按职责边界和影响范围排序** - 实施顺序：独立模块 → 边界清晰 → 影响范围小 → 影响范围大；职责单一优先，职责复杂的后处理
> 8. **任务细化原则** - 每个任务应在 1-2 小时内完成，可独立验证，便于回滚和 Code Review

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

### 2.6 源码修改时的测试同步规范

> **原则 6 实现**: 修改源代码中的常量/枚举/字段时，必须扫描并同步修改相关测试中的硬编码引用

#### 2.6.1 问题场景

当修改源代码中的枚举值、常量或字段时，测试中往往存在硬编码的引用，容易导致测试失败：

```python
# 源码修改：重命名枚举值
# src/state_machine/traversal_fsm.py
class TraversalState(str, Enum):
    NODE_SELECT = "node_select"  # 原值
    NODE_SELECTION = "node_selection"  # 新值

# 但测试中的硬编码会被遗漏
# tests/state_machine/test_transition_to.py
assert state == "NODE_SELECT"  # ❌ 仍然是旧值，测试会失败
assert state == TraversalState.NODE_SELECT  # ❌ 属性名也变了，会报错
```

#### 2.6.2 扫描检查流程

**修改源码前的检查清单：**

| 检查项 | 说明 | 验证方法 |
|--------|------|----------|
| ✅ 确认变更类型 | 是枚举值/常量/字段/方法名？ | 查看变更内容 |
| ✅ 扫描测试引用 | 找出所有引用该值的测试 | grep 扫描 |
| ✅ 确认影响范围 | 列出需要修改的测试文件 | 记录文件清单 |
| ✅ 同步修改测试 | 确保所有测试都已更新 | 运行测试验证 |
| ✅ 无残留引用 | 确认没有遗漏的旧引用 | grep 扫描返回空 |

#### 2.6.3 自动化扫描脚本

```python
# scripts/scan_test_references.py
import subprocess
from pathlib import Path
from typing import List, Dict

def find_test_references(source_symbol: str, tests_dir: str = "tests") -> List[Dict[str, any]]:
    """
    扫描测试中对源码符号的所有引用
    
    Args:
        source_symbol: 源码符号名称，如 "NODE_SELECT" 或 "TraversalState"
        tests_dir: 测试目录路径
    
    Returns:
        引用位置列表
    """
    results = []
    
    # 扫描字符串引用
    cmd = f'grep -r "{source_symbol}" {tests_dir} --include="*.py" -n'
    output = subprocess.run(cmd, shell=True, capture_output=True, text=True)
    
    if output.returncode == 0:
        for line in output.stdout.strip().split('\n'):
            if line:
                parts = line.split(':')
                if len(parts) >= 2:
                    results.append({
                        "file": parts[0],
                        "line": int(parts[1]),
                        "content": ':'.join(parts[2:]),
                        "type": "string_literal" if '"' in line or "'" in line else "symbol_reference"
                    })
    
    return results

def check_enum_migration(old_name: str, new_name: str, tests_dir: str = "tests"):
    """
    检查枚举迁移是否完整
    
    Args:
        old_name: 旧名称，如 "NODE_SELECT"
        new_name: 新名称，如 "NODE_SELECTION"
        tests_dir: 测试目录路径
    """
    old_refs = find_test_references(old_name, tests_dir)
    new_refs = find_test_references(new_name, tests_dir)
    
    print(f"旧名称 '{old_name}' 引用数: {len(old_refs)}")
    print(f"新名称 '{new_name}' 引用数: {len(new_refs)}")
    
    if old_refs:
        print(f"\n⚠️  仍有 {len(old_refs)} 处旧引用：")
        for ref in old_refs:
            print(f"  {ref['file']}:{ref['line']} - {ref['content']}")
    
    if not old_refs and new_refs:
        print(f"\n✅ 迁移完成，所有引用已更新")
    
    return len(old_refs) == 0

# 使用示例
# check_enum_migration("NODE_SELECT", "NODE_SELECTION")
```

#### 2.6.4 常见变更场景

| 变更类型 | 示例 | 扫描命令 | 检查要点 |
|---------|------|----------|----------|
| **枚举值重命名** | `NODE_SELECT` → `NODE_SELECTION` | `grep -r "NODE_SELECT" tests/` | 检查字符串和属性引用 |
| **枚举类重命名** | `TraversalState` → `NodeState` | `grep -r "TraversalState" tests/` | 检查导入语句 |
| **常量值修改** | `Timeout.LONG = 10` → `15` | `grep -r "Timeout\.LONG" tests/` | 确认依赖此常量的测试 |
| **字段重命名** | `node_id` → `node_identifier` | `grep -r "\.node_id\|'node_id'" tests/` | 检查属性访问和字符串键 |
| **方法签名变更** | 参数名或类型变化 | `grep -r "method_name" tests/` | 检查调用点 |

#### 2.6.5 完整修改流程

```
1. 修改源码前
   └─> 运行扫描脚本，记录当前引用
   └─> 记录：影响文件数、引用位置

2. 修改源码
   └─> 执行源码变更

3. 同步修改测试
   └─> 逐个修改记录的引用位置
   └─> 确保所有引用都已更新

4. 验证
   └─> 再次运行扫描，确认旧引用数为 0
   └─> 运行相关测试，确保通过
   └─> 运行完整测试套件，确保无遗漏

5. 提交
   └─> 在 commit message 中说明测试同步修改
```

#### 2.6.6 检查清单模板

```markdown
## 源码修改测试同步检查清单

### 变更信息
- **变更类型**: [枚举重命名 / 常量修改 / 字段重命名]
- **旧值**: `OLD_VALUE`
- **新值**: `NEW_VALUE`
- **源码文件**: `src/path/to/file.py`

### 扫描结果
- **扫描命令**: `grep -r "OLD_VALUE" tests/`
- **影响文件数**: X
- **影响文件列表**:
  - [ ] `tests/file1.py` (3 处引用)
  - [ ] `tests/file2.py` (1 处引用)

### 修改确认
- [ ] 所有测试引用已更新
- [ ] 旧引用扫描返回空结果
- [ ] 相关测试通过
- [ ] 完整测试套件通过

### 验证命令
```bash
# 验证无残留
grep -r "OLD_VALUE" tests/

# 运行测试
pytest tests/affected/path/ -v
```
```

#### 2.6.7 风险与缓解

| 风险 | 缓解措施 |
|------|----------|
| **遗漏引用点** | 使用自动化扫描，不依赖人工记忆 |
| **测试延迟失败** | 修改后立即验证，不延后提交 |
| **跨目录引用** | 扫描整个 tests/ 目录，不限定子目录 |
| **间接引用** | 检查工厂方法、fixture 等间接使用位置 |

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

> **实施原则** (原则 7-8):
> - 按职责边界和影响范围排序：独立新增 → 边界清晰 → 小影响 → 大影响
> - 任务细化：每个任务 1-2 小时，可独立验证

### 4.1 任务列表（按影响范围从小到大）

#### 阶段 A：基础设施创建（无影响，新增文件）

| 任务 ID | 任务描述 | 职责边界 | 影响范围 | 估计工时 | 可验证 |
|---------|----------|----------|----------|----------|--------|
| A-01 | 创建 `tests/config/__init__.py`，添加模块导出 | 独立模块 | 无 | 0.5h | 文件存在 |
| A-02 | 实现 `tests/config/constants.py`：Timeout 类 | 独立模块 | 无 | 0.5h | 可导入 |
| A-03 | 实现 `tests/config/constants.py`：Retry 类 | 独立模块 | 无 | 0.5h | 可导入 |
| A-04 | 实现 `tests/config/constants.py`：Concurrency 类 | 独立模块 | 无 | 0.5h | 可导入 |
| A-05 | 实现 `tests/config/constants.py`：ScrollThreshold 类（可选） | 独立模块 | 无 | 0.5h | 可导入 |
| A-06 | 实现 `tests/config/test_ids.py`：TestIdGenerator 类 | 独立模块 | 无 | 1h | 可导入 |
| A-07 | 实现 `tests/factories/device_factory.py`：DeviceFactory 类 | 独立模块 | 无 | 1h | 可导入 |
| A-08 | 实现 `tests/factories/device_factory.py`：CoordinateFactory 类 | 独立模块 | 无 | 1h | 可导入 |

**小计**: 6h，8个任务，每个 0.5-1h

**验证方式**: `python -c "from tests.config.constants import Timeout; from tests.factories.device_factory import DeviceFactory"`

---

#### 阶段 B：枚举导入（边界清晰，单文件改动）

| 任务 ID | 任务描述 | 职责边界 | 影响范围 | 估计工时 | 可验证 |
|---------|----------|----------|----------|----------|--------|
| B-01 | `test_transition_to.py`：添加 TraversalState 导入，替换硬编码字符串 | 单文件内 | 1 文件 | 0.5h | 测试通过 |
| B-02 | `test_trace_analyzer.py`：添加相关枚举导入 | 单文件内 | 1 文件 | 0.5h | 测试通过 |
| B-03 | `test_trace_models.py`：添加相关枚举导入 | 单文件内 | 1 文件 | 0.5h | 测试通过 |
| B-04 | `test_trace_recovery.py`：添加相关枚举导入 | 单文件内 | 1 文件 | 0.5h | 测试通过 |
| B-05 | `test_trace_recording.py`：添加相关枚举导入 | 单文件内 | 1 文件 | 0.5h | 测试通过 |
| B-06 | `test_v6_9_dynamic_matching.py`：添加相关枚举导入 | 单文件内 | 1 文件 | 0.5h | 测试通过 |
| B-07 | 其他 FSM 测试：批量添加枚举导入 | 单文件内 | 多文件 | 1h | 测试通过 |

**小计**: 4.5h，7个任务，每个 0.5-1h

**验证方式**: 每个任务完成后运行 `pytest <文件> -v`

---

#### 阶段 C：简单常量替换（低影响，1-2处修改）

| 任务 ID | 任务描述 | 职责边界 | 影响范围 | 估计工时 | 可验证 |
|---------|----------|----------|----------|----------|--------|
| C-01 | `test_settings_full_traversal.py`：替换 max_children=10 | 单文件内 | 1 处 | 0.5h | 测试通过 |
| C-02 | `test_branch_handling.py`：替换 max_children=2 | 单文件内 | 1 处 | 0.5h | 测试通过 |
| C-03 | `test_target_search.py`：替换 max_retries=3 | 单文件内 | 1 处 | 0.5h | 测试通过 |
| C-04 | `test_state_machine_error_integration.py`：替换 max_retries | 单文件内 | 3 处 | 0.5h | 测试通过 |
| C-05 | `test_state_machine_intelligence.py`：替换 max_retries | 单文件内 | 5 处 | 0.5h | 测试通过 |

**小计**: 2.5h，5个任务，每个 0.5h

**验证方式**: 每个任务完成后运行 `pytest <文件> -v`

---

#### 阶段 D：批量常量替换（中影响，多处修改）

| 任务 ID | 任务描述 | 职责边界 | 影响范围 | 估计工时 | 可验证 |
|---------|----------|----------|----------|----------|--------|
| D-01 | `test_trace_integration.py`：替换 timeout=5.0（3处） | 单文件内 | 3 处 | 0.5h | 测试通过 |
| D-02 | `test_trace_storage.py`：替换 timeout=5.0（6处） | 单文件内 | 6 处 | 1h | 测试通过 |
| D-03 | `dashboard_performance_v2.py`：替换 timeout 和 CONCURRENT_REQUESTS（8处） | 单文件内 | 8 处 | 1h | 测试通过 |

**小计**: 2.5h，3个任务，每个 0.5-1h

**验证方式**: 每个任务完成后运行 `pytest <文件> -v`

---

#### 阶段 E：复杂常量替换（高影响，20处修改）

| 任务 ID | 任务描述 | 职责边界 | 影响范围 | 估计工时 | 可验证 |
|---------|----------|----------|----------|----------|--------|
| E-01 | `test_error_handler.py`：替换 max_retries（20处） | 单文件内 | 20 处 | 1.5h | 测试通过 |

**小计**: 1.5h，1个任务

**验证方式**: 完成后运行 `pytest tests/v6/test_error_handler.py -v`

---

#### 阶段 F：ID 迁移（需扫描引用点，中高影响）

| 任务 ID | 任务描述 | 职责边界 | 影响范围 | 估计工时 | 可验证 |
|---------|----------|----------|----------|----------|--------|
| F-01 | `test_transition_to.py`：扫描 node123 引用，替换为 TestIdGenerator | 跨断言 | 15 处 | 1.5h | 测试通过 |
| F-02 | `test_v6_9_dynamic_matching.py`：替换 child1/child2 ID | 单文件内 | 10 处 | 1h | 测试通过 |
| F-03 | `test_trace_analyzer.py`：替换 t1/sp1 ID | 单文件内 | 20 处 | 1.5h | 测试通过 |
| F-04 | `test_trace_models.py`：替换 trace/span ID | 单文件内 | 8 处 | 1h | 测试通过 |
| F-05 | `test_trace_recovery.py`：替换 trace/span ID | 单文件内 | 6 处 | 1h | 测试通过 |
| F-06 | `test_trace_recording.py`：替换 child1 ID | 单文件内 | 5 处 | 1h | 测试通过 |
| F-07 | `test_problem_detector.py`：替换 btn1 ID | 单文件内 | 2 处 | 0.5h | 测试通过 |
| F-08 | `test_behavior_validator.py`：替换 ID | 单文件内 | 1 处 | 0.5h | 测试通过 |

**小计**: 9h，8个任务，每个 0.5-1.5h

**注意**: 每个任务前必须运行 `grep` 扫描所有引用点，确保一次性替换

**验证方式**: 每个任务完成后：1) grep 确认无残留；2) `pytest <文件> -v`

---

#### 阶段 G：工厂方法引入（结构变更，高影响）

| 任务 ID | 任务描述 | 职责边界 | 影响范围 | 估计工时 | 可验证 |
|---------|----------|----------|----------|----------|--------|
| G-01 | `test_match_result.py`：使用 CoordinateFactory.center() | 单文件内 | 1 处 | 0.5h | 测试通过 |
| G-02 | `test_v6_6_trace_handler_metrics.py`：使用 CoordinateFactory | 单文件内 | 1 处 | 0.5h | 测试通过 |
| G-03 | `test_vision_service.py`：使用 CoordinateFactory.create() | 单文件内 | 3 处 | 1h | 测试通过 |
| G-04 | `test_stateful_mock_vision.py`：使用 CoordinateFactory | 单文件内 | 3 处 | 1h | 测试通过 |
| G-05 | `test_clicks.py`：使用 DeviceFactory.DEFAULT_PHONE | 单文件内 | 2 处 | 1h | 测试通过 |

**小计**: 4h，5个任务，每个 0.5-1h

**验证方式**: 每个任务完成后运行 `pytest <文件> -v`

---

#### 阶段 H：可选优化（滚动阈值，评估后决定）

| 任务 ID | 任务描述 | 职责边界 | 影响范围 | 估计工时 | 可验证 |
|---------|----------|----------|----------|----------|--------|
| H-01 | 评估 ScrollThreshold 使用场景 | 分析 | - | 1h | 评估报告 |
| H-02 | [可选] `test_scrollable_vision.py`：选择性替换 | 单文件内 | 待定 | 待定 | 测试通过 |
| H-03 | [可选] `test_models.py`：选择性替换 | 单文件内 | 待定 | 待定 | 测试通过 |
| H-04 | [可选] `test_scrollable_action.py`：选择性替换 | 单文件内 | 待定 | 待定 | 测试通过 |
| H-05 | [可选] `test_scenarios.py`：选择性替换 | 单文件内 | 待定 | 待定 | 测试通过 |

**小计**: 1h（评估）+ 待定（实施）

**注意**: 此阶段取决于 H-01 评估结果

---

### 4.2 任务汇总

| 阶段 | 任务数 | 总工时 | 影响等级 | 并行度 |
|------|--------|--------|----------|--------|
| A - 基础设施 | 8 | 6h | 无 | 高（可并行） |
| B - 枚举导入 | 7 | 4.5h | 低 | 高（可并行） |
| C - 简单替换 | 5 | 2.5h | 低 | 高（可并行） |
| D - 批量替换 | 3 | 2.5h | 中 | 中（文件独立） |
| E - 复杂替换 | 1 | 1.5h | 中 | 低（单文件） |
| F - ID迁移 | 8 | 9h | 中高 | 中（文件独立） |
| G - 工厂引入 | 5 | 4h | 高 | 中（文件独立） |
| H - 可选优化 | 5+ | 1h+ | 待定 | 低 |
| **总计** | **42+** | **31h+** | - | - |

**关键指标**:
- 平均任务工时：0.74h（符合 1-2h 原则）
- 最大任务工时：1.5h（F-01, F-03）
- 可高度并行：A 阶段（8 任务）、B 阶段（7 任务）

---

### 4.3 实施顺序建议

```
Week 1:
├── Day 1-2: 阶段 A（基础设施） - 6h
├── Day 3: 阶段 B（枚举导入） - 4.5h
├── Day 4: 阶段 C（简单替换） - 2.5h
└── Day 5: 验证 + 缓冲

Week 2:
├── Day 1: 阶段 D（批量替换） - 2.5h
├── Day 2: 阶段 E（复杂替换） - 1.5h
├── Day 3-5: 阶段 F（ID迁移） - 9h
└── 缓冲: 2h

Week 3 (可选):
├── Day 1-2: 阶段 G（工厂引入） - 4h
├── Day 3: 阶段 H 评估 - 1h
└── 缓冲: 2h
```

---

### 4.4 回滚策略

每个任务均可独立回滚：

| 任务类型 | 回滚方式 | 时间 |
|---------|---------|------|
| A（新增文件） | 删除文件 | <1 分钟 |
| B-G（修改文件） | `git checkout <file>` | <1 分钟 |
| 批量回滚 | `git reset --soft HEAD~N` | 5 分钟 |
| `tests/simulation/scroll/test_data_store.py` | threshold | 16 | 评估是否替换常见值 |

**说明**: 此阶段为 P2，先评估价值再决定是否实施。如果 0.0/0.5/1.0 使用频率高且语义清晰，可选择性替换。

---

#### 4.2.6 枚举导入（多个文件）

| 文件 | 修改 | 说明 |
|------|------|------|
| 多个 FSM 测试 | 添加枚举导入 | 从源码导入 `TraversalState`, `GlobalState`, `FallbackAction` |
| 多个 trace 测试 | 添加枚举导入 | 导入相关枚举类型 |

---

### 4.3 文件修改汇总表（更新）

| 阶段 | 新增文件 | 修改文件 | 任务数 | 总工时 | 风险 |
|------|---------|---------|--------|--------|------|
| A - 基础设施 | 4 | 0 | 8 | 6h | 🟢 无 |
| B - 枚举导入 | 0 | 7 | 7 | 4.5h | 🟢 低 |
| C - 简单替换 | 0 | 5 | 5 | 2.5h | 🟢 低 |
| D - 批量替换 | 0 | 3 | 3 | 2.5h | 🟡 中 |
| E - 复杂替换 | 0 | 1 | 1 | 1.5h | 🟡 中 |
| F - ID迁移 | 0 | 8 | 8 | 9h | 🟡 中 |
| G - 工厂引入 | 0 | 5 | 5 | 4h | 🟡 中高 |
| H - 可选优化 | 0 | 待定 | 5+ | 1h+ | 🟢 低 |
| **总计（A-G）** | **4** | **29** | **37** | **30h** | **🟡 中** |

**关键改进**:
- 任务粒度细化：37 个任务，平均 0.8h/任务（符合 1-2h 原则）
- 最大任务：1.5h（F-01, F-03），无超大任务
- 可高度并行：A、B、C 阶段任务可并行开发
- 每任务可独立回滚

---

### 4.4 影响范围详细文件清单

#### 新增文件（4个）
- `tests/config/__init__.py`
- `tests/config/constants.py`
- `tests/config/test_ids.py`
- `tests/factories/device_factory.py`

#### 修改文件（29个）

| 阶段 | 文件列表 |
|------|----------|
| B | `test_transition_to.py`, `test_trace_analyzer.py`, `test_trace_models.py`, `test_trace_recovery.py`, `test_trace_recording.py`, `test_v6_9_dynamic_matching.py`, 其他 FSM 测试 |
| C | `test_settings_full_traversal.py`, `test_branch_handling.py`, `test_target_search.py`, `test_state_machine_error_integration.py`, `test_state_machine_intelligence.py` |
| D | `test_trace_integration.py`, `test_trace_storage.py`, `dashboard_performance_v2.py` |
| E | `test_error_handler.py` |
| F | `test_transition_to.py`, `test_v6_9_dynamic_matching.py`, `test_trace_analyzer.py`, `test_trace_models.py`, `test_trace_recovery.py`, `test_trace_recording.py`, `test_problem_detector.py`, `test_behavior_validator.py` |
| G | `test_match_result.py`, `test_v6_6_trace_handler_metrics.py`, `test_vision_service.py`, `test_stateful_mock_vision.py`, `test_clicks.py` |

**注意**: `test_transition_to.py` 在阶段 B 和 F 都有修改（枚举导入 + ID迁移）

---

### 4.5 回滚策略（细化到任务级）

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
| 2026-06-11 | V2.1 | 新增原则 #6：源码修改需同步检查测试<br>- 新增 2.6 节：源码修改时的测试同步规范<br>- 提供自动化扫描脚本示例<br>- 提供检查清单模板 |
| 2026-06-11 | V2.2 | **实施原则强化**: 新增原则 7-8 并细化任务<br>- 原则 #7: 按职责边界和影响范围从小到大排序<br>- 原则 #8: 任务细化（1-2h/任务，可独立验证）<br>- 重写 4.1 节：按影响范围划分 A-H 阶段<br>- 任务细化：37 个明确任务，平均 0.8h/任务<br>- 更新工时估算：17h → 30h（更细化，更可追踪）<br>- 新增 4.4 节：详细文件清单<br>- 新增 4.5 节：细化到任务级的回滚策略 |
