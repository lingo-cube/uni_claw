"""
Operation executor interface and implementations.

Provides a pluggable interface for executing traversal operations,
with both mock and real implementations.
"""

from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Any, Dict, Optional
from datetime import datetime


@dataclass
class ExecutionContext:
    """Context for operation execution."""
    node_id: str
    node_name: str
    operation: Dict[str, Any]
    screen_info: Optional[Dict[str, Any]] = None
    timestamp: Optional[datetime] = None


@dataclass
class ExecutionResult:
    """Result of operation execution."""
    success: bool
    action: str  # Actual action performed (e.g., "click: Settings")
    error: Optional[str] = None
    metadata: Dict[str, Any] = None

    def __post_init__(self):
        if self.metadata is None:
            self.metadata = {}


class OperationExecutor(ABC):
    """
    Interface for executing traversal operations.

    Implementations can be mock (for simulation) or real (for actual device control).
    """

    @abstractmethod
    def execute(self, context: ExecutionContext) -> ExecutionResult:
        """
        Execute an operation.

        Args:
            context: Execution context with operation details

        Returns:
            ExecutionResult with success status and details
        """
        pass

    @abstractmethod
    def get_executed_actions(self) -> list[str]:
        """
        Get list of all executed actions.

        Returns:
            List of action descriptions (e.g., ["click: Settings", "back"])
        """
        pass

    @abstractmethod
    def clear_history(self) -> None:
        """Clear execution history."""
        pass


class MockOperationExecutor(OperationExecutor):
    """
    Mock operation executor for simulation.

    Records operations without actually executing them.
    """

    def __init__(self):
        self._executed_actions: list[Dict[str, Any]] = []

    def execute(self, context: ExecutionContext) -> ExecutionResult:
        """
        Simulate executing an operation.

        Parses the operation and creates a human-readable description.
        """
        operation = context.operation
        action = operation.get("action", "unknown")
        target = operation.get("target")
        params = operation.get("params", {})

        # Build action description
        action_desc = self._build_action_description(action, target, params)

        # Record the action
        self._executed_actions.append({
            "node_id": context.node_id,
            "node_name": context.node_name,
            "action": action,
            "target": target,
            "params": params,
            "description": action_desc,
            "timestamp": context.timestamp or datetime.now(),
        })

        return ExecutionResult(
            success=True,
            action=action_desc,
            metadata={"mock": True}
        )

    def _build_action_description(
        self, action: str, target: Optional[Dict[str, Any]], params: Dict[str, Any]
    ) -> str:
        """Build human-readable action description."""
        if action == "click":
            if target:
                by = target.get("by", "unknown")
                value = target.get("value", "unknown")
                return f"click: {value}"
            return "click"
        elif action == "back":
            return "back"
        elif action == "input_text":
            text = params.get("text", "")
            return f"input: {text}"
        elif action == "swipe":
            direction = params.get("direction", "unknown")
            return f"swipe: {direction}"
        elif action == "no_action":
            duration = params.get("duration_ms", 0)
            return f"wait: {duration}ms"
        else:
            return f"{action}: {target.get('value', '') if target else ''}"

    def get_executed_actions(self) -> list[str]:
        """Get list of all executed actions."""
        return [action["description"] for action in self._executed_actions]

    def get_action_history(self) -> list[Dict[str, Any]]:
        """Get full action history."""
        return self._executed_actions.copy()

    def clear_history(self) -> None:
        """Clear execution history."""
        self._executed_actions = []


class RealOperationExecutor(OperationExecutor):
    """
    Real operation executor for actual device control.

    Uses ADB or other device control mechanisms to execute operations.
    """

    def __init__(self, adb_client=None):
        """
        Initialize the real operation executor.

        Args:
            adb_client: Optional ADB client for device communication
        """
        self._adb_client = adb_client
        self._executed_actions: list[Dict[str, Any]] = []

    def execute(self, context: ExecutionContext) -> ExecutionResult:
        """
        Execute an operation on real device.

        Args:
            context: Execution context with operation details

        Returns:
            ExecutionResult with success status and details
        """
        operation = context.operation
        action = operation.get("action", "unknown")

        try:
            # Execute based on action type
            if action == "click":
                result = self._execute_click(operation)
            elif action == "back":
                result = self._execute_back()
            elif action == "input_text":
                result = self._execute_input_text(operation)
            elif action == "swipe":
                result = self._execute_swipe(operation)
            else:
                result = ExecutionResult(
                    success=False,
                    action=f"unknown: {action}",
                    error=f"Unknown action: {action}"
                )

            # Record successful action
            if result.success:
                self._executed_actions.append({
                    "node_id": context.node_id,
                    "node_name": context.node_name,
                    "action": action,
                    "description": result.action,
                    "timestamp": datetime.now(),
                })

            return result

        except Exception as e:
            return ExecutionResult(
                success=False,
                action=f"{action}: failed",
                error=str(e)
            )

    def _execute_click(self, operation: Dict[str, Any]) -> ExecutionResult:
        """Execute click operation."""
        target = operation.get("target", {})
        by = target.get("by", "text")
        value = target.get("value", "")

        # TODO: Implement actual click via ADB
        # if self._adb_client:
        #     self._adb_client.tap(by, value)

        return ExecutionResult(
            success=True,
            action=f"click: {value}"
        )

    def _execute_back(self) -> ExecutionResult:
        """Execute back operation."""
        # TODO: Implement actual back via ADB
        # if self._adb_client:
        #     self._adb_client.press_back()

        return ExecutionResult(
            success=True,
            action="back"
        )

    def _execute_input_text(self, operation: Dict[str, Any]) -> ExecutionResult:
        """Execute input text operation."""
        params = operation.get("params", {})
        text = params.get("text", "")

        # TODO: Implement actual input via ADB
        # if self._adb_client:
        #     self._adb_client.input_text(text)

        return ExecutionResult(
            success=True,
            action=f"input: {text}"
        )

    def _execute_swipe(self, operation: Dict[str, Any]) -> ExecutionResult:
        """Execute swipe operation."""
        params = operation.get("params", {})
        direction = params.get("direction", "down")

        # TODO: Implement actual swipe via ADB
        # if self._adb_client:
        #     self._adb_client.swipe(direction)

        return ExecutionResult(
            success=True,
            action=f"swipe: {direction}"
        )

    def get_executed_actions(self) -> list[str]:
        """Get list of all executed actions."""
        return [action["description"] for action in self._executed_actions]

    def get_action_history(self) -> list[Dict[str, Any]]:
        """Get full action history."""
        return self._executed_actions.copy()

    def clear_history(self) -> None:
        """Clear execution history."""
        self._executed_actions = []
