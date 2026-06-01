"""Tests for graph node models.

This module tests the models from src/graph/node.py including:
- Target
- RestoreAction
- Operation
- Precondition
- DynamicRule
- ChildrenStrategy
- ErrorPolicy
- TraversalNode
- NodeType enum
- ChildrenStrategyType enum
"""

import pytest
from src.graph.node import (
    Target,
    RestoreAction,
    Operation,
    Precondition,
    DynamicRule,
    ChildrenStrategy,
    ErrorPolicy,
    TraversalNode,
    NodeType,
    ChildrenStrategyType,
)


class TestNodeType:
    """Tests for NodeType enum."""

    def test_node_type_values(self):
        """Test NodeType has correct values."""
        assert NodeType.CONTAINER.value == "container"
        assert NodeType.LEAF_SWITCH.value == "leaf_switch"
        assert NodeType.LEAF_SLIDER.value == "leaf_slider"
        assert NodeType.LEAF_ACTION.value == "leaf_action"
        assert NodeType.LEAF_INFO.value == "leaf_info"

    def test_node_type_values_method(self):
        """Test NodeType.values() method."""
        values = NodeType.values()
        assert len(values) == 5
        assert "container" in values

    def test_node_type_from_value(self):
        """Test NodeType.from_value() method."""
        node_type = NodeType.from_value("container")
        assert node_type == NodeType.CONTAINER

    def test_node_type_from_value_invalid(self):
        """Test NodeType.from_value() with invalid value."""
        with pytest.raises(ValueError, match="Invalid NodeType value"):
            NodeType.from_value("invalid")

    def test_node_type_is_valid(self):
        """Test NodeType.is_valid() method."""
        assert NodeType.is_valid("container") is True
        assert NodeType.is_valid("invalid") is False


class TestChildrenStrategyType:
    """Tests for ChildrenStrategyType enum."""

    def test_children_strategy_type_values(self):
        """Test ChildrenStrategyType has correct values."""
        assert ChildrenStrategyType.STATIC.value == "static"
        assert ChildrenStrategyType.DYNAMIC_MATCH.value == "dynamic_match"
        assert ChildrenStrategyType.NONE.value == "none"


class TestTarget:
    """Tests for Target model."""

    def test_target_creation(self):
        """Test creating target."""
        target = Target(by="text", value="Settings")
        assert target.by == "text"
        assert target.value == "Settings"

    def test_target_with_meta(self):
        """Test target with metadata."""
        target = Target(
            by="text",
            value="WiFi",
            meta={"source": "ai_analysis"}
        )
        assert target.meta["source"] == "ai_analysis"

    def test_target_by_validation(self):
        """Test Target validates 'by' field."""
        # Valid values
        Target(by="text", value="test")
        Target(by="coordinate", value=(0.5, 0.5))
        Target(by="ui_index", value=0)

        # Invalid value
        with pytest.raises(ValueError, match="Invalid 'by'"):
            Target(by="invalid", value="test")


class TestRestoreAction:
    """Tests for RestoreAction model."""

    def test_restore_action_creation(self):
        """Test creating restore action."""
        restore = RestoreAction(action="click")
        assert restore.action == "click"

    def test_restore_action_with_target(self):
        """Test restore action with target."""
        target = Target(by="text", value="WiFi")
        restore = RestoreAction(action="click", target=target)
        assert restore.target == target

    def test_restore_action_validation(self):
        """Test RestoreAction validates action field."""
        # Valid actions
        RestoreAction(action="click")
        RestoreAction(action="swipe")
        RestoreAction(action="back")
        RestoreAction(action="input_text")
        RestoreAction(action="no_action")

        # Invalid action
        with pytest.raises(ValueError, match="Invalid action"):
            RestoreAction(action="invalid_action")


class TestOperation:
    """Tests for Operation model."""

    def test_operation_creation(self):
        """Test creating operation."""
        op = Operation(action="click")
        assert op.action == "click"

    def test_operation_with_target(self):
        """Test operation with target."""
        target = Target(by="text", value="Settings")
        op = Operation(action="click", target=target)
        assert op.target == target

    def test_operation_with_restore(self):
        """Test operation with restore action."""
        restore = RestoreAction(action="click")
        op = Operation(action="click", restore=restore)
        assert op.restore == restore

    def test_operation_validation(self):
        """Test Operation validates action field."""
        # Valid actions
        Operation(action="click")
        Operation(action="swipe")
        Operation(action="back")
        Operation(action="input_text")
        Operation(action="no_action")

        # Invalid action
        with pytest.raises(ValueError, match="Invalid action"):
            Operation(action="invalid_action")


