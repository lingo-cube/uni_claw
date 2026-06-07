# V6 遍历系统优化改进方案

> 基于 `test_settings_simulation` 问题修复过程的经验总结
>
> **创建日期**：2026-06-08
> **目标**：提升调试效率、增强可观测性、提高代码质量

---

## 一、问题回顾

### 1.1 现象

`test_settings_simulation` 测试异常：
- 只访问了 3 个节点（42.9% 覆盖率）
- 达到 500 步上限但未完成
- 预期：深度优先遍历全部 6 个设置菜单项

### 1.2 根本原因

`src/state_machine/traversal_fsm.py` 中的 `_handle_branch` 方法对 DYNAMIC_MATCH 节点处理错误：

```python
# 错误代码（Line 1786-1790）
elif current_node.children_strategy.type == ChildrenStrategyType.DYNAMIC_MATCH:
    has_unvisited_children = True  # ❌ 总是假设有未访问子节点
```

**后果**：Wi-Fi menu_container 处理完 switch 子节点后，BRANCH 状态仍返回 NODE_SELECT 而不是 FRAME_COMPLETE，形成无限循环。

### 1.3 修复困难的原因

| 类别 | 具体问题 | 影响 |
|------|----------|------|
| **架构复杂** | 4个组件交互（Engine、FSM、Matcher、Fixture） | 难以定位问题所在层 |
| **Trace 数据大** | 501步产生2000+条记录，关键信息分散 | 分析耗时长 |
| **多Bug掩盖** | 5个表面bug需先修复 | 偏离根因 |
| **工具不足** | 缺少堆栈显示、状态可视化 | 调试依赖脚本 |
| **假设验证慢** | 每次"假设→修改→测试"需5-10分钟 | 定位周期长 |

---

## 二、优化目标

| 维度 | 当前状态 | 目标状态 |
|------|----------|----------|
| **调试效率** | 问题定位需 2-4 小时 | < 30 分钟 |
| **可观测性** | 关键决策无记录 | 所有关键点有 trace |
| **错误信息** | 缺少上下文 | 包含堆栈/历史/建议 |
| **测试覆盖** | ~60% | > 85% |
| **文档** | 分散在各处 | 集中的调试指南 |

---

## 三、改进方案

### 3.1 调试工具增强（P0 优先级）

#### 3.1.1 实时状态堆栈查看器

**新建文件**：`dashboards/state_stack_viewer.py`

```python
"""实时显示状态机堆栈和当前状态"""

class StateStackViewer:
    """状态堆栈可视化工具"""

    def show_stack(self, engine: GraphTraversalEngine) -> None:
        """显示当前堆栈状态"""
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

    def show_transitions(self, engine: GraphTraversalEngine, last_n: int = 10) -> None:
        """显示最近的状态转换"""
        history = engine.state_machine.get_transition_history()
        recent = history[-last_n:] if len(history) > last_n else history

        print(f"\nRecent Transitions (last {len(recent)}):")
        for trans in recent:
            print(f"  {trans.from_state} → {trans.to_state} | node: {trans.node_id}")
```

**使用方式**：
```python
# 在测试或调试时
viewer = StateStackViewer()
viewer.show_stack(engine)
viewer.show_transitions(engine, last_n=5)
```

#### 3.1.2 决策点 Trace 增强

**修改文件**：`src/traversal/graph_engine.py`

**新增方法**：
```python
def _record_decision(self, decision: str, context: Dict[str, Any]) -> None:
    """
    记录关键决策点和上下文。
    
    V6.9.5: 增强调试信息，包含完整决策上下文
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
                    context.get("node_id", ""), []
                )
            ),
            **context
        }
    )
    self.trace_recorder.record_span(span)
```

**使用示例**：
```python
# 在 BRANCH 状态处理时
if should_complete_frame:
    self._record_decision("branch_complete_frame", {
        "reason": "no_more_children",
        "node": current.node_id,
        "visited_count": len(self.context.visited_children.get(current.node_id, []))
    })
```

### 3.2 状态机逻辑优化（P1-P2）

#### 3.2.1 提取未访问子节点检查

**修改文件**：`src/state_machine/traversal_fsm.py`

