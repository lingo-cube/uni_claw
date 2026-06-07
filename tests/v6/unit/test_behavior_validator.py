"""Unit tests for BehaviorValidator module."""

import pytest

from src.simulation.expected_behavior import (
    CompletionMode,
    ExpectedAction,
    ExpectedPageTransition,
    ExpectedBehavior,
)
from src.simulation.behavior_validator import (
    ValidationResultStatus,
    MatchType,
    MatchResult,
    ValidationIssue,
    ValidationResult,
    BehaviorValidator,
)


# -- Task 65: Test action sequence validation ---------------------------------

def test_action_sequence_validation_match():
    """Test validation passes when action sequences match."""
    expected = ExpectedBehavior(
        scenario="Test",
        description="Test",
        actions=[
            ExpectedAction(action="no_action", node="root", order=0),
            ExpectedAction(action="click", node="btn1", order=1),
        ],
        visited_nodes={"root", "btn1"},
    )

    actual_trace = [
        # Step nodes for visitation
        {"node_type": "step", "node_id": "root"},
        {"node_type": "step", "node_id": "btn1"},
        # Action spans
        {"node_type": "span", "span_type": "execution", "action": "no_action", "target": "root", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
    ]

    actual_result = {"status": "completed"}

    validator = BehaviorValidator()
    result = validator.validate(expected, actual_trace, actual_result)

    # Should have no errors (warnings are OK)
    error_issues = [i for i in result.issues if i.severity == "error"]
    assert len(error_issues) == 0
    assert result.exact_match_count >= 2


def test_action_sequence_validation_mismatch():
    """Test validation detects action sequence mismatches."""
    expected = ExpectedBehavior(
        scenario="Test",
        description="Test",
        actions=[
            ExpectedAction(action="click", node="btn1", order=0),
        ],
        visited_nodes={"btn1"},
    )

    # Actual trace has wrong action type
    actual_trace = [
        {"node_type": "span", "span_type": "execution", "action": "back", "target": "btn1", "status": "success"},
    ]

    validator = BehaviorValidator()
    result = validator.validate(expected, actual_trace)

    assert not result.is_valid()
    assert any(i.category == "action_sequence" and "Action type mismatch" in i.message for i in result.issues)


# -- Task 66: Test node matching ------------------------------------------------

def test_exact_node_matching():
    """Test exact node ID matching."""
    validator = BehaviorValidator()

    result = validator._match_node("btn1", "btn1")

    assert result.matched is True
    assert result.match_type == MatchType.EXACT
    assert result.confidence == 1.0
    assert result.expected_id == "btn1"
    assert result.actual_id == "btn1"


def test_fuzzy_id_substring_matching():
    """Test fuzzy ID substring matching."""
    validator = BehaviorValidator()

    # Expected ID is substring of actual
    result = validator._match_node("btn1", "btn1_generated_123")

    assert result.matched is True
    assert result.match_type == MatchType.FUZZY_ID
    assert result.confidence == 0.9
    assert "substring match" in result.reason.lower()

    # Actual ID is substring of expected
    result = validator._match_node("btn1_long", "btn1")

    assert result.matched is True
    assert result.match_type == MatchType.FUZZY_ID


def test_no_node_matching():
    """Test no match when IDs are completely different."""
    validator = BehaviorValidator()

    result = validator._match_node("btn1", "switch1")

    assert result.matched is False
    assert result.match_type == MatchType.NONE
    assert result.confidence == 0.0


# -- Task 67-69: Test fuzzy match modes ---------------------------------------

def test_strict_fuzzy_match_mode():
    """Test strict fuzzy match mode treats fuzzy matches as errors."""
    expected = ExpectedBehavior(
        scenario="Test",
        description="Test",
        actions=[
            ExpectedAction(action="click", node="btn1", order=0),
        ],
        visited_nodes={"btn1"},
    )

    # Actual trace has generated ID (fuzzy match)
    actual_trace = [
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1_generated_123", "status": "success"},
        {"node_type": "step", "node_id": "btn1_generated_123"},
    ]

    validator = BehaviorValidator(strict_fuzzy_match=True)
    result = validator.validate(expected, actual_trace)

    # Fuzzy match should be error in strict mode
    assert not result.is_valid()
    fuzzy_issues = [i for i in result.issues if "fuzzy" in i.message.lower()]
    assert len(fuzzy_issues) > 0
    assert any(i.severity == "error" for i in fuzzy_issues)


def test_lenient_fuzzy_match_mode():
    """Test lenient fuzzy match mode treats fuzzy matches as warnings."""
    expected = ExpectedBehavior(
        scenario="Test",
        description="Test",
        actions=[
            ExpectedAction(action="click", node="btn1", order=0),
        ],
        visited_nodes={"btn1"},
    )

    # Actual trace has generated ID (fuzzy match)
    actual_trace = [
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1_generated_123", "status": "success"},
        {"node_type": "step", "node_id": "btn1_generated_123"},
    ]

    validator = BehaviorValidator(strict_fuzzy_match=False)
    result = validator.validate(expected, actual_trace)

    # Fuzzy match should be warning in lenient mode
    fuzzy_issues = [i for i in result.issues if "fuzzy" in i.message.lower()]
    assert len(fuzzy_issues) > 0
    # All fuzzy issues should be warnings, not errors
    assert all(i.severity != "error" for i in fuzzy_issues)


# -- Tests for page transition validation --------------------------------------

def test_page_transition_validation_match():
    """Test page transition validation passes when transitions match."""
    expected = ExpectedBehavior(
        scenario="Test",
        description="Test",
        page_transitions=[
            ExpectedPageTransition(from_page="home", to_page="detail", trigger="btn1", order=0),
        ],
        visited_nodes={"root"},
    )

    actual_trace = [
        {"node_type": "span", "span_type": "page_transition", "from_page": "home", "to_page": "detail", "trigger_element": "btn1"},
    ]

    validator = BehaviorValidator()
    result = validator.validate(expected, actual_trace)

    transition_errors = [i for i in result.issues if i.category == "page_transition" and i.severity == "error"]
    assert len(transition_errors) == 0


def test_page_transition_validation_missing():
    """Test page transition validation detects missing transitions."""
    expected = ExpectedBehavior(
        scenario="Test",
        description="Test",
        page_transitions=[
            ExpectedPageTransition(from_page="home", to_page="detail", trigger="btn1", order=0),
        ],
        visited_nodes={"root"},
    )

    actual_trace = []  # No transitions in actual trace

    validator = BehaviorValidator()
    result = validator.validate(expected, actual_trace)

    transition_errors = [i for i in result.issues if i.category == "page_transition"]
    assert len(transition_errors) > 0
    assert any("Missing expected page transition" in e.message for e in transition_errors)


# -- Tests for node visitation validation --------------------------------------

def test_node_visitation_validation_all_visited():
    """Test node visitation validation when all expected nodes visited."""
    expected = ExpectedBehavior(
        scenario="Test",
        description="Test",
        visited_nodes={"root", "btn1", "btn2"},
    )

    actual_trace = [
        {"node_type": "step", "node_id": "root"},
        {"node_type": "step", "node_id": "btn1"},
        {"node_type": "step", "node_id": "btn2"},
    ]

    validator = BehaviorValidator()
    result = validator.validate(expected, actual_trace)

    visitation_errors = [i for i in result.issues if i.category == "node_visitation" and i.severity == "error"]
    assert len(visitation_errors) == 0


def test_node_visitation_validation_missing_node():
    """Test node visitation validation detects missing nodes."""
    expected = ExpectedBehavior(
        scenario="Test",
        description="Test",
        visited_nodes={"root", "btn1", "btn2"},
    )

    actual_trace = [
        {"node_type": "step", "node_id": "root"},
        {"node_type": "step", "node_id": "btn1"},
        # btn2 is missing
    ]

    validator = BehaviorValidator()
    result = validator.validate(expected, actual_trace)

    visitation_errors = [i for i in result.issues if i.category == "node_visitation" and i.severity == "error"]
    assert len(visitation_errors) > 0
    assert any("btn2" in str(i.expected) for i in visitation_errors)


def test_node_visitation_validation_unexpected_node():
    """Test node visitation validation detects unexpected nodes."""
    expected = ExpectedBehavior(
        scenario="Test",
        description="Test",
        visited_nodes={"root", "btn1"},
    )

    actual_trace = [
        {"node_type": "step", "node_id": "root"},
        {"node_type": "step", "node_id": "btn1"},
        {"node_type": "step", "node_id": "btn_unexpected"},
    ]

    validator = BehaviorValidator()
    result = validator.validate(expected, actual_trace)

    visitation_warnings = [i for i in result.issues if i.category == "node_visitation" and i.severity == "warning"]
    assert len(visitation_warnings) > 0
    assert any("Unexpected visited node" in i.message for i in visitation_warnings)


# -- Tests for final state validation -----------------------------------------

def test_final_state_validation_match():
    """Test final state validation passes when states match."""
    expected = ExpectedBehavior(
        scenario="Test",
        description="Test",
        visited_nodes={"root"},
        final_state="COMPLETED",
    )

    actual_result = {"status": "completed"}

    validator = BehaviorValidator()
    result = validator.validate(expected, [], actual_result)

    state_errors = [i for i in result.issues if i.category == "state" and i.severity == "error"]
    assert len(state_errors) == 0


def test_final_state_validation_mismatch():
    """Test final state validation detects state mismatches."""
    expected = ExpectedBehavior(
        scenario="Test",
        description="Test",
        visited_nodes={"root"},
        final_state="COMPLETED",
    )

    actual_result = {"status": "error"}

    validator = BehaviorValidator()
    result = validator.validate(expected, [], actual_result)

    state_errors = [i for i in result.issues if i.category == "state"]
    assert len(state_errors) > 0
    assert any("state mismatch" in i.message.lower() for i in state_errors)


# -- Tests for completion mode validation -------------------------------------

def test_completion_mode_validation_normal():
    """Test completion mode validation for NORMAL mode."""
    expected = ExpectedBehavior(
        scenario="Test",
        description="Test",
        visited_nodes={"root"},
        completion_mode=CompletionMode.NORMAL,
    )

    actual_result = {"status": "completed"}

    validator = BehaviorValidator()
    result = validator.validate(expected, [], actual_result)

    completion_errors = [i for i in result.issues if i.category == "completion_mode" and i.severity == "error"]
    assert len(completion_errors) == 0


def test_completion_mode_validation_exception_match():
    """Test completion mode validation for EXCEPTION mode with matching exception."""
    expected = ExpectedBehavior(
        scenario="Test",
        description="Test",
        visited_nodes={"root"},
        completion_mode=CompletionMode.EXCEPTION,
        expected_exception="TimeoutError",
    )

    actual_result = {"status": "error", "error_type": "TimeoutError"}

    validator = BehaviorValidator()
    result = validator.validate(expected, [], actual_result)

    completion_errors = [i for i in result.issues if i.category == "completion_mode" and i.severity == "error"]
    assert len(completion_errors) == 0


def test_completion_mode_validation_exception_mismatch():
    """Test completion mode validation for EXCEPTION mode without exception."""
    expected = ExpectedBehavior(
        scenario="Test",
        description="Test",
        visited_nodes={"root"},
        completion_mode=CompletionMode.EXCEPTION,
        expected_exception="TimeoutError",
    )

    actual_result = {"status": "completed"}  # No exception

    validator = BehaviorValidator()
    result = validator.validate(expected, [], actual_result)

    completion_errors = [i for i in result.issues if i.category == "completion_mode"]
    assert len(completion_errors) > 0


# -- Tests for helper extraction methods ---------------------------------------

def test_extract_actions():
    """Test _extract_actions extracts actions from trace."""
    validator = BehaviorValidator()

    trace = [
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "back", "target": None, "status": "success"},
    ]

    actions = validator._extract_actions(trace)

    assert len(actions) == 2
    assert actions[0]["action"] == "click"
    assert actions[0]["node_id"] == "btn1"
    assert actions[1]["action"] == "back"


