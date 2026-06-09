# V6.13.0 Legacy State Module Migration

> **版本**: V6.13.0
> **日期**: 2026-06-09
> **依赖**: V6.12.0
> **状态**: 设计阶段
> **修订**: 1.8 (基于第八轮对抗审阅更新 - 修正 TraversalState 命名冲突，重命名为 SimulationState)

---

## 1. 背景

### 1.1 当前问题

`/src/state` 目录包含遗留的状态管理代码，存在以下问题：

| 问题 | 描述 | 影响 |
|------|------|------|
| **职责不清** | 混合了仿真数据模型、持久化逻辑和内容树模型 | 违反单一职责 |
| **依赖关系混乱** | 仿真测试和集成测试依赖遗留代码 | 架构不清晰 |
| **文档缺失** | 没有明确标记为遗留 | 新开发者可能误用 |
| **技术债务积累** | 未被 V6 使用的代码未被清理 | 维护负担 |

> **重要澄清 (V6.13 v1.7)**: `TraversalState` (BaseModel) **不能简单删除**。代码分析显示集成测试实际使用其方法（如 `add_level1_menu`, `get_level2_menus`, `add_items` 等）。本 PRD 提供两种处理方案：
> - **方案 A (推荐)**: 保留 `TraversalState` 在 `src.models.content_models`，标记为"仅用于仿真和集成测试"
> - **方案 B**: 提供功能到 `TraversalRuntimeContext` 的迁移路径（需额外 5-8h）

> **说明**: `TraversalState` 在 `src.state_machine` 是 Enum（FSM 状态），在 `src.state` 是 BaseModel（运行时状态），两者服务于不同目的。这不是"命名混淆"问题，而是职责混合问题。

### 1.1.1 TraversalState 命名冲突分析 (V6.13 v1.8 新增)

**问题发现**：第八轮审阅发现 `TraversalState` 存在双重定义导致的命名冲突：

| 位置 | 类型 | 用途 | 字段/值 |
|------|------|------|---------|
| `src/state_machine/traversal_fsm.py:131` | **Enum** | FSM 状态定义 | NODE_SELECT, BRANCH, ERROR_HANDLING, POPUP_HANDLER, END |
| `src/state/content_tree.py:428` | **BaseModel** | 运行时状态容器 | 13 字段（见下表） |

**命名冲突风险**：
```python
# 潜在混淆场景
from src.state_machine import TraversalState  # Enum
from src.state import TraversalState          # BaseModel
# 如果同时导入，后者会覆盖前者！

# 类型检查时的歧义
state: TraversalState  # 这是 Enum 还是 BaseModel？
```

**v1.8 解决方案：重命名 BaseModel 为 SimulationState**

将 `TraversalState` (BaseModel) 重命名为 `SimulationState`，原因：
1. **语义清晰**：该类用于仿真和集成测试的运行时状态，不是 FSM 状态
2. **消除歧义**：避免与 `TraversalState` (Enum) 的命名冲突
3. **职责明确**：`SimulationState` 专用于仿真场景，生产代码使用 `TraversalRuntimeContext`

**迁移影响**：
- v1.7 PRD 中的 `TraversalState` (BaseModel) → v1.8 PRD 中的 `SimulationState`
- v1.7 的 15 字段 → v1.8 的 13 字段（见下节修正）
- `src/models/__init__.py` 需要导出 `SimulationState` 和向后兼容别名

### 1.2 当前使用情况

```
/src/state/
├── state_manager.py         # ← 遗留：未被 V6 使用
└── content_tree.py          # ← 被多处使用
    ├── Coordinate           #   被仿真使用 (3 文件)
    ├── Direction            #   被仿真使用 (3 文件)
    ├── MenuInfo             #   被仿真使用 (3 文件)
    ├── MenuItem             #   被仿真使用 (3 文件)
    ├── MenuItemType         #   被仿真使用 (3 文件)
    ├── ExpectedAction       #   被仿真使用 (3 文件)
    ├── PageAnalysis         #   被仿真使用 (4 文件)
    ├── PopupInfo            #   被仿真使用 (3 文件)
    ├── TraversalState       #   TYPE_CHECKING 导入 (1 文件) + 集成测试使用方法
    ├── SimulationState      #   新命名（v1.8）：原 TraversalState (BaseModel)，消除命名冲突
    ├── ContentTree          #   被集成测试使用 (2 文件)
    ├── ContentNode          #   被集成测试使用 (2 文件)
    └── VisitFingerprint     #   被集成测试使用 (2 文件)

使用方（共 33 个文件）：
├── Simulation Mocks (3 文件)          → PageAnalysis 等模型
├── Exception Tests (3 文件)           → TraversalState (TYPE_CHECKING)
├── Integration Tests (2 文件)         → ContentTree, ContentNode, VisitFingerprint
├── Other Tests (12 文件)              → 各种模型
├── AI Integration Tests (1 文件)      → PageAnalysis
├── Model Tests (12 文件)              → 各种模型
└── V6 Engine                           → 不使用（用 TraversalRuntimeContext）

总计: 33 个文件涉及导入 (16 src + 17 tests + 2 tests/integration)
```

### 1.3 关键依赖问题

**`src/exception/context.py` 的 TYPE_CHECKING 导入**：

```python
# src/exception/context.py
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from src.state.content_tree import TraversalState
```

这是一个特殊的导入方式，**仅在静态类型检查时生效**（mypy），**不会在运行时执行**。删除 `src.state` 会导致：
- 类型检查失败（mypy 错误）
- **不会**造成运行时问题（TYPE_CHECKING 在运行时被跳过）

**实际风险**：mypy 静态类型检查失败，而非运行时崩溃。

**⚠️ SimulationState 运行时依赖 (V6.13 v1.8 修正)**：

> **v1.8 重大变更**: 将 `TraversalState` (BaseModel) 重命名为 `SimulationState`，消除与 `TraversalState` (Enum) 的命名冲突。

除了 TYPE_CHECKING，代码分析显示集成测试**运行时使用 SimulationState 的方法**：

```python
# SimulationState (原 TraversalState BaseModel) 的字段和方法：
class SimulationState(BaseModel):
    # 字段（13 个，含 2 个带 alias 的字段）
    current_path: list[str] = Field(default_factory=list)
    visited: set[str] = Field(default_factory=set)
    all_level1_menus: dict[str, MenuInfo] = Field(default_factory=dict)
    level2_menus_cache: dict[str, list[MenuInfo]] = Field(default_factory=dict)
    items_cache: dict[str, list[MenuItem]] = Field(default_factory=dict)
    content_tree: ContentTree = Field(default_factory=ContentTree)
    step_count: int = 0
    current_phase: str = "initialized"
    consecutive_errors: int = 0
    last_error: Optional[str] = None
    target_app: Optional[str] = None
    exception_history_records: list[dict] = Field(default_factory=list, alias="_exception_history_records")  # 带别名
    node_stack: list[dict] = Field(default_factory=list, alias="_node_stack")  # 带别名
    current_node_id: Optional[str] = None
    use_graph_mode: bool = False
    
    # 方法（8 个）- 集成测试调用
    def add_level1_menu(self, name: str, menu: MenuInfo) -> None
    def get_level2_menus(self, level1: str) -> list[MenuInfo]
    def add_items(self, level1: str, level2: str, items: list[MenuItem]) -> None
    def get_current_cache_key(self) -> str
    def is_visited(self, fingerprint: str) -> bool
    def mark_visited(self, fingerprint: str) -> None
    def get_exception_history_summary(self) -> dict
    def get_exceptions_by_type(self, exception_type: str) -> list[dict]
```

**v1.8 字段修正说明**：
- v1.7 PRD 声称有 **15 个字段**，但实际代码只有 **13 个字段**
- 其中 2 个字段带 `alias`：`_exception_history_records` 和 `_node_stack`
- 这 2 个别名用于 JSON 序列化兼容性

**影响**：不能简单删除 SimulationState，需要保留或提供迁移方案。

### 1.4 根本原因

V6 架构重构后，`/src/state` 没有及时清理：
- V6 使用 `TraversalRuntimeContext` 替代了运行时状态管理
- 仿真测试仍然使用旧的 `PageAnalysis` 模型
- 集成测试使用 `ContentTree`/`ContentNode`/`VisitFingerprint`
- 异常处理通过 TYPE_CHECKING 依赖旧模型
- 没有迁移计划

---

## 2. 解决方案概述

