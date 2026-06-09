# V6.12.0 分层上下文架构设计

> **版本**: V6.12.0-Alt
> **日期**: 2026-06-09
> **状态**: 设计提案（替代方案）
> **对比原设计**: [PRD_V6_12_0_node_execution_context.md](./PRD_V6_12_0_node_execution_context.md)

---

## 1. 执行摘要

本文档提出**分层上下文架构（Layered Context Architecture）**作为 V6.12.0 的替代实现方案。相比原设计的单一 `NodeExecutionContext` 上帝类，本方案将上下文分为三层，每层职责明确，解决了原设计中的关键架构问题。

### 核心改进

| 维度 | 原设计问题 | 分层上下文方案 |
|------|-----------|---------------|
| **职责分离** | 25+ 字段的上帝类 | 三层各司其职，每层 <10 字段 |
| **架构一致性** | DYNAMIC 去重是节点级（与实际不符） | 页面级去重（与实际一致） |
| **性能** | `get_parent_context()` O(n) 树遍历 | PageContext 直接访问 O(1) |
| **内存管理** | 无清理策略，可能 OOM | TTL 自动清理过期页面 |
| **滚动语义** | 每个节点独立滚动（不合理） | 页面级共享滚动状态 |
| **失效跟踪** | 节点级（同一元素重复失效） | 页面级（全局失效标记） |

---

## 2. 架构对比

### 2.1 原设计架构（PRD_V6_12_0）

```
TraversalRuntimeContext (全局)
├── context_tree: ContextTree
│   └── nodes: Dict[str, NodeExecutionContext]  <-- 所有节点状态
└── current_path: List[str]

NodeExecutionContext (单个节点)
├── node_id, node_type
├── child_queue, visited_child_ids, current_child_idx
├── scroll_state, scroll_position, scroll_attempts     <-- 每个节点独立滚动
├── page_fingerprint, page_element_cache               <-- 页面级数据放在节点
├── invalid_elements                                    <-- 节点级失效标记
├── visit_count, last_visit_time, first_visit_time
├── consecutive_errors, total_errors, last_error
├── retry_count
├── dynamic_children_generated, dynamic_generation_timestamp
└── meta: Dict[str, Any]
```

**问题分析**：
- ❌ 滚动状态是**页面行为**，不应属于单个节点
- ❌ 页面元素缓存在节点级，**同一页面不同节点重复缓存**
- ❌ 失效元素是**页面级事实**，节点级标记导致重复失效
- ❌ 25+ 字段违反单一职责原则

### 2.2 分层上下文架构（本文案）

```
SessionContext (全局 - Session 生命周期)
├── visited_pages: Dict[str, PageSummary]        # 页面访问摘要
├── page_contexts: Dict[str, PageLevelContext]   # 页面级上下文缓存
├── node_contexts: Dict[str, NodeContext]        # 节点级上下文缓存
└── global_stats: GlobalStats

PageLevelContext (页面级 - TTL 生命周期)
├── page_fingerprint: str
├── generated_dynamic_pairs: Set[Tuple[str, str]]  # 页面级 DYNAMIC 去重
├── scroll_state, scroll_position, scroll_attempts  # 页面级滚动
├── element_cache: Dict[str, Any]                   # 页面级元素缓存
├── invalid_elements: Set[str]                      # 页面级失效元素
├── last_access_time: float
└── ttl_seconds: int

NodeContext (节点级 - 瞬时生命周期)
├── node_id, node_type
├── child_queue: List[str]
├── visited_child_ids: Set[str]
├── current_child_idx: int
├── visit_count: int
├── consecutive_errors: int
└── last_error: Optional[Exception]
```

**优势**：
- ✅ 滚动状态在页面级，**符合语义**
- ✅ 元素缓存在页面级，**避免重复**
- ✅ 失效标记在页面级，**一次标记全局生效**
- ✅ 每层 <10 字段，**职责清晰**

---

## 3. 详细设计

### 3.1 SessionContext（全局）

