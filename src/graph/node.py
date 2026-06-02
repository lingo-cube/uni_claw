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
    SCREEN = "screen"  # Screen page
    ACTION = "action"  # Generic action
    TARGET = "target"  # Target node

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings."""
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "NodeType":
        """Create an enum instance from a string value."""
        try:
            return cls(value)
        except ValueError as e:
            raise ValueError(
                f"Invalid {cls.__name__} value: {value}. "
                f"Valid values: {cls.values()}"
            ) from e

    @classmethod
    def is_valid(cls, value: str) -> bool:
        """Check if a string value is a valid enum value."""
        return value in cls.values()


# ============================================================================
# V6 New Enum Types
# ============================================================================


class ExitConditionType(str, Enum):
    """Container node exit condition types."""

    ALL_CHILDREN_VISITED = "all_children_visited"  # Wait for all children to be processed
    DEPTH_LIMITED = "depth_limited"  # Exit when max depth is reached
    SINGLE_LEVEL = "single_level"  # Only process direct children, no recursion

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings."""
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "ExitConditionType":
        """Create an enum instance from a string value."""
        try:
            return cls(value)
        except ValueError as e:
            raise ValueError(
                f"Invalid {cls.__name__} value: {value}. "
                f"Valid values: {cls.values()}"
            ) from e

    @classmethod
    def is_valid(cls, value: str) -> bool:
        """Check if a string value is a valid enum value."""
        return value in cls.values()


class FallbackAction(str, Enum):
    """Action to perform when exiting a container."""

    BACK = "back"  # Press Back key to pop current frame
    AUTO_ESCAPE = "auto_escape"  # Try clicking sibling menu, or Back if none
    SKIP = "skip"  # Skip without executing Back, just pop frame
    ABORT = "abort"  # Abort the entire traversal

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings."""
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "FallbackAction":
        """Create an enum instance from a string value."""
        try:
            return cls(value)
        except ValueError as e:
            raise ValueError(
                f"Invalid {cls.__name__} value: {value}. "
                f"Valid values: {cls.values()}"
            ) from e

    @classmethod
    def is_valid(cls, value: str) -> bool:
        """Check if a string value is a valid enum value."""
        return value in cls.values()


class CompletionPolicyType(str, Enum):
    """Global traversal completion policy types."""

    NONE = "none"  # Run until natural completion (stack empty)
    TARGET_FOUND = "target_found"  # Terminate when target is found
    TIMEOUT = "timeout"  # Terinate after timeout
    MAX_STEPS = "max_steps"  # Terminate after max steps

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings."""
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "CompletionPolicyType":
        """Create an enum instance from a string value."""
        try:
            return cls(value)
        except ValueError as e:
            raise ValueError(
                f"Invalid {cls.__name__} value: {value}. "
                f"Valid values: {cls.values()}"
            ) from e

    @classmethod
    def is_valid(cls, value: str) -> bool:
        """Check if a string value is a valid enum value."""
        return value in cls.values()


class TargetFoundAction(str, Enum):
    """Action to perform when target is found."""

    MARK_AND_STOP = "mark_and_stop"  # Mark target and immediately terminate
    EXECUTE_THEN_STOP = "execute_then_stop"  # Execute operation then terminate

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings."""
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "TargetFoundAction":
        """Create an enum instance from a string value."""
        try:
            return cls(value)
        except ValueError as e:
            raise ValueError(
                f"Invalid {cls.__name__} value: {value}. "
                f"Valid values: {cls.values()}"
            ) from e

    @classmethod
    def is_valid(cls, value: str) -> bool:
        """Check if a string value is a valid enum value."""
        return value in cls.values()


class MatchMode(str, Enum):
    """Text matching mode for target search."""

    EXACT = "exact"  # Exact match
    CONTAINS = "contains"  # Contains match

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings."""
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "MatchMode":
        """Create an enum instance from a string value."""
        try:
            return cls(value)
        except ValueError as e:
            raise ValueError(
                f"Invalid {cls.__name__} value: {value}. "
                f"Valid values: {cls.values()}"
            ) from e

    @classmethod
    def is_valid(cls, value: str) -> bool:
        """Check if a string value is a valid enum value."""
        return value in cls.values()


class EntryStrategy(str, Enum):
    """Strategy for entering the target application."""

    COLD_LAUNCH = "cold_launch"  # Find and click app icon from home screen
    DIRECT_DEEPLINK = "direct_deeplink"  # Use adb/am start via Intent
    BIND_CURRENT_SCREEN = "bind_current_screen"  # Assume already on target screen

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings."""
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "EntryStrategy":
        """Create an enum instance from a string value."""
        try:
            return cls(value)
        except ValueError as e:
            raise ValueError(
                f"Invalid {cls.__name__} value: {value}. "
                f"Valid values: {cls.values()}"
            ) from e

    @classmethod
    def is_valid(cls, value: str) -> bool:
        """Check if a string value is a valid enum value."""
        return value in cls.values()


class TraversalMode(str, Enum):
    """Traversal execution mode."""

    HYBRID = "hybrid"  # Hybrid mode (static + dynamic)
    CONCRETE = "concrete"  # Concrete mode (predefined static paths only)
    ABSTRACT = "abstract"  # Abstract mode (fully dynamic generation)

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings."""
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "TraversalMode":
        """Create an enum instance from a string value."""
        try:
            return cls(value)
        except ValueError as e:
            raise ValueError(
                f"Invalid {cls.__name__} value: {value}. "
                f"Valid values: {cls.values()}"
            ) from e

    @classmethod
    def is_valid(cls, value: str) -> bool:
        """Check if a string value is a valid enum value."""
        return value in cls.values()

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

    on_error: str  # "retry", "skip", "abort", "fallback", "backtrack"
    max_retries: int = 1  # For "retry" action
    fallback_target: Optional[str] = None  # For "fallback" action
    continue_on_error: bool = False  # If True, continue traversal even on error

    def __post_init__(self):
        """Validate error policy configuration."""
        valid_actions = {"retry", "skip", "abort", "fallback", "backtrack"}
        if self.on_error not in valid_actions:
            raise ValueError(f"Invalid on_error: {self.on_error}. Must be one of {valid_actions}")
        if self.max_retries < 0:
            raise ValueError(f"max_retries cannot be negative, got {self.max_retries}")
        if self.max_retries > 100:
            raise ValueError(f"max_retries cannot exceed 100, got {self.max_retries}")


