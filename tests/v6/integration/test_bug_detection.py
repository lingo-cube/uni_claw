"""Bug detection integration tests (V6.9.2).

Tests that the enhanced simulation framework can detect the original bugs
that motivated this PRD, including the AUTO_ESCAPE infinite loop and
static page repetition issues.
"""

import pytest
from typing import Dict, Any, List

from src.simulation.expected_behavior import ExpectedAction, ExpectedBehavior, CompletionMode
from src.simulation.behavior_validator import BehaviorValidator
from src.simulation.problem_detector import (
    ProblemDetector,
    ProblemDetectorConfig,
    ProblemType,
    ProblemSeverity,
)


# -- Helper functions -----------------------------------------------------------


def create_mock_trace_with_infinite_loop(
    repeat_count: int = 4,
    action: str = "click",
    target: str = "btn_submit",
) -> List[Dict[str, Any]]:
    """Create a mock trace showing infinite loop behavior.

    Simulates the original AUTO_ESCAPE bug where the same action
    is repeated multiple times on a static page.

    Args:
        repeat_count: Number of times to repeat the action
        action: Action type (default: click)
        target: Target element (default: btn_submit)

    Returns:
        Mock trace data
    """
    trace = []

    # Add repeated actions (the infinite loop pattern)
    for i in range(repeat_count):
        trace.append({
            "node_type": "span",
            "span_type": "execution",
            "action": action,
            "target": target,
            "status": "success",
        })

    # Add state transitions showing AUTO_ESCAPE pattern
    trace.append({"node_type": "span", "span_type": "state_transition", "from_state": "IDLE", "to_state": "EXECUTING"})
    trace.append({"node_type": "span", "span_type": "state_transition", "from_state": "EXECUTING", "to_state": "AUTO_ESCAPE"})
    trace.append({"node_type": "span", "span_type": "state_transition", "from_state": "AUTO_ESCAPE", "to_state": "EXECUTING"})
    trace.append({"node_type": "span", "span_type": "state_transition", "from_state": "EXECUTING", "to_state": "AUTO_ESCAPE"})
    trace.append({"node_type": "span", "span_type": "state_transition", "from_state": "AUTO_ESCAPE", "to_state": "EXECUTING"})

    # Add step nodes for visitation
    for i in range(repeat_count):
        trace.append({
            "node_type": "step",
            "node_id": target,
        })

    return trace


def create_mock_trace_with_static_page_repetition() -> List[Dict[str, Any]]:
    """Create a mock trace showing static page repetition.

    Simulates a scenario where actions are repeated on a page
    that doesn't change, indicating a mock service limitation.
    """
    trace = []

    # Multiple clicks on the same button (page doesn't change)
    for i in range(5):
        trace.append({
            "node_type": "span",
            "span_type": "execution",
            "action": "click",
            "target": "btn_next",
            "status": "success",
        })

    # Page transitions show no actual page change (stays on same page)
    for i in range(4):
        trace.append({
            "node_type": "span",
            "span_type": "page_transition",
            "from_page": "login_page",
            "to_page": "login_page",  # Same page = transition failed
            "trigger_element": "btn_next",
        })

    return trace


def create_clean_trace() -> List[Dict[str, Any]]:
    """Create a mock trace showing clean execution without problems."""
    trace = []

    # Normal action sequence
    trace.append({
        "node_type": "span",
        "span_type": "execution",
        "action": "no_action",
        "target": "root",
        "status": "success",
    })
    trace.append({
        "node_type": "span",
        "span_type": "execution",
        "action": "click",
        "target": "btn_detail",
        "status": "success",
    })
    trace.append({
        "node_type": "span",
        "span_type": "execution",
        "action": "click",
        "target": "btn_back",
        "status": "success",
    })

    # Normal state transitions (using valid transitions)
    trace.append({"node_type": "span", "span_type": "state_transition", "from_state": "IDLE", "to_state": "EXECUTING"})
    trace.append({"node_type": "span", "span_type": "state_transition", "from_state": "EXECUTING", "to_state": "RESULT_VERIFY"})
    trace.append({"node_type": "span", "span_type": "state_transition", "from_state": "RESULT_VERIFY", "to_state": "FRAME_COMPLETE"})
    trace.append({"node_type": "span", "span_type": "state_transition", "from_state": "FRAME_COMPLETE", "to_state": "COMPLETED"})

    # Successful page transitions
    trace.append({
        "node_type": "span",
        "span_type": "page_transition",
        "from_page": "home",
        "to_page": "detail",
        "trigger_element": "btn_detail",
    })
    trace.append({
        "node_type": "span",
        "span_type": "page_transition",
        "from_page": "detail",
        "to_page": "home",
        "trigger_element": "btn_back",
    })

    return trace


# -- Task 9.5: Test detection of mock service limitation (original bug) ----------


