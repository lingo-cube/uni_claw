"""
Factory functions for creating test artifacts.

Provides convenient constructors for creating minimal test instances
of TraversalPlan, TraversalNode, and MockVisionService.
"""

from typing import Any, Dict, List, Optional

from src.graph.node import (
    CompletionPolicy,
    CompletionPolicyType,
    EntryConfig,
    EntryPolicy,
    EntryStrategy,
    IntentSlots,
    MatchMode,
    NodeType,
    TraversalMode,
    TraversalNode,
)
from src.graph.plan import TraversalPlan
from src.simulation.mock_vision import MockVisionService


def create_minimal_plan(
    entry_app: str = "test_app",
    max_depth: int = 2,
    mode: TraversalMode = TraversalMode.CONCRETE,
) -> TraversalPlan:
    """Create a minimal valid TraversalPlan for testing.

    Args:
        entry_app: Target application name
        max_depth: Maximum traversal depth (stored in intent_slots)
        mode: Traversal mode (default: depth-first)

    Returns:
        Valid TraversalPlan with CONTAINER root node
    """
    root_node = create_test_node(
        node_id="root",
        node_type=NodeType.CONTAINER,
        name="Root",
    )

    return TraversalPlan(
        entry_app=entry_app,
        entry_policy=EntryPolicy(
            strategy=EntryStrategy.BIND_CURRENT_SCREEN,
        ),
        root_node=root_node,
        mode=mode,
        intent_slots=IntentSlots(depth=max_depth),
    )


def create_test_node(
    node_id: str = "test_node",
    node_type: NodeType = NodeType.LEAF_ACTION,
    name: str = "Test",
    operation: Optional[str] = None,
    **kwargs,
) -> TraversalNode:
    """Create a TraversalNode with common test defaults.

    Args:
        node_id: Unique identifier for the node
        node_type: Type of node (default: LEAF_ACTION)
        name: Human-readable name
        operation: Optional operation action (defaults based on node_type)
        **kwargs: Additional TraversalNode fields (precondition, error_policy, etc.)

    Returns:
        Configured TraversalNode instance
    """
    from src.graph.node import Operation, ChildrenStrategy, ChildrenStrategyType

    # Determine default operation
    if operation is None:
        operation = "no_action" if node_type == NodeType.CONTAINER else "click"

    # Set default children strategy
    children_strategy = kwargs.pop("children_strategy", None)
    if children_strategy is None:
        if node_type == NodeType.CONTAINER:
            # Container nodes need DYNAMIC_MATCH or STATIC, not NONE
            children_strategy = ChildrenStrategy(
                type=ChildrenStrategyType.DYNAMIC_MATCH,
                dynamic_rules={}
            )
        else:
            children_strategy = ChildrenStrategy(type=ChildrenStrategyType.NONE)

    # Set default error policy if provided
    error_policy = kwargs.pop("error_policy", None)

    # Set precondition if provided
    precondition = kwargs.pop("precondition", None)

    return TraversalNode(
        node_id=node_id,
        name=name,
        node_type=node_type,
        operation=Operation(action=operation),
        children_strategy=children_strategy,
        error_policy=error_policy,
        precondition=precondition,
        meta=kwargs.pop("meta", {}) or {},
    )


def create_mock_vision(
    virtual_pages: Optional[Dict[str, Dict[str, Any]]] = None,
) -> MockVisionService:
    """Create a MockVisionService configured with virtual pages.

    Args:
        virtual_pages: Optional dict of page_path -> page_data.
                      If None, creates minimal default pages.

    Returns:
        Configured MockVisionService instance
    """
    if virtual_pages is None:
        # Create minimal default pages
        virtual_pages = {
            "root": {
                "page_name": "HomeScreen",
                "elements": [
                    {
                        "id": "settings_button",
                        "type": "button",
                        "text": "Settings",
                        "coordinate": {"x": 0.5, "y": 0.3},
                        "clickable": True,
                    }
                ],
            },
            "settings": {
                "page_name": "SettingsPage",
                "elements": [
                    {
                        "id": "display_option",
                        "type": "menu_item",
                        "text": "Display",
                        "coordinate": {"x": 0.5, "y": 0.4},
                        "clickable": True,
                    }
                ],
            },
        }

    return MockVisionService(virtual_pages)


# Convenience functions for creating specific node types


def create_container_node(node_id: str, text: str, children: List[TraversalNode]) -> TraversalNode:
    """Create a CONTAINER type node with children."""
    return create_test_node(
        node_id=node_id,
        node_type=NodeType.CONTAINER,
        text=text,
        children=children,
    )


def create_action_node(
    node_id: str,
    text: str,
    coordinate: Optional[Dict[str, float]] = None,
) -> TraversalNode:
    """Create a LEAF_ACTION type node."""
    return create_test_node(
        node_id=node_id,
        node_type=NodeType.LEAF_ACTION,
        text=text,
        coordinate=coordinate,
    )


def create_target_node(
    node_id: str,
    text: str,
    coordinate: Optional[Dict[str, float]] = None,
) -> TraversalNode:
    """Create a TARGET type node."""
    return create_test_node(
        node_id=node_id,
        node_type=NodeType.TARGET,
        text=text,
        coordinate=coordinate,
    )
