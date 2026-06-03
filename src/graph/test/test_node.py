"""
Unit tests for TraversalNode and related data classes.

Tests cover:
- TraversalNode data class
- Operation and Target classes
- Precondition validation
- ChildrenStrategy (static and dynamic)
"""

import pytest

from src.graph.node import (
    ChildrenStrategy,
    ChildrenStrategyType,
    DynamicRule,
    ErrorPolicy,
    NodeType,
    Operation,
    Precondition,
    RestoreAction,
    Target,
    TraversalNode,
)


class TestTarget:
    """Tests for Target class."""

    def test_create_text_target(self):
        """Test creating a text-based target."""
        target = Target(by="text", value="Settings")
        assert target.by == "text"
        assert target.value == "Settings"

    def test_create_coordinate_target(self):
        """Test creating a coordinate-based target."""
        target = Target(by="coordinate", value=(0.5, 0.7))
        assert target.by == "coordinate"
        assert target.value == (0.5, 0.7)

    def test_create_ui_index_target(self):
        """Test creating a UI index target."""
        target = Target(by="ui_index", value=3)
        assert target.by == "ui_index"
        assert target.value == 3

    def test_invalid_by_raises_error(self):
        """Test that invalid 'by' value raises error."""
        with pytest.raises(ValueError, match="Invalid 'by'"):
            Target(by="invalid", value="test")

    def test_target_with_meta(self):
        """Test target with metadata."""
        target = Target(by="text", value="test", meta={"confidence": 0.9})
        assert target.meta == {"confidence": 0.9}


class TestRestoreAction:
    """Tests for RestoreAction class."""

    def test_create_restore_action(self):
        """Test creating a restore action."""
        restore = RestoreAction(action="click", target=Target(by="text", value="Back"))
        assert restore.action == "click"
        assert restore.target is not None
        assert restore.target.value == "Back"

    def test_restore_action_with_params(self):
        """Test restore action with parameters."""
        restore = RestoreAction(
            action="swipe",
            params={"direction": "left", "distance": 0.2},
        )
        assert restore.action == "swipe"
        assert restore.params == {"direction": "left", "distance": 0.2}


class TestOperation:
    """Tests for Operation class."""

    def test_create_click_operation(self):
        """Test creating a click operation."""
        op = Operation(action="click", target=Target(by="text", value="Submit"))
        assert op.action == "click"
        assert op.target.value == "Submit"

    def test_create_swipe_operation(self):
        """Test creating a swipe operation."""
        op = Operation(
            action="swipe",
            target=Target(by="coordinate", value=(0.5, 0.5)),
            params={"direction": "up", "distance": 0.3},
        )
        assert op.action == "swipe"
        assert op.params["direction"] == "up"

    def test_create_back_operation(self):
        """Test creating a back operation (no target)."""
        op = Operation(action="back")
        assert op.action == "back"
        assert op.target is None

    def test_operation_with_restore(self):
        """Test operation with restore action."""
        op = Operation(
            action="click",
            target=Target(by="text", value="Toggle"),
            restore=RestoreAction(action="click", target=Target(by="text", value="Toggle")),
        )
        assert op.restore is not None
        assert op.restore.action == "click"

    def test_invalid_action_raises_error(self):
        """Test that invalid action raises error."""
        with pytest.raises(ValueError, match="Invalid action"):
            Operation(action="invalid_action")


class TestPrecondition:
    """Tests for Precondition class."""

    def test_create_page_name_precondition(self):
        """Test precondition with page name."""
        precond = Precondition(page_name="SettingsPage")
        assert precond.page_name == "SettingsPage"

    def test_create_path_precondition(self):
        """Test precondition with path."""
        precond = Precondition(path=["Home", "Settings", "Display"])
        assert precond.path == ["Home", "Settings", "Display"]

    def test_create_ui_condition_precondition(self):
        """Test precondition with UI condition."""
        precond = Precondition(ui_condition="switch.enabled == true")
        assert precond.ui_condition == "switch.enabled == true"

    def test_precondition_with_timeout(self):
        """Test precondition with custom timeout."""
        precond = Precondition(page_name="TestPage", timeout_seconds=10.0)
        assert precond.timeout_seconds == 10.0

    def test_default_timeout(self):
        """Test default timeout value."""
        precond = Precondition(page_name="Test")
        assert precond.timeout_seconds == 5.0


