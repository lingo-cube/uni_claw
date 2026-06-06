"""
Mock action executor for V6.4 simulation.

Implements OperationExecutor ABC for compatibility with GraphTraversalEngine.
Records all operations without actually controlling a device.
"""

import time
from datetime import datetime
from typing import Any, Dict, List, Optional

from typing import TypedDict

from .operation_executor import ExecutionContext, ExecutionResult, OperationExecutor


class OperationRecord(TypedDict):
    """Legacy operation record type (backward-compatible stub)."""
    action_type: str
    timestamp: float
    result: str
    current_node: Optional[str]
    current_path: List[str]
    page_context: Dict[str, Any]
    target_info: Dict[str, Any]
    metadata: Dict[str, Any]
    node_stack: List[str]


class MockActionExecutor(OperationExecutor):
    """Mock action executor implementing OperationExecutor ABC.

    Records operations via execute() without performing real device actions.
    Compatible with GraphTraversalEngine injection.
    """

    def __init__(self, simulate_delay: float = 0.0):
        self._history: List[Dict[str, Any]] = []
        self.simulate_delay = simulate_delay

    # -- OperationExecutor ABC implementation --------------------------------

    def execute(self, context: ExecutionContext) -> ExecutionResult:
        """Record an operation and return success.

        Simulates device latency if simulate_delay > 0.
        """
        if self.simulate_delay > 0:
            time.sleep(self.simulate_delay)

        op = context.operation
        action_name = op.get("action", "unknown")
        target = op.get("target")

        self._history.append({
            "node_id": context.node_id,
            "node_name": context.node_name,
            "operation": op,
            "action_type": action_name,
            "target": target,
            "timestamp": context.timestamp or datetime.now(),
            "result": "success",
        })
        return ExecutionResult(
            success=True,
            action=f"{action_name}: {target}" if target else action_name,
        )

    def get_executed_actions(self) -> list[str]:
        return [
            h.get("operation", {}).get("action", "unknown")
            for h in self._history
        ]

    def clear_history(self) -> None:
        self._history.clear()

    # -- Legacy helpers (kept for test compatibility) -----------------------

    @property
    def action_history(self) -> List[Dict[str, Any]]:
        """Backward-compatible alias for history."""
        return list(self._history)

    @property
    def history(self) -> List[Dict[str, Any]]:
        return list(self._history)

    def get_history(self) -> List[Dict[str, Any]]:
        return list(self._history)

    def get_operation_count(self) -> int:
        return len(self._history)

    def get_action_count(self) -> int:
        return len(self._history)

    def reset(self) -> None:
        self._history.clear()
