# V6.12.0 分层上下文架构集成指南

> **版本**: V6.12.0-Integration
> **日期**: 2026-06-09
> **状态**: 实施指南
> **基于**: [PRD_V6_12_0_Layered_Context_Design.md](./PRD_V6_12_0_Layered_Context_Design.md)

---

## 1. 当前架构分析

### 1.1 核心组件关系

```
GraphTraversalEngine
├── TraversalRuntimeContext (当前的全局上下文)
│   ├── node_stack: List[StackFrame]
│   ├── visited_children: Dict[str, Set[str]]  # 节点级访问跟踪
│   ├── page_cache: Dict[str, Any]
│   └── current_page_analysis: Any
│
├── DynamicChildManager (动态子节点生成)
│   ├── _dynamic_children: Dict[str, List[TraversalNode]]
│   ├── _generated_pairs: Set[tuple]  # 页面级去重 (page_fp, element_name)
│   └── generate(node, context)
│
├── StepOrchestrator (单步执行)
│   └── execute_step(ctx: StepContext)
│
└── TraceCoordinator (跟踪协调)
    └── record_*(...)
```

### 1.2 当前数据流

```
1. 初始化: Engine.create() → TraversalRuntimeContext()
2. 页面分析: AI 返回 → context.current_page_analysis
3. 动态生成: DynamicChildManager.generate()
   - 使用 PageSnapshotManager.fingerprint() 获取页面指纹
   - 去重: (page_fp, element_name) 存入 _generated_pairs
4. 子节点访问: context.visited_children[node_id].add(child_id)
5. 滚动: 在 state_machine 中处理 (节点级)
```

---

## 2. 分层上下文架构集成

### 2.1 新架构组件

```
src/traversal/layered_context.py
├── SessionContext (全局)
│   ├── page_contexts: Dict[str, PageLevelContext]
│   ├── node_contexts: Dict[str, NodeContext]
│   └── get_or_create_page_context(fp) → PageLevelContext
│
├── PageLevelContext (页面级) ← 核心创新
│   ├── generated_dynamic_pairs: Set[Tuple[str, str]]
│   ├── scroll_state: ScrollState
│   ├── invalid_elements: Set[str]
│   └── element_cache: Dict[str, Any]
│
└── NodeContext (节点级)
    ├── child_queue: List[str]
    ├── visited_child_ids: Set[str]
    └── has_unvisited_children() → bool
```

### 2.2 集成点概览

| 组件 | 当前实现 | 分层架构实现 | 变更点 |
|------|---------|-------------|-------|
| **TraversalRuntimeContext** | 直接存储所有状态 | 持有 SessionContext | 添加 session_context 字段 |
| **DynamicChildManager** | 使用 `_generated_pairs` | 使用 `PageLevelContext.generated_dynamic_pairs` | 修改 generate() |
| **StepOrchestrator** | 使用 `context.visited_children` | 使用 `NodeContext.visited_child_ids` | 修改子节点管理 |
| **State Machine** | 节点级滚动状态 | 页面级滚动状态 | 滚动逻辑迁移到 PageContext |

---

## 3. 详细实施方案

### 3.1 阶段 1：创建分层上下文基础（P0）

#### 文件: `src/traversal/layered_context.py`