### 2.1 核心方案

**单文件迁移 + 两阶段过渡**：将所有内容模型移到 `src/models/content_models.py`，分两阶段完成迁移

| 阶段 | 版本 | 内容 | 风险 |
|------|------|------|------|
| **P0** | V6.13.0 | 创建新模型文件，包含所有 11 个类 | 低 |
| **P1** | V6.13.0 | 更新仿真测试导入 | 中 |
| **P2** | V6.13.0 | 处理 TYPE_CHECKING 依赖 | 中 |
| **P3** | V6.13.0 | 更新单元测试导入 | 中 |
| **P4** | V6.13.0 | 更新集成测试导入 | 中 |
| **P5** | V6.14.0 | 删除 `/src/state` 目录 | 低 |
| **P6** | V6.14.0 | 验证所有测试通过 | 中 |

### 2.2 目标架构

```
迁移前:
/src/state/
├── content_tree.py      # 550 行：混合数据模型 + 遗留状态
└── state_manager.py     # 140 行：遗留状态管理

迁移后:
/src/models/
└── content_models.py    # ~410 行：所有内容模型
    ├── Coordinate       #   坐标模型
    ├── Direction        #   方向枚举 + helper methods
    ├── MenuInfo         #   菜单信息
    ├── MenuItem         #   菜单项 + helper methods
    ├── MenuItemType     #   菜单项类型 + helper methods
    ├── ExpectedAction   #   预期行为 + helper methods
    ├── PageAnalysis     #   页面分析
    ├── PopupInfo        #   弹窗信息
    ├── ContentTree      #   内容树（集成测试使用）
    ├── ContentNode      #   内容节点（集成测试使用）
    └── VisitFingerprint #   访问指纹（集成测试使用）

/src/state/ (V6.14.0 删除)
```

#### src/models/__init__.py 修改

**修改策略**: 使用追加模式（保留 vision 导出），而非重写。

需要添加 `content_models` 的公共类导出，保持 API 一致性：

```python
# src/models/__init__.py (V6.13.0)

# ============================================================
# Vision models (existing) - 保留不变
# ============================================================
from src.models.vision import (
    BoundingBox,
    FlattenedElement,
    FlattenedScreen,
    Region,
    ScreenHints,
    SelectionState,
    TypeHint,
)

# ============================================================
# Content models (V6.13) - 新增追加
# ============================================================
from src.models.content_models import (
    Coordinate,
    Direction,
    MenuInfo,
    MenuItem,
    MenuItemType,
    ExpectedAction,
    PageAnalysis,
    PopupInfo,
    ContentTree,
    ContentNode,
    VisitFingerprint,
    SimulationState,  # v1.8: 重命名自 TraversalState (BaseModel)
)

# 向后兼容别名（v1.8 新增）
# 允许旧代码通过 TraversalState 别名访问 SimulationState
TraversalState = SimulationState

__all__ = [
    # Vision models
    "BoundingBox",
    "FlattenedElement",
    "FlattenedScreen",
    "Region",
    "ScreenHints",
    "SelectionState",
    "TypeHint",
    # Content models
    "Coordinate",
    "Direction",
    "MenuInfo",
    "MenuItem",
    "MenuItemType",
    "ExpectedAction",
    "PageAnalysis",
    "PopupInfo",
    "ContentTree",
    "ContentNode",
    "VisitFingerprint",
    "SimulationState",
    "TraversalState",  # 向后兼容别名
]
```

**注意**: 
- 使用追加模式，不修改现有的 vision 模型导出
- 保持与 `src/state/__init__.py` 的导出一致性，便于迁移

### 2.3 为什么选择单文件？

> **验证结果**: `src/models/vision/` 使用 **多文件结构**（7 个独立文件，共 788 行）：
> - `bounding_box.py` (108 行)
> - `flattened_element.py` (110 行)
> - `flattened_screen.py` (113 行)
> - `region.py` (74 行)
> - `screen_hints.py` (71 行)
> - `selection_state.py` (90 行)
> - `type_hint.py` (101 行)

**设计决策不一致性说明**：
本 PRD 选择 **单文件方案** (`src/models/content_models.py`) 与 `vision/` 的多文件模式 **不一致**。

**不一致的原因（为什么仍选择单文件）**：
1. **模型内聚性**：content_tree.py 的 11 个类紧密关联（Coordinate → MenuItem → PageAnalysis → ContentTree），适合单文件
2. **迁移成本**：单文件移动更易验证和测试，多文件拆分需要额外设计模块依赖关系
3. **代码量适中**：410 行代码（迁移后）单文件可维护，vision/ 的 788 行多文件更合理
4. **使用模式**：content 模型主要用于测试/仿真，vision 模型用于生产 AI 分析，复杂度不同

**未来考虑**：
如果 `content_models.py` 增长到 600+ 行，可在 V7.0 拆分为多文件结构（参考 vision/ 模式）。

---

## 3. 详细设计

### 3.1 单文件模型迁移

#### A. 完整模型定义 → `src/models/content_models.py`

