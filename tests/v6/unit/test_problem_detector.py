"""Unit tests for ProblemDetector module."""

import pytest

from src.simulation.problem_detector import (
    ProblemType,
    ProblemSeverity,
    SensitivityLevel,
    Problem,
    ProblemDetectorConfig,
    ProblemDetector,
)


# -- Task 8.2: Test infinite loop detection for repeated actions -----------------


def test_infinite_loop_detection_repeated_actions():
    """Test detection of infinite loop from repeated actions on same element."""
    detector = ProblemDetector()

    # Create trace with same action repeated 4 times (exceeds default max of 3)
    trace = [
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
    ]

    problems = detector.detect(trace)

    # Should detect infinite loop
    assert len(problems) > 0
    infinite_loop_problems = [p for p in problems if p.type == ProblemType.INFINITE_LOOP]
    assert len(infinite_loop_problems) > 0
    assert infinite_loop_problems[0].severity == ProblemSeverity.CRITICAL
    assert infinite_loop_problems[0].location == "btn1"
    assert infinite_loop_problems[0].evidence["repeat_count"] >= 4


def test_infinite_loop_detection_no_loop_under_threshold():
    """Test that repeated actions under threshold don't trigger detection."""
    detector = ProblemDetector()

    # Create trace with same action repeated 2 times (under default max of 3)
    trace = [
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
    ]

    problems = detector.detect(trace)

    # Should not detect infinite loop
    infinite_loop_problems = [p for p in problems if p.type == ProblemType.INFINITE_LOOP]
    assert len(infinite_loop_problems) == 0


# -- Task 8.3: Test infinite loop detection for state sequence loops -------------


def test_infinite_loop_detection_state_sequence():
    """Test detection of infinite loop from repeating state sequence."""
    detector = ProblemDetector()

    # Create trace with EXECUTING -> AUTO_ESCAPE -> EXECUTING -> AUTO_ESCAPE pattern
    trace = [
        {"node_type": "span", "span_type": "state_transition", "from_state": "IDLE", "to_state": "EXECUTING"},
        {"node_type": "span", "span_type": "state_transition", "from_state": "EXECUTING", "to_state": "AUTO_ESCAPE"},
        {"node_type": "span", "span_type": "state_transition", "from_state": "AUTO_ESCAPE", "to_state": "EXECUTING"},
        {"node_type": "span", "span_type": "state_transition", "from_state": "EXECUTING", "to_state": "AUTO_ESCAPE"},
        {"node_type": "span", "span_type": "state_transition", "from_state": "AUTO_ESCAPE", "to_state": "EXECUTING"},
        {"node_type": "span", "span_type": "state_transition", "from_state": "EXECUTING", "to_state": "AUTO_ESCAPE"},
    ]

    problems = detector.detect(trace)

    # Should detect infinite loop
    infinite_loop_problems = [p for p in problems if p.type == ProblemType.INFINITE_LOOP]
    assert len(infinite_loop_problems) > 0
    # State sequence loops have WARNING severity
    assert infinite_loop_problems[0].severity == ProblemSeverity.WARNING
    assert infinite_loop_problems[0].location == "state_machine"
    assert "pattern" in infinite_loop_problems[0].evidence


def test_infinite_loop_detection_no_pattern_unique_sequence():
    """Test that unique state sequence doesn't trigger loop detection."""
    detector = ProblemDetector()

    # Create trace with all unique states (no pattern)
    trace = [
        {"node_type": "span", "span_type": "state_transition", "from_state": "IDLE", "to_state": "EXECUTING"},
        {"node_type": "span", "span_type": "state_transition", "from_state": "EXECUTING", "to_state": "RESULT_VERIFY"},
        {"node_type": "span", "span_type": "state_transition", "from_state": "RESULT_VERIFY", "to_state": "FRAME_COMPLETE"},
        {"node_type": "span", "span_type": "state_transition", "from_state": "FRAME_COMPLETE", "to_state": "COMPLETED"},
    ]

    problems = detector.detect(trace)

    # Should not detect infinite loop
    infinite_loop_problems = [p for p in problems if p.type == ProblemType.INFINITE_LOOP]
    assert len(infinite_loop_problems) == 0


