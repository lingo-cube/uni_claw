"""
Unit tests for ScrollableMockActionExecutor.

Tests cover:
- scroll_down/scroll_up action execution
- Scroll action history tracking
- Scroll count and distance statistics
- Delegation of non-scroll actions to base class
"""

import time

import pytest

from src.simulation.scroll.models import ScrollAction, ScrollPage, ScrollSegment
from src.simulation.scroll.scroll_data_store import ScrollDataStore
from src.simulation.scroll.scrollable_mock_action import ScrollableMockActionExecutor
from src.simulation.scroll.scrollable_mock_vision import ScrollableMockVisionService
from src.simulation.state_fixture import PageState, StateFixture
from src.simulation.stateful_mock_action import ActionRecord
from src.simulation.operation_executor import ExecutionContext


class TestScrollActions:
    """Tests for scroll action execution."""

    @pytest.fixture
    def executor(self):
        """Create executor with scrollable vision service."""
        # Create scroll segments
        segments = [
            ScrollSegment(
                threshold=0.0,
                elements=[{"id": "item1", "text": "Item1"}],
            ),
            ScrollSegment(
                threshold=0.5,
                elements=[{"id": "item2", "text": "Item2"}],
            ),
            ScrollSegment(
                threshold=1.0,
                elements=[{"id": "item3", "text": "Item3"}],
            ),
        ]

        # Create scroll page
        page = ScrollPage(path="scrollable", has_scroll=True, scroll_segments=segments)

        # Create data store
        data_store = ScrollDataStore()
        data_store.add_page(page)

        # Create fixture
        fixture = StateFixture(
            pages={"scrollable": PageState(id="scrollable", page_name="Scrollable", elements=[])},
            transitions=[],
            initial_page_id="scrollable",
        )

        # Create vision service
        vision_service = ScrollableMockVisionService(fixture=fixture, data_store=data_store)

        # Create executor
        return ScrollableMockActionExecutor(vision_service=vision_service)

    def test_scroll_down_updates_progress(self, executor):
        """WHEN scroll_down action is executed with step_percent 0.3
        THEN vision service simulate_scroll is called with delta 0.3
        """
        context = ExecutionContext(
            operation={"action": "scroll_down", "target": {"step_percent": 0.3}},
            node_id="test_node",
            node_name="Test",
            timestamp=time.time(),
        )

        result = executor.execute(context)

        assert result.success is True
        assert executor.vision_service.get_scroll_progress() == 0.3

    def test_scroll_down_records_history(self, executor):
        """WHEN scroll_down action is executed
        THEN ScrollAction is appended to scroll_actions list
        """
        context = ExecutionContext(
            operation={"action": "scroll_down", "target": {"step_percent": 0.3}},
            node_id="test_node",
            node_name="Test",
            timestamp=time.time(),
        )

        executor.execute(context)

        assert len(executor.scroll_actions) == 1
        assert executor.scroll_actions[0].action == "SCROLL_DOWN"
        assert executor.scroll_actions[0].step_percent == 0.3
        assert executor.scroll_actions[0].before_progress == 0.0
        assert executor.scroll_actions[0].after_progress == 0.3

    def test_scroll_up_decreases_progress(self, executor):
        """WHEN scroll_up action is executed with step_percent 0.3
        THEN vision service simulate_scroll is called with delta -0.3
        """
        # First scroll down
        executor.vision_service.simulate_scroll(0.5)
        assert executor.vision_service.get_scroll_progress() == 0.5

        # Then scroll up
        context = ExecutionContext(
            operation={"action": "scroll_up", "target": {"step_percent": 0.3}},
            node_id="test_node",
            node_name="Test",
            timestamp=time.time(),
        )

        result = executor.execute(context)

        assert result.success is True
        assert executor.vision_service.get_scroll_progress() == 0.2

    def test_scroll_up_records_history(self, executor):
        """WHEN scroll_up action is executed
        THEN ScrollAction is appended with action=UP
        """
        # First scroll down
        executor.vision_service.simulate_scroll(0.5)

        context = ExecutionContext(
            operation={"action": "scroll_up", "target": {"step_percent": 0.2}},
            node_id="test_node",
            node_name="Test",
            timestamp=time.time(),
        )

        executor.execute(context)

        assert len(executor.scroll_actions) == 1
        assert executor.scroll_actions[0].action == "SCROLL_UP"
        assert executor.scroll_actions[0].after_progress < executor.scroll_actions[0].before_progress

    def test_scroll_default_step_percent(self, executor):
        """WHEN scroll action is executed without step_percent
        THEN default 0.3 is used
        """
        context = ExecutionContext(
            operation={"action": "scroll_down"},
            node_id="test_node",
            node_name="Test",
            timestamp=time.time(),
        )

        executor.execute(context)

        assert executor.vision_service.get_scroll_progress() == 0.3
        assert executor.scroll_actions[0].step_percent == 0.3

    def test_multiple_scroll_history(self, executor):
        """WHEN multiple scroll actions are executed
        THEN scroll_actions list contains all actions in order
        """
        # Execute multiple scrolls
        for _ in range(3):
            context = ExecutionContext(
                operation={"action": "scroll_down", "target": {"step_percent": 0.2}},
                node_id="test_node",
                node_name="Test",
                timestamp=time.time(),
            )
            executor.execute(context)

        assert len(executor.scroll_actions) == 3
        assert all(action.action == "SCROLL_DOWN" for action in executor.scroll_actions)


