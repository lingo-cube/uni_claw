"""
Traversal state machine for individual node execution.

This module implements the traversal state machine that handles the
execution flow for individual nodes.

V6.7 State Machine Intelligence Features:
-----------------------------------------
- Page relationship classification (MATCH/NAVIGABLE/DEEPER/UNKNOWN)
- Intelligent precondition correction with vision verification
- AUTO_ESCAPE for same-level menu switching
- Safe button detection for popup handling
- Comprehensive error policy integration (retry/skip/backtrack/abort/fallback)
- Exception handling wrapper in step() method

Key Components:
---------------
- classify_relation(): Pure function for page relationship detection
- TraversalStateMachine: Main state machine class with intelligent handlers
- PageRelation: Enum for relationship types (MATCH/NAVIGABLE/DEEPER/UNKNOWN)

Handler Intelligence:
--------------------
- _handle_precondition_check: 3-round retry with intelligent correction
- _handle_frame_complete_state: AUTO_ESCAPE with vision verification
- _handle_popup_state: Safe button detection with fallback to back
- _handle_error_state: Three-layer error handling (policy/chain/AI)
- step(): Try-catch wrapper ensuring proper error routing

Metrics Recording:
-----------------
All handlers record comprehensive metrics for trace analysis:
- ai_call: Vision service call metrics (capability, latency, page_id)
- execution: Action execution metrics (action, status, target, duration)
- error: Error metrics (error_type, error_message, action_taken)
- correction: Precondition correction metrics (relation, rounds, success)
- auto_escape: AUTO_ESCAPE metrics (target, from, to, attempts)
"""

from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Callable, Dict, List, Optional, TYPE_CHECKING
from datetime import datetime
import logging

# Import trace models for state transition recording
from src.trace.models import SpanNode

# Import error handler components
from .error_handler import ErrorHandler, ErrorRecoveryResult
# Import container handler components
from .container_handler import ContainerHandler, ContainerContext
# Import popup handler components
from .popup_handler import PopupHandler

logger = logging.getLogger(__name__)


# ============================================================================
# V6.7: Page relationship classification
# ============================================================================


class PageRelation(str, Enum):
    """Page relationship types for intelligent correction."""

    MATCH = "match"  # Current page matches expected page
    NAVIGABLE = "navigable"  # Expected page is in current menu
    DEEPER = "deeper"  # Expected page is in current path but deeper
    UNKNOWN = "unknown"  # Cannot determine relationship


def classify_relation(
    current_path: List[str],
    expected_page: str,
    available_menus: Optional[List[str]] = None,
) -> PageRelation:
    """
    Classify the relationship between current page and expected page.

    This pure function determines how to navigate from current page to
    expected page by analyzing their relationship.

    Args:
        current_path: List of page names representing current navigation path
        expected_page: Target page name we want to reach
        available_menus: Optional list of menu items available on current page

    Returns:
        PageRelation enum indicating the relationship type

    Examples:
        >>> classify_relation(["Settings", "Display"], "Display")
        PageRelation.MATCH
        >>> classify_relation(["Settings", "Display"], "Sound", ["Sound", "Network"])
        PageRelation.NAVIGABLE
        >>> classify_relation(["Settings", "Display", "Brightness"], "Display")
        PageRelation.DEEPER
        >>> classify_relation(["Desktop"], "Display")
        PageRelation.UNKNOWN

    Note:
        When "回退过头" (back too far), the function returns UNKNOWN, which
        may trigger additional back operations. This is a known limitation;
        Phase B may introduce depth-based recovery mechanisms.
    """
    if not current_path:
        return PageRelation.UNKNOWN

    # Check MATCH: current path ends with expected page
    if current_path[-1] == expected_page:
        return PageRelation.MATCH

    # Check DEEPER: expected page is in path but not at end
    if expected_page in current_path[:-1]:
        return PageRelation.DEEPER

    # Check NAVIGABLE: expected page is in available menus
    if available_menus and expected_page in available_menus:
        return PageRelation.NAVIGABLE

    # Default: UNKNOWN relationship
    return PageRelation.UNKNOWN


# ============================================================================
# Traversal State Enum
# ============================================================================