# -- Task 8.4: Test unvisited node detection ------------------------------------


def test_unvisited_node_detection():
    """Test detection of expected nodes that were not visited."""
    detector = ProblemDetector()

    # Expected to visit root, btn1, btn2
    expected_nodes = {"root", "btn1", "btn2"}

    # But only visited root and btn1
    trace = [
        {"node_type": "step", "node_id": "root"},
        {"node_type": "step", "node_id": "btn1"},
    ]

    problems = detector.detect(trace, expected_nodes=expected_nodes)

    # Should detect btn2 as unvisited
    unvisited_problems = [p for p in problems if p.type == ProblemType.UNVISITED_NODE]
    assert len(unvisited_problems) == 1
    assert unvisited_problems[0].location == "btn2"
    assert unvisited_problems[0].severity == ProblemSeverity.WARNING
    assert "btn2" in unvisited_problems[0].description


def test_unvisited_node_detection_all_visited():
    """Test that all expected nodes visited doesn't trigger detection."""
    detector = ProblemDetector()

    expected_nodes = {"root", "btn1", "btn2"}

    # All expected nodes were visited
    trace = [
        {"node_type": "step", "node_id": "root"},
        {"node_type": "step", "node_id": "btn1"},
        {"node_type": "step", "node_id": "btn2"},
    ]

    problems = detector.detect(trace, expected_nodes=expected_nodes)

    # Should not detect unvisited nodes
    unvisited_problems = [p for p in problems if p.type == ProblemType.UNVISITED_NODE]
    assert len(unvisited_problems) == 0


# -- Task 8.5: Test repeated action detection -----------------------------------


def test_repeated_action_detection():
    """Test detection of abnormal repeated actions on same node."""
    detector = ProblemDetector()

    # Same action repeated 3 times on same node (at threshold)
    trace = [
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "submit_btn", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "submit_btn", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "submit_btn", "status": "success"},
    ]

    problems = detector.detect(trace)

    # Should detect repeated action
    repeated_problems = [p for p in problems if p.type == ProblemType.REPEATED_ACTION]
    assert len(repeated_problems) > 0
    assert repeated_problems[0].severity == ProblemSeverity.WARNING
    assert repeated_problems[0].location == "submit_btn"
    assert repeated_problems[0].evidence["repeat_count"] >= 3


def test_repeated_action_detection_different_nodes():
    """Test that same action on different nodes is OK."""
    detector = ProblemDetector()

    # Same action but on different nodes
    trace = [
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn2", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn3", "status": "success"},
    ]

    problems = detector.detect(trace)

    # Should not detect repeated action (different nodes)
    repeated_problems = [p for p in problems if p.type == ProblemType.REPEATED_ACTION]
    assert len(repeated_problems) == 0


# -- Task 8.6: Test state machine error detection ------------------------------


def test_state_machine_error_detection_final_error():
    """Test detection of final ERROR state."""
    detector = ProblemDetector()

    trace = []  # No trace data needed for this test

    actual_result = {
        "status": "ERROR",
        "error_type": "StateTransitionError",
        "error": "Invalid state transition from COMPLETED to EXECUTING",
    }

    problems = detector.detect(trace, actual_result=actual_result)

    # Should detect state machine error
    error_problems = [p for p in problems if p.type == ProblemType.STATE_MACHINE_ERROR]
    assert len(error_problems) > 0
    assert error_problems[0].severity == ProblemSeverity.CRITICAL
    assert error_problems[0].location == "final_state"
    assert "ERROR" in error_problems[0].description