```python
"""
Content models for simulation and testing.

Moved from src/state.content_tree in V6.13.0.

Contains 11 classes:
- Coordinate, Direction, MenuInfo, MenuItem, MenuItemType, ExpectedAction
- PageAnalysis, PopupInfo
- ContentTree, ContentNode, VisitFingerprint (integration test support)
"""

from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Dict, List, Optional, Set
from pydantic import BaseModel, Field


# ============================================================================
# Coordinate
# ============================================================================

class Coordinate(BaseModel):
    """Normalized coordinate (0-1)."""
    x: float = Field(ge=0.0, le=1.0, description="X coordinate (normalized 0-1)")
    y: float = Field(ge=0.0, le=1.0, description="Y coordinate (normalized 0-1)")


# ============================================================================
# Direction
# ============================================================================

class Direction(str, Enum):
    """Menu direction enumeration."""
    LEFT = "left"
    RIGHT = "right"
    TOP = "top"
    BOTTOM = "bottom"

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings."""
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "Direction":
        """Create an enum instance from a string value."""
        try:
            return cls(value)
        except ValueError:
            raise ValueError(f"Invalid Direction: {value}. Valid: {cls.values()}")

    @classmethod
    def is_valid(cls, value: str) -> bool:
        """Check if a string value is valid."""
        return value in cls.values()


# ============================================================================
# Menu Models
# ============================================================================

class MenuInfo(BaseModel):
    """Information about a menu item."""
    name: str
    coordinate: Coordinate
    active: bool = False


class MenuItemType(str, Enum):
    """Type of menu item with behavior classification."""
    # Navigation types
    MENU_ITEM = "menu_item"
    TAB = "tab"
    BACK_BUTTON = "back_button"
    # Action types
    SWITCH = "switch"
    TOGGLE = "toggle"
    BUTTON = "button"
    # Other types
    ICON = "icon"
    LINK = "link"
    TEXT = "text"
    READONLY = "readonly"
    # Legacy
    ITEM = "item"

    @classmethod
    def values(cls) -> list[str]:
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "MenuItemType":
        try:
            return cls(value)
        except ValueError:
            raise ValueError(f"Invalid MenuItemType: {value}. Valid: {cls.values()}")

    @classmethod
    def is_valid(cls, value: str) -> bool:
        return value in cls.values()


class ExpectedAction(str, Enum):
    """Expected button behavior/action type."""
    NAVIGATE = "navigate"
    TOGGLE = "toggle"
    ACTION = "action"
    NONE = "none"

    @classmethod
    def values(cls) -> list[str]:
        return [e.value for e in cls]


class MenuItem(BaseModel):
    """A clickable item on the screen."""
    name: str
    type: MenuItemType = Field(default=MenuItemType.ITEM)
    coordinate: Coordinate
    parent: Optional[str] = None
    description: Optional[str] = None
    expected_action: ExpectedAction = Field(default=ExpectedAction.ACTION)
    expects_page_change: bool = Field(default=False)
    expects_state_change: bool = Field(default=False)

    class Config:
        use_enum_values = True

    def get_fingerprint(self, level1: str, level2: str) -> str:
        """Generate unique fingerprint for this item."""
        return f"{level1}|{level2}|{self.name}"


# ============================================================================
# Page Analysis
# ============================================================================

class PopupInfo(BaseModel):
    """Information about a popup."""
    title: Optional[str] = None
    content: Optional[str] = None
    close_button: Optional[Coordinate] = None


class PageAnalysis(BaseModel):
    """Complete analysis of a screen page."""
    level1_dir: Direction
    level1_menus: list[MenuInfo]
    level2_dir: Direction
    level2_menus: list[MenuInfo]
    current_path: list[str]
    items: list[MenuItem]
    is_popup: bool = False
    popup_info: Optional[PopupInfo] = None
    close_button: Optional[Coordinate] = None
    back_button: Optional[Coordinate] = None
    has_scroll: bool = False
    is_end_of_list: bool = False


# ============================================================================
# Content Tree (Integration Test Support)
# ============================================================================

class VisitFingerprint(BaseModel):
    """Fingerprint for tracking visited elements."""
    level1: str
    level2: str
    item_name: str

    def __str__(self) -> str:
        """String representation for set membership."""
        return f"{self.level1}|{self.level2}|{self.item_name}"

    @classmethod
    def from_string(cls, value: str) -> "VisitFingerprint":
        """Create from string."""
        parts = value.split("|")
        if len(parts) != 3:
            raise ValueError(f"Invalid fingerprint format: {value}")
        return cls(level1=parts[0], level2=parts[1], item_name=parts[2])


class ContentNode(BaseModel):
    """A node in the content tree (used by integration tests)."""
    id: str
    title: str
    level: int
    parent_id: Optional[str] = None
    children: list[str] = Field(default_factory=list)
    coordinate: Optional[Coordinate] = None
    node_type: str = "item"
    description: Optional[str] = None
    visited: bool = False

    def to_markdown(
        self,
        include_children: bool = True,
        tree: "ContentTree" = None,
        visited: Optional[set[str]] = None,
        max_depth: int = 1000
    ) -> str:
        """Convert to markdown representation with cycle detection and depth limit.

        Args:
            include_children: If True, recursively include child nodes (requires tree parameter)
            tree: ContentTree instance for recursive child rendering
            visited: Set of visited node IDs for cycle detection (internal use)
            max_depth: Maximum recursion depth to prevent stack overflow

        Returns:
            Markdown string representation
        """
        # Initialize visited set on first call
        if visited is None:
            visited = set()
        
        # Cycle detection
        if self.id in visited:
            return f"  [cyclic reference: {self.id}]\n"
        visited.add(self.id)
        
        # Depth limit check
        if self.level > max_depth:
            return f"  [max depth reached: {self.id}]\n"
        
        indent = "  " * (self.level - 1)
        type_suffix = f" ({self.node_type})" if self.node_type != "item" else ""
        line = f"{indent}{self.id}. {self.title}{type_suffix}\n"

        if include_children and tree:
            for child_id in self.children:
                if child_id in tree.nodes:
                    child = tree.nodes[child_id]
                    line += child.to_markdown(
                        include_children=True,
                        tree=tree,
                        visited=visited.copy(),  # Copy for each branch to allow diamond structures
                        max_depth=max_depth
                    )

        return line


class ContentTree(BaseModel):
    """Tree structure of discovered content (used by integration tests)."""
    root_title: str = "Root"
    nodes: dict[str, ContentNode] = Field(default_factory=dict)
    level_counters: dict[int, int] = Field(default_factory=dict, alias="_level_counters")

    def add_node(
        self,
        title: str,
        level: int,
        parent_id: Optional[str] = None,
        node_type: str = "item",
        coordinate: Optional[Coordinate] = None,
        description: Optional[str] = None,
    ) -> ContentNode:
        """Add a new node to the tree."""
        node_id = self._generate_id(level)
        node = ContentNode(
            id=node_id,
            title=title,
            level=level,
            parent_id=parent_id,
            node_type=node_type,
            coordinate=coordinate,
            description=description,
        )
        self.nodes[node_id] = node
        if parent_id and parent_id in self.nodes:
            self.nodes[parent_id].children.append(node_id)
        return node

    def _generate_id(self, level: int) -> str:
        """Generate a hierarchical ID based on level."""
        if level not in self.level_counters:
            self.level_counters[level] = 0
        self.level_counters[level] += 1
        return str(self.level_counters[level])

    def add_child_node(
        self,
        title: str,
        parent_id: str,
        node_type: str = "item",
        coordinate: Optional[Coordinate] = None,
        description: Optional[str] = None,
    ) -> Optional[ContentNode]:
        """Add a child node with automatic ID generation."""
        if parent_id not in self.nodes:
            return None
        parent = self.nodes[parent_id]
        child_level = parent.level + 1
        if not parent.children:
            child_id = f"{parent.id}.1"
        else:
            last_child_id = parent.children[-1]
            last_number = int(last_child_id.split(".")[-1])
            child_id = f"{parent.id}.{last_number + 1}"
        node = ContentNode(
            id=child_id,
            title=title,
            level=child_level,
            parent_id=parent_id,
            node_type=node_type,
            coordinate=coordinate,
            description=description,
        )
        self.nodes[child_id] = node
        parent.children.append(child_id)
        return node

    def mark_visited(self, node_id: str) -> None:
        """Mark a node as visited."""
        if node_id in self.nodes:
            self.nodes[node_id].visited = True

    def get_unvisited_children(self, node_id: str) -> list[ContentNode]:
        """Get unvisited children of a node."""
        if node_id not in self.nodes:
            return []
        return [
            self.nodes[child_id]
            for child_id in self.nodes[node_id].children
            if not self.nodes[child_id].visited
        ]

    def to_markdown(self) -> str:
        """Export the entire tree as markdown."""
        lines = [f"0. {self.root_title}\n"]
        for node in sorted(self.nodes.values(), key=lambda n: n.id):
            lines.append(node.to_markdown(include_children=False))
        return "".join(lines)
```

#### B. 遗留代码删除

```python
# 以下内容将被删除（不迁移）：

# StateManager - 被 Trace recording 替代
```

**⚠️ SimulationState 处理方案 (V6.13 v1.8 修正)**：

> **v1.8 重大变更**: 将 `TraversalState` (BaseModel) 重命名为 `SimulationState`，消除与 `TraversalState` (Enum) 的命名冲突。
> **重要变更**: SimulationState 不能简单删除。集成测试运行时使用其方法。

**方案 A (推荐)**: 保留 SimulationState 在 content_models 中

```python
# src/models/content_models.py (V6.13.0)

"""
Content models for simulation and testing.

Moved from src.state.content_tree in V6.13.0.

Contains 12 classes:
- Coordinate, Direction, MenuInfo, MenuItem, MenuItemType, ExpectedAction
- PageAnalysis, PopupInfo
- ContentTree, ContentNode, VisitFingerprint (integration test support)
- SimulationState (⚠️ 仅用于仿真和集成测试，生产代码使用 TraversalRuntimeContext)
"""

class SimulationState(BaseModel):
    """Runtime state for simulation and integration tests.
    
    ⚠️ DEPRECATED for production code: Use TraversalRuntimeContext in src/trace/context.py
    This model is kept for simulation and integration test compatibility.
    
    Renamed from TraversalState in V6.13.0 v1.8 to avoid naming conflict with 
    TraversalState Enum in src/state_machine/traversal_fsm.py.
    """
    # 字段（13 个，含 2 个带 alias 的字段）
    current_path: list[str] = Field(default_factory=list)
    visited: set[str] = Field(default_factory=set)
    all_level1_menus: dict[str, MenuInfo] = Field(default_factory=dict)
    level2_menus_cache: dict[str, list[MenuInfo]] = Field(default_factory=dict)
    items_cache: dict[str, list[MenuItem]] = Field(default_factory=dict)
    content_tree: ContentTree = Field(default_factory=ContentTree)
    step_count: int = 0
    current_phase: str = "initialized"
    consecutive_errors: int = 0
    last_error: Optional[str] = None
    target_app: Optional[str] = None
    exception_history_records: list[dict] = Field(
        default_factory=list, 
        alias="_exception_history_records"
    )
    node_stack: list[dict] = Field(
        default_factory=list, 
        alias="_node_stack"
    )
    current_node_id: Optional[str] = None
    use_graph_mode: bool = False
    
    # 方法（8 个）- 集成测试使用
    def add_level1_menu(self, name: str, menu: MenuInfo) -> None:
        """Add a level 1 menu to the state."""
        self.all_level1_menus[name] = menu
    
    def get_level2_menus(self, level1: str) -> list[MenuInfo]:
        """Get cached level 2 menus for a level 1 menu."""
        return self.level2_menus_cache.get(level1, [])
    
    def add_items(self, level1: str, level2: str, items: list[MenuItem]) -> None:
        """Add items to the cache."""
        cache_key = f"{level1}|{level2}"
        self.items_cache[cache_key] = items
    
    def get_current_cache_key(self) -> str:
        """Generate cache key from current path."""
        return "|".join(self.current_path)
    
    def is_visited(self, fingerprint: str) -> bool:
        """Check if element was visited."""
        return fingerprint in self.visited
    
    def mark_visited(self, fingerprint: str) -> None:
        """Mark element as visited."""
        self.visited.add(fingerprint)
    
    def get_exception_history_summary(self) -> dict:
        """Get exception statistics."""
        if not self.exception_history_records:
            return {"total": 0, "by_type": {}}
        from collections import Counter
        return {
            "total": len(self.exception_history_records),
            "by_type": dict(Counter(r.get("type", "unknown") for r in self.exception_history_records))
        }
    
    def get_exceptions_by_type(self, exception_type: str) -> list[dict]:
        """Filter exceptions by type."""
        return [r for r in self.exception_history_records if r.get("type") == exception_type]
```

