"""
Tests for V6 graph model extensions.

Tests all new enums, data classes, and TraversalPlan functionality.
"""

import json
import sys
from pathlib import Path

import pytest

# 添加项目根目录到 sys.path
sys.path.insert(0, str(Path(__file__).parent.parent.parent.parent))

from src.graph.node import (
    ChildrenStrategy,
    ChildrenStrategyType,
    CompletionPolicy,
    CompletionPolicyType,
    DynamicRule,
    EntryPolicy,
    EntryStrategy,
    ErrorPolicy,
    ExitCondition,
    ExitConditionType,
    FallbackAction,
    IntentSlots,
    MatchMode,
    NodeType,
    Operation,
    Precondition,
    Target,
    TargetFoundAction,
    TraversalMode,
    TraversalNode,
)
from src.graph.plan import TraversalPlan


# ============================================================================
# Test New Enum Types (Tasks 1.1.1 - 1.1.7)
# ============================================================================


class TestExitConditionType:
    """Tests for ExitConditionType enum."""

    def test_enum_values(self):
        """Test that all required values exist."""
        assert ExitConditionType.ALL_CHILDREN_VISITED.value == "all_children_visited"
        assert ExitConditionType.DEPTH_LIMITED.value == "depth_limited"
        assert ExitConditionType.SINGLE_LEVEL.value == "single_level"

    def test_values_method(self):
        """Test values() class method."""
        values = ExitConditionType.values()
        assert "all_children_visited" in values
        assert "depth_limited" in values
        assert "single_level" in values

    def test_from_value(self):
        """Test from_value() class method."""
        assert ExitConditionType.from_value("all_children_visited") == ExitConditionType.ALL_CHILDREN_VISITED
        assert ExitConditionType.from_value("depth_limited") == ExitConditionType.DEPTH_LIMITED
        assert ExitConditionType.from_value("single_level") == ExitConditionType.SINGLE_LEVEL

    def test_from_value_invalid(self):
        """Test from_value() with invalid value."""
        with pytest.raises(ValueError, match="Invalid ExitConditionType value"):
            ExitConditionType.from_value("invalid")

    def test_is_valid(self):
        """Test is_valid() class method."""
        assert ExitConditionType.is_valid("all_children_visited") is True
        assert ExitConditionType.is_valid("invalid") is False


class TestFallbackAction:
    """Tests for FallbackAction enum."""

    def test_enum_values(self):
        """Test that all required values exist."""
        assert FallbackAction.BACK.value == "back"
        assert FallbackAction.AUTO_ESCAPE.value == "auto_escape"
        assert FallbackAction.SKIP.value == "skip"
        assert FallbackAction.ABORT.value == "abort"

    def test_values_method(self):
        """Test values() class method."""
        values = FallbackAction.values()
        assert "back" in values
        assert "auto_escape" in values
        assert "skip" in values
        assert "abort" in values

    def test_from_value(self):
        """Test from_value() class method."""
        assert FallbackAction.from_value("back") == FallbackAction.BACK
        assert FallbackAction.from_value("auto_escape") == FallbackAction.AUTO_ESCAPE
        assert FallbackAction.from_value("skip") == FallbackAction.SKIP
        assert FallbackAction.from_value("abort") == FallbackAction.ABORT


class TestCompletionPolicyType:
    """Tests for CompletionPolicyType enum."""

    def test_enum_values(self):
        """Test that all required values exist."""
        assert CompletionPolicyType.NONE.value == "none"
        assert CompletionPolicyType.TARGET_FOUND.value == "target_found"
        assert CompletionPolicyType.TIMEOUT.value == "timeout"
        assert CompletionPolicyType.MAX_STEPS.value == "max_steps"

    def test_from_value(self):
        """Test from_value() class method."""
        assert CompletionPolicyType.from_value("none") == CompletionPolicyType.NONE
        assert CompletionPolicyType.from_value("target_found") == CompletionPolicyType.TARGET_FOUND
        assert CompletionPolicyType.from_value("timeout") == CompletionPolicyType.TIMEOUT
        assert CompletionPolicyType.from_value("max_steps") == CompletionPolicyType.MAX_STEPS


class TestTargetFoundAction:
    """Tests for TargetFoundAction enum."""

    def test_enum_values(self):
        """Test that all required values exist."""
        assert TargetFoundAction.MARK_AND_STOP.value == "mark_and_stop"
        assert TargetFoundAction.EXECUTE_THEN_STOP.value == "execute_then_stop"

    def test_from_value(self):
        """Test from_value() class method."""
        assert TargetFoundAction.from_value("mark_and_stop") == TargetFoundAction.MARK_AND_STOP
        assert TargetFoundAction.from_value("execute_then_stop") == TargetFoundAction.EXECUTE_THEN_STOP


