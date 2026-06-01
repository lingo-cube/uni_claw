"""Unit tests for TraversalContext."""

import json
import pytest
from datetime import datetime

from src.context.traversal_context import TraversalContext, ErrorRecord, ActionRecord
from src.ai.types import ContainerInference


class TestErrorRecord:
    """Tests for ErrorRecord."""

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


class TestActionRecord:
    """Tests for ActionRecord."""

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


class TestTraversalContext:
    """Tests for TraversalContext."""

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
        assert context.goal_attempts == {}

    def test_action_history_limit(self):
        """Test action_history is limited to 5 items."""
        actions = [
            ActionRecord(f"action_{i}", f"target_{i}", datetime.now(), f"result_{i}")
            for i in range(10)
        ]
        context = TraversalContext(action_history=actions)
        assert len(context.action_history) == 5
        # Should keep the last 5
        assert context.action_history[0].action_type == "action_5"

    def test_inference_history_limit(self):
        """Test inference_history is limited to 3 items."""
        inferences = [
            ContainerInference(f"TYPE_{i}", 0.5 + i * 0.1) for i in range(10)
        ]
        context = TraversalContext(inference_history=inferences)
        assert len(context.inference_history) == 3
        # Should keep the last 3
        assert context.inference_history[0].container_type == "TYPE_7"

    def test_to_json(self):
        """Test serialization to JSON."""
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
        assert "Settings" in data["visited_pages"]
        assert data["goal_attempts"]["return_to_root"] == 3

    def test_frozen_immutability(self):
        """Test TraversalContext is frozen (immutable)."""
        context = TraversalContext()
        with pytest.raises(Exception):  # FrozenInstanceError
            context.current_path = ["New"]

    def test_with_all_fields(self):
        """Test TraversalContext with all fields populated."""
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