```python
"""
Layered context architecture for V6.12.

Three-layer hierarchy:
- SessionContext: Global session state
- PageLevelContext: Page-level state (TTL managed)
- NodeContext: Node-level state (ephemeral)
"""

from dataclasses import dataclass, field
from typing import Dict, List, Optional, Set, Tuple, Any
from enum import Enum
import time


# ============================================================================
# Enums
# ============================================================================

class ScrollState(Enum):
    """滚动状态"""
    IDLE = "idle"
    SCROLLING = "scrolling"
    END_REACHED = "end_reached"
    ERROR = "error"


# ============================================================================
# PageLevelContext (页面级)
# ============================================================================

@dataclass
class PageLevelContext:
    """页面级上下文
    
    职责:
    - DYNAMIC 去重 (页面级别)
    - 滚动状态 (页面级别)
    - 失效元素 (页面级别)
    - 元素缓存 (页面级别)
    """
    
    # === 页面标识 ===
    page_fingerprint: str
    """页面唯一标识"""
    
    # === DYNAMIC 去重 (页面级别) ===
    generated_dynamic_pairs: Set[Tuple[str, str]] = field(default_factory=set)
    """已生成的 (node_id, element_name) 对 - 页面级去重"""
    
    # === 滚动状态 (页面级别) ===
    scroll_state: ScrollState = ScrollState.IDLE
    scroll_position: float = 0.0
    scroll_attempts: int = 0
    max_scroll_attempts: int = 5
    
    # === 失效元素 (页面级别) ===
    invalid_elements: Set[str] = field(default_factory=set)
    """在这个页面上失效的元素"""
    
    # === 元素缓存 ===
    element_cache: Dict[str, Any] = field(default_factory=dict)
    """页面级别的元素缓存"""
    
    # === TTL 管理 ===
    created_time: float = field(default_factory=time.time)
    last_access_time: float = field(default_factory=time.time)
    ttl_seconds: int = 3600  # 1小时
    
    # ========================================================================
    # DYNAMIC 管理
    # ========================================================================
    
    def is_dynamic_generated(self, node_id: str, element_name: str) -> bool:
        """检查是否已生成该动态元素"""
        return (node_id, element_name) in self.generated_dynamic_pairs
    
    def record_dynamic_generation(self, node_id: str, element_name: str) -> None:
        """记录动态元素生成"""
        self.generated_dynamic_pairs.add((node_id, element_name))
        self.last_access_time = time.time()
    
    # ========================================================================
    # 滚动管理
    # ========================================================================
    
    def should_continue_scroll(self) -> bool:
        """是否应该继续滚动"""
        return (
            self.scroll_state != ScrollState.END_REACHED
            and self.scroll_state != ScrollState.ERROR
            and self.scroll_attempts < self.max_scroll_attempts
        )
    
    def record_scroll_attempt(self) -> None:
        """记录一次滚动尝试"""
        self.scroll_attempts += 1
        self.last_access_time = time.time()
    
    def mark_scroll_end(self) -> None:
        """标记滚动到达末尾"""
        self.scroll_state = ScrollState.END_REACHED
        self.last_access_time = time.time()
    
    def reset_scroll_state(self) -> None:
        """重置滚动状态（新页面）"""
        self.scroll_state = ScrollState.IDLE
        self.scroll_position = 0.0
        self.scroll_attempts = 0
        self.last_access_time = time.time()
    
    # ========================================================================
    # 失效元素管理
    # ========================================================================
    
    def mark_element_invalid(self, element_name: str) -> None:
        """标记元素为失效"""
        self.invalid_elements.add(element_name)
        self.last_access_time = time.time()
    
    def is_element_invalid(self, element_name: str) -> bool:
        """检查元素是否失效"""
        return element_name in self.invalid_elements
    
    # ========================================================================
    # TTL 管理
    # ========================================================================
    
    def is_expired(self) -> bool:
        """检查是否过期"""
        return time.time() - self.last_access_time > self.ttl_seconds
    
    def refresh_ttl(self) -> None:
        """刷新 TTL"""
        self.last_access_time = time.time()


# ============================================================================
# NodeContext (节点级)
# ============================================================================

@dataclass
class NodeContext:
    """节点级上下文
    
    职责:
    - 子节点队列管理
    - 访问跟踪
    - 节点级错误计数
    """
    
    # === 节点标识 ===
    node_id: str
    node_type: str
    
    # === 子节点管理 ===
    child_queue: List[str] = field(default_factory=list)
    visited_child_ids: Set[str] = field(default_factory=set)
    current_child_idx: int = 0
    
    # === 执行统计 ===
    visit_count: int = 0
    last_visit_time: Optional[float] = None
    
    # === 错误和重试 ===
    consecutive_errors: int = 0
    last_error: Optional[Exception] = None
    
    # ========================================================================
    # 子节点管理
    # ========================================================================
    
    def has_unvisited_children(self) -> bool:
        """是否有未访问的子节点"""
        return len(self.visited_child_ids) < len(self.child_queue)
    
    def get_next_unvisited_child(self) -> Optional[str]:
        """获取下一个未访问的子节点"""
        for child_id in self.child_queue:
            if child_id not in self.visited_child_ids:
                return child_id
        return None
    
    def mark_child_visited(self, child_id: str) -> None:
        """标记子节点已访问"""
        self.visited_child_ids.add(child_id)
    
    def add_children(self, child_ids: List[str]) -> None:
        """批量添加子节点到队列"""
        self.child_queue.extend(child_ids)
    
    def get_completion_rate(self) -> float:
        """获取子节点完成率"""
        if not self.child_queue:
            return 1.0
        return len(self.visited_child_ids) / len(self.child_queue)


# ============================================================================
# SessionContext (全局)
# ============================================================================

@dataclass
class SessionContext:
    """一次遍历任务的全局上下文
    
    职责:
    - 管理页面级和节点级上下文的缓存
    - 提供全局统计信息
    """
    
    # === 上下文缓存 ===
    page_contexts: Dict[str, PageLevelContext] = field(default_factory=dict)
    node_contexts: Dict[str, NodeContext] = field(default_factory=dict)
    
    # === 配置 ===
    page_context_ttl: int = 3600  # 1小时
    max_page_contexts: int = 100
    
    # ========================================================================
    # 页面级上下文管理
    # ========================================================================
    
    def get_or_create_page_context(
        self, page_fingerprint: str
    ) -> PageLevelContext:
        """获取或创建页面级上下文"""
        if page_fingerprint not in self.page_contexts:
            # 清理过期上下文
            self._cleanup_expired_page_contexts()
            
            # 检查缓存上限
            if len(self.page_contexts) >= self.max_page_contexts:
                self._evict_oldest_page_context()
            
            # 创建新上下文
            self.page_contexts[page_fingerprint] = PageLevelContext(
                page_fingerprint=page_fingerprint,
                ttl_seconds=self.page_context_ttl
            )
        
        # 更新访问时间
        ctx = self.page_contexts[page_fingerprint]
        ctx.last_access_time = time.time()
        return ctx
    
    def get_page_context(
        self, page_fingerprint: str
    ) -> Optional[PageLevelContext]:
        """获取页面级上下文（不创建）"""
        ctx = self.page_contexts.get(page_fingerprint)
        if ctx and not ctx.is_expired():
            ctx.last_access_time = time.time()
            return ctx
        return None
    
    def _cleanup_expired_page_contexts(self) -> None:
        """清理过期的页面上下文"""
        expired = [
            fp for fp, ctx in self.page_contexts.items()
            if ctx.is_expired()
        ]
        for fp in expired:
            del self.page_contexts[fp]
    
    def _evict_oldest_page_context(self) -> None:
        """淘汰最老的页面上下文（LRU）"""
        if not self.page_contexts:
            return
        
        oldest_fp = min(
            self.page_contexts.keys(),
            key=lambda fp: self.page_contexts[fp].last_access_time
        )
        del self.page_contexts[oldest_fp]
    
    # ========================================================================
    # 节点级上下文管理
    # ========================================================================
    
    def get_or_create_node_context(
        self, node_id: str, node_type: str
    ) -> NodeContext:
        """获取或创建节点级上下文"""
        if node_id not in self.node_contexts:
            self.node_contexts[node_id] = NodeContext(
                node_id=node_id,
                node_type=node_type
            )
        return self.node_contexts[node_id]
    
    def get_node_context(self, node_id: str) -> Optional[NodeContext]:
        """获取节点级上下文（不创建）"""
        return self.node_contexts.get(node_id)
```

