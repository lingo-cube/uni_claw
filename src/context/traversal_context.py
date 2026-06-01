"""Traversal context for AI advisor.

This module provides the TraversalContext dataclass, which encapsulates
read-only runtime state passed to AI advisors.
"""

from dataclasses import dataclass, field
from typing import Dict, List, Set, Optional
from datetime import datetime


@dataclass(frozen=True)
class ErrorRecord:
    """Record of a failed node."""

    node_id: str
    error_type: str
    timestamp: datetime
    retry_count: int


@dataclass(frozen=True)
class ActionRecord:
    """Record of an action taken."""

    action_type: str
    target: Optional[str]
    timestamp: datetime
    result: Optional[str]


@dataclass(frozen=True)
class TraversalContext:
    """Read-only runtime state for AI advisors.

    This dataclass encapsulates the current traversal state, providing
    AI advisors with the context they need to make informed decisions.
    """

    node_stack: List[str] = field(default_factory=list)
    current_path: List[str] = field(default_factory=list)
    visited_pages: Set[str] = field(default_factory=set)
    failed_nodes: Dict[str, ErrorRecord] = field(default_factory=dict)
    action_history: List[ActionRecord] = field(default_factory=list)
    inference_history: List["ContainerInference"] = field(default_factory=list)
    goal_attempts: Dict[str, int] = field(default_factory=dict)

    def __post_init__(self):
        """Enforce history limits."""
        # Limit action history to 5 items
        if len(self.action_history) > 5:
            object.__setattr__(self, "action_history", self.action_history[-5:])

        # Limit inference history to 3 items
        if len(self.inference_history) > 3:
            object.__setattr__(self, "inference_history", self.inference_history[-3:])

    def to_json(self) -> str:
        """Serialize to JSON.

        Returns:
            JSON string representation of the context.
        """
        import json

        def _convert_sets(obj):
            """Convert sets to lists for JSON serialization."""
            if isinstance(obj, set):
                return list(obj)
            if isinstance(obj, datetime):
                return obj.isoformat()
            if hasattr(obj, "__dict__"):
                return {k: _convert_sets(v) for k, v in obj.__dict__.items()}
            if isinstance(obj, dict):
                return {k: _convert_sets(v) for k, v in obj.items()}
            if isinstance(obj, list):
                return [_convert_sets(item) for item in obj]
            return obj

        data = {
            "node_stack": self.node_stack,
            "current_path": self.current_path,
            "visited_pages": list(self.visited_pages),
            "failed_nodes": _convert_sets(self.failed_nodes),
            "action_history": _convert_sets(self.action_history),
            "inference_history": _convert_sets(self.inference_history),
            "goal_attempts": self.goal_attempts,
        }
        return json.dumps(data, indent=2)


__all__ = ["TraversalContext", "ErrorRecord", "ActionRecord"]
