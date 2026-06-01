"""
Node data classes for the graph model.

This module defines the unified node abstraction for traversal operations,
including TraversalNode and all associated data classes.
"""

from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Dict, List, Optional


class NodeType(str, Enum):
    """Types of traversal nodes."""

    CONTAINER = "container"  # Can expand to show children (e.g., menu items)
    LEAF_SWITCH = "leaf_switch"  # Switch/toggle control
    LEAF_SLIDER = "leaf_slider"  # Slider control
    LEAF_ACTION = "leaf_action"  # Action button (one-time operation)
    LEAF_INFO = "leaf_info"  # Information display (no operation)

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings.

        Returns:
            List of enum values
        """
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "NodeType":
        """Create an enum instance from a string value.

        Args:
            value: String value to convert

        Returns:
            NodeType enum instance

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


@dataclass
class Target:
    """
    Target specification for locating UI elements.

    Supports multiple targeting strategies:
    - text: Locate by text content
    - coordinate: Locate by normalized (0-1) coordinates
    - ui_index: Locate by UI list index
    """

    by: str  # "text", "coordinate", "ui_index"
    value: Any  # The actual value (str for text, tuple for coordinate, int for index)
    meta: Dict[str, Any] = field(default_factory=dict)

    def __post_init__(self):
        """Validate target configuration."""
        valid_by = {"text", "coordinate", "ui_index"}
        if self.by not in valid_by:
            raise ValueError(f"Invalid 'by': {self.by}. Must be one of {valid_by}")


@dataclass
class RestoreAction:
    """
    Restore operation to return to previous state after leaf node operation.

    Example: After toggling a switch, restore it back to original state.
    """

    action: str  # Same as Operation.action (click, swipe, back, input_text, no_action)
    target: Optional[Target] = None
    params: Dict[str, Any] = field(default_factory=dict)

    def __post_init__(self):
        """Validate restore action configuration."""
        valid_actions = {"click", "swipe", "back", "input_text", "no_action"}
        if self.action not in valid_actions:
            raise ValueError(f"Invalid action: {self.action}. Must be one of {valid_actions}")


@dataclass
class Operation:
    """
    Operation to execute on a node.

    Defines what action to perform, which element to target, and optional parameters.
    """

    action: str  # "click", "swipe", "back", "input_text", "no_action"
    target: Optional[Target] = None
    params: Dict[str, Any] = field(default_factory=dict)
    restore: Optional[RestoreAction] = None  # Optional restore action

    def __post_init__(self):
        """Validate operation configuration."""
        valid_actions = {"click", "swipe", "back", "input_text", "no_action"}
        if self.action not in valid_actions:
            raise ValueError(f"Invalid action: {self.action}. Must be one of {valid_actions}")


@dataclass
class Precondition:
    """
    Precondition that must be satisfied before executing a node.

    If the condition is not met, the system will attempt automatic navigation
    (e.g., continuous back presses) to satisfy the condition.
    """

    page_name: Optional[str] = None  # Required current page name
    path: Optional[List[str]] = None  # Required full path from root
    ui_condition: Optional[str] = None  # UI condition expression
    timeout_seconds: float = 5.0  # Max time to wait for condition satisfaction

    def __post_init__(self):
        """Validate precondition configuration."""
        if self.timeout_seconds <= 0:
            raise ValueError(f"timeout_seconds must be positive, got {self.timeout_seconds}")
        if self.timeout_seconds > 300:  # 5 minutes max
            raise ValueError(f"timeout_seconds cannot exceed 300 seconds, got {self.timeout_seconds}")


class ChildrenStrategyType(str, Enum):
    """Strategy type for generating children nodes."""

    STATIC = "static"  # Use pre-defined static children list
    DYNAMIC_MATCH = "dynamic_match"  # Dynamically match UI elements to templates
    NONE = "none"  # Leaf node, no children

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings.

        Returns:
            List of enum values
        """
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "ChildrenStrategyType":
        """Create an enum instance from a string value.

        Args:
            value: String value to convert

        Returns:
            ChildrenStrategyType enum instance

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


@dataclass
class DynamicRule:
    """
    Rule for dynamically matching UI elements to templates.

    Used in DYNAMIC_MATCH children strategy to determine which template
    to instantiate for each discovered UI element.
    """

    rule_id: str
    match_condition: Dict[str, Any]  # Condition to match MenuItem attributes
    child_template: str  # Template ID to instantiate on match
    action: str = "generate_child"  # "generate_child", "skip", "execute_inline"

    def __post_init__(self):
        """Validate dynamic rule configuration."""
        if not self.rule_id:
            raise ValueError("rule_id cannot be empty")
        if not self.child_template:
            raise ValueError("child_template cannot be empty")
        valid_actions = {"generate_child", "skip", "execute_inline"}
        if self.action not in valid_actions:
            raise ValueError(f"Invalid action: {self.action}. Must be one of {valid_actions}")


