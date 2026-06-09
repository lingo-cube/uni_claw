"""
Unit tests for scroll simulation data models.

Tests cover:
- ScrollSegment creation and serialization
- ScrollState initialization, progress tracking, history, and fault injection
- ScrollAction creation and serialization for down/up scrolls
- ScrollPage aggregation and serialization
"""

import time
from unittest.mock import patch

import pytest

from src.simulation.scroll.models import ScrollAction, ScrollPage, ScrollSegment, ScrollState


class TestScrollSegment:
    """Tests for ScrollSegment dataclass."""

    def test_scroll_segment_creation_with_threshold_and_elements(self):
        """WHEN ScrollSegment is created with threshold=0.5 and elements
        THEN segment.threshold equals 0.5 and segment.elements contains one element
        """
        element = {"id": "item1", "name": "Item 1"}
        segment = ScrollSegment(threshold=0.5, elements=[element])

        assert segment.threshold == 0.5
        assert len(segment.elements) == 1
        assert segment.elements[0]["id"] == "item1"

    def test_scroll_segment_default_elements(self):
        """WHEN ScrollSegment is created without elements
        THEN elements defaults to empty list
        """
        segment = ScrollSegment(threshold=0.0)

        assert segment.elements == []

    def test_scroll_segment_to_dict(self):
        """WHEN to_dict() is called on ScrollSegment
        THEN returns dictionary with threshold and elements keys
        """
        segment = ScrollSegment(threshold=0.3, elements=[{"id": "item1"}])
        result = segment.to_dict()

        assert "threshold" in result
        assert "elements" in result
        assert result["threshold"] == 0.3
        assert len(result["elements"]) == 1

    def test_scroll_segment_multiple_elements(self):
        """WHEN ScrollSegment contains multiple elements
        THEN all elements are preserved in order
        """
        elements = [{"id": f"item{i}"} for i in range(3)]
        segment = ScrollSegment(threshold=0.5, elements=elements)

        assert len(segment.elements) == 3
        assert segment.elements[0]["id"] == "item0"
        assert segment.elements[2]["id"] == "item2"


class TestScrollState:
    """Tests for ScrollState dataclass."""

    def test_scroll_state_initialization(self):
        """WHEN ScrollState is created without parameters
        THEN current_progress is 0.0, scroll_count is 0, scroll_history is empty list
        """
        state = ScrollState()

        assert state.current_progress == 0.0
        assert state.scroll_count == 0
        assert state.scroll_history == []
        assert state.last_scroll_time is None

    def test_scroll_state_progress_updates(self):
        """WHEN current_progress is updated from 0.0 to 0.5
        THEN progress is stored and can be retrieved
        """
        state = ScrollState()
        state.current_progress = 0.5

        assert state.current_progress == 0.5

    def test_scroll_state_records_scroll_history(self):
        """WHEN scroll progresses through 0.3, 0.6, 0.9
        THEN scroll_history contains [0.3, 0.6, 0.9]
        """
        state = ScrollState()
        state.scroll_history = [0.3, 0.6, 0.9]

        assert state.scroll_history == [0.3, 0.6, 0.9]
        assert len(state.scroll_history) == 3

    def test_scroll_state_fault_injection_fail_next_scroll(self):
        """WHEN fail_next_scroll is set to True
        THEN flag indicates next scroll should fail
        """
        state = ScrollState()
        state.fail_next_scroll = True

        assert state.fail_next_scroll is True

    def test_scroll_state_delay_field(self):
        """WHEN simulate_delay_ms is set to 500
        THEN delay value is stored for use during scroll operations
        """
        state = ScrollState()
        state.simulate_delay_ms = 500

        assert state.simulate_delay_ms == 500

    def test_scroll_state_to_dict(self):
        """WHEN to_dict() is called on ScrollState
        THEN returns dictionary with all state fields
        """
        state = ScrollState(
            current_progress=0.5,
            scroll_count=3,
            scroll_history=[0.3, 0.5],
            fail_next_scroll=True,
            simulate_delay_ms=100,
        )
        result = state.to_dict()

        assert result["current_progress"] == 0.5
        assert result["scroll_count"] == 3
        assert len(result["scroll_history"]) == 2
        assert result["fail_next_scroll"] is True
        assert result["simulate_delay_ms"] == 100

    def test_scroll_state_increment_count(self):
        """WHEN scroll_count is incremented multiple times
        THEN count reflects total number of scrolls
        """
        state = ScrollState()
        state.scroll_count += 1
        state.scroll_count += 1
        state.scroll_count += 1

        assert state.scroll_count == 3

    def test_scroll_state_progress_bounds_clamping(self):
        """WHEN scroll progress is set outside [0.0, 1.0] range
        THEN value is stored (application should clamp before setting)
        """
        state = ScrollState()

        # Store values directly (application layer should clamp)
        state.current_progress = -0.1
        assert state.current_progress == -0.1

        state.current_progress = 1.5
        assert state.current_progress == 1.5