class TestPrecondition:
    """Tests for Precondition model."""

    def test_precondition_creation(self):
        """Test creating precondition."""
        precond = Precondition(page_name="Settings")
        assert precond.page_name == "Settings"

    def test_precondition_with_path(self):
        """Test precondition with path."""
        precond = Precondition(path=["Home", "Settings"])
        assert precond.path == ["Home", "Settings"]

    def test_precondition_timeout_validation(self):
        """Test Precondition validates timeout_seconds."""
        # Valid timeouts
        Precondition(timeout_seconds=5.0)
        Precondition(timeout_seconds=0.1)
        Precondition(timeout_seconds=300)

        # Invalid - negative
        with pytest.raises(ValueError, match="timeout_seconds must be positive"):
            Precondition(timeout_seconds=-5)

        # Invalid - too large
        with pytest.raises(ValueError, match="timeout_seconds cannot exceed"):
            Precondition(timeout_seconds=500)


class TestDynamicRule:
    """Tests for DynamicRule model."""

    def test_dynamic_rule_creation(self):
        """Test creating dynamic rule."""
        rule = DynamicRule(
            rule_id="rule1",
            match_condition={"type": "menu_item"},
            child_template="menu_container",
        )
        assert rule.rule_id == "rule1"
        assert rule.child_template == "menu_container"

    def test_dynamic_rule_with_action(self):
        """Test dynamic rule with custom action."""
        rule = DynamicRule(
            rule_id="rule1",
            match_condition={},
            child_template="template1",
            action="skip",
        )
        assert rule.action == "skip"

    def test_dynamic_rule_validation(self):
        """Test DynamicRule validates fields."""
        # Valid
        DynamicRule(
            rule_id="test",
            match_condition={},
            child_template="template",
        )

        # Invalid - empty rule_id
        with pytest.raises(ValueError, match="rule_id cannot be empty"):
            DynamicRule(
                rule_id="",
                match_condition={},
                child_template="template",
            )

        # Invalid - empty child_template
        with pytest.raises(ValueError, match="child_template cannot be empty"):
            DynamicRule(
                rule_id="test",
                match_condition={},
                child_template="",
            )

        # Invalid - action
        with pytest.raises(ValueError, match="Invalid action"):
            DynamicRule(
                rule_id="test",
                match_condition={},
                child_template="template",
                action="invalid_action",
            )


class TestChildrenStrategy:
    """Tests for ChildrenStrategy model."""

    def test_children_strategy_creation(self):
        """Test creating children strategy."""
        strategy = ChildrenStrategy(type=ChildrenStrategyType.STATIC)
        assert strategy.type == ChildrenStrategyType.STATIC

    def test_children_strategy_with_static_children(self):
        """Test strategy with static children."""
        strategy = ChildrenStrategy(
            type=ChildrenStrategyType.STATIC,
            static_children=["child1", "child2"],
        )
        assert len(strategy.static_children) == 2

    def test_children_strategy_max_children_validation(self):
        """Test ChildrenStrategy validates max_children."""
        # Valid
        ChildrenStrategy(type=ChildrenStrategyType.NONE, max_children=10)
        ChildrenStrategy(type=ChildrenStrategyType.NONE, max_children=0)
        ChildrenStrategy(type=ChildrenStrategyType.NONE, max_children=10000)

        # Invalid - negative
        with pytest.raises(ValueError, match="max_children cannot be negative"):
            ChildrenStrategy(type=ChildrenStrategyType.NONE, max_children=-1)

        # Invalid - too large
        with pytest.raises(ValueError, match="max_children cannot exceed"):
            ChildrenStrategy(type=ChildrenStrategyType.NONE, max_children=20000)


class TestErrorPolicy:
    """Tests for ErrorPolicy model."""

    def test_error_policy_creation(self):
        """Test creating error policy."""
        policy = ErrorPolicy(on_error="retry")
        assert policy.on_error == "retry"

    def test_error_policy_with_fallback(self):
        """Test error policy with fallback target."""
        policy = ErrorPolicy(
            on_error="fallback",
            fallback_target="parent_node",
        )
        assert policy.fallback_target == "parent_node"

    def test_error_policy_validation(self):
        """Test ErrorPolicy validates on_error field."""
        # Valid
        ErrorPolicy(on_error="retry")
        ErrorPolicy(on_error="skip")
        ErrorPolicy(on_error="abort")
        ErrorPolicy(on_error="fallback")

        # Invalid
        with pytest.raises(ValueError, match="Invalid on_error"):
            ErrorPolicy(on_error="invalid_action")

    def test_error_policy_max_retries_validation(self):
        """Test ErrorPolicy validates max_retries."""
        # Valid
        ErrorPolicy(on_error="retry", max_retries=5)
        ErrorPolicy(on_error="retry", max_retries=0)
        ErrorPolicy(on_error="retry", max_retries=100)

        # Invalid - negative
        with pytest.raises(ValueError, match="max_retries cannot be negative"):
            ErrorPolicy(on_error="retry", max_retries=-1)

        # Invalid - too large
        with pytest.raises(ValueError, match="max_retries cannot exceed"):
            ErrorPolicy(on_error="retry", max_retries=200)


