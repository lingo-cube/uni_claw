"""Unified tests for all enum helper methods.

This module tests the values(), from_value(), and is_valid() methods
for all enum types in the Uni-Claw system.
"""

import pytest
from src.state.content_tree import Direction, MenuItemType, ExpectedAction
from src.graph.node import NodeType, ChildrenStrategyType
from src.state_machine.global_fsm import GlobalState
from src.state_machine.traversal_fsm import TraversalState
from src.exception.exceptions import ExceptionSeverity
from src.exception.context import ExceptionAction, RecoveryAction
from src.trace.models import ExecutionStatus

# List of all enum classes to test
ALL_ENUMS = [
    Direction,
    MenuItemType,
    ExpectedAction,
    NodeType,
    ChildrenStrategyType,
    GlobalState,
    TraversalState,
    ExceptionSeverity,
    ExceptionAction,
    RecoveryAction,
    ExecutionStatus,
]


class TestEnumHelperMethods:
    """Test suite for enum helper methods."""

    def test_all_enums_have_values_method(self):
        """Verify all enums have values() class method."""
        for enum_class in ALL_ENUMS:
            assert hasattr(enum_class, 'values'), f"{enum_class.__name__} missing values() method"
            assert callable(enum_class.values), f"{enum_class.__name__}.values is not callable"

    def test_all_enums_have_from_value_method(self):
        """Verify all enums have from_value() class method."""
        for enum_class in ALL_ENUMS:
            assert hasattr(enum_class, 'from_value'), f"{enum_class.__name__} missing from_value() method"
            assert callable(enum_class.from_value), f"{enum_class.__name__}.from_value is not callable"

    def test_all_enums_have_is_valid_method(self):
        """Verify all enums have is_valid() class method."""
        for enum_class in ALL_ENUMS:
            assert hasattr(enum_class, 'is_valid'), f"{enum_class.__name__} missing is_valid() method"
            assert callable(enum_class.is_valid), f"{enum_class.__name__}.is_valid is not callable"


class TestDirection:
    """Tests for Direction enum."""

    def test_values_returns_all_directions(self):
        """Test values() returns all direction values."""
        values = Direction.values()
        assert len(values) == 4
        assert "left" in values
        assert "right" in values
        assert "top" in values
        assert "bottom" in values

    def test_from_value_valid(self):
        """Test from_value() with valid input."""
        result = Direction.from_value("left")
        assert result == Direction.LEFT

    def test_from_value_invalid(self):
        """Test from_value() with invalid input raises ValueError."""
        with pytest.raises(ValueError, match="Invalid Direction value"):
            Direction.from_value("diagonal")

    def test_is_valid_true(self):
        """Test is_valid() returns True for valid values."""
        assert Direction.is_valid("top") is True

    def test_is_valid_false(self):
        """Test is_valid() returns False for invalid values."""
        assert Direction.is_valid("diagonal") is False
        assert Direction.is_valid("") is False


class TestMenuItemType:
    """Tests for MenuItemType enum."""

    def test_values_returns_all_types(self):
        """Test values() returns all menu item types."""
        values = MenuItemType.values()
        assert len(values) == 11  # All types including legacy ITEM
        assert "menu_item" in values
        assert "switch" in values

    def test_from_value_valid(self):
        """Test from_value() with valid input."""
        result = MenuItemType.from_value("switch")
        assert result == MenuItemType.SWITCH

    def test_from_value_invalid(self):
        """Test from_value() with invalid input raises ValueError."""
        with pytest.raises(ValueError, match="Invalid MenuItemType value"):
            MenuItemType.from_value("unknown_type")

    def test_is_valid_true(self):
        """Test is_valid() returns True for valid values."""
        assert MenuItemType.is_valid("tab") is True

    def test_is_valid_false(self):
        """Test is_valid() returns False for invalid values."""
        assert MenuItemType.is_valid("unknown") is False


class TestExpectedAction:
    """Tests for ExpectedAction enum."""

    def test_values_returns_all_actions(self):
        """Test values() returns all expected actions."""
        values = ExpectedAction.values()
        assert len(values) == 4
        assert "navigate" in values
        assert "toggle" in values
        assert "action" in values
        assert "none" in values

    def test_from_value_valid(self):
        """Test from_value() with valid input."""
        result = ExpectedAction.from_value("toggle")
        assert result == ExpectedAction.TOGGLE

    def test_from_value_invalid(self):
        """Test from_value() with invalid input raises ValueError."""
        with pytest.raises(ValueError, match="Invalid ExpectedAction value"):
            ExpectedAction.from_value("unknown")


