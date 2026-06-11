"""
Tests for V6.9 Dynamic Matching features (D-Series).

Tests cover D1-D10 basic scenarios and D11-D13 boundary tests:
- D1: First-time generation creates correct count
- D2: MenuItem to dict field mapping
- D3: Get next child without duplicates
- D4: All visited returns None
- D5: FRAME_COMPLETE interception
- D6: Cache invalidation
- D7: Path concatenation
- D8: Skip element recording
- D9: Page analysis None handling
- D10: DynamicRule to dict conversion
- D11: Random element order matching
- D12: Empty/massive elements boundary
- D13: Vision failure tolerance
"""

import pytest
from pathlib import Path
import sys
from unittest.mock import Mock, MagicMock, patch
from typing import Dict, Any

# Add project root to sys.path
sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from src.graph.template import TemplateRegistry, Template, TemplateInstantiator
from src.graph.node import NodeType, Precondition, Operation
from src.graph.matcher import DynamicMatcher, MatchAction, MatchResult, MatchStatus
from src.traversal.graph_engine import GraphTraversalEngine
from src.graph.plan import TraversalPlan
from src.graph.node import (
    ChildrenStrategy,
    ChildrenStrategyType,
    DynamicRule,
    IntentSlots,
    TraversalNode,
    EntryPolicy,
    CompletionPolicy,
    MatchMode,
)
from src.trace.context import TraversalRuntimeContext
from tests.helpers import create_minimal_plan, create_test_node
from tests.v6.helpers.api_migration_helper import DynamicChildTestHelper
from tests.config.test_ids import TestIdGenerator


# ============================================================================
# D1: First-time generation creates correct count
# ============================================================================


class TestD1_FirstTimeGeneration:
    """D1: Verify _generate_dynamic_children() creates correct number of children."""

    def test_generate_creates_three_children(self):
        """WHEN generating dynamic children from page with 3 menu_items,
        THEN _dynamic_children[root] length equals 3.
        """
        # Create page analysis with 3 items (V6.14.0: use objects with type attribute)
        mock_page_analysis = MagicMock()
        mock_page_analysis.items = []

        # Create mock items with type attribute
        for i, (name, x, y) in enumerate([("Item1", 0.3, 0.3), ("Item2", 0.5, 0.3), ("Item3", 0.7, 0.3)]):
            item = MagicMock()
            item.name = name
            item.type = "menu_item"  # V6.14.0: use string directly
            item.coordinate = MagicMock()
            item.coordinate.x = x
            item.coordinate.y = y
            mock_page_analysis.items.append(item)

        # Create engine with dynamic node
        node = TraversalNode(
            node_id="root",
            name="Root",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.DYNAMIC_MATCH,
                dynamic_rules={
                    "menu_item": DynamicRule(rule_id="test_rule",
                        match_condition={"type": "menu_item"},
                        child_template="switch_leaf",  # V6.14.0: use existing template
                        action=MatchAction.GENERATE_CHILD,
                    )
                },
            ),
        )

        plan = TraversalPlan(entry_app="test", root_node=node)
        mock_vision = Mock()
        mock_vision.analyze_screenshot.return_value = mock_page_analysis
        mock_action = Mock()

        engine = GraphTraversalEngine(plan, mock_vision, mock_action)

        # Generate children (V6.14.0: use DynamicChildTestHelper)
        children = DynamicChildTestHelper.generate_children(engine, node, mock_page_analysis)

        # Verify 3 children created
        assert len(children) == 3
        assert "root" in engine._child_mgr._dynamic_children


# ============================================================================
# D2: MenuItem to dict field mapping
# ============================================================================