class TestMatchMode:
    """Tests for MatchMode enum."""

    def test_enum_values(self):
        """Test that all required values exist."""
        assert MatchMode.EXACT.value == "exact"
        assert MatchMode.CONTAINS.value == "contains"

    def test_from_value(self):
        """Test from_value() class method."""
        assert MatchMode.from_value("exact") == MatchMode.EXACT
        assert MatchMode.from_value("contains") == MatchMode.CONTAINS


class TestEntryStrategy:
    """Tests for EntryStrategy enum."""

    def test_enum_values(self):
        """Test that all required values exist."""
        assert EntryStrategy.COLD_LAUNCH.value == "cold_launch"
        assert EntryStrategy.DIRECT_DEEPLINK.value == "direct_deeplink"
        assert EntryStrategy.BIND_CURRENT_SCREEN.value == "bind_current_screen"

    def test_from_value(self):
        """Test from_value() class method."""
        assert EntryStrategy.from_value("cold_launch") == EntryStrategy.COLD_LAUNCH
        assert EntryStrategy.from_value("direct_deeplink") == EntryStrategy.DIRECT_DEEPLINK
        assert EntryStrategy.from_value("bind_current_screen") == EntryStrategy.BIND_CURRENT_SCREEN


class TestTraversalMode:
    """Tests for TraversalMode enum."""

    def test_enum_values(self):
        """Test that all required values exist."""
        assert TraversalMode.HYBRID.value == "hybrid"
        assert TraversalMode.CONCRETE.value == "concrete"
        assert TraversalMode.ABSTRACT.value == "abstract"

    def test_from_value(self):
        """Test from_value() class method."""
        assert TraversalMode.from_value("hybrid") == TraversalMode.HYBRID
        assert TraversalMode.from_value("concrete") == TraversalMode.CONCRETE
        assert TraversalMode.from_value("abstract") == TraversalMode.ABSTRACT


# ============================================================================
# Test New Data Classes (Tasks 1.2.1 - 1.2.4)
# ============================================================================


class TestExitCondition:
    """Tests for ExitCondition data class."""

    def test_create_basic(self):
        """Test creating basic exit condition."""
        ec = ExitCondition(type=ExitConditionType.ALL_CHILDREN_VISITED)
        assert ec.type == ExitConditionType.ALL_CHILDREN_VISITED
        assert ec.fallback == FallbackAction.BACK
        assert ec.max_depth is None

    def test_with_custom_fallback(self):
        """Test exit condition with custom fallback."""
        ec = ExitCondition(
            type=ExitConditionType.ALL_CHILDREN_VISITED,
            fallback=FallbackAction.SKIP,
        )
        assert ec.fallback == FallbackAction.SKIP

    def test_depth_limited_requires_max_depth(self):
        """Test that DEPTH_LIMITED requires max_depth."""
        with pytest.raises(ValueError, match="max_depth must be specified"):
            ExitCondition(type=ExitConditionType.DEPTH_LIMITED)

    def test_depth_limited_with_max_depth(self):
        """Test DEPTH_LIMITED with max_depth."""
        ec = ExitCondition(
            type=ExitConditionType.DEPTH_LIMITED,
            max_depth=5,
        )
        assert ec.max_depth == 5

    def test_invalid_max_depth_negative(self):
        """Test that negative max_depth is rejected."""
        with pytest.raises(ValueError, match="max_depth must be positive"):
            ExitCondition(
                type=ExitConditionType.DEPTH_LIMITED,
                max_depth=-1,
            )

    def test_invalid_max_depth_too_large(self):
        """Test that max_depth > 1000 is rejected."""
        with pytest.raises(ValueError, match="max_depth cannot exceed 1000"):
            ExitCondition(
                type=ExitConditionType.DEPTH_LIMITED,
                max_depth=1001,
            )