**方案 B**: 功能迁移到 TraversalRuntimeContext（需额外 5-8h）

- 优点：完全清理遗留代码
- 缺点：需要重写集成测试，工作量较大
- 建议：仅在 V7.0 考虑

**TYPE_CHECKING 处理**（方案 A）：
```python
# V6.13.0: ExceptionContext 继续使用类型别名
if TYPE_CHECKING:
    from src.trace.context import TraversalRuntimeContext as TraversalState
    # 或者
    from src.models.content_models import SimulationState  # 新命名（v1.8）
```

**向后兼容性**（v1.8 新增）：
为保持向后兼容性，在 `src/models/__init__.py` 中提供别名：
```python
# src/models/__init__.py (V6.13.0 v1.8)
from src.models.content_models import SimulationState

# 向后兼容别名（可选，用于平滑迁移）
TraversalState = SimulationState  # 类型别名，指向新名称
```

### 3.2 导入更新策略

#### 仿真测试更新

```python
# 之前
from src.state.content_tree import (
    Coordinate, Direction, MenuInfo, MenuItem, MenuItemType,
    ExpectedAction, PageAnalysis, PopupInfo
)

# 之后 (V6.13.0)
from src.models.content_models import (
    Coordinate, Direction, MenuInfo, MenuItem, MenuItemType,
    ExpectedAction, PageAnalysis, PopupInfo
)
```

#### 集成测试更新

```python
# 之前
from src.state import TraversalState, ContentTree, VisitFingerprint

# 之后 (V6.13.0)
from src.models.content_models import ContentTree, VisitFingerprint
# TraversalState 不再需要，使用 TraversalRuntimeContext 替代（如需要）
```

### 3.3 TYPE_CHECKING 依赖处理

#### 明确方案：类型别名（单一方案）

> **决策**: 使用类型别名方案，不提供其他选项。
> **理由**: 最简单、与 V6 架构一致、类型安全完整。

```python
# src/exception/context.py

# 之前
from typing import TYPE_CHECKING
if TYPE_CHECKING:
    from src.state.content_tree import TraversalState

# 之后 (V6.13.0) - 类型别名
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from src.trace.context import TraversalRuntimeContext as TraversalState

# V6.13.0 期间：ExceptionContext 继续使用 TraversalState 类型注解
# V6.14.0：可考虑完全移除 TraversalState 引用（或保留为类型别名）
```

### 3.4 弃用警告策略

> **注意**: 需要验证无循环导入风险。使用 `__all__` 控制导出，或使用 `__getattr__` 进行延迟导入。
> **Pydantic 模型特殊处理**: 避免序列化时的递归警告问题。

```python
# src/state/__init__.py (V6.13.0)

"""
State persistence and legacy models (V6.13 DEPRECATED).

This module is deprecated and will be removed in V6.14.0.
Use src.models.content_models instead.
"""

import warnings
from typing import Any
import inspect

# 使用 __getattr__ 延迟导入，避免循环依赖
def __getattr__(name: str) -> Any:
    """Lazy import from new location with deprecation warning.
    
    Special handling for Pydantic models to avoid recursion during serialization.
    When Pydantic serializes a model, it accesses model.__module__ which can
    trigger this __getattr__, causing recursive warnings.
    """
    from src.models import content_models
    
    # 增强的 Pydantic 检测逻辑：检查堆栈帧中的函数名和文件名
    is_pydantic_context = False
    for frame_info in inspect.stack()[1:6]:  # 检查最近 5 层堆栈
        frame_obj = frame_info[0]  # frame object
        code_obj = frame_obj.f_code
        # 检查函数名
        if 'pydantic' in code_obj.co_name.lower() or 'serialize' in code_obj.co_name.lower():
            is_pydantic_context = True
            break
        # 检查文件名
        filename = code_obj.co_filename or ''
        if 'pydantic' in filename.lower():
            is_pydantic_context = True
            break
    
    if is_pydantic_context:
        return getattr(content_models, name)
    
    warnings.warn(
        f"src.state.{name} is deprecated. Use src.models.content_models.{name} instead. "
        "This module will be removed in V6.14.0.",
        DeprecationWarning,
        stacklevel=2
    )
    
    return getattr(content_models, name)


__all__ = [
    "Coordinate", "Direction", "MenuInfo", "MenuItem", "MenuItemType",
    "ExpectedAction", "PageAnalysis", "PopupInfo",
    "ContentTree", "ContentNode", "VisitFingerprint",
    # 以下已移除，不再导出
    # "TraversalState",  # 使用 TraversalRuntimeContext 替代
    # "StateManager",    # 已废弃
]
```

**弃用警告噪音缓解**：
`__getattr__` 会在每次导入时产生警告（33 个文件），可能导致 CI/CD pipeline 失败。

**CI/CD 配置**（在 `setup.cfg` 或 `pyproject.toml`）：
```ini
# setup.cfg
[tool:pytest]
filterwarnings =
    ignore::DeprecationWarning:src.state.*
```

或在测试代码中：
```python
# conftest.py
import warnings
warnings.filterwarnings("ignore", category=DeprecationWarning, module="src.state.*")
```

**循环导入风险缓解**：
- 使用 `__getattr__` 延迟导入，只在访问时导入
- 在函数内部导入，避免模块级导入
- 通过 `getattr(content_models, name)` 动态获取

---

## 4. 实施计划

### 4.1 阶段划分

| 阶段 | 版本 | 内容 | 验收标准 | 工时 |
|------|------|------|----------|------|
| **P0** | V6.13.0 | 创建单文件 `content_models.py`（12 个类，含 SimulationState）+ fixture 检查 + 向后兼容验证 | 单元测试通过 | 6h |
| **P1** | V6.13.0 | 更新仿真测试导入（3 文件） | 仿真测试通过 | 1.5h |
| **P2** | V6.13.0 | 处理 TYPE_CHECKING 依赖（1 文件） | 异常测试通过 | 1h |
| **P3a** | V6.13.0 | 批次 1：底层模型（Coordinate, Direction, MenuInfo） | 单元测试通过 | 3h |
| **P3b** | V6.13.0 | 批次 2：中层模型（MenuItem, MenuItemType, ExpectedAction, PageAnalysis, PopupInfo） | 单元测试通过 | 4h |
| **P3c** | V6.13.0 | 批次 3：高层模型（ContentTree, ContentNode, VisitFingerprint）+ TraversalState 测试（7 文件） | 单元测试通过 | 4h |
| **P4** | V6.13.0 | 更新集成测试导入（2 文件）+ TraversalState 方法验证 | 集成测试通过 | 5h |
| **P5** | V6.13.0 | 添加弃用警告 + CI/CD 配置 | 弃用警告正确显示 | 1h |
| **P6** | V6.14.0 | 删除 `src.state` 目录（保留 StateManager 删除说明） | 无导入错误 | 0.5h |
| **P7** | V6.14.0 | 全量验证 | 全量测试通过 | 3h |

**总计**: 29 小时（V6.13: 24.5h + V6.14: 3.5h）

> **v1.4 变更说明**:
> - P0: 2h → 5h（添加 fixture 检查，更完整的测试）
> - P3: 按依赖层级划分批次（非机械按文件数）
> - P5: 0.5h → 1h（添加 CI/CD warnings filter 配置）
> - 总工时: 18.5h → 24.5h

