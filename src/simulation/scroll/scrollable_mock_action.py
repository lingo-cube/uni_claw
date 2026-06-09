"""
Scrollable mock action executor for simulating scroll actions in tests.

Extends StatefulMockActionExecutor with scroll_down and scroll_up actions.
Coordinates with ScrollableMockVisionService for scroll simulation.
"""

import time
from typing import Any, Dict, List, Optional

from src.simulation.scroll.scrollable_mock_vision import ScrollableMockVisionService
from src.simulation.stateful_mock_action import StatefulMockActionExecutor
from src.simulation.stateful_mock_vision import StatefulMockVisionService

from .models import ScrollAction


class ScrollableMockActionExecutor(StatefulMockActionExecutor):
    """
    Mock action executor with scroll action support.

    Extends StatefulMockActionExecutor to support scroll_down and scroll_up actions
    that coordinate with ScrollableMockVisionService for scroll simulation.

    Attributes:
        vision_service: The ScrollableMockVisionService to coordinate with
        scroll_actions: List of scroll action records (ScrollAction objects)
    """

    def __init__(self, vision_service: ScrollableMockVisionService):
        """
        Initialize the scrollable mock action executor.

        Args:
            vision_service: ScrollableMockVisionService to coordinate with
        """
        super().__init__(vision_service)
        self._scroll_actions: List[ScrollAction] = []

    def execute(self, context) -> Any:
        """
        Execute an operation with scroll action support.

        Extends base class to handle scroll_down and scroll_up actions.

        Args:
            context: Execution context with operation details

        Returns:
            ExecutionResult indicating success and action performed
        """
        op = context.operation
        action_name = op.get("action", "unknown")

        # Handle scroll actions
        if action_name in ("scroll_down", "scroll_up"):
            return self._execute_scroll_action(context)

        # Delegate other actions to base class
        return super().execute(context)

    def _execute_scroll_action(self, context) -> Any:
        """
        Execute a scroll action.

        Args:
            context: Execution context with operation details

        Returns:
            ExecutionResult indicating success and action performed
        """
        op = context.operation
        action_name = op.get("action", "unknown")
        target = op.get("target", {})
        timestamp = time.time()

        # Get scroll step percent (default 0.3)
        step_percent = target.get("step_percent", 0.3) if isinstance(target, dict) else 0.3

        # Get current page context
        page_context = self._vision_service.get_current_page() or {}
        page_key = page_context.get("page_id", "unknown")

        # Get scroll progress before scroll
        before_progress = self._vision_service.get_scroll_progress()

        # Execute scroll
        if action_name == "scroll_down":
            delta = step_percent
            success = self._vision_service.simulate_scroll(delta)
        else:  # scroll_up
            delta = -step_percent
            success = self._vision_service.simulate_scroll(delta)

        # Get scroll progress after scroll
        after_progress = self._vision_service.get_scroll_progress()

        # Record scroll action
        scroll_action = ScrollAction(
            action=action_name.upper(),
            path=page_key,
            step_percent=step_percent,
            before_progress=before_progress,
            after_progress=after_progress,
            timestamp=timestamp,
        )
        self._scroll_actions.append(scroll_action)

        # Build result message
        action_desc = f"{action_name}: {step_percent:.2f}"

        # Create ExecutionResult-like return
        # Note: We're not importing ExecutionResult to avoid circular dependency
        return type("ExecutionResult", (), {
            "success": success,
            "action": action_desc,
        })()

    def _execute_scroll_down(self, step_percent: float = 0.3) -> bool:
        """
        Execute scroll_down action.

        Args:
            step_percent: Scroll step size (default 0.3)

        Returns:
            True if scroll succeeded
        """
        return self._vision_service.simulate_scroll(step_percent)

    def _execute_scroll_up(self, step_percent: float = 0.3) -> bool:
        """
        Execute scroll_up action.

        Args:
            step_percent: Scroll step size (default 0.3)

        Returns:
            True if scroll succeeded
        """
        return self._vision_service.simulate_scroll(-step_percent)

    def get_scroll_count(self, path: Optional[str] = None) -> int:
        """
        Get number of scroll actions for a page.

        Args:
            path: Optional page path. If None, uses current page.

        Returns:
            Number of scroll actions executed for the page
        """
        if path is None:
            page_context = self._vision_service.get_current_page() or {}
            path = page_context.get("page_id", "")

        return sum(1 for action in self._scroll_actions if action.path == path)

    def get_total_scroll_distance(self, path: Optional[str] = None) -> float:
        """
        Get total scroll distance for a page.

        Args:
            path: Optional page path. If None, uses current page.

        Returns:
            Cumulative scroll distance (sum of absolute deltas)
        """
        if path is None:
            page_context = self._vision_service.get_current_page() or {}
            path = page_context.get("page_id", "")

        total = 0.0
        for action in self._scroll_actions:
            if action.path == path:
                total += abs(action.after_progress - action.before_progress)
        return total

    def clear_scroll_history(self) -> None:
        """Clear scroll action history."""
        self._scroll_actions.clear()

    # -- Properties -----------------------------------------------------------

    @property
    def scroll_actions(self) -> List[ScrollAction]:
        """Get list of scroll action records."""
        return list(self._scroll_actions)

    @property
    def vision_service(self) -> ScrollableMockVisionService:
        """Get the coordinated vision service."""
        return self._vision_service
