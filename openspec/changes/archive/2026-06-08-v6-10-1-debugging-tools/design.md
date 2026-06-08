# V6.10.1 调试工具与 BRANCH 处理测试增强 - 设计文档

> **变更**: v6-10-1-debugging-tools
> **日期**: 2026-06-08

---

## 1. 架构设计

### 1.1 组件关系

```
┌─────────────────────────────────────────────────────────┐
│                     GraphTraversalEngine                 │
│  ┌─────────────────────────────────────────────────────┐  │
│  │              StateStackViewer (新建)               │  │
│  │  - show_stack(engine)                              │  │
│  │  - show_transitions(engine, last_n)               │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                            │
│  ┌─────────────────────────────────────────────────────┐  │
│  │           _record_decision() (新增)                  │  │
│  └─────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────┐
│                  Trace Recorder (现有)                    │
│  - 记录 decision spans                                │
│  - 记录 state_transition spans                         │
└─────────────────────────────────────────────────────────┘
```

### 1.2 数据流

```
调试流程：
1. 测试失败/问题发现
2. 使用 StateStackViewer 查看当前状态
3. 检查 Trace 中的 decision spans
4. 定位问题 → 修复 → 验证
```

---

## 2. 组件详细设计

### 2.1 StateStackViewer

#### 文件位置

新建文件：`dashboards/state_stack_viewer.py`

#### 类设计

```python
"""
状态堆栈可视化工具。

提供实时查看状态机堆栈和最近状态转换的功能。
"""

from typing import Optional, Any
from src.traversal.graph_engine import GraphTraversalEngine


class StateStackViewer:
    """状态堆栈可视化工具。"""

    def show_stack(self, engine: GraphTraversalEngine) -> None:
        """
        显示当前堆栈状态。

        Args:
            engine: 图遍历引擎实例（包含 TraversalStateMachine 实例作为 engine.state_machine）

        显示内容：
            - 堆栈深度
            - 当前状态
            - 当前路径
            - 每层堆栈节点及其已访问子节点

        注意：
            - engine.state_machine 是 TraversalStateMachine 实例
            - 需要通过 engine.state_machine._state 访问当前状态
            - 需要通过 engine.context 访问遍历上下文
        """
        stack = engine.context.node_stack
        print(f"\n{'='*60}")
        print(f"State Stack (depth: {stack.size()})")
        print(f"Current State: {engine.state_machine._state}")
        print(f"Current Path: {engine.context.current_path}")
        print(f"{'='*60}")

        for i, node in enumerate(reversed(list(stack._stack))):
            indent = "  " * i
            marker = "→ " if i == 0 else "  "
            print(f"{indent}{marker}{node.node_id} ({node.name})")

            # 显示该节点的已访问子节点
            visited = engine.context.visited_children.get(node.node_id, set())
            if visited:
                print(f"{indent}   Visited: {sorted(visited)}")

    def show_transitions(
        self,
        engine: GraphTraversalEngine,
        last_n: int = 10
    ) -> None:
        """
        显示最近的状态转换。

        Args:
            engine: 图遍历引擎实例
            last_n: 显示最近 N 条转换，默认 10

        注意：
            - TraversalStateMachine.get_transition_history() 方法已存在（第1812行）
            - 可以直接使用，无需修改
        """
        history = engine.state_machine.get_transition_history()
        recent = history[-last_n:] if len(history) > last_n else history

        print(f"\nRecent Transitions (last {len(recent)}):")
        for trans in recent:
            print(
                f"  {trans.from_state} → {trans.to_state} | "
                f"node: {trans.node_id}"
            )
```

#### 使用方式

```python
# 在测试或调试时
from dashboards.state_stack_viewer import StateStackViewer

viewer = StateStackViewer()
viewer.show_stack(engine)
viewer.show_transitions(engine, last_n=5)
```

---

### 2.2 决策点 Trace 增强

#### 文件位置

修改文件：`src/traversal/graph_engine.py`

#### 新增方法