> **v1.7 变更说明**:
> - P0: 5h → 6h（添加 TraversalState 迁移）
> - P0 类别: 11 个类 → 12 个类（含 TraversalState）
> - P4: 1.5h → 5h（添加 TraversalState 方法验证）
> - 总工时: 24.5h → 29h

> **风险缓冲**: 实际实施建议预留 50% buffer，总计约 40-50 小时（1-1.5 周全职工作）。

**P3 批次划分说明**（按依赖层级）：
- **批次 1**：底层基础模型（Coordinate, Direction, MenuInfo）- 无内部依赖
- **批次 2**：中层组合模型（依赖批次 1）- MenuItem, MenuItemType, ExpectedAction, PageAnalysis, PopupInfo
- **批次 3**：高层树模型（依赖批次 1,2）+ TraversalState 测试 - ContentTree, ContentNode, VisitFingerprint + 7 个 TraversalState 测试文件

**P3 批次依赖验证**：
- 在 **P3a 完成后**：验证 P3b 的导入不会因为 P3a 的修改而失败
  ```bash
  # 验证中层模型可以正常导入
  python -c "from src.models.content_models import MenuItem, MenuItemType, ExpectedAction, PageAnalysis, PopupInfo"
  ```
- 在 **P3b 完成后**：验证 P3c 的导入不会因为 P3a/P3b 的修改而失败
  ```bash
  # 验证高层模型可以正常导入
  python -c "from src.models.content_models import ContentTree, ContentNode, VisitFingerprint"
  ```
- **如果发现批次间依赖冲突**：应合并批次，一次性完成所有单元测试的导入更新

**自动化验证脚本**（集成到 T4a/T4b 验收步骤）：

```python
# scripts/verify_batch_dependencies.py
"""验证批次间的依赖关系和模型兼容性"""

import subprocess
import sys

def verify_batch_1_to_2():
    """验证批次 1 完成后，批次 2 可以正常导入和实例化。"""
    try:
        # 验证导入
        result = subprocess.run(
            ["python", "-c",
             "from src.models.content_models import MenuItem, MenuItemType, ExpectedAction, PageAnalysis, PopupInfo; "
             "print('✓ Batch 2 imports OK')"],
            capture_output=True, text=True, timeout=30
        )
        if result.returncode != 0:
            print(f"❌ Batch 2 import failed: {result.stderr}")
            return False
        
        # 验证实例化
        result = subprocess.run(
            ["python", "-c",
             "from src.models.content_models import MenuItem, Coordinate; "
             "coord = Coordinate(x=0.5, y=0.5); "
             "item = MenuItem(name='test', coordinate=coord); "
             "print(f'✓ Batch 2 instantiation OK: {item.name}')"],
            capture_output=True, text=True, timeout=30
        )
        if result.returncode != 0:
            print(f"❌ Batch 2 instantiation failed: {result.stderr}")
            return False
        
        # 验证序列化/反序列化
        result = subprocess.run(
            ["python", "-c",
             "from src.models.content_models import MenuItem, Coordinate; "
             "import json; "
             "coord = Coordinate(x=0.5, y=0.5); "
             "item = MenuItem(name='test', coordinate=coord); "
             "serialized = item.json(); "
             "restored = MenuItem.parse_raw(serialized); "
             "print(f'✓ Batch 2 serialization OK')"],
            capture_output=True, text=True, timeout=30
        )
        if result.returncode != 0:
            print(f"❌ Batch 2 serialization failed: {result.stderr}")
            return False
        
        print("✓ Batch 1 → Batch 2 dependency verified")
        return True
    except Exception as e:
        print(f"❌ Batch verification error: {e}")
        return False

def verify_batch_2_to_3():
    """验证批次 2 完成后，批次 3 可以正常导入和实例化。"""
    try:
        # 验证导入
        result = subprocess.run(
            ["python", "-c",
             "from src.models.content_models import ContentTree, ContentNode, VisitFingerprint; "
             "print('✓ Batch 3 imports OK')"],
            capture_output=True, text=True, timeout=30
        )
        if result.returncode != 0:
            print(f"❌ Batch 3 import failed: {result.stderr}")
            return False
        
        # 验证实例化
        result = subprocess.run(
            ["python", "-c",
             "from src.models.content_models import ContentTree, ContentNode; "
             "tree = ContentTree(); "
             "node = tree.add_node('Root', 1); "
             "print(f'✓ Batch 3 instantiation OK: {node.id}')"],
            capture_output=True, text=True, timeout=30
        )
        if result.returncode != 0:
            print(f"❌ Batch 3 instantiation failed: {result.stderr}")
            return False
        
        # 验证 to_markdown 方法
        result = subprocess.run(
            ["python", "-c",
             "from src.models.content_models import ContentTree; "
             "tree = ContentTree(); "
             "node = tree.add_node('Test', 1); "
             "md = tree.to_markdown(); "
             "print(f'✓ Batch 3 to_markdown OK')"],
            capture_output=True, text=True, timeout=30
        )
        if result.returncode != 0:
            print(f"❌ Batch 3 to_markdown failed: {result.stderr}")
            return False
        
        print("✓ Batch 2 → Batch 3 dependency verified")
        return True
    except Exception as e:
        print(f"❌ Batch verification error: {e}")
        return False

def main():
    print("=== Verifying P3 Batch Dependencies ===\n")
    
    checks = [
        ("Batch 1 → Batch 2", verify_batch_1_to_2),
        ("Batch 2 → Batch 3", verify_batch_2_to_3),
    ]
    
    results = []
    for name, check in checks:
        print(f"\n[{name}]")
        results.append(check())
    
    if all(results):
        print("\n✓ All batch dependencies verified successfully!")
        return 0
    else:
        print("\n❌ Some batch dependencies failed!")
        print("Recommendation: Merge batches and update all tests together")
        return 1

if __name__ == "__main__":
    sys.exit(main())
```

**集成到验收步骤**：
- T4a 验收：运行 `python scripts/verify_batch_dependencies.py`，检查 Batch 1 → Batch 2
- T4b 验收：运行 `python scripts/verify_batch_dependencies.py`，检查 Batch 2 → Batch 3

### 4.2 V6.13.0 详细任务清单

#### T1: 创建新模型文件 + Fixture 检查 + 向后兼容验证 (6h)

> **v1.8 变更说明**: 添加向后兼容验证（SimulationState 别名测试）。

- [ ] **Fixture 检查**（0.5h）
  ```bash
  # 步骤 1: 检查所有 fixture 文件是否包含序列化模型
  grep -r "PageAnalysis\|ContentTree\|TraversalState" fixtures/ --include="*.json" --include="*.pkl"
  ```
  
  ```python
  # 步骤 2: 实际加载 JSON fixture 并验证反序列化
  # 步骤 3: 验证 pickle 文件兼容性
  # scripts/verify_fixture_compatibility.py
  
  import json
  import pickle
  from pathlib import Path
  from src.models.content_models import PageAnalysis, ContentTree
  
  def verify_fixture_compatibility():
      """验证 fixture 文件与新模型的兼容性。"""
      fixtures_dir = Path("fixtures")
      issues = []
      
      # JSON fixture 验证
      for json_file in fixtures_dir.rglob("*.json"):
          try:
              with open(json_file) as f:
                  data = json.load(f)
              
              # 检查是否包含 PageAnalysis 或 ContentTree 结构
              for key, value in data.items():
                  if isinstance(value, dict):
                      # 尝试解析为 PageAnalysis
                      if "level1_dir" in value or "level1_menus" in value:
                          try:
                              PageAnalysis(**value)
                          except Exception as e:
                              issues.append(f"{json_file}: {key} - {e}")
                      
                      # 尝试解析为 ContentTree
                      elif "nodes" in value or "root_title" in value:
                          try:
                              ContentTree(**value)
                          except Exception as e:
                              issues.append(f"{json_file}: {key} - {e}")
                              
          except Exception as e:
              issues.append(f"{json_file}: Failed to load - {e}")
      
      # Pickle fixture 验证
      for pkl_file in fixtures_dir.rglob("*.pkl"):
          try:
              with open(pkl_file, "rb") as f:
                  obj = pickle.load(f)
              
              # 检查对象的 __module__ 属性
              if hasattr(obj, "__module__"):
                  if "src.state" in obj.__module__:
                      issues.append(f"{pkl_file}: Contains old module reference {obj.__module__}")
                      # 建议重新生成
                      issues.append(f"  → Suggestion: Regenerate using src.models.content_models")
              
              # 如果是 PageAnalysis 或 ContentTree 实例，尝试验证结构
              if isinstance(obj, (PageAnalysis, ContentTree)):
                  # 使用新模型重新序列化验证
                  try:
                      obj.dict()  # Pydantic 序列化
                  except Exception as e:
                      issues.append(f"{pkl_file}: Serialization failed - {e}")
                      
          except Exception as e:
              issues.append(f"{pkl_file}: Failed to load pickle - {e}")
      
      if issues:
          print("❌ Fixture compatibility issues found:")
          for issue in issues:
              print(f"  - {issue}")
          print("\n💡 Recommendation: Regenerate incompatible fixtures using new models")
          return False
      else:
          print("✓ All fixtures are compatible with new models")
          return True
  
  if __name__ == "__main__":
      import sys
      sys.exit(0 if verify_fixture_compatibility() else 1)
  ```
  
  - 如发现 .pkl 文件包含旧 `__module__` 引用（`src.state.content_tree`），需：
    1. 提供重新生成 fixture 脚本（使用新模型重新序列化）
    2. 或提供迁移脚本更新 pickle 文件中的 `__module__` 属性
  - 如发现类路径引用（如 `"__module__": "src.state.content_tree"`），需提供迁移脚本
  - 记录检查结果到 PRD 或迁移日志