```python
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Set, Tuple
from datetime import datetime
import time

@dataclass
class PageSummary:
    """页面访问摘要（持久化）"""
    page_fingerprint: str
    first_visit_time: float
    last_visit_time: float
    nodes_visited: Set[str]
    total_duration_seconds: float = 0.0


@dataclass
class GlobalStats:
    """全局统计"""
    total_nodes_visited: int = 0
    total_pages_visited: int = 0
    total_errors: int = 0
    start_time: float = field(default_factory=time.time)
    end_time: Optional[float] = None


@dataclass
class SessionContext:
    """一次遍历任务的全局上下文
    
    生命周期: 与 TraversalRuntimeContext 相同（Session 级别）
    职责:
    - 管理页面级和节点级上下文的缓存
    - 提供全局统计信息
    - 支持序列化和恢复
    """
    
    # === 页面状态 ===
    visited_pages: Dict[str, PageSummary] = field(default_factory=dict)
    """已访问页面的摘要信息"""
    
    # === 上下文缓存 ===
    page_contexts: Dict[str, PageLevelContext] = field(default_factory=dict)
    """页面级上下文缓存 key: page_fingerprint"""
    
    node_contexts: Dict[str, NodeContext] = field(default_factory=dict)
    """节点级上下文缓存 key: node_id"""
    
    # === 全局统计 ===
    stats: GlobalStats = field(default_factory=GlobalStats)
    
    # === 当前执行位置 ===
    current_path: List[str] = field(default_factory=list)
    """当前执行路径（节点 ID 列表）"""
    
    # === 配置 ===
    page_context_ttl: int = 3600  # 1小时
    max_page_contexts: int = 100  # 最多缓存 100 个页面
    
    # ========================================================================
    # 页面级上下文管理
    # ========================================================================
    
    def get_or_create_page_context(
        self, page_fingerprint: str
    ) -> PageLevelContext:
        """获取或创建页面级上下文
        
        Args:
            page_fingerprint: 页面指纹
            
        Returns:
            PageLevelContext: 页面级上下文
        """
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
        now = time.time()
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
        """获取或创建节点级上下文
        
        Args:
            node_id: 节点 ID
            node_type: 节点类型
            
        Returns:
            NodeContext: 节点级上下文
        """
        if node_id not in self.node_contexts:
            self.node_contexts[node_id] = NodeContext(
                node_id=node_id,
                node_type=node_type
            )
        return self.node_contexts[node_id]
    
    def get_node_context(self, node_id: str) -> Optional[NodeContext]:
        """获取节点级上下文（不创建）"""
        return self.node_contexts.get(node_id)
    
    # ========================================================================
    # 统计和查询
    # ========================================================================
    
    def record_page_visit(self, page_fingerprint: str, node_id: str) -> None:
        """记录页面访问"""
        now = time.time()
        
        if page_fingerprint not in self.visited_pages:
            self.visited_pages[page_fingerprint] = PageSummary(
                page_fingerprint=page_fingerprint,
                first_visit_time=now,
                last_visit_time=now,
                nodes_visited=set()
            )
            self.stats.total_pages_visited += 1
        
        summary = self.visited_pages[page_fingerprint]
        summary.last_visit_time = now
        summary.nodes_visited.add(node_id)
    
    def record_node_visit(self, node_id: str) -> None:
        """记录节点访问"""
        self.stats.total_nodes_visited += 1
    
    def get_session_summary(self) -> Dict[str, any]:
        """获取会话摘要"""
        return {
            "total_pages_visited": self.stats.total_pages_visited,
            "total_nodes_visited": self.stats.total_nodes_visited,
            "total_errors": self.stats.total_errors,
            "duration_seconds": (
                (self.stats.end_time or time.time()) - self.stats.start_time
            ),
            "active_page_contexts": len(self.page_contexts),
            "active_node_contexts": len(self.node_contexts),
        }
```

### 3.2 PageLevelContext（页面级）- **核心创新**