class TestDynamicRule:
    """Tests for DynamicRule class."""

    def test_create_dynamic_rule(self):
        """Test creating a dynamic rule."""
        rule = DynamicRule(
            rule_id="menu_rule",
            match_condition={"type": "menu_item"},
            child_template="menu_container",
        )
        assert rule.rule_id == "menu_rule"
        assert rule.match_condition == {"type": "menu_item"}
        assert rule.child_template == "menu_container"

    def test_dynamic_rule_with_action(self):
        """Test dynamic rule with custom action."""
        rule = DynamicRule(
            rule_id="skip_rule",
            match_condition={"type": "disabled"},
            child_template="none",
            action="skip",
        )
        assert rule.action == "skip"


class TestChildrenStrategy:
    """Tests for ChildrenStrategy class."""

    def test_static_strategy(self):
        """Test static children strategy."""
        strategy = ChildrenStrategy(
            type=ChildrenStrategyType.STATIC,
            static_children=["child1", "child2", "child3"],
        )
        assert strategy.type == ChildrenStrategyType.STATIC
        assert len(strategy.static_children) == 3

    def test_dynamic_match_strategy(self):
        """Test dynamic match strategy."""
        rule = DynamicRule(
            rule_id="rule1",
            match_condition={"type": "menu"},
            child_template="menu_container",
        )
        strategy = ChildrenStrategy(
            type=ChildrenStrategyType.DYNAMIC_MATCH,
            dynamic_rules={"rule1": rule},
        )
        assert strategy.type == ChildrenStrategyType.DYNAMIC_MATCH
        assert "rule1" in strategy.dynamic_rules

    def test_none_strategy(self):
        """Test none strategy (leaf node)."""
        strategy = ChildrenStrategy(type=ChildrenStrategyType.NONE)
        assert strategy.type == ChildrenStrategyType.NONE
        assert len(strategy.static_children) == 0

    def test_max_children_limit(self):
        """Test max children limit."""
        strategy = ChildrenStrategy(
            type=ChildrenStrategyType.DYNAMIC_MATCH,
            max_children=50,
        )
        assert strategy.max_children == 50

    def test_default_max_children(self):
        """Test default max children value."""
        strategy = ChildrenStrategy(type=ChildrenStrategyType.STATIC)
        assert strategy.max_children == 100


class TestErrorPolicy:
    """Tests for ErrorPolicy class."""

    def test_retry_policy(self):
        """Test retry error policy."""
        policy = ErrorPolicy(on_error="retry", max_retries=3)
        assert policy.on_error == "retry"
        assert policy.max_retries == 3

    def test_skip_policy(self):
        """Test skip error policy."""
        policy = ErrorPolicy(on_error="skip")
        assert policy.on_error == "skip"

    def test_abort_policy(self):
        """Test abort error policy."""
        policy = ErrorPolicy(on_error="abort")
        assert policy.on_error == "abort"

    def test_fallback_policy(self):
        """Test fallback error policy."""
        policy = ErrorPolicy(on_error="fallback", fallback_target="parent_node")
        assert policy.on_error == "fallback"
        assert policy.fallback_target == "parent_node"

    def test_continue_on_error(self):
        """Test continue_on_error flag."""
        policy = ErrorPolicy(on_error="skip", continue_on_error=True)
        assert policy.continue_on_error is True


