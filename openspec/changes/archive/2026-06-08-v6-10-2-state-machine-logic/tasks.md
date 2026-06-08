# V6.10.2 状态机逻辑与可观测性增强 - 实施任务

> **变更**: v6-10-2-state-machine-logic
> **创建日期**: 2026-06-08
> **状态**: 实施阶段

---

## 任务概览

| 任务 ID | 描述 | 预计时间 | 依赖 | 可验证 |
|---------|------|----------|------|--------|
| T1 | 新增 `_has_unvisited_children()` 方法 | 2h | - | `pytest tests/state_machine/test_has_unvisited_children.py -v` 通过 |
| T2 | 优化 `transition_to()` 错误信息 | 1.5h | - | 触发无效转换，检查错误信息包含所有字段 |
| T3 | 添加状态转换 Trace 记录 | 1h | T2 | 检查 trace 输出包含 state_transition span |
| T4 | 注入 trace_recorder 给 state_machine | 0.5h | - | 验证 state_machine 有 _trace_recorder 属性 |
| T5 | 编写单元测试 | 0.5h | T1,T2,T3,T4 | 所有测试通过，覆盖率达标 |

**总计**: 5.5 小时

---

## 详细任务

### T1: 新增 `_has_unvisited_children()` 方法

**文件**: `src/traversal/graph_engine.py`

**步骤**:

1. 在 `GraphTraversalEngine` 类中新增私有方法 `_has_unvisited_children()`
2. 实现以下逻辑：
   - 检查 `children_strategy` 是否存在
   - 处理 `NONE` 类型：返回 False
   - 处理 `STATIC` 类型：遍历 `static_children`，检查是否有未访问的
   - 处理 `DYNAMIC_MATCH` 类型：调用 `_get_next_unvisited_child()` 检查
   - 处理不支持的类型：抛出 `ValueError`
3. 添加完整的类型注解和 docstring
4. 通过 mypy strict 检查

**验证**:
```bash
pytest tests/state_machine/test_has_unvisited_children.py -v
mypy src/traversal/graph_engine.py --strict
```

---

### T2: 优化 `transition_to()` 错误信息

**文件**: `src/state_machine/traversal_fsm.py`

**步骤**:

1. 在 `transition_to()` 方法的错误处理部分增强错误信息
2. 添加以下信息：
   - 当前节点 ID
   - 目标节点 ID（从 metadata 获取）
   - 最近 5 条状态转换（处理历史少于 5 条的情况）
   - 从当前状态的有效转换列表
3. 修复 IndexError：使用 `min(5, len(self._transition_history))`
4. 格式化错误信息，使其易于阅读

**验证**:
```python
# 在测试中触发无效转换
with pytest.raises(ValueError) as exc_info:
    fsm.transition_to(TraversalState.BRANCH)

# 验证错误信息包含所有字段
assert "Invalid state transition" in str(exc_info.value)
assert "Recent transitions" in str(exc_info.value)
assert "Valid transitions" in str(exc_info.value)
```

---

### T3: 添加状态转换 Trace 记录

**文件**: `src/state_machine/traversal_fsm.py`

**步骤**:

1. 在 `transition_to()` 方法中，在状态转换前添加 Trace 记录
2. 检查 `_trace_recorder` 属性是否存在且不为 None
3. 创建 `SpanNode`，包含以下 metadata：
   - `from_state`: 当前状态
   - `to_state`: 目标状态
   - `node_id`: 相关节点 ID
   - `action`: 动作类型
   - 其他 metadata
4. 调用 `_trace_recorder.record_span(span)`

**验证**:
```python
# 验证 trace 记录
if fsm._trace_recorder:
    assert any(
        span.span_type == "state_transition"
        for span in fsm._trace_recorder.spans
    )
```

---

### T4: 注入 trace_recorder 给 state_machine

**文件**: `src/traversal/graph_engine.py`

**步骤**:

1. 在 `GraphTraversalEngine.__init__()` 末尾添加代码
2. 检查 `self.trace_recorder` 是否存在
3. 如果存在，注入给 `self.state_machine._trace_recorder`

**代码**:
```python
# 在 GraphTraversalEngine.__init__ 末尾添加
if self.trace_recorder:
    self.state_machine._trace_recorder = self.trace_recorder
```

**验证**:
```python
# 验证注入成功
engine = GraphTraversalEngine(...)
if engine.trace_recorder:
    assert hasattr(engine.state_machine, '_trace_recorder')
    assert engine.state_machine._trace_recorder is engine.trace_recorder
```

---

### T5: 编写单元测试

**文件**:
- `tests/state_machine/test_has_unvisited_children.py`
- `tests/state_machine/test_transition_to.py`

**步骤**:

1. 创建 `test_has_unvisited_children.py`，覆盖所有场景：
   - 无子节点策略
   - NONE 策略
   - 静态-无子节点
   - 静态-全部已访问
   - 静态-有未访问
   - 动态-全部已访问
   - 动态-有未访问
   - 不支持的策略

2. 创建 `test_transition_to.py`，覆盖：
   - 无效转换的错误信息
   - 有效转换
   - Trace 记录

3. 使用 Given-When-Then 格式的 docstring

4. 命名符合 `test_<method>_<scenario>_<expected>` 格式

**验证**:
```bash
pytest tests/state_machine/test_has_unvisited_children.py -v
pytest tests/state_machine/test_transition_to.py -v
pytest tests/state_machine/ --cov=src/state_machine --cov=src/traversal
```

---

## 验收标准

### 功能验收

- ✅ `_has_unvisited_children()` 方法能正确处理 STATIC 和 DYNAMIC_MATCH 策略
- ✅ `_has_unvisited_children()` 对 DYNAMIC_MATCH 节点不总是返回 True
- ✅ `transition_to()` 的错误信息包含：当前节点、目标节点、最近转换、有效转换列表
- ✅ 所有状态转换都有 Trace 记录（当 trace_recorder 存在时）

### 代码质量验收

- ✅ 通过 mypy strict 类型检查
- ✅ 所有新增/修改方法有完整类型注解（参数 + 返回值）
- ✅ 禁用 `Any` 类型（除 metadata 参数）
- ✅ 通过 ruff linting（零警告）
- ✅ 符合强类型要求（CLAUDE_CONVENTIONS.md §1）
- ✅ `_has_unvisited_children()` 方法圈复杂度 < 10

### 测试覆盖验收

- ✅ `test_has_unvisited_children.py` 覆盖率 **> 90%**
- ✅ `test_transition_to.py` 覆盖率 **> 85%**
- ✅ 所有测试命名符合 `test_<method>_<scenario>_<expected>` 格式
- ✅ 测试文件放置在 `tests/state_machine/`
- ✅ 测试使用 Given-When-Then 格式的 docstring

---

## 完成检查清单

- [x] 所有任务完成
- [x] 所有测试通过
- [x] 代码质量检查通过（mypy strict, ruff）
- [x] 测试覆盖率达到目标
- [ ] 更新 `CLAUDE_STATUS.md` 版本信息
- [ ] 更新 `src/state_machine/README.md`（如果存在）

---

## 修订记录

| 日期 | 版本 | 修订内容 |
|------|------|----------|
| 2026-06-08 | 1.0 | 初始任务清单 |
