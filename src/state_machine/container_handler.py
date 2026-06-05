"""
Container handling system for V6.1 traversal state machine.

This module provides comprehensive container node traversal support,
including completion detection and fallback action decision logic.
"""

from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Dict, List, Optional, Set
import logging
import time
from datetime import datetime, timedelta

logger = logging.getLogger(__name__)


class CompletionStatus(str, Enum):
    """Status of container frame completion."""

    ALL_VISITED = "ALL_VISITED"  # All children have been visited
    MAX_DEPTH = "MAX_DEPTH"  # Maximum depth reached
    TIMEOUT = "TIMEOUT"  # Processing timeout exceeded
    INCOMPLETE = "INCOMPLETE"  # Still processing, not complete
    ERROR = "ERROR"  # Error occurred during processing


class FallbackAction(str, Enum):
    """Fallback actions for container completion."""

    BACK = "back"  # Press back and pop frame
    AUTO_ESCAPE = "auto_escape"  # Try sibling menu, or back if none
    SKIP = "skip"  # Just pop frame
    ABORT = "abort"  # Abort traversal


@dataclass
class ContainerContext:
    """Container processing context."""

    container_node: Dict[str, Any]
    visited_children: List[str] = field(default_factory=list)
    total_children: int = 0
    current_depth: int = 0
    max_depth: int = 10
    completion_status: CompletionStatus = CompletionStatus.INCOMPLETE
    fallback_action: FallbackAction = FallbackAction.BACK
    processing_start_time: float = field(default_factory=time.time)
    elapsed_time_ms: float = 0.0
    timeout_seconds: int = 60


@dataclass
class FrameCompleteResult:
    """Frame completion detection result."""

    is_complete: bool
    completion_reason: str
    remaining_children: List[str]
    suggested_action: FallbackAction
    can_continue: bool
    should_backtrack: bool
    depth_limit_reached: bool = False
    timeout_exceeded: bool = False


class CompletionDetector:
    """Detect container frame completion conditions."""

    def __init__(self):
        """Initialize completion detector."""
        self._detection_cache: Dict[str, FrameCompleteResult] = {}

    def detect_completion(
        self,
        container: Dict[str, Any],
        context: ContainerContext
    ) -> FrameCompleteResult:
        """
        Detect if container frame is complete.

        Args:
            container: Container node information
            context: Current container processing context

        Returns:
            FrameCompleteResult with completion details
        """
        container_id = container.get("id", "unknown")
        cache_key = f"{container_id}_{context.visited_children}_{context.current_depth}"

        # Check cache
        if cache_key in self._detection_cache:
            return self._detection_cache[cache_key]

        # Calculate elapsed time
        elapsed = time.time() - context.processing_start_time
        context.elapsed_time_ms = elapsed * 1000

        # Check completion conditions in priority order
        result = self._check_completion_conditions(container, context)

        # Cache result
        self._detection_cache[cache_key] = result

        return result

    def _check_completion_conditions(
        self,
        container: Dict[str, Any],
        context: ContainerContext
    ) -> FrameCompleteResult:
        """
        Check all completion conditions.

        Args:
            container: Container node information
            context: Current container processing context

        Returns:
            FrameCompleteResult with completion details
        """
        # Check timeout first (safety condition)
        if context.elapsed_time_ms > (context.timeout_seconds * 1000):
            return FrameCompleteResult(
                is_complete=True,
                completion_reason="TIMEOUT",  # Changed to match CompletionStatus enum
                remaining_children=self._get_remaining_children(context),
                suggested_action=FallbackAction.BACK,
                can_continue=False,
                should_backtrack=True,
                timeout_exceeded=True,
            )

        # Check max depth (safety condition)
        if context.current_depth >= context.max_depth:
            return FrameCompleteResult(
                is_complete=True,
                completion_reason="MAX_DEPTH",  # Changed to match CompletionStatus enum
                remaining_children=self._get_remaining_children(context),
                suggested_action=FallbackAction.BACK,
                can_continue=False,
                should_backtrack=True,
                depth_limit_reached=True,
            )

        # Check if container has no children (edge case)
        if context.total_children == 0:
            return FrameCompleteResult(
                is_complete=True,
                completion_reason="ALL_VISITED",  # Empty containers are considered complete
                remaining_children=[],
                suggested_action=FallbackAction.BACK,
                can_continue=True,
                should_backtrack=True,
            )

        # Check if all children visited (normal completion)
        if self._all_children_visited(context):
            return FrameCompleteResult(
                is_complete=True,
                completion_reason="ALL_VISITED",  # Changed to match CompletionStatus enum
                remaining_children=[],
                suggested_action=self._determine_fallback_action(container, context),
                can_continue=True,
                should_backtrack=True,
            )

        # Still processing
        return FrameCompleteResult(
            is_complete=False,
            completion_reason="INCOMPLETE",  # Changed to match CompletionStatus enum
            remaining_children=self._get_remaining_children(context),
            suggested_action=FallbackAction.BACK,  # Default suggestion
            can_continue=True,
            should_backtrack=False,
        )

    def _all_children_visited(self, context: ContainerContext) -> bool:
        """Check if all children have been visited."""
        return len(context.visited_children) >= context.total_children

    def _get_remaining_children(self, context: ContainerContext) -> List[str]:
        """Get list of remaining (unvisited) children."""
        if not context.container_node:
            return []

        all_children = context.container_node.get("children", [])
        visited_set = set(context.visited_children)
        return [child for child in all_children if child not in visited_set]

    def _determine_fallback_action(
        self,
        container: Dict[str, Any],
        context: ContainerContext
    ) -> FallbackAction:
        """
        Determine appropriate fallback action based on container properties.

        Args:
            container: Container node information
            context: Current container processing context

        Returns:
            Appropriate FallbackAction
        """
        # Check container's exit_condition if available
        exit_condition = container.get("exit_condition", "").upper()

        try:
            return FallbackAction(exit_condition)
        except (ValueError, AttributeError):
            # Default to BACK if exit_condition is invalid or missing
            return FallbackAction.BACK