class TestCompletionPolicy:
    """Tests for CompletionPolicy data class."""

    def test_default_none(self):
        """Test default NONE policy."""
        cp = CompletionPolicy()
        assert cp.type == CompletionPolicyType.NONE
        assert cp.target_name is None
        assert cp.match_mode == MatchMode.CONTAINS
        assert cp.action_on_found == TargetFoundAction.MARK_AND_STOP

    def test_target_found_requires_target_name(self):
        """Test that TARGET_FOUND requires target_name."""
        with pytest.raises(ValueError, match="target_name must be specified"):
            CompletionPolicy(type=CompletionPolicyType.TARGET_FOUND)

    def test_target_found_with_name(self):
        """Test TARGET_FOUND with target_name."""
        cp = CompletionPolicy(
            type=CompletionPolicyType.TARGET_FOUND,
            target_name="Version",
        )
        assert cp.type == CompletionPolicyType.TARGET_FOUND
        assert cp.target_name == "Version"

    def test_timeout_requires_timeout_seconds(self):
        """Test that TIMEOUT requires timeout_seconds."""
        with pytest.raises(ValueError, match="timeout_seconds must be specified"):
            CompletionPolicy(type=CompletionPolicyType.TIMEOUT)

    def test_timeout_with_seconds(self):
        """Test TIMEOUT with timeout_seconds."""
        cp = CompletionPolicy(
            type=CompletionPolicyType.TIMEOUT,
            timeout_seconds=60.0,
        )
        assert cp.type == CompletionPolicyType.TIMEOUT
        assert cp.timeout_seconds == 60.0

    def test_invalid_timeout_negative(self):
        """Test that negative timeout_seconds is rejected."""
        with pytest.raises(ValueError, match="timeout_seconds must be positive"):
            CompletionPolicy(
                type=CompletionPolicyType.TIMEOUT,
                timeout_seconds=-1.0,
            )

    def test_max_steps_requires_max_steps(self):
        """Test that MAX_STEPS requires max_steps."""
        with pytest.raises(ValueError, match="max_steps must be specified"):
            CompletionPolicy(type=CompletionPolicyType.MAX_STEPS)

    def test_max_steps_with_value(self):
        """Test MAX_STEPS with max_steps."""
        cp = CompletionPolicy(
            type=CompletionPolicyType.MAX_STEPS,
            max_steps=1000,
        )
        assert cp.type == CompletionPolicyType.MAX_STEPS
        assert cp.max_steps == 1000


class TestEntryPolicy:
    """Tests for EntryPolicy data class."""

    def test_default_cold_launch(self):
        """Test default COLD_LAUNCH strategy."""
        ep = EntryPolicy()
        assert ep.strategy == EntryStrategy.COLD_LAUNCH
        assert ep.timeout_seconds == 10.0
        assert ep.fallback is None
        assert ep.wait_condition is None

    def test_custom_strategy(self):
        """Test custom entry strategy."""
        ep = EntryPolicy(strategy=EntryStrategy.DIRECT_DEEPLINK)
        assert ep.strategy == EntryStrategy.DIRECT_DEEPLINK

    def test_with_wait_condition(self):
        """Test with wait condition."""
        wait_condition = {"page_name": "MainScreen"}
        ep = EntryPolicy(
            strategy=EntryStrategy.COLD_LAUNCH,
            wait_condition=wait_condition,
        )
        assert ep.wait_condition == wait_condition

    def test_invalid_timeout_negative(self):
        """Test that negative timeout_seconds is rejected."""
        with pytest.raises(ValueError, match="timeout_seconds must be positive"):
            EntryPolicy(timeout_seconds=-1.0)

    def test_invalid_timeout_too_large(self):
        """Test that timeout_seconds > 300 is rejected."""
        with pytest.raises(ValueError, match="timeout_seconds cannot exceed 300"):
            EntryPolicy(timeout_seconds=301.0)


class TestIntentSlots:
    """Tests for IntentSlots data class."""

    def test_empty_slots(self):
        """Test empty intent slots."""
        slots = IntentSlots()
        assert slots.target_app is None
        assert slots.scope is None
        assert slots.target is None

    def test_all_slots(self):
        """Test with all slots filled."""
        slots = IntentSlots(
            target_app="Settings",
            scope="full",
            target="Version",
            depth=10,
            element_handling="click_all",
            navigation="dfs",
            restore=True,
            completion="all_visited",
        )
        assert slots.target_app == "Settings"
        assert slots.scope == "full"
        assert slots.target == "Version"
        assert slots.depth == 10
        assert slots.element_handling == "click_all"
        assert slots.navigation == "dfs"
        assert slots.restore is True
        assert slots.completion == "all_visited"

    def test_valid_depth_accepted(self):
        """Test that valid depth values are accepted by IntentSlots."""
        # V6.9: IntentSlots no longer validates depth - PlanCompiler does
        slots = IntentSlots(depth=10)
        assert slots.depth == 10

        slots = IntentSlots(depth=100)
        assert slots.depth == 100

    def test_invalid_scope(self):
        """Test that invalid scope is rejected."""
        with pytest.raises(ValueError, match="Invalid scope"):
            IntentSlots(scope="invalid")