**新增方法**：
```python
def has_unvisited_children(
    self, 
    node: TraversalNode, 
    context: TraversalContext,
    engine: Optional[GraphTraversalEngine] = None
) -> Optional[bool]:
    """
    检查节点是否有未访问的子节点。
    
    Args:
        node: 要检查的节点
        context: 遍历上下文
        engine: 图遍历引擎（用于 DYNAMIC_MATCH 检查）
    
    Returns:
        - True: 有未访问子节点
        - False: 无未访问子节点
        - None: 无法确定（需要 engine 进一步检查）
    
    V6.9.5: 修复 DYNAMIC_MATCH 节点总是返回 True 的问题
    """
    if not node.children_strategy:
        return False

    if node.children_strategy.type == ChildrenStrategyType.NONE:
        return False

    # 获取已访问的子节点
    visited = context.visited_children.get(node.node_id, set())

    if node.children_strategy.type == ChildrenStrategyType.STATIC:
        # 静态子节点：直接检查
        for child_id in node.children_strategy.static_children:
            if child_id not in visited:
                return True
        return False

    elif node.children_strategy.type == ChildrenStrategyType.DYNAMIC_MATCH:
        # 动态子节点：需要 engine 检查
        if engine and hasattr(engine, '_get_next_unvisited_child'):
            child_id = engine._get_next_unvisited_child(node)
            return child_id is not None
        else:
            # 没有 engine 时返回 None 表示不确定
            return None

    return False
```

#### 3.2.2 状态转换断言增强

**修改文件**：`src/state_machine/traversal_fsm.py`

**优化 transition_to 方法**：
```python
def transition_to(
    self, 
    target_state: TraversalState, 
    node_id: Optional[str] = None, 
    **metadata
) -> bool:
    """转换到目标状态（带增强的错误信息）"""
    if not self.can_transition_to(target_state):
        # ✅ V6.9.5: 增强错误信息，包含调试上下文
        raise ValueError(
            f"Invalid state transition: {self._state} → {target_state}\n"
            f"  Current node: {self._current_node_id}\n"
            f"  Target node: {node_id}\n"
            f"  Recent transitions:\n" +
            "\n".join(
                f"    {t.from_state} → {t.to_state} (node: {t.node_id})"
                for t in self._transition_history[-5:]
            ) +
            f"\n  Valid transitions from {self._state}: " +
            f"{self.VALID_TRANSITIONS.get(self._state, set())}"
        )
    
    # 记录转换
    # ... 原有逻辑 ...
```

### 3.3 测试覆盖增强（P0-P2）

#### 3.3.1 BRANCH 处理单元测试

**新建文件**：`tests/state_machine/test_branch_handling.py`

```python
"""
测试 BRANCH 状态对各种子节点策略的处理。

V6.9.5: 新增测试覆盖 DYNAMIC_MATCH 边界条件
"""

import pytest
from src.state_machine.traversal_fsm import TraversalStateMachine, TraversalState
from src.graph.node import TraversalNode, NodeType, ChildrenStrategy, ChildrenStrategyType
from src.trace.context import TraversalContext

class TestBranchHandling:
    """测试 BRANCH 状态处理"""

    def test_branch_with_no_children_static(self):
        """静态节点无子节点时应返回 FRAME_COMPLETE"""
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
        
        next_state = fsm._handle_branch(context.node_stack, context)
        assert next_state == TraversalState.FRAME_COMPLETE

    def test_branch_with_all_children_visited_static(self):
        """静态节点所有子节点已访问时应返回 FRAME_COMPLETE"""
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
        next_state = fsm._handle_branch(context.node_stack, context)
        assert next_state == TraversalState.FRAME_COMPLETE

    def test_branch_with_unvisited_child_static(self):
        """静态节点有未访问子节点时应返回 NODE_SELECT"""
        node = TraversalNode(
            node_id="test_container",
            node_type=NodeType.CONTAINER,
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.STATIC,
                static_children=["child1", "child2"]
            )
        )
        context = TraversalContext()
        context.visited_children["test_container"] = {"child1"}  # child2 未访问
        
        fsm = TraversalStateMachine()
        next_state = fsm._handle_branch(context.node_stack, context)
        assert next_state == TraversalState.NODE_SELECT

    def test_branch_with_all_children_visited_dynamic(self, mock_engine):
        """DYNAMIC_MATCH 节点所有子节点已访问时应返回 FRAME_COMPLETE
        
        这是 V6.9.5 修复的核心问题：DYNAMIC_MATCH 节点不应总是返回 True
        """
        node = TraversalNode(
            node_id="test_container",
            node_type=NodeType.CONTAINER,
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.DYNAMIC_MATCH,
                dynamic_rules={"menu_rule": {...}}
            )
        )
        context = TraversalContext()
        context.visited_children["test_container"] = {"child1", "child2"}
        
        # Mock engine._get_next_unvisited_child 返回 None
        mock_engine._get_next_unvisited_child.return_value = None
        
        fsm = TraversalStateMachine()
        next_state = fsm._handle_branch(context.node_stack, context)
        assert next_state == TraversalState.FRAME_COMPLETE
```