class FallbackDecider:
    """Decide fallback actions for container processing."""

    def __init__(self):
        """Initialize fallback decider."""
        self._decision_cache: Dict[str, FallbackAction] = {}

    def decide_fallback(
        self,
        completion_result: FrameCompleteResult,
        context: ContainerContext
    ) -> FallbackAction:
        """
        Decide fallback action based on completion status.

        Args:
            completion_result: Result from completion detection
            context: Current container processing context

        Returns:
            Decided FallbackAction
        """
        # Create cache key
        cache_key = f"{completion_result.completion_reason}_{context.current_depth}_{context.total_children}"

        # Check cache
        if cache_key in self._decision_cache:
            return self._decision_cache[cache_key]

        # Decide action
        action = self._make_decision(completion_result, context)

        # Cache decision
        self._decision_cache[cache_key] = action

        return action

    def _make_decision(
        self,
        completion_result: FrameCompleteResult,
        context: ContainerContext
    ) -> FallbackAction:
        """
        Make fallback decision based on completion result and context.

        Args:
            completion_result: Result from completion detection
            context: Current container processing context

        Returns:
            Decided FallbackAction
        """
        # Safety conditions - always use BACK
        if completion_result.timeout_exceeded or completion_result.depth_limit_reached:
            return FallbackAction.BACK

        # Normal completion - use suggested action or default
        if completion_result.is_complete:
            if completion_result.suggested_action:
                return completion_result.suggested_action
            return FallbackAction.BACK  # Default fallback

        # Still processing but need to handle somehow
        if not completion_result.can_continue:
            return FallbackAction.BACK  # Default to BACK if can't continue

        # Incomplete but can continue - use SKIP to move on
        return FallbackAction.SKIP


