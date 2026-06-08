# V6.10.3 代码质量改进与集成测试

**版本**: V6.10.3
**日期**: 2026-06-08
**依赖**: V6.10.2 state-machine-logic
**状态**: 设计阶段
**优先级**: P2
**预计工时**: 9h

---

## 1. 背景

### 1.1 问题回顾

V6.10.2 实施后，状态机逻辑已优化，但代码质量仍存在以下问题：

| 类别 | 具体问题 | 影响 |
|------|----------|------|
| **嵌套过深** | `graph_engine.py` 中 `_step_once` 方法的 BRANCH 处理嵌套深度 4-5 层 | 难以阅读和维护 |
| **缺少运行时检查** | 没有不变量检查，问题发展到后期才暴露 | 定位困难 |
| **集成测试不足** | 端到端场景覆盖不完整 | 回归风险高 |

### 1.2 根本原因

1. **复杂条件逻辑未提取**：BRANCH 处理逻辑嵌套在 `_step_once` 中
2. **缺少不变量断言**：运行时没有验证关键假设
3. **集成测试场景有限**：缺少复杂边界条件的测试

### 1.3 改进目标

| 维度 | 当前状态 | 目标状态 |
|------|----------|----------|
| **代码复杂度** | 嵌套深度 4-5 层 | < 3 层 |
| **问题发现时机** | 运行后期 | 早期（不变量检查） |
| **集成测试覆盖** | ~60% | > 85% |

---

## 2. 目标

### 2.1 功能目标

1. **添加不变量检查**：在关键步骤验证系统状态
2. **重构复杂条件逻辑**：提取 BRANCH 处理为独立方法
3. **扩展集成测试**：增加复杂场景测试

### 2.2 质量目标

| 指标 | 目标值 |
|------|--------|
| 不变量检查开销 | < 5% 运行时间 |
| 代码嵌套深度 | < 3 层 |
| 集成测试覆盖率 | > 85% |
| 类型检查 | mypy strict 通过 |

---

## 3. 详细设计

### 3.1 不变量检查 (_assert_invariants)

#### 3.1.1 文件位置

修改文件：`src/traversal/graph_engine.py`

#### 3.1.2 新增方法

```python
def _assert_invariants(self) -> None:
    """
    断言关键不变量，提前发现异常。

    V6.10.3: 添加运行时检查以快速发现问题。

    Raises:
        AssertionError: 如果任何不变量被违反
    """
    stack = self.context.node_stack

    # 不变量 1: 堆栈深度应合理
    assert stack.size() <= 100, (
        f"Stack too deep: {stack.size()}. "
        f"This may indicate an infinite loop."
    )

    # 不变量 2: 访问节点数不应超过合理范围
    assert len(self.context.visited_nodes) <= 10000, (
        f"Too many visited nodes: {len(self.context.visited_nodes)}. "
        f"This may indicate an infinite traversal."
    )

    # 不变量 3: 当前路径长度应与堆栈深度匹配
    assert len(self.context.current_path) == stack.size(), (
        f"Path length mismatch: path={len(self.context.current_path)}, "
        f"stack={stack.size()}"
    )

    # 不变量 4: 当前状态应在有效状态列表中
    valid_states = {s.value for s in TraversalState}
    assert self.state_machine._state.value in valid_states, (
        f"Invalid state: {self.state_machine._state.value}"
    )

    # 不变量 5: 堆栈中的每个节点应在 node_registry 中
    for node in stack._stack:
        assert node.node_id in self._node_registry, (
            f"Node {node.node_id} not in registry"
        )
```

#### 3.1.3 调用位置

在 `_step_once()` 方法末尾调用：

```python
def _step_once(self) -> dict[str, Any]:
    """执行单步遍历。"""
    try:
        # ... 原有逻辑 ...

        # V6.10.3: 每步后检查不变量
        self._assert_invariants()

        return transition_dict
    except AssertionError as e:
        # 捕获不变量违反，记录更详细的上下文
        self._record_error("invariant_violation", str(e))
        raise
```

---

### 3.2 重构复杂条件逻辑 (_handle_branch_for_children)

#### 3.2.1 文件位置

修改文件：`src/traversal/graph_engine.py`

#### 3.2.2 优化前（嵌套过深）

```python
# 原始代码（嵌套深度 4-5 层）
if transition.to_state == TraversalState.BRANCH:
    from_state_enum = transition.from_state
    if from_state_enum in (EXECUTE, RESULT_VERIFY, PRECONDITION_CHECK):
        current = stack.peek()
        if current and not current.is_leaf():
            child_id = self._get_next_unvisited_child(current)
            if child_id:
                self._push_node(child_id)
                child_pushed = child_id
            else:
                should_complete_frame = True
```

#### 3.2.3 优化后（提取方法）

**重要（基于审阅意见）**：为提高返回值可读性，使用 dataclass 替代 tuple。

