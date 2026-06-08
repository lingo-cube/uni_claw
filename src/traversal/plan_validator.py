"""Traversal plan validation — checks structural correctness."""

from src.graph.node import NodeType
from src.graph.plan import TraversalPlan
from src.exception.initialization import ConfigurationError


class PlanValidator:
    """Validates a TraversalPlan before execution."""

    @staticmethod
    def validate(plan: TraversalPlan) -> None:
        """Raise ConfigurationError if the plan is structurally invalid."""
        if plan.root_node is None:
            raise ConfigurationError("root_node is required in traversal plan")

        root = plan.root_node

        if root.node_type != NodeType.CONTAINER:
            raise ConfigurationError(
                f"Root node must be CONTAINER type, got {root.node_type.value}"
            )

        if root.operation.action != "no_action":
            raise ConfigurationError(
                f"Root node operation should be 'no_action', got '{root.operation.action}'"
            )