def test_extract_page_transitions():
    """Test _extract_page_transitions extracts transitions from trace."""
    validator = BehaviorValidator()

    trace = [
        {"node_type": "span", "span_type": "page_transition", "from_page": "home", "to_page": "detail", "trigger_element": "btn1"},
    ]

    transitions = validator._extract_page_transitions(trace)

    assert len(transitions) == 1
    assert transitions[0]["from_page"] == "home"
    assert transitions[0]["to_page"] == "detail"
    assert transitions[0]["trigger_element"] == "btn1"


def test_extract_visited_nodes():
    """Test _extract_visited_nodes extracts visited nodes from trace."""
    validator = BehaviorValidator()

    trace = [
        {"node_type": "step", "node_id": "root"},
        {"node_type": "step", "node_id": "btn1"},
        {"node_type": "span", "span_type": "execution"},  # Not a step, should be ignored
    ]

    visited = validator._extract_visited_nodes(trace)

    assert "root" in visited
    assert "btn1" in visited
    assert len(visited) == 2


# -- Tests for ValidationResult -----------------------------------------------

def test_validation_result_add_issue():
    """Test adding issues to ValidationResult."""
    result = ValidationResult()

    result.add_issue("test_category", "error", "Test issue", expected="expected", actual="actual")

    assert len(result.issues) == 1
    assert result.issues[0].category == "test_category"
    assert result.issues[0].severity == "error"
    assert result.status == ValidationResultStatus.FAIL


def test_validation_result_add_warning():
    """Test adding warnings doesn't set status to FAIL."""
    result = ValidationResult()

    result.add_issue("test_category", "warning", "Test warning")

    assert len(result.issues) == 1
    assert result.status == ValidationResultStatus.WARNING
    assert result.is_valid()  # WARNING is still valid (no errors)


def test_validation_result_is_valid():
    """Test is_valid returns False when there are errors."""
    result = ValidationResult()

    assert result.is_valid()  # No errors yet

    result.add_issue("test", "error", "Error")

    assert not result.is_valid()  # Has error


if __name__ == "__main__":
    pytest.main([__file__, "-v"])