---

### 3.2 阶段 2：修改 TraversalRuntimeContext（P1）

#### 文件: `src/trace/context.py`

```python
# 在 TraversalRuntimeContext 中添加:

@dataclass
class TraversalRuntimeContext:
    """Mutable runtime context used by the traversal engine."""
    
    # ... 现有字段保持不变 ...
    
    # V6.12: 分层上下文架构
    session_context: Optional["SessionContext"] = None
    
    # ========================================================================
    # 兼容性属性（逐步废弃）
    # ========================================================================
    
    @property
    def visited_children(self) -> Dict[str, Set[str]]:
        """兼容性：从 session_context 提取 visited_children
        
        警告：此属性仅为向后兼容，建议直接使用 session_context
        """
        if self.session_context:
            return {
                node_id: ctx.visited_child_ids
                for node_id, ctx in self.session_context.node_contexts.items()
            }
        return {}
    
    @visited_children.setter
    def visited_children(self, value: Dict[str, Set[str]]) -> None:
        """兼容性：设置 visited_children（迁移到 session_context）"""
        # 迁移期间，将旧数据同步到 session_context
        if self.session_context:
            from src.graph.node import NodeType
            for node_id, child_ids in value.items():
                node_ctx = self.session_context.get_or_create_node_context(
                    node_id, NodeType.SCREEN.value
                )
                node_ctx.visited_child_ids.update(child_ids)
```

