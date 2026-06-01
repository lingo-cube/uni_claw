"""
State machine and graph model interaction.

This module provides the interface between the state machines and the graph model,
handling precondition validation, automatic navigation, and coordination.
"""

from dataclasses import dataclass, field
from typing import Any, Callable, Dict, List, Optional
from datetime import datetime

from .global_fsm import GlobalStateMachine, GlobalState
from .traversal_fsm import TraversalStateMachine, TraversalState
from .node_stack import NodeStack, StackFrame

from src.graph.node import TraversalNode


@dataclass
class TraversalContext:
    """
    Context for traversal execution.

    Contains all state needed during traversal:
    - Current path
    - Visited pages/nodes
    - Current page analysis
    - Configuration
    """

    current_path: List[str] = field(default_factory=list)
    visited_pages: Dict[str, datetime] = field(default_factory=dict)
    visited_nodes: Dict[str, datetime] = field(default_factory=dict)
    current_page_analysis: Optional[Dict[str, Any]] = None
    config: Dict[str, Any] = field(default_factory=dict)

    def mark_page_visited(self, page_name: str) -> None:
        """Mark a page as visited."""
        self.visited_pages[page_name] = datetime.now()

    def mark_node_visited(self, node_id: str) -> None:
        """Mark a node as visited."""
        self.visited_nodes[node_id] = datetime.now()

    def is_page_visited(self, page_name: str) -> bool:
        """Check if page was visited."""
        return page_name in self.visited_pages

    def is_node_visited(self, node_id: str) -> bool:
        """Check if node was visited."""
        return node_id in self.visited_nodes


@dataclass
class NavigationResult:
    """Result of automatic navigation."""

    success: bool
    actions_taken: List[str] = field(default_factory=list)
    final_path: List[str] = field(default_factory=list)
    error_message: Optional[str] = None