#### 3.3.2 完整遍历集成测试

**新建文件**：`tests/v6/settings/test_settings_full_traversal.py`

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
def test_settings_depth_first_traversal(settings_traversal_plan, settings_fixture):
    """验证深度优先遍历访问所有主要页面"""
    from src.traversal.graph_engine import GraphTraversalEngine
    from src.simulation.stateful_mock_vision import StatefulMockVisionService
    from src.simulation.stateful_mock_action import StatefulMockActionExecutor
    from src.trace.storage import FileStorage
    from src.trace.recorder import TraceRecorder

    # 创建服务和引擎
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

    # 运行遍历
    result = engine.run()

    # 验证结果
    assert result.status == "GlobalState.COMPLETED"
    assert result.total_steps < 500  # 不应达到步数上限

    # 验证所有主要菜单项被访问
    expected_pages = {"root", "Wi-Fi", "Bluetooth", "Display", "Storage", "Battery", "Apps"}
    visited_pages = extract_page_names(result.visited_nodes)
    assert visited_pages >= expected_pages

    # 验证遍历顺序（Wi-Fi 应在 Bluetooth 之前）
    visited_order = get_visit_order(result.trace_id)
    wifi_idx = visited_order.index("Wi-Fi")
    bluetooth_idx = visited_order.index("Bluetooth")
    assert wifi_idx < bluetooth_idx  # 深度优先：Wi-Fi 子树完成后才访问 Bluetooth
```

### 3.4 文档和指南（P3）

#### 3.4.1 状态机调试指南

**新建文件**：`docs/debugging/STATE_MACHINE_DEBUGGING.md`

```markdown
# 状态机调试指南

## 快速诊断

### 问题：无限循环

**症状**：
- 步数达到上限（500/1000）
- 状态在 NODE_SELECT/BRANCH 间循环
- Trace 显示大量重复的状态转换

**诊断步骤**：

1. **检查 BRANCH 状态处理**
   ```bash
   # 查看最近的 BRANCH 状态转换
   cat trace.jsonl | jq 'select(.to_state == "branch")' | tail -20
   ```

2. **检查 visited_children 记录**
   ```bash
   # 查看哪些子节点被标记为已访问
   cat trace.jsonl | jq 'select(.visited_children)' | tail -10
   ```

3. **使用状态堆栈查看器**
   ```python
   from dashboards.state_stack_viewer import StateStackViewer
   viewer = StateStackViewer()
   viewer.show_stack(engine)  # 在循环中调用查看当前状态
   ```

**常见原因**：
- `_handle_branch` 对 DYNAMIC_MATCH 节点总是返回 `has_unvisited_children = True`
- `visited_children` 未正确更新
- `FRAME_COMPLETE` 转换条件错误

**相关文件**：
- `src/state_machine/traversal_fsm.py:_handle_branch`
- `src/traversal/graph_engine.py:_get_next_unvisited_child`

### 问题：子节点未入栈

**症状**：
- 父节点完成后直接返回，未处理下一个子节点
- Trace 中 `child_pushed` 为空

**诊断步骤**：

1. **检查 should_complete_frame 标志**
   ```bash
   cat trace.jsonl | jq 'select(.should_complete_frame == true)'
   ```

2. **检查 FRAME_COMPLETE → NODE_SELECT 转换**
   ```bash
   cat trace.jsonl | jq 'select(.from_state == "frame_complete" and .to_state == "node_select")'
   ```

**常见原因**：
- `_get_next_unvisited_child` 错误返回 None
- 状态转换到 FRAME_COMPLETE 过早
- `_push_node` 未被调用

**相关文件**：
- `src/traversal/graph_engine.py:1037-1090`

## Trace 分析技巧

### 过滤特定事件

```bash
# 查看所有决策点
jq 'select(.span_type == "decision")' trace.jsonl

# 查看所有动态匹配（包括跳过的元素）
jq 'select(.span_type == "dynamic_matching")' trace.jsonl

# 查看状态转换序列
jq 'select(.span_type == "state_transition")' trace.jsonl | \
  jq -r '"\(.from_state) → \(.to_state) | \(.node_id)"'
```

### 统计状态转换

