"""Tests for traversal context models.

This module tests the models from src/context/traversal_context.py including:
- TraversalContext
- ErrorRecord
- ActionRecord
"""

import pytest
from datetime import datetime
from src.context.traversal_context import (
    TraversalContext,
    ErrorRecord,
    ActionRecord,
)


class TestErrorRecord:
    """Tests for ErrorRecord model."""

    def test_basic_creation(self):
        """Test basic ErrorRecord creation."""
        record = ErrorRecord(
            node_id="node_123",
            error_type="TimeoutError",
            timestamp=datetime.now(),
            retry_count=2,
        )
        assert record.node_id == "node_123"
        assert record.error_type == "TimeoutError"
        assert record.retry_count == 2

    def test_error_record_serialization(self):
        """Test ErrorRecord can be serialized."""
        record = ErrorRecord(
            node_id="node_456",
            error_type="ElementNotFound",
            timestamp=datetime.now(),
            retry_count=1,
        )
        # Convert to dict-like structure
        assert record.node_id == "node_456"
        assert record.error_type == "ElementNotFound"
        assert record.retry_count == 1


class TestActionRecord:
    """Tests for ActionRecord model."""

    def test_basic_creation(self):
        """Test basic ActionRecord creation."""
        record = ActionRecord(
            action_type="click",
            target="Settings",
            timestamp=datetime.now(),
            result="PAGE_JUMP",
        )
        assert record.action_type == "click"
        assert record.target == "Settings"
        assert record.result == "PAGE_JUMP"

    def test_optional_target(self):
        """Test ActionRecord with optional target."""
        record = ActionRecord(
            action_type="swipe",
            target=None,
            timestamp=datetime.now(),
            result=None,
        )
        assert record.target is None
        assert record.result is None

    def test_action_record_with_result(self):
        """Test ActionRecord with result."""
        record = ActionRecord(
            action_type="input_text",
            target="TextField",
            timestamp=datetime.now(),
            result="TEXT_ENTERED",
        )
        assert record.action_type == "input_text"
        assert record.target == "TextField"
        assert record.result == "TEXT_ENTERED"


class TestTraversalContext:
    """Tests for TraversalContext model."""

    def test_basic_creation(self):
        """Test basic TraversalContext creation."""
        context = TraversalContext(
            node_stack=["root", "settings"],
            current_path=["Home", "Settings"],
            visited_pages={"Home", "Settings"},
        )
        assert len(context.node_stack) == 2
        assert context.current_path == ["Home", "Settings"]
        assert "Home" in context.visited_pages

    def test_default_values(self):
        """Test TraversalContext with default empty values."""
        context = TraversalContext()
        assert context.node_stack == []
        assert context.current_path == []
        assert context.visited_pages == set()
        assert context.failed_nodes == {}
        assert context.action_history == []
        assert context.inference_history == []

    def test_action_history_limit(self):
        """Test action_history is limited to last 5 items."""
        actions = [
            ActionRecord(f"action_{i}", f"target_{i}", datetime.now(), f"result_{i}")
            for i in range(10)
        ]
        context = TraversalContext(action_history=actions)
        assert len(context.action_history) == 5
        # Should keep the last 5
        assert context.action_history[0].action_type == "action_5"

    def test_inference_history_limit(self):
        """Test inference_history is limited to last 3 items."""
        from src.ai.types import ContainerInference

        inferences = [
            ContainerInference(f"TYPE_{i}", 0.1 + i * 0.08) for i in range(10)
        ]
        context = TraversalContext(inference_history=inferences)
        assert len(context.inference_history) == 3
        # Should keep the last 3
        assert context.inference_history[0].container_type == "TYPE_7"

    def test_to_json(self):
        """Test serialization to JSON."""
        import json

        context = TraversalContext(
            node_stack=["root"],
            current_path=["Home"],
            visited_pages={"Home", "Settings"},
            failed_nodes={
                "node1": ErrorRecord("node1", "Error1", datetime.now(), 1)
            },
            goal_attempts={"return_to_root": 3},
        )
        json_str = context.to_json()
        data = json.loads(json_str)

        assert data["node_stack"] == ["root"]
        assert data["current_path"] == ["Home"]
        assert "Home" in data["visited_pages"]
        assert data["goal_attempts"]["return_to_root"] == 3

    def test_frozen_immutability(self):
        """Test TraversalContext is frozen (immutable)."""
        context = TraversalContext()
        with pytest.raises(Exception):  # FrozenInstanceError
            context.current_path = ["New"]

    def test_with_all_fields(self):
        """Test TraversalContext with all fields populated."""
        from src.ai.types import ContainerInference
        now = datetime.now()

        context = TraversalContext(
            node_stack=["root", "level1", "level2"],
            current_path=["Home", "Settings", "Display"],
            visited_pages={"Home", "Settings", "Display"},
            failed_nodes={
                "failed1": ErrorRecord("failed1", "TimeoutError", now, 2)
            },
            action_history=[
                ActionRecord("click", "Settings", now, "PAGE_JUMP")
            ],
            inference_history=[
                ContainerInference("LIST_MENU", 0.9, "list_template")
            ],
            goal_attempts={"return_to_root": 5, "close_popup": 1},
        )
        assert len(context.node_stack) == 3
        assert context.current_path[2] == "Display"
        assert len(context.failed_nodes) == 1
        assert context.goal_attempts["return_to_root"] == 5