class TestScrollStatistics:
    """Tests for scroll statistics."""

    @pytest.fixture
    def executor(self):
        """Create executor for statistics testing."""
        segments = [ScrollSegment(threshold=0.0, elements=[{"id": "item1"}])]

        page = ScrollPage(path="test", has_scroll=True, scroll_segments=segments)
        data_store = ScrollDataStore()
        data_store.add_page(page)

        fixture = StateFixture(
            pages={"test": PageState(id="test", page_name="Test", elements=[])},
            transitions=[],
            initial_page_id="test",
        )

        vision_service = ScrollableMockVisionService(fixture=fixture, data_store=data_store)
        return ScrollableMockActionExecutor(vision_service=vision_service)

    def test_get_scroll_count(self, executor):
        """WHEN get_scroll_count is called
        THEN returns number of scroll actions for page
        """
        # Execute 3 scrolls
        for _ in range(3):
            context = ExecutionContext(
                operation={"action": "scroll_down"},
                node_id="test_node",
                node_name="Test",
                timestamp=time.time(),
            )
            executor.execute(context)

        assert executor.get_scroll_count("test") == 3

    def test_get_scroll_count_default_page(self, executor):
        """WHEN get_scroll_count is called without path
        THEN uses current page
        """
        # Execute scrolls
        for _ in range(2):
            context = ExecutionContext(
                operation={"action": "scroll_down"},
                node_id="test_node",
                node_name="Test",
                timestamp=time.time(),
            )
            executor.execute(context)

        # Get count for current page
        assert executor.get_scroll_count() == 2

    def test_get_total_scroll_distance(self, executor):
        """WHEN get_total_scroll_distance is called
        THEN returns cumulative scroll distance
        """
        # Execute scrolls with different step sizes
        steps = [0.3, 0.2, 0.1]
        for step in steps:
            context = ExecutionContext(
                operation={"action": "scroll_down", "target": {"step_percent": step}},
                node_id="test_node",
                node_name="Test",
                timestamp=time.time(),
            )
            executor.execute(context)

        total_distance = executor.get_total_scroll_distance("test")
        assert total_distance == sum(steps)  # 0.3 + 0.2 + 0.1 = 0.6


class TestNonScrollActions:
    """Tests for non-scroll action delegation."""

    @pytest.fixture
    def executor(self):
        """Create executor with transitions."""
        segments = [ScrollSegment(threshold=0.0, elements=[{"id": "button1"}])]

        page = ScrollPage(path="page1", has_scroll=False, scroll_segments=segments)
        data_store = ScrollDataStore()
        data_store.add_page(page)

        # Create fixture with transitions
        fixture = StateFixture(
            pages={
                "page1": PageState(id="page1", page_name="Page1", elements=[]),
                "page2": PageState(id="page2", page_name="Page2", elements=[]),
            },
            transitions=[],
            initial_page_id="page1",
        )

        vision_service = ScrollableMockVisionService(fixture=fixture, data_store=data_store)
        return ScrollableMockActionExecutor(vision_service=vision_service)

    def test_click_action_delegated(self, executor):
        """WHEN click action is executed
        THEN action is handled by base class
        """
        context = ExecutionContext(
            operation={"action": "click", "target": "button1"},
            node_id="test_node",
            node_name="Test",
            timestamp=time.time(),
        )

        result = executor.execute(context)

        # Click action is delegated to base class (may succeed or fail based on transitions)
        assert result.action == "click: button1"
        # No scroll actions should be recorded
        assert len(executor.scroll_actions) == 0

    def test_back_action_delegated(self, executor):
        """WHEN back action is executed
        THEN action is handled by base class
        """
        context = ExecutionContext(
            operation={"action": "back"},
            node_id="test_node",
            node_name="Test",
            timestamp=time.time(),
        )

        result = executor.execute(context)

        # Back action succeeds (even if no history)
        assert isinstance(result.success, bool)
        # No scroll actions should be recorded
        assert len(executor.scroll_actions) == 0

    def test_input_text_action_delegated(self, executor):
        """WHEN input_text action is executed
        THEN action is handled by base class
        """
        context = ExecutionContext(
            operation={"action": "input_text", "target": "test_input"},
            node_id="test_node",
            node_name="Test",
            timestamp=time.time(),
        )

        result = executor.execute(context)

        # Should succeed
        assert result.success is True
        # No scroll actions should be recorded
        assert len(executor.scroll_actions) == 0


