"""
Integration tests for ScrollableMockVisionService.

Tests cover:
- Basic functionality (initialization, scroll simulation, element visibility)
- Fault injection (delay, unresponsiveness)
- Accumulation mode element visibility
- Element deduplication
- History tracking
- Edge cases
"""

import tempfile
import time
from unittest.mock import patch

import pytest

from src.simulation.scroll.models import ScrollPage, ScrollSegment
from src.simulation.scroll.scroll_data_store import ScrollDataStore
from src.simulation.scroll.scrollable_mock_vision import ScrollableMockVisionService
from src.simulation.state_fixture import PageElement, PageState, StateFixture


class TestBasicFunctionality:
    """Tests for basic ScrollableMockVisionService functionality."""

    @pytest.fixture
    def vision_service(self):
        """Create vision service with WiFi list data."""
        # Create scroll segments
        segments = [
            ScrollSegment(
                threshold=0.0,
                elements=[
                    {"id": "net1", "text": "Network1", "type": "button"},
                    {"id": "net2", "text": "Network2", "type": "button"},
                ],
            ),
            ScrollSegment(
                threshold=0.5,
                elements=[
                    {"id": "net3", "text": "Network3", "type": "button"},
                    {"id": "net4", "text": "Network4", "type": "button"},
                ],
            ),
            ScrollSegment(
                threshold=1.0,
                elements=[{"id": "net5", "text": "Network5", "type": "button"}],
            ),
        ]

        # Create scroll page
        page = ScrollPage(path="wifi_list", has_scroll=True, scroll_segments=segments)

        # Create data store
        data_store = ScrollDataStore()
        data_store.add_page(page)

        # Create fixture
        fixture = StateFixture(
            pages={"wifi_list": PageState(id="wifi_list", page_name="WiFi List", elements=[])},
            transitions=[],
            initial_page_id="wifi_list",
        )

        return ScrollableMockVisionService(fixture=fixture, data_store=data_store)

    def test_initial_scroll_progress(self, vision_service):
        """WHEN service is initialized
        THEN scroll progress starts at 0.0
        """
        progress = vision_service.get_scroll_progress()
        assert progress == 0.0

    def test_simulate_scroll_down(self, vision_service):
        """WHEN simulate_scroll is called with positive delta
        THEN progress increases and is clamped to [0.0, 1.0]
        """
        result = vision_service.simulate_scroll(0.3)
        assert result is True
        assert vision_service.get_scroll_progress() == 0.3

        # Another scroll
        result = vision_service.simulate_scroll(0.3)
        assert result is True
        assert vision_service.get_scroll_progress() == 0.6

    def test_simulate_scroll_up(self, vision_service):
        """WHEN simulate_scroll is called with negative delta
        THEN progress decreases
        """
        vision_service.simulate_scroll(0.6)
        assert vision_service.get_scroll_progress() == 0.6

        result = vision_service.simulate_scroll(-0.3)
        assert result is True
        assert vision_service.get_scroll_progress() == 0.3

    def test_scroll_progress_clamping(self, vision_service):
        """WHEN scroll would exceed bounds [0.0, 1.0]
        THEN progress is clamped to valid range
        """
        # Scroll beyond 1.0
        vision_service.simulate_scroll(2.0)
        assert vision_service.get_scroll_progress() == 1.0

        # Reset and scroll below 0.0
        vision_service.reset_scroll_state()
        vision_service.simulate_scroll(-1.0)
        assert vision_service.get_scroll_progress() == 0.0

    def test_analyze_screenshot_initial(self, vision_service):
        """WHEN analyze_screenshot is called at progress 0.0
        THEN returns only elements from threshold 0.0
        """
        analysis = vision_service.analyze_screenshot(b"fake_image")

        assert len(analysis.items) == 2
        assert analysis.items[0].name == "Network1"
        assert analysis.items[1].name == "Network2"
        assert analysis.has_scroll is True
        assert analysis.is_end_of_list is False

    def test_analyze_screenshot_after_scroll(self, vision_service):
        """WHEN analyze_screenshot is called after scrolling
        THEN returns accumulated elements from all segments up to progress
        """
        vision_service.simulate_scroll(0.5)
        analysis = vision_service.analyze_screenshot(b"fake_image")

        assert len(analysis.items) == 4
        item_names = {item.name for item in analysis.items}
        assert "Network1" in item_names
        assert "Network2" in item_names
        assert "Network3" in item_names
        assert "Network4" in item_names

    def test_analyze_screenshot_at_bottom(self, vision_service):
        """WHEN analyze_screenshot is called at progress 1.0
        THEN returns all elements and is_end_of_list is True
        """
        vision_service.simulate_scroll(1.0)
        analysis = vision_service.analyze_screenshot(b"fake_image")

        assert len(analysis.items) == 5
        assert analysis.is_end_of_list is True
        assert analysis.has_scroll is False