```python
from enum import Enum
from dataclasses import dataclass, field
from typing import Dict, Set, Tuple, Any, Optional
import time


class ScrollState(Enum):
    """滚动状态"""
    IDLE = "idle"
    SCROLLING = "scrolling"
    END_REACHED = "end_reached"
    ERROR = "error"


@dataclass
class PageLevelContext:
    """页面级上下文
    
    生命周期: TTL 管理（默认 1 小时）
    职责:
    - DYNAMIC 子节点生成的页面级去重
    - 滚动状态管理（页面级）
    - 页面元素缓存
    - 失效元素标记（页面级）
    
    关键设计决策:
    - 滚动是页面行为，不是节点行为
    - 失效元素是页面级事实（一个元素失效，在整个页面都失效）
    - DYNAMIC 去重应该是页面级（避免重复生成相同元素）
    """
    
    # === 页面标识 ===
    page_fingerprint: str
    """页面唯一标识"""
    
    # === DYNAMIC 去重 (页面级别) ===
    generated_dynamic_pairs: Set[Tuple[str, str]] = field(default_factory=set)
    """已生成的 (node_id, element_name) 对
    
    关键: 这是页面级别的去重，确保：
    - 同一页面不同节点不会重复生成相同元素
    - 符合实际的 DYNAMIC_MATCH 语义（页面级去重）
    """
    
    # === 滚动状态 (页面级别) ===
    scroll_state: ScrollState = ScrollState.IDLE
    """当前滚动状态"""
    
    scroll_position: float = 0.0
    """当前滚动位置（0-1）"""
    
    scroll_attempts: int = 0
    """滚动尝试次数"""
    
    max_scroll_attempts: int = 5
    """最大滚动次数限制"""
    
    # === 页面元素缓存 ===
    element_cache: Dict[str, Any] = field(default_factory=dict)
    """页面级别的元素缓存
    
    用途:
    - 缓存页面上的 AI 识别结果
    - 缓存页面上的布局信息
    - 避免重复分析同一页面
    """
    
    # === 失效元素 (页面级别) ===
    invalid_elements: Set[str] = field(default_factory=set)
    """在这个页面上失效的元素
    
    关键: 失效是页面级事实（点击无反应是元素的页面级属性）
    - 一次标记，整个页面生效
    - 避免在不同节点重复测试同一失效元素
    """
    
    # === TTL 管理 ===
    created_time: float = field(default_factory=time.time)
    """创建时间"""
    
    last_access_time: float = field(default_factory=time.time)
    """最后访问时间"""
    
    ttl_seconds: int = 3600
    """存活时间（秒）"""
    
    # === 统计 ===
    total_dynamic_generated: int = 0
    """在这个页面生成的 DYNAMIC 子节点总数"""
    
    # ========================================================================
    # DYNAMIC 管理
    # ========================================================================
    
    def is_dynamic_generated(
        self, node_id: str, element_name: str
    ) -> bool:
        """检查是否已生成该动态元素
        
        Args:
            node_id: 节点 ID
            element_name: 元素名称
            
        Returns:
            bool: True 如果已生成
        """
        return (node_id, element_name) in self.generated_dynamic_pairs
    
    def record_dynamic_generation(
        self, node_id: str, element_name: str
    ) -> None:
        """记录动态元素生成
        
        Args:
            node_id: 节点 ID
            element_name: 元素名称
        """
        self.generated_dynamic_pairs.add((node_id, element_name))
        self.total_dynamic_generated += 1
        self.last_access_time = time.time()
    
    def get_dynamic_count_for_node(self, node_id: str) -> int:
        """获取指定节点生成的动态元素数量
        
        Args:
            node_id: 节点 ID
            
        Returns:
            int: 动态元素数量
        """
        return sum(
            1 for (nid, _) in self.generated_dynamic_pairs
            if nid == node_id
        )
    
    # ========================================================================
    # 滚动管理
    # ========================================================================
    
    def should_continue_scroll(self) -> bool:
        """是否应该继续滚动
        
        Returns:
            bool: True 如果应该继续滚动
        """
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
    
    def mark_scroll_error(self) -> None:
        """标记滚动出错"""
        self.scroll_state = ScrollState.ERROR
        self.last_access_time = time.time()
    
    def reset_scroll_state(self) -> None:
        """重置滚动状态（用于新页面）"""
        self.scroll_state = ScrollState.IDLE
        self.scroll_position = 0.0
        self.scroll_attempts = 0
        self.last_access_time = time.time()
    
    # ========================================================================
    # 失效元素管理
    # ========================================================================
    
    def mark_element_invalid(self, element_name: str) -> None:
        """标记元素为失效
        
        Args:
            element_name: 元素名称
        """
        self.invalid_elements.add(element_name)
        self.last_access_time = time.time()
    
    def is_element_invalid(self, element_name: str) -> bool:
        """检查元素是否失效
        
        Args:
            element_name: 元素名称
            
        Returns:
            bool: True 如果元素已失效
        """
        return element_name in self.invalid_elements
    
    # ========================================================================
    # 元素缓存管理
    # ========================================================================
    
    def cache_element(self, element_name: str, data: Any) -> None:
        """缓存元素数据
        
        Args:
            element_name: 元素名称
            data: 元素数据
        """
        self.element_cache[element_name] = data
        self.last_access_time = time.time()
    
    def get_cached_element(self, element_name: str) -> Optional[Any]:
        """获取缓存的元素数据
        
        Args:
            element_name: 元素名称
            
        Returns:
            Optional[Any]: 元素数据，如果不存在则返回 None
        """
        return self.element_cache.get(element_name)
    
    def clear_element_cache(self) -> None:
        """清空元素缓存"""
        self.element_cache.clear()
        self.last_access_time = time.time()
    
    # ========================================================================
    # TTL 管理
    # ========================================================================
    
    def is_expired(self) -> bool:
        """检查是否过期
        
        Returns:
            bool: True 如果已过期
        """
        return time.time() - self.last_access_time > self.ttl_seconds
    
    def refresh_ttl(self) -> None:
        """刷新 TTL"""
        self.last_access_time = time.time()
    
    def get_age_seconds(self) -> float:
        """获取上下文年龄（秒）
        
        Returns:
            float: 自创建以来的秒数
        """
        return time.time() - self.created_time
    
    def get_idle_seconds(self) -> float:
        """获取空闲时间（秒）
        
        Returns:
            float: 自上次访问以来的秒数
        """
        return time.time() - self.last_access_time
    
    # ========================================================================
    # 序列化
    # ========================================================================
    
    def to_dict(self) -> Dict[str, any]:
        """序列化为字典"""
        return {
            "page_fingerprint": self.page_fingerprint,
            "dynamic_pairs_count": len(self.generated_dynamic_pairs),
            "scroll_state": self.scroll_state.value,
            "scroll_position": self.scroll_position,
            "scroll_attempts": self.scroll_attempts,
            "invalid_elements_count": len(self.invalid_elements),
            "element_cache_size": len(self.element_cache),
            "total_dynamic_generated": self.total_dynamic_generated,
            "age_seconds": self.get_age_seconds(),
            "idle_seconds": self.get_idle_seconds(),
        }
```