---

### 3.3 阶段 3：修改 GraphTraversalEngine（P1）

#### 文件: `src/traversal/graph_engine.py`

```python
# 在 __init__ 中添加:

class GraphTraversalEngine:
    def __init__(
        self,
        plan: TraversalPlan,
        vision_service: Any,
        action_executor: Any,
        exception_chain: Optional[Any] = None,
        trace_recorder: Optional[TraceRecorder] = None,
        test_metadata: Optional[Dict[str, Any]] = None,
    ):
        # ... 现有代码 ...
        
        # V6.12: 初始化 SessionContext
        from src.traversal.layered_context import SessionContext
        self.context.session_context = SessionContext()
        
        # ... 现有代码 ...

# 在 initialize() 中添加:

def initialize(self) -> None:
    """Initialize the traversal engine."""
    # ... 现有代码 ...
    
    # V6.12: 为根节点创建 NodeContext
    if self.plan.root_node:
        root_ctx = self.context.session_context.get_or_create_node_context(
            self.plan.root_node.node_id,
            self.plan.root_node.node_type.value
        )
        root_ctx.record_visit()
```

---

### 3.4 阶段 4：修改 DynamicChildManager（P1）

#### 文件: `src/traversal/dynamic_child_manager.py`

```python
class DynamicChildManager:
    """Manages dynamic child node lifecycle."""
    
    def __init__(
        self,
        dynamic_matcher: Optional[DynamicMatcher],
        node_registry: Dict[str, TraversalNode],
        trace: Optional[Any] = None,
    ):
        self._dynamic_matcher = dynamic_matcher
        self._node_registry = node_registry
        self._trace = trace
        
        # V6.12: 移除 _generated_pairs，使用 PageLevelContext
        # self._generated_pairs: Set[tuple] = set()  # ← 删除
        
        # 保留 _dynamic_children 用于缓存生成的节点
        self._dynamic_children: Dict[str, List[TraversalNode]] = {}
    
    def get_next_unvisited_child(
        self, node: TraversalNode, context: TraversalRuntimeContext
    ) -> Optional[str]:
        """获取下一个未访问的子节点"""
        # V6.12: 使用 NodeContext 替代 visited_children
        if not context.session_context:
            # 兼容性：回退到旧方法
            return self._get_next_unvisited_child_legacy(node, context)
        
        node_ctx = context.session_context.get_or_create_node_context(
            node.node_id,
            node.node_type.value
        )
        
        strategy = node.children_strategy
        if not strategy:
            return None
        
        if strategy.type == ChildrenStrategyType.STATIC:
            # 确保 child_queue 已初始化
            if not node_ctx.child_queue:
                node_ctx.add_children(strategy.static_children)
            
            return node_ctx.get_next_unvisited_child()
        
        elif strategy.type == ChildrenStrategyType.DYNAMIC_MATCH:
            # 确保已生成动态子节点
            if node.node_id not in self._dynamic_children:
                self.generate(node, context)
            
            children = self._dynamic_children.get(node.node_id, [])
            if not node_ctx.child_queue:
                node_ctx.add_children([c.node_id for c in children])
            
            return node_ctx.get_next_unvisited_child()
        
        return None
    
    def _get_next_unvisited_child_legacy(
        self, node: TraversalNode, context: TraversalRuntimeContext
    ) -> Optional[str]:
        """兼容性：旧方法（逐步废弃）"""
        if node.node_id not in context.visited_children:
            context.visited_children[node.node_id] = set()
        
        visited = context.visited_children[node.node_id]
        strategy = node.children_strategy
        
        if not strategy:
            return None
        
        if strategy.type == ChildrenStrategyType.STATIC:
            for child_id in strategy.static_children:
                if child_id not in visited:
                    visited.add(child_id)
                    return child_id
            return None
        
        elif strategy.type == ChildrenStrategyType.DYNAMIC_MATCH:
            if node.node_id not in self._dynamic_children:
                self.generate(node, context)
            children = self._dynamic_children.get(node.node_id, [])
            for child in children:
                if child.node_id not in visited:
                    visited.add(child.node_id)
                    return child.node_id
            return None
        
        return None
    
    def has_unvisited(
        self, node: TraversalNode, context: TraversalRuntimeContext
    ) -> Optional[bool]:
        """检查是否有未访问的子节点"""
        # V6.12: 使用 NodeContext
        if context.session_context:
            node_ctx = context.session_context.get_or_create_node_context(
                node.node_id,
                node.node_type.value
            )
            return node_ctx.has_unvisited_children()
        
        # 兼容性：回退到旧方法
        return self._has_unvisited_legacy(node, context)
    
    def _has_unvisited_legacy(
        self, node: TraversalNode, context: TraversalRuntimeContext
    ) -> Optional[bool]:
        """兼容性：旧方法"""
        if not node.children_strategy:
            return False
        
        visited = context.visited_children.get(node.node_id, set())
        
        if node.children_strategy.type == ChildrenStrategyType.STATIC:
            for child_id in node.children_strategy.static_children:
                if child_id not in visited:
                    return True
            return False
        
        elif node.children_strategy.type == ChildrenStrategyType.DYNAMIC_MATCH:
            return self.get_next_unvisited_child_legacy(node, context) is not None
        
        return False
    
    def generate(
        self, node: TraversalNode, context: TraversalRuntimeContext
    ) -> None:
        """生成动态子节点"""
        if not self._dynamic_matcher:
            self._dynamic_children[node.node_id] = []
            return
        
        # V6.12: 使用 PageLevelContext 进行去重
        if not context.session_context:
            # 兼容性：回退到旧方法
            return self._generate_legacy(node, context)
        
        page_fp = PageSnapshotManager.fingerprint(context.current_page_analysis)
        page_ctx = context.session_context.get_or_create_page_context(page_fp)
        
        # ... 现有的规则转换和匹配逻辑 ...
        
        # 规则转换
        rules = {}
        if node.children_strategy and node.children_strategy.dynamic_rules:
            for rule_id, rule in node.children_strategy.dynamic_rules.items():
                # ... 规则转换逻辑 ...
                pass
        
        if rules:
            self._dynamic_matcher.load_rules(rules)
        
        # 提取页面元素
        items = []
        page_analysis = context.current_page_analysis
        if page_analysis and hasattr(page_analysis, "items"):
            # ... 元素提取逻辑 ...
            pass
        
        # 匹配和实例化
        results = self._dynamic_matcher.match_all(items, parent_node=node)
        children = []
        
        for r in results:
            if r.matched and r.action == MatchAction.GENERATE_CHILD:
                child = self._dynamic_matcher.instantiate_match(r)
                if child:
                    # V6.12: 使用 PageLevelContext 进行去重
                    pair = (node.node_id, child.name)
                    if pair in page_ctx.generated_dynamic_pairs:
                        # 已生成，跳过
                        if self._trace:
                            self._trace.record_skip_span(r)
                        continue
                    
                    # 记录生成
                    page_ctx.record_dynamic_generation(node.node_id, child.name)
                    
                    # 设置 precondition
                    if not child.precondition:
                        child.precondition = Precondition(page_name=None, timeout_seconds=5.0)
                    child.precondition.path = list(context.current_path) + [child.name]
                    
                    # 记录 trace
                    if self._trace:
                        self._trace.record_dynamic_lifecycle(
                            event="created",
                            node_id=child.node_id,
                            parent_id=node.node_id,
                            match_rule_id=getattr(r, "rule_id", None),
                            element_id=getattr(r, "element_id", None),
                        )
                    
                    self._node_registry[child.node_id] = child
                    children.append(child)
        
        self._dynamic_children[node.node_id] = children
    
    def _generate_legacy(
        self, node: TraversalNode, context: TraversalRuntimeContext
    ) -> None:
        """兼容性：旧的生成方法"""
        # ... 保留旧实现 ...
        pass
```