def test_state_machine_error_detection_invalid_transition():
    """Test detection of invalid state transition."""
    detector = ProblemDetector()

    # COMPLETED should not transition to EXECUTING (terminal state)
    trace = [
        {"node_type": "span", "span_type": "state_transition", "from_state": "IDLE", "to_state": "EXECUTING"},
        {"node_type": "span", "span_type": "state_transition", "from_state": "EXECUTING", "to_state": "COMPLETED"},
        {"node_type": "span", "span_type": "state_transition", "from_state": "COMPLETED", "to_state": "EXECUTING"},
    ]

    problems = detector.detect(trace)

    # Should detect invalid transition
    error_problems = [p for p in problems if p.type == ProblemType.STATE_MACHINE_ERROR]
    assert len(error_problems) > 0
    assert error_problems[0].severity == ProblemSeverity.ERROR
    assert "COMPLETED" in error_problems[0].description
    assert "EXECUTING" in error_problems[0].description


def test_state_machine_error_detection_valid_transitions():
    """Test that valid transitions don't trigger detection."""
    detector = ProblemDetector()

    # All valid transitions (RESULT_VERIFY -> FRAME_COMPLETE -> COMPLETED)
    trace = [
        {"node_type": "span", "span_type": "state_transition", "from_state": "IDLE", "to_state": "EXECUTING"},
        {"node_type": "span", "span_type": "state_transition", "from_state": "EXECUTING", "to_state": "RESULT_VERIFY"},
        {"node_type": "span", "span_type": "state_transition", "from_state": "RESULT_VERIFY", "to_state": "FRAME_COMPLETE"},
        {"node_type": "span", "span_type": "state_transition", "from_state": "FRAME_COMPLETE", "to_state": "COMPLETED"},
    ]

    problems = detector.detect(trace)

    # Should not detect state machine errors
    error_problems = [p for p in problems if p.type == ProblemType.STATE_MACHINE_ERROR]
    assert len(error_problems) == 0


# -- Task 8.7: Test page mismatch detection --------------------------------------


def test_page_mismatch_detection():
    """Test detection of failed page transitions (from == to)."""
    detector = ProblemDetector()

    # Transition stays on same page (indicates failure)
    trace = [
        {
            "node_type": "span",
            "span_type": "page_transition",
            "from_page": "login_page",
            "to_page": "login_page",  # Stayed on same page
            "trigger_element": "submit_btn",
        },
    ]

    problems = detector.detect(trace)

    # Should detect page mismatch
    mismatch_problems = [p for p in problems if p.type == ProblemType.PAGE_MISMATCH]
    assert len(mismatch_problems) > 0
    assert mismatch_problems[0].severity == ProblemSeverity.WARNING
    assert mismatch_problems[0].location == "login_page"
    assert "same page" in mismatch_problems[0].description


def test_page_mismatch_detection_successful_transitions():
    """Test that successful page transitions don't trigger detection."""
    detector = ProblemDetector()

    # Successful transitions (from != to)
    trace = [
        {
            "node_type": "span",
            "span_type": "page_transition",
            "from_page": "login_page",
            "to_page": "home_page",
            "trigger_element": "submit_btn",
        },
        {
            "node_type": "span",
            "span_type": "page_transition",
            "from_page": "home_page",
            "to_page": "settings_page",
            "trigger_element": "settings_btn",
        },
    ]

    problems = detector.detect(trace)

    # Should not detect page mismatches
    mismatch_problems = [p for p in problems if p.type == ProblemType.PAGE_MISMATCH]
    assert len(mismatch_problems) == 0


# -- Task 8.8: Test orphan node detection ---------------------------------------


def test_orphan_node_detection():
    """Test detection of dynamic nodes created but never executed."""
    detector = ProblemDetector()

    # Dynamic node was created but never executed
    trace = [
        {
            "node_type": "span",
            "span_type": "dynamic_lifecycle",
            "event": "created",
            "node_id": "dynamic_btn_123",
            "parent_id": "root",
        },
        {
            "node_type": "span",
            "span_type": "dynamic_lifecycle",
            "event": "matched",
            "node_id": "dynamic_btn_123",
            "parent_id": "root",
        },
        # No "executed" event - this is an orphan
    ]

    problems = detector.detect(trace)

    # Should detect orphan node
    orphan_problems = [p for p in problems if p.type == ProblemType.ORPHAN_NODE]
    assert len(orphan_problems) > 0
    assert orphan_problems[0].severity == ProblemSeverity.WARNING
    assert orphan_problems[0].location == "dynamic_btn_123"
    assert "never executed" in orphan_problems[0].description


