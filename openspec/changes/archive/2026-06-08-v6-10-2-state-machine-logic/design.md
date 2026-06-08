# V6.10.2 状态机逻辑与可观测性增强 - 设计

> **变更**: v6-10-2-state-machine-logic
> **创建日期**: 2026-06-08
> **状态**: 设计阶段

---

## 1. 架构概述

### 1.1 架构调整

**重要变更（基于审阅意见）**：

原计划将 `has_unvisited_children()` 作为 `TraversalStateMachine` 的方法，但审阅发现这违反单一职责原则（StateMachine 不应负责图的逻辑检查，也不应依赖 GraphTraversalEngine）。

**调整方案**：将此方法移至 `GraphTraversalEngine` 作为私有辅助方法 `_has_unvisited_children()`。

### 1.2 组件关系

```
┌─────────────────────────────────────────────────────────────────┐
│                     GraphTraversalEngine                        │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │              _has_unvisited_children()                    │  │
│  │  (新增：私有辅助方法，检查节点是否有未访问子节点)          │  │
│  └───────────────────────────────────────────────────────────┘  │
│                                                                 │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │              __init__()                                     │  │
│  │  (修改：注入 trace_recorder 给 state_machine)              │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ 使用
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                  TraversalStateMachine                           │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │              transition_to()                                │  │
│  │  (修改：增强错误信息，添加 Trace 记录)                      │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 2. 详细设计

### 2.1 新增 `_has_unvisited_children()` 方法

#### 2.1.1 文件位置

修改文件：`src/traversal/graph_engine.py`

#### 2.1.2 方法签名

```python
def _has_unvisited_children(
    self,
    node: TraversalNode,
    context: TraversalContext
) -> Optional[bool]:
    """
    检查节点是否有未访问的子节点。

    V6.10.2: 提取未访问子节点检查逻辑，解决 DYNAMIC_MATCH 节点
    总是返回 True 的问题。

    Args:
        node: 要检查的节点
        context: 遍历上下文

    Returns:
        - True: 有未访问子节点
        - False: 无未访问子节点
        - None: 无法确定（需要进一步检查）

    Raises:
        ValueError: 如果 children_strategy 类型不支持
    """
```

#### 2.1.3 实现逻辑

```python
def _has_unvisited_children(
    self,
    node: TraversalNode,
    context: TraversalContext
) -> Optional[bool]:
    """检查节点是否有未访问的子节点。"""
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
        # 动态子节点：调用 _get_next_unvisited_child 检查
        child_id = self._get_next_unvisited_child(node)
        return child_id is not None

    raise ValueError(
        f"Unsupported children_strategy type: "
        f"{node.children_strategy.type}"
    )
```

#### 2.1.4 使用方式

```python
# 在 _handle_branch 中调用
has_unvisited = self._has_unvisited_children(
    current_node,
    context
)

if has_unvisited is True:
    return TraversalState.NODE_SELECT
elif has_unvisited is False:
    return TraversalState.FRAME_COMPLETE
# has_unvisited 不应该是 None（DYNAMIC_MATCH 已经处理）
```

---

### 2.2 优化 `transition_to()` 方法

#### 2.2.1 文件位置

修改文件：`src/state_machine/traversal_fsm.py`

#### 2.2.2 增强错误信息

```python
def transition_to(
    self,
    target_state: TraversalState,
    node_id: Optional[str] = None,
    **metadata: Any
) -> bool:
    """
    转换到目标状态（带增强的错误信息）。

    V6.10.2: 增强错误信息，包含调试上下文。

    Args:
        target_state: 目标状态
        node_id: 相关节点 ID
        **metadata: 额外的元数据

    Returns:
        True 如果转换成功

    Raises:
        ValueError: 如果转换无效
    """
    if not self.can_transition_to(target_state):
        # V6.10.2: 增强错误信息，包含调试上下文
        # 修复：处理历史记录少于5条的情况，避免 IndexError
        recent_count = min(5, len(self._transition_history))
        recent_transitions = self._transition_history[-recent_count:]
        recent_str = "\n".join(
            f"    {t.from_state} → {t.to_state} (node: {t.node_id})"
            for t in recent_transitions
        )

        valid_transitions = self.VALID_TRANSITIONS.get(self._state, set())
        valid_str = ", ".join(sorted(s.value for s in valid_transitions))

        raise ValueError(
            f"Invalid state transition: {self._state.value} → {target_state.value}\n"
            f"  Current node: {node_id}\n"
            f"  Target node: {metadata.get('target_node_id', 'N/A')}\n"
            f"  Recent transitions:\n"
            f"{recent_str}\n"
            f"  Valid transitions from {self._state.value}: [{valid_str}]"
        )

    # 记录转换
    transition = StateTransition(
        from_state=self._state,
        to_state=target_state,
        node_id=node_id,
        action=metadata.get('action', 'unknown'),
        timestamp=datetime.now()
    )
    self._transition_history.append(transition)

    # 更新状态
    self._state = target_state
    self._current_node_id = node_id

    return True