class TestAccumulationMode:
    """Tests for accumulation mode element visibility."""

    @pytest.fixture
    def vision_service(self):
        """Create vision service with multiple segments."""
        segments = [
            ScrollSegment(threshold=0.0, elements=[{"id": "item1", "text": "Item1"}]),
            ScrollSegment(threshold=0.3, elements=[{"id": "item2", "text": "Item2"}]),
            ScrollSegment(threshold=0.6, elements=[{"id": "item3", "text": "Item3"}]),
            ScrollSegment(threshold=1.0, elements=[{"id": "item4", "text": "Item4"}]),
        ]

        page = ScrollPage(path="test_list", has_scroll=True, scroll_segments=segments)
        data_store = ScrollDataStore()
        data_store.add_page(page)

        fixture = StateFixture(
            pages={"test_list": PageState(id="test_list", page_name="Test", elements=[])},
            transitions=[],
        initial_page_id="test_list",
        )

        return ScrollableMockVisionService(fixture=fixture, data_store=data_store)

    def test_accumulation_at_0_0(self, vision_service):
        """WHEN progress is 0.0
        THEN only threshold 0.0 elements are visible
        """
        analysis = vision_service.analyze_screenshot(b"")
        assert len(analysis.items) == 1
        assert analysis.items[0].name == "Item1"

    def test_accumulation_at_0_3(self, vision_service):
        """WHEN progress is 0.3
        THEN threshold <= 0.3 elements are visible (accumulated)
        """
        vision_service.simulate_scroll(0.3)
        analysis = vision_service.analyze_screenshot(b"")

        assert len(analysis.items) == 2
        item_names = {item.name for item in analysis.items}
        assert item_names == {"Item1", "Item2"}

    def test_accumulation_at_1_0(self, vision_service):
        """WHEN progress is 1.0
        THEN all elements are visible (accumulated)
        """
        vision_service.simulate_scroll(1.0)
        analysis = vision_service.analyze_screenshot(b"")

        assert len(analysis.items) == 4
        item_names = {item.name for item in analysis.items}
        assert item_names == {"Item1", "Item2", "Item3", "Item4"}


class TestElementDeduplication:
    """Tests for element ID-based deduplication."""

    @pytest.fixture
    def vision_service(self):
        """Create vision service with duplicate elements."""
        segments = [
            ScrollSegment(
                threshold=0.0, elements=[{"id": "net1", "text": "Network1"}]
            ),
            ScrollSegment(
                threshold=0.5, elements=[{"id": "net1", "text": "Network1"}]  # Duplicate
            ),
        ]

        page = ScrollPage(path="dup_list", has_scroll=True, scroll_segments=segments)
        data_store = ScrollDataStore()
        data_store.add_page(page)

        fixture = StateFixture(
            pages={"dup_list": PageState(id="dup_list", page_name="Dup", elements=[])},
            transitions=[],
        initial_page_id="dup_list",
        )

        return ScrollableMockVisionService(fixture=fixture, data_store=data_store)

    def test_duplicate_element_single_visibility(self, vision_service):
        """WHEN element with same ID appears in multiple segments
        THEN element appears only once in visible elements
        """
        vision_service.simulate_scroll(0.5)
        analysis = vision_service.analyze_screenshot(b"")

        assert len(analysis.items) == 1
        assert analysis.items[0].name == "Network1"