class ContainerActionExecutor:
    """Execute container fallback actions."""

    def __init__(self):
        """Initialize container action executor."""
        self._action_hooks = {
            FallbackAction.BACK: self._execute_back_action,
            FallbackAction.AUTO_ESCAPE: self._execute_auto_escape_action,
            FallbackAction.SKIP: self._execute_skip_action,
            FallbackAction.ABORT: self._execute_abort_action,
        }

    def execute_fallback(
        self,
        action: FallbackAction,
        context: ContainerContext
    ) -> Dict[str, Any]:
        """
        Execute fallback action.

        Args:
            action: Fallback action to execute
            context: Current container processing context

        Returns:
            Execution result dictionary
        """
        start_time = time.time()
        success = False
        result_message = "action_not_executed"
        state_changes = {}

        try:
            action_hook = self._action_hooks.get(action)
            if action_hook:
                result = action_hook(context)
                success = result.get('success', False)
                result_message = result.get('message', action.value)
                state_changes = result.get('state_changes', {})
            else:
                result_message = f"Unknown action: {action}"
                logger.error(result_message)

        except Exception as e:
            result_message = f"Action execution failed: {e}"
            logger.error(result_message)
            # Fall back to safe BACK action
            result = self._execute_back_action(context)
            success = result.get('success', False)
            state_changes = result.get('state_changes', {})

        execution_time = (time.time() - start_time) * 1000

        return {
            'success': success,
            'action': action.value,
            'message': result_message,
            'state_changes': state_changes,
            'execution_time_ms': execution_time,
        }

    def _execute_back_action(self, context: ContainerContext) -> Dict[str, Any]:
        """Execute BACK fallback action."""
        return {
            'success': True,
            'message': 'back_to_parent',
            'state_changes': {
                'action': 'press_back',
                'pop_frame': True,
                'restore_parent': True,
            }
        }

    def _execute_auto_escape_action(self, context: ContainerContext) -> Dict[str, Any]:
        """Execute AUTO_ESCAPE fallback action."""
        return {
            'success': True,
            'message': 'auto_escape_attempt',
            'state_changes': {
                'action': 'try_sibling_menu',
                'fallback_to_back': True,
                'pop_frame': True,
            }
        }

    def _execute_skip_action(self, context: ContainerContext) -> Dict[str, Any]:
        """Execute SKIP fallback action."""
        return {
            'success': True,
            'message': 'skip_container',
            'state_changes': {
                'action': 'skip_remaining',
                'pop_frame': True,
                'mark_complete': True,
            }
        }

    def _execute_abort_action(self, context: ContainerContext) -> Dict[str, Any]:
        """Execute ABORT fallback action."""
        return {
            'success': False,
            'message': 'abort_traversal',
            'state_changes': {
                'action': 'abort',
                'stop_traversal': True,
                'cleanup': True,
            }
        }


class ContainerHandler:
    """Complete container handling system for V6.1."""

    def __init__(self):
        """Initialize container handler."""
        self.completion_detector = CompletionDetector()
        self.fallback_decider = FallbackDecider()
        self.action_executor = ContainerActionExecutor()

        # Statistics
        self.processed_count = 0
        self.completed_count = 0
        self.action_statistics: Dict[str, int] = {}
        self.total_depth = 0
        self.total_processing_time_ms = 0.0

    def handle_frame_complete(
        self,
        container: Dict[str, Any],
        context: ContainerContext
    ) -> Dict[str, Any]:
        """
        Handle container frame completion.

        Args:
            container: Container node information
            context: Current container processing context

        Returns:
            Handling result with action taken
        """
        self.processed_count += 1
        start_time = time.time()

        # Detect completion status
        completion_result = self.completion_detector.detect_completion(container, context)

        # Decide fallback action
        fallback_action = self.fallback_decider.decide_fallback(completion_result, context)

        # Update context
        context.completion_status = CompletionStatus(completion_result.completion_reason)
        context.fallback_action = fallback_action

        # Execute fallback action if complete
        execution_result = {}
        if completion_result.is_complete:
            execution_result = self.action_executor.execute_fallback(fallback_action, context)
            self.completed_count += 1

        # Update statistics
        action_name = fallback_action.value
        self.action_statistics[action_name] = self.action_statistics.get(action_name, 0) + 1
        self.total_depth += context.current_depth

        processing_time = (time.time() - start_time) * 1000
        self.total_processing_time_ms += processing_time

        return {
            'container_id': container.get('id', 'unknown'),
            'is_complete': completion_result.is_complete,
            'completion_reason': completion_result.completion_reason,
            'fallback_action': fallback_action.value,
            'execution_result': execution_result,
            'processing_time_ms': processing_time,
            'remaining_children': completion_result.remaining_children,
        }

    @property
    def avg_depth(self) -> float:
        """Calculate average depth processed."""
        if self.processed_count == 0:
            return 0.0
        return self.total_depth / self.processed_count

    def get_container_statistics(self) -> Dict[str, Any]:
        """Get comprehensive container processing statistics."""
        return {
            "processed_containers": self.processed_count,
            "completed_containers": self.completed_count,
            "completion_rate": self.completed_count / self.processed_count if self.processed_count > 0 else 0.0,
            "average_depth": self.avg_depth,
            "fallback_actions": self.action_statistics.copy(),
            "total_processing_time_ms": self.total_processing_time_ms,
        }