class TestTraversalNode:
    """Tests for TraversalNode model."""

    def test_traversal_node_creation(self):
        """Test creating traversal node."""
        op = Operation(action="no_action")
        strategy = ChildrenStrategy(type=ChildrenStrategyType.STATIC)
        node = TraversalNode(
            node_id="test_node",
            name="Test Node",
            node_type=NodeType.CONTAINER,
            operation=op,
            children_strategy=strategy,
        )
        assert node.node_id == "test_node"
        assert node.name == "Test Node"

    def test_traversal_node_with_precondition(self):
        """Test node with precondition."""
        op = Operation(action="click")
        precond = Precondition(page_name="Settings")
        node = TraversalNode(
            node_id="node1",
            name="Node 1",
            node_type=NodeType.LEAF_ACTION,
            operation=op,
            precondition=precond,
        )
        assert node.precondition is not None
        assert node.precondition.page_name == "Settings"

    def test_traversal_node_validation(self):
        """Test TraversalNode validates required fields."""
        op = Operation(action="no_action")
        strategy = ChildrenStrategy(type=ChildrenStrategyType.STATIC)

        # Valid
        TraversalNode(
            node_id="test",
            name="Test",
            node_type=NodeType.CONTAINER,
            operation=op,
            children_strategy=strategy,
        )

        # Invalid - empty node_id
        with pytest.raises(ValueError, match="node_id cannot be empty"):
            TraversalNode(
                node_id="",
                name="Test",
                node_type=NodeType.CONTAINER,
                operation=op,
                children_strategy=strategy,
            )

        # Invalid - empty name
        with pytest.raises(ValueError, match="name cannot be empty"):
            TraversalNode(
                node_id="test",
                name="",
                node_type=NodeType.CONTAINER,
                operation=op,
                children_strategy=strategy,
            )

    def test_traversal_node_container_validation(self):
        """Test container node must have children strategy."""
        op = Operation(action="no_action")

        # Container with NONE strategy should fail
        with pytest.raises(ValueError, match="must have children strategy"):
            TraversalNode(
                node_id="container1",
                name="Container",
                node_type=NodeType.CONTAINER,
                operation=op,
                children_strategy=ChildrenStrategy(type=ChildrenStrategyType.NONE),
            )

    def test_is_container(self):
        """Test is_container method."""
        op = Operation(action="no_action")
        strategy = ChildrenStrategy(type=ChildrenStrategyType.STATIC)
        container = TraversalNode(
            node_id="c1",
            name="Container",
            node_type=NodeType.CONTAINER,
            operation=op,
            children_strategy=strategy,
        )
        leaf = TraversalNode(
            node_id="l1",
            name="Leaf",
            node_type=NodeType.LEAF_SWITCH,
            operation=op,
        )

        assert container.is_container() is True
        assert leaf.is_container() is False

    def test_is_leaf(self):
        """Test is_leaf method."""
        op = Operation(action="no_action")
        strategy = ChildrenStrategy(type=ChildrenStrategyType.STATIC)
        container = TraversalNode(
            node_id="c1",
            name="Container",
            node_type=NodeType.CONTAINER,
            operation=op,
            children_strategy=strategy,
        )
        leaf = TraversalNode(
            node_id="l1",
            name="Leaf",
            node_type=NodeType.LEAF_SWITCH,
            operation=op,
        )

        assert container.is_leaf() is False
        assert leaf.is_leaf() is True

    def test_has_precondition(self):
        """Test has_precondition method."""
        op = Operation(action="click")
        precond = Precondition(page_name="Settings")

        node_with = TraversalNode(
            node_id="n1",
            name="Node",
            node_type=NodeType.LEAF_ACTION,
            operation=op,
            precondition=precond,
        )
        node_without = TraversalNode(
            node_id="n2",
            name="Node",
            node_type=NodeType.LEAF_ACTION,
            operation=op,
        )

        assert node_with.has_precondition() is True
        assert node_without.has_precondition() is False

    def test_needs_restore(self):
        """Test needs_restore method."""
        op = Operation(action="click")
        restore = RestoreAction(action="click")

        node_with = TraversalNode(
            node_id="n1",
            name="Node",
            node_type=NodeType.LEAF_SWITCH,
            operation=Operation(action="click", restore=restore),
        )
        node_without = TraversalNode(
            node_id="n2",
            name="Node",
            node_type=NodeType.LEAF_ACTION,
            operation=op,
        )

        assert node_with.needs_restore() is True
        assert node_without.needs_restore() is False

    def test_get_child_count(self):
        """Test get_child_count method."""
        op = Operation(action="no_action")
        node = TraversalNode(
            node_id="c1",
            name="Container",
            node_type=NodeType.CONTAINER,
            operation=op,
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.STATIC,
                static_children=["child1", "child2", "child3"],
            ),
        )

        assert node.get_child_count() == 3

    def test_get_set_meta(self):
        """Test get_meta and set_meta methods."""
        op = Operation(action="no_action")
        node = TraversalNode(
            node_id="n1",
            name="Node",
            node_type=NodeType.LEAF_ACTION,
            operation=op,
        )

        assert node.get_meta("custom_key") is None
        node.set_meta("custom_key", "custom_value")
        assert node.get_meta("custom_key") == "custom_value"