class TestFaultInjection:
    """Tests for fault injection capabilities."""

    @pytest.fixture
    def vision_service(self):
        """Create vision service for fault injection testing."""
        segments = [
            ScrollSegment(threshold=0.0, elements=[{"id": "item1"}]),
            ScrollSegment(threshold=1.0, elements=[{"id": "item2"}]),
        ]

        page = ScrollPage(path="fault_test", has_scroll=True, scroll_segments=segments)
        data_store = ScrollDataStore()
        data_store.add_page(page)

        fixture = StateFixture(
            pages={"fault_test": PageState(id="fault_test", page_name="Fault", elements=[])},
            transitions=[],
        initial_page_id="fault_test",
        )

        return ScrollableMockVisionService(fixture=fixture, data_store=data_store)

    def test_scroll_delay(self, vision_service):
        """WHEN set_scroll_delay is called
        THEN scroll operations pause for specified duration
        """
        page_key = vision_service._resolve_path_key()
        vision_service.set_scroll_delay(page_key, 150)  # 150ms delay

        start = time.time()
        vision_service.simulate_scroll(0.5)
        elapsed = time.time() - start

        assert elapsed >= 0.15  # At least 150ms

    def test_scroll_failure_once(self, vision_service):
        """WHEN enable_scroll_failure is called with fail_once=True
        THEN only the next scroll fails, subsequent scrolls succeed
        """
        page_key = vision_service._resolve_path_key()
        vision_service.enable_scroll_failure(page_key, fail_once=True)

        # First scroll should fail
        result = vision_service.simulate_scroll(0.5)
        assert result is False
        assert vision_service.get_scroll_progress() == 0.0

        # Second scroll should succeed
        result = vision_service.simulate_scroll(0.5)
        assert result is True
        assert vision_service.get_scroll_progress() == 0.5

    def test_reset_scroll_state(self, vision_service):
        """WHEN reset_scroll_state is called
        THEN scroll state returns to initial values
        """
        # Scroll to 0.7
        vision_service.simulate_scroll(0.7)
        assert vision_service.get_scroll_progress() == 0.7

        # Reset
        page_key = vision_service._resolve_path_key()
        vision_service.reset_scroll_state(page_key)
        assert vision_service.get_scroll_progress() == 0.0


class TestHistoryTracking:
    """Tests for scroll history and count tracking."""

    @pytest.fixture
    def vision_service(self):
        """Create vision service for history tracking."""
        segments = [ScrollSegment(threshold=0.0, elements=[{"id": "item1"}])]

        page = ScrollPage(path="history_test", has_scroll=True, scroll_segments=segments)
        data_store = ScrollDataStore()
        data_store.add_page(page)

        fixture = StateFixture(
            pages={
                "history_test": PageState(
                    id="history_test", page_name="History", elements=[]
                )
            },
            transitions=[],
        initial_page_id="history_test",
        )

        return ScrollableMockVisionService(fixture=fixture, data_store=data_store)

    def test_scroll_count_increments(self, vision_service):
        """WHEN multiple scrolls are performed
        THEN scroll_count increments with each scroll
        """
        page_key = vision_service._resolve_path_key()
        state = vision_service._get_scroll_state(page_key)

        assert state.scroll_count == 0

        vision_service.simulate_scroll(0.3)
        assert state.scroll_count == 1

        vision_service.simulate_scroll(0.3)
        assert state.scroll_count == 2

    def test_scroll_history_tracking(self, vision_service):
        """WHEN scrolls are performed
        THEN scroll_history records progress after each scroll
        """
        page_key = vision_service._resolve_path_key()
        vision_service.simulate_scroll(0.2)
        vision_service.simulate_scroll(0.2)
        vision_service.simulate_scroll(0.2)

        state = vision_service._get_scroll_state(page_key)
        # Use approximate comparison for floating point
        assert len(state.scroll_history) == 3
        assert state.scroll_history[0] == pytest.approx(0.2)
        assert state.scroll_history[1] == pytest.approx(0.4)
        assert state.scroll_history[2] == pytest.approx(0.6)


