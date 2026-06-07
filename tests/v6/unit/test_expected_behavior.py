"""Unit tests for ExpectedBehavior module."""

import pytest
import tempfile
from pathlib import Path

from src.simulation.expected_behavior import (
    CompletionMode,
    ExpectedAction,
    ExpectedPageTransition,
    ExpectedBehavior,
)


# -- Task 64: Test loading expected behavior from YAML ----------------------

def test_load_expected_behavior_from_yaml():
    """Test loading expected behavior from a YAML file."""
    yaml_content = """
scenario: "Test Scenario"
description: "Test description"
actions:
  - action: "no_action"
    node: "root"
    order: 0
  - action: "click"
    node: "btn1"
    target: "Submit"
    order: 1

page_transitions:
  - from: "home"
    to: "detail"
    trigger: "btn1"
    order: 0

visited_nodes:
  - "root"
  - "btn1"

final_state: "COMPLETED"
completion_mode: "normal"
"""

    with tempfile.NamedTemporaryFile(mode='w', suffix='.yaml', delete=False) as f:
        f.write(yaml_content)
        temp_path = f.name

    try:
        behavior = ExpectedBehavior.from_yaml(temp_path)

        assert behavior.scenario == "Test Scenario"
        assert behavior.description == "Test description"
        assert len(behavior.actions) == 2
        assert behavior.actions[0].action == "no_action"
        assert behavior.actions[0].node == "root"
        assert behavior.actions[1].action == "click"
        assert behavior.actions[1].target == "Submit"

        assert len(behavior.page_transitions) == 1
        assert behavior.page_transitions[0].from_page == "home"
        assert behavior.page_transitions[0].to_page == "detail"

        assert "root" in behavior.visited_nodes
        assert "btn1" in behavior.visited_nodes

        assert behavior.final_state == "COMPLETED"
        assert behavior.completion_mode == CompletionMode.NORMAL
    finally:
        Path(temp_path).unlink()


# -- Task 63: Test ExpectedBehavior validation --------------------------------

def test_expected_behavior_validation_valid():
    """Test validation passes for valid expected behavior."""
    behavior = ExpectedBehavior(
        scenario="Test",
        description="Test description",
        actions=[
            ExpectedAction(action="no_action", node="root", order=0),
            ExpectedAction(action="click", node="btn1", order=1),
        ],
        visited_nodes={"root", "btn1"},
    )

    errors = behavior.validate()
    assert len(errors) == 0


def test_expected_behavior_validation_action_order_mismatch():
    """Test validation detects action order mismatches."""
    behavior = ExpectedBehavior(
        scenario="Test",
        description="Test description",
        actions=[
            ExpectedAction(action="no_action", node="root", order=0),
            ExpectedAction(action="click", node="btn1", order=0),  # Wrong order
        ],
        visited_nodes={"root", "btn1"},
    )

    errors = behavior.validate()
    assert len(errors) > 0
    assert any("order=" in e for e in errors)


def test_expected_behavior_validation_empty_visited_nodes():
    """Test validation detects empty visited_nodes."""
    behavior = ExpectedBehavior(
        scenario="Test",
        description="Test description",
        actions=[],
        visited_nodes=set(),
    )

    errors = behavior.validate()
    assert len(errors) > 0
    assert any("visited_nodes" in e for e in errors)


def test_expected_behavior_validation_exception_without_exception():
    """Test validation detects EXCEPTION mode without expected_exception."""
    behavior = ExpectedBehavior(
        scenario="Test",
        description="Test description",
        actions=[],
        visited_nodes={"root"},
        completion_mode=CompletionMode.EXCEPTION,
        expected_exception=None,
    )

    errors = behavior.validate()
    assert len(errors) > 0
    assert any("expected_exception" in e for e in errors)


def test_expected_behavior_to_dict():
    """Test to_dict conversion."""
    behavior = ExpectedBehavior(
        scenario="Test",
        description="Test",
        actions=[
            ExpectedAction(action="click", node="btn1", target="Submit", order=0)
        ],
        visited_nodes={"root", "btn1"},
        final_state="COMPLETED",
        completion_mode=CompletionMode.NORMAL,
    )

    result = behavior.to_dict()
    assert result["scenario"] == "Test"
    assert result["final_state"] == "COMPLETED"
    assert result["completion_mode"] == "normal"
    assert len(result["actions"]) == 1
    assert result["actions"][0]["action"] == "click"
    assert "root" in result["visited_nodes"]


# -- Tests for ExpectedAction --------------------------------------------------

def test_expected_action_to_dict():
    """Test ExpectedAction to_dict conversion."""
    action = ExpectedAction(
        action="click",
        node="btn1",
        target="Submit",
        order=0,
    )

    result = action.to_dict()
    assert result["action"] == "click"
    assert result["node"] == "btn1"
    assert result["target"] == "Submit"
    assert result["order"] == 0


# -- Tests for ExpectedPageTransition ----------------------------------------

def test_expected_page_transition_to_dict():
    """Test ExpectedPageTransition to_dict conversion."""
    transition = ExpectedPageTransition(
        from_page="home",
        to_page="detail",
        trigger="btn1",
        order=0,
    )

    result = transition.to_dict()
    assert result["from"] == "home"
    assert result["to"] == "detail"
    assert result["trigger"] == "btn1"
    assert result["order"] == 0


# -- Tests for CompletionMode -------------------------------------------------

def test_completion_mode_values():
    """Test CompletionMode enum values."""
    assert CompletionMode.NORMAL.value == "normal"
    assert CompletionMode.EXCEPTION.value == "exception"
    assert CompletionMode.CANCELLED.value == "cancelled"
    assert CompletionMode.TIMEOUT.value == "timeout"


if __name__ == "__main__":
    pytest.main([__file__, "-v"])