@dataclass
class ChildrenStrategy:
    """
    Strategy for generating child nodes.

    Defines how to obtain or generate the list of child node IDs.
    """

    type: ChildrenStrategyType  # STATIC, DYNAMIC_MATCH, or NONE
    static_children: List[str] = field(default_factory=list)  # For STATIC type
    dynamic_rules: Dict[str, DynamicRule] = field(default_factory=dict)  # For DYNAMIC_MATCH
    max_children: int = 100  # Maximum children to generate (safety limit)

    def __post_init__(self):
        """Validate children strategy configuration."""
        if self.max_children < 0:
            raise ValueError(f"max_children cannot be negative, got {self.max_children}")
        if self.max_children > 10000:
            raise ValueError(f"max_children cannot exceed 10000, got {self.max_children}")


@dataclass
class ErrorPolicy:
    """
    Policy for handling errors during node execution.

    Defines what to do when an operation fails.
    """

    on_error: str  # "retry", "skip", "abort", "fallback"
    max_retries: int = 1  # For "retry" action
    fallback_target: Optional[str] = None  # For "fallback" action
    continue_on_error: bool = False  # If True, continue traversal even on error

    def __post_init__(self):
        """Validate error policy configuration."""
        valid_actions = {"retry", "skip", "abort", "fallback"}
        if self.on_error not in valid_actions:
            raise ValueError(f"Invalid on_error: {self.on_error}. Must be one of {valid_actions}")
        if self.max_retries < 0:
            raise ValueError(f"max_retries cannot be negative, got {self.max_retries}")
        if self.max_retries > 100:
            raise ValueError(f"max_retries cannot exceed 100, got {self.max_retries}")


@dataclass
class TraversalNode:
    """
    Unified node abstraction for traversal operations.

    Represents a single node in the traversal graph, with complete information
    about what to do, when to do it, and how to handle results.

    Attributes:
        node_id: Unique identifier for this node
        name: Human-readable display name
        node_type: Type of node (container or leaf)
        operation: The operation to execute
        precondition: Optional precondition to check before execution
        children_strategy: How to generate child nodes
        error_policy: Optional error handling policy
        meta: Runtime metadata (state, annotations, etc.)
    """

    node_id: str
    name: str
    node_type: NodeType
    operation: Operation
    precondition: Optional[Precondition] = None
    children_strategy: ChildrenStrategy = field(
        default_factory=lambda: ChildrenStrategy(type=ChildrenStrategyType.NONE)
    )
    error_policy: Optional[ErrorPolicy] = None
    meta: Dict[str, Any] = field(default_factory=dict)

    def __post_init__(self):
        """Validate node configuration."""
        if not self.node_id:
            raise ValueError("node_id cannot be empty")
        if not self.name:
            raise ValueError("name cannot be empty")

        # Validate node_type and children_strategy compatibility
        if self.node_type == NodeType.CONTAINER:
            if self.children_strategy.type == ChildrenStrategyType.NONE:
                raise ValueError(f"Container node {self.node_id} must have children strategy")
        else:
            # Leaf nodes typically have no children
            if self.children_strategy.type != ChildrenStrategyType.NONE:
                # This is not necessarily an error - some leaf nodes might have children
                # but we log a warning
                pass

    def is_container(self) -> bool:
        """Check if this node is a container (can have children)."""
        return self.node_type == NodeType.CONTAINER

    def is_leaf(self) -> bool:
        """Check if this node is a leaf (terminal operation)."""
        return self.node_type != NodeType.CONTAINER

    def has_precondition(self) -> bool:
        """Check if this node has a precondition to verify."""
        return self.precondition is not None

    def needs_restore(self) -> bool:
        """Check if this node needs a restore operation after execution."""
        return self.operation.restore is not None

    def get_child_count(self) -> int:
        """Get the number of static children."""
        if self.children_strategy.type == ChildrenStrategyType.STATIC:
            return len(self.children_strategy.static_children)
        return 0

    def get_meta(self, key: str, default: Any = None) -> Any:
        """Get a metadata value."""
        return self.meta.get(key, default)

    def set_meta(self, key: str, value: Any) -> None:
        """Set a metadata value."""
        self.meta[key] = value