class TestCoordinateFormats:
    """Tests for coordinate and bounds format support."""

    @pytest.fixture
    def vision_service_with_coordinate(self):
        """Create vision service with coordinate format elements."""
        segments = [
            ScrollSegment(
                threshold=0.0,
                elements=[
                    {
                        "id": "item1",
                        "text": "Item1",
                        "coordinate": {"x": 0.2, "y": 0.4},  # Normalized coordinates
                    }
                ],
            )
        ]

        page = ScrollPage(path="coord_test", has_scroll=True, scroll_segments=segments)
        data_store = ScrollDataStore()
        data_store.add_page(page)

        fixture = StateFixture(
            pages={
                "coord_test": PageState(id="coord_test", page_name="Coord", elements=[])
            },
            transitions=[],
        initial_page_id="coord_test",
        )

        return ScrollableMockVisionService(fixture=fixture, data_store=data_store)

    @pytest.fixture
    def vision_service_with_bounds(self):
        """Create vision service with bounds format elements."""
        segments = [
            ScrollSegment(
                threshold=0.0,
                elements=[
                    {"id": "item2", "text": "Item2", "bounds": [100, 200, 50, 50]}
                ],
            )
        ]

        page = ScrollPage(path="bounds_test", has_scroll=True, scroll_segments=segments)
        data_store = ScrollDataStore()
        data_store.add_page(page)

        fixture = StateFixture(
            pages={
                "bounds_test": PageState(id="bounds_test", page_name="Bounds", elements=[])
            },
            transitions=[],
        initial_page_id="bounds_test",
        )

        return ScrollableMockVisionService(
            fixture=fixture, data_store=data_store, screen_width=1080, screen_height=1920
        )

    def test_coordinate_format_parsed(self, vision_service_with_coordinate):
        """WHEN element has coordinate format
        THEN coordinate is used directly
        """
        analysis = vision_service_with_coordinate.analyze_screenshot(b"")

        # Coordinate should be used as provided (normalized)
        assert len(analysis.items) == 1
        assert analysis.items[0].coordinate.x == 0.2
        assert analysis.items[0].coordinate.y == 0.4

    def test_bounds_format_converted(self, vision_service_with_bounds):
        """WHEN element has bounds format
        THEN bounds are converted to coordinate
        """
        analysis = vision_service_with_bounds.analyze_screenshot(b"")

        assert len(analysis.items) == 1
        # Bounds [100, 200, 50, 50] -> x=100, y=200
        # Normalized to screen dimensions
        expected_x = 100 / 1080
        expected_y = 200 / 1920
        assert abs(analysis.items[0].coordinate.x - expected_x) < 0.01
        assert abs(analysis.items[0].coordinate.y - expected_y) < 0.01