---

### 3.5 阶段 5：修改 StepOrchestrator（P1）

#### 文件: `src/traversal/step_orchestrator.py`

```python
class StepOrchestrator:
    """Executes one state machine step with engine-level gates."""
    
    def execute_step(self, ctx: StepContext) -> Dict[str, Any]:
        """执行单步"""
        # ... 现有代码 ...
        
        # V6.12: 记录节点访问
        if ctx.context.session_context:
            current_node = stack.peek()
            if current_node:
                node_ctx = ctx.context.session_context.get_or_create_node_context(
                    current_node.node_id,
                    current_node.node_type.value
                )
                node_ctx.record_visit()
        
        # ... 现有代码 ...
```

---

## 4. 完整集成流程图

```
┌─────────────────────────────────────────────────────────────────┐
│                     GraphTraversalEngine                         │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │            TraversalRuntimeContext                         │  │
│  │                                                            │  │
│  │  ┌────────────────────────────────────────────────────┐  │  │
│  │  │         SessionContext (V6.12 新增)                 │  │  │
│  │  │                                                     │  │  │
│  │  │  page_contexts: Dict[str, PageLevelContext]        │  │  │
│  │  │  node_contexts: Dict[str, NodeContext]             │  │  │
│  │  │                                                     │  │  │
│  │  │  get_or_create_page_context(fp)                     │  │  │
│  │  │  get_or_create_node_context(id, type)              │  │  │
│  │  └────────────────────────────────────────────────────┘  │  │
│  │                                                            │  │
│  │  node_stack, current_path, ... (现有字段)                 │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│  ┌────────────────────┐  ┌──────────────────────────────────┐  │
│  │ DynamicChildManager│  │      StepOrchestrator             │  │
│  │                    │  │                                  │  │
│  │ generate()         │  │ execute_step()                   │  │
│  │   ↓                │  │   ↓                              │  │
│  │ PageLevelContext   │  │ NodeContext                      │  │
│  │   .generated_      │  │   .visited_child_ids             │  │
│  │   dynamic_pairs    │  │                                  │  │
│  └────────────────────┘  └──────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 5. 测试策略

### 5.1 单元测试

```python
# tests/traversal/test_layered_context.py