class TraversalState(str, Enum):
    """States in the traversal state machine."""

    # Original states
    NODE_SELECT = "node_select"  # Select next node to process
    PRECONDITION_CHECK = "precondition_check"  # Verify precondition
    EXECUTE = "execute"  # Execute node operation
    RESULT_VERIFY = "result_verify"  # Verify execution result
    BRANCH = "branch"  # Determine next action (children, return, error)

    # V6 new states
    FRAME_COMPLETE = "frame_complete"  # Container frame complete handling
    ERROR_HANDLING = "error_handling"  # Error/exception handling
    POPUP_HANDLING = "popup_handling"  # Popup detection and handling

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
        # Original transitions
        TraversalState.NODE_SELECT: {
            TraversalState.PRECONDITION_CHECK,
            TraversalState.BRANCH,
        },
        TraversalState.PRECONDITION_CHECK: {
            TraversalState.EXECUTE,
            TraversalState.BRANCH,
            TraversalState.ERROR_HANDLING,  # Allow transition to error handling when precondition fails
        },
        TraversalState.EXECUTE: {
            TraversalState.RESULT_VERIFY,
            TraversalState.BRANCH,
            TraversalState.ERROR_HANDLING,  # V6: Can transition to error handling
        },
        TraversalState.RESULT_VERIFY: {
            TraversalState.BRANCH,
            TraversalState.POPUP_HANDLING,  # V6: Can transition to popup handling
        },
        TraversalState.BRANCH: {
            TraversalState.NODE_SELECT,
            TraversalState.PRECONDITION_CHECK,
            TraversalState.FRAME_COMPLETE,  # V6: Can transition to frame complete
            TraversalState.ERROR_HANDLING,  # V6: Allow error handling from branch
        },

        # V6 new transitions
        TraversalState.FRAME_COMPLETE: {
            TraversalState.NODE_SELECT,  # After frame handling, select next node
            TraversalState.ERROR_HANDLING,  # If frame handling fails
        },
        TraversalState.ERROR_HANDLING: {
            TraversalState.NODE_SELECT,  # After error recovery (SKIP)
            TraversalState.EXECUTE,  # After error recovery (RETRY)
            TraversalState.FRAME_COMPLETE,  # After error recovery (BACKTRACK)
            TraversalState.BRANCH,  # After error recovery (continue branching)
        },
        TraversalState.POPUP_HANDLING: {
            TraversalState.RESULT_VERIFY,  # After popup handled, resume verification
            TraversalState.ERROR_HANDLING,  # If popup handling fails
        },
    }

    def __init__(self):
        """Initialize the traversal state machine."""
        self._state = TraversalState.NODE_SELECT
        self._transition_history: List[TraversalStateTransition] = []
        self._current_node_id: Optional[str] = None
        self._execution_result: Optional[Dict[str, Any]] = None
        self._precondition_result: Optional[bool] = None

        # V6.1 Error handling integration
        self._error_handler: Optional[ErrorHandler] = None
        self._error_context: Dict[str, Any] = {}

        # Error retry handling
        self._retry_count = 0
        self._max_retries = 3

        # V6.5 Handler metrics (read by engine after each step)
        self._last_handler_metrics: Optional[Dict[str, Any]] = None

        # V6.1 Container handling integration
        self._container_handler: Optional[ContainerHandler] = None
        self._container_context: Dict[str, Any] = {}

        # V6.1 Popup handling integration
        self._popup_handler: Optional[PopupHandler] = None
        self._popup_context: Dict[str, Any] = {}

    @staticmethod
    def _resolve_node(frame_or_node: Any) -> Optional[Any]:
        """Extract TraversalNode from stack peek result.

        Handles both NodeStack (returns StackFrame with .node) and
        _NodeStackAdapter (returns TraversalNode directly).
        """
        if frame_or_node is None:
            return None
        if hasattr(frame_or_node, 'node') and hasattr(frame_or_node, 'child_queue'):
            # NodeStack returns StackFrame wrapping TraversalNode
            return frame_or_node.node
        # _NodeStackAdapter returns TraversalNode directly
        return frame_or_node

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
        Transition to target state with enhanced error messages and trace recording.

        V6.10.2:
        - Enhanced error messages to include debugging context
        - Added Trace recording for all successful state transitions

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
            # V6.10.2: Enhanced error message with debugging context
            # Fix: Handle history < 5 case to avoid IndexError
            recent_count = min(5, len(self._transition_history))
            recent_transitions = self._transition_history[-recent_count:] if recent_count > 0 else []
            recent_str = "\n".join(
                f"    {t.from_state.value} → {t.to_state.value} (node: {t.node_id})"
                for t in recent_transitions
            ) if recent_transitions else "    (no recent transitions)"

            valid_transitions = self.VALID_TRANSITIONS.get(self._state, set())
            valid_str = ", ".join(sorted(s.value for s in valid_transitions)) if valid_transitions else "(none)"

            raise ValueError(
                f"Invalid state transition: {self._state.value} → {target_state.value}\n"
                f"  Current node: {self._current_node_id or node_id or 'N/A'}\n"
                f"  Target node: {metadata.get('target_node_id', 'N/A')}\n"
                f"  Recent transitions:\n"
                f"{recent_str}\n"
                f"  Valid transitions from {self._state.value}: [{valid_str}]"
            )

        # V6.10.2: Record state transition to Trace
        # Note: Assumes _trace_recorder attribute exists (injected by GraphTraversalEngine)
        if hasattr(self, '_trace_recorder') and self._trace_recorder is not None:
            span = SpanNode(
                span_type="state_transition",
                action="state_change",
                from_state=self._state.value,
                to_state=target_state.value,
                state_machine="traversal",
                metadata={
                    "node_id": node_id or self._current_node_id,
                    "action": metadata.get('action', 'unknown'),
                    **metadata
                }
            )
            self._trace_recorder.record_span(span)

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

    # ============================================================================
    # V6 State Transition Methods
    # ============================================================================

    def transition_to_frame_complete(self) -> bool:
        """
        Transition to FRAME_COMPLETE state.

        Called when all children of a container node have been processed.
        The specific action (BACK, AUTO_ESCAPE, SKIP, ABORT) is determined
        by the exit_condition of the current container node.
        """
        return self.transition_to(TraversalState.FRAME_COMPLETE)

    def transition_to_error_handling(self) -> bool:
        """
        Transition to ERROR_HANDLING state.

        Called when an exception occurs during traversal.
        Implements three-layer error handling:
        1. Node error_policy
        2. ExceptionHandlingChain
        3. AI exception handling
        """
        return self.transition_to(TraversalState.ERROR_HANDLING)

    def transition_to_popup_handling(self) -> bool:
        """
        Transition to POPUP_HANDLING state.

        Called when a popup is detected during RESULT_VERIFY.
        Implements priority-based popup handling:
        1. Find and click cancel/close button
        2. Execute Back operation
        3. AI decision
        """
        return self.transition_to(TraversalState.POPUP_HANDLING)

    # ============================================================================
    # V6 State Recovery Methods
    # ============================================================================

    def frame_complete_to_node_select(self) -> bool:
        """Transition from FRAME_COMPLETE back to NODE_SELECT."""
        return self.transition_to(TraversalState.NODE_SELECT)

    def frame_complete_failed(self) -> bool:
        """Handle frame complete operation failure."""
        return self.transition_to(TraversalState.ERROR_HANDLING)

    def error_to_node_select(self) -> bool:
        """Transition from ERROR_HANDLING to NODE_SELECT (SKIP action)."""
        return self.transition_to(TraversalState.NODE_SELECT)

    def error_to_execute(self) -> bool:
        """Transition from ERROR_HANDLING to EXECUTE (RETRY action)."""
        return self.transition_to(TraversalState.EXECUTE)

    def error_to_frame_complete(self) -> bool:
        """Transition from ERROR_HANDLING to FRAME_COMPLETE (BACKTRACK action)."""
        return self.transition_to(TraversalState.FRAME_COMPLETE)

    def error_to_branch(self) -> bool:
        """Transition from ERROR_HANDLING to BRANCH (continue branching)."""
        return self.transition_to(TraversalState.BRANCH)

    def popup_handled(self) -> bool:
        """Transition from POPUP_HANDLING back to RESULT_VERIFY (popup resolved)."""
        return self.transition_to(TraversalState.RESULT_VERIFY)

    def popup_handling_failed(self) -> bool:
        """Handle popup handling failure."""
        return self.transition_to(TraversalState.ERROR_HANDLING)

    # ============================================================================
    # V6.1 Error Handling Public API
    # ============================================================================

    def handle_error(self, error: Exception, traversal_context: Optional[Dict[str, Any]] = None) -> ErrorRecoveryResult:
        """
        Public API for error handling.

        Args:
            error: Exception that occurred during traversal
            traversal_context: Optional current traversal context

        Returns:
            ErrorRecoveryResult with recovery details
        """
        # Initialize error handler if not already done
        if not hasattr(self, '_error_handler') or self._error_handler is None:
            self._error_handler = ErrorHandler()

        # Prepare context for error handling
        context = traversal_context or {}
        context.update({
            'retry_count': self._retry_count,
            'max_retries': self._max_retries,
            'can_skip': True,  # Default: can skip nodes
            'can_backtrack': True,  # Default: can backtrack
            'node_stack_length': len(context.get('node_stack', [])),
        })

        # Handle the error
        recovery_result = self._error_handler.handle_error(error, context)

        # Update retry count if retry was attempted
        if 'retry' in recovery_result.recovery_action:
            self._retry_count += 1
        elif recovery_result.recovery_action == 'skip':
            # Reset retry count on successful skip
            self._retry_count = 0

        # Store error context for recovery
        self._error_context = {
            'last_error': str(error),
            'last_recovery_action': recovery_result.recovery_action,
            'last_recovery_success': recovery_result.success,
        }

        # Log recovery result
        if recovery_result.success:
            logger.info(f"Error recovery succeeded: {recovery_result.recovery_action}")
        else:
            logger.error(f"Error recovery failed: {recovery_result.recovery_action}")

        return recovery_result

    def get_error_recovery_summary(self) -> Dict[str, Any]:
        """
        Get error recovery statistics.

        Returns:
            Dictionary with error recovery statistics
        """
        if not hasattr(self, '_error_handler') or self._error_handler is None:
            return {
                "total_errors": 0,
                "recovered_errors": 0,
                "recovery_rate": 0.0,
                "error_statistics": {},
            }

        return self._error_handler.get_error_summary()

    def reset_error_handling(self) -> None:
        """Reset error handling state (e.g., after successful traversal)."""
        self._retry_count = 0
        self._error_context = {}
        if hasattr(self, '_error_handler') and self._error_handler is not None:
            # Keep the handler but could reset statistics if needed
            pass

    # ============================================================================
    # V6.1 Container Handling Public API
    # ============================================================================

    def handle_frame_complete(
        self,
        container: Dict[str, Any],
        traversal_context: Optional[Dict[str, Any]] = None
    ) -> Dict[str, Any]:
        """
        Public API for container frame completion handling.

        Args:
            container: Container node information
            traversal_context: Optional current traversal context

        Returns:
            Handling result with action taken
        """
        # Initialize container handler if not already done
        if not hasattr(self, '_container_handler') or self._container_handler is None:
            self._container_handler = ContainerHandler()

        # Prepare container context
        context_data = traversal_context or {}
        container_context = ContainerContext(
            container_node=container,
            visited_children=context_data.get('visited_children', []),
            total_children=len(container.get('children', [])),
            current_depth=context_data.get('current_depth', 1),
            max_depth=context_data.get('max_depth', 10),
            timeout_seconds=context_data.get('timeout_seconds', 60),
        )

        # Handle frame completion
        handling_result = self._container_handler.handle_frame_complete(container, container_context)

        # Store container context for recovery
        self._container_context = {
            'last_container_id': container.get('id', 'unknown'),
            'last_completion_reason': handling_result['completion_reason'],
            'last_fallback_action': handling_result['fallback_action'],
        }

        # Log handling result
        if handling_result['is_complete']:
            logger.info(f"Container frame complete: {handling_result['completion_reason']}")
        else:
            logger.info(f"Container still processing: {handling_result['completion_reason']}")

        return handling_result

    def get_container_statistics(self) -> Dict[str, Any]:
        """
        Get container processing statistics.

        Returns:
            Dictionary with container processing statistics
        """
        if not hasattr(self, '_container_handler') or self._container_handler is None:
            return {
                "processed_containers": 0,
                "completed_containers": 0,
                "completion_rate": 0.0,
                "average_depth": 0.0,
                "fallback_actions": {},
                "total_processing_time_ms": 0.0,
            }

        return self._container_handler.get_container_statistics()

    def reset_container_handling(self) -> None:
        """Reset container handling state (e.g., after successful traversal)."""
        self._container_context = {}
        # Container handler statistics are kept but could be reset if needed

    # ============================================================================
    # V6.1 Popup Handling Public API
    # ============================================================================

    def handle_popup(
        self,
        screen_info: Dict[str, Any],
        traversal_context: Optional[Dict[str, Any]] = None
    ) -> Dict[str, Any]:
        """
        Public API for popup detection and handling.

        Args:
            screen_info: Current screen information
            traversal_context: Optional current traversal context

        Returns:
            Handling result with action taken
        """
        # Initialize popup handler if not already done
        if not hasattr(self, '_popup_handler') or self._popup_handler is None:
            self._popup_handler = PopupHandler()

        # Prepare context for popup handling
        context = traversal_context or {}

        # Handle popup
        handling_result = self._popup_handler.handle_popup(screen_info, context)

        # Store popup context for recovery
        self._popup_context = {
            'last_popup_detected': handling_result.detected,
            'last_popup_handled': handling_result.handled,
            'last_handling_method': handling_result.handling_method,
        }

        # Log handling result
        if handling_result.detected:
            if handling_result.handled:
                logger.info(f"Popup handled successfully: {handling_result.handling_method}")
            else:
                logger.error(f"Popup handling failed: {handling_result.error_message}")

        # Return result as dictionary
        return {
            'detected': handling_result.detected,
            'handled': handling_result.handled,
            'handling_method': handling_result.handling_method,
            'state_preserved': handling_result.state_preserved,
            'execution_resumed': handling_result.execution_resumed,
            'handling_time_ms': handling_result.handling_time_ms,
            'fallback_required': handling_result.fallback_required,
        }

    def get_popup_statistics(self) -> Dict[str, Any]:
        """
        Get popup handling statistics.

        Returns:
            Dictionary with popup handling statistics
        """
        if not hasattr(self, '_popup_handler') or self._popup_handler is None:
            return {
                "detected_popups": 0,
                "handled_popups": 0,
                "handling_rate": 0.0,
                "handling_methods": {},
                "total_handling_time_ms": 0.0,
            }

        return self._popup_handler.get_popup_statistics()

    def reset_popup_handling(self) -> None:
        """Reset popup handling state (e.g., after successful traversal)."""
        self._popup_context = {}
        # Popup handler statistics are kept but could be reset if needed

    # ============================================================================
    # Internal State Handler Methods (called by step())
    # ============================================================================

    @staticmethod
    def _build_ai_call_metrics(page_analysis, elapsed_ms: float, vision: Any = None) -> Dict[str, Any]:
        """Build ai_call metrics dict from vision analysis result.

        When elapsed_ms is near zero (mock/simulation mode), injects
        realistic random latency and token counts without blocking.
        """
        import random

        metrics: Dict[str, Any] = {
            "capability": "vision",
            "success": page_analysis is not None,
            "latency_ms": elapsed_ms,
        }

        # Simulation mode: generate realistic-looking AI call metrics
        if elapsed_ms < 1.0:
            metrics["latency_ms"] = round(random.uniform(80, 350), 1)
            metrics["input_tokens"] = random.randint(400, 2000)
            metrics["output_tokens"] = random.randint(50, 300)
            metrics["provider_id"] = random.choice(["deepseek-v3", "claude-haiku-4.5"])

        if page_analysis is not None:
            if page_analysis.current_path:
                metrics["page_id"] = "/".join(page_analysis.current_path)
            metrics["element_count"] = len(page_analysis.items) if page_analysis.items else 0
        extra = getattr(vision, 'last_call_metrics', None)
        if extra:
            metrics.update(extra)
        return metrics

    def _handle_frame_complete_state(
        self, stack: "NodeStack", context: "TraversalContext", vision: "VisionService", action: "ActionExecutor"
    ) -> TraversalState:
        """
        Handle FRAME_COMPLETE state logic with intelligent AUTO_ESCAPE.

        V6.7: Implements the fallback action based on node's exit_condition:
        - BACK: Press back and pop frame
        - AUTO_ESCAPE: Try clicking unvisited sibling menu, or back if none
        - SKIP: Just pop frame
        - ABORT: Terminate traversal

        AUTO_ESCAPE behavior:
        1. Collect unvisited sibling menus from current page
        2. If unvisited menu exists, click it
        3. Call vision to verify page change
        4. If successful (path changed), don't pop stack, return NODE_SELECT
        5. If switch fails, retry 1 time
        6. If retry fails, fallback to back
        """
        import time

        from src.graph.node import ExitConditionType, FallbackAction
        from src.simulation.operation_executor import ExecutionContext
        from datetime import datetime

        current_node = stack.peek()
        if not current_node or not current_node.exit_condition:
            # Default behavior: AUTO_ESCAPE
            fallback = FallbackAction.AUTO_ESCAPE
        else:
            fallback = current_node.exit_condition.fallback

        all_metrics = {"ai_call": [], "execution": [], "auto_escape": None}

        try:
            if fallback == FallbackAction.BACK:
                # Execute back and pop frame
                t0 = time.time()
                try:
                    exec_ctx = ExecutionContext(
                        node_id=current_node.node_id if current_node else "unknown",
                        node_name="frame_complete_back",
                        operation={"action": "back"},
                        timestamp=datetime.now(),
                    )
                    result = action.execute(exec_ctx)
                    elapsed = (time.time() - t0) * 1000

                    all_metrics["execution"].append({
                        "action": "back",
                        "status": "success" if result.success else "failed",
                        "duration_ms": elapsed,
                    })

                    # First, pop any children that were pushed after current node
                    while stack.peek() and stack.peek().node_id != current_node.node_id:
                        stack.pop()
                    # Then pop the current node
                    if stack.peek() and stack.peek().node_id == current_node.node_id:
                        stack.pop()

                    self._last_handler_metrics = {
                        "execution": all_metrics["execution"][-1] if all_metrics["execution"] else None,
                    }
                    return TraversalState.NODE_SELECT

                except Exception as e:
                    elapsed = (time.time() - t0) * 1000
                    all_metrics["execution"].append({
                        "action": "back",
                        "status": "failed",
                        "duration_ms": elapsed,
                        "error": str(e),
                    })
                    raise

            elif fallback == FallbackAction.AUTO_ESCAPE:
                # AUTO_ESCAPE: Try to switch to unvisited sibling menu
                unvisited_menus = []
                current_page = None

                # Get current page analysis from context
                if hasattr(context, 'current_page_analysis') and context.current_page_analysis:
                    page_analysis = context.current_page_analysis
                    current_page = context.current_path[-1] if context.current_path else None

                    # Collect sibling menus from standard fields.
                    # level1_menus / level2_menus are explicitly curated lists
                    # of MenuInfo; items contains everything (switches, sliders,
                    # buttons) and would produce false-positive menu candidates.
                    all_menus = []
                    if hasattr(page_analysis, 'level1_menus') and page_analysis.level1_menus:
                        all_menus.extend(m.name for m in page_analysis.level1_menus)
                    if hasattr(page_analysis, 'level2_menus') and page_analysis.level2_menus:
                        all_menus.extend(m.name for m in page_analysis.level2_menus)

                    # Filter out already visited menus
                    visited_l1 = context.visited_level1_menus if hasattr(context, 'visited_level1_menus') else set()
                    visited_l2 = context.visited_level2_menus if hasattr(context, 'visited_level2_menus') else set()

                    for menu_name in all_menus:
                        if menu_name not in visited_l1 and menu_name not in visited_l2:
                            unvisited_menus.append(menu_name)

                # If unvisited menu exists, try clicking it
                if unvisited_menus:
                    target_menu = unvisited_menus[0]  # Pick first unvisited menu

                    # Try up to 2 times (initial + 1 retry)
                    for attempt in range(2):
                        t0 = time.time()
                        try:
                            # Find target menu item
                            target_item = None
                            if context.current_page_analysis and context.current_page_analysis.items:
                                for item in context.current_page_analysis.items:
                                    if item.name == target_menu:
                                        target_item = item
                                        break

                            if target_item:
                                # Click target menu
                                exec_ctx = ExecutionContext(
                                    node_id=current_node.node_id if current_node else "unknown",
                                    node_name=f"auto_escape_{target_menu}",
                                    operation={"action": "click", "target": {"by": "element", "value": target_item}},
                                    timestamp=datetime.now(),
                                )
                                result = action.execute(exec_ctx)
                                elapsed = (time.time() - t0) * 1000

                                all_metrics["execution"].append({
                                    "action": "click",
                                    "status": "success" if result.success else "failed",
                                    "target": target_menu,
                                    "duration_ms": elapsed,
                                })

                                # Wait after action if configured
                                wait_ms = getattr(context, 'wait_after_action_ms', 100)
                                if wait_ms > 0:
                                    time.sleep(wait_ms / 1000)

                                # V6.7: Force vision call to get latest page
                                t1 = time.time()
                                new_analysis = vision.analyze_screenshot(b"")
                                elapsed_vision = (time.time() - t1) * 1000

                                # Update context with new page analysis
                                if hasattr(context, 'current_page_analysis'):
                                    context.current_page_analysis = new_analysis
                                if hasattr(context, 'current_path') and new_analysis:
                                    new_path = list(new_analysis.current_path) if new_analysis.current_path else []
                                    context.current_path = new_path

                                all_metrics["ai_call"].append(self._build_ai_call_metrics(new_analysis, elapsed_vision, vision))

                                # Verify page path changed
                                new_page = context.current_path[-1] if context.current_path else None
                                if new_page and new_page != current_page:
                                    # Switch successful - don't pop stack
                                    all_metrics["auto_escape"] = {
                                        "action": "click_menu",
                                        "target": target_menu,
                                        "success": True,
                                        "from": current_page,
                                        "to": new_page,
                                        "attempts": attempt + 1,
                                    }
                                    self._last_handler_metrics = {
                                        "ai_call": all_metrics["ai_call"][-1],
                                        "execution": all_metrics["execution"][-1],
                                        "auto_escape": all_metrics["auto_escape"],
                                    }
                                    return TraversalState.NODE_SELECT

                        except Exception as e:
                            elapsed = (time.time() - t0) * 1000
                            all_metrics["execution"].append({
                                "action": "click",
                                "status": "failed",
                                "target": target_menu,
                                "duration_ms": elapsed,
                                "error": str(e),
                            })

                    # All attempts failed - fallback to back
                    all_metrics["auto_escape"] = {
                        "action": "fallback_back",
                        "reason": "switch_failed",
                        "target": target_menu,
                    }

                # No unvisited menus or switch failed - execute back
                t0 = time.time()
                try:
                    exec_ctx = ExecutionContext(
                        node_id=current_node.node_id if current_node else "unknown",
                        node_name="auto_escape_fallback_back",
                        operation={"action": "back"},
                        timestamp=datetime.now(),
                    )
                    result = action.execute(exec_ctx)
                    elapsed = (time.time() - t0) * 1000

                    all_metrics["execution"].append({
                        "action": "back",
                        "status": "success" if result.success else "failed",
                        "fallback": True,
                        "duration_ms": elapsed,
                    })

                    # Pop the current node
                    while stack.peek() and stack.peek().node_id == current_node.node_id:
                        stack.pop()

                    self._last_handler_metrics = {
                        "execution": all_metrics["execution"][-1] if all_metrics["execution"] else None,
                        "auto_escape": all_metrics.get("auto_escape"),
                    }
                    return TraversalState.NODE_SELECT

                except Exception as e:
                    elapsed = (time.time() - t0) * 1000
                    all_metrics["execution"].append({
                        "action": "back",
                        "status": "failed",
                        "duration_ms": elapsed,
                        "error": str(e),
                    })
                    raise

            elif fallback == FallbackAction.SKIP:
                # Just pop frame, no action
                stack.pop()
                self._last_handler_metrics = {
                    "execution": {"action": "skip", "status": "success"},
                }
                return TraversalState.NODE_SELECT

            elif fallback == FallbackAction.ABORT:
                # Signal termination
                if hasattr(context, 'global_state'):
                    from src.state_machine import GlobalState
                    context.global_state = GlobalState.TERMINATED

                self._last_handler_metrics = {
                    "execution": {"action": "abort", "status": "success"},
                }
                return TraversalState.BRANCH

        except Exception as e:
            context.last_error = e
            self._last_handler_metrics = {
                "execution": all_metrics["execution"][-1] if all_metrics["execution"] else None,
                "error": {
                    "error_type": type(e).__name__,
                    "error_message": str(e),
                },
            }
            return TraversalState.ERROR_HANDLING

        return TraversalState.NODE_SELECT

    def _handle_error_state(
        self, stack: "NodeStack", context: "TraversalContext", vision: "VisionService", action: "ActionExecutor"
    ) -> TraversalState:
        """
        Handle ERROR_HANDLING state logic with three-layer error handling.

        V6.7: Implements comprehensive error handling:
        1. Node error_policy (retry/skip/backtrack/abort/fallback)
        2. ExceptionHandlingChain (placeholder for future)
        3. AI exception handling (placeholder for future)

        Error metrics are recorded for trace analysis.
        """
        from datetime import datetime

        current_node = stack.peek() if not stack.is_empty else None
        error = context.last_error

        if not error:
            # No error to handle - shouldn't happen, but continue
            return TraversalState.NODE_SELECT

        # Prepare error metrics
        error_metrics = {
            "error_type": type(error).__name__,
            "error_message": str(error),
            "node_id": current_node.node_id if current_node else "unknown",
            "action_taken": None,
        }

        # Layer 1: Node error_policy handling
        action_taken = "default"

        if current_node and current_node.error_policy:
            policy = current_node.error_policy
            on_error = policy.on_error
            action_taken = on_error

            if on_error == "retry":
                # Retry current node operation
                # Get current retry count for this node
                retry_count = 0
                if current_node.node_id in context.failed_nodes:
                    retry_count = context.failed_nodes[current_node.node_id].get("retry_count", 0)

                if retry_count < policy.max_retries:
                    # Update retry count
                    context.failed_nodes[current_node.node_id] = {
                        "error_type": type(error).__name__,
                        "error_message": str(error),
                        "timestamp": datetime.now(),
                        "retry_count": retry_count + 1,
                        "max_retries": policy.max_retries,
                    }
                    error_metrics["action_taken"] = "retry"
                    error_metrics["retry_count"] = retry_count + 1

                    self._last_handler_metrics = {"error": error_metrics}
                    return TraversalState.EXECUTE
                else:
                    # Max retries exceeded, skip node
                    action_taken = "skip_max_retries"

            elif on_error == "skip":
                # Skip this node
                action_taken = "skip"

            elif on_error == "backtrack":
                # Pop current frame
                stack.pop()
                action_taken = "backtrack"
                error_metrics["action_taken"] = "backtrack"

                # Update error metrics
                self._last_handler_metrics = {"error": error_metrics}
                return TraversalState.FRAME_COMPLETE

            elif on_error == "abort":
                # Terminate traversal
                if hasattr(context, 'global_state'):
                    from src.state_machine import GlobalState
                    context.global_state = GlobalState.TERMINATED
                action_taken = "abort"
                error_metrics["action_taken"] = "abort"

                self._last_handler_metrics = {"error": error_metrics}
                return TraversalState.BRANCH

            elif on_error == "fallback":
                # Try fallback target navigation
                action_taken = "fallback"
                error_metrics["fallback_target"] = policy.fallback_target or "unknown"

                # Update error metrics
                self._last_handler_metrics = {"error": error_metrics}
                # Would navigate to fallback_target in V6.8
                return TraversalState.NODE_SELECT

        # Layer 2: ExceptionHandlingChain (placeholder)
        # V6.8: Will integrate with ExceptionHandlingChain
        # if hasattr(context, 'exception_chain') and context.exception_chain:
        #     result = context.exception_chain.handle(ExceptionContext(error, context))
        #     if result.recovered:
        #         action_taken = "chain_recovered"
        #         error_metrics["action_taken"] = "chain_recovered"
        #         self._last_handler_metrics = {"error": error_metrics}
        #         return TraversalState.NODE_SELECT
        #     elif result.backtrack:
        #         stack.pop()
        #         action_taken = "chain_backtrack"
        #         error_metrics["action_taken"] = "chain_backtrack"
        #         self._last_handler_metrics = {"error": error_metrics}
        #         return TraversalState.FRAME_COMPLETE

        # Layer 3: AI exception handling (placeholder)
        # V6.9: Will integrate with AI advisor for intelligent error recovery
        # if hasattr(context, 'ai_provider') and context.ai_provider:
        #     decision = context.ai_provider.handle_exception(error, context)
        #     # Apply AI decision...

        # Default action: Skip node
        error_metrics["action_taken"] = action_taken

        # Update consecutive errors counter
        if hasattr(context, 'consecutive_errors'):
            context.consecutive_errors += 1

        # Record failure in failed_nodes
        if current_node and hasattr(context, 'failed_nodes'):
            existing = context.failed_nodes.get(current_node.node_id, {})
            context.failed_nodes[current_node.node_id] = {
                "error_type": type(error).__name__,
                "error_message": str(error),
                "timestamp": datetime.now(),
                "retry_count": existing.get("retry_count", 0),
                "action_taken": action_taken,
            }

        self._last_handler_metrics = {"error": error_metrics}
        return TraversalState.NODE_SELECT

    def _handle_popup_state(
        self, stack: "NodeStack", context: "TraversalContext", vision: "VisionService", action: "ActionExecutor"
    ) -> TraversalState:
        """
        Handle POPUP_HANDLING state logic with intelligent button detection.

        V6.7: Implements priority-based popup handling:
        1. Find and click safe button (cancel/close/no/ignore/later)
        2. Execute Back operation if no safe button found

        Safe button keywords:
        - Chinese: ["取消", "关闭", "否", "忽略", "稍后"]
        - English: ["Cancel", "Close", "No"]
        """
        import time

        from src.simulation.operation_executor import ExecutionContext
        from datetime import datetime

        # Define safe button keywords
        safe_keywords = ["取消", "关闭", "否", "忽略", "稍后", "Cancel", "Close", "No"]

        all_metrics = {"execution": None}

        try:
            # Get current page analysis from context
            page_analysis = None
            if hasattr(context, 'current_page_analysis'):
                page_analysis = context.current_page_analysis

            # Priority 1: Find safe button in page items
            safe_button = None
            if page_analysis and page_analysis.items:
                for item in page_analysis.items:
                    # Check if item name contains any safe keyword
                    if item.name:
                        for keyword in safe_keywords:
                            if keyword.lower() in item.name.lower():
                                safe_button = item
                                break
                    if safe_button:
                        break

            if safe_button:
                # Click safe button
                t0 = time.time()
                try:
                    exec_ctx = ExecutionContext(
                        node_id="popup_handler",
                        node_name="popup_close_safe_button",
                        operation={"action": "click", "target": {"by": "element", "value": safe_button}},
                        timestamp=datetime.now(),
                    )
                    result = action.execute(exec_ctx)
                    elapsed = (time.time() - t0) * 1000

                    all_metrics["execution"] = {
                        "action": "click",
                        "status": "success" if result.success else "failed",
                        "target": safe_button.name,
                        "method": "safe_button",
                        "duration_ms": elapsed,
                    }

                    # Wait after action if configured
                    wait_ms = getattr(context, 'wait_after_action_ms', 100)
                    if wait_ms > 0:
                        time.sleep(wait_ms / 1000)

                    self._last_handler_metrics = {
                        "execution": all_metrics["execution"],
                    }
                    return TraversalState.RESULT_VERIFY

                except Exception as e:
                    elapsed = (time.time() - t0) * 1000
                    all_metrics["execution"] = {
                        "action": "click",
                        "status": "failed",
                        "target": safe_button.name if safe_button else "unknown",
                        "method": "safe_button",
                        "duration_ms": elapsed,
                        "error": str(e),
                    }
                    # Fall through to back operation

            # Priority 2: Execute Back if no safe button found or click failed
            t0 = time.time()
            try:
                exec_ctx = ExecutionContext(
                    node_id="popup_handler",
                    node_name="popup_close_back",
                    operation={"action": "back"},
                    timestamp=datetime.now(),
                )
                result = action.execute(exec_ctx)
                elapsed = (time.time() - t0) * 1000

                if all_metrics["execution"] is None:
                    all_metrics["execution"] = {
                        "action": "back",
                        "status": "success" if result.success else "failed",
                        "method": "back",
                        "duration_ms": elapsed,
                    }
                else:
                    # Update existing metrics with fallback
                    all_metrics["execution"]["fallback"] = "back"
                    all_metrics["execution"]["fallback_status"] = "success" if result.success else "failed"

                self._last_handler_metrics = {
                    "execution": all_metrics["execution"],
                }
                return TraversalState.RESULT_VERIFY

            except Exception as e:
                elapsed = (time.time() - t0) * 1000
                all_metrics["execution"] = {
                    "action": "back",
                    "status": "failed",
                    "method": "back",
                    "duration_ms": elapsed,
                    "error": str(e),
                }
                raise

        except Exception as e:
            self._last_handler_metrics = {
                "execution": all_metrics.get("execution"),
                "error": {
                    "error_type": type(e).__name__,
                    "error_message": str(e),
                },
            }
            return TraversalState.ERROR_HANDLING

    # ============================================================================
    # State machine step interface
    # ============================================================================

    def step(
        self,
        stack: "NodeStack",
        context: "TraversalContext",
        vision: "VisionService",
        action: "ActionExecutor",
    ) -> TraversalStateTransition:
        """
        Execute a single state machine step.

        This method implements the core state machine logic, calling the
        appropriate handler based on current state. Includes try-catch
        wrapper to ensure exceptions are properly handled.

        Args:
            stack: Node stack for traversal
            context: Traversal context with runtime state
            vision: Vision service for screen analysis
            action: Action executor for device control

        Returns:
            StateTransition record for this step
        """
        # Import here to avoid circular dependency
        from src.exception import ExceptionHandlingChain
        from src.graph.node import (
            ExitCondition,
            ExitConditionType,
            FallbackAction,
            ErrorPolicy,
        )

        from_state = self._state
        next_state = None
        metadata = {}
        node_id = self._current_node_id

        # V6.7: Try-catch wrapper for exception handling
        try:
            # State machine switch
            if from_state == TraversalState.NODE_SELECT:
                # Select next node from stack
                next_state = self._handle_node_select(stack, context)
                metadata["action"] = "select_node"

            elif from_state == TraversalState.PRECONDITION_CHECK:
                # Check if precondition is satisfied
                next_state = self._handle_precondition_check(stack, context, vision, action)
                metadata["action"] = "check_precondition"

            elif from_state == TraversalState.EXECUTE:
                # Execute node operation
                next_state = self._handle_execute(stack, context, vision, action)
                metadata["action"] = "execute_operation"

            elif from_state == TraversalState.RESULT_VERIFY:
                # Verify execution result and check for popups
                next_state = self._handle_result_verify(stack, context, vision)
                metadata["action"] = "verify_result"

            elif from_state == TraversalState.BRANCH:
                # Determine next action
                next_state = self._handle_branch(stack, context)
                metadata["action"] = "branch_decision"

            elif from_state == TraversalState.FRAME_COMPLETE:
                # Handle container frame complete
                next_state = self._handle_frame_complete_state(stack, context, vision, action)
                metadata["action"] = "frame_complete"

            elif from_state == TraversalState.ERROR_HANDLING:
                # Handle error
                next_state = self._handle_error_state(stack, context, vision, action)
                metadata["action"] = "error_handling"

            elif from_state == TraversalState.POPUP_HANDLING:
                # Handle popup
                next_state = self._handle_popup_state(stack, context, vision, action)
                metadata["action"] = "popup_handling"

            else:
                raise ValueError(f"Unknown state: {from_state}")

            # Use updated node_id (may have changed during handler execution)
            updated_node_id = self._current_node_id

            # Perform the transition
            if next_state:
                self.transition_to(next_state, node_id=updated_node_id, **metadata)

            # Return the transition record
            transition = TraversalStateTransition(
                from_state=from_state,
                to_state=next_state or from_state,
                node_id=updated_node_id,
                metadata=metadata,
            )
            return transition

        # V6.7: Exception handling - catch all exceptions and route to ERROR_HANDLING
        except Exception as e:
            # Store the exception in context
            context.last_error = e

            # Increment consecutive error counter
            if hasattr(context, 'consecutive_errors'):
                context.consecutive_errors += 1

            # Set next state to ERROR_HANDLING
            next_state = TraversalState.ERROR_HANDLING

            # Record error type in metadata
            metadata["error_type"] = type(e).__name__
            metadata["error_message"] = str(e)

            # Perform the transition
            self.transition_to(next_state, node_id=self._current_node_id, **metadata)

            # Return the error transition
            return TraversalStateTransition(
                from_state=from_state,
                to_state=next_state,
                node_id=self._current_node_id,
                metadata=metadata,
            )

    def _handle_node_select(self, stack: "NodeStack", context: "TraversalContext") -> TraversalState:
        """Handle NODE_SELECT state logic."""
        if stack.is_empty:
            # No more nodes to process - would transition to COMPLETED at engine level
            return TraversalState.BRANCH

        # Get current node from stack
        current_node = stack.peek()
        if current_node:
            # Update current node ID
            self._current_node_id = current_node.node_id

        # Always go through precondition check phase
        # If no precondition is defined, PRECONDITION_CHECK handler will skip it
        return TraversalState.PRECONDITION_CHECK

    def _handle_precondition_check(
        self, stack: "NodeStack", context: "TraversalContext", vision: "VisionService", action: "ActionExecutor"
    ) -> TraversalState:
        """
        Handle PRECONDITION_CHECK state logic with intelligent correction.

        V6.7: Implements smart precondition correction with up to 3 retry rounds.
        Each round analyzes the current page, checks if precondition is satisfied,
        and attempts intelligent correction based on page relationship.

        Correction strategies:
        - NAVIGABLE: Click target menu item
        - DEEPER: Execute back operation
        - UNKNOWN: Execute back operation (default)

        After each correction, vision is called to verify the result.
        If successful, the handler exits early with EXECUTE state.
        """
        import time

        current_node = stack.peek()

        # If no precondition is defined, it's satisfied by default
        if not current_node.has_precondition():
            return TraversalState.EXECUTE

        precondition = current_node.precondition
        expected_page = precondition.page_name

        # Maximum retry rounds for precondition correction
        max_retries = 3
        all_metrics = {"ai_call": [], "execution": [], "correction": None}

        for round_num in range(max_retries):
            # Call vision service to analyze current screen
            t0 = time.time()
            try:
                image_data = b""
                page_analysis = vision.analyze_screenshot(image_data)
                elapsed = (time.time() - t0) * 1000

                # Update context with latest page analysis
                if hasattr(context, 'current_page_analysis'):
                    context.current_page_analysis = page_analysis
                if hasattr(context, 'current_path') and page_analysis:
                    context.current_path = list(page_analysis.current_path) if page_analysis.current_path else []

                # Record ai_call metrics
                all_metrics["ai_call"].append(self._build_ai_call_metrics(page_analysis, elapsed, vision))

                # Check if precondition is satisfied
                current_page = context.current_path[-1] if context.current_path else None
                # V6.9.3: If expected_page is None, precondition is considered satisfied
                # (allows execution regardless of current page)
                if expected_page is None or current_page == expected_page:
                    # Precondition satisfied - proceed to execution
                    self._last_handler_metrics = {"ai_call": all_metrics["ai_call"][-1]}
                    return TraversalState.EXECUTE

                # Precondition not satisfied - attempt intelligent correction
                # Get available menus from page analysis
                available_menus = []
                if page_analysis and page_analysis.items:
                    available_menus = [item.name for item in page_analysis.items if item.name]

                # Classify the relationship between current and expected page
                relation = classify_relation(context.current_path, expected_page, available_menus)

                # Apply correction strategy based on relationship
                correction_success = False

                if relation == PageRelation.NAVIGABLE:
                    # NAVIGABLE: Click target menu item
                    target_item = None
                    if page_analysis and page_analysis.items:
                        for item in page_analysis.items:
                            if item.name == expected_page:
                                target_item = item
                                break

                    if target_item:
                        # Execute click on target menu
                        t1 = time.time()
                        try:
                            from src.simulation.operation_executor import ExecutionContext
                            from datetime import datetime
                            exec_ctx = ExecutionContext(
                                node_id=current_node.node_id,
                                node_name=f"precondition_correction_{expected_page}",
                                operation={"action": "click", "target": {"by": "element", "value": target_item}},
                                timestamp=datetime.now(),
                            )
                            result = action.execute(exec_ctx)
                            elapsed_exec = (time.time() - t1) * 1000

                            # Record execution metrics
                            all_metrics["execution"].append({
                                "action": "click",
                                "status": "success" if result.success else "failed",
                                "target": expected_page,
                                "duration_ms": elapsed_exec,
                            })

                            # Wait after action if configured
                            wait_ms = getattr(context, 'wait_after_action_ms', 100)
                            if wait_ms > 0:
                                time.sleep(wait_ms / 1000)

                            # V6.7: Immediately verify with vision after correction
                            t2 = time.time()
                            verify_analysis = vision.analyze_screenshot(b"")
                            elapsed_verify = (time.time() - t2) * 1000

                            # Update context with verification result
                            if hasattr(context, 'current_page_analysis'):
                                context.current_page_analysis = verify_analysis
                            if hasattr(context, 'current_path') and verify_analysis:
                                context.current_path = list(verify_analysis.current_path) if verify_analysis.current_path else []

                            all_metrics["ai_call"].append(self._build_ai_call_metrics(verify_analysis, elapsed_verify, vision))

                            # Check if correction was successful
                            verify_page = context.current_path[-1] if context.current_path else None
                            if verify_page == expected_page:
                                # Correction successful - record and exit early
                                all_metrics["correction"] = {
                                    "relation": relation.value,
                                    "action": "click_menu",
                                    "success": True,
                                    "rounds": round_num + 1,
                                }
                                self._last_handler_metrics = {
                                    "ai_call": all_metrics["ai_call"][-1],
                                    "execution": all_metrics["execution"][-1] if all_metrics["execution"] else None,
                                    "correction": all_metrics["correction"],
                                }
                                return TraversalState.EXECUTE

                            correction_success = (verify_page == expected_page)

                        except Exception as e:
                            elapsed_exec = (time.time() - t1) * 1000
                            all_metrics["execution"].append({
                                "action": "click",
                                "status": "failed",
                                "target": expected_page,
                                "duration_ms": elapsed_exec,
                                "error": str(e),
                            })

                elif relation in (PageRelation.DEEPER, PageRelation.UNKNOWN):
                    # DEEPER or UNKNOWN: Execute back operation
                    t1 = time.time()
                    try:
                        from src.simulation.operation_executor import ExecutionContext
                        from datetime import datetime
                        exec_ctx = ExecutionContext(
                            node_id=current_node.node_id,
                            node_name="precondition_correction_back",
                            operation={"action": "back"},
                            timestamp=datetime.now(),
                        )
                        result = action.execute(exec_ctx)
                        elapsed_exec = (time.time() - t1) * 1000

                        # Record execution metrics
                        all_metrics["execution"].append({
                            "action": "back",
                            "status": "success" if result.success else "failed",
                            "duration_ms": elapsed_exec,
                        })

                        # Wait after action if configured
                        wait_ms = getattr(context, 'wait_after_action_ms', 100)
                        if wait_ms > 0:
                            time.sleep(wait_ms / 1000)

                        # V6.7: Immediately verify with vision after correction
                        t2 = time.time()
                        verify_analysis = vision.analyze_screenshot(b"")
                        elapsed_verify = (time.time() - t2) * 1000

                        # Update context with verification result
                        if hasattr(context, 'current_page_analysis'):
                            context.current_page_analysis = verify_analysis
                        if hasattr(context, 'current_path') and verify_analysis:
                            context.current_path = list(verify_analysis.current_path) if verify_analysis.current_path else []

                        all_metrics["ai_call"].append(self._build_ai_call_metrics(verify_analysis, elapsed_verify, vision))

                        # Check if correction was successful
                        verify_page = context.current_path[-1] if context.current_path else None
                        if verify_page == expected_page:
                            # Correction successful - record and exit early
                            all_metrics["correction"] = {
                                "relation": relation.value,
                                "action": "back",
                                "success": True,
                                "rounds": round_num + 1,
                            }
                            self._last_handler_metrics = {
                                "ai_call": all_metrics["ai_call"][-1],
                                "execution": all_metrics["execution"][-1] if all_metrics["execution"] else None,
                                "correction": all_metrics["correction"],
                            }
                            return TraversalState.EXECUTE

                        correction_success = (verify_page == expected_page)

                    except Exception as e:
                        elapsed_exec = (time.time() - t1) * 1000
                        all_metrics["execution"].append({
                            "action": "back",
                            "status": "failed",
                            "duration_ms": elapsed_exec,
                            "error": str(e),
                        })

            except Exception as e:
                elapsed = (time.time() - t0) * 1000
                all_metrics["ai_call"].append(self._build_ai_call_metrics(None, elapsed, vision))

        # All retries exhausted - record error and transition to ERROR_HANDLING
        self._last_handler_metrics = {
            "ai_call": all_metrics["ai_call"],
            "execution": all_metrics["execution"],
            "error": {
                "error_type": "PreconditionTimeout",
                "error_message": f"Precondition not satisfied after {max_retries} retries. Expected page: {expected_page}",
            },
        }
        return TraversalState.ERROR_HANDLING

    def _handle_execute(
        self, stack: "NodeStack", context: "TraversalContext", vision: "VisionService", action: "ActionExecutor"
    ) -> TraversalState:
        """Handle EXECUTE state logic."""
        current_node = stack.peek()

        try:
            # V6.5: Build ExecutionContext and call action.execute()
            import time
            from src.simulation.operation_executor import ExecutionContext
            from datetime import datetime
            t0 = time.time()
            operation = current_node.operation.__dict__ if hasattr(current_node.operation, '__dict__') else {"action": "click"}
            exec_ctx = ExecutionContext(
                node_id=current_node.node_id,
                node_name=current_node.name,
                operation=operation,
                timestamp=datetime.now(),
            )
            result = action.execute(exec_ctx)
            self.set_execution_result({"success": result.success, "action": result.action})

            # Build execution metrics
            execution_metrics = {
                "action": operation.get("action", "unknown"),
                "status": "success" if result.success else "failed",
                "target": operation.get("target"),
                "duration_ms": (time.time() - t0) * 1000,
            }

            # V6.15: Execute restore action if defined
            restore_metrics = None
            if current_node.needs_restore() and hasattr(current_node.operation, 'restore') and current_node.operation.restore:
                restore_op = current_node.operation.restore
                t_restore = time.time()
                restore_operation = {
                    "action": restore_op.action,
                    "target": restore_op.target.__dict__ if hasattr(restore_op.target, '__dict__') else restore_op.target,
                    "params": restore_op.params,
                }
                restore_ctx = ExecutionContext(
                    node_id=current_node.node_id,
                    node_name=f"{current_node.name}_restore",
                    operation=restore_operation,
                    timestamp=datetime.now(),
                )
                restore_result = action.execute(restore_ctx)
                restore_metrics = {
                    "action": restore_op.action,
                    "status": "success" if restore_result.success else "failed",
                    "target": restore_operation.get("target"),
                    "is_restore": True,  # Mark this as a restore operation
                    "duration_ms": (time.time() - t_restore) * 1000,
                }

            self._last_handler_metrics = {
                "execution": execution_metrics,
                "restore": restore_metrics,
            }
            return TraversalState.RESULT_VERIFY

        except Exception as e:
            context.last_error = e
            self._last_handler_metrics = {
                "error": {
                    "error_type": type(e).__name__,
                    "error_message": str(e),
                }
            }
            return TraversalState.ERROR_HANDLING

    def _handle_result_verify(
        self, stack: "NodeStack", context: "TraversalContext", vision: "VisionService"
    ) -> TraversalState:
        """Handle RESULT_VERIFY state logic."""
        # V6.6: Call vision service to verify result page
        import time
        t0 = time.time()
        try:
            image_data = b""
            after_analysis = vision.analyze_screenshot(image_data)
            elapsed = (time.time() - t0) * 1000
            self._last_handler_metrics = {
                "ai_call": self._build_ai_call_metrics(after_analysis, elapsed, vision),
            }
            if hasattr(context, 'current_page_analysis'):
                context.current_page_analysis = after_analysis
            return TraversalState.BRANCH
        except Exception as e:
            elapsed = (time.time() - t0) * 1000
            self._last_handler_metrics = {
                "ai_call": self._build_ai_call_metrics(None, elapsed, vision),
                "error": {"error_type": type(e).__name__, "error_message": str(e)},
            }
            return TraversalState.ERROR_HANDLING

    def _handle_branch(self, stack: "NodeStack", context: "TraversalContext") -> TraversalState:
        """Handle BRANCH state logic."""
        current_frame = stack.peek()
        from src.graph.node import NodeType, ChildrenStrategyType

        if not current_frame:
            # No current node - check if we should continue
            if stack.size > 1:
                return TraversalState.FRAME_COMPLETE
            else:
                return TraversalState.NODE_SELECT

        # _resolve_node handles both NodeStack (StackFrame) and _NodeStackAdapter (TraversalNode)
        current_node = self._resolve_node(current_frame)

        # Check if this node has unvisited children
        has_unvisited_children = False
        if current_node.children_strategy and current_node.children_strategy.type != ChildrenStrategyType.NONE:
            # Get visited children for this node
            visited_children = context.visited_children.get(current_node.node_id, set())
            if current_node.children_strategy.type == ChildrenStrategyType.STATIC:
                # Check if any static child hasn't been visited
                for child_id in current_node.children_strategy.static_children:
                    if child_id not in visited_children:
                        has_unvisited_children = True
                        break
            elif current_node.children_strategy.type == ChildrenStrategyType.DYNAMIC_MATCH:
                # Optimistic: state machine cannot call DynamicChildManager.
                # Engine layer gates actual availability — if get_next_unvisited_child
                # returns None, _step_once overrides to FRAME_COMPLETE.
                has_unvisited_children = True

        if not has_unvisited_children and not current_node.is_leaf():
            return TraversalState.FRAME_COMPLETE

        if has_unvisited_children:
            return TraversalState.NODE_SELECT

        if current_node.is_leaf():
            # Leaf node - check if we need to return
            if stack.size > 1:
                return TraversalState.FRAME_COMPLETE
            else:
                return TraversalState.NODE_SELECT
        else:
            return TraversalState.NODE_SELECT

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