# ============================================================================
# V6 New Data Classes
# ============================================================================


@dataclass
class ExitCondition:
    """
    Container node exit condition.

    Defines when and how to exit a container node during traversal.
    """

    type: ExitConditionType  # Exit condition type
    fallback: FallbackAction = FallbackAction.BACK  # Action to perform on exit
    max_depth: Optional[int] = None  # For DEPTH_LIMITED type

    def __post_init__(self):
        """Validate exit condition configuration."""
        if self.type == ExitConditionType.DEPTH_LIMITED and self.max_depth is None:
            raise ValueError("max_depth must be specified for DEPTH_LIMITED type")
        if self.max_depth is not None and self.max_depth <= 0:
            raise ValueError(f"max_depth must be positive, got {self.max_depth}")
        if self.max_depth is not None and self.max_depth > 1000:
            raise ValueError(f"max_depth cannot exceed 1000, got {self.max_depth}")


@dataclass
class CompletionPolicy:
    """
    Global traversal completion policy.

    Defines when to terminate the entire traversal.
    """

    type: CompletionPolicyType = CompletionPolicyType.NONE  # Completion policy type
    target_name: Optional[str] = None  # For TARGET_FOUND type
    match_mode: MatchMode = MatchMode.CONTAINS  # Text matching mode
    action_on_found: TargetFoundAction = TargetFoundAction.MARK_AND_STOP  # Action when target found
    timeout_seconds: Optional[float] = None  # For TIMEOUT type
    max_steps: Optional[int] = None  # For MAX_STEPS type

    def __post_init__(self):
        """Validate completion policy configuration."""
        if self.type == CompletionPolicyType.TARGET_FOUND and not self.target_name:
            raise ValueError("target_name must be specified for TARGET_FOUND type")
        if self.type == CompletionPolicyType.TIMEOUT and self.timeout_seconds is None:
            raise ValueError("timeout_seconds must be specified for TIMEOUT type")
        if self.type == CompletionPolicyType.MAX_STEPS and self.max_steps is None:
            raise ValueError("max_steps must be specified for MAX_STEPS type")
        if self.timeout_seconds is not None and self.timeout_seconds <= 0:
            raise ValueError(f"timeout_seconds must be positive, got {self.timeout_seconds}")
        if self.timeout_seconds is not None and self.timeout_seconds > 86400:  # 24 hours
            raise ValueError(f"timeout_seconds cannot exceed 86400, got {self.timeout_seconds}")
        if self.max_steps is not None and self.max_steps <= 0:
            raise ValueError(f"max_steps must be positive, got {self.max_steps}")
        if self.max_steps is not None and self.max_steps > 1000000:
            raise ValueError(f"max_steps cannot exceed 1000000, got {self.max_steps}")


@dataclass
class EntryPolicy:
    """
    Application entry policy.

    Defines how to enter the target application.
    """

    strategy: EntryStrategy = EntryStrategy.COLD_LAUNCH  # Entry strategy
    fallback: Optional[str] = None  # Fallback entry if primary fails
    wait_condition: Optional[Dict[str, Any]] = None  # Expected screen state after entry
    timeout_seconds: float = 10.0  # Timeout for entry operation

    def __post_init__(self):
        """Validate entry policy configuration."""
        if self.timeout_seconds <= 0:
            raise ValueError(f"timeout_seconds must be positive, got {self.timeout_seconds}")
        if self.timeout_seconds > 300:  # 5 minutes
            raise ValueError(f"timeout_seconds cannot exceed 300, got {self.timeout_seconds}")


@dataclass
class IntentSlots:
    """
    AI-extracted intent slots.

    Stores structured information extracted from natural language commands.
    All fields are optional as AI may only partially extract intent.
    """

    target_app: Optional[str] = None  # Target application name
    scope: Optional[str] = None  # Traversal scope: "full", "partial", "target_only"
    target: Optional[str] = None  # Specific target (e.g., "version number")
    depth: Optional[int] = None  # Maximum traversal depth
    element_handling: Optional[str] = None  # Element handling strategy
    navigation: Optional[str] = None  # Navigation strategy
    restore: Optional[bool] = None  # Whether to restore state
    completion: Optional[str] = None  # Completion criteria

    def __post_init__(self):
        """Validate intent slots configuration."""
        if self.depth is not None and self.depth <= 0:
            raise ValueError(f"depth must be positive, got {self.depth}")
        if self.depth is not None and self.depth > 1000:
            raise ValueError(f"depth cannot exceed 1000, got {self.depth}")
        if self.scope is not None and self.scope not in {"full", "partial", "target_only"}:
            raise ValueError(f"Invalid scope: {self.scope}. Must be one of: full, partial, target_only")


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
        exit_condition: V6: Optional exit condition for container nodes
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
    exit_condition: Optional[ExitCondition] = None  # V6: Exit condition for containers
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
        # A node is leaf if it has no children strategy or strategy type is NONE
        from src.graph.node import ChildrenStrategyType
        if not self.children_strategy:
            return True
        return self.children_strategy.type == ChildrenStrategyType.NONE

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