- [ ] **创建 `src/models/content_models.py`**（3h）
  - 迁移所有 11 个类（Coordinate, Direction, MenuInfo, MenuItem, MenuItemType, ExpectedAction, PageAnalysis, PopupInfo, ContentTree, ContentNode, VisitFingerprint）
  - 保持单文件结构（约 410 行）
  - 添加所有 helper methods（from_value, is_valid, values, add_node, add_child_node, mark_visited, get_unvisited_children, to_markdown）
  - 添加模块文档说明迁移来源
  - 验证序列化/反序列化兼容性（特别是 `__module__` 属性）

- [ ] **创建单元测试**（1.5h）
  - 创建 `src/models/test/test_content_models.py`
  - 测试所有 12 个类的基本功能（含 SimulationState）
  - 测试所有 helper methods
  - 验证序列化/反序列化（`model.dict()`, `model.parse_obj()`）
  - 验证 ContentTree 方法完整性
  - 验证 `__module__` 属性变化不影响反序列化
  - 验证 SimulationState 的 13 个字段（含 2 个 alias 字段）

- [ ] **向后兼容验证**（1h，v1.8 新增）
  ```python
  # 验证 SimulationState 和 TraversalState 别名
  # scripts/verify_backward_compatibility.py
  
  def verify_simulation_state_alias():
      """验证 SimulationState 可以通过 TraversalState 别名访问。"""
      from src.models import SimulationState, TraversalState
      
      # 验证别名指向同一个类
      assert SimulationState is TraversalState, \
          "TraversalState should be an alias for SimulationState"
      
      # 验证可以通过别名实例化
      state1 = SimulationState()
      state2 = TraversalState()  # 通过别名
      
      assert type(state1) == type(state2), \
          "Both should create the same type"
      
      # 验证字段兼容性（特别是 alias 字段）
      state = SimulationState(
          current_path=["test"],
          _exception_history_records=[{"type": "test"}],
          _node_stack=[{"node": "test"}]
      )
      
      # 验证 alias 字段正常工作
      assert state.exception_history_records == [{"type": "test"}]
      assert state.node_stack == [{"node": "test"}]
      
      # 验证 JSON 序列化使用别名
      import json
      state_dict = json.loads(state.json())
      assert "_exception_history_records" in state_dict
      assert "_node_stack" in state_dict
      
      print("✓ SimulationState backward compatibility verified")
      return True
  
  if __name__ == "__main__":
      verify_simulation_state_alias()
  ```
  
  - 运行 `python scripts/verify_backward_compatibility.py`
  - 验证 `TraversalState` 别名指向 `SimulationState`
  - 验证可以通过别名实例化
  - 验证 alias 字段（`_exception_history_records`, `_node_stack`）正常工作

- [ ] 验收：`pytest src/models/test/test_content_models.py -v` 通过，无 fixture 兼容性问题，向后兼容验证通过

#### T2: 更新仿真测试 (1.5h)

- [ ] 更新 `src/simulation/mock_vision.py`
  - 修改导入语句
  - 验证功能不变
- [ ] 更新 `src/simulation/stateful_mock_vision.py`
  - 修改导入语句
- [ ] 更新 `src/simulation/scroll/scrollable_mock_vision.py`
  - 修改导入语句
- [ ] 验收：`pytest src/simulation/ -v` 通过

#### T3: 处理 TYPE_CHECKING 依赖 (1h)

- [ ] 更新 `src/exception/context.py`
  - 使用 Any 注解替代 TraversalState
  - 或使用 Protocol 定义类型接口
- [ ] 更新 `src/exception/test/*.py`
  - 验证类型检查仍然工作
- [ ] 验收：`mypy src/exception/` 通过，异常处理测试通过

#### T4a: 更新单元测试批次 1 - 底层模型 (3h)

**依赖层级**: 底层基础模型（Coordinate, Direction, MenuInfo）- 无内部依赖

- [ ] 更新 `src/models/test/test_coordinate.py`
  - 修改导入 `from src.models.content_models import Coordinate`
- [ ] 更新 `src/models/test/test_direction.py`
  - 修改导入 `from src.models.content_models import Direction`
  - 验证 Enum helper methods (values, from_value, is_valid)
- [ ] 更新 `src/models/test/test_menu_info.py`
  - 修改导入 `from src.models.content_models import MenuInfo`
  - 验证 Coordinate 嵌套序列化
- [ ] 搜索并更新其他使用底层模型的测试文件
- [ ] 验收：批次 1 测试通过，底层模型功能完整

#### T4b: 更新单元测试批次 2 - 中层模型 (4h)

**依赖层级**: 中层组合模型（依赖批次 1）- MenuItem, MenuItemType, ExpectedAction, PageAnalysis, PopupInfo

- [ ] 更新 `src/models/test/test_menu_item.py`
  - 修改导入 `from src.models.content_models import MenuItem, MenuItemType, ExpectedAction`
  - 验证 get_fingerprint() 方法
- [ ] 更新 `src/models/test/test_page_analysis.py`
  - 修改导入 `from src.models.content_models import PageAnalysis, PopupInfo`
  - 验证嵌套模型序列化
- [ ] 更新 `src/simulation/test/test_*.py`（仿真测试，约 4 个文件）
  - 修改 PageAnalysis 等模型导入
  - 验证仿真功能不变
- [ ] 更新 `src/ai/test/integration/test_new_architecture.py`
  - 修改 PageAnalysis 导入
- [ ] 验收：批次 2 测试通过，中层模型功能完整

#### T4c: 更新单元测试批次 3 - 高层模型 + TraversalState (4h)

**依赖层级**: 高层树模型（依赖批次 1,2）+ TraversalState 测试 - ContentTree, ContentNode, VisitFingerprint + 7 个 TraversalState 测试文件

- [ ] 更新 `src/models/test/test_content_tree.py`
  - 修改导入 `from src.models.content_models import ContentTree, ContentNode, VisitFingerprint`
  - 验证所有 6 个方法：add_node, add_child_node, mark_visited, get_unvisited_children, to_markdown
- [ ] 更新 TraversalState 相关测试（7 个文件）
  - `src/exception/test/test_handlers.py` - 修改 TraversalState 类型注解
  - `src/exception/test/test_history.py` - 修改 TraversalState 类型注解
  - `src/exception/test/test_integration.py` - 修改 TraversalState 类型注解
  - `src/exception/test/test_context.py` - 修改 TraversalState 类型注解
  - `src/exception/test/test_chain.py` - 修改 TraversalState 类型注解
  - 搜索并更新其他 TraversalState 测试文件
- [ ] 全面搜索验证无遗漏
  ```bash
  grep -r "from src.state" src/ --include="*.py"
  grep -r "import src.state" src/ --include="*.py"
  ```
- [ ] 验收：批次 3 测试通过，高层模型功能完整，无遗留 TraversalState 导入

#### T5: 更新集成测试 (4-6h)

> **v1.7 变更**: 工时从 1.5h 调整为 4-6h，因为需要处理 SimulationState 方法迁移。
> **v1.8 变更**: TraversalState 重命名为 SimulationState，更新所有导入引用。

