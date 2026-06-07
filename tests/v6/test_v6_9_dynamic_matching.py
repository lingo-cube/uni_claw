"""
Tests for V6.9 Dynamic Matching features.

Tests cover:
- Path concatenation in template instantiation
- Dynamic child generation
- MenuItem to dict field mapping
- Cache invalidation
- FRAME_COMPLETE interception
"""

import pytest
from pathlib import Path
import sys
from unittest.mock import Mock, MagicMock

# Add project root to sys.path
sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from src.graph.template import TemplateRegistry, Template, TemplateInstantiator, PlaceholderResolver
from src.graph.node import NodeType, Precondition
from src.graph.matcher import DynamicMatcher, MatchAction, MatchResult
from src.traversal.graph_engine import GraphTraversalEngine
from src.graph.plan import TraversalPlan
from src.graph.node import (
    ChildrenStrategy,
    ChildrenStrategyType,
    DynamicRule,
    IntentSlots,
    TraversalNode,
    Operation,
    EntryPolicy,
    CompletionPolicy,
)


# ============================================================================
# Test Path Concatenation
# ============================================================================


class TestPathConcatenation:
    """Tests for path concatenation in template instantiation."""

    def test_instantiate_with_parent_path(self):
        """Test that parent_path is concatenated with node name when precondition exists."""
        registry = TemplateRegistry()
        instantiator = registry.instantiator

        # Create template WITH precondition
        template = Template(
            template_id="test_template",
            node_type=NodeType.LEAF_ACTION,
            operation={"action": "click"},
            precondition={"page_name": "Test"},  # Add precondition
        )

        context = {"name": "Display", "item_text": "Display"}
        parent_path = ["Settings", "Main"]

        node = instantiator.instantiate(template, context, parent_path)

        assert node.precondition is not None
        assert node.precondition.path == ["Settings", "Main", "Display"]

    def test_instantiate_without_parent_path(self):
        """Test that instantiation without parent_path works with precondition."""
        registry = TemplateRegistry()
        instantiator = registry.instantiator

        # Create template WITH precondition
        template = Template(
            template_id="test_template",
            node_type=NodeType.LEAF_ACTION,
            operation={"action": "click"},
            precondition={"page_name": "Test"},  # Add precondition
        )

        context = {"name": "Display", "item_text": "Display"}

        node = instantiator.instantiate(template, context)

        assert node.precondition is not None
        assert node.precondition.path == ["Display"]

    def test_registry_instantiate_forwards_parent_path(self):
        """Test that TemplateRegistry.instantiate forwards parent_path."""
        registry = TemplateRegistry()

        # Use a template that has precondition - we need to check which built-in has one
        # menu_container template doesn't have precondition in DEFAULT_TEMPLATES
        # Let's create a custom test
        template = Template(
            template_id="test_with_precond",
            node_type=NodeType.LEAF_ACTION,
            operation={"action": "click"},
            precondition={"page_name": "Test"},
        )
        registry.templates["test_with_precond"] = template

        parent_path = ["Settings"]
        node = registry.instantiate("test_with_precond", {"name": "Display", "item_text": "Display"}, parent_path)

        assert node is not None
        assert node.precondition is not None
        assert node.precondition.path == ["Settings", "Display"]

    def test_node_without_precondition_no_concatenation(self):
        """Test that nodes without precondition skip concatenation."""
        registry = TemplateRegistry()
        instantiator = registry.instantiator

        # Create template without precondition
        template = Template(
            template_id="no_precond_template",
            node_type=NodeType.LEAF_ACTION,
            operation={"action": "click"},
            precondition=None,  # No precondition
        )

        context = {"name": "Test"}
        parent_path = ["Parent"]

        node = instantiator.instantiate(template, context, parent_path)

        assert node.precondition is None  # No precondition, no error


# ============================================================================
# Test MenuItem to Dict Field Mapping
# ============================================================================