class TestNodeType:
    """Tests for NodeType enum."""

    def test_values_returns_all_types(self):
        """Test values() returns all node types."""
        values = NodeType.values()
        assert len(values) == 5
        assert "container" in values
        assert "leaf_switch" in values

    def test_from_value_valid(self):
        """Test from_value() with valid input."""
        result = NodeType.from_value("container")
        assert result == NodeType.CONTAINER


class TestChildrenStrategyType:
    """Tests for ChildrenStrategyType enum."""

    def test_values_returns_all_strategies(self):
        """Test values() returns all strategy types."""
        values = ChildrenStrategyType.values()
        assert len(values) == 3
        assert "static" in values
        assert "dynamic_match" in values
        assert "none" in values


class TestGlobalState:
    """Tests for GlobalState enum."""

    def test_values_returns_all_states(self):
        """Test values() returns all global states."""
        values = GlobalState.values()
        assert len(values) == 8
        assert "idle" in values
        assert "traversing" in values
        assert "completed" in values

    def test_from_value_valid(self):
        """Test from_value() with valid input."""
        result = GlobalState.from_value("idle")
        assert result == GlobalState.IDLE


class TestTraversalState:
    """Tests for TraversalState enum."""

    def test_values_returns_all_states(self):
        """Test values() returns all traversal states."""
        values = TraversalState.values()
        assert len(values) == 5
        assert "node_select" in values
        assert "execute" in values


class TestExceptionSeverity:
    """Tests for ExceptionSeverity enum."""

    def test_values_returns_all_severities(self):
        """Test values() returns all severity levels."""
        values = ExceptionSeverity.values()
        assert len(values) == 5
        assert "info" in values
        assert "warning" in values
        assert "error" in values
        assert "critical" in values
        assert "fatal" in values

    def test_from_value_valid(self):
        """Test from_value() with valid input."""
        result = ExceptionSeverity.from_value("error")
        assert result == ExceptionSeverity.ERROR


class TestExceptionAction:
    """Tests for ExceptionAction enum."""

    def test_values_returns_all_actions(self):
        """Test values() returns all exception actions."""
        values = ExceptionAction.values()
        assert len(values) == 6
        assert "retry" in values
        assert "skip" in values
        assert "backtrack" in values


class TestRecoveryAction:
    """Tests for RecoveryAction enum."""

    def test_values_returns_all_actions(self):
        """Test values() returns all recovery actions."""
        values = RecoveryAction.values()
        assert len(values) == 6
        assert "reconnect_adb" in values
        assert "close_popup" in values


class TestExecutionStatus:
    """Tests for ExecutionStatus enum."""

    def test_values_returns_all_statuses(self):
        """Test values() returns all execution statuses."""
        values = ExecutionStatus.values()
        assert len(values) == 4
        assert "success" in values
        assert "failed" in values
        assert "skipped" in values
        assert "timeout" in values

    def test_from_value_valid(self):
        """Test from_value() with valid input."""
        result = ExecutionStatus.from_value("success")
        assert result == ExecutionStatus.SUCCESS


class TestEdgeCases:
    """Test edge cases for enum helper methods."""

    def test_from_value_case_sensitive(self):
        """Test from_value() is case sensitive."""
        with pytest.raises(ValueError):
            Direction.from_value("Left")  # Should be "left"

    def test_from_value_empty_string(self):
        """Test from_value() with empty string raises ValueError."""
        with pytest.raises(ValueError):
            Direction.from_value("")

    def test_is_valid_empty_string(self):
        """Test is_valid() returns False for empty string."""
        assert Direction.is_valid("") is False

    def test_is_valid_case_sensitive(self):
        """Test is_valid() is case sensitive."""
        assert Direction.is_valid("left") is True
        assert Direction.is_valid("Left") is False

    def test_error_message_includes_valid_values(self):
        """Test that ValueError from from_value() includes valid values."""
        with pytest.raises(ValueError) as exc_info:
            Direction.from_value("invalid")
        error_message = str(exc_info.value)
        assert "Valid values:" in error_message or "valid values" in error_message.lower()
        assert "left" in error_message