def test_session_context_page_lifecycle():
    """测试 SessionContext 的页面上下文管理"""
    session = SessionContext()
    
    # 创建页面上下文
    ctx1 = session.get_or_create_page_context("fp1")
    assert ctx1.page_fingerprint == "fp1"
    
    # 重复获取返回同一实例
    ctx2 = session.get_or_create_page_context("fp1")
    assert ctx1 is ctx2
    
    # 不同指纹返回不同实例
    ctx3 = session.get_or_create_page_context("fp2")
    assert ctx1 is not ctx3


def test_page_context_dynamic_dedup():
    """测试 PageLevelContext 的 DYNAMIC 去重"""
    page_ctx = PageLevelContext(page_fingerprint="fp1")
    
    # 记录生成
    page_ctx.record_dynamic_generation("node1", "button1")
    
    # 检查已生成
    assert page_ctx.is_dynamic_generated("node1", "button1")
    assert not page_ctx.is_dynamic_generated("node1", "button2")


def test_node_context_child_management():
    """测试 NodeContext 的子节点管理"""
    node_ctx = NodeContext(node_id="node1", node_type="CONTAINER")
    
    # 添加子节点
    node_ctx.add_children(["child1", "child2", "child3"])
    
    # 检查是否有未访问
    assert node_ctx.has_unvisited_children()
    
    # 获取下一个
    next_child = node_ctx.get_next_unvisited_child()
    assert next_child == "child1"
    
    # 标记访问
    node_ctx.mark_child_visited("child1")
    assert node_ctx.get_next_unvisited_child() == "child2"


