"""
Unit tests for TraceAsserter component.

Tests trace comparison logic, assertion methods,
and validation functionality.
"""

import pytest
from tests.simulation.helpers.assertions import (
    TraceAsserter,
    AssertionResult
)


class TestTraceAsserter:
    """Test suite for TraceAsserter component."""

    @pytest.fixture
    def sample_trace(self):
        """Create sample trace for testing."""
        return [
            {
                "action_type": "enter",
                "current_node": "root",
                "target_info": {},
                "timestamp": 1.0
            },
            {
                "action_type": "click",
                "current_node": "root",
                "target_info": {"element_id": "SettingsButton", "text": "Settings"},
                "timestamp": 1.1
            },
            {
                "action_type": "enter",
                "current_node": "settings",
                "target_info": {},
                "timestamp": 1.2
            },
            {
                "action_type": "scroll",
                "current_node": "settings",
                "target_info": {"direction": "down", "distance": 1},
                "timestamp": 1.3
            },
            {
                "action_type": "go_back",
                "current_node": "settings",
                "target_info": {},
                "timestamp": 1.4,
                "completion_reason": "completed"
            }
        ]

    def test_step_to_nl_enter(self):
        """Test natural language conversion for enter actions."""
        step = {
            "action_type": "enter",
            "current_node": "settings",
            "target_info": {}
        }
        result = TraceAsserter.step_to_nl(step)
        assert result == "进入 settings"

    def test_step_to_nl_click(self):
        """Test natural language conversion for click actions."""
        step = {
            "action_type": "click",
            "current_node": "root",
            "target_info": {"element_id": "SettingsButton", "text": "Settings"}
        }
        result = TraceAsserter.step_to_nl(step)
        assert "点击 SettingsButton" in result

    def test_step_to_nl_scroll(self):
        """Test natural language conversion for scroll actions."""
        step = {
            "action_type": "scroll",
            "current_node": "settings",
            "target_info": {"direction": "down", "distance": 1}
        }
        result = TraceAsserter.step_to_nl(step)
        assert result == "滑动 settings"

    def test_step_to_nl_go_back(self):
        """Test natural language conversion for go_back actions."""
        step = {
            "action_type": "go_back",
            "current_node": "settings",
            "target_info": {}
        }
        result = TraceAsserter.step_to_nl(step)
        assert result == "返回上一级"

    def test_step_to_nl_unknown_action(self):
        """Test natural language conversion for unknown actions."""
        step = {
            "action_type": "unknown_action",
            "current_node": "test",
            "target_info": {}
        }
        result = TraceAsserter.step_to_nl(step)
        assert result == "unknown_action test"

    def test_is_subsequence_positive(self):
        """Test subsequence check with matching sequence."""
        expected = ["进入 root", "点击 Settings", "进入 settings"]
        actual = [
            "进入 root",
            "点击 Settings",
            "点击 Other",
            "进入 settings",
            "返回上一级"
        ]
        result = TraceAsserter.is_subsequence(expected, actual)
        assert result is True

    def test_is_subsequence_negative(self):
        """Test subsequence check with non-matching sequence."""
        expected = ["进入 root", "点击 Settings", "进入 settings"]
        actual = [
            "进入 root",
            "点击 Other",
            "返回上一级"
        ]
        result = TraceAsserter.is_subsequence(expected, actual)
        assert result is False

    def test_is_subsequence_empty_expected(self):
        """Test subsequence check with empty expected sequence."""
        expected = []
        actual = ["进入 root", "点击 Settings"]
        result = TraceAsserter.is_subsequence(expected, actual)
        assert result is True

    def test_assert_trace_matches_expected_success(self, sample_trace):
        """Test successful trace assertion."""
        expected = {
            "key_events": [
                "进入 root",
                "点击 SettingsButton",
                "滑动 settings"
            ],
            "total_steps_min": 3,
            "total_steps_max": 10
        }

        result = TraceAsserter.assert_trace_matches_expected(sample_trace, expected)

        assert result.success is True
        assert result.key_events_matched == 3
        assert len(result.missing_events) == 0
        assert result.steps_valid is True

    def test_assert_trace_matches_expected_missing_events(self, sample_trace):
        """Test trace assertion with missing events."""
        expected = {
            "key_events": [
                "进入 root",
                "点击 SettingsButton",
                "进入 settings",
                "点击 NonExistent"
            ],
            "total_steps_min": 3,
            "total_steps_max": 10
        }

        result = TraceAsserter.assert_trace_matches_expected(sample_trace, expected)

        assert result.success is False
        assert len(result.missing_events) == 1
        assert "点击 NonExistent" in result.missing_events

    def test_assert_trace_matches_expected_violations(self, sample_trace):
        """Test trace assertion with violation detection."""
        expected = {
            "key_events": ["进入 root"],
            "must_not_contain": ["错误", "崩溃", "异常"],
            "total_steps_min": 1,
            "total_steps_max": 20
        }

        # Add violation to trace
        trace_with_violation = sample_trace.copy()
        trace_with_violation.append({
            "action_type": "error",
            "current_node": "root",
            "target_info": {"error": "系统错误"},
            "timestamp": 1.5,
            "completion_reason": "error"
        })

        result = TraceAsserter.assert_trace_matches_expected(trace_with_violation, expected)

        assert result.success is False
        assert len(result.violations) > 0

    def test_assert_trace_matches_expected_step_count_invalid(self, sample_trace):
        """Test trace assertion with invalid step count."""
        expected = {
            "key_events": ["进入 root"],
            "total_steps_min": 10,
            "total_steps_max": 20
        }

        result = TraceAsserter.assert_trace_matches_expected(sample_trace, expected)

        assert result.success is False
        assert result.steps_valid is False

    def test_assert_trace_matches_expected_completion_reason(self, sample_trace):
        """Test trace assertion with completion reason validation."""
        expected = {
            "key_events": ["进入 root"],
            "completion_reason": "completed",
            "total_steps_min": 1,
            "total_steps_max": 20
        }

        result = TraceAsserter.assert_trace_matches_expected(sample_trace, expected)

        assert result.success is True
        assert result.completion_reason_match is True

    def test_assert_trace_matches_expected_wrong_completion_reason(self, sample_trace):
        """Test trace assertion with wrong completion reason."""
        # Modify last step to have different completion reason
        trace_modified = sample_trace.copy()
        trace_modified[-1]["completion_reason"] = "error"

        expected = {
            "key_events": ["进入 root"],
            "completion_reason": "completed",
            "total_steps_min": 1,
            "total_steps_max": 20
        }

        result = TraceAsserter.assert_trace_matches_expected(trace_modified, expected)

        assert result.success is False
        assert result.completion_reason_match is False

    def test_validate_step_count_valid(self, sample_trace):
        """Test step count validation with valid range."""
        result = TraceAsserter.validate_step_count(sample_trace, min_steps=3, max_steps=10)
        assert result is True

    def test_validate_step_count_too_few(self, sample_trace):
        """Test step count validation with too few steps."""
        result = TraceAsserter.validate_step_count(sample_trace, min_steps=10, max_steps=20)
        assert result is False

    def test_validate_step_count_too_many(self, sample_trace):
        """Test step count validation with too many steps."""
        result = TraceAsserter.validate_step_count(sample_trace, min_steps=1, max_steps=3)
        assert result is False

    def test_validate_step_count_no_max(self, sample_trace):
        """Test step count validation with no maximum."""
        result = TraceAsserter.validate_step_count(sample_trace, min_steps=1, max_steps=None)
        assert result is True

    def test_validate_completion_reason_match(self, sample_trace):
        """Test completion reason validation with match."""
        result = TraceAsserter.validate_completion_reason(sample_trace, "completed")
        assert result is True

    def test_validate_completion_reason_no_match(self, sample_trace):
        """Test completion reason validation without match."""
        result = TraceAsserter.validate_completion_reason(sample_trace, "error")
        assert result is False

    def test_validate_completion_reason_empty_trace(self):
        """Test completion reason validation with empty trace."""
        result = TraceAsserter.validate_completion_reason([], "completed")
        assert result is False

    def test_find_event_pattern(self, sample_trace):
        """Test finding event pattern in trace."""
        pattern = "settings"
        matches = TraceAsserter.find_event_pattern(sample_trace, pattern)

        assert len(matches) == 3  # "进入 settings", "滑动 settings", "go_back settings"

    def test_find_event_pattern_no_matches(self, sample_trace):
        """Test finding event pattern with no matches."""
        pattern = "NonExistent"
        matches = TraceAsserter.find_event_pattern(sample_trace, pattern)

        assert len(matches) == 0

    def test_extract_action_sequence(self, sample_trace):
        """Test extraction of action sequence."""
        actions = TraceAsserter.extract_action_sequence(sample_trace)

        expected_actions = ["enter", "click", "enter", "scroll", "go_back"]
        assert actions == expected_actions

    def test_compute_trace_coverage(self, sample_trace):
        """Test trace coverage computation."""
        expected_actions = ["enter", "click", "scroll", "go_back"]
        coverage = TraceAsserter.compute_trace_coverage(sample_trace, expected_actions)

        assert coverage == 100.0  # All expected actions present

    def test_compute_trace_coverage_partial(self, sample_trace):
        """Test trace coverage computation with partial coverage."""
        expected_actions = ["enter", "click", "scroll", "go_back", "input_text"]
        coverage = TraceAsserter.compute_trace_coverage(sample_trace, expected_actions)

        assert coverage == 80.0  # 4 out of 5 actions present

    def test_compute_trace_coverage_no_expected(self, sample_trace):
        """Test trace coverage computation with no expected actions."""
        expected_actions = []
        coverage = TraceAsserter.compute_trace_coverage(sample_trace, expected_actions)

        assert coverage == 0.0

    def test_assertion_result_details(self, sample_trace):
        """Test that AssertionResult contains correct details."""
        expected = {
            "key_events": ["进入 root", "点击 SettingsButton"],
            "total_steps_min": 1,
            "total_steps_max": 20
        }

        result = TraceAsserter.assert_trace_matches_expected(sample_trace, expected)

        assert "total_steps" in result.details
        assert "matched_key_events" in result.details
        assert "matched_percentage" in result.details
        assert result.details["total_steps"] == len(sample_trace)