class TestScrollAction:
    """Tests for ScrollAction dataclass."""

    def test_scroll_action_for_down_scroll(self):
        """WHEN ScrollAction is created with action="DOWN", before_progress=0.0, after_progress=0.3
        THEN all fields are populated with provided values
        """
        action = ScrollAction(
            action="DOWN",
            path="wifi_list",
            step_percent=0.3,
            before_progress=0.0,
            after_progress=0.3,
            timestamp=1234567890.0,
        )

        assert action.action == "DOWN"
        assert action.path == "wifi_list"
        assert action.step_percent == 0.3
        assert action.before_progress == 0.0
        assert action.after_progress == 0.3
        assert action.timestamp == 1234567890.0

    def test_scroll_action_for_up_scroll(self):
        """WHEN ScrollAction is created with action="UP", before_progress=0.6, after_progress=0.3
        THEN after_progress is less than before_progress
        """
        action = ScrollAction(
            action="UP",
            path="settings_list",
            step_percent=0.3,
            before_progress=0.6,
            after_progress=0.3,
            timestamp=1234567890.0,
        )

        assert action.action == "UP"
        assert action.after_progress < action.before_progress
        assert action.step_percent == 0.3

    def test_scroll_action_to_dict(self):
        """WHEN to_dict() is called on ScrollAction
        THEN returns dictionary with all action fields
        """
        action = ScrollAction(
            action="DOWN",
            path="test_page",
            step_percent=0.2,
            before_progress=0.1,
            after_progress=0.3,
            timestamp=999999.0,
        )
        result = action.to_dict()

        assert result["action"] == "DOWN"
        assert result["path"] == "test_page"
        assert result["step_percent"] == 0.2
        assert result["before_progress"] == 0.1
        assert result["after_progress"] == 0.3
        assert result["timestamp"] == 999999.0

    def test_scroll_action_with_current_timestamp(self):
        """WHEN ScrollAction is created with time.time() for timestamp
        THEN timestamp represents current time
        """
        current_time = time.time()
        action = ScrollAction(
            action="DOWN",
            path="page",
            step_percent=0.1,
            before_progress=0.0,
            after_progress=0.1,
            timestamp=current_time,
        )

        assert action.timestamp == current_time
        assert action.timestamp > 0


class TestScrollPage:
    """Tests for ScrollPage dataclass."""

    def test_scroll_page_with_multiple_segments(self):
        """WHEN ScrollPage is created with three scroll segments at thresholds 0.0, 0.5, 1.0
        THEN scroll_segments list contains all three segments in order
        """
        segments = [
            ScrollSegment(threshold=0.0, elements=[{"id": "item1"}]),
            ScrollSegment(threshold=0.5, elements=[{"id": "item2"}]),
            ScrollSegment(threshold=1.0, elements=[{"id": "item3"}]),
        ]
        page = ScrollPage(path="wifi_list", has_scroll=True, scroll_segments=segments)

        assert page.path == "wifi_list"
        assert page.has_scroll is True
        assert len(page.scroll_segments) == 3
        assert page.scroll_segments[0].threshold == 0.0
        assert page.scroll_segments[1].threshold == 0.5
        assert page.scroll_segments[2].threshold == 1.0

    def test_scroll_page_default_segments(self):
        """WHEN ScrollPage is created without scroll_segments
        THEN scroll_segments defaults to empty list
        """
        page = ScrollPage(path="single_page", has_scroll=False)

        assert page.scroll_segments == []
        assert page.has_scroll is False

    def test_scroll_page_to_dict(self):
        """WHEN to_dict() is called on ScrollPage
        THEN returns dictionary with path, has_scroll, and scroll_segments keys
        """
        segments = [
            ScrollSegment(threshold=0.0, elements=[{"id": "item1"}]),
            ScrollSegment(threshold=1.0, elements=[{"id": "item2"}]),
        ]
        page = ScrollPage(path="test_list", has_scroll=True, scroll_segments=segments)
        result = page.to_dict()

        assert result["path"] == "test_list"
        assert result["has_scroll"] is True
        assert len(result["scroll_segments"]) == 2
        assert result["scroll_segments"][0]["threshold"] == 0.0
        assert result["scroll_segments"][1]["threshold"] == 1.0

    def test_scroll_page_empty_segment_list(self):
        """WHEN ScrollPage has_scroll=False but contains empty segments list
        THEN has_scroll correctly indicates no scroll capability
        """
        page = ScrollPage(path="static_page", has_scroll=False, scroll_segments=[])

        assert page.has_scroll is False
        assert len(page.scroll_segments) == 0
