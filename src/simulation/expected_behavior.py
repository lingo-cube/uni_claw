"""Expected behavior definition for simulation testing.

Defines YAML-based format for specifying expected simulation behavior,
including action sequences, page transitions, node visitation, and
completion modes.
"""

from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path
from typing import Any, Dict, List, Optional, Set, Union

import yaml


class CompletionMode(str, Enum):
    """Expected completion mode for a simulation run."""

    NORMAL = "normal"  # Completed successfully
    EXCEPTION = "exception"  # Completed with expected exception
    CANCELLED = "cancelled"  # User cancelled the operation
    TIMEOUT = "timeout"  # Operation timed out


@dataclass
class ExpectedAction:
    """Defines an expected action in the simulation.

    Attributes:
        action: Action type (click, back, swipe, no_action, etc.)
        node: Node ID that performs this action
        target: Optional target element identifier
        order: Expected order in the action sequence (0-based)
    """

    action: str
    node: str
    target: Optional[str] = None
    order: int = 0

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary representation."""
        return {
            "action": self.action,
            "node": self.node,
            "target": self.target,
            "order": self.order,
        }


@dataclass
class ExpectedPageTransition:
    """Defines an expected page transition.

    Attributes:
        from_page: Source page ID
        to_page: Target page ID
        trigger: Element ID that triggers the transition
        order: Expected order in the transition sequence (0-based)
    """

    from_page: str
    to_page: str
    trigger: str
    order: int = 0

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary representation."""
        return {
            "from": self.from_page,
            "to": self.to_page,
            "trigger": self.trigger,
            "order": self.order,
        }


@dataclass
class ExpectedBehavior:
    """Defines expected simulation behavior for validation.

    Loaded from YAML format, this defines what should happen during
    a simulation run for validation purposes.

    Attributes:
        scenario: Human-readable scenario name
        description: Detailed scenario description
        actions: List of expected actions in sequence
        page_transitions: List of expected page transitions
        visited_nodes: Set of expected visited node IDs
        final_state: Expected final execution state
        completion_mode: Expected completion mode
        expected_exception: Expected exception (for EXCEPTION mode)
    """

    scenario: str
    description: str
    actions: List[ExpectedAction] = field(default_factory=list)
    page_transitions: List[ExpectedPageTransition] = field(default_factory=list)
    visited_nodes: Set[str] = field(default_factory=set)
    final_state: str = "COMPLETED"
    completion_mode: CompletionMode = CompletionMode.NORMAL
    expected_exception: Optional[str] = None

    @classmethod
    def from_yaml(cls, yaml_path: Union[str, Path]) -> "ExpectedBehavior":
        """Load expected behavior from a YAML file.

        Args:
            yaml_path: Path to the YAML file

        Returns:
            ExpectedBehavior instance

        Raises:
            ValueError: If the YAML is invalid
            FileNotFoundError: If the file doesn't exist
        """
        path = Path(yaml_path)
        if not path.exists():
            raise FileNotFoundError(f"Expected behavior file not found: {yaml_path}")

        with open(path, "r") as f:
            data = yaml.safe_load(f)

        # Parse actions
        actions = []
        for action_data in data.get("actions", []):
            actions.append(ExpectedAction(
                action=action_data["action"],
                node=action_data["node"],
                target=action_data.get("target"),
                order=action_data.get("order", 0),
            ))

        # Parse page transitions
        transitions = []
        for trans_data in data.get("page_transitions", []):
            transitions.append(ExpectedPageTransition(
                from_page=trans_data["from"],
                to_page=trans_data["to"],
                trigger=trans_data["trigger"],
                order=trans_data.get("order", 0),
            ))

        # Parse completion mode
        completion_mode_str = data.get("completion_mode", "normal")
        try:
            completion_mode = CompletionMode(completion_mode_str)
        except ValueError:
            completion_mode = CompletionMode.NORMAL

        return cls(
            scenario=data.get("scenario", ""),
            description=data.get("description", ""),
            actions=actions,
            page_transitions=transitions,
            visited_nodes=set(data.get("visited_nodes", [])),
            final_state=data.get("final_state", "COMPLETED"),
            completion_mode=completion_mode,
            expected_exception=data.get("expected_exception"),
        )

    def validate(self) -> List[str]:
        """Validate the expected behavior definition.

        Returns:
            List of validation error messages (empty if valid)
        """
        errors: List[str] = []

        # Check actions have valid orders
        for i, action in enumerate(self.actions):
            if action.order != i:
                errors.append(
                    f"Action at index {i} has order={action.order}, expected {i}"
                )

        # Check visited_nodes is not empty
        if not self.visited_nodes:
            errors.append("visited_nodes cannot be empty")

        # Check completion_mode EXCEPTION has expected_exception
        if self.completion_mode == CompletionMode.EXCEPTION and not self.expected_exception:
            errors.append(
                "completion_mode is EXCEPTION but expected_exception is not specified"
            )

        # Check page_transitions orders
        for i, transition in enumerate(self.page_transitions):
            if transition.order != i:
                errors.append(
                    f"Page transition at index {i} has order={transition.order}, expected {i}"
                )

        return errors

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary representation."""
        return {
            "scenario": self.scenario,
            "description": self.description,
            "actions": [a.to_dict() for a in self.actions],
            "page_transitions": [t.to_dict() for t in self.page_transitions],
            "visited_nodes": list(self.visited_nodes),
            "final_state": self.final_state,
            "completion_mode": self.completion_mode.value,
            "expected_exception": self.expected_exception,
        }