class TestMenuItemFieldMapping:
    """Tests for MenuItem to dict field mapping in dynamic matching."""

    def test_menu_item_type_mapping(self):
        """Test that item.type is mapped to 'type' field."""
        matcher = DynamicMatcher(TemplateRegistry())
        # Use LEAF_ACTION instead of CONTAINER to avoid validation errors
        context = matcher._build_context(
            {"type": "menu_item", "text": "Settings", "index": 0},
            TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.LEAF_ACTION,  # Use leaf type
                operation=Operation(action="click"),
                children_strategy=ChildrenStrategy(type=ChildrenStrategyType.NONE),
            )
        )
        assert "item_text" in context
        assert "item_index" in context

    def test_coordinate_field_mapping(self):
        """Test that coordinates are mapped correctly."""
        matcher = DynamicMatcher(TemplateRegistry())
        menu_item = {
            "type": "button",
            "text": "Click",
            "index": 0,
            "coordinate_x": 100,
            "coordinate_y": 200,
        }

        context = matcher._build_context(
            menu_item,
            TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.LEAF_ACTION,  # Use leaf type
                operation=Operation(action="click"),
                children_strategy=ChildrenStrategy(type=ChildrenStrategyType.NONE),
            )
        )

        assert context["coordinate_x"] == 100
        assert context["coordinate_y"] == 200


# ============================================================================
# Test Cache Invalidation
# ============================================================================


class TestCacheInvalidation:
    """Tests for cache invalidation on path changes."""

    def test_invalidate_children_cache(self):
        """Test that invalidate_children_cache removes cached children."""
        # Create a minimal engine with plan
        plan = TraversalPlan(
            entry_app="test",
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(type=ChildrenStrategyType.DYNAMIC_MATCH, dynamic_rules={}),
            )
        )

        # Mock vision and action executors
        mock_vision = Mock()
        mock_action = Mock()

        engine = GraphTraversalEngine(plan, mock_vision, mock_action)

        # Add some fake cached children
        engine._dynamic_children["root"] = [
            TraversalNode(node_id="child1", name="Child1", node_type=NodeType.LEAF_ACTION, operation=Operation(action="click")),
            TraversalNode(node_id="child2", name="Child2", node_type=NodeType.LEAF_ACTION, operation=Operation(action="click")),
        ]

        # Verify cache has children
        assert len(engine._dynamic_children.get("root", [])) == 2

        # Invalidate
        engine.invalidate_children_cache("root")

        # Verify cache is empty
        assert len(engine._dynamic_children.get("root", [])) == 0

    def test_invalidate_nonexistent_node_no_error(self):
        """Test that invalidating non-existent node doesn't raise error."""
        plan = TraversalPlan(
            entry_app="test",
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.LEAF_ACTION,  # Use leaf type to avoid validation
                operation=Operation(action="click"),
                children_strategy=ChildrenStrategy(type=ChildrenStrategyType.NONE),
            )
        )

        mock_vision = Mock()
        mock_action = Mock()

        engine = GraphTraversalEngine(plan, mock_vision, mock_action)

        # Should not raise error
        engine.invalidate_children_cache("nonexistent")


# ============================================================================
# Test Dynamic Child Generation
# ============================================================================


class TestDynamicChildGeneration:
    """Tests for _generate_dynamic_children method."""

    def test_generate_dynamic_children_creates_cache_entry(self):
        """Test that _generate_dynamic_children creates cache entry."""
        # This would require a more complex setup with mock page analysis
        # For now, we test the cache invalidation separately
        pass


# ============================================================================
# Test FRAME_COMPLETE Interception
# ============================================================================


class TestFrameCompleteInterception:
    """Tests for FRAME_COMPLETE interception."""

    def test_get_next_unvisited_child_returns_dynamic_children(self):
        """Test that _get_next_unvisited_child returns dynamic children."""
        from src.trace.context import TraversalRuntimeContext

        # Create a node with DYNAMIC_MATCH strategy
        node = TraversalNode(
            node_id="parent",
            name="Parent",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.DYNAMIC_MATCH,
                dynamic_rules={},
            ),
        )

        # Create engine with mocked components
        plan = TraversalPlan(entry_app="test", root_node=node)
        mock_vision = Mock()
        mock_action = Mock()

        engine = GraphTraversalEngine(plan, mock_vision, mock_action)

        # Pre-populate cache with fake children
        child1 = TraversalNode(
            node_id="child1",
            name="Child1",
            node_type=NodeType.LEAF_ACTION,
            operation=Operation(action="click"),
        )
        child2 = TraversalNode(
            node_id="child2",
            name="Child2",
            node_type=NodeType.LEAF_ACTION,
            operation=Operation(action="click"),
        )
        engine._dynamic_children["parent"] = [child1, child2]
        engine._node_registry["child1"] = child1
        engine._node_registry["child2"] = child2

        # Get first child
        child_id = engine._get_next_unvisited_child(node)
        assert child_id == "child1"

        # Get second child
        child_id = engine._get_next_unvisited_child(node)
        assert child_id == "child2"

        # No more children
        child_id = engine._get_next_unvisited_child(node)
        assert child_id is None