```python
def _record_decision(
    self,
    decision: str,
    context: dict[str, Any]
) -> None:
    """
    记录关键决策点和上下文。

    V6.10.1: 增强调试信息，包含完整决策上下文。

    Args:
        decision: 决策类型标识（如 "branch_complete_frame"）
        context: 决策上下文信息
    """
    if not self.trace_recorder or not self.trace_recorder.trace_id:
        return

    span = SpanNode(
        span_type="decision",
        action=decision,
        metadata={
            "stack_depth": self.context.node_stack.size(),
            "current_state": self.state_machine._state.value,
            "current_path": list(self.context.current_path),
            "visited_children": list(
                self.context.visited_children.get(
                    context.get("node_id", ""),
                    []
                )
            ),
            **context
        }
    )
    self.trace_recorder.record_span(span)
```

#### 使用示例

```python
# 在 BRANCH 状态处理时
if should_complete_frame:
    self._record_decision("branch_complete_frame", {
        "reason": "no_more_children",
        "node": current.node_id,
        "visited_count": len(self.context.visited_children.get(current.node_id, []))
    })
```

---

### 2.3 BRANCH 处理单元测试

#### 文件位置

新建文件：`tests/state_machine/test_branch_handling.py`

#### 测试场景

**场景覆盖**：

| 场景 | 节点类型 | 子节点策略 | 已访问子节点 | 预期状态 |
|------|----------|------------|-------------|----------|
| 无子节点-静态 | LEAF_ACTION | STATIC, [] | N/A | FRAME_COMPLETE |
| 全部已访问-静态 | CONTAINER | STATIC, [c1,c2] | {c1,c2} | FRAME_COMPLETE |
| 有未访问-静态 | CONTAINER | STATIC, [c1,c2] | {c1} | NODE_SELECT |
| **全部已访问-动态** | CONTAINER | DYNAMIC_MATCH | {c1,c2} | **FRAME_COMPLETE** |
| **有未访问-动态** | CONTAINER | DYNAMIC_MATCH | {c1} | **NODE_SELECT** |

**核心测试**：DYNAMIC_MATCH 节点不应总是返回 True

---

### 2.4 完整遍历集成测试

#### 文件位置

新建文件：`tests/v6/settings/test_settings_full_traversal.py`

#### 测试设计

**验证目标**：
1. 所有主要菜单项被访问（7个页面）
2. 遍历顺序符合深度优先（Wi-Fi < Bluetooth 索引）
3. 无无限循环（steps < 500）

**关键实现**：`get_visit_order()` 需要从 trace 文件实际解析

```python
def get_visit_order(trace_id: str) -> list[str]:
    """
    从 trace 中获取页面访问顺序。

    Args:
        trace_id: Trace ID，用于定位 trace 文件

    Returns:
        按访问顺序排列的页面名称列表

    实现说明：
        需要从 TraceRecorder 的 storage 中读取 trace 文件，
        解析其中的 state_transition 事件，按时间顺序提取 node_id 或 node.name。
        这是一个真实实现，不是简化版本。
    """
    from src.trace.storage import FileStorage
    import json

    # 读取 trace 文件
    storage = FileStorage(base_dir='.traces')
    trace_data = storage.load_trace(trace_id)

    # 解析 state_transition 事件
    visited_order = []
    for line in trace_data.split('\n'):
        if not line.strip():
            continue
        event = json.loads(line)
        if event.get('span_type') == 'state_transition':
            node_id = event.get('metadata', {}).get('node_id')
            if node_id and node_id not in visited_order:
                visited_order.append(node_id)

    return visited_order
```

---

## 3. 接口定义

### 3.1 StateStackViewer 接口

```python
class StateStackViewer:
    def show_stack(self, engine: GraphTraversalEngine) -> None:
        """显示当前堆栈状态。"""

    def show_transitions(
        self,
        engine: GraphTraversalEngine,
        last_n: int = 10
    ) -> None:
        """显示最近的状态转换。"""
```