- [ ] 更新 `tests/integration/test_complete_traversal.py`
  - 修改 ContentTree, VisitFingerprint 导入
  - 修改 TraversalState 导入为 SimulationState（或使用 TraversalState 别名）
  - 验证 SimulationState 方法调用兼容性（add_level1_menu, get_level2_menus 等）
- [ ] 更新 `tests/integration/test_with_real_data.py`
  - 修改 ContentTree 导入
  - 修改 TraversalState 导入为 SimulationState（如有使用）
  - 验证功能不变
- [ ] **SimulationState 方法验证**（2-3h）
  - 确认所有 SimulationState 方法在新位置正常工作
  - 如发现方法签名变化，需相应修改测试代码
  - 运行集成测试验证功能完整
- [ ] **搜索并替换所有 TraversalState 导入**（v1.8 新增）
  ```bash
  # 搜索所有 TraversalState（BaseModel）导入
  grep -r "from src.state.*TraversalState" tests/integration/ --include="*.py"
  grep -r "from src.models.*import.*TraversalState" tests/integration/ --include="*.py"
  
  # 替换为 SimulationState 或保留 TraversalState 别名
  # 选项 A: 使用新名称
  from src.models import SimulationState
  # 选项 B: 使用别名（向后兼容）
  from src.models import TraversalState  # 别名指向 SimulationState
  ```
- [ ] 验收：`pytest tests/integration/ -v` 通过

#### T6: 添加弃用警告 + CI/CD 配置 (1h)

- [ ] 更新 `src/state/__init__.py`
  - 添加 `__getattr__` 延迟导入
  - 添加 DeprecationWarning
  - 添加重定向到新模型（11 个类）
  - 添加模块文档说明
- [ ] 配置 CI/CD warnings filter（避免警告噪音）
  ```ini
  # setup.cfg 或 pyproject.toml
  [tool:pytest]
  filterwarnings =
      ignore::DeprecationWarning:src.state.*
  ```
- [ ] 验证弃用警告正确显示（在开发环境）
- [ ] 验证 CI/CD 不因警告失败
- [ ] 验收：开发环境能看到警告，CI/CD 通过

### 4.3 V6.14.0 详细任务清单

#### T7: 删除遗留代码 (0.5h)

- [ ] 删除 `src/state/state_manager.py`
- [ ] 删除 `src/state/content_tree.py`
- [ ] 删除 `src/state/__init__.py`
- [ ] 删除空目录 `src/state/`
- [ ] 验收：无 `from src.state` 导入错误

#### T8: 全量验证 (3h)

- [ ] 运行全量测试 `pytest tests/ -v`
- [ ] 运行仿真测试 `pytest src/simulation/ -v`
- [ ] 运行类型检查 `mypy src/ --strict`
- [ ] 验证无遗留导入
  ```bash
  grep -r "from src.state" src/ --include="*.py"
  grep -r "import src.state" src/ --include="*.py"
  grep -r "from src.state" tests/ --include="*.py"
  grep -r "from src.state.content_tree" tests/ --include="*.py"
  ```
- [ ] 运行验证脚本 `python scripts/verify_state_migration.py`
- [ ] 更新相关文档（INDEX.md, ARCHITECTURE.md）
- [ ] 验收：所有测试通过，无遗留导入，文档更新完成
- [ ] 运行仿真测试 `pytest src/simulation/ -v`
- [ ] 运行类型检查 `mypy src/ --strict`
- [ ] 验证无遗留导入
  ```bash
  grep -r "from src.state" src/ --include="*.py"
  grep -r "from src.state" tests/ --include="*.py"
  grep -r "from src.state" tests/integration/ --include="*.py"
  ```
- [ ] 运行验证脚本 `python scripts/verify_state_migration.py`
- [ ] 更新相关文档
- [ ] 验收：所有测试通过，无遗留导入

---

## 5. 成功标准

### 5.1 功能验收

- ✅ 所有测试通过（无回归）
- ✅ 仿真测试正常工作
- ✅ 集成测试正常工作（ContentTree/ContentNode 功能完整）
- ✅ 异常处理测试通过
- ✅ 类型检查通过（mypy）
- ✅ V6.14.0: 无 `from src.state` 导入
- ✅ V6.14.0: `/src/state` 目录完全删除

### 5.2 代码质量

- ✅ 通过 `mypy strict` 类型检查
- ✅ 通过 `ruff` linting
- ✅ 新模型文件有完整单元测试
- ✅ 导入路径清晰一致
- ✅ 无循环导入
- ✅ ContentTree/ContentNode 方法完整测试

### 5.3 文档更新

- ✅ 更新 `docs/INDEX.md`
- ✅ 更新 `docs/architecture/ARCHITECTURE.md`
- ✅ 添加迁移说明

### 5.4 验证脚本

```python
# scripts/verify_state_migration.py
"""验证 src.state 迁移完整性"""

import subprocess
import sys

# 所有导入检测模式
IMPORT_PATTERNS = [
    "from src.state",
    "import src.state",
    "from src.state.content_tree",
    "import src.state.content_tree",
    "from src.state.state_manager",
    "import src.state.state_manager",
    # 相对导入模式（虽然当前代码库可能不使用，但验证脚本应完整）
    "from \\.state",
    "from \\.state\\.content_tree",
    "from \\.state\\.state_manager",
]

def check_imports_in_directory(directory: str) -> bool:
    """检查目录中是否有任何 src.state 导入"""
    found_issues = []
    
    for pattern in IMPORT_PATTERNS:
        result = subprocess.run(
            ["grep", "-r", pattern, directory, "--include=*.py", "--exclude-dir=__pycache__"],
            capture_output=True, text=True
        )
        if result.returncode == 0:
            found_issues.append(f"Pattern '{pattern}':\n{result.stdout}")
    
    if found_issues:
        print(f"ERROR: Found src.state imports in {directory}:")
        for issue in found_issues:
            print(issue)
        return False
    print(f"✓ No src.state imports found in {directory}/")
    return True

def verify_no_src_imports():
    """验证 src/ 中没有 src.state 导入"""
    return check_imports_in_directory("src/")

def verify_no_test_imports():
    """验证 tests/ 中没有 src.state 导入"""
    return check_imports_in_directory("tests/")

def verify_no_integration_imports():
    """验证 tests/integration/ 中没有 src.state 导入（子集检查）"""
    return check_imports_in_directory("tests/integration/")

def verify_tests():
    """验证所有测试通过"""
    result = subprocess.run(
        ["pytest", "tests/", "-v", "--tb=short"],
        capture_output=True, text=True
    )
    if result.returncode != 0:
        print(f"ERROR: Tests failed:\n{result.stdout}")
        return False
    print("✓ All tests passed")
    return True

def verify_mypy():
    """验证类型检查通过"""
    result = subprocess.run(
        ["mypy", "src/", "--strict"],
        capture_output=True, text=True
    )
    if result.returncode != 0:
        print(f"ERROR: mypy failed:\n{result.stdout}")
        return False
    print("✓ mypy strict passed")
    return True

def main():
    print("=== Verifying src.state migration ===\n")
    checks = [
        ("No src/ imports", verify_no_src_imports),
        ("No tests/ imports", verify_no_test_imports),
        ("No integration imports", verify_no_integration_imports),
        ("Tests pass", verify_tests),
        ("mypy strict", verify_mypy),
    ]
    
    results = []
    for name, check in checks:
        print(f"\n[{name}]")
        results.append(check())
    
    if all(results):
        print("\n✓ All verification checks passed!")
        return 0
    print("\n✗ Some verification checks failed!")
    return 1

if __name__ == "__main__":
    sys.exit(main())
```

---

## 6. 实施建议和回滚计划

### 6.1 实施建议

- **优先级顺序**: P0 → P1 → P2 → P3a → P3b → P3c → P4 → P5（V6.13.0）→ P6 → P7（V6.14.0）
- **分支策略**: 在实施前创建 feature branch，便于回滚
  ```bash
  git checkout -b feature/v6.13-state-migration
  ```
- **批次验证**: P3 阶段分三批次进行，每批完成后验证测试通过
- **增量提交**: 每个阶段完成后创建 commit，便于定位问题
- **测试优先**: 在 P0 完成后立即运行新模型单元测试，验证核心功能

### 6.2 回滚计划

