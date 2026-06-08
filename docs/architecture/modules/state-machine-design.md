# 状态机模块设计

> **实现层面**: 技术实现、代码结构和API
> **概念层面**: 详见 [状态机概念设计](../../concepts/state-machine-design.md)
> **测试场景**: 详见 [测试场景](#测试场景)
> **模块路径**: `src/state_machine/`
> **版本**: V6.5
> **最后更新**: 2026-06-08 (补充测试场景)

---

## 文档说明

本文档包含：
- ✅ 模块代码结构
- ✅ 类和接口设计
- ✅ 技术实现细节
- ✅ API使用方法
- ✅ **测试场景和Mock配置** (新增)

---

## Overview

The State Machine module (`src/state_machine/`) implements a three-layer state machine system for uni-claw V6.0, managing the traversal task lifecycle and individual node execution flow with support for hierarchical state machines, error handling, and popup detection.

## Module Location

```
src/state_machine/
├── __init__.py            # Public API exports
├── global_fsm.py          # Global state machine
├── traversal_fsm.py      # Traversal state machine
├── node_stack.py         # Depth-first traversal stack
└── interaction.py        # Orchestrator and coordination
```

---

## 核心类设计 (Agent 1: 代码分析)

### 1. GlobalStateMachine

```python
class GlobalStateMachine:
    """管理整个遍历任务的生命周期"""
    
    VALID_TRANSITIONS = {
        GlobalState.IDLE: {GlobalState.INITIALIZING},
        GlobalState.INITIALIZING: {GlobalState.TRAVERSING, GlobalState.ERROR},
        GlobalState.TRAVERSING: {GlobalState.PAUSED, GlobalState.ERROR, GlobalState.COMPLETED},
        # ... 更多转换
    }
    
    def __init__(self):
        self.current_state = GlobalState.IDLE
        self.error_handler = ErrorHandler()
    
    # 核心方法
    def transition_to(self, target_state: GlobalState, reason: str = None, **metadata) -> None:
        """状态转换，验证转换合法性"""
        if target_state not in self.VALID_TRANSITIONS[self.current_state]:
            raise StateTransitionError(...)
        # 执行转换
    
    def get_state(self) -> GlobalState:
        """获取当前状态"""
    
    def is_complete(self) -> bool:
        """检查是否完成"""
```

**状态定义**:
```python
class GlobalState(Enum):
    IDLE = "idle"
    INITIALIZING = "initializing"
    TRAVERSING = "traversing"
    PAUSED = "paused"
    ERROR = "error"
    RECOVERING = "recovering"
    COMPLETED = "completed"
    TERMINATED = "terminated"
```

**外部依赖**: 无

**不变量**:
- `current_state in VALID_STATES`
- 状态转换必须在 `VALID_TRANSITIONS` 中

---

### 2. TraversalStateMachine

```python
class TraversalStateMachine:
    """管理单个遍历任务的状态"""
    
    def __init__(self, context: TraversalContext, trace_recorder: Optional[TraceRecorder] = None):
        self.context = context
        self.current_state = TraversalState.NODE_SELECT
        self.trace_recorder = trace_recorder
    
    # 核心方法
    def transition_to(self, target_state: TraversalState, node_id: str = None, **metadata) -> None:
        """状态转换"""
    
    def has_unvisited_children(self, engine: TraversalEngine) -> bool:
        """V6.9.5核心修复：检查是否有未访问的子节点"""
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

**状态定义**:
```python
class TraversalState(Enum):
    NODE_SELECT = "node_select"
    PRECONDITION_CHECK = "precondition_check"
    EXECUTE = "execute"
    RESULT_VERIFY = "result_verify"
    BRANCH = "branch"
    FRAME_COMPLETE = "frame_complete"      # V6
    ERROR_HANDLING = "error_handling"      # V6
    POPUP_HANDLING = "popup_handling"      # V6
```

**外部依赖**:
- `context: TraversalContext` (必需)
  - `context.node_stack: NodeStack`
  - `context.visited_children: Dict[str, List[str]]`
  - `context.current_frame: TraversalNode`
- `engine: TraversalEngine` (has_unvisited_children方法)
  - `engine._get_next_unvisited_child(node: TraversalNode) -> Optional[TraversalNode]`

**不变量**:
- `context != None`
- `current_state in VALID_TRAVERSAL_STATES`
- `node_stack.depth >= 0`

---

### 3. NodeStack

```python
class NodeStack:
    """深度优先遍历栈"""
    
    DEFAULT_MAX_DEPTH = 10
    
    def __init__(self, max_depth: int = DEFAULT_MAX_DEPTH):
        self._frames: List[StackFrame] = []
        self.max_depth = max_depth
    
    @property
    def depth(self) -> int:
        """栈深度"""
        return len(self._frames)
    
    def push(self, node: TraversalNode, children: List[str] = None) -> bool:
        """压入节点，返回是否成功"""
        if self.depth >= self.max_depth:
            return False
        # ...
    
    def pop(self) -> Optional[StackFrame]:
        """弹出节点"""
    
    def peek(self, offset: int = 0) -> Optional[StackFrame]:
        """查看节点（不弹出）"""
    
    def is_empty(self) -> bool:
        """检查是否为空"""
```

**外部依赖**: 无

**不变量**:
- `0 <= depth <= max_depth`
- `depth == len(_frames)`

---

## 测试场景 (Agent 2 & 3: 测试验证)

### 场景ID系统

| 场景ID | 类型 | 组件 | 方法 | 优先级 | 规范 |
|--------|------|------|------|--------|------|
| **SM-001** | normal | GlobalStateMachine | initialize | P1 | 初始化后状态应为IDLE |
| **SM-002** | boundary | NodeStack | peek | P1 | 空栈时peek返回None |
| **SM-003** | boundary | NodeStack | pop | P1 | 空栈时pop返回None |
| **SM-004** | normal | TraversalStateMachine | has_unvisited_children | P1 | 有未访问子节点返回True |
| **SM-005** | **critical** | TraversalStateMachine | has_unvisited_children | **P0** | V6.9.5核心修复：所有子节点已访问返回False |
| **SM-006** | error | TraversalStateMachine | transition_to | P1 | 非法状态转换抛出异常 |

---

### SM-005: V6.9.5核心修复验证

**类型**: critical  
**优先级**: P0 (必须测试)

**问题**: DYNAMIC_MATCH状态无限循环
- **原因**: `has_unvisited_children()` 所有子节点已访问时仍返回True
- **影响**: 无法退出DYNAMIC_MATCH状态，导致无限循环

**Given**:
```python
# 所有子节点都已访问
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
assert result is False  # 关键断言

# 验证能正确退出DYNAMIC_MATCH
next_state = state_machine.get_next_state()
assert next_state == TraversalState.FRAME_COMPLETE
```

**需要的Mock**:
```python
mock_context = Mock(spec=TraversalContext)
mock_context.visited_children = {'frame1': ['child1', 'child2']}
mock_context.current_frame = Mock(id='frame1')
mock_context.current_frame.static_children = ['child1', 'child2']

mock_engine = Mock(spec=TraversalEngine)
# 注意：修复后不应该调用这个方法，或者返回None
```

**验证的副作用**:
- 状态能从DYNAMIC_MATCH转换到FRAME_COMPLETE
- 不会进入无限循环

**检查的不变量**:
- `len(visited_children) <= len(static_children)`

---

### SM-002: 空栈边界条件

**类型**: boundary

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

**需要的Mock**: 无

---

## Mock配置指南

### 标准Mock模板

```python
@pytest.fixture
def state_machine_test_setup():
    """完整的状态机测试Mock配置"""
    
    # Mock Context
    mock_context = Mock(spec=TraversalContext)
    mock_context.node_stack = NodeStack()
    mock_context.visited_children = {}
    mock_context.current_frame = Mock(id='frame1')
    mock_context.current_frame.static_children = ['child1', 'child2']
    
    # Mock Engine
    mock_engine = Mock(spec=TraversalEngine)
    mock_engine._get_next_unvisited_child.return_value = Mock(id='child2')
    
    # Mock Trace Recorder
    mock_trace = Mock(spec=TraceRecorder)
    mock_trace.record_decision_point = Mock()
    mock_trace.record_state_transition = Mock()
    
    # 创建状态机
    state_machine = TraversalStateMachine(mock_context, mock_trace)
    
    return {
        'state_machine': state_machine,
        'context': mock_context,
        'engine': mock_engine,
        'trace': mock_trace
    }
```

### Mock映射表

| 组件 | 方法/属性 | 是否必须Mock | Mock返回值 |
|------|-----------|-------------|------------|
| TraversalContext | node_stack | 可选(真实对象) | NodeStack() |
| TraversalContext | visited_children | 可选(真实对象) | {} |
| TraversalContext | current_frame | 必须Mock | Mock(id='frame1') |
| TraversalEngine | _get_next_unvisited_child | 必须Mock | Mock(id='child1') or None |
| TraceRecorder | record_decision_point | 可选Mock | 无返回值 |
| TraceRecorder | record_state_transition | 可选Mock | 无返回值 |

---

## V6.9.5修复详情

### 问题描述

在DYNAMIC_MATCH状态下，`has_unvisited_children()` 方法存在缺陷：

```python
# 修复前 (有问题的代码)
def has_unvisited_children(self, engine):
    next_child = engine._get_next_unvisited_child(self.current_frame)
    return next_child is not None  # ❌ 只检查engine返回值
```

**问题**:
- 只检查 `engine._get_next_unvisited_child()` 的返回值
- 不检查 `visited_children` 记录
- 导致所有子节点已访问后仍返回True
- 结果：无法退出DYNAMIC_MATCH，进入无限循环

### 修复方案

```python
# 修复后
def has_unvisited_children(self, engine):
    # 1. 检查frame的静态子节点
    static_children = self.current_frame.static_children
    visited = self.context.visited_children.get(self.current_frame.id, [])
    
    # 2. 如果所有静态子节点都已访问，返回False
    if len(visited) >= len(static_children):
        return False  # ✅ 关键修复
    
    # 3. 否则检查engine是否有下一个未访问的子节点
    next_child = engine._get_next_unvisited_child(self.current_frame)
    return next_child is not None
```

### 测试要求

1. **正常场景**: 有未访问子节点时返回True
2. **边界场景**: 所有子节点已访问时返回False（关键）
3. **边界场景**: 空子节点列表时返回False
4. **集成场景**: DYNAMIC_MATCH能正确转换到FRAME_COMPLETE

---

## 相关文档

- **概念设计**: [状态机概念设计](../../concepts/state-machine-design.md)
- **测试场景**: [STATE_MACHINE_TEST_SCENARIOS.md](../../testing/STATE_MACHINE_TEST_SCENARIOS.md)
- **质量报告**: [UNIT_TEST_QUALITY_REPORT.md](../../reports/UNIT_TEST_QUALITY_REPORT.md)

---

**维护者**: Uni-Claw Development Team
**版本**: V6.5 (补充测试场景版本)
