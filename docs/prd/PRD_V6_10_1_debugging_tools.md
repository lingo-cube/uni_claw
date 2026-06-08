# V6.10.1 调试工具与 BRANCH 处理测试增强

**版本**: V6.10.1
**日期**: 2026-06-08
**依赖**: V6.9 plan-compilation-and-matching
**状态**: 设计阶段
**优先级**: P0
**预计工时**: 5h

---

## 1. 背景

### 1.1 问题回顾

在修复 `test_settings_simulation` 问题时，调试过程耗时 2-4 小时，主要困难包括：

| 类别 | 具体问题 | 影响 |
|------|----------|------|
| **架构复杂** | 4个组件交互（Engine、FSM、Matcher、Fixture） | 难以定位问题所在层 |
| **Trace 数据大** | 501步产生2000+条记录，关键信息分散 | 分析耗时长 |
| **工具不足** | 缺少堆栈显示、状态可视化 | 调试依赖脚本 |
| **假设验证慢** | 每次"假设→修改→测试"需5-10分钟 | 定位周期长 |

### 1.2 根本原因

核心问题在 `src/state_machine/traversal_fsm.py` 的 `_handle_branch` 方法：

```python
# 错误代码（Line 1786-1790）
elif current_node.children_strategy.type == ChildrenStrategyType.DYNAMIC_MATCH:
    has_unvisited_children = True  # ❌ 总是假设有未访问子节点
```

**后果**：Wi-Fi menu_container 处理完 switch 子节点后，BRANCH 状态仍返回 NODE_SELECT 而不是 FRAME_COMPLETE，形成无限循环。

### 1.3 改进目标

| 维度 | 当前状态 | 目标状态 |
|------|----------|----------|
| **调试效率** | 问题定位需 2-4 小时 | < 30 分钟 |
| **可观测性** | 关键决策无记录 | 所有关键点有 trace |
| **错误信息** | 缺少上下文 | 包含堆栈/历史/建议 |
| **测试覆盖** | ~60% | > 85% |

---

## 2. 目标

### 2.1 功能目标

1. **状态堆栈查看器**：实时显示状态机堆栈和当前状态
2. **决策点 Trace 增强**：关键决策点包含完整上下文信息
3. **BRANCH 处理单元测试**：覆盖 DYNAMIC_MATCH 边界条件
4. **完整遍历集成测试**：端到端验证深度优先遍历

### 2.2 质量目标

| 指标 | 目标值 |
|------|--------|
| BRANCH 处理测试覆盖率 | > 90% |
| 完整遍历测试覆盖率 | > 85% |
| 类型检查 | mypy strict 通过 |
| Linting | ruff 零警告 |

---

## 3. 详细设计

### 3.1 状态堆栈查看器

#### 3.1.1 文件位置

新建文件：`dashboards/state_stack_viewer.py`

#### 3.1.2 类设计

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
        """
        # 注意：假设 TraversalStateMachine 有 get_transition_history() 方法
        # 如果不存在，改用 engine.state_machine._transition_history
        history = engine.state_machine.get_transition_history()
        recent = history[-last_n:] if len(history) > last_n else history

        print(f"\nRecent Transitions (last {len(recent)}):")
        for trans in recent:
            print(
                f"  {trans.from_state} → {trans.to_state} | "
                f"node: {trans.node_id}"
            )
```

#### 3.1.3 使用方式

```python
# 在测试或调试时
from dashboards.state_stack_viewer import StateStackViewer

viewer = StateStackViewer()
viewer.show_stack(engine)
viewer.show_transitions(engine, last_n=5)
```

---

### 3.2 决策点 Trace 增强

#### 3.2.1 文件位置

修改文件：`src/traversal/graph_engine.py`

#### 3.2.2 新增方法

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

#### 3.2.3 使用示例

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

### 3.3 BRANCH 处理单元测试

#### 3.3.1 文件位置

新建文件：`tests/state_machine/test_branch_handling.py`

#### 3.3.2 测试场景设计

**注意**: DYNAMIC_MATCH 测试场景需要 mock `_get_next_unvisited_child` 方法。实际实现中需要通过 fixture 或 monkeypatch 来 mock 该方法的行为。

```python
"""
测试 BRANCH 状态对各种子节点策略的处理。