| 场景 | 回滚方式 | 恢复步骤 |
|------|----------|----------|
| **P0-P1 失败** | Git reset | `git reset --hard origin/main` |
| **P2 失败** | 恢复异常处理文件 | `git checkout HEAD~1 -- src/exception/context.py` |
| **P3a/P3b 失败** | Git revert 单个 commit | `git revert HEAD` |
| **P4 失败** | 恢复集成测试 | `git checkout HEAD~1 -- tests/integration/` |
| **全量失败** | 删除分支，切回 main | `git checkout main && git branch -D feature/v6.13-state-migration` |

**回滚验证**:
```bash
# 回滚后验证测试通过
pytest tests/ -v

# 验证无遗留导入错误
python -c "from src.state import PageAnalysis; print('OK')"
```

---

## 7. 风险和缓解

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| **测试失败** | 迁移可能导致测试意外失败 | 每个阶段后运行测试，及时修复 |
| **遗漏导入** | 可能有隐藏的导入未发现 | 使用 grep 全面搜索所有导入模式（6 种） |
| **集成测试破坏** | ContentTree/ContentNode 方法不完整可能导致集成测试失败 | 迁移所有方法并测试，在 P4 阶段验证 |
| **TYPE_CHECKING 错误** | 类型检查可能失败 | 使用类型别名 `TraversalRuntimeContext as TraversalState` |
| **仿真测试破坏** | 模型变更可能影响仿真 | 优先验证仿真测试（P1 阶段） |
| **文档过期** | 文档可能仍引用旧路径 | 全面更新文档 |
| **弃用警告噪音** | 33 个文件每次导入产生警告，CI/CD 可能因超阈值失败 | 配置 warnings filter 忽略 `DeprecationWarning:src.state.*` |
| **批次依赖冲突** | P3 批次间可能有依赖关系，机械划分导致冲突 | 按依赖层级划分批次（底层→中层→高层），非按文件数分配 |
| **Fixture 反序列化失败** | 序列化的 .pkl 文件包含旧 `__module__` 路径，反序列化失败 | 在 P0 阶段检查，提供迁移脚本或重新生成 fixture |
| **工时低估风险** | 24.5h 估算仍可能乐观，实际需要更多时间 | 预留 50% buffer，总计 35-40 小时 |

---

### 工时估算说明（24.5 小时）

**24.5 小时包括**：
- 代码迁移和修改（P0-P5: 20h）
- 验证和测试（P6-P7: 3.5h）
- 基本文档更新

**24.5 小时不包括**：
- Debug 时间和意外问题处理（预计 5-10h）
- CI/CD 配置调试（预计 2-3h）
- 文档修订和审阅（预计 2-3h）
- 会议和沟通（预计 2-3h）
- 代码审查和迭代（预计 3-5h）

**实际建议预算**: 40-50 小时（约 1-1.5 周全职工作）

---

## 8. 替代方案

### 方案 A：完全重写仿真测试（不推荐）

将仿真测试重写为使用新的架构

| 维度 | 评估 |
|------|------|
| **工时** | 20-30 小时 |
| **收益** | 彻底清理遗留代码 |
| **风险** | 高，仿真测试是关键基础设施 |
| **建议** | ❌ 不推荐 |

### 方案 B：保留遗留模块（保守）

保留 `/src/state` 作为遗留模块，添加明确标记

| 维度 | 评估 |
|------|------|
| **工时** | 1 小时 |
| **收益** | 零风险 |
| **风险** | 低 |
| **建议** | ⚠️ 可接受，技术债务可推迟到 V7.0 |

### 方案 C：单文件迁移（推荐）

整体移动到 `src/models/content_models.py`

| 维度 | 评估 |
|------|------|
| **工时** | 14.5 小时（两版本） |
| **收益** | 彻底清理，架构清晰 |
| **风险** | 中（通过两阶段缓解） |
| **建议** | ✅ 推荐 |

---

## 9. 未来考虑

### 9.1 仿真测试重构（V7.0）

在 V7.0 可以考虑：
- 基于实际实施的架构进行重构
- 统一仿真和生产的数据模型
- 提供仿真专用的模型扩展

### 9.2 模型统一

当前有多个类似的数据模型：
- `PageAnalysis` (仿真)
- `TraversalContext` (AI)
- `TraversalRuntimeContext` (运行时)

未来可以考虑统一这些模型，减少重复。

---

## 10. 修订记录

| 日期 | 版本 | 内容 |
|------|------|------|
| 2026-06-09 | 1.0 | 初始设计 |
| 2026-06-09 | 1.1 | 基于第一轮对抗审阅更新：修正问题描述、更新工时估算、增加 TYPE_CHECKING 处理 |
| 2026-06-09 | 1.2 | 基于第二轮对抗审阅更新：修正文件计数（33 个）、行数估算（410 行）、添加 ContentTree/ContentNode/VisitFingerprint 迁移、修正代码示例语法 |
| 2026-06-09 | 1.3 | 基于第三轮对抗审阅更新：添加 src/models/vision/ 验证要求、移除 Any 类型使用、P3 工时调整为分批次（9h）、明确 TraversalState 处理方案、完善弃用警告实现、修正验证脚本导入检测模式、更新总工时为 18.5h |
| 2026-06-09 | 1.4 | 基于第四轮对抗审阅更新：确认 vision/ 为多文件结构（7 文件，788 行），说明单文件方案与其不一致但合理；P0 工时调整为 5h（含 fixture 检查）；P3 按依赖层级划分批次（底层→中层→高层）；TYPE_CHECKING 明确为单一类型别名方案；添加 CI/CD warnings filter 配置；总工时调整为 24.5h（建议 35-40h 含 buffer）；添加弃用警告噪音和批次依赖冲突风险 |
| 2026-06-09 | 1.5 | 基于第五轮对抗审阅更新：T1 添加实际 fixture 加载验证代码；ContentNode.to_markdown() 添加递归实现；3.4 弃用警告添加 Pydantic 模型特殊处理避免序列化递归；5.4 验证脚本添加相对导入检测模式；4.1 P3 批次添加依赖验证步骤；2.2 添加 src/models/__init__.py 修改示例；7 明确工时估算排除项（debug/CI/CD/文档/会议/审查）；评分提升至 8/10 |
| 2026-06-09 | 1.6 | 基于第六轮对抗审阅更新：T1 添加 pickle 序列化/反序列化兼容性验证；ContentNode.to_markdown() 添加循环引用检测和深度限制（visited 集合、max_depth 参数）；3.4 弃用警告增强 Pydantic 检测逻辑（检查函数名和文件名）；4.1 P3 批次添加自动化验证脚本 scripts/verify_batch_dependencies.py；2.2 明确 src/models/__init__.py 使用追加模式（保留 vision 导出） |
| 2026-06-09 | 1.7 | 基于第七轮对抗审阅更新：关键修正 TraversalState 处理方案 - 从"可删除"改为"必须保留"；添加 TraversalState 完整定义（声称 15 字段 + 8 方法）到 content_models.py；标记为"仅用于仿真和集成测试"；1.1 添加重要澄清说明；1.2 修正 TraversalState 使用情况（TYPE_CHECKING + 方法调用）；1.3 添加 TraversalState 运行时依赖说明；P0 工时 5h → 6h（添加 TraversalState 迁移）；P4 工时 1.5h → 5h（添加方法验证）；总工时 24.5h → 29h；提供方案 A（保留）vs 方案 B（迁移）对比 |
| 2026-06-09 | 1.8 | 基于第八轮对抗审阅更新：关键修正 TraversalState 命名冲突 - 将 BaseModel 重命名为 SimulationState，消除与 TraversalState (Enum) 的冲突；修正字段数量从 15 → 13（实际代码验证）；添加 alias 字段文档（_exception_history_records, _node_stack）；1.1.1 新增 TraversalState 命名冲突分析章节；2.2 更新 src/models/__init__.py 导出（含 SimulationState 和 TraversalState 别名）；3.1 更新 SimulationState 完整定义（13 字段 + 8 方法）；3.3 添加向后兼容别名和 TYPE_CHECKING 处理；T1 添加向后兼容验证脚本（1h）；T5 添加搜索替换所有 TraversalState 导入；评分维持 8/10 |

---

**文档所有者**: Uni-Claw 开发团队
**状态**: 设计阶段
**相关文档**:
- [ARCHITECTURE.md](../architecture/ARCHITECTURE.md)
- [PRD_V6_12_0_Layered_Context_Design.md](./PRD_V6_12_0_Layered_Context_Design.md)