```bash
jq 'select(.span_type == "state_transition")' trace.jsonl | \
  jq -r '.to_state' | sort | uniq -c | sort -rn
```
```

#### 3.4.2 Trace 事件参考

**新建文件**：`docs/TRACE_EVENT_REFERENCE.md`

```markdown
# Trace 事件类型参考

## decision

记录关键决策点和上下文。

**何时记录**：
- 进入/退出 FRAME_COMPLETE
- 决定跳过子节点生成
- 选择恢复策略

**示例**：
```json
{
  "span_type": "decision",
  "action": "branch_complete_frame",
  "metadata": {
    "reason": "no_more_children",
    "node": "menu_container-Wi-Fi-0-root",
    "stack_depth": 2,
    "visited_count": 1
  }
}
```

## dynamic_matching

记录动态匹配结果，包括跳过的元素。

**何时记录**：
- 元素匹配成功
- 元素不匹配任何规则（跳过）
- 元素匹配但动作不是 GENERATE_CHILD

**示例**：
```json
{
  "span_type": "dynamic_matching",
  "metadata": {
    "reason": "no_match",
    "item": {
      "type": "menu_item",
      "text": "HomeNetwork",
      "index": 1
    }
  }
}
```

## state_transition

记录状态机转换。

**字段**：
- `from_state`: 源状态
- `to_state`: 目标状态
- `node_id`: 相关节点ID
- `action`: 触发动作（push_child, no_more_children等）

**示例**：
```json
{
  "span_type": "state_transition",
  "from_state": "branch",
  "to_state": "node_select",
  "node_id": "menu_container-Wi-Fi-0-root",
  "action": "push_child"
}
```
```

### 3.5 代码质量改进（P2）

#### 3.5.1 添加不变量检查

**修改文件**：`src/traversal/graph_engine.py`

**新增方法**：
```python
def _assert_invariants(self) -> None:
    """
    断言关键不变量，提前发现异常。
    
    V6.9.5: 添加运行时检查以快速发现问题
    """
    stack = self.context.node_stack
    
    # 堆栈深度应合理
    assert stack.size() <= 100, f"Stack too deep: {stack.size()}"
    
    # 访问节点数不应超过合理范围
    assert len(self.context.visited_nodes) <= 10000, \
        f"Too many visited nodes: {len(self.context.visited_nodes)}"
    
    # 当前路径长度应与堆栈深度匹配
    assert len(self.context.current_path) == stack.size(), \
        f"Path length mismatch: path={len(self.context.current_path)}, stack={stack.size()}"
    
    # 当前状态应在有效状态列表中
    valid_states = {s.value for s in TraversalState}
    assert self.state_machine._state.value in valid_states, \
        f"Invalid state: {self.state_machine._state}"
```

**在 _step_once 中调用**：
```python
def _step_once(self) -> Dict[str, Any]:
    """执行单步遍历"""
    try:
        # ... 原有逻辑 ...
        
        # 每步后检查不变量
        self._assert_invariants()
        
        return transition_dict
    except AssertionError as e:
        # 捕获不变量违反，记录更详细的上下文
        self._record_error("invariant_violation", str(e))
        raise
```

#### 3.5.2 重构复杂条件逻辑

**修改文件**：`src/traversal/graph_engine.py`

**优化前**（嵌套过深）：
```python
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