### 3.3 NodeContext（节点级）- **轻量简洁**

```python
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Set
from enum import Enum
import time


class NodeType(Enum):
    """节点类型"""
    SCREEN = "screen"
    CONTAINER = "container"
    ACTION = "action"


@dataclass
class NodeContext:
    """节点级上下文
    
    生命周期: 瞬时（不持久化）
    职责:
    - 子节点队列管理
    - 访问跟踪
    - 节点级错误计数
    
    设计原则:
    - 保持轻量（<10 字段）
    - 只包含节点执行必需的状态
    - 页面级状态交给 PageLevelContext
    """
    
    # === 节点标识 ===
    node_id: str
    node_type: str
    """节点类型（字符串形式，兼容性）"""
    
    # === 子节点管理 ===
    child_queue: List[str] = field(default_factory=list)
    """待访问子节点队列"""
    
    visited_child_ids: Set[str] = field(default_factory=set)
    """已访问的子节点 ID 集合"""
    
    current_child_idx: int = 0
    """当前子队列索引（用于 STATIC 顺序遍历）"""
    
    # === 执行统计（瞬时） ===
    visit_count: int = 0
    """该节点被访问的次数"""
    
    last_visit_time: Optional[float] = None
    """最后一次访问时间戳"""
    
    # === 错误和重试 ===
    consecutive_errors: int = 0
    """连续错误次数"""
    
    last_error: Optional[Exception] = None
    """最后一次错误"""
    
    # === 元数据 ===
    meta: Dict[str, any] = field(default_factory=dict)
    """其他节点特定的元数据"""
    
    # ========================================================================
    # 子节点管理
    # ========================================================================
    
    def has_unvisited_children(self) -> bool:
        """是否有未访问的子节点
        
        Returns:
            bool: True 如果有未访问的子节点
        """
        return len(self.visited_child_ids) < len(self.child_queue)
    
    def get_next_unvisited_child(self) -> Optional[str]:
        """获取下一个未访问的子节点
        
        Returns:
            Optional[str]: 下一个未访问的子节点 ID，如果没有则返回 None
        """
        for child_id in self.child_queue:
            if child_id not in self.visited_child_ids:
                return child_id
        return None
    
    def mark_child_visited(self, child_id: str) -> None:
        """标记子节点已访问
        
        Args:
            child_id: 子节点 ID
        """
        self.visited_child_ids.add(child_id)
    
    def add_child(self, child_id: str) -> None:
        """添加子节点到队列
        
        Args:
            child_id: 子节点 ID
        """
        self.child_queue.append(child_id)
    
    def get_completion_rate(self) -> float:
        """获取子节点完成率
        
        Returns:
            float: 完成率 (0-1)
        """
        if not self.child_queue:
            return 1.0
        return len(self.visited_child_ids) / len(self.child_queue)
    
    # ========================================================================
    # 访问管理
    # ========================================================================
    
    def record_visit(self) -> None:
        """记录一次节点访问"""
        self.visit_count += 1
        self.last_visit_time = time.time()
    
    def record_error(self, error: Exception) -> None:
        """记录一次错误
        
        Args:
            error: 错误对象
        """
        self.last_error = error
        self.consecutive_errors += 1
    
    def clear_errors(self) -> None:
        """清除错误计数（成功后调用）"""
        self.consecutive_errors = 0
        self.last_error = None
    
    # ========================================================================
    # 序列化
    # ========================================================================
    
    def to_dict(self) -> Dict[str, any]:
        """序列化为字典"""
        return {
            "node_id": self.node_id,
            "node_type": self.node_type,
            "total_children": len(self.child_queue),
            "visited_children": len(self.visited_child_ids),
            "completion_rate": self.get_completion_rate(),
            "visit_count": self.visit_count,
            "consecutive_errors": self.consecutive_errors,
            "last_visit_time": self.last_visit_time,
        }
```