def test_ttl_cleanup():
    """测试 TTL 清理"""
    session = SessionContext(page_context_ttl=0)  # 立即过期
    
    # 创建页面上下文
    session.get_or_create_page_context("fp1")
    
    # 清理过期上下文
    session._cleanup_expired_page_contexts()
    
    # 应该被清理
    assert "fp1" not in session.page_contexts
```

### 5.2 集成测试

```python
# tests/traversal/test_layered_integration.py

def test_dynamic_generation_with_page_context(simulation_engine):
    """测试使用 PageLevelContext 的动态生成"""
    engine = simulation_engine
    
    # 运行遍历
    result = engine.run()
    
    # 验证 PageContext 被创建
    session_ctx = engine.context.session_context
    assert session_ctx is not None
    assert len(session_ctx.page_contexts) > 0
    
    # 验证去重工作
    page_ctx = list(session_ctx.page_contexts.values())[0]
    assert len(page_ctx.generated_dynamic_pairs) > 0


def test_node_context_tracking(simulation_engine):
    """测试 NodeContext 的访问跟踪"""
    engine = simulation_engine
    
    # 运行遍历
    result = engine.run()
    
    # 验证 NodeContext 被创建
    session_ctx = engine.context.session_context
    
    # 至少根节点应该有上下文
    assert engine.plan.root_node.node_id in session_ctx.node_contexts
    
    # 验证访问跟踪
    root_ctx = session_ctx.get_node_context(engine.plan.root_node.node_id)
    assert root_ctx is not None
    assert root_ctx.visit_count > 0
```

### 5.3 仿真测试

```bash
# 运行现有的仿真测试
pytest tests/simulation/ -v

# 验证：
# - 89 步 COMPLETED
# - 19 节点访问
# - DYNAMIC 去重正确
# - 滚动行为正常
```

---

## 6. 迁移路径

### 6.1 兼容性策略

| 阶段 | 内容 | 兼容性 |
|------|------|--------|
| **V6.12.0** | 引入 SessionContext，保留旧 API | 100% 兼容 |
| **V6.13.0** | 新功能使用新 API，旧 API 标记废弃 | 100% 兼容 |
| **V6.14.0** | 移除旧 API | 需要迁移 |

### 6.2 渐进式迁移

```python
# V6.12.0: 双写模式
def mark_child_visited(node_id: str, child_id: str):
    # 新方式
    if session_context:
        node_ctx = session_context.get_or_create_node_context(node_id, "SCREEN")
        node_ctx.mark_child_visited(child_id)
    
    # 旧方式（兼容）
    if node_id not in visited_children:
        visited_children[node_id] = set()
    visited_children[node_id].add(child_id)

# V6.13.0: 仅新方式 + 警告
def mark_child_visited(node_id: str, child_id: str):
    if not session_context:
        raise DeprecationWarning("session_context required")
    
    node_ctx = session_context.get_or_create_node_context(node_id, "SCREEN")
    node_ctx.mark_child_visited(child_id)