### 3.2 GraphTraversalEngine 新增接口

```python
class GraphTraversalEngine:
    def _record_decision(
        self,
        decision: str,
        context: dict[str, Any]
    ) -> None:
        """记录关键决策点和上下文。"""
```

---

## 4. 修改文件清单

| 文件 | 类型 | 内容 | 位置 |
|------|------|------|------|
| `dashboards/state_stack_viewer.py` | 新建 | StateStackViewer 类 | `dashboards/` |
| `dashboards/README.md` | 新建 | 模块文档，说明 StateStackViewer 用法及与现有工具的定位差异 | `dashboards/` |
| `src/traversal/graph_engine.py` | 修改 | 新增 `_record_decision()` 方法 | `src/traversal/` |
| `tests/state_machine/test_branch_handling.py` | 新建 | BRANCH 处理单元测试 | `tests/state_machine/` |
| `tests/v6/settings/test_settings_full_traversal.py` | 新建 | 完整遍历集成测试 | `tests/v6/settings/` |

---

## 5. 测试策略

### 5.1 单元测试

- **test_branch_handling.py**: 6个场景，覆盖率 > 90%
- 使用 mock 来模拟 engine._get_next_unvisited_child 行为

### 5.2 集成测试

- **test_settings_full_traversal.py**: 端到端验证
- 使用真实的 StatefulMockVisionService 和 StatefulMockActionExecutor
- 从 trace 文件解析访问顺序

### 5.3 手动验证

- 在调试时使用 StateStackViewer 查看状态
- 检查 trace 输出中的 decision spans

---

## 6. 风险与缓解

### 风险1: StateStackViewer 适配问题

**风险**: GraphTraversalEngine 的封装结构可能不同于预期

**缓解**:
- 已验证 TraversalStateMachine.get_transition_history() 存在（第1812行）
- 在设计文档中明确说明访问路径
- 实施时先验证再使用

### 风险2: get_visit_order() 实现复杂

**风险**: 从 trace 解析访问顺序的逻辑复杂

**缓解**:
- 已提供真实实现代码示例
- 使用现有的 TraceRecorder 和 FileStorage 接口
- 实施时可以先简化，后续完善

### 风险3: Mock fixture 设计

**风险**: DYNAMIC_MATCH 测试的 mock 可能不准确

**缓解**:
- 使用 pytest.mark 进行标记
- 提供清晰的 fixture 说明
- 实施时根据实际情况调整

---

## 7. 实施步骤

| Step | 内容 | 可验证 | 预计时间 |
|------|------|--------|----------|
| 1 | 创建 `dashboards/state_stack_viewer.py` | 运行查看器，能显示堆栈 | 2h |
| 2 | 在 `graph_engine.py` 中新增 `_record_decision()` 方法 | 检查 trace 输出包含 decision span | 1h |
| 3 | 编写 `test_branch_handling.py` 单元测试 | `pytest tests/state_machine/test_branch_handling.py -v` 通过 | 1h |
| 4 | 编写 `test_settings_full_traversal.py` 集成测试 | `pytest tests/v6/settings/test_settings_full_traversal.py -v` 通过 | 1h |

**总计**: 5 小时

---

## 8. 验收标准

### 功能验收

- [ ] StateStackViewer 能正确显示堆栈和状态
- [ ] Trace 包含所有关键决策点的上下文
- [ ] BRANCH 单元测试 6 个场景全部通过
- [ ] 集成测试无无限循环，访问 7 个主要页面

### 质量验收

- [ ] mypy strict 通过
- [ ] ruff 零警告
- [ ] 测试覆盖率达标（> 90%, > 85%）
- [ ] 文档完整（README.md + docstring）

---

## 9. 后续工作

本变更完成后，为后续变更奠定基础：

- **V6.10.2**: 使用本变更的调试工具验证状态机逻辑修改
- **V6.10.3**: 基于本变更的测试进行代码质量改进
- **V6.10.4**: 基于本变更的经验编写调试文档