---

## 4. 关键设计决策说明

### 4.1 为什么滚动应该是页面级？

**原设计问题**：每个 NodeExecutionContext 有独立的滚动状态

```python
# 原设计 - 每个节点独立滚动
for node in nodes:
    if node.node_context.scroll_state == END_REACHED:
        continue  # 这个节点滚动完了，但同页面的其他节点还要滚？
```

**分层设计**：滚动是页面行为

```python
# 分层设计 - 页面级滚动
page_context = session.get_page_context(page_fingerprint)
if page_context.should_continue_scroll():
    # 整个页面共享滚动状态
    perform_scroll()
```

**理由**：
- 滚动操作作用于**整个页面**，不是单个节点
- 页面滚动后，**所有节点**看到的是相同的视图
- 独立滚动状态会导致：节点 A 滚动到底 → 节点 B 重新从头滚动

### 4.2 为什么失效元素应该是页面级？

**原设计问题**：每个节点独立标记失效元素

```python
# 原设计 - 节点级失效
node_a_context.invalid_elements.add("submit_button")  # A 节点标记失效
# 但 B 节点不知道，会重复尝试点击 submit_button
```

**分层设计**：失效是页面级事实

```python
# 分层设计 - 页面级失效
page_context.mark_element_invalid("submit_button")
# 所有节点都知道这个元素失效，避免重复尝试
```

**理由**：
- 元素失效（点击无反应）是**页面级属性**
- 一个按钮失效，在整个页面都失效
- 避免在不同节点重复测试同一失效元素

### 4.3 为什么 DYNAMIC 去重应该是页面级？

**原设计问题**：去重是节点级

```python
# 原设计 - 节点级去重（实际不存在）
# NodeExecutionContext 没有跨节点的去重机制
# 导致：同一页面的不同节点可能生成相同的动态元素
```

**分层设计**：页面级去重

```python
# 分层设计 - 页面级去重
page_context.record_dynamic_generation(node_id, element_name)
# 确保：同一页面不会重复生成相同的 (node_id, element_name) 对
```

**理由**：
- DYNAMIC_MATCH 的实际语义是**页面级去重**
- 避免在同一页面重复生成相同的子节点
- 符合 V6.11 的实际行为（_generated_pairs 在 Engine 级别）

### 4.4 为什么需要 TTL？

**原设计问题**：无内存管理策略

```python
# 原设计 - 无清理机制
# 长时间运行后，context_tree.nodes 会积累所有访问过的节点
# 可能导致 OOM
```

**分层设计**：TTL 自动清理

```python
# 分层设计 - TTL 清理
# PageLevelContext 默认 1 小时 TTL
# 过期的页面上下文自动清理
# NodeContext 不持久化，用完即丢
```

