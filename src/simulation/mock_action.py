"""
Mock action executor for V6 simulation.

Provides virtual device control without requiring real devices.
Enhanced with comprehensive operation recording for testing assertions.
"""

import time
from typing import Any, Dict, List, Optional, Tuple, TypedDict


class OperationRecord(TypedDict):
    """Complete operation record structure."""
    # Basic operation info
    action_type: str
    timestamp: float
    result: str

    # Context information
    current_node: Optional[str]
    current_path: List[str]
    page_context: Dict[str, Any]

    # Target information
    target_info: Dict[str, Any]

    # Operation details
    metadata: Dict[str, Any]

    # Debug stack information
    node_stack: List[str]


class MockActionExecutor:
    """
    Mock action executor for simulation testing.

    Records all actions without actually controlling a device.
    """

    def __init__(self, simulate_delay: float = 0.0):
        """
        Initialize mock action executor with comprehensive recording.

        Args:
            simulate_delay: Optional delay in seconds to simulate device latency
        """
        self.action_history: List[OperationRecord] = []
        self.simulate_delay = simulate_delay
        self._operation_context: Dict[str, Any] = {}
        self._page_context: Optional[Dict[str, Any]] = None
        self._node_stack: List[str] = []

    def set_context(self, context: Any) -> None:
        """
        Set operation context for comprehensive recording.

        Args:
            context: Context object with current traversal state
        """
        self._operation_context = {
            "current_node": getattr(context, 'current_node', None),
            "current_path": getattr(context, 'current_path', []),
            "depth": getattr(context, 'depth', 0)
        }

    def set_page_context(self, page_context: Dict[str, Any]) -> None:
        """
        Set page context for operation recording.

        Args:
            page_context: Page context dictionary
        """
        self._page_context = page_context.copy() if page_context else {}

    def tap(self, x: float, y: float) -> bool:
        """
        Execute a tap action with comprehensive recording.

        Args:
            x: X coordinate (normalized 0-1)
            y: Y coordinate (normalized 0-1)

        Returns:
            True (always succeeds in mock)
        """
        return self._record_operation(
            action_type="tap",
            target_info={"x": x, "y": y},
            result=True,
            metadata={"delay": self.simulate_delay}
        )

    def swipe(
        self,
        start: Tuple[float, float],
        end: Tuple[float, float],
        duration: float = 0.3,
    ) -> bool:
        """
        Execute a swipe action with comprehensive recording.

        Args:
            start: Starting (x, y) coordinates
            end: Ending (x, y) coordinates
            duration: Swipe duration in seconds

        Returns:
            True (always succeeds in mock)
        """
        return self._record_operation(
            action_type="swipe",
            target_info={
                "start": list(start),
                "end": list(end),
                "duration": duration
            },
            result=True,
            metadata={"delay": self.simulate_delay}
        )

    def press_back(self) -> bool:
        """
        Execute a back button press with comprehensive recording.

        Returns:
            True (always succeeds in mock)
        """
        return self._record_operation(
            action_type="go_back",
            target_info={},
            result=True,
            metadata={"delay": self.simulate_delay}
        )

    def press_home(self) -> bool:
        """
        Execute a home button press with comprehensive recording.

        Returns:
            True (always succeeds in mock)
        """
        return self._record_operation(
            action_type="press_home",
            target_info={},
            result=True,
            metadata={"delay": self.simulate_delay}
        )

    def input_text(self, text: str, element_id: Optional[str] = None) -> bool:
        """
        Input text into the focused field with comprehensive recording.

        Args:
            text: Text to input
            element_id: Optional element ID for the input field

        Returns:
            True (always succeeds in mock)
        """
        return self._record_operation(
            action_type="input_text",
            target_info={"text": text, "element_id": element_id},
            result=True,
            metadata={"delay": self.simulate_delay}
        )

    def click(self, element_id: str, **kwargs) -> bool:
        """
        Execute a click action on an element.

        Args:
            element_id: ID of the element to click
            **kwargs: Additional parameters

        Returns:
            True (always succeeds in mock)
        """
        return self._record_operation(
            action_type="click",
            target_info={"element_id": element_id, **kwargs},
            result=True,
            metadata={"delay": self.simulate_delay}
        )

    def scroll(self, direction: str, distance: int = 1, **kwargs) -> bool:
        """
        Execute a scroll action with comprehensive recording.

        Args:
            direction: Scroll direction (up, down, left, right)
            distance: Scroll distance amount
            **kwargs: Additional parameters

        Returns:
            True (always succeeds in mock)
        """
        return self._record_operation(
            action_type="scroll",
            target_info={"direction": direction, "distance": distance, **kwargs},
            result=True,
            metadata={"delay": self.simulate_delay}
        )

    def go_back(self, **kwargs) -> bool:
        """
        Execute a back navigation action.

        Args:
            **kwargs: Additional parameters

        Returns:
            True (always succeeds in mock)
        """
        return self._record_operation(
            action_type="go_back",
            target_info=kwargs,
            result=True,
            metadata={"delay": self.simulate_delay}
        )

    def _record_operation(
        self,
        action_type: str,
        target_info: Optional[Dict[str, Any]] = None,
        result: bool = True,
        metadata: Optional[Dict[str, Any]] = None,
    ) -> bool:
        """
        Record comprehensive operation information.

        Args:
            action_type: Type of action being performed
            target_info: Information about the target of the action
            result: Whether the action succeeded
            metadata: Additional metadata about the operation

        Returns:
            The result parameter (True for success, False for failure)
        """
        # Update path based on action type
        self._update_path_for_action(action_type, target_info)

        operation_record: OperationRecord = {
            # Basic operation information
            "action_type": action_type,
            "timestamp": time.time(),
            "result": "success" if result else "failed",

            # Context information
            "current_node": self._operation_context.get("current_node"),
            "current_path": self._operation_context.get("current_path", []).copy(),
            "page_context": self._page_context.copy() if self._page_context else {},

            # Target information
            "target_info": target_info or {},

            # Operation details
            "metadata": metadata or {},

            # Debug stack information
            "node_stack": self._node_stack.copy(),
        }

        self.action_history.append(operation_record)

        # Simulate delay if configured
        if self.simulate_delay > 0:
            time.sleep(self.simulate_delay)

        return result

    def _update_path_for_action(self, action_type: str, target_info: Optional[Dict[str, Any]]) -> None:
        """
        Update current path based on action type.

        Args:
            action_type: Type of action being performed
            target_info: Information about the target of the action
        """
        current_path = self._operation_context.get("current_path", [])

        if action_type in ["click", "tap"]:
            # Extract element name or ID from target_info
            element_id = target_info.get("element_id") if target_info else None
            element_name = target_info.get("text") if target_info else None
            path_component = element_id or element_name

            if path_component and path_component not in current_path:
                current_path.append(str(path_component))
                self._operation_context["current_path"] = current_path

        elif action_type == "go_back":
            # Remove last path component
            if current_path:
                current_path.pop()
                self._operation_context["current_path"] = current_path

    def get_history(self) -> List[OperationRecord]:
        """
        Get a copy of the action history.

        Returns:
            Copy of action history list
        """
        return self.action_history.copy()

    def get_operations_by_type(self, action_type: str) -> List[OperationRecord]:
        """
        Filter action history by operation type.

        Args:
            action_type: Type of operation to filter by

        Returns:
            List of operations matching the specified type
        """
        return [op for op in self.action_history if op["action_type"] == action_type]

    def get_operation_count(self) -> int:
        """Get total number of operations executed."""
        return len(self.action_history)

    def reset(self) -> None:
        """Reset executor state for reuse in tests."""
        self.action_history.clear()
        self._operation_context.clear()
        self._page_context = None
        self._node_stack.clear()

    def push_node(self, node_id: str) -> None:
        """
        Push node onto stack for tracking nested calls.

        Args:
            node_id: ID of the node to push
        """
        self._node_stack.append(node_id)

    def pop_node(self) -> Optional[str]:
        """
        Pop node from stack.

        Returns:
            The popped node ID, or None if stack is empty
        """
        return self._node_stack.pop() if self._node_stack else None

    def clear_history(self) -> None:
        """Clear the action history."""
        self.action_history.clear()

    def get_action_count(self) -> int:
        """Get total number of actions executed."""
        return len(self.action_history)

    def get_tap_count(self) -> int:
        """Get number of tap actions."""
        return sum(1 for a in self.action_history if a["action_type"] == "tap")

    def get_back_count(self) -> int:
        """Get number of back actions."""
        return sum(1 for a in self.action_history if a["action_type"] == "go_back")

    def get_swipe_count(self) -> int:
        """Get number of swipe actions."""
        return sum(1 for a in self.action_history if a["action_type"] == "swipe")

    def has_action(self, action_type: str) -> bool:
        """Check if history contains an action of given type."""
        return any(a["action_type"] == action_type for a in self.action_history)

    def get_last_action(self) -> Optional[OperationRecord]:
        """Get the last action executed."""
        if self.action_history:
            return self.action_history[-1]
        return None