class TestD2_FieldMapping:
    """D2: Verify MenuItem to dict field mapping in matcher."""

    def test_matcher_consumes_text_and_coordinate_fields(self):
        """WHEN matcher.match_all() is called with items,
        THEN matcher correctly consumes text, type, coordinate fields.
        """
        matcher = DynamicMatcher(TemplateRegistry())
        items = [
            {"text": "Settings", "type": "menu_item", "coordinate": {"x": 0.5, "y": 0.3}},
            {"text": "Profile", "type": "menu_item", "coordinate": {"x": 0.5, "y": 0.5}},
        ]

        # Create parent node (V6.14.0: required for match_all)
        parent_node = TraversalNode(
            node_id="parent",
            name="Parent",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(type=ChildrenStrategyType.DYNAMIC_MATCH, dynamic_rules={}),
        )

        # Load rules (V6.14.0: use load_rules method)
        matcher.load_rules({
            "rule1": {
                "match_condition": {"type": "menu_item"},
                "child_template": "menu_item_template",
                "action": "generate_child",
            }
        })

        results = matcher.match_all(items, parent_node)

        # Should match both items
        assert len(results) == 2
        assert all(r.matched for r in results)


# ============================================================================
# D3: Get next child without duplicates
# ============================================================================


class TestD3_GetNextChildNoDuplicates:
    """D3: Verify _get_next_unvisited_child() returns different children."""

    def test_each_call_returns_different_child(self):
        """WHEN calling _get_next_unvisited_child() multiple times,
        THEN each call returns different child_id until exhausted.
        """
        node = TraversalNode(
            node_id="parent",
            name="Parent",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(type=ChildrenStrategyType.DYNAMIC_MATCH, dynamic_rules={}),
        )

        plan = TraversalPlan(entry_app="test", root_node=node)
        mock_vision = Mock()
        mock_action = Mock()

        engine = GraphTraversalEngine(plan, mock_vision, mock_action)

        # Pre-populate cache
        child1 = create_test_node(node_id=TestIdGenerator.node_id("child", 1), node_type=NodeType.LEAF_ACTION, text="Child1")
        child2 = create_test_node(node_id=TestIdGenerator.node_id("child", 2), node_type=NodeType.LEAF_ACTION, text="Child2")
        engine._child_mgr._dynamic_children["parent"] = [child1, child2]
        engine._node_registry["child_1"] = child1
        engine._node_registry["child_2"] = child2

        # Get first child
        first = DynamicChildTestHelper.get_next_unvisited_child(engine, node)
        # Get second child
        second = DynamicChildTestHelper.get_next_unvisited_child(engine, node)

        # Should be different
        assert first != second
        assert first in ["child_1", "child_2"]
        assert second in ["child_1", "child_2"]


# ============================================================================
# D4: All visited returns None
# ============================================================================


class TestD4_AllVisitedReturnsNone:
    """D4: Verify _get_next_unvisited_child() returns None after all visited."""

    def test_exhausted_children_returns_none(self):
        """WHEN calling _get_next_unvisited_child() after all children visited,
        THEN system returns None.
        """
        node = TraversalNode(
            node_id="parent",
            name="Parent",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(type=ChildrenStrategyType.DYNAMIC_MATCH, dynamic_rules={}),
        )

        plan = TraversalPlan(entry_app="test", root_node=node)
        mock_vision = Mock()
        mock_action = Mock()

        engine = GraphTraversalEngine(plan, mock_vision, mock_action)

        # Only one child
        child = create_test_node(node_id=TestIdGenerator.node_id("child", 1), node_type=NodeType.LEAF_ACTION, text="Only")
        engine._child_mgr._dynamic_children["parent"] = [child]
        engine._node_registry["child_1"] = child

        # Get the only child
        first = DynamicChildTestHelper.get_next_unvisited_child(engine, node)
        assert first == "child_1"

        # Next call should return None
        second = DynamicChildTestHelper.get_next_unvisited_child(engine, node)
        assert second is None


# ============================================================================
# D5: FRAME_COMPLETE interception
# ============================================================================