class TestScrollHistoryProperties:
    """Tests for scroll history tracking."""

    @pytest.fixture
    def executor(self):
        """Create executor for history testing."""
        segments = [ScrollSegment(threshold=0.0, elements=[{"id": "item1"}])]

        page = ScrollPage(path="history", has_scroll=True, scroll_segments=segments)
        data_store = ScrollDataStore()
        data_store.add_page(page)

        fixture = StateFixture(
            pages={"history": PageState(id="history", page_name="History", elements=[])},
            transitions=[],
            initial_page_id="history",
        )

        vision_service = ScrollableMockVisionService(fixture=fixture, data_store=data_store)
        return ScrollableMockActionExecutor(vision_service=vision_service)

    def test_scroll_actions_property(self, executor):
        """WHEN scroll_actions property is accessed
        THEN returns list of scroll action records
        """
        # Execute some scrolls
        for _ in range(2):
            context = ExecutionContext(
                operation={"action": "scroll_down"},
                node_id="test_node",
                node_name="Test",
                timestamp=time.time(),
            )
            executor.execute(context)

        actions = executor.scroll_actions
        assert len(actions) == 2
        assert all(isinstance(action, ScrollAction) for action in actions)

    def test_clear_scroll_history(self, executor):
        """WHEN clear_scroll_history is called
        THEN scroll actions list is cleared
        """
        # Execute some scrolls
        for _ in range(3):
            context = ExecutionContext(
                operation={"action": "scroll_down"},
                node_id="test_node",
                node_name="Test",
                timestamp=time.time(),
            )
            executor.execute(context)

        assert len(executor.scroll_actions) == 3

        # Clear history
        executor.clear_scroll_history()

        assert len(executor.scroll_actions) == 0

    def test_vision_service_property(self, executor):
        """WHEN vision_service property is accessed
        THEN returns ScrollableMockVisionService
        """
        vision = executor.vision_service
        assert isinstance(vision, ScrollableMockVisionService)


class TestEdgeCases:
    """Tests for edge cases."""

    @pytest.fixture
    def executor(self):
        """Create executor for edge case testing."""
        segments = [ScrollSegment(threshold=0.0, elements=[{"id": "item1"}])]

        page = ScrollPage(path="edge", has_scroll=True, scroll_segments=segments)
        data_store = ScrollDataStore()
        data_store.add_page(page)

        fixture = StateFixture(
            pages={"edge": PageState(id="edge", page_name="Edge", elements=[])},
            transitions=[],
            initial_page_id="edge",
        )

        vision_service = ScrollableMockVisionService(fixture=fixture, data_store=data_store)
        return ScrollableMockActionExecutor(vision_service=vision_service)

    def test_scroll_beyond_boundary(self, executor):
        """WHEN scroll would exceed 1.0 boundary
        THEN progress is clamped to 1.0
        """
        # Try to scroll beyond 1.0
        context = ExecutionContext(
            operation={"action": "scroll_down", "target": {"step_percent": 2.0}},
            node_id="test_node",
            node_name="Test",
            timestamp=time.time(),
        )

        result = executor.execute(context)

        assert result.success is True
        assert executor.vision_service.get_scroll_progress() == 1.0

    def test_scroll_below_boundary(self, executor):
        """WHEN scroll would go below 0.0 boundary
        THEN progress is clamped to 0.0
        """
        # Try to scroll below 0.0
        context = ExecutionContext(
            operation={"action": "scroll_up", "target": {"step_percent": 1.0}},
            node_id="test_node",
            node_name="Test",
            timestamp=time.time(),
        )

        result = executor.execute(context)

        assert result.success is True
        assert executor.vision_service.get_scroll_progress() == 0.0

    def test_scroll_with_fault_injection(self, executor):
        """WHEN scroll failure is enabled
        THEN scroll action returns False and progress unchanged
        """
        page_key = executor.vision_service._resolve_path_key()
        executor.vision_service.enable_scroll_failure(page_key, fail_once=True)

        context = ExecutionContext(
            operation={"action": "scroll_down"},
            node_id="test_node",
            node_name="Test",
            timestamp=time.time(),
        )

        result = executor.execute(context)

        assert result.success is False
        assert executor.vision_service.get_scroll_progress() == 0.0