**优化后**（提取方法）：
```python
def _handle_branch_for_children(
    self, 
    transition: TraversalStateTransition, 
    stack: NodeStack
) -> Tuple[Optional[str], bool]:
    """
    处理 BRANCH 状态的子节点逻辑。
    
    Returns:
        (child_pushed, should_complete_frame)
    """
    if transition.to_state != TraversalState.BRANCH:
        return None, False
    
    if transition.from_state not in (EXECUTE, RESULT_VERIFY, PRECONDITION_CHECK):
        return None, False
    
    current = stack.peek()
    if not current or current.is_leaf():
        return None, False
    
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

## 四、实施计划

### 4.1 优先级排序

| 优先级 | 改进项 | 文件 | 预计工时 | 价值 |
|--------|--------|------|----------|------|
| **P0** | 状态堆栈查看器 | `dashboards/state_stack_viewer.py` | 2h | 立即提升调试效率 |
| **P0** | BRANCH 处理单元测试 | `tests/state_machine/test_branch_handling.py` | 3h | 防止回归 |
| **P1** | 决策点 Trace 增强 | `src/traversal/graph_engine.py` | 4h | 提升可观测性 |
| **P1** | 状态转换断言增强 | `src/state_machine/traversal_fsm.py` | 1h | 更好的错误信息 |
| **P2** | 提取未访问子节点检查 | `src/state_machine/traversal_fsm.py` | 2h | 代码质量 |
| **P2** | 不变量检查 | `src/traversal/graph_engine.py` | 2h | 早期问题发现 |
| **P2** | 重构复杂条件逻辑 | `src/traversal/graph_engine.py` | 4h | 长期可维护性 |
| **P2** | 完整遍历集成测试 | `tests/v6/settings/test_settings_full_traversal.py` | 3h | 端到端验证 |
| **P3** | 状态机调试指南 | `docs/debugging/STATE_MACHINE_DEBUGGING.md` | 2h | 知识沉淀 |
| **P3** | Trace 事件参考 | `docs/TRACE_EVENT_REFERENCE.md` | 2h | 文档完善 |

**总工时**：约 25 小时

### 4.2 实施顺序

**第一阶段**（1-2天）：立即见效
- P0：状态堆栈查看器
- P0：BRANCH 处理单元测试

**第二阶段**（3-5天）：增强可观测性
- P1：决策点 Trace 增强
- P1：状态转换断言增强

**第三阶段**（1-2周）：长期质量
- P2：代码重构和优化
- P2：集成测试完善
- P3：文档编写

---

## 五、验证方式

### 5.1 功能验证

1. **状态堆栈查看器**
   ```python
   # 运行查看器
   python dashboards/state_stack_viewer.py
   
   # 验证：能正确显示堆栈深度、节点名称、状态信息
   ```

2. **单元测试**
   ```bash
   pytest tests/state_machine/test_branch_handling.py -v
   # 验证：所有测试通过，覆盖率达到目标
   ```

3. **集成测试**
   ```bash
   pytest tests/v6/settings/test_settings_full_traversal.py -v
   # 验证：完整遍历成功，无无限循环
   ```

### 5.2 性能验证

1. **Trace 大小**
   - 增强后的 trace 应 < 原始大小的 150%
   - 决策点 span 不应显著增加 trace 大小

2. **运行速度**
   - 不变量检查不应增加 > 5% 运行时间
   - 状态堆栈查看器仅在调试时启用

### 5.3 效果验证

**重现原始问题测试**：
1. 故意引入 BRANCH 处理 bug
2. 使用新工具定位问题
3. 记录定位时间
4. **目标**：< 30 分钟

**对比测试**：
| 指标 | 修复前 | 修复后 |
|------|--------|--------|
| 定位时间 | 2-4h | < 30min |
| 需要的trace分析 | 手动编写脚本 | 开箱即用 |
| 错误信息 | 简单的 ValueError | 包含堆栈/历史/建议 |

---

## 六、成功标准

1. ✅ 状态堆栈查看器能实时显示当前状态和堆栈
2. ✅ Trace 中包含所有关键决策点的上下文
3. ✅ 状态转换错误提供明确的调试信息
4. ✅ BRANCH 处理有完整的单元测试覆盖（> 90%）
5. ✅ 调试指南能帮助开发者快速定位常见问题
6. ✅ 类似问题的修复时间 < 30 分钟

---

## 附录

### A. 相关文件清单

**核心代码**：
- `src/traversal/graph_engine.py` - 图遍历引擎
- `src/state_machine/traversal_fsm.py` - 状态机
- `src/graph/matcher.py` - 动态匹配器

**测试代码**：
- `tests/v6/settings/test_settings_simulation.py` - Settings 遍历测试
- `tests/state_machine/test_branch_handling.py` - BRANCH 处理测试（新增）
- `tests/v6/settings/test_settings_full_traversal.py` - 完整遍历测试（新增）

**工具**：
- `dashboards/state_stack_viewer.py` - 状态堆栈查看器（新增）
- `dashboards/visited_nodes_tree.py` - 节点树可视化

**文档**：
- `docs/debugging/STATE_MACHINE_DEBUGGING.md` - 调试指南（新增）
- `docs/TRACE_EVENT_REFERENCE.md` - Trace 事件参考（新增）

### B. 关键代码位置

| 功能 | 文件 | 行号 |
|------|------|------|
| BRANCH 处理 | `src/state_machine/traversal_fsm.py` | 1763-1810 |
| 获取未访问子节点 | `src/traversal/graph_engine.py` | 1132-1186 |
| 子节点生成 | `src/traversal/graph_engine.py` | 1188-1310 |
| 状态转换 | `src/state_machine/traversal_fsm.py` | 306-343 |
| 跳过元素记录 | `src/traversal/graph_engine.py` | 1321-1359 |