```python
from dataclasses import dataclass

@dataclass
class BranchHandlingResult:
    """BRANCH 处理结果。"""
    child_pushed: Optional[str]
    should_complete_frame: bool

def _handle_branch_for_children(
    self,
    transition: StateTransition,
    stack: NodeStack
) -> BranchHandlingResult:
    """
    处理 BRANCH 状态的子节点逻辑。

    V6.10.3: 提取复杂条件逻辑，降低嵌套深度，使用 dataclass 提高可读性。

    Args:
        transition: 状态转换对象
        stack: 节点堆栈

    Returns:
        BranchHandlingResult: 包含 child_pushed 和 should_complete_frame
        - child_pushed: 推入的子节点 ID，如果没有则为 None
        - should_complete_frame: 是否应该完成当前帧
    """
    # 检查是否进入 BRANCH 状态
    if transition.to_state != TraversalState.BRANCH:
        return None, False

    # 检查来源状态
    if transition.from_state not in (EXECUTE, RESULT_VERIFY, PRECONDITION_CHECK):
        return None, False

    # 获取当前节点
    current = stack.peek()
    if not current or current.is_leaf():
        return None, False

    # 获取下一个未访问的子节点
    child_id = self._get_next_unvisited_child(current)
    if child_id:
        self._push_node(child_id)
        return child_id, False
    else:
        return None, True

# 使用
child_pushed, should_complete = self._handle_branch_for_children(transition, stack)
```

---

### 3.3 集成测试扩展

#### 3.3.1 文件位置

新建文件：`tests/v6/integration/test_complex_scenarios.py`

#### 3.3.2 测试场景

```python
"""
复杂场景集成测试。

V6.10.3: 新增复杂边界条件测试。
"""

import pytest
from tests.v6.settings.test_settings_simulation import (
    settings_traversal_plan,
    settings_fixture
)


class TestComplexScenarios:
    """复杂场景测试。"""

    @pytest.mark.integration
    def test_deep_nesting_traversal(self) -> None:
        """
        测试深度嵌套遍历。

        Given:
            - 深度嵌套的遍历计划（深度 > 5）
        When:
            - 运行遍历
        Then:
            - 成功完成，不违反堆栈深度不变量
        """
        # 实现深度嵌套测试
        pass

    @pytest.mark.integration
    def test_large_menu_traversal(self) -> None:
        """
        测试大量菜单项遍历。

        Given:
            - 包含 20+ 菜单项的页面
        When:
            - 运行遍历
        Then:
            - 成功完成，访问节点数在合理范围内
        """
        # 实现大菜单测试
        pass

    @pytest.mark.integration
    def test_dynamic_children_invalidation(self) -> None:
        """
        测试动态子节点缓存失效。

        Given:
            - DYNAMIC_MATCH 节点
            - 页面变化导致路径改变
        When:
            - 页面变化后继续遍历
        Then:
            - 缓存失效，重新生成子节点

        **实施说明（基于审阅意见）**：
        此测试实现复杂度较高，需要：
        1. 设计一个页面切换的 fixture（如从 Wi-Fi 菜单返回后进入 Bluetooth）
        2. 验证第一次访问时的 _dynamic_children 缓存
        3. 模拟页面变化（路径改变）
        4. 验证缓存被清空（invalidate_children_cache 被调用）
        5. 验证第二次访问时重新生成子节点

        建议标记为 V6.10.3.1 后续实现，或在实施时先实现简化版本。
        """
        # 实现缓存失效测试（建议后续实现）
        pass

    @pytest.mark.integration
    def test_invariant_violation_detection(self) -> None:
        """
        测试不变量违反检测。

        Given:
            - 人为制造不变量违反（如路径不匹配）
        When:
            - 运行遍历
        Then:
            - AssertionError 被抛出，包含详细错误信息
        """
        # 实现不变量违反测试
        pass
```

---

## 4. 修改文件清单

| 文件 | 类型 | 内容 | 位置 |
|------|------|------|------|
| `src/traversal/graph_engine.py` | 修改 | 新增 `_assert_invariants()` 方法 | `src/traversal/` |
| `src/traversal/graph_engine.py` | 修改 | 新增 `_handle_branch_for_children()` 方法 | `src/traversal/` |
| `src/traversal/graph_engine.py` | 修改 | 在 `_step_once()` 中调用不变量检查 | `src/traversal/` |
| `src/traversal/graph_engine.py` | 修改 | 重构 BRANCH 处理逻辑 | `src/traversal/` |
| `tests/v6/integration/test_complex_scenarios.py` | 新建 | 复杂场景集成测试 | `tests/v6/integration/` |

---

## 5. 测试矩阵

### 5.1 不变量检查测试