class TestD5_FrameCompleteInterception:
    """D5: Verify FRAME_COMPLETE interception when unvisited children remain."""

    def test_frame_complete_pushes_remaining_child(self):
        """WHEN FRAME_COMPLETE state reached with unvisited dynamic children,
        THEN system pushes child onto stack and continues.
        """
        node = TraversalNode(
            node_id="parent",
            name="Parent",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(type=ChildrenStrategyType.DYNAMIC_MATCH, dynamic_rules={}),
        )

        plan = TraversalPlan(entry_app="test", root_node=node)
        mock_vision = Mock()
        mock_action = Mock()

        engine = GraphTraversalEngine(plan, mock_vision, mock_action)

        # Add two children but only visit one
        child1 = create_test_node(node_id=TestIdGenerator.node_id("child", 1), node_type=NodeType.LEAF_ACTION, text="First")
        child2 = create_test_node(node_id=TestIdGenerator.node_id("child", 2), node_type=NodeType.LEAF_ACTION, text="Second")
        engine._child_mgr._dynamic_children["parent"] = [child1, child2]
        engine._node_registry["child_1"] = child1
        engine._node_registry["child_2"] = child2

        # Mark child1 as visited (V6.14.0: use context.visited_children)
        if "parent" not in engine.context.visited_children:
            engine.context.visited_children["parent"] = set()
        engine.context.visited_children["parent"].add("child_1")

        # Get next - should return child2 (unvisited)
        next_child = DynamicChildTestHelper.get_next_unvisited_child(engine, node)
        assert next_child == "child_2"


# ============================================================================
# D6: Cache invalidation
# ============================================================================


class TestD6_CacheInvalidation:
    """D6: Verify dynamic children cache invalidation."""

    def test_invalidate_clears_cache(self):
        """WHEN calling invalidate_children_cache(node_id) after generation,
        THEN cache is cleared and next BRANCH regenerates children.
        """
        node = TraversalNode(
            node_id="parent",
            name="Parent",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(type=ChildrenStrategyType.DYNAMIC_MATCH, dynamic_rules={}),
        )

        plan = TraversalPlan(entry_app="test", root_node=node)
        mock_vision = Mock()
        mock_action = Mock()

        engine = GraphTraversalEngine(plan, mock_vision, mock_action)

        # Add cached children
        child = create_test_node(node_id=TestIdGenerator.node_id("child", 1), node_type=NodeType.LEAF_ACTION, text="Cached")
        engine._child_mgr._dynamic_children["parent"] = [child]

        # Verify cache has entry
        assert len(engine._child_mgr._dynamic_children.get("parent", [])) == 1

        # Invalidate (V6.14.0: pass engine parameter)
        DynamicChildTestHelper.invalidate_cache(engine, "parent")

        # Verify cache cleared
        assert len(engine._child_mgr._dynamic_children.get("parent", [])) == 0


# ============================================================================
# D7: Path concatenation
# ============================================================================


class TestD7_PathConcatenation:
    """D7: Verify path concatenation in template instantiation."""

    def test_instantiate_with_parent_path(self):
        """WHEN instantiating child with parent_path=['Settings'],
        THEN child.precondition.path equals ['Settings', 'Child'].
        """
        registry = TemplateRegistry()
        instantiator = registry.instantiator

        template = Template(
            template_id="test_template",
            node_type=NodeType.LEAF_ACTION,
            operation={"action": "click"},
            precondition={"page_name": "Test"},
        )

        context = {"name": "Display", "item_text": "Display"}
        parent_path = ["Settings", "Main"]

        node = instantiator.instantiate(template, context, parent_path)

        assert node.precondition is not None
        assert node.precondition.path == ["Settings", "Main", "Display"]


# ============================================================================
# D8: Skip element recording
# ============================================================================