def test_orphan_node_detection_executed_nodes():
    """Test that executed dynamic nodes don't trigger detection."""
    detector = ProblemDetector()

    # Dynamic node was created, matched, and executed
    trace = [
        {
            "node_type": "span",
            "span_type": "dynamic_lifecycle",
            "event": "created",
            "node_id": "dynamic_btn_123",
            "parent_id": "root",
        },
        {
            "node_type": "span",
            "span_type": "dynamic_lifecycle",
            "event": "matched",
            "node_id": "dynamic_btn_123",
            "parent_id": "root",
        },
        {
            "node_type": "span",
            "span_type": "dynamic_lifecycle",
            "event": "executed",
            "node_id": "dynamic_btn_123",
            "parent_id": "root",
        },
    ]

    problems = detector.detect(trace)

    # Should not detect orphan nodes
    orphan_problems = [p for p in problems if p.type == ProblemType.ORPHAN_NODE]
    assert len(orphan_problems) == 0


# -- Task 8.9: Test sensitivity level configuration -----------------------------


def test_sensitivity_level_low():
    """Test that low sensitivity doubles the thresholds."""
    config = ProblemDetectorConfig(
        max_action_repeats=3,
        max_loop_depth=5,
        loop_detection_sensitivity=SensitivityLevel.LOW,
    )
    detector = ProblemDetector(config)

    # Low sensitivity should double thresholds
    assert detector._effective_max_repeats == 6  # 3 * 2
    assert detector._effective_max_loop_depth == 10  # 5 * 2

    # Create trace with 4 repeats (would trigger at default, but not with low)
    trace = [
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
    ]

    problems = detector.detect(trace)

    # Should NOT detect infinite loop (4 < 6)
    infinite_loop_problems = [p for p in problems if p.type == ProblemType.INFINITE_LOOP]
    assert len(infinite_loop_problems) == 0


def test_sensitivity_level_high():
    """Test that high sensitivity halves the thresholds."""
    config = ProblemDetectorConfig(
        max_action_repeats=4,
        max_loop_depth=6,
        loop_detection_sensitivity=SensitivityLevel.HIGH,
    )
    detector = ProblemDetector(config)

    # High sensitivity should halve thresholds (with minimums)
    assert detector._effective_max_repeats == 2  # max(1, 4 // 2)
    assert detector._effective_max_loop_depth == 3  # max(2, 6 // 2)

    # Create trace with 2 repeats (would not trigger at default, but does with high)
    trace = [
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
    ]

    problems = detector.detect(trace)

    # Should detect repeated action (3 >= 2)
    repeated_problems = [p for p in problems if p.type == ProblemType.REPEATED_ACTION]
    assert len(repeated_problems) > 0


def test_sensitivity_level_medium():
    """Test that medium sensitivity uses default thresholds."""
    config = ProblemDetectorConfig(
        max_action_repeats=3,
        max_loop_depth=5,
        loop_detection_sensitivity=SensitivityLevel.MEDIUM,
    )
    detector = ProblemDetector(config)

    # Medium sensitivity should use thresholds as-is
    assert detector._effective_max_repeats == 3
    assert detector._effective_max_loop_depth == 5


# -- Task 8.10: Test feature toggles -------------------------------------------


def test_feature_toggle_disable_infinite_loop_detection():
    """Test disabling infinite loop detection."""
    config = ProblemDetectorConfig(enable_infinite_loop_detection=False)
    detector = ProblemDetector(config)

    # Create trace with obvious infinite loop
    trace = [
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
    ]

    problems = detector.detect(trace)

    # Should NOT detect infinite loop (feature disabled)
    infinite_loop_problems = [p for p in problems if p.type == ProblemType.INFINITE_LOOP]
    assert len(infinite_loop_problems) == 0