class TestTraversalNode:
    """Tests for TraversalNode class."""

    def test_create_container_node(self):
        """Test creating a container node."""
        node = TraversalNode(
            node_id="settings_root",
            name="Settings",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click", target=Target(by="text", value="Settings")),
            children_strategy=ChildrenStrategy(type=ChildrenStrategyType.DYNAMIC_MATCH),
        )
        assert node.node_id == "settings_root"
        assert node.name == "Settings"
        assert node.node_type == NodeType.CONTAINER
        assert node.is_container()
        assert not node.is_leaf()

    def test_create_leaf_node(self):
        """Test creating a leaf node."""
        node = TraversalNode(
            node_id="brightness_slider",
            name="Brightness",
            node_type=NodeType.LEAF_SLIDER,
            operation=Operation(action="swipe"),
            children_strategy=ChildrenStrategy(type=ChildrenStrategyType.NONE),
        )
        assert node.node_type == NodeType.LEAF_SLIDER
        assert node.is_leaf()
        assert not node.is_container()

    def test_node_with_precondition(self):
        """Test node with precondition."""
        node = TraversalNode(
            node_id="test_node",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
            precondition=Precondition(page_name="RequiredPage"),
        )
        assert node.has_precondition()
        assert node.precondition.page_name == "RequiredPage"

    def test_node_with_error_policy(self):
        """Test node with error policy."""
        node = TraversalNode(
            node_id="test_node",
            name="Test",
            node_type=NodeType.LEAF_ACTION,
            operation=Operation(action="click"),
            error_policy=ErrorPolicy(on_error="retry", max_retries=2),
        )
        assert node.error_policy.on_error == "retry"
        assert node.error_policy.max_retries == 2

    def test_node_with_restore_operation(self):
        """Test node with restore operation."""
        node = TraversalNode(
            node_id="toggle_switch",
            name="Airplane Mode",
            node_type=NodeType.LEAF_SWITCH,
            operation=Operation(
                action="click",
                restore=RestoreAction(action="click"),
            ),
        )
        assert node.needs_restore()
        assert node.operation.restore is not None

    def test_node_meta_operations(self):
        """Test node metadata operations."""
        node = TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.LEAF_ACTION,
            operation=Operation(action="click"),
        )

        # Test set and get meta
        node.set_meta("visited_count", 5)
        assert node.get_meta("visited_count") == 5
        assert node.get_meta("nonexistent", "default") == "default"

    def test_container_without_children_strategy_raises_error(self):
        """Test that container without children strategy raises error."""
        with pytest.raises(ValueError, match="must have children strategy"):
            TraversalNode(
                node_id="invalid",
                name="Invalid",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="click"),
                children_strategy=ChildrenStrategy(type=ChildrenStrategyType.NONE),
            )

    def test_empty_node_id_raises_error(self):
        """Test that empty node_id raises error."""
        with pytest.raises(ValueError, match="node_id cannot be empty"):
            TraversalNode(
                node_id="",
                name="Test",
                node_type=NodeType.LEAF_ACTION,
                operation=Operation(action="click"),
            )

    def test_empty_name_raises_error(self):
        """Test that empty name raises error."""
        with pytest.raises(ValueError, match="name cannot be empty"):
            TraversalNode(
                node_id="test",
                name="",
                node_type=NodeType.LEAF_ACTION,
                operation=Operation(action="click"),
            )

    def test_get_child_count_static(self):
        """Test getting child count for static strategy."""
        node = TraversalNode(
            node_id="parent",
            name="Parent",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.STATIC,
                static_children=["c1", "c2", "c3"],
            ),
        )
        assert node.get_child_count() == 3

    def test_get_child_count_dynamic(self):
        """Test getting child count for dynamic strategy."""
        node = TraversalNode(
            node_id="parent",
            name="Parent",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="click"),
            children_strategy=ChildrenStrategy(type=ChildrenStrategyType.DYNAMIC_MATCH),
        )
        assert node.get_child_count() == 0