# ============================================================================
# Test TraversalNode Extensions (Tasks 1.4.1 - 1.4.2)
# ============================================================================


class TestTraversalNodeExtensions:
    """Tests for TraversalNode V6 extensions."""

    def test_node_with_exit_condition(self):
        """Test node with exit_condition field."""
        node = TraversalNode(
            node_id="settings_menu",
            name="Settings",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(type=ChildrenStrategyType.DYNAMIC_MATCH),
            exit_condition=ExitCondition(
                type=ExitConditionType.ALL_CHILDREN_VISITED,
                fallback=FallbackAction.AUTO_ESCAPE,
            ),
        )
        assert node.exit_condition is not None
        assert node.exit_condition.type == ExitConditionType.ALL_CHILDREN_VISITED
        assert node.exit_condition.fallback == FallbackAction.AUTO_ESCAPE

    def test_node_without_exit_condition(self):
        """Test node without exit_condition (backward compatibility)."""
        node = TraversalNode(
            node_id="switch",
            name="Toggle",
            node_type=NodeType.LEAF_SWITCH,
            operation=Operation(action="click"),
        )
        assert node.exit_condition is None


class TestErrorPolicyBacktrack:
    """Tests for ErrorPolicy backtrack extension."""

    def test_backtrack_action(self):
        """Test that backtrack is a valid error action."""
        ep = ErrorPolicy(on_error="backtrack")
        assert ep.on_error == "backtrack"

    def test_all_valid_actions(self):
        """Test all valid error actions including backtrack."""
        valid_actions = ["retry", "skip", "abort", "fallback", "backtrack"]
        for action in valid_actions:
            ep = ErrorPolicy(on_error=action)
            assert ep.on_error == action


# ============================================================================
# Test TraversalPlan (Tasks 1.3.1 - 1.3.4)
# ============================================================================