V6.10.1: 新增测试覆盖 DYNAMIC_MATCH 边界条件。
"""

import pytest
from src.state_machine.traversal_fsm import TraversalStateMachine, TraversalState
from src.graph.node import (
    TraversalNode,
    NodeType,
    ChildrenStrategy,
    ChildrenStrategyType
)
from src.trace.context import TraversalContext


class TestBranchHandling:
    """测试 BRANCH 状态处理。"""

    def test_branch_with_no_children_static(self) -> None:
        """
        静态节点无子节点时应返回 FRAME_COMPLETE。

        Given:
            - LEAF_ACTION 类型节点
            - children_strategy.type = STATIC
            - static_children = []
        When:
            - 调用 _handle_branch
        Then:
            - 返回 FRAME_COMPLETE 状态
        """
        node = TraversalNode(
            node_id="test_leaf",
            node_type=NodeType.LEAF_ACTION,
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.STATIC,
                static_children=[]
            )
        )
        context = TraversalContext()
        fsm = TraversalStateMachine()

        # Act
        next_state = fsm._handle_branch(context.node_stack, context)

        # Assert
        assert next_state == TraversalState.FRAME_COMPLETE

    def test_branch_with_all_children_visited_static(self) -> None:
        """
        静态节点所有子节点已访问时应返回 FRAME_COMPLETE。

        Given:
            - CONTAINER 类型节点
            - children_strategy.type = STATIC
            - static_children = ["child1", "child2"]
            - visited_children = {"child1", "child2"}
        When:
            - 调用 _handle_branch
        Then:
            - 返回 FRAME_COMPLETE 状态
        """
        node = TraversalNode(
            node_id="test_container",
            node_type=NodeType.CONTAINER,
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.STATIC,
                static_children=["child1", "child2"]
            )
        )
        context = TraversalContext()
        context.visited_children["test_container"] = {"child1", "child2"}

        fsm = TraversalStateMachine()

        # Act
        next_state = fsm._handle_branch(context.node_stack, context)

        # Assert
        assert next_state == TraversalState.FRAME_COMPLETE

    def test_branch_with_unvisited_child_static(self) -> None:
        """
        静态节点有未访问子节点时应返回 NODE_SELECT。

        Given:
            - CONTAINER 类型节点
            - children_strategy.type = STATIC
            - static_children = ["child1", "child2"]
            - visited_children = {"child1"} (child2 未访问)
        When:
            - 调用 _handle_branch
        Then:
            - 返回 NODE_SELECT 状态
        """
        node = TraversalNode(
            node_id="test_container",
            node_type=NodeType.CONTAINER,
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.STATIC,
                static_children=["child1", "child2"]
            )
        )
        context = TraversalContext()
        context.visited_children["test_container"] = {"child1"}

        fsm = TraversalStateMachine()

        # Act
        next_state = fsm._handle_branch(context.node_stack, context)

        # Assert
        assert next_state == TraversalState.NODE_SELECT

    def test_branch_with_all_children_visited_dynamic(
        self,
        mock_engine: pytest.MockFixture
    ) -> None:
        """
        DYNAMIC_MATCH 节点所有子节点已访问时应返回 FRAME_COMPLETE。

        这是 V6.10.1 修复的核心问题：
        DYNAMIC_MATCH 节点不应总是返回 True。

        Given:
            - CONTAINER 类型节点
            - children_strategy.type = DYNAMIC_MATCH
            - visited_children = {"child1", "child2"}
            - mock_engine._get_next_unvisited_child 返回 None
        When:
            - 调用 _handle_branch
        Then:
            - 返回 FRAME_COMPLETE 状态（而非无限循环）
        """
        node = TraversalNode(
            node_id="test_container",
            node_type=NodeType.CONTAINER,
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.DYNAMIC_MATCH,
                dynamic_rules={"menu_rule": {}}
            )
        )
        context = TraversalContext()
        context.visited_children["test_container"] = {"child1", "child2"}

        # Mock engine._get_next_unvisited_child 返回 None
        mock_engine._get_next_unvisited_child.return_value = None

        fsm = TraversalStateMachine()

        # Act
        next_state = fsm._handle_branch(context.node_stack, context)

        # Assert
        assert next_state == TraversalState.FRAME_COMPLETE

    def test_branch_with_unvisited_child_dynamic(
        self,
        mock_engine: pytest.MockFixture
    ) -> None:
        """
        DYNAMIC_MATCH 节点有未访问子节点时应返回 NODE_SELECT。

        Given:
            - CONTAINER 类型节点
            - children_strategy.type = DYNAMIC_MATCH
            - visited_children = {"child1"} (child2 未访问)
            - mock_engine._get_next_unvisited_child 返回 "child2"
        When:
            - 调用 _handle_branch
        Then:
            - 返回 NODE_SELECT 状态
        """
        node = TraversalNode(
            node_id="test_container",
            node_type=NodeType.CONTAINER,
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.DYNAMIC_MATCH,
                dynamic_rules={"menu_rule": {}}
            )
        )
        context = TraversalContext()
        context.visited_children["test_container"] = {"child1"}

        # Mock engine._get_next_unvisited_child 返回 "child2"
        mock_engine._get_next_unvisited_child.return_value = "child2"

        fsm = TraversalStateMachine()

        # Act
        next_state = fsm._handle_branch(context.node_stack, context)

        # Assert
        assert next_state == TraversalState.NODE_SELECT
```

---

### 3.4 完整遍历集成测试

#### 3.4.1 文件位置

新建文件：`tests/v6/settings/test_settings_full_traversal.py`

#### 3.4.2 测试设计

```python
"""
测试完整的设置页面深度优先遍历。

