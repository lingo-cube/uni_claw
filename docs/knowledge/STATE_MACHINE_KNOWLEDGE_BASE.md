# 状态机模块知识库

> **State Machine Knowledge Base**
> **用途**: 为多Agent测试验证提供状态机模块的专业知识
> **更新**: 2026-06-08 (V2.0 - 架构对齐版)

---

## 快速导航

| Agent角色 | 需要的信息 | 位置 |
|-----------|-----------|------|
| Agent 1: 代码分析 | 类定义、方法签名、外部依赖、状态变更、不变量 | [核心组件详解](#核心组件详解-agent-1) |
| Agent 2: 文档分析 | 行为规范、参数要求、测试场景 | [行为规范与场景](#行为规范与场景-agent-2) |
| Agent 3: 场景综合 | Given/When/Then场景、Mock映射、验证清单 | [测试场景清单](#测试场景清单-agent-3) |

---

## 核心组件详解 (Agent 1)

### 1. GlobalStateMachine (全局状态机)

#### 类定义

```python
class GlobalStateMachine:
    """管理整个遍历流程的全局状态"""
    
    def __init__(self):
        self.current_state = GlobalState.IDLE
        self.error_handler = ErrorHandler()
```

#### 方法签名表

| 方法 | 参数 | 返回类型 | 异常 | 外部依赖 |
|------|------|----------|------|----------|
| `initialize()` | `context: TraversalContext` | `None` | `ValueError` | `context.visited_children` (read) |
| `get_state()` | 无 | `GlobalState` | 无 | 无 |
| `transition_to()` | `event: Event` | `None` | `StateTransitionError` | 无 |
| `is_complete()` | 无 | `bool` | 无 | `context.node_stack` (read) |
| `start()` | 无 | `None` | `RuntimeError` | 无 |
| `pause()` | 无 | `None` | 无 | 无 |
| `resume()` | 无 | `None` | `RuntimeError` | 无 |
| `stop()` | 无 | `None` | 无 | 无 |

#### 状态变更

- `initialize()`: 设置 `current_state = IDLE`
- `start()`: 设置 `current_state = RUNNING`
- `pause()`: 设置 `current_state = PAUSED`
- `resume()`: 设置 `current_state = RUNNING`
- `stop()`: 设置 `current_state = COMPLETE`

#### 不变量

- `context != None`
- `current_state in VALID_STATES`

---

### 2. TraversalStateMachine (遍历状态机)

#### 类定义

```python
class TraversalStateMachine:
    """管理单个遍历任务的状态"""
    
    def __init__(self, context: TraversalContext, 
                 trace_recorder: Optional[TraceRecorder] = None):
        self.context = context
        self.current_state = TraversalState.FRAME_START
        self.trace_recorder = trace_recorder
```

#### 方法签名表

| 方法 | 参数 | 返回类型 | 异常 | 外部依赖 |
|------|------|----------|------|----------|
| `initialize()` | `context: TraversalContext` | `None` | `ValueError` | `context.node_stack` (write) |
| `process_event()` | `event: Event` | `None` | `InvalidEventError` | `engine._get_next_unvisited_child` |
| `has_unvisited_children()` | `engine: TraversalEngine` | `bool` | `ValueError` | `engine._get_next_unvisited_child`, `context.visited_children` |
| `_handle_branch()` | `stack, context, engine=None` | `TraversalState` | `StackError` | `engine._get_next_unvisited_child` |
| `_handle_dynamic_match()` | `stack, context, engine=None` | `TraversalState` | `ValueError` | `engine._get_next_unvisited_child`, `context.visited_children` |
| `get_next_state()` | 无 | `TraversalState` | 无 | 无 |

#### V6.9.5核心修复

**方法**: `has_unvisited_children(engine: TraversalEngine) -> bool`

**问题**: DYNAMIC_MATCH状态无限循环
- 原因: 所有子节点已访问时仍返回True
- 影响: 无法退出DYNAMIC_MATCH状态

**修复**:
```python
# 修复前
def has_unvisited_children(self, engine):
    next_child = engine._get_next_unvisited_child(self.current_frame)
    return next_child is not None  # ❌ 不检查visited_children

# 修复后
def has_unvisited_children(self, engine):
    # 检查frame的静态子节点
    static_children = self.current_frame.static_children
    visited = self.context.visited_children.get(self.current_frame.id, [])
    
    # 如果所有静态子节点都已访问，返回False
    if len(visited) >= len(static_children):
        return False
    
    # 否则检查是否有下一个未访问的子节点
    next_child = engine._get_next_unvisited_child(self.current_frame)
    return next_child is not None
```

**测试要求**: 必须验证修复后的行为

#### 状态变更

- `initialize()`: 设置 `current_state = FRAME_START`, 初始化 `context.node_stack`
- `process_event()`: 根据event类型更新 `current_state`
- `_handle_branch()`: 可能触发状态转换到 `BRANCH` 或 `FRAME_COMPLETE`
- `_handle_dynamic_match()`: 可能触发状态转换到 `DYNAMIC_MATCH` 或 `FRAME_COMPLETE`

#### 不变量

- `context != None`
- `current_state in VALID_TRAVERSAL_STATES`
- `node_stack.depth >= 0`

---

### 3. NodeStack (节点栈)

#### 类定义

```python
class NodeStack:
    """管理遍历路径的栈结构"""
    
    def __init__(self, max_depth: int = 100):
        self._items = []
        self.max_depth = max_depth
    
    @property
    def depth(self) -> int:
        return len(self._items)
```

#### 方法签名表

| 方法 | 参数 | 返回类型 | 异常 | 状态变更 |
|------|------|----------|------|----------|
| `push()` | `node: Node` | `None` | `StackOverflowError` | `depth += 1` |
| `pop()` | 无 | `Node or None` | 无 | `depth -= 1` |
| `peek()` | 无 | `Node or None` | 无 | 无 |
| `is_empty()` | 无 | `bool` | 无 | 无 |
| `clear()` | 无 | `None` | 无 | `depth = 0` |
| `depth` | (属性) | `int` | 无 | (只读) |

#### 边界条件

- **空栈**: `is_empty() = True`, `peek() = None`, `pop() = None`
- **栈溢出**: `depth >= max_depth` 时 `push()` 抛出 `StackOverflowError`
- **并发修改**: 多线程同时操作可能产生竞态条件

#### 不变量

- `0 <= depth <= max_depth`
- `depth == len(_items)`

---

## 外部依赖清单

### Mock映射表

| 组件 | 方法/属性 | Mock要求 | 返回值设置 |
|------|-----------|----------|------------|
| **TraversalEngine** | `_get_next_unvisited_child(node)` | 必须Mock | `Mock(id='child1')` or `None` |
| **TraversalEngine** | `_push_node(node, context)` | 必须Mock | 无返回值 |
| **TraversalContext** | `node_stack` | 真实对象或Mock | `NodeStack()` 实例 |
| **TraversalContext** | `visited_children` | 真实对象或Mock | `dict` 实例 |
| **TraversalContext** | `current_frame` | 必须Mock | `Mock(id='frame1')` |
| **TraceRecorder** | `record_decision_point()` | 可选Mock | 无返回值 |
| **TraceRecorder** | `record_state_transition()` | 可选Mock | 无返回值 |
| **TraceRecorder** | `record_event()` | 可选Mock | 无返回值 |

---

## 行为规范与场景 (Agent 2)

### Should 规范列表

| ID | 规范 | 适用组件 |
|----|------|----------|
| **BR-001** | 初始化后状态应为IDLE | GlobalStateMachine |
| **BR-002** | 空栈时peek返回None | NodeStack |
| **BR-003** | 空栈时pop返回None | NodeStack |
| **BR-004** | 所有子节点已访问时has_unvisited_children返回False | TraversalStateMachine |
| **BR-005** | DYNAMIC_MATCH所有子节点已访问时返回FRAME_COMPLETE | TraversalStateMachine |
| **BR-006** | 栈深度等于元素数量 | NodeStack |
| **BR-007** | 状态转换必须在VALID_TRANSITIONS中 | TraversalStateMachine |

### Should_Not 规范列表

| ID | 规范 | 适用组件 |
|----|------|----------|
| **BR-N01** | 不应允许None context初始化 | TraversalStateMachine |
| **BR-N02** | 不应允许栈深度超过max_depth | NodeStack |
| **BR-N03** | DYNAMIC_MATCH不应无限循环 | TraversalStateMachine |
| **BR-N04** | 不应允许非法状态转换 | TraversalStateMachine |

---

## 测试场景清单 (Agent 3)

### 场景映射表

| 场景ID | 类型 | 组件 | 方法 | 规范ID |
|--------|------|------|------|--------|
| **SM-001** | normal | GlobalStateMachine | initialize | BR-001 |
| **SM-002** | boundary | NodeStack | peek | BR-002 |
| **SM-003** | boundary | NodeStack | pop | BR-003 |
| **SM-004** | normal | TraversalStateMachine | has_unvisited_children | BR-004 |
| **SM-005** | critical | TraversalStateMachine | has_unvisited_children | BR-005 |
| **SM-006** | normal | NodeStack | depth property | BR-006 |
| **SM-007** | error | TraversalStateMachine | transition_to | BR-N04 |

---

### 结构化场景详情

#### SM-001: 全局状态机初始化

**类型**: normal  
**组件**: GlobalStateMachine  
**方法**: initialize  
**规范**: BR-001

**Given**:
```python
context = Mock(spec=TraversalContext)
context.visited_children = {}
state_machine = GlobalStateMachine()
```

**When**:
```python
state_machine.initialize(context)
```

**Then**:
```python
assert state_machine.get_state() == GlobalState.IDLE
assert state_machine.context == context
```

**需要的Mock**:
- `context: Mock(spec=TraversalContext)`

**验证的副作用**:
- `current_state` 从 None 变为 IDLE
- `context` 被正确存储

**检查的不变量**:
- `context != None`
- `current_state in VALID_STATES`

---

#### SM-002: 空栈peek操作

**类型**: boundary  
**组件**: NodeStack  
**方法**: peek  
**规范**: BR-002

**Given**:
```python
stack = NodeStack()
```

**When**:
```python
result = stack.peek()
```

**Then**:
```python
assert result is None
assert stack.depth == 0  # 栈深度不变
```

**需要的Mock**:
- 无

**验证的副作用**:
- `depth` 不变
- `_items` 不变

**检查的不变量**:
- `0 <= depth <= max_depth`

---

#### SM-003: 空栈pop操作

**类型**: boundary  
**组件**: NodeStack  
**方法**: pop  
**规范**: BR-003

**Given**:
```python
stack = NodeStack()
```

**When**:
```python
result = stack.pop()
```

**Then**:
```python
assert result is None
assert stack.depth == 0
```

**需要的Mock**:
- 无

**验证的副作用**:
- `depth` 不变

**检查的不变量**:
- `0 <= depth <= max_depth`

---

#### SM-004: 有未访问子节点

**类型**: normal  
**组件**: TraversalStateMachine  
**方法**: has_unvisited_children  
**规范**: BR-004

**Given**:
```python
context = Mock(spec=TraversalContext)
context.visited_children = {'frame1': ['child1']}
context.current_frame = Mock(id='frame1')
context.current_frame.static_children = ['child1', 'child2']

engine = Mock(spec=TraversalEngine)
engine._get_next_unvisited_child.return_value = Mock(id='child2')

state_machine = TraversalStateMachine(context)
```

**When**:
```python
result = state_machine.has_unvisited_children(engine)
```

**Then**:
```python
assert result is True
assert engine._get_next_unvisited_child.called
```

**需要的Mock**:
- `engine._get_next_unvisited_child` → `Mock(id='child2')`
- `context.visited_children` → `{'frame1': ['child1']}`
- `context.current_frame.static_children` → `['child1', 'child2']`

**验证的副作用**:
- `engine._get_next_unvisited_child` 被调用

**检查的不变量**:
- `context != None`

---

#### SM-005: 所有子节点已访问 (V6.9.5核心修复)

**类型**: **critical**  
**组件**: TraversalStateMachine  
**方法**: has_unvisited_children  
**规范**: BR-005  
**优先级**: P0 (必须测试)

**Given**:
```python
context = Mock(spec=TraversalContext)
context.visited_children = {'frame1': ['child1', 'child2']}
context.current_frame = Mock(id='frame1')
context.current_frame.static_children = ['child1', 'child2']

engine = Mock(spec=TraversalEngine)

state_machine = TraversalStateMachine(context)
state_machine.current_state = TraversalState.DYNAMIC_MATCH
```

**When**:
```python
result = state_machine.has_unvisited_children(engine)
```

**Then**:
```python
assert result is False  # 关键断言：所有子节点已访问

# 验证DYNAMIC_MATCH能正确退出
next_state = state_machine.get_next_state()
assert next_state == TraversalState.FRAME_COMPLETE
```

**需要的Mock**:
- `context.visited_children` → `{'frame1': ['child1', 'child2']}` (所有子节点已访问)
- `context.current_frame.static_children` → `['child1', 'child2']`
- `engine._get_next_unvisited_child` → 不应该被调用，或返回None

**验证的副作用**:
- 状态能从DYNAMIC_MATCH转换到FRAME_COMPLETE
- 不会进入无限循环

**检查的不变量**:
- `current_state in VALID_TRAVERSAL_STATES`
- `len(visited_children) <= len(static_children)`

---

#### SM-006: 栈深度属性

**类型**: normal  
**组件**: NodeStack  
**方法**: depth property  
**规范**: BR-006

**Given**:
```python
stack = NodeStack()
for i in range(3):
    stack.push(Node(id=i))
```

**When**:
```python
depth = stack.depth
```

**Then**:
```python
assert depth == 3
assert depth == len(stack._items)
```

**需要的Mock**:
- 无

**验证的副作用**:
- 无副作用（只读属性）

**检查的不变量**:
- `0 <= depth <= max_depth`
- `depth == len(_items)`

---

#### SM-007: 非法状态转换

**类型**: error  
**组件**: TraversalStateMachine  
**方法**: transition_to  
**规范**: BR-N04

**Given**:
```python
state_machine = TraversalStateMachine(Mock())
state_machine.current_state = TraversalState.COMPLETE
```

**When**:
```python
with pytest.raises(StateTransitionError):
    state_machine.transition_to(TraversalState.DYNAMIC_MATCH)
```

**Then**:
```python
# 异常被抛出
assert state_machine.current_state == TraversalState.COMPLETE  # 状态不变
```

**需要的Mock**:
- `context: Mock()`

**验证的副作用**:
- `current_state` 不变（异常回滚）

**检查的不变量**:
- 状态转换前和后的 `current_state` 相同

---

## 边界条件场景

| 场景ID | 类型 | 组件 | 方法 | 边界条件 |
|--------|------|------|------|----------|
| **SM-B001** | boundary | NodeStack | push | depth == max_depth |
| **SM-B002** | boundary | NodeStack | push | depth == 0 (第一次push) |
| **SM-B003** | boundary | NodeStack | pop | depth == 1 (pop后变空) |
| **SM-B004** | boundary | TraversalStateMachine | has_unvisited_children | static_children == [] |
| **SM-B005** | boundary | TraversalStateMachine | has_unvisited_children | visited_children数据损坏 |

---

## 错误场景

| 场景ID | 类型 | 组件 | 方法 | 错误条件 |
|--------|------|------|------|----------|
| **SM-E001** | error | TraversalStateMachine | initialize | context == None |
| **SM-E002** | error | NodeStack | push | depth >= max_depth (StackOverflowError) |
| **SM-E003** | error | TraversalStateMachine | process_event | event类型无效 |
| **SM-E004** | error | TraversalStateMachine | has_unvisited_children | context.current_frame == None |

---

## Trace集成验证场景

| 场景ID | 类型 | 验证项 | 断言 |
|--------|------|--------|------|
| **SM-T001** | normal | 决策点记录 | `trace_recorder.record_decision_point.called` |
| **SM-T002** | normal | 状态转换追踪 | `trace_recorder.state_transitions[-1]['to'] == expected_state` |
| **SM-T003** | normal | 事件格式 | `trace_recorder.events[-1]['format'] == 'standard'` |

---

## 完整Mock配置模板

```python
@pytest.fixture
def state_machine_test_setup():
    """
    完整的状态机测试Mock配置
    
    返回包含所有必要Mock的字典
    """
    # Mock Engine
    mock_engine = Mock(spec=TraversalEngine)
    mock_engine._get_next_unvisited_child.return_value = Mock(id='child1')
    mock_engine._push_node = Mock()
    
    # Mock Context
    mock_context = Mock(spec=TraversalContext)
    mock_context.node_stack = NodeStack()
    mock_context.visited_children = {}
    mock_context.current_frame = Mock(id='frame1')
    mock_context.current_frame.static_children = ['child1', 'child2']
    
    # Mock Trace Recorder
    mock_trace = Mock(spec=TraceRecorder)
    mock_trace.record_decision_point = Mock()
    mock_trace.record_state_transition = Mock()
    mock_trace.record_event = Mock()
    
    # Mock Node
    mock_node = Mock()
    mock_node.id = 'node1'
    
    # 创建状态机
    state_machine = TraversalStateMachine(mock_context, mock_trace)
    
    return {
        'state_machine': state_machine,
        'engine': mock_engine,
        'context': mock_context,
        'trace': mock_trace,
        'node': mock_node
    }
```

---

## JSON摘要 (供Agent解析)

```json
{
  "module": "state_machine",
  "version": "2.0",
  "classes": [
    {
      "name": "GlobalStateMachine",
      "methods": ["initialize", "get_state", "transition_to", "is_complete", "start", "pause", "resume", "stop"],
      "state_vars": ["current_state", "error_handler"]
    },
    {
      "name": "TraversalStateMachine",
      "methods": ["initialize", "process_event", "has_unvisited_children", "_handle_branch", "_handle_dynamic_match", "get_next_state"],
      "state_vars": ["current_state", "context", "trace_recorder"],
      "critical_fix": "has_unvisited_children V6.9.5"
    },
    {
      "name": "NodeStack",
      "methods": ["push", "pop", "peek", "is_empty", "clear"],
      "properties": ["depth"],
      "state_vars": ["_items", "max_depth"]
    }
  ],
  "external_dependencies": [
    {"component": "TraversalEngine", "methods": ["_get_next_unvisited_child", "_push_node"]},
    {"component": "TraversalContext", "attributes": ["node_stack", "visited_children", "current_frame"]},
    {"component": "TraceRecorder", "methods": ["record_decision_point", "record_state_transition", "record_event"]}
  ],
  "scenarios": [
    {"id": "SM-001", "type": "normal", "priority": "P1"},
    {"id": "SM-002", "type": "boundary", "priority": "P1"},
    {"id": "SM-003", "type": "boundary", "priority": "P1"},
    {"id": "SM-004", "type": "normal", "priority": "P1"},
    {"id": "SM-005", "type": "critical", "priority": "P0", "note": "V6.9.5核心修复"},
    {"id": "SM-006", "type": "normal", "priority": "P2"},
    {"id": "SM-007", "type": "error", "priority": "P1"}
  ],
  "invariants": [
    "context != None",
    "current_state in VALID_STATES",
    "0 <= depth <= max_depth",
    "depth == len(_items)"
  ],
  "behaviors": {
    "should": ["BR-001", "BR-002", "BR-003", "BR-004", "BR-005", "BR-006", "BR-007"],
    "should_not": ["BR-N01", "BR-N02", "BR-N03", "BR-N04"]
  }
}
```

---

## 相关文档

- **设计文档**: `docs/architecture/concepts/state-machine-design.md`
- **测试场景**: `docs/testing/STATE_MACHINE_TEST_SCENARIOS.md`
- **API文档**: `docs/api/state-machine.md`

---

**维护者**: Uni-Claw Development Team  
**版本**: V2.0 (架构对齐版)  
**更新频率**: 随模块更新同步更新