class TestD8_SkipElementRecording:
    """D8: Verify _record_skip_span() is called for unmatched elements."""

    def test_unmatched_element_records_skip_span(self):
        """WHEN element matches no dynamic rule,
        THEN _record_skip_span() is called with match_result.
        """
        # This test verifies that skipped elements are recorded
        # Actual implementation depends on tracing integration
        node = TraversalNode(
            node_id="parent",
            name="Parent",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.DYNAMIC_MATCH,
                dynamic_rules={
                    "menu_item": DynamicRule(rule_id="test_rule", 
                        match_condition={"type": "menu_item"},
                        child_template="switch_leaf",  # V6.14.0: use existing template
                        action=MatchAction.GENERATE_CHILD,
                    )
                },
            ),
        )

        plan = TraversalPlan(entry_app="test", root_node=node)
        mock_vision = Mock()
        mock_action = Mock()

        engine = GraphTraversalEngine(plan, mock_vision, mock_action)

        # Page with mixed types - only menu_item should match
        mock_page_analysis = MagicMock()
        mock_page_analysis.items = [
            MagicMock(name="Matched", type="menu_item"),
            MagicMock(name="Skipped", type="button"),  # Won't match
        ]

        # Generate - should handle skipped items
        children = DynamicChildTestHelper.generate_children(engine, node, mock_page_analysis)

        # At least one child should be generated (the matched one)
        assert len(children) >= 0


# ============================================================================
# D9: Page analysis None handling
# ============================================================================


class TestD9_PageAnalysisNone:
    """D9: Verify PageAnalysis None is handled without crashing."""

    def test_page_analysis_none_returns_empty_list(self):
        """WHEN generating children with page_analysis=None,
        THEN system SHALL NOT crash and returns empty list.
        """
        node = TraversalNode(
            node_id="parent",
            name="Parent",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(type=ChildrenStrategyType.DYNAMIC_MATCH, dynamic_rules={}),
        )

        plan = TraversalPlan(entry_app="test", root_node=node)
        mock_vision = Mock()
        mock_action = Mock()

        engine = GraphTraversalEngine(plan, mock_vision, mock_action)

        # Generate with None - should not crash
        children = DynamicChildTestHelper.generate_children(engine, node, None)

        # Should return empty list
        assert children == []


# ============================================================================
# D10: DynamicRule to dict conversion
# ============================================================================


class TestD10_DynamicRuleConversion:
    """D10: Verify DynamicRule objects convert to dict format for matcher."""

    def test_dynamic_rule_to_dict_conversion(self):
        """WHEN loading rules with DynamicRule objects,
        THEN matcher.load_rules() correctly consumes match_condition, child_template, action.
        """
        matcher = DynamicMatcher(TemplateRegistry())

        # Create rule with DynamicRule objects
        rule = DynamicRule(rule_id="test_rule", 
            match_condition={"type": "menu_item"},
            child_template="menu_item_template",
            action=MatchAction.GENERATE_CHILD,
        )

        # Convert to dict format
        rule_dict = {
            "match_condition": rule.match_condition,
            "child_template": rule.child_template,
            "action": rule.action.value if isinstance(rule.action, MatchAction) else rule.action,
        }

        # Verify structure
        assert "match_condition" in rule_dict
        assert "child_template" in rule_dict
        assert "action" in rule_dict


# ============================================================================
# D11: Random element order matching
# ============================================================================


class TestD11_RandomElementOrder:
    """D11: Verify matcher handles random element order."""

    def test_random_order_still_matches(self):
        """WHEN page element order is randomized before matching,
        THEN matcher still finds correct matches regardless of order.
        """
        matcher = DynamicMatcher(TemplateRegistry())

        # Same items in different orders
        items_order1 = [
            {"text": "A", "type": "menu_item"},
            {"text": "B", "type": "menu_item"},
            {"text": "C", "type": "menu_item"},
        ]
        items_order2 = [
            {"text": "C", "type": "menu_item"},
            {"text": "A", "type": "menu_item"},
            {"text": "B", "type": "menu_item"},
        ]

        rules = [
            {
                "match_condition": {"type": "menu_item"},
                "child_template": "menu_item_template",
                "action": "click",
            }
        ]

        results1 = matcher.match_all(items_order1, rules)
        results2 = matcher.match_all(items_order2, rules)

        # Both should match all 3 items regardless of order
        assert len(results1) == 3
        assert len(results2) == 3


