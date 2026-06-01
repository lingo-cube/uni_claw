"""
Traversal state machine for individual node execution.

This module implements the traversal state machine that handles the
execution flow for individual nodes.
"""

from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Callable, Dict, List, Optional
from datetime import datetime


class TraversalState(str, Enum):
    """States in the traversal state machine."""

    NODE_SELECT = "node_select"  # Select next node to process
    PRECONDITION_CHECK = "precondition_check"  # Verify precondition
    EXECUTE = "execute"  # Execute node operation
    RESULT_VERIFY = "result_verify"  # Verify execution result
    BRANCH = "branch"  # Determine next action (children, return, error)

    @classmethod
    def values(cls) -> list[str]:
        """Get all enum values as a list of strings.

        Returns:
            List of enum values
        """
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "TraversalState":
        """Create an enum instance from a string value.

        Args:
            value: String value to convert

        Returns:
            TraversalState enum instance

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
class TraversalStateTransition:
    """Record of a traversal state transition."""

    from_state: TraversalState
    to_state: TraversalState
    timestamp: datetime = field(default_factory=datetime.now)
    node_id: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


class TraversalStateMachine:
    """
    Traversal state machine for node execution flow.

    Manages the state transitions for processing individual nodes,
    coordinating with the global state machine and node stack.
    """

    # Valid state transitions
    VALID_TRANSITIONS = {
        TraversalState.NODE_SELECT: {TraversalState.PRECONDITION_CHECK, TraversalState.BRANCH},
        TraversalState.PRECONDITION_CHECK: {TraversalState.EXECUTE, TraversalState.BRANCH},
        TraversalState.EXECUTE: {TraversalState.RESULT_VERIFY, TraversalState.BRANCH},
        TraversalState.RESULT_VERIFY: {TraversalState.BRANCH},
        TraversalState.BRANCH: {TraversalState.NODE_SELECT, TraversalState.PRECONDITION_CHECK},
    }

    def __init__(self):
        """Initialize the traversal state machine."""
        self._state = TraversalState.NODE_SELECT
        self._transition_history: List[TraversalStateTransition] = []
        self._current_node_id: Optional[str] = None
        self._execution_result: Optional[Dict[str, Any]] = None
        self._precondition_result: Optional[bool] = None

    @property
    def state(self) -> TraversalState:
        """Get current state."""
        return self._state

    @property
    def current_node_id(self) -> Optional[str]:
        """Get current node ID being processed."""
        return self._current_node_id

    @property
    def execution_result(self) -> Optional[Dict[str, Any]]:
        """Get execution result from last EXECUTE state."""
        return self._execution_result

    @property
    def precondition_result(self) -> Optional[bool]:
        """Get precondition check result."""
        return self._precondition_result

    def can_transition_to(self, target_state: TraversalState) -> bool:
        """
        Check if transition to target state is valid.

        Args:
            target_state: Desired target state

        Returns:
            True if transition is valid
        """
        return target_state in self.VALID_TRANSITIONS.get(self._state, set())

    def transition_to(
        self, target_state: TraversalState, node_id: Optional[str] = None, **metadata
    ) -> bool:
        """
        Transition to target state.

        Args:
            target_state: Desired target state
            node_id: Current node ID (if applicable)
            **metadata: Optional metadata

        Returns:
            True if transition succeeded

        Raises:
            ValueError: If transition is invalid
        """
        if not self.can_transition_to(target_state):
            raise ValueError(
                f"Invalid transition from {self._state} to {target_state}. "
                f"Valid transitions: {self.VALID_TRANSITIONS.get(self._state, set())}"
            )

        # Record transition
        transition = TraversalStateTransition(
            from_state=self._state,
            to_state=target_state,
            node_id=node_id or self._current_node_id,
            metadata=metadata,
        )
        self._transition_history.append(transition)

        # Update state
        self._state = target_state
        if node_id:
            self._current_node_id = node_id

        return True

    def set_current_node(self, node_id: str) -> None:
        """
        Set the current node being processed.

        Args:
            node_id: ID of the node to process
        """
        self._current_node_id = node_id

    def set_execution_result(self, result: Dict[str, Any]) -> None:
        """
        Set execution result after EXECUTE state.

        Args:
            result: Execution result data
        """
        self._execution_result = result

    def set_precondition_result(self, satisfied: bool) -> None:
        """
        Set precondition check result.

        Args:
            satisfied: Whether precondition was satisfied
        """
        self._precondition_result = satisfied

    # State-specific methods

    def start_node_select(self, node_id: str) -> bool:
        """
        Start processing a new node.

        Args:
            node_id: ID of node to process
        """
        return self.transition_to(TraversalState.NODE_SELECT, node_id=node_id)

    def start_precondition_check(self) -> bool:
        """Start precondition check for current node."""
        return self.transition_to(TraversalState.PRECONDITION_CHECK)

    def precondition_failed(self) -> bool:
        """Handle precondition check failure."""
        return self.transition_to(
            TraversalState.BRANCH,
            reason="precondition_not_satisfied",
        )

    def start_execute(self) -> bool:
        """Start executing node operation."""
        return self.transition_to(TraversalState.EXECUTE)

    def execution_failed(self, error: Exception) -> bool:
        """
        Handle execution failure.

        Args:
            error: Exception that occurred
        """
        return self.transition_to(
            TraversalState.BRANCH,
            reason="execution_failed",
            error=str(error),
        )

    def start_result_verify(self) -> bool:
        """Start result verification."""
        return self.transition_to(TraversalState.RESULT_VERIFY)

    def branch_to_children(self) -> bool:
        """Branch to generate/process children."""
        return self.transition_to(
            TraversalState.BRANCH,
            reason="processing_children",
        )

    def branch_to_restore(self) -> bool:
        """Branch to restore leaf node state."""
        return self.transition_to(
            TraversalState.BRANCH,
            reason="restoring_leaf",
        )

    def branch_to_parent(self) -> bool:
        """Branch to return to parent node."""
        return self.transition_to(
            TraversalState.BRANCH,
            reason="returning_to_parent",
        )

    def branch_to_next_node(self) -> bool:
        """Branch to select next node."""
        return self.transition_to(TraversalState.NODE_SELECT)

    def branch_to_precondition(self) -> bool:
        """Branch back to precondition check (e.g., after navigation)."""
        return self.transition_to(TraversalState.PRECONDITION_CHECK)

    def get_transition_history(self) -> List[TraversalStateTransition]:
        """Get list of all state transitions."""
        return self._transition_history.copy()

    def reset(self) -> None:
        """Reset state machine (for new node)."""
        self._state = TraversalState.NODE_SELECT
        self._current_node_id = None
        self._execution_result = None
        self._precondition_result = None
        # Keep transition history for debugging
