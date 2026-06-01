"""Data models for content tree and state management."""

from dataclasses import dataclass, field
from enum import Enum
from typing import Optional

from pydantic import BaseModel, Field


class Direction(str, Enum):
    """Menu direction enumeration."""

    LEFT = "left"
    RIGHT = "right"
    TOP = "top"
    BOTTOM = "bottom"

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings.

        Returns:
            List of enum values
        """
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "Direction":
        """Create an enum instance from a string value.

        Args:
            value: String value to convert

        Returns:
            Direction enum instance

        Raises:
            ValueError: If value is not a valid enum value
        """
        try:
            return cls(value)
        except ValueError as e:
            raise ValueError(
                f"Invalid {cls.__name__} value: {value}. "
                f"Valid values: {cls.values()}"
            ) from e

    @classmethod
    def is_valid(cls, value: str) -> bool:
        """Check if a string value is a valid enum value.

        Args:
            value: String value to validate

        Returns:
            True if value is valid, False otherwise
        """
        return value in cls.values()


class Coordinate(BaseModel):
    """Normalized coordinate (0-1)."""

    x: float = Field(ge=0.0, le=1.0, description="X coordinate (normalized 0-1)")
    y: float = Field(ge=0.0, le=1.0, description="Y coordinate (normalized 0-1)")


class MenuInfo(BaseModel):
    """Information about a menu item."""

    name: str
    coordinate: Coordinate
    active: bool = False


class MenuItemType(str, Enum):
    """Type of menu item.

    Extended to support fine-grained button type classification for
    differentiated handling (wait times, verification strategies).
    """

    # Navigation types
    MENU_ITEM = "menu_item"  # Clickable menu item (list item)
    TAB = "tab"  # Tab button
    BACK_BUTTON = "back_button"  # Back navigation button

    # Action types
    SWITCH = "switch"  # Switch/toggle (changes state)
    TOGGLE = "toggle"  # Toggle button (on/off state)
    BUTTON = "button"  # Generic button (triggers action)

    # Other types
    ICON = "icon"  # Icon
    LINK = "link"  # Link/navigation
    TEXT = "text"  # Plain text
    READONLY = "readonly"  # Read-only element

    # Legacy compatibility
    ITEM = "item"  # Legacy: equivalent to MENU_ITEM

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings.

        Returns:
            List of enum values
        """
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "MenuItemType":
        """Create an enum instance from a string value.

        Args:
            value: String value to convert

        Returns:
            MenuItemType enum instance

        Raises:
            ValueError: If value is not a valid enum value
        """
        try:
            return cls(value)
        except ValueError as e:
            raise ValueError(
                f"Invalid {cls.__name__} value: {value}. "
                f"Valid values: {cls.values()}"
            ) from e

    @classmethod
    def is_valid(cls, value: str) -> bool:
        """Check if a string value is a valid enum value.

        Args:
            value: String value to validate

        Returns:
            True if value is valid, False otherwise
        """
        return value in cls.values()