| 场景 | 条件 | 预期 |
|------|------|------|
| 堆栈深度正常 | stack.size() = 5 | 通过 |
| 堆栈过深 | stack.size() = 101 | AssertionError |
| 访问节点正常 | visited_nodes = 100 | 通过 |
| 访问节点过多 | visited_nodes = 10001 | AssertionError |
| 路径匹配 | path长度 = stack深度 | 通过 |
| 路径不匹配 | path长度 ≠ stack深度 | AssertionError |
| 状态有效 | state in valid_states | 通过 |
| 状态无效 | state not in valid_states | AssertionError |
| 节点在注册表 | 所有节点在 registry | 通过 |
| 节点不在注册表 | 某节点不在 registry | AssertionError |

### 5.2 重构方法测试

| 场景 | 输入 | 预期输出 |
|------|------|----------|
| 非 BRANCH 状态 | to_state != BRANCH | (None, False) |
| 无效来源状态 | from_state not in (EXECUTE, RESULT_VERIFY, PRECONDITION_CHECK) | (None, False) |
| 无当前节点 | stack.peek() = None | (None, False) |
| 叶子节点 | current.is_leaf() = True | (None, False) |
| 有未访问子节点 | _get_next_unvisited_child 返回 ID | (child_id, False) |
| 无未访问子节点 | _get_next_unvisited_child 返回 None | (None, True) |

### 5.3 集成测试场景

| 场景 | 描述 | 预期 |
|------|------|------|
| 深度嵌套 | 嵌套深度 > 5 | 成功完成，无不变量违反 |
| 大菜单 | 20+ 菜单项 | 成功完成，visited_nodes < 10000 |
| 缓存失效 | 路径变化后重新生成 | 缓存清空，子节点重新生成 |
| 不变量违反 | 人为制造违反 | AssertionError 包含详细信息 |

---

## 6. 实施步骤

| Step | 内容 | 可验证 | 预计时间 |
|------|------|--------|----------|
| 1 | 新增 `_assert_invariants()` 方法 | 触发各种违反，验证断言 | 2h |
| 2 | 在 `_step_once()` 中调用不变量检查 | 运行测试，验证正常场景不影响性能 | 1h |
| 3 | 新增 `_handle_branch_for_children()` 方法 | 单元测试通过 | 2h |
| 4 | 重构 BRANCH 处理逻辑调用新方法 | 集成测试通过 | 2h |
| 5 | 编写复杂场景集成测试 | 所有测试通过 | 2h |

**总计**: 9 小时

---

## 7. 成功标准

### 7.1 功能验证

- ✅ 不变量检查能检测到所有 5 类不变量违反
- ✅ 不变量违反时抛出包含详细信息的 AssertionError
- ✅ `_handle_branch_for_children()` 正确处理所有 6 种场景
- ✅ 重构后的代码嵌套深度 < 3 层
- ✅ 复杂场景集成测试全部通过

### 7.2 代码质量

- ✅ `_assert_invariants()` 方法通过 **mypy strict** 类型检查
- ✅ `_handle_branch_for_children()` 方法通过 **mypy strict** 类型检查
- ✅ 所有新增方法有完整类型注解
- ✅ 禁用 `Any` 类型（除 metadata 参数）
- ✅ 通过 **ruff** linting（零警告）
- ✅ 重构后代码圈复杂度 < 10

### 7.3 性能验证

- ✅ 不变量检查增加的运行时间 < 5%
- ✅ 正常场景下不变量检查不触发 AssertionError

### 7.4 测试覆盖

- ✅ `test_complex_scenarios.py` 覆盖率 **> 85%**
- ✅ 不变量检查测试覆盖所有 5 类不变量
- ✅ 重构方法测试覆盖所有 6 种场景
- ✅ 所有测试命名符合 `test_<method>_<scenario>_<expected>` 格式

### 7.5 文档一致性

- ✅ `_assert_invariants()` 方法有完整的 docstring
- ✅ `_handle_branch_for_children()` 方法有完整的 docstring
- ✅ 变更已更新 `CLAUDE_STATUS.md` 的版本信息

---

## 8. 修订记录

| 日期 | 版本 | 修订内容 |
|------|------|----------|
| 2026-06-08 | V6.10.3.0 | 初始版本：代码质量改进与集成测试 |

---

## 9. 已知限制

| 限制 | 影响 | 后续版本 |
|------|------|----------|
| 不变量检查增加运行时间 | 每步额外检查 | 已优化，< 5% 开销 |
| 堆栈深度限制为 100 | 极端深度场景可能误报 | 100 层已足够 |
| 访问节点限制为 10000 | 极大遍历可能误报 | 10000 节点已足够 |

---

## 10. 参考文档

- `docs/V6_OPTIMIZATION_IMPROVEMENTS.md` - 源改进方案
- `CLAUDE_CONVENTIONS.md` - 代码质量标准
- `docs/superpowers/specs/2026-06-08-v6-10-prd-series-design.md` - 系列设计文档
- `docs/prd/PRD_V6_10_2_state_machine_logic.md` - 前置 PRD