**理由**：
- 长时间遍历任务可能访问数千页面
- 不清理的上下文会导致内存无限增长
- TTL 提供自动清理机制

---

## 5. 实施计划

### 5.1 阶段划分

| 阶段 | 内容 | 验收标准 | 工时 |
|------|------|----------|------|
| **P0** | 创建三层基础类 | 单元测试通过 | 3h |
| **P1** | 实现页面级 DYNAMIC 去重 | 仿真测试通过 | 2h |
| **P2** | 集成到 GraphTraversalEngine | 仿真测试通过 | 4h |
| **P3** | 实现 PageContext TTL 清理 | 压力测试通过 | 2h |
| **P4** | 全量测试和文档 | 全量测试通过 | 4h |

**总计**: 15 小时

### 5.2 详细任务清单

#### T1: 创建基础类 (3h)

- [ ] 创建 `src/traversal/layered_context.py`
  - `SessionContext` 类
  - `PageLevelContext` 类
  - `NodeContext` 类
  - `PageSummary` 类
  - `GlobalStats` 类
- [ ] 创建单元测试 `tests/traversal/test_layered_context.py`
- [ ] 验收：`pytest tests/traversal/test_layered_context.py -v` 通过

#### T2: 实现页面级 DYNAMIC 去重 (2h)

- [ ] 在 `DynamicChildManager` 中集成 PageLevelContext
- [ ] 修改 `generate_dynamic_children()` 使用 `page_context.is_dynamic_generated()`
- [ ] 修改 `generate_dynamic_children()` 调用 `page_context.record_dynamic_generation()`
- [ ] 验收：仿真测试通过，DYNAMIC 去重正确

#### T3: 集成到 GraphTraversalEngine (4h)

- [ ] 在 `TraversalRuntimeContext` 中添加 `session_context: SessionContext` 字段
- [ ] 修改 `StepOrchestrator` 使用 `session_context`
- [ ] 修改滚动逻辑使用 `page_context`
- [ ] 修改失效元素标记使用 `page_context`
- [ ] 验收：仿真测试通过（89 步 COMPLETED）

#### T4: 实现 PageContext TTL 清理 (2h)

- [ ] 实现 `SessionContext._cleanup_expired_page_contexts()`
- [ ] 实现 `SessionContext._evict_oldest_page_context()`
- [ ] 创建压力测试 `tests/traversal/test_context_ttl.py`
- [ ] 验收：压力测试通过，内存使用稳定

#### T5: 全量测试和文档 (4h)

- [ ] 运行全量测试 `pytest tests/ -v`
- [ ] 创建设计文档 `docs/architecture/modules/layered-context-design.md`
- [ ] 更新 `docs/architecture/ARCHITECTURE.md`
- [ ] 验收：全量测试通过，文档完整

### 5.3 风险和缓解

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| **架构复杂度** | 新增概念可能增加理解成本 | 完善文档，提供迁移指南 |
| **仿真测试失败** | 新架构可能有兼容性问题 | 每个阶段后运行仿真测试 |
| **TTL 配置不当** | 过短导致上下文丢失，过长导致内存增长 | 提供可配置 TTL，默认 1 小时 |
| **迁移成本** | 现有代码需要大量修改 | 提供兼容层，渐进式迁移 |

---

## 6. 成功标准

### 6.1 功能验收

- ✅ 仿真测试通过（89 步 COMPLETED，19 节点，6 菜单 + 二级）
- ✅ 页面级 DYNAMIC 去重正确工作
- ✅ 页面级滚动状态正确共享
- ✅ 页面级失效元素正确标记
- ✅ TTL 清理机制正确工作
- ✅ 所有现有功能不退化

### 6.2 性能验收

- ✅ 仿真测试执行时间不增加（±5%）
- ✅ 内存使用不超过原设计的 120%
- ✅ 压力测试（1000+ 页面）内存使用稳定

### 6.3 代码质量

- ✅ 通过 `mypy strict` 类型检查
- ✅ 通过 `ruff` linting（零警告）
- ✅ 单元测试覆盖率 > 90%

---

## 7. 与原设计的直接对比

### 7.1 代码复杂度对比