def test_feature_toggle_enable_all_features():
    """Test that all features work when enabled."""
    config = ProblemDetectorConfig(
        enable_infinite_loop_detection=True,
        enable_repeated_action_detection=True,
        enable_unvisited_node_detection=True,
        enable_state_machine_error_detection=True,
        enable_page_mismatch_detection=True,
        enable_orphan_node_detection=True,
    )
    detector = ProblemDetector(config)

    # Create trace that should trigger all detection types
    trace = [
        # Repeated actions (infinite loop and repeated action)
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        # Invalid state transition
        {"node_type": "span", "span_type": "state_transition", "from_state": "COMPLETED", "to_state": "EXECUTING"},
        # Page mismatch
        {
            "node_type": "span",
            "span_type": "page_transition",
            "from_page": "page1",
            "to_page": "page1",
            "trigger_element": "btn1",
        },
        # Orphan dynamic node
        {"node_type": "span", "span_type": "dynamic_lifecycle", "event": "created", "node_id": "dyn1"},
    ]

    expected_nodes = {"root", "btn1", "btn2"}  # btn2 not visited
    actual_result = {"status": "ERROR", "error_type": "TestError"}

    problems = detector.detect(trace, expected_nodes=expected_nodes, actual_result=actual_result)

    # Should detect multiple problem types
    problem_types = {p.type for p in problems}
    assert len(problem_types) >= 3  # At least 3 different types


def test_feature_toggle_partial_disable():
    """Test that disabling some features doesn't affect others."""
    config = ProblemDetectorConfig(
        enable_infinite_loop_detection=False,  # Disabled
        enable_repeated_action_detection=True,  # Enabled
        enable_page_mismatch_detection=True,  # Enabled
    )
    detector = ProblemDetector(config)

    trace = [
        # Repeated actions
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        # Page mismatch
        {
            "node_type": "span",
            "span_type": "page_transition",
            "from_page": "page1",
            "to_page": "page1",
            "trigger_element": "btn1",
        },
    ]

    problems = detector.detect(trace)

    # Should NOT detect infinite loop (disabled)
    infinite_loop_problems = [p for p in problems if p.type == ProblemType.INFINITE_LOOP]
    assert len(infinite_loop_problems) == 0

    # Should detect repeated action (enabled)
    repeated_problems = [p for p in problems if p.type == ProblemType.REPEATED_ACTION]
    assert len(repeated_problems) > 0

    # Should detect page mismatch (enabled)
    mismatch_problems = [p for p in problems if p.type == ProblemType.PAGE_MISMATCH]
    assert len(mismatch_problems) > 0


# -- Helper methods tests --------------------------------------------------------


def test_problem_serialization():
    """Test Problem.to_dict() method."""
    problem = Problem(
        type=ProblemType.INFINITE_LOOP,
        description="Test infinite loop",
        severity=ProblemSeverity.CRITICAL,
        location="btn1",
        evidence={"repeat_count": 4},
        hint="Check element accessibility",
    )

    result = problem.to_dict()

    assert result["type"] == "infinite_loop"
    assert result["description"] == "Test infinite loop"
    assert result["severity"] == "critical"
    assert result["location"] == "btn1"
    assert result["evidence"]["repeat_count"] == 4
    assert result["hint"] == "Check element accessibility"


def test_is_valid_transition():
    """Test _is_valid_transition helper method."""
    detector = ProblemDetector()

    # Valid transitions
    assert detector._is_valid_transition("IDLE", "EXECUTING") is True
    assert detector._is_valid_transition("EXECUTING", "RESULT_VERIFY") is True
    assert detector._is_valid_transition("AUTO_ESCAPE", "EXECUTING") is True

    # Invalid transitions
    assert detector._is_valid_transition("COMPLETED", "EXECUTING") is False
    assert detector._is_valid_transition("ERROR", "EXECUTING") is False