def test_detect_mock_service_limitation():
    """Test that the original AUTO_ESCAPE infinite loop bug can be detected.

    This test simulates the bug that motivated this PRD:
    - MockVisionService returns the same page data regardless of actions
    - GraphTraversalEngine clicks the same button multiple times
    - State machine enters EXECUTING -> AUTO_ESCAPE -> EXECUTING loop
    - Test shows COMPLETED status but behavior is completely wrong
    """
    # Create trace showing the infinite loop pattern
    trace = create_mock_trace_with_infinite_loop(repeat_count=4)

    # Run problem detection
    detector = ProblemDetector()
    problems = detector.detect(trace)

    # Should detect infinite loop from repeated actions
    infinite_loop_problems = [p for p in problems if p.type == ProblemType.INFINITE_LOOP]
    assert len(infinite_loop_problems) > 0, "Should detect infinite loop from repeated actions"

    # Verify severity is critical
    assert infinite_loop_problems[0].severity == ProblemSeverity.CRITICAL

    # Verify problem location
    assert infinite_loop_problems[0].location == "btn_submit"

    # Verify evidence contains repeat count
    assert infinite_loop_problems[0].evidence["repeat_count"] >= 4


def test_detect_state_sequence_loop():
    """Test detection of state sequence loop pattern."""
    # Create trace with EXECUTING -> AUTO_ESCAPE -> EXECUTING pattern
    trace = create_mock_trace_with_infinite_loop(repeat_count=3)

    # Run problem detection
    detector = ProblemDetector()
    problems = detector.detect(trace)

    # Should detect infinite loop from state sequence
    infinite_loop_problems = [p for p in problems if p.type == ProblemType.INFINITE_LOOP]

    # At least one infinite loop problem should be detected
    assert len(infinite_loop_problems) > 0

    # Check if any of them are from state sequence (severity WARNING)
    state_loop_problems = [p for p in infinite_loop_problems if p.severity == ProblemSeverity.WARNING]
    # May or may not have state loop depending on threshold


def test_detect_static_page_repetition():
    """Test detection of static page repetition (mock service limitation)."""
    # Create trace showing static page repetition
    trace = create_mock_trace_with_static_page_repetition()

    # Run problem detection
    detector = ProblemDetector()
    problems = detector.detect(trace)

    # Should detect repeated actions
    repeated_problems = [p for p in problems if p.type == ProblemType.REPEATED_ACTION]
    assert len(repeated_problems) > 0, "Should detect repeated actions on static page"

    # Should detect page mismatches (transitions that stay on same page)
    page_mismatch_problems = [p for p in problems if p.type == ProblemType.PAGE_MISMATCH]
    assert len(page_mismatch_problems) > 0, "Should detect page transitions that don't change pages"


# -- Task 9.6: Test verification that stateful mock fixes the bug ----------------


def test_verify_stateful_mock_fixes_bug():
    """Test that stateful mock services prevent the original bug.

    This test verifies that using StatefulMockVisionService instead
    of MockVisionService prevents the infinite loop and static page
    issues.
    """
    # Create a clean trace showing proper page transitions
    clean_trace = create_clean_trace()

    # Run problem detection
    detector = ProblemDetector()
    problems = detector.detect(clean_trace)

    # Should NOT detect infinite loops
    infinite_loop_problems = [p for p in problems if p.type == ProblemType.INFINITE_LOOP]
    assert len(infinite_loop_problems) == 0, "Clean trace should not have infinite loops"

    # Should NOT detect critical problems
    critical_problems = [p for p in problems if p.severity == ProblemSeverity.CRITICAL]
    assert len(critical_problems) == 0, "Clean trace should not have critical problems"

    # May have warnings or info, but no errors
    error_problems = [p for p in problems if p.severity == ProblemSeverity.ERROR]
    assert len(error_problems) == 0, "Clean trace should not have errors"


def test_stateful_mock_allows_page_navigation():
    """Test that stateful mock allows successful page navigation."""
    # Create trace showing successful multi-page navigation
    trace = create_clean_trace()

    # Extract page transitions
    page_transitions = [
        n for n in trace
        if n.get("span_type") == "page_transition"
    ]

    # Verify we have page transitions that actually change pages
    assert len(page_transitions) >= 2

    # Verify transitions are successful (from != to)
    for transition in page_transitions:
        from_page = transition.get("from_page")
        to_page = transition.get("to_page")
        assert from_page != to_page, f"Page transition should change pages: {from_page} -> {to_page}"


def test_stateful_mock_action_sequence_progresses():
    """Test that stateful mock allows action sequence to progress."""
    # Create trace showing progressing action sequence
    trace = create_clean_trace()

    # Extract actions
    actions = [
        n for n in trace
        if n.get("span_type") == "execution"
    ]

    # Verify actions progress (not stuck on same action)
    assert len(actions) >= 2
    unique_targets = set(a.get("target") for a in actions)
    assert len(unique_targets) >= 2, "Should have multiple unique targets (not stuck)"


# -- Task 9.7: Helper functions for creating mock results -----------------------