| 指标 | 原设计 | 分层设计 | 改善 |
|------|-------|---------|------|
| **最大类字段数** | 25+ | <10 | -60% |
| **类数量** | 2 (NodeExecutionContext, ContextTree) | 3 (Session, Page, Node) | +1 |
| **总代码行数** | ~600 | ~800 | +33% |
| **职责清晰度** | 低（上帝类） | 高（分层清晰） | ✅ |
| **语义正确性** | 中（滚动/失效在错误层级） | 高（符合语义） | ✅ |

### 7.2 性能对比

| 操作 | 原设计 | 分层设计 | 说明 |
|------|-------|---------|------|
| **获取页面上下文** | O(n) 遍历树 | O(1) 字典查找 | ✅ 改善 |
| **检查 DYNAMIC 重复** | 不支持 | O(1) 集合查找 | ✅ 新功能 |
| **滚动状态查询** | O(n) 遍历子节点 | O(1) 直接访问 | ✅ 改善 |
| **失效元素检查** | O(n) 遍历节点 | O(1) 集合查找 | ✅ 改善 |
| **内存清理** | 无策略 | TTL 自动清理 | ✅ 改善 |

### 7.3 语义正确性对比

| 场景 | 原设计行为 | 分层设计行为 | 哪个更合理 |
|------|-----------|-------------|-----------|
| **页面滚动** | 每个节点独立滚动 | 页面级共享滚动 | 分层设计 ✅ |
| **元素失效** | 每个节点独立标记 | 页面级共享标记 | 分层设计 ✅ |
| **DYNAMIC 去重** | 不支持跨节点去重 | 页面级去重 | 分层设计 ✅ |
| **页面缓存** | 节点级（重复缓存） | 页面级（共享缓存） | 分层设计 ✅ |

---

## 8. 迁移策略

### 8.1 兼容性考虑

为减少迁移风险，建议提供兼容层：

```python
@dataclass
class TraversalRuntimeContext:
    """兼容性包装"""
    
    # 新架构
    session_context: SessionContext = field(default_factory=SessionContext)
    
    # 兼容性属性
    @property
    def visited_children(self) -> Dict[str, Set[str]]:
        """兼容性：从 session_context 提取 visited_children"""
        return {
            node_id: ctx.visited_child_ids
            for node_id, ctx in self.session_context.node_contexts.items()
        }
```

### 8.2 渐进式迁移

| 阶段 | 迁移内容 | 保留兼容性 |
|------|---------|-----------|
| **V6.12.0** | 引入三层架构 | 保留 `visited_children` 兼容层 |
| **V6.13.0** | 移除大部分兼容层 | 保留关键兼容性属性 |
| **V6.14.0** | 完全移除兼容层 | - |

---

## 9. 未来扩展

基于分层架构的未来功能：

1. **智能滚动策略**：根据 PageContext 的错误率调整滚动行为
2. **页面级缓存预热**：在首次访问时缓存整个页面的 AI 分析结果
3. **跨页面状态传递**：通过 SessionContext 传递页面间的状态
4. **页面级重试策略**：每个页面可以有独立的重试配置
5. **内存使用监控**：实时监控 SessionContext 的内存使用，主动清理

---

## 10. 总结

### 10.1 为什么分层设计更好？

1. **语义正确性**：滚动、失效、缓存都是页面级行为
2. **性能优化**：O(1) 页面上下文访问 vs O(n) 树遍历
3. **内存安全**：TTL 自动清理 vs 无限制增长
4. **职责清晰**：三层各司其职 vs 单一上帝类
5. **可扩展性**：每层可独立扩展 vs 修改单一类

### 10.2 实施建议

- ✅ **推荐采用分层设计**作为 V6.12.0 的实现方案
- ⚠️ 需要额外的 5.5 小时（15h vs 9.5h）
- ✅ 投资回报率高：更好的架构、更正确的语义、更安全的内存

### 10.3 决策建议

| 场景 | 建议 |
|------|------|
| **追求架构正确性** | 选择分层设计 |
| **追求快速实施** | 选择原设计（需修复必须修改的问题） |
| **长期维护项目** | 选择分层设计 |
| **一次性原型** | 选择原设计 |

---

**文档所有者**: Uni-Claw 开发团队
**状态**: 设计提案
**相关文档**: 
- [PRD_V6_12_0_node_execution_context.md](./PRD_V6_12_0_node_execution_context.md) - 原设计
- [graph-engine-design.md](../architecture/modules/graph-engine-design.md) - 引擎设计
- [traversal-design.md](../architecture/modules/traversal-design.md) - 遍历设计