# V6.14.0: 仅新方式
def mark_child_visited(node_id: str, child_id: str):
    node_ctx = session_context.get_or_create_node_context(node_id, "SCREEN")
    node_ctx.mark_child_visited(child_id)
```

---

## 7. 实施检查清单

### P0: 基础类创建
- [ ] 创建 `src/traversal/layered_context.py`
- [ ] 实现 `SessionContext` 类
- [ ] 实现 `PageLevelContext` 类
- [ ] 实现 `NodeContext` 类
- [ ] 创建单元测试 `tests/traversal/test_layered_context.py`
- [ ] 验收：单元测试通过

### P1: Engine 集成
- [ ] 修改 `TraversalRuntimeContext` 添加 `session_context` 字段
- [ ] 修改 `GraphTraversalEngine.__init__()` 初始化 `SessionContext`
- [ ] 修改 `DynamicChildManager.generate()` 使用 `PageLevelContext`
- [ ] 修改 `DynamicChildManager.get_next_unvisited_child()` 使用 `NodeContext`
- [ ] 修改 `StepOrchestrator.execute_step()` 使用 `NodeContext`
- [ ] 添加兼容性属性 `visited_children`
- [ ] 验收：仿真测试通过

### P2: 滚动逻辑迁移
- [ ] 识别当前滚动逻辑位置
- [ ] 将滚动状态迁移到 `PageLevelContext`
- [ ] 修改滚动决策使用 `page_context.should_continue_scroll()`
- [ ] 验收：仿真测试通过

### P3: TTL 清理
- [ ] 实现 `SessionContext._cleanup_expired_page_contexts()`
- [ ] 实现 `SessionContext._evict_oldest_page_context()`
- [ ] 创建压力测试验证内存管理
- [ ] 验收：压力测试通过

### P4: 文档和清理
- [ ] 更新 `docs/architecture/ARCHITECTURE.md`
- [ ] 创建迁移指南
- [ ] 添加示例代码
- [ ] 运行全量测试
- [ ] 验收：全量测试通过，文档完整

---

## 8. 关键注意事项

### 8.1 数据一致性

在迁移期间，需要确保新旧数据同步：

```python
# 问题：新旧数据可能不同步
context.visited_children[node_id].add(child_id)  # 旧方式
# 如果这里出错，新方式不会更新
node_ctx.mark_child_visited(child_id)  # 新方式

# 解决：使用双写模式
def mark_child_visited_safe(node_id: str, child_id: str):
    # 先写新方式
    if session_context:
        node_ctx = session_context.get_or_create_node_context(node_id, "SCREEN")
        node_ctx.mark_child_visited(child_id)
    
    # 再写旧方式（兼容）
    if node_id not in visited_children:
        visited_children[node_id] = set()
    visited_children[node_id].add(child_id)
```

### 8.2 页面指纹一致性

确保 `PageLevelContext` 使用的页面指纹与 `DynamicChildManager` 一致：

```python
# DynamicChildManager 中
page_fp = PageSnapshotManager.fingerprint(context.current_page_analysis)

# SessionContext 中
page_ctx = session_context.get_or_create_page_context(page_fp)

# 必须使用相同的指纹方法
```

### 8.3 初始化顺序

确保 `SessionContext` 在使用前已初始化：

```python
# GraphTraversalEngine.__init__
self.context.session_context = SessionContext()  # 必须在使用前初始化

# 错误示例
self._child_mgr = DynamicChildManager(...)  # 如果这里使用 session_context 会出错
self.context.session_context = SessionContext()  # 太晚了

# 正确顺序
self.context.session_context = SessionContext()  # 先初始化
self._child_mgr = DynamicChildManager(...)  # 后使用
```

---

**文档所有者**: Uni-Claw 开发团队
**状态**: 实施指南
**相关文档**: 
- [PRD_V6_12_0_Layered_Context_Design.md](./PRD_V6_12_0_Layered_Context_Design.md) - 分层架构设计
- [PRD_V6_12_0_node_execution_context.md](./PRD_V6_12_0_node_execution_context.md) - 原设计