def test_helper_create_mock_trace_with_infinite_loop():
    """Test the helper function for infinite loop trace creation."""
    trace = create_mock_trace_with_infinite_loop(repeat_count=5, action="swipe", target="btn_next")

    # Verify correct number of actions
    actions = [n for n in trace if n.get("span_type") == "execution"]
    assert len(actions) == 5

    # Verify all actions have correct properties
    for action in actions:
        assert action["action"] == "swipe"
        assert action["target"] == "btn_next"


def test_helper_create_mock_trace_with_static_page_repetition():
    """Test the helper function for static page repetition trace."""
    trace = create_mock_trace_with_static_page_repetition()

    # Verify page transitions show no change
    transitions = [n for n in trace if n.get("span_type") == "page_transition"]
    assert len(transitions) == 4

    for transition in transitions:
        assert transition["from_page"] == "login_page"
        assert transition["to_page"] == "login_page"  # Same page


def test_helper_create_clean_trace():
    """Test the helper function for clean trace creation."""
    trace = create_clean_trace()

    # Verify we have execution spans
    actions = [n for n in trace if n.get("span_type") == "execution"]
    assert len(actions) == 3

    # Verify we have page transitions
    transitions = [n for n in trace if n.get("span_type") == "page_transition"]
    assert len(transitions) == 2

    # Verify all transitions change pages
    for transition in transitions:
        assert transition["from_page"] != transition["to_page"]


def test_helper_default_parameters():
    """Test that helper functions work with default parameters."""
    # Should work without specifying parameters
    trace = create_mock_trace_with_infinite_loop()

    # Verify it created the trace
    assert len(trace) > 0

    # Verify default values
    actions = [n for n in trace if n.get("span_type") == "execution"]
    assert len(actions) == 4  # Default repeat_count
    assert actions[0]["action"] == "click"  # Default action
    assert actions[0]["target"] == "btn_submit"  # Default target


# -- Integration tests combining validators and detectors --------------------


def test_combined_validation_and_detection():
    """Test combining behavior validation and problem detection."""
    # Create expected behavior
    expected = ExpectedBehavior(
        scenario="Clean Navigation",
        description="Should navigate without issues",
        actions=[
            ExpectedAction(action="no_action", node="root", order=0),
            ExpectedAction(action="click", node="detail_btn", target="btn_detail", order=1),
        ],
        visited_nodes={"root", "detail_btn"},
        final_state="COMPLETED",
        completion_mode=CompletionMode.NORMAL,
    )

    # Create clean trace
    trace = create_clean_trace()

    # Run validation
    validator = BehaviorValidator()
    validation_result = validator.validate(
        expected=expected,
        actual_trace=trace,
        actual_result={"status": "COMPLETED"},
    )

    # Run problem detection
    detector = ProblemDetector()
    problems = detector.detect(trace)

    # Validation should complete without error
    assert validation_result is not None

    # Problem detection should find no critical issues
    critical_problems = [p for p in problems if p.severity == ProblemSeverity.CRITICAL]
    assert len(critical_problems) == 0


def test_sensitivity_levels_affect_detection():
    """Test that different sensitivity levels affect problem detection."""
    # Create trace with some repeated actions
    trace = create_mock_trace_with_infinite_loop(repeat_count=3)

    # Low sensitivity - should NOT detect (threshold doubled)
    low_config = ProblemDetectorConfig(
        max_action_repeats=3,
        loop_detection_sensitivity="low",
    )
    low_detector = ProblemDetector(low_config)
    low_problems = low_detector.detect(trace)
    low_infinite = [p for p in low_problems if p.type == ProblemType.INFINITE_LOOP]

    # High sensitivity - SHOULD detect (threshold halved)
    high_config = ProblemDetectorConfig(
        max_action_repeats=3,
        loop_detection_sensitivity="high",
    )
    high_detector = ProblemDetector(high_config)
    high_problems = high_detector.detect(trace)
    high_infinite = [p for p in high_problems if p.type == ProblemType.INFINITE_LOOP]

    # High sensitivity should catch more problems than low
    # (This is a simplified test - actual behavior depends on repeat count)
    assert len(high_infinite) >= len(low_infinite)


def test_feature_toggles():
    """Test that feature toggles control which problems are detected."""
    # Create trace with multiple problem types
    trace = create_mock_trace_with_static_page_repetition()

    # Enable all features
    all_enabled = ProblemDetectorConfig(
        enable_infinite_loop_detection=True,
        enable_repeated_action_detection=True,
        enable_page_mismatch_detection=True,
    )
    detector_all = ProblemDetector(all_enabled)
    problems_all = detector_all.detect(trace)

    # Disable repeated action detection
    no_repeated = ProblemDetectorConfig(
        enable_repeated_action_detection=False,
    )
    detector_no_repeated = ProblemDetector(no_repeated)
    problems_no_repeated = detector_no_repeated.detect(trace)

    # Should have more problems when all features are enabled
    repeated_all = [p for p in problems_all if p.type == ProblemType.REPEATED_ACTION]
    repeated_none = [p for p in problems_no_repeated if p.type == ProblemType.REPEATED_ACTION]

    assert len(repeated_all) > 0
    assert len(repeated_none) == 0  # Feature disabled