class StateMachineOrchestrator:
    """
    Orchestrates interaction between state machines and graph model.

    Coordinates:
    - Global and traversal state machines
    - Node stack
    - Precondition validation and navigation
    - Graph model interaction
    """

    def __init__(self, max_stack_depth: int = 10):
        """
        Initialize the orchestrator.

        Args:
            max_stack_depth: Maximum depth for node stack
        """
        self.global_fsm = GlobalStateMachine()
        self.traversal_fsm = TraversalStateMachine()
        self.node_stack = NodeStack(max_depth=max_stack_depth)
        self.context = TraversalContext()

        # Navigation callback (to be set by engine)
        self._navigation_callback: Optional[Callable] = None
        self._operation_callback: Optional[Callable] = None
        self._children_generator_callback: Optional[Callable] = None

    def register_navigation_callback(self, callback: Callable) -> None:
        """Register callback for automatic navigation."""
        self._navigation_callback = callback

    def register_operation_callback(self, callback: Callable) -> None:
        """Register callback for executing operations."""
        self._operation_callback = callback

    def register_children_generator_callback(self, callback: Callable) -> None:
        """Register callback for generating children."""
        self._children_generator_callback = callback

    # Lifecycle methods

    def initialize(self, root_node: TraversalNode) -> bool:
        """
        Initialize traversal with root node.

        Args:
            root_node: Root node to start traversal from

        Returns:
            True if initialization succeeded
        """
        try:
            # Start global initialization
            self.global_fsm.start_initialization()

            # Push root node onto stack
            self.node_stack.push(root_node)

            # Move to traversing state
            self.global_fsm.start_traversing()

            return True
        except Exception as e:
            self.global_fsm.report_error(e)
            return False

    # Precondition validation

    def validate_precondition(self, node: TraversalNode) -> bool:
        """
        Validate node precondition.

        Args:
            node: Node to validate precondition for

        Returns:
            True if precondition is satisfied or navigation succeeded
        """
        if not node.has_precondition():
            # No precondition, automatically satisfied
            return True

        precondition = node.precondition

        # Check page name condition
        if precondition.page_name:
            current_page = self._get_current_page_name()
            if current_page != precondition.page_name:
                # Need navigation
                return self._navigate_to_page(precondition.page_name, precondition.timeout_seconds)

        # Check path condition
        if precondition.path:
            if not self._check_path_condition(precondition.path):
                return self._navigate_to_path(precondition.path, precondition.timeout_seconds)

        # Check UI condition
        if precondition.ui_condition:
            return self._check_ui_condition(precondition.ui_condition)

        return True

    def _get_current_page_name(self) -> Optional[str]:
        """Get current page name from context."""
        if self.context.current_path:
            return self.context.current_path[-1]
        return None

    def _check_path_condition(self, required_path: List[str]) -> bool:
        """Check if current path matches required path."""
        current = self.context.current_path
        return len(current) >= len(required_path) and current[: len(required_path)] == required_path

    def _check_ui_condition(self, condition: str) -> bool:
        """
        Check UI condition expression.

        This is a simplified implementation. In production, this would
        evaluate the condition against current UI state.

        Args:
            condition: UI condition expression

        Returns:
            True if condition is satisfied
        """
        # For now, return True if we have page analysis
        return self.context.current_page_analysis is not None

    def _navigate_to_page(self, target_page: str, timeout: float) -> bool:
        """
        Navigate to target page.

        Args:
            target_page: Page name to navigate to
            timeout: Timeout in seconds

        Returns:
            True if navigation succeeded
        """
        if self._navigation_callback:
            result = self._navigation_callback(target_page, timeout)
            if result.success:
                self.context.current_path = result.final_path
            return result.success
        return False

    def _navigate_to_path(self, target_path: List[str], timeout: float) -> bool:
        """Navigate to target path."""
        if self._navigation_callback:
            result = self._navigation_callback(target_path, timeout)
            if result.success:
                self.context.current_path = result.final_path
            return result.success
        return False

    # Node execution

    def execute_node(self, node: TraversalNode) -> Dict[str, Any]:
        """
        Execute a node operation.

        Args:
            node: Node to execute

        Returns:
            Execution result dict
        """
        if self._operation_callback:
            return self._operation_callback(node)

        # Default fallback
        return {"success": False, "error": "No operation callback registered"}

    # Children generation

    def generate_children(self, node: TraversalNode) -> List[str]:
        """
        Generate children for a node.

        Args:
            node: Node to generate children for

        Returns:
            List of child node IDs
        """
        if self._children_generator_callback:
            return self._children_generator_callback(node, self.context.current_page_analysis)

        # Static children
        if node.children_strategy.static_children:
            return node.children_strategy.static_children

        return []

    # Flow control

    def get_next_node(self) -> Optional[TraversalNode]:
        """
        Get the next node to process.

        Implements depth-first traversal logic:
        1. Check if top frame has remaining children
        2. If yes, get next child
        3. If no, pop frame and check parent

        Returns:
            Next node to process or None if traversal complete
        """
        while not self.node_stack.is_empty:
            top_frame = self.node_stack.top()

            if top_frame and not top_frame.is_complete:
                # Get next child ID
                child_id = top_frame.get_next_child()
                if child_id:
                    # Return node (caller should fetch actual node)
                    # For now, return placeholder
                    return TraversalNode(
                        node_id=child_id,
                        name=f"Node {child_id}",
                        node_type=getattr(top_frame.node, "node_type", None),
                        operation=top_frame.node.operation,
                    )

            # Frame complete, pop it
            self.node_stack.pop()

        return None

    def should_restore(self, node: TraversalNode) -> bool:
        """
        Check if node needs restore operation.

        Args:
            node: Node to check

        Returns:
            True if restore is needed
        """
        return node.needs_restore()

    def execute_restore(self, node: TraversalNode) -> bool:
        """
        Execute restore operation for a node.

        Args:
            node: Node to restore

        Returns:
            True if restore succeeded
        """
        if not node.needs_restore():
            return True

        restore_op = node.operation.restore
        if restore_op and self._operation_callback:
            result = self._operation_callback(
                TraversalNode(
                    node_id=f"{node.node_id}_restore",
                    name=f"Restore {node.name}",
                    node_type=node.node_type,
                    operation=type(node.operation)(action=restore_op.action),
                )
            )
            return result.get("success", False)

        return False

    # State queries

    def is_traversal_complete(self) -> bool:
        """Check if traversal is complete."""
        return (
            self.global_fsm.state == GlobalState.COMPLETED
            or self.node_stack.is_empty
        )

    def get_status_summary(self) -> Dict[str, Any]:
        """
        Get comprehensive status summary.

        Returns:
            Dict with status information
        """
        return {
            "global_state": self.global_fsm.state.value,
            "traversal_state": self.traversal_fsm.state.value,
            "stack": self.node_stack.get_summary(),
            "current_path": self.context.current_path,
            "is_complete": self.is_traversal_complete(),
        }