class ExpectedAction(str, Enum):
    """Expected button behavior/action type.

    Used to determine wait times and verification strategies.
    """

    NAVIGATE = "navigate"  # Expects page navigation (menu, tab)
    TOGGLE = "toggle"  # Expects state change (switch)
    ACTION = "action"  # Expects action trigger (popup, jump)
    NONE = "none"  # No expected response (read-only)

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings.

        Returns:
            List of enum values
        """
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "ExpectedAction":
        """Create an enum instance from a string value.

        Args:
            value: String value to convert

        Returns:
            ExpectedAction enum instance

        Raises:
            ValueError: If value is not a valid enum value
        """
        try:
            return cls(value)
        except ValueError as e:
            raise ValueError(
                f"Invalid {cls.__name__} value: {value}. "
                f"Valid values: {cls.values()}"
            ) from e

    @classmethod
    def is_valid(cls, value: str) -> bool:
        """Check if a string value is a valid enum value.

        Args:
            value: String value to validate

        Returns:
            True if value is valid, False otherwise
        """
        return value in cls.values()


class MenuItem(BaseModel):
    """A clickable item on the screen.

    Extended with behavior prediction fields for differentiated
    handling (wait times, verification strategies).
    """

    name: str
    type: MenuItemType = Field(default=MenuItemType.ITEM)
    coordinate: Coordinate
    parent: Optional[str] = None
    description: Optional[str] = None

    # New: Expected behavior fields
    expected_action: ExpectedAction = Field(
        default=ExpectedAction.ACTION,
        description="Expected button behavior (navigate/toggle/action/none)",
    )
    expects_page_change: bool = Field(
        default=False,
        description="Whether clicking should change the current page path",
    )
    expects_state_change: bool = Field(
        default=False,
        description="Whether clicking should change UI state (toggle, etc.)",
    )

    class Config:
        """Pydantic config for enum handling."""
        use_enum_values = True  # Allow string values from JSON

    def get_fingerprint(self, level1: str, level2: str) -> str:
        """Generate unique fingerprint for this item."""
        return f"{level1}|{level2}|{self.name}"


class PopupInfo(BaseModel):
    """Information about a popup."""

    title: Optional[str] = None
    content: Optional[str] = None
    close_button: Optional[Coordinate] = None


class PageAnalysis(BaseModel):
    """Complete analysis of a screen page."""

    # Menu structure
    level1_dir: Direction
    level1_menus: list[MenuInfo]
    level2_dir: Direction
    level2_menus: list[MenuInfo]

    # Current location
    current_path: list[str]

    # Content items
    items: list[MenuItem]

    # Special elements
    is_popup: bool = False
    popup_info: Optional[PopupInfo] = None
    close_button: Optional[Coordinate] = None
    back_button: Optional[Coordinate] = None

    # Navigation hints
    has_scroll: bool = False
    is_end_of_list: bool = False


class ContentNode(BaseModel):
    """A node in the content tree."""

    id: str
    title: str
    level: int
    parent_id: Optional[str] = None
    children: list[str] = Field(default_factory=list)
    coordinate: Optional[Coordinate] = None
    node_type: str = "item"  # item, popup, jump, no_feedback
    description: Optional[str] = None
    visited: bool = False

    def to_markdown(self, include_children: bool = True) -> str:
        """Convert to markdown representation."""
        indent = "  " * (self.level - 1)
        type_suffix = f" ({self.node_type})" if self.node_type != "item" else ""
        line = f"{indent}{self.id}. {self.title}{type_suffix}\n"

        if include_children:
            # Children would be rendered by the tree traversal
            pass

        return line


class ContentTree(BaseModel):
    """Tree structure of discovered content."""

    root_title: str = "Root"
    nodes: dict[str, ContentNode] = Field(default_factory=dict)
    # Track level-specific counters for proper hierarchical IDs
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
        """Generate a hierarchical ID based on parent and level."""
        # Initialize counter for this level if needed
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

        # Build hierarchical ID: parent_id.child_number
        if not parent.children:
            # First child
            child_id = f"{parent.id}.1"
        else:
            # Next child
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

        # Simple breadth-first traversal for display
        # In production, would track proper numbering
        for node in sorted(self.nodes.values(), key=lambda n: n.id):
            lines.append(node.to_markdown(include_children=False))

        return "".join(lines)


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


class TraversalState(BaseModel):
    """Complete traversal state for persistence."""

    # Current location (program's truth)
    current_path: list[str] = Field(default_factory=list)

    # Visited tracking
    visited: set[str] = Field(default_factory=set)

    # Caches
    all_level1_menus: dict[str, MenuInfo] = Field(default_factory=dict)
    level2_menus_cache: dict[str, list[MenuInfo]] = Field(default_factory=dict)
    items_cache: dict[str, list[MenuItem]] = Field(default_factory=dict)

    # Content tree
    content_tree: ContentTree = Field(default_factory=ContentTree)

    # Progress tracking
    step_count: int = 0
    current_phase: str = "initialized"

    # Error recovery
    consecutive_errors: int = 0
    last_error: Optional[str] = None

    # Target info
    target_app: Optional[str] = None

    # Exception history (task 10.1)
    # Stored as list of serialized exception contexts
    exception_history_records: list[dict] = Field(default_factory=list, alias="_exception_history_records")

    # Graph mode support (V4.0)
    # Node stack for depth-first traversal
    node_stack: list[dict] = Field(default_factory=list, alias="_node_stack")
    # Current node being processed
    current_node_id: Optional[str] = None
    # Graph mode flag
    use_graph_mode: bool = False

    def get_current_cache_key(self) -> str:
        """Get cache key for current path."""
        if len(self.current_path) < 2:
            return "root"
        return "|".join(self.current_path[-2:])

    def is_visited(self, fingerprint: VisitFingerprint) -> bool:
        """Check if element has been visited."""
        return str(fingerprint) in self.visited

    def mark_visited(self, fingerprint: VisitFingerprint) -> None:
        """Mark element as visited."""
        self.visited.add(str(fingerprint))

    def add_level1_menu(self, menu: MenuInfo) -> None:
        """Add a level1 menu to cache."""
        self.all_level1_menus[menu.name] = menu

    def add_level2_menus(self, level1: str, menus: list[MenuInfo]) -> None:
        """Add level2 menus for a level1."""
        self.level2_menus_cache[level1] = menus

    def get_level2_menus(self, level1: str) -> list[MenuInfo]:
        """Get cached level2 menus for a level1."""
        return self.level2_menus_cache.get(level1, [])

    def add_items(self, cache_key: str, items: list[MenuItem]) -> None:
        """Add items to cache."""
        self.items_cache[cache_key] = items

    def get_items(self, cache_key: str) -> list[MenuItem]:
        """Get items from cache."""
        return self.items_cache.get(cache_key, [])

    # Exception history methods (task 10.4)

    def get_exception_history_summary(self) -> dict:
        """Get summary of exception history.

        Returns:
            Dictionary with total count, by_type, and by_severity
        """
        from collections import Counter

        if not self.exception_history_records:
            return {"total": 0, "by_type": {}, "by_severity": {}}

        by_type = Counter(r.get("exception_type", "unknown") for r in self.exception_history_records)
        by_severity = Counter(r.get("severity", "unknown") for r in self.exception_history_records)

        return {
            "total": len(self.exception_history_records),
            "by_type": dict(by_type.most_common()),
            "by_severity": dict(by_severity.most_common()),
        }

    def get_exceptions_by_type(self, exc_type: str) -> list[dict]:
        """Get exceptions of specific type.

        Args:
            exc_type: Exception type name to filter by

        Returns:
            List of exception records matching the type
        """
        return [
            r for r in self.exception_history_records
            if r.get("exception_type") == exc_type
        ]

    def get_exceptions_by_severity(self, severity: str) -> list[dict]:
        """Get exceptions of specific severity.

        Args:
            severity: Severity level to filter by

        Returns:
            List of exception records matching the severity
        """
        return [
            r for r in self.exception_history_records
            if r.get("severity") == severity
        ]
