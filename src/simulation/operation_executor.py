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

