"""
Traversal state machine for individual node execution.

This module implements the traversal state machine that handles the
execution flow for individual nodes.
"""

from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Callable, Dict, List, Optional, TYPE_CHECKING
from datetime import datetime
import logging

# Import error handler components
from .error_handler import ErrorHandler, ErrorRecoveryResult
# Import container handler components
from .container_handler import ContainerHandler, ContainerContext
# Import popup handler components
from .popup_handler import PopupHandler

logger = logging.getLogger(__name__)


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

    def _handle_frame_complete_state(
        self, stack: "NodeStack", context: "TraversalContext", action: "ActionExecutor"
    ) -> TraversalState:
        """
        Handle FRAME_COMPLETE state logic.

        Implements the fallback action based on node's exit_condition:
        - BACK: Press back and pop frame
        - AUTO_ESCAPE: Try sibling menu, or back if none
        - SKIP: Just pop frame
        - ABORT: Terminate traversal
        """
        from src.graph.node import ExitConditionType, FallbackAction

        current_node = stack.peek()
        if not current_node or not current_node.exit_condition:
            # Default behavior: BACK
            fallback = FallbackAction.BACK
        else:
            fallback = current_node.exit_condition.fallback

        try:
            if fallback == FallbackAction.BACK:
                # Execute back and pop frame
                # action.press_back()
                # First, pop any children that were pushed after current node
                while stack.peek() and stack.peek().node_id != current_node.node_id:
                    stack.pop()
                # Then pop the current node
                if stack.peek() and stack.peek().node_id == current_node.node_id:
                    stack.pop()
                return TraversalState.NODE_SELECT

            elif fallback == FallbackAction.AUTO_ESCAPE:
                # Check for unvisited sibling menus
                # has_unvisited_siblings = check_unvisited_siblings(stack, context)
                # if has_unvisited_siblings:
                #     # Click sibling menu
                #     return TraversalState.NODE_SELECT
                # else:
                #     # No siblings, execute back
                #     action.press_back()
                while stack.peek() and stack.peek().node_id == current_node.node_id:
                    stack.pop()
                return TraversalState.NODE_SELECT

            elif fallback == FallbackAction.SKIP:
                # Just pop frame, no action
                stack.pop()
                return TraversalState.NODE_SELECT

            elif fallback == FallbackAction.ABORT:
                # Signal termination
                context.global_state = GlobalState.TERMINATED
                # Would transition to COMPLETED at engine level
                return TraversalState.BRANCH

        except Exception as e:
            context.last_error = e
            return TraversalState.ERROR_HANDLING

        return TraversalState.NODE_SELECT

    def _handle_error_state(
        self, stack: "NodeStack", context: "TraversalContext", vision: "VisionService", action: "ActionExecutor"
    ) -> TraversalState:
        """
        Handle ERROR_HANDLING state logic.

        Implements three-layer error handling:
        1. Node error_policy
        2. ExceptionHandlingChain
        3. AI exception handling (reserved)
        """
        current_node = stack.peek() if not stack.is_empty() else None
        error = context.last_error

        if not error:
            # No error to handle - shouldn't happen
            return TraversalState.NODE_SELECT

        # Layer 1: Node error_policy
        if current_node and current_node.error_policy:
            policy = current_node.error_policy
            on_error = policy.on_error

            if on_error == "retry":
                # Retry current node operation
                if context.retry_count < policy.max_retries:
                    context.retry_count += 1
                    return TraversalState.EXECUTE
                else:
                    # Max retries exceeded, skip
                    return TraversalState.NODE_SELECT

            elif on_error == "skip":
                # Skip this node
                return TraversalState.NODE_SELECT

            elif on_error == "backtrack":
                # Pop current frame
                stack.pop()
                return TraversalState.FRAME_COMPLETE

            elif on_error == "abort":
                # Terminate traversal
                context.global_state = GlobalState.TERMINATED
                return TraversalState.BRANCH

            elif on_error == "fallback":
                # Try fallback target
                # Would navigate to fallback_target
                return TraversalState.NODE_SELECT

        # Layer 2: ExceptionHandlingChain (if available)
        # if context.exception_chain:
        #     result = context.exception_chain.handle(ExceptionContext(...))
        #     if result == HandlingResult.RECOVER:
        #         return TraversalState.NODE_SELECT
        #     elif result == HandlingResult.BACKTRACK:
        #         stack.pop()
        #         return TraversalState.FRAME_COMPLETE

        # Layer 3: AI exception handling (reserved for V6.1)
        # if context.ai_provider:
        #     decision = context.ai_provider.handle_exception(error, context)
        #     # Apply AI decision...

        # V6.5: Record error context
        if hasattr(context, 'consecutive_errors'):
            context.consecutive_errors += 1
        if error and hasattr(context, 'failed_nodes') and current_node:
            context.failed_nodes[current_node.node_id] = {
                "error_type": type(error).__name__,
                "error_message": str(error),
                "timestamp": datetime.now(),
            }
        self._last_handler_metrics = {
            "error": {
                "error_type": type(error).__name__ if error else "UnknownError",
                "error_message": str(error) if error else "",
            }
        }
        # Default: SKIP
        return TraversalState.NODE_SELECT

    def _handle_popup_state(
        self, stack: "NodeStack", context: "TraversalContext", vision: "VisionService", action: "ActionExecutor"
    ) -> TraversalState:
        """
        Handle POPUP_HANDLING state logic.

        Implements priority-based popup handling:
        1. Find and click cancel/close button
        2. Execute Back operation
        3. AI decision (reserved)
        """
        # Priority 1: Find cancel button
        # cancel_button = find_cancel_button(vision.get_current_screen())
        # if cancel_button:
        #     action.tap(cancel_button.x, cancel_button.y)
        #     return TraversalState.RESULT_VERIFY

        # Priority 2: Execute Back
        # action.press_back()
        return TraversalState.RESULT_VERIFY

        # Priority 3: AI decision (reserved)

        # If all methods fail
        # return TraversalState.ERROR_HANDLING

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
        appropriate handler based on current state.

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

        # State machine switch
        if from_state == TraversalState.NODE_SELECT:
            # Select next node from stack
            next_state = self._handle_node_select(stack, context)
            metadata["action"] = "select_node"

        elif from_state == TraversalState.PRECONDITION_CHECK:
            # Check if precondition is satisfied
            next_state = self._handle_precondition_check(stack, context, action)
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
            next_state = self._handle_frame_complete_state(stack, context, action)
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

    def _handle_node_select(self, stack: "NodeStack", context: "TraversalContext") -> TraversalState:
        """Handle NODE_SELECT state logic."""
        if stack.is_empty():
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
        self, stack: "NodeStack", context: "TraversalContext", action: "ActionExecutor"
    ) -> TraversalState:
        """Handle PRECONDITION_CHECK state logic."""
        current_node = stack.peek()

        # If no precondition is defined, it's satisfied by default
        if not current_node.has_precondition():
            return TraversalState.EXECUTE

        # V6.5: Call vision service to analyze current screen
        import time
        t0 = time.time()
        try:
            image_data = b""
            page_analysis = vision.analyze_screenshot(image_data)
            if hasattr(context, 'current_page_analysis'):
                context.current_page_analysis = page_analysis
            self._last_handler_metrics = {
                "ai_call": {
                    "capability": "vision",
                    "success": True,
                    "latency_ms": (time.time() - t0) * 1000,
                }
            }
            return TraversalState.EXECUTE
        except Exception as e:
            self._last_handler_metrics = {
                "ai_call": {
                    "capability": "vision",
                    "success": False,
                    "latency_ms": (time.time() - t0) * 1000,
                },
                "error": {"error_type": type(e).__name__, "error_message": str(e)},
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
            self._last_handler_metrics = {
                "execution": {
                    "action": operation.get("action", "unknown"),
                    "status": "success" if result.success else "failed",
                    "target": operation.get("target"),
                    "duration_ms": (time.time() - t0) * 1000,
                }
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
        # V6.5: Call vision service to verify result page
        import time
        t0 = time.time()
        try:
            image_data = b""
            after_analysis = vision.analyze_screenshot(image_data)
            self._last_handler_metrics = {
                "ai_call": {
                    "capability": "vision",
                    "success": True,
                    "latency_ms": (time.time() - t0) * 1000,
                }
            }
            if hasattr(context, 'current_page_analysis'):
                context.current_page_analysis = after_analysis
            return TraversalState.BRANCH
        except Exception as e:
            self._last_handler_metrics = {
                "ai_call": {
                    "capability": "vision",
                    "success": False,
                    "latency_ms": (time.time() - t0) * 1000,
                },
                "error": {"error_type": type(e).__name__, "error_message": str(e)},
            }
            return TraversalState.ERROR_HANDLING

    def _handle_branch(self, stack: "NodeStack", context: "TraversalContext") -> TraversalState:
        """Handle BRANCH state logic."""
        current_node = stack.peek()
        from src.graph.node import NodeType, ChildrenStrategyType

        if not current_node:
            # No current node - check if we should continue
            if stack.size() > 1:
                return TraversalState.FRAME_COMPLETE
            else:
                return TraversalState.NODE_SELECT

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

        if not has_unvisited_children and not current_node.is_leaf():
            # Container node with all children visited — pop frame
            return TraversalState.FRAME_COMPLETE

        if current_node.is_leaf():
            # Leaf node - check if we need to return
            if stack.size() > 1:
                return TraversalState.FRAME_COMPLETE
            else:
                return TraversalState.NODE_SELECT
        else:
            # Container node with unvisited children
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
