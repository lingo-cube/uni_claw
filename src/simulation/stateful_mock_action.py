"""Stateful mock action executor for simulation testing.

Implements OperationExecutor ABC with coordination to StatefulMockVisionService
for action history tracking and page transition simulation.

Key Features:
- Coordinates with StatefulMockVisionService for page transitions
- Tracks action history with metadata
- Extracts element IDs from operations for simulation
"""

from dataclasses import dataclass, field
from datetime import datetime
from typing import Any, Dict, List, Optional

from .operation_executor import ExecutionContext, ExecutionResult, OperationExecutor
from .stateful_mock_vision import StatefulMockVisionService


@dataclass
class ActionRecord:
    """Record of an executed action.

    Attributes:
        node_id: Node that executed this action
        node_name: Human-readable node name
        action_type: Type of action (click, back, swipe, etc.)
        target: Target element or coordinate
        element_id: Extracted element ID if applicable
        success: Whether the action succeeded
        timestamp: When the action was executed
        page_context: Page state at time of action
    """

    node_id: str
    node_name: str
    action_type: str
    target: Any
    element_id: Optional[str] = None
    success: bool = True
    timestamp: Optional[datetime] = None
    page_context: Dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary representation."""
        return {
            "node_id": self.node_id,
            "node_name": self.node_name,
            "action_type": self.action_type,
            "target": self.target,
            "element_id": self.element_id,
            "success": self.success,
            "timestamp": self.timestamp.isoformat() if self.timestamp else None,
            "page_context": self.page_context,
        }


class StatefulMockActionExecutor(OperationExecutor):
    """Mock action executor with vision service coordination.

    Coordinates with StatefulMockVisionService to:
    - Simulate page transitions when actions are executed
    - Track action history for validation
    - Provide context about executed actions

    Attributes:
        vision_service: The StatefulMockVisionService to coordinate with
        action_history: List of all executed actions
    """

    def __init__(self, vision_service: StatefulMockVisionService):
        """Initialize the stateful mock action executor.

        Args:
            vision_service: StatefulMockVisionService to coordinate with
        """
        self._vision_service = vision_service
        self._action_history: List[ActionRecord] = []

    # -- OperationExecutor ABC implementation --------------------------------

    def execute(self, context: ExecutionContext) -> ExecutionResult:
        """Execute an operation and coordinate with vision service.

        Args:
            context: Execution context with operation details

        Returns:
            ExecutionResult indicating success and action performed
        """
        op = context.operation
        action_name = op.get("action", "unknown")
        target = op.get("target")
        timestamp = context.timestamp or datetime.now()

        # Extract element ID from target if applicable
        element_id = self._extract_element_id(target)

        # Get current page context before action
        page_context = self._vision_service.get_current_page() or {}

        # Execute action and simulate page transition if applicable
        success = True
        if element_id and action_name in ("click", "swipe"):
            # Try to simulate the action on the vision service
            transition_success = self._vision_service.simulate_action(
                element_id=element_id,
                action=action_name,
            )
            success = transition_success
        elif action_name == "back":
            # Handle back navigation
            nav_success = self._vision_service.navigate_back()
            success = nav_success

        # Record the action
        record = ActionRecord(
            node_id=context.node_id,
            node_name=context.node_name,
            action_type=action_name,
            target=target,
            element_id=element_id,
            success=success,
            timestamp=timestamp,
            page_context=page_context,
        )
        self._action_history.append(record)

        # Build result message
        action_desc = f"{action_name}"
        if target:
            action_desc += f": {target}"

        return ExecutionResult(
            success=success,
            action=action_desc,
        )

    def get_executed_actions(self) -> list[str]:
        """Get list of all executed action descriptions.

        Returns:
            List of action description strings
        """
        return [
            f"{record.action_type}: {record.target}" if record.target else record.action_type
            for record in self._action_history
        ]

    def clear_history(self) -> None:
        """Clear action history."""
        self._action_history.clear()

    # -- Additional methods for stateful testing -----------------------------

    def get_history(self) -> List[ActionRecord]:
        """Get full action history with metadata.

        Returns:
            List of ActionRecord objects
        """
        return list(self._action_history)

    def reset(self) -> None:
        """Reset executor and vision service to initial state."""
        self.clear_history()
        self._vision_service.reset_to_initial()

    # -- Internal helpers -----------------------------------------------------

    def _extract_element_id(self, target: Any) -> Optional[str]:
        """Extract element ID from operation target.

        The target can be:
        - A string element ID directly
        - A dict with 'element_id' or 'value' key
        - A dict with 'by' and 'value' keys (e.g., {"by": "text", "value": "Wi-Fi"})
        - A Target object with 'by' and 'value' attributes
        - A MenuItem object
        - None

        Args:
            target: Operation target value

        Returns:
            Extracted element ID or None
        """
        if target is None:
            return None

        # Direct string element ID
        if isinstance(target, str):
            return target

        # Dict with various key formats
        if isinstance(target, dict):
            # Try common keys
            if "element_id" in target:
                return target["element_id"]
            if "value" in target:
                return target["value"]
            if "text" in target:
                return target["text"]
            # For {"by": "text", "value": "Wi-Fi"} format
            if "by" in target and "value" in target:
                return target["value"]

        # Target object (from src.graph.node.Target)
        # Has 'by' and 'value' attributes
        if hasattr(target, "by") and hasattr(target, "value"):
            return target.value

        # MenuItem object (has 'name' attribute, but we need 'id')
        # For compatibility, use the name if available
        if hasattr(target, "id"):
            return getattr(target, "id")
        if hasattr(target, "name"):
            return getattr(target, "name")

        return None

    # -- Properties -----------------------------------------------------------

    @property
    def action_count(self) -> int:
        """Get the number of executed actions."""
        return len(self._action_history)

    @property
    def vision_service(self) -> StatefulMockVisionService:
        """Get the coordinated vision service."""
        return self._vision_service