class TestTraversalPlan:
    """Tests for TraversalPlan class."""

    def test_create_minimal_plan(self):
        """Test creating a minimal traversal plan."""
        plan = TraversalPlan(entry_app="Settings")
        assert plan.entry_app == "Settings"
        assert plan.mode == TraversalMode.HYBRID
        assert plan.completion_policy.type == CompletionPolicyType.NONE
        assert plan.root_node is None
        assert plan.static_nodes == {}

    def test_plan_with_root_node(self):
        """Test plan with root node."""
        root = TraversalNode(
            node_id="root",
            name="Root",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(type=ChildrenStrategyType.DYNAMIC_MATCH),
        )
        plan = TraversalPlan(entry_app="Settings", root_node=root)
        assert plan.root_node is not None
        assert plan.root_node.node_id == "root"

    def test_plan_with_static_nodes(self):
        """Test plan with static nodes registry."""
        node1 = TraversalNode(
            node_id="node1",
            name="Node 1",
            node_type=NodeType.LEAF_ACTION,
            operation=Operation(action="click"),
        )
        plan = TraversalPlan(entry_app="Settings", static_nodes={"node1": node1})
        assert "node1" in plan.static_nodes
        assert plan.static_nodes["node1"].node_id == "node1"

    def test_add_static_node(self):
        """Test adding a static node."""
        plan = TraversalPlan(entry_app="Settings")
        node = TraversalNode(
            node_id="node1",
            name="Node 1",
            node_type=NodeType.LEAF_ACTION,
            operation=Operation(action="click"),
        )
        plan.add_static_node(node)
        assert "node1" in plan.static_nodes

    def test_get_node_by_id(self):
        """Test getting node by ID."""
        node = TraversalNode(
            node_id="node1",
            name="Node 1",
            node_type=NodeType.LEAF_ACTION,
            operation=Operation(action="click"),
        )
        plan = TraversalPlan(entry_app="Settings", static_nodes={"node1": node})
        retrieved = plan.get_node_by_id("node1")
        assert retrieved is not None
        assert retrieved.node_id == "node1"

    def test_get_node_by_id_not_found(self):
        """Test getting non-existent node."""
        plan = TraversalPlan(entry_app="Settings")
        assert plan.get_node_by_id("nonexistent") is None

    def test_invalid_empty_entry_app(self):
        """Test that empty entry_app is rejected."""
        with pytest.raises(ValueError, match="entry_app cannot be empty"):
            TraversalPlan(entry_app="")

    def test_to_json_minimal(self):
        """Test serializing minimal plan to JSON."""
        plan = TraversalPlan(entry_app="Settings")
        json_str = plan.to_json()
        data = json.loads(json_str)
        assert data["entry_app"] == "Settings"
        assert data["mode"] == "hybrid"

    def test_to_json_with_completion_policy(self):
        """Test serializing plan with completion policy."""
        plan = TraversalPlan(
            entry_app="Settings",
            completion_policy=CompletionPolicy(
                type=CompletionPolicyType.TARGET_FOUND,
                target_name="Version",
            ),
        )
        json_str = plan.to_json()
        data = json.loads(json_str)
        assert data["completion_policy"]["type"] == "target_found"
        assert data["completion_policy"]["target_name"] == "Version"

    def test_from_json_minimal(self):
        """Test deserializing minimal plan from JSON."""
        json_str = '{"entry_app": "Settings"}'
        plan = TraversalPlan.from_json(json_str)
        assert plan.entry_app == "Settings"
        assert plan.mode == TraversalMode.HYBRID

    def test_from_json_with_completion_policy(self):
        """Test deserializing plan with completion policy."""
        json_str = """
        {
            "entry_app": "Settings",
            "completion_policy": {
                "type": "target_found",
                "target_name": "Version"
            }
        }
        """
        plan = TraversalPlan.from_json(json_str)
        assert plan.completion_policy.type == CompletionPolicyType.TARGET_FOUND
        assert plan.completion_policy.target_name == "Version"

    def test_from_json_invalid_json(self):
        """Test deserializing invalid JSON."""
        with pytest.raises(ValueError, match="Invalid JSON"):
            TraversalPlan.from_json("not valid json")

    def test_from_json_missing_entry_app(self):
        """Test deserializing JSON without entry_app."""
        with pytest.raises(ValueError, match="entry_app is required"):
            TraversalPlan.from_json("{}")

    def test_serialize_deserialize_roundtrip(self):
        """Test full serialize/deserialize roundtrip."""
        original = TraversalPlan(
            entry_app="Settings",
            mode=TraversalMode.CONCRETE,
            completion_policy=CompletionPolicy(
                type=CompletionPolicyType.MAX_STEPS,
                max_steps=500,
            ),
            intent_slots=IntentSlots(
                target_app="Settings",
                scope="partial",
                target="Version",
            ),
            meta={"version": "1.0"},
        )

        json_str = original.to_json()
        restored = TraversalPlan.from_json(json_str)

        assert restored.entry_app == original.entry_app
        assert restored.mode == original.mode
        assert restored.completion_policy.type == original.completion_policy.type
        assert restored.completion_policy.max_steps == original.completion_policy.max_steps
        assert restored.intent_slots.target == original.intent_slots.target
        assert restored.meta == original.meta

    def test_has_completion_policy(self):
        """Test has_completion_policy method."""
        plan_none = TraversalPlan(entry_app="Settings")
        assert plan_none.has_completion_policy() is False

        plan_with = TraversalPlan(
            entry_app="Settings",
            completion_policy=CompletionPolicy(
                type=CompletionPolicyType.TARGET_FOUND,
                target_name="Version",
            ),
        )
        assert plan_with.has_completion_policy() is True

    def test_serialize_with_node(self):
        """Test serializing plan with a node."""
        root = TraversalNode(
            node_id="root",
            name="Root",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(type=ChildrenStrategyType.DYNAMIC_MATCH),
            exit_condition=ExitCondition(
                type=ExitConditionType.ALL_CHILDREN_VISITED,
            ),
        )
        plan = TraversalPlan(entry_app="Settings", root_node=root)
        json_str = plan.to_json()
        data = json.loads(json_str)

        assert "root_node" in data
        assert data["root_node"]["node_id"] == "root"
        assert data["root_node"]["exit_condition"]["type"] == "all_children_visited"

    def test_deserialize_with_node(self):
        """Test deserializing plan with a node."""
        json_str = """
        {
            "entry_app": "Settings",
            "root_node": {
                "node_id": "root",
                "name": "Root",
                "node_type": "container",
                "operation": {"action": "no_action"},
                "children_strategy": {"type": "dynamic_match"},
                "exit_condition": {
                    "type": "all_children_visited",
                    "fallback": "back"
                }
            }
        }
        """
        plan = TraversalPlan.from_json(json_str)

        assert plan.root_node is not None
        assert plan.root_node.node_id == "root"
        assert plan.root_node.node_type == NodeType.CONTAINER
        assert plan.root_node.exit_condition is not None
        assert plan.root_node.exit_condition.type == ExitConditionType.ALL_CHILDREN_VISITED