```

---

### 2.3 状态转换 Trace 标准化

#### 2.3.1 Trace 注入机制设计

**重要（基于审阅意见）**：

当前 `TraversalStateMachine` 没有 `_trace_recorder` 属性。需要在 `GraphTraversalEngine.__init__` 中将 `trace_recorder` 注入给 `state_machine`。

**注入方案**：在 `GraphTraversalEngine.__init__` 中添加：

```python
# 在 GraphTraversalEngine.__init__ 末尾添加
if self.trace_recorder:
    self.state_machine._trace_recorder = self.trace_recorder
```

#### 2.3.2 文件位置

修改文件：`src/state_machine/traversal_fsm.py`

#### 2.3.3 在 transition_to 中添加 Trace 记录

```python
def transition_to(
    self,
    target_state: TraversalState,
    node_id: Optional[str] = None,
    **metadata: Any
) -> bool:
    """转换到目标状态（带 Trace 记录）。"""
    if not self.can_transition_to(target_state):
        # ... 错误处理 ...

    # V6.10.2: 记录状态转换到 Trace
    # 注意：假设 TraversalStateMachine 有 _trace_recorder 属性
    # 该属性由 GraphTraversalEngine 在初始化时注入
    if hasattr(self, '_trace_recorder') and self._trace_recorder:
        span = SpanNode(
            span_type="state_transition",
            action="state_change",
            metadata={
                "from_state": self._state.value,
                "to_state": target_state.value,
                "node_id": node_id,
                "action": metadata.get('action', 'unknown'),
                **metadata
            }
        )
        self._trace_recorder.record_span(span)

    # 记录到历史并更新状态
    # ... 原有逻辑 ...

    return True
```

#### 2.3.4 Trace 事件格式

```json
{
  "span_type": "state_transition",
  "action": "state_change",
  "metadata": {
    "from_state": "branch",
    "to_state": "node_select",
    "node_id": "menu_container-Wi-Fi-0-root",
    "action": "push_child",
    "child_id": "switch-Wi-Fi-0"
  }
}
```

---

## 3. 修改文件清单

| 文件 | 类型 | 内容 | 位置 |
|------|------|------|------|
| `src/traversal/graph_engine.py` | 修改 | 新增 `_has_unvisited_children()` 方法 | `src/traversal/` |
| `src/traversal/graph_engine.py` | 修改 | 在 `__init__()` 中注入 trace_recorder | `src/traversal/` |
| `src/state_machine/traversal_fsm.py` | 修改 | 优化 `transition_to()` 方法 | `src/state_machine/` |
| `src/state_machine/traversal_fsm.py` | 修改 | 在 `transition_to()` 中添加 Trace 记录 | `src/state_machine/` |
| `tests/state_machine/test_has_unvisited_children.py` | 新建 | `_has_unvisited_children()` 单元测试 | `tests/state_machine/` |
| `tests/state_machine/test_transition_to.py` | 新建 | `transition_to()` 单元测试 | `tests/state_machine/` |

---

## 4. 测试设计

### 4.1 `_has_unvisited_children()` 测试矩阵

| 场景 | 节点类型 | 子节点策略 | 已访问子节点 | 预期返回值 |
|------|----------|------------|-------------|------------|
| 无子节点策略 | LEAF_ACTION | None | N/A | False |
| NONE 策略 | CONTAINER | NONE | N/A | False |
| 静态-无子节点 | CONTAINER | STATIC, [] | N/A | False |
| 静态-全部已访问 | CONTAINER | STATIC, [c1,c2] | {c1,c2} | False |
| 静态-有未访问 | CONTAINER | STATIC, [c1,c2] | {c1} | True |
| 动态-全部已访问 | CONTAINER | DYNAMIC_MATCH | {c1,c2} | False |
| 动态-有未访问 | CONTAINER | DYNAMIC_MATCH | {c1} | True |
| 不支持的策略 | CONTAINER | INVALID | N/A | ValueError |

### 4.2 `transition_to()` 错误信息测试

| 场景 | 当前状态 | 目标状态 | 预期 |
|------|----------|----------|------|
| 无效转换 | NODE_SELECT | BRANCH | 抛出 ValueError，包含最近5条转换 |
| 有效转换 | NODE_SELECT | EXECUTE | 成功，返回 True |
| 无效转换-无历史 | NODE_SELECT | BRANCH (空历史) | 抛出 ValueError，recent_transitions 为空 |

### 4.3 `transition_to()` Trace 记录测试

| 场景 | 有 trace_recorder | 预期 |
|------|-------------------|------|
| 有效转换 | 是 | 记录 state_transition span |
| 有效转换 | 否 | 不记录，不崩溃 |
| 无效转换 | 是 | 抛出异常，无记录 |

---

## 5. 风险与缓解

### 5.1 风险

| 风险 | 影响 | 概率 | 缓解措施 |
|------|------|------|----------|
| 修改核心逻辑导致回归 | 高 | 中 | 完善单元测试和集成测试 |
| Trace 记录影响性能 | 低 | 低 | 可配置，可选启用 |
| 错误信息格式变化影响工具 | 低 | 低 | 保持关键信息兼容 |

### 5.2 缓解措施

1. **完善测试覆盖**：确保所有新增和修改的方法都有对应的单元测试
2. **渐进式修改**：先新增方法，再逐步替换原有逻辑
3. **性能测试**：验证 Trace 记录不影响关键路径性能

---

## 6. 修订记录

| 日期 | 版本 | 修订内容 |
|------|------|----------|
| 2026-06-08 | 1.0 | 初始设计文档 |