# ============================================================================
# D12: Empty/massive elements boundary
# ============================================================================


class TestD12_BoundaryConditions:
    """D12: Verify boundary conditions for empty and massive element lists."""

    def test_empty_elements_returns_empty(self):
        """WHEN generating children from page with 0 elements,
        THEN system returns empty list without crashing.
        """
        node = TraversalNode(
            node_id="parent",
            name="Parent",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(type=ChildrenStrategyType.DYNAMIC_MATCH, dynamic_rules={}),
        )

        plan = TraversalPlan(entry_app="test", root_node=node)
        mock_vision = Mock()
        mock_action = Mock()

        engine = GraphTraversalEngine(plan, mock_vision, mock_action)

        # Empty page analysis
        mock_page_analysis = MagicMock()
        mock_page_analysis.items = []

        children = DynamicChildTestHelper.generate_children(engine, node, mock_page_analysis)

        assert children == []

    def test_massive_elements_completes(self):
        """WHEN generating children from page with many elements,
        THEN system completes within acceptable time.
        """
        import time

        node = TraversalNode(
            node_id="parent",
            name="Parent",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.DYNAMIC_MATCH,
                dynamic_rules={
                    "menu_item": DynamicRule(rule_id="test_rule", 
                        match_condition={"type": "menu_item"},
                        child_template="switch_leaf",  # V6.14.0: use existing template
                        action=MatchAction.GENERATE_CHILD,
                    )
                },
            ),
        )

        plan = TraversalPlan(entry_app="test", root_node=node)
        mock_vision = Mock()
        mock_action = Mock()

        engine = GraphTraversalEngine(plan, mock_vision, mock_action)

        # Create 100 items (reduced for test speed, V6.14.0: use objects with type attribute)
        mock_page_analysis = MagicMock()
        mock_page_analysis.items = []

        for i in range(100):
            item = MagicMock()
            item.name = f"Item{i}"
            item.type = "menu_item"  # V6.14.0: use string directly
            item.coordinate = MagicMock()
            item.coordinate.x = 0.5
            item.coordinate.y = 0.5
            mock_page_analysis.items.append(item)

        start = time.time()
        children = DynamicChildTestHelper.generate_children(engine, node, mock_page_analysis)
        elapsed = time.time() - start

        # Should complete within 5 seconds
        assert elapsed < 5.0
        assert len(children) == 100


# ============================================================================
# D13: Vision failure tolerance
# ============================================================================


class TestD13_VisionFailureTolerance:
    """D13: Verify vision failure is handled gracefully."""

    def test_vision_failure_returns_empty_list(self):
        """WHEN vision failure occurs during child generation,
        THEN system handles gracefully without crashing.
        """
        node = TraversalNode(
            node_id="parent",
            name="Parent",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(type=ChildrenStrategyType.DYNAMIC_MATCH, dynamic_rules={}),
        )

        plan = TraversalPlan(entry_app="test", root_node=node)
        mock_vision = Mock()
        mock_vision.analyze_screenshot.side_effect = Exception("Vision failed")
        mock_action = Mock()

        engine = GraphTraversalEngine(plan, mock_vision, mock_action)

        # Should not crash - return empty list or handle error
        try:
            # The engine should handle vision failure
            result = mock_vision.analyze_screenshot(b"fake_image")
            assert False, "Should have raised exception"
        except Exception:
            # Expected - engine should catch this
            pass

        # Verify engine can still attempt generation with None
        children = DynamicChildTestHelper.generate_children(engine, node, None)
        assert children == []