验证：
1. 所有主要菜单项被访问
2. 遍历顺序符合深度优先
3. 无无限循环
"""

import pytest
from tests.v6.settings.test_settings_simulation import (
    settings_traversal_plan,
    settings_fixture
)


@pytest.mark.integration
def test_settings_depth_first_traversal(
    settings_traversal_plan,
    settings_fixture
) -> None:
    """
    验证深度优先遍历访问所有主要页面。

    Given:
        - settings_traversal_plan: 完整的设置遍历计划
        - settings_fixture: 设置页面 fixture
    When:
        - 运行 GraphTraversalEngine.run()
    Then:
        - result.status == COMPLETED
        - total_steps < 500 (不达到步数上限)
        - 所有主要菜单项被访问
        - 遍历顺序符合深度优先
    """
    from src.traversal.graph_engine import GraphTraversalEngine
    from src.simulation.stateful_mock_vision import StatefulMockVisionService
    from src.simulation.stateful_mock_action import StatefulMockActionExecutor
    from src.trace.storage import FileStorage
    from src.trace.recorder import TraceRecorder

    # Arrange
    vision = StatefulMockVisionService(settings_fixture)
    action = StatefulMockActionExecutor(vision)
    storage = FileStorage(base_dir='.traces')
    recorder = TraceRecorder(storage=storage)

    engine = GraphTraversalEngine(
        plan=settings_traversal_plan,
        vision_service=vision,
        action_executor=action,
        trace_recorder=recorder,
    )

    # Act
    result = engine.run()

    # Assert - 基本结果
    assert result.status == "GlobalState.COMPLETED"
    assert result.total_steps < 500  # 不应达到步数上限

    # Assert - 所有主要菜单项被访问
    expected_pages = {
        "root",
        "Wi-Fi",
        "Bluetooth",
        "Display",
        "Storage",
        "Battery",
        "Apps"
    }
    visited_pages = extract_page_names(result.visited_nodes)
    assert visited_pages >= expected_pages

    # Assert - 遍历顺序符合深度优先
    # Wi-Fi 应在 Bluetooth 之前（Wi-Fi 子树完成后才访问 Bluetooth）
    visited_order = get_visit_order(result.trace_id)
    wifi_idx = visited_order.index("Wi-Fi")
    bluetooth_idx = visited_order.index("Bluetooth")
    assert wifi_idx < bluetooth_idx


def extract_page_names(visited_nodes: list) -> set[str]:
    """从访问节点列表中提取页面名称。"""
    return {node.name for node in visited_nodes}


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

## 4. 修改文件清单

| 文件 | 类型 | 内容 | 位置 |
|------|------|------|------|
| `dashboards/state_stack_viewer.py` | 新建 | StateStackViewer 类 | `dashboards/` |
| `dashboards/README.md` | 新建 | 模块文档，说明 StateStackViewer 用法及与现有工具的定位差异 | `dashboards/` |
| `src/traversal/graph_engine.py` | 修改 | 新增 `_record_decision()` 方法 | `src/traversal/` |
| `tests/state_machine/test_branch_handling.py` | 新建 | BRANCH 处理单元测试 | `tests/state_machine/` |
| `tests/v6/settings/test_settings_full_traversal.py` | 新建 | 完整遍历集成测试 | `tests/v6/settings/` |

---

## 5. 测试矩阵

### 5.1 状态堆栈查看器测试

| 场景 | 输入 | 预期输出 |
|------|------|----------|
| 空堆栈显示 | `stack=[]` | 显示 `depth=0`，无节点列表 |
| 单层堆栈显示 | `stack深度=1` | 显示 1 层节点及 visited_children |
| 多层堆栈显示 | `stack深度=3` | 显示 3 层节点，每层有缩进 |
| 无转换历史 | `history=[]` | 显示 `last 0` |
| 有转换历史 | `history长度=15` | 显示最近 10 条转换 |

### 5.2 决策点 Trace 记录测试

| 场景 | 决策类型 | 上下文字段 |
|------|----------|------------|
| FRAME_COMPLETE 决策 | `branch_complete_frame` | `reason`, `node`, `visited_count` |
| 跳过子节点生成 | `skip_child_generation` | `reason`, `node` |
| 恢复策略选择 | `select_recovery_strategy` | `strategy`, `context` |
| 无 trace_recorder | 任意 | 无操作（不崩溃） |

### 5.3 BRANCH 处理单元测试

| 场景 | 节点类型 | 子节点策略 | 已访问子节点 | 预期状态 |
|------|----------|------------|-------------|----------|
| 无子节点-静态 | LEAF_ACTION | STATIC, [] | N/A | FRAME_COMPLETE |
| 全部已访问-静态 | CONTAINER | STATIC, [c1,c2] | {c1,c2} | FRAME_COMPLETE |
| 有未访问-静态 | CONTAINER | STATIC, [c1,c2] | {c1} | NODE_SELECT |
| 全部已访问-动态 | CONTAINER | DYNAMIC_MATCH | {c1,c2} | FRAME_COMPLETE |
| 有未访问-动态 | CONTAINER | DYNAMIC_MATCH | {c1} | NODE_SELECT |
| 无子节点-动态 | CONTAINER | DYNAMIC_MATCH | {} | FRAME_COMPLETE |

### 5.4 完整遍历集成测试

| 场景 | 输入 | 预期 |
|------|------|------|
| 完整遍历 | settings_traversal_plan + settings_fixture | status=COMPLETED, steps<500 |
| 访问所有菜单 | 同上 | visited_pages 包含所有 7 个主要页面 |
| 深度优先顺序 | 同上 | Wi-Fi 索引 < Bluetooth 索引 |
| 无无限循环 | 同上 | 步数单调递增直到完成 |

---

## 6. 实施步骤

| Step | 内容 | 可验证 | 预计时间 |
|------|------|--------|----------|
| 1 | 创建 `dashboards/state_stack_viewer.py` | 运行查看器，能显示堆栈 | 2h |
| 2 | 在 `graph_engine.py` 中新增 `_record_decision()` 方法 | 检查 trace 输出包含 decision span | 1h |
| 3 | 编写 `test_branch_handling.py` 单元测试 | `pytest tests/state_machine/test_branch_handling.py -v` 通过 | 1h |
| 4 | 编写 `test_settings_full_traversal.py` 集成测试 | `pytest tests/v6/settings/test_settings_full_traversal.py -v` 通过 | 1h |

**总计**: 5 小时

---

## 7. 成功标准

### 7.1 功能验证

- ✅ `StateStackViewer.show_stack()` 能正确显示堆栈深度、节点名称、状态信息
- ✅ `StateStackViewer.show_transitions()` 能显示最近 N 条状态转换
- ✅ Trace 记录包含所有关键决策点的完整上下文（`stack_depth`、`current_state`、`visited_children`）
- ✅ BRANCH 处理单元测试覆盖 DYNAMIC_MATCH 边界条件（6 个场景全部通过）
- ✅ 完整遍历集成测试无无限循环，访问所有主要菜单项（7 个页面）

### 7.2 代码质量

- ✅ `StateStackViewer` 类通过 **mypy strict** 类型检查
- ✅ 所有新增方法有完整类型注解（参数 + 返回值）
- ✅ 禁用 `Any` 类型，使用具体类型
- ✅ 通过 **ruff** linting（零警告）
- ✅ 符合依赖注入原则（CLAUDE_CONVENTIONS.md §2.2）
- ✅ 符合强类型要求（CLAUDE_CONVENTIONS.md §1）

### 7.3 测试覆盖

- ✅ `test_branch_handling.py` 覆盖率 **> 90%**
- ✅ `test_settings_full_traversal.py` 覆盖率 **> 85%**
- ✅ 所有测试命名符合 `test_<method>_<scenario>_<expected>` 格式
- ✅ 测试文件放置在正确目录：
  - `tests/state_machine/test_branch_handling.py`
  - `tests/v6/settings/test_settings_full_traversal.py`
- ✅ 测试使用 Given-When-Then 格式的 docstring

### 7.4 文档一致性

- ✅ `dashboards/README.md` 已创建，说明 StateStackViewer 用法
- ✅ `StateStackViewer` 类有完整的 docstring
- ✅ `_record_decision()` 方法有 docstring
- ✅ 变更已更新 `CLAUDE_STATUS.md` 的版本信息

---

## 8. 修订记录

| 日期 | 版本 | 修订内容 |
|------|------|----------|
| 2026-06-08 | V6.10.1.0 | 初始版本：调试工具与 BRANCH 处理测试增强 |

---

## 9. 已知限制

| 限制 | 影响 | 后续版本 |
|------|------|----------|
| `StateStackViewer` 仅支持命令行输出 | 不支持图形界面 | V6.10+ 可扩展 GUI |
| `_record_decision()` 依赖 `trace_recorder` | 无 tracer 时无记录 | 正常行为，非限制 |
| DYNAMIC_MATCH 测试使用 mock | 可能遗漏真实场景问题 | 集成测试覆盖 |
| `get_visit_order()` 实现为简化版本 | 需要实际 trace 解析 | 测试辅助函数，可简化 |

---

## 10. 参考文档

- `docs/V6_OPTIMIZATION_IMPROVEMENTS.md` - 源改进方案
- `CLAUDE_CONVENTIONS.md` - 代码质量标准
- `docs/superpowers/specs/2026-06-08-v6-10-prd-series-design.md` - 系列设计文档
