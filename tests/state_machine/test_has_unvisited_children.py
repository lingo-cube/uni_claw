"""
Unit tests for GraphTraversalEngine._has_unvisited_children() method.

V6.10.2: Tests for the extracted unvisited children check logic.
Covers all scenarios: NONE, STATIC, DYNAMIC_MATCH strategies.
"""

import pytest
from src.traversal.graph_engine import GraphTraversalEngine
from src.graph.plan import TraversalPlan
from src.graph.node import TraversalNode, NodeType, ChildrenStrategy, ChildrenStrategyType, Operation
from src.trace.context import TraversalRuntimeContext
from unittest.mock import Mock, MagicMock


class TestHasUnvisitedChildren:
    """Test suite for _has_unvisited_children() method."""

    # ============================================================================
    # No Children Strategy Tests
    # ============================================================================

    def test_has_unvisited_children_no_strategy(self):
        """
        Given: A node without children_strategy attribute
        When: _has_unvisited_children is called
        Then: Returns False
        """
        # Setup: Create engine and context
        plan = TraversalPlan(entry_app="test_app", root_node=None)
        vision_service = Mock()
        action_executor = Mock()
        engine = GraphTraversalEngine(plan, vision_service, action_executor)
        context = TraversalRuntimeContext(max_depth=100)

        # Create mock node without children_strategy
        node = Mock()
        node.node_id = "test_leaf"
        node.children_strategy = None

        # Execute
        result = engine._has_unvisited_children(node, context)

        # Verify
        assert result is False, "Node without children_strategy should return False"

    # ============================================================================
    # NONE Strategy Tests
    # ============================================================================

    def test_has_unvisited_children_none_strategy(self):
        """
        Given: A LEAF node with NONE children_strategy
        When: _has_unvisited_children is called
        Then: Returns False
        """
        # Setup
        plan = TraversalPlan(entry_app="test_app", root_node=None)
        vision_service = Mock()
        action_executor = Mock()
        engine = GraphTraversalEngine(plan, vision_service, action_executor)
        context = TraversalRuntimeContext(max_depth=100)

        # Use LEAF_ACTION for NONE strategy (Container requires non-NONE strategy)
        node = TraversalNode(
            node_id="test_leaf",
            name="Test Leaf",
            node_type=NodeType.LEAF_ACTION,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(type=ChildrenStrategyType.NONE)
        )

        # Execute
        result = engine._has_unvisited_children(node, context)

        # Verify
        assert result is False, "NONE strategy should return False"

    # ============================================================================
    # STATIC Strategy Tests
    # ============================================================================

    def test_has_unvisited_children_static_no_children(self):
        """
        Given: A node with STATIC strategy but empty static_children list
        When: _has_unvisited_children is called
        Then: Returns False
        """
        # Setup
        plan = TraversalPlan(entry_app="test_app", root_node=None)
        vision_service = Mock()
        action_executor = Mock()
        engine = GraphTraversalEngine(plan, vision_service, action_executor)
        context = TraversalRuntimeContext(max_depth=100)

        node = TraversalNode(
            node_id="test_container",
            name="Test Container",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.STATIC,
                static_children=[]
            )
        )

        # Execute
        result = engine._has_unvisited_children(node, context)

        # Verify
        assert result is False, "STATIC with no children should return False"

    def test_has_unvisited_children_static_all_visited(self):
        """
        Given: A node with STATIC strategy, all children already visited
        When: _has_unvisited_children is called
        Then: Returns False
        """
        # Setup
        plan = TraversalPlan(entry_app="test_app", root_node=None)
        vision_service = Mock()
        action_executor = Mock()
        engine = GraphTraversalEngine(plan, vision_service, action_executor)
        context = TraversalRuntimeContext(max_depth=100)

        # Mark children as visited
        context.visited_children["test_container"] = {"child1", "child2"}

        node = TraversalNode(
            node_id="test_container",
            name="Test Container",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.STATIC,
                static_children=["child1", "child2"]
            )
        )

        # Execute
        result = engine._has_unvisited_children(node, context)

        # Verify
        assert result is False, "All children visited should return False"

    def test_has_unvisited_children_static_has_unvisited(self):
        """
        Given: A node with STATIC strategy, some children not visited
        When: _has_unvisited_children is called
        Then: Returns True
        """
        # Setup
        plan = TraversalPlan(entry_app="test_app", root_node=None)
        vision_service = Mock()
        action_executor = Mock()
        engine = GraphTraversalEngine(plan, vision_service, action_executor)
        context = TraversalRuntimeContext(max_depth=100)

        # Mark only one child as visited
        context.visited_children["test_container"] = {"child1"}

        node = TraversalNode(
            node_id="test_container",
            name="Test Container",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.STATIC,
                static_children=["child1", "child2"]
            )
        )

        # Execute
        result = engine._has_unvisited_children(node, context)

        # Verify
        assert result is True, "Has unvisited children should return True"

    def test_has_unvisited_children_static_no_visited(self):
        """
        Given: A node with STATIC strategy, no children visited yet
        When: _has_unvisited_children is called
        Then: Returns True
        """
        # Setup
        plan = TraversalPlan(entry_app="test_app", root_node=None)
        vision_service = Mock()
        action_executor = Mock()
        engine = GraphTraversalEngine(plan, vision_service, action_executor)
        context = TraversalRuntimeContext(max_depth=100)

        node = TraversalNode(
            node_id="test_container",
            name="Test Container",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.STATIC,
                static_children=["child1", "child2", "child3"]
            )
        )

        # Execute
        result = engine._has_unvisited_children(node, context)

        # Verify
        assert result is True, "No children visited yet should return True"

    # ============================================================================
    # DYNAMIC_MATCH Strategy Tests
    # ============================================================================

    def test_has_unvisited_children_dynamic_all_visited(self):
        """
        Given: A node with DYNAMIC_MATCH strategy, all children visited
        When: _has_unvisited_children is called
        Then: Returns False
        """
        # Setup
        plan = TraversalPlan(entry_app="test_app", root_node=None)
        vision_service = Mock()
        action_executor = Mock()
        engine = GraphTraversalEngine(plan, vision_service, action_executor)
        context = TraversalRuntimeContext(max_depth=100)

        # Mark all children as visited
        context.visited_children["test_container"] = {"dynamic_child1", "dynamic_child2"}

        node = TraversalNode(
            node_id="test_container",
            name="Test Container",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.DYNAMIC_MATCH,
                dynamic_rules={}
            )
        )

        # Mock DynamicChildManager.get_next_unvisited_child to return None (all visited)
        engine._child_mgr.get_next_unvisited_child = Mock(return_value=None)

        # Execute
        result = engine._has_unvisited_children(node, context)

        # Verify
        assert result is False, "All dynamic children visited should return False"
        engine._child_mgr.get_next_unvisited_child.assert_called_once_with(node, context)

    def test_has_unvisited_children_dynamic_has_unvisited(self):
        """
        Given: A node with DYNAMIC_MATCH strategy, some children not visited
        When: _has_unvisited_children is called
        Then: Returns True
        """
        # Setup
        plan = TraversalPlan(entry_app="test_app", root_node=None)
        vision_service = Mock()
        action_executor = Mock()
        engine = GraphTraversalEngine(plan, vision_service, action_executor)
        context = TraversalRuntimeContext(max_depth=100)

        # Mark only one child as visited
        context.visited_children["test_container"] = {"dynamic_child1"}

        node = TraversalNode(
            node_id="test_container",
            name="Test Container",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.DYNAMIC_MATCH,
                dynamic_rules={}
            )
        )

        # Mock DynamicChildManager.get_next_unvisited_child to return an unvisited child
        engine._child_mgr.get_next_unvisited_child = Mock(return_value="dynamic_child2")

        # Execute
        result = engine._has_unvisited_children(node, context)

        # Verify
        assert result is True, "Has unvisited dynamic children should return True"
        engine._child_mgr.get_next_unvisited_child.assert_called_once_with(node, context)

    # ============================================================================
    # Unsupported Strategy Tests
    # ============================================================================

    def test_has_unvisited_children_unsupported_strategy(self):
        """
        Given: A node with unsupported children_strategy type
        When: _has_unvisited_children is called
        Then: Raises ValueError
        """
        # Setup
        plan = TraversalPlan(entry_app="test_app", root_node=None)
        vision_service = Mock()
        action_executor = Mock()
        engine = GraphTraversalEngine(plan, vision_service, action_executor)
        context = TraversalRuntimeContext(max_depth=100)

        # Create node with invalid strategy (mocked)
        node = Mock()
        node.node_id = "test_container"
        node.children_strategy = Mock()
        node.children_strategy.type = "INVALID_TYPE"  # Not a valid ChildrenStrategyType

        # Execute & Verify
        with pytest.raises(ValueError) as exc_info:
            engine._has_unvisited_children(node, context)

        assert "Unsupported children_strategy type" in str(exc_info.value)