def test_find_repeating_patterns_abab():
    """Test _find_repeating_patterns with ABAB pattern."""
    detector = ProblemDetector()

    sequence = ["A", "B", "A", "B", "A", "B"]
    pattern = detector._find_repeating_patterns(sequence)

    assert pattern is not None
    assert pattern == ["A", "B"]


def test_find_repeating_patterns_unique_sequence():
    """Test _find_repeating_patterns with unique sequence."""
    detector = ProblemDetector()

    sequence = ["A", "B", "C", "D", "E", "F"]
    pattern = detector._find_repeating_patterns(sequence)

    assert pattern is None


def test_find_repeating_patterns_short_sequence():
    """Test _find_repeating_patterns with sequence too short."""
    detector = ProblemDetector()

    sequence = ["A", "B"]
    pattern = detector._find_repeating_patterns(sequence)

    assert pattern is None


def test_extract_actions():
    """Test _extract_actions helper method."""
    detector = ProblemDetector()

    trace = [
        {"node_type": "span", "span_type": "execution", "action": "click", "target": "btn1", "status": "success"},
        {"node_type": "span", "span_type": "execution", "action": "swipe", "target": "btn2", "status": "success"},
        {"node_type": "span", "span_type": "other", "action": "back"},  # Should be ignored
    ]

    actions = detector._extract_actions(trace)

    assert len(actions) == 2
    assert actions[0]["action"] == "click"
    assert actions[0]["node_id"] == "btn1"  # target is stored as node_id
    assert actions[1]["action"] == "swipe"
    assert actions[1]["node_id"] == "btn2"  # target is stored as node_id


def test_extract_state_sequence():
    """Test _extract_state_sequence helper method."""
    detector = ProblemDetector()

    trace = [
        {"node_type": "span", "span_type": "state_transition", "from_state": "IDLE", "to_state": "EXECUTING"},
        {"node_type": "span", "span_type": "state_transition", "from_state": "EXECUTING", "to_state": "COMPLETED"},
    ]

    states = detector._extract_state_sequence(trace)

    assert len(states) == 2
    assert states[0] == "EXECUTING"
    assert states[1] == "COMPLETED"


def test_extract_page_transitions():
    """Test _extract_page_transitions helper method."""
    detector = ProblemDetector()

    trace = [
        {
            "node_type": "span",
            "span_type": "page_transition",
            "from_page": "login",
            "to_page": "home",
            "trigger_element": "submit",
        },
    ]

    transitions = detector._extract_page_transitions(trace)

    assert len(transitions) == 1
    assert transitions[0]["from_page"] == "login"
    assert transitions[0]["to_page"] == "home"
    assert transitions[0]["trigger_element"] == "submit"


def test_extract_visited_nodes():
    """Test _extract_visited_nodes helper method."""
    detector = ProblemDetector()

    trace = [
        {"node_type": "step", "node_id": "root"},
        {"node_type": "step", "node_id": "btn1"},
        {"node_type": "step", "node_id": "btn2"},
        {"node_type": "span", "node_id": "btn3"},  # Should be ignored (not step)
    ]

    visited = detector._extract_visited_nodes(trace)

    assert len(visited) == 3
    assert "root" in visited
    assert "btn1" in visited
    assert "btn2" in visited
    assert "btn3" not in visited


def test_extract_dynamic_lifecycle():
    """Test _extract_dynamic_lifecycle helper method."""
    detector = ProblemDetector()

    trace = [
        {
            "node_type": "span",
            "span_type": "dynamic_lifecycle",
            "event": "created",
            "node_id": "dyn1",
            "parent_id": "root",
        },
        {
            "node_type": "span",
            "span_type": "dynamic_lifecycle",
            "event": "executed",
            "node_id": "dyn1",
            "parent_id": "root",
        },
    ]

    events = detector._extract_dynamic_lifecycle(trace)

    assert len(events) == 2
    assert events[0]["event"] == "created"
    assert events[0]["node_id"] == "dyn1"
    assert events[1]["event"] == "executed"