class TestPageStateIsolation:
    """Tests for per-page scroll state isolation."""

    @pytest.fixture
    def vision_service_multi_page(self):
        """Create vision service with multiple pages."""
        # Page 1
        segments1 = [
            ScrollSegment(threshold=0.0, elements=[{"id": "page1_item1"}])
        ]
        page1 = ScrollPage(path="page1", has_scroll=True, scroll_segments=segments1)

        # Page 2
        segments2 = [
            ScrollSegment(threshold=0.0, elements=[{"id": "page2_item1"}])
        ]
        page2 = ScrollPage(path="page2", has_scroll=True, scroll_segments=segments2)

        data_store = ScrollDataStore()
        data_store.add_page(page1)
        data_store.add_page(page2)

        fixture = StateFixture(
            pages={
                "page1": PageState(id="page1", page_name="Page1", elements=[]),
                "page2": PageState(id="page2", page_name="Page2", elements=[]),
            },
            transitions=[],
        initial_page_id="page1",
        )

        return ScrollableMockVisionService(fixture=fixture, data_store=data_store)

    def test_different_pages_independent_progress(
        self, vision_service_multi_page
    ):
        """WHEN scrolling on different pages
        THEN each page maintains independent scroll progress
        """
        service = vision_service_multi_page

        # Scroll page1
        page1_key = service._resolve_path_key()
        service.simulate_scroll(0.5)
        page1_progress = service.get_scroll_progress()

        # Navigate to page2
        service._current_page_id = "page2"
        page2_key = service._resolve_path_key()
        page2_progress = service.get_scroll_progress()

        # Page2 should start at 0.0
        assert page2_progress == 0.0

        # Scroll page2
        service.simulate_scroll(0.3)
        page2_progress_after = service.get_scroll_progress()

        # Page1 progress should be unchanged
        page1_state = service._get_scroll_state(page1_key)
        assert page1_state.current_progress == page1_progress

        # Page2 progress should be updated
        assert page2_progress_after == 0.3


class TestEdgeCases:
    """Tests for edge cases and error handling."""

    def test_empty_segment_list(self):
        """WHEN page has no scroll segments
        THEN analyze_screenshot returns empty items list
        """
        page = ScrollPage(path="empty", has_scroll=False, scroll_segments=[])
        data_store = ScrollDataStore()
        data_store.add_page(page)

        fixture = StateFixture(
            pages={"empty": PageState(id="empty", page_name="Empty", elements=[])},
            transitions=[],
        initial_page_id="empty",
        )

        service = ScrollableMockVisionService(fixture=fixture, data_store=data_store)
        analysis = service.analyze_screenshot(b"")

        assert len(analysis.items) == 0
        assert analysis.has_scroll is False

    def test_element_without_id(self):
        """WHEN element doesn't have an ID
        THEN auto-generated ID is created
        """
        segments = [
            ScrollSegment(
                threshold=0.0, elements=[{"text": "Unnamed"}]  # No ID
            )
        ]

        page = ScrollPage(path="no_id", has_scroll=True, scroll_segments=segments)
        data_store = ScrollDataStore()
        data_store.add_page(page)

        fixture = StateFixture(
            pages={"no_id": PageState(id="no_id", page_name="NoID", elements=[])},
            transitions=[],
        initial_page_id="no_id",
        )

        service = ScrollableMockVisionService(fixture=fixture, data_store=data_store)
        analysis = service.analyze_screenshot(b"")

        # Should still create an item with generated ID
        assert len(analysis.items) == 1
        assert analysis.items[0].name == "Unnamed"

    def test_single_segment_no_scroll(self):
        """WHEN page has only one segment
        THEN has_scroll should be False at progress >= threshold
        """
        segments = [
            ScrollSegment(threshold=0.0, elements=[{"id": "item1"}, {"id": "item2"}])
        ]

        page = ScrollPage(path="single", has_scroll=True, scroll_segments=segments)
        data_store = ScrollDataStore()
        data_store.add_page(page)

        fixture = StateFixture(
            pages={"single": PageState(id="single", page_name="Single", elements=[])},
            transitions=[],
        initial_page_id="single",
        )

        service = ScrollableMockVisionService(fixture=fixture, data_store=data_store)
        analysis = service.analyze_screenshot(b"")

        # Single segment at threshold 0.0 - no scrolling needed
        assert len(analysis.items) == 2
        assert analysis.has_scroll is False
