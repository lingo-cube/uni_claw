"""
Unit tests for MockActionExecutor component.

Tests comprehensive operation recording, context integration,
and node stack management functionality.
"""

import pytest
import time
from src.simulation.mock_action import MockActionExecutor, OperationRecord


class MockContext:
    """Mock context for testing."""
    def __init__(self, current_node="test_node", current_path=None, depth=0):
        self.current_node = current_node
        self.current_path = current_path or ["root"]
        self.depth = depth


class TestMockActionExecutor:
    """Test suite for MockActionExecutor component."""

    @pytest.fixture
    def executor(self):
        """Create MockActionExecutor instance."""
        return MockActionExecutor()

    def test_init(self, executor):
        """Test initialization."""
        assert executor.action_history == []
        assert executor.simulate_delay == 0.0
        assert executor._operation_context == {}
        assert executor._page_context is None
        assert executor._node_stack == []

    def test_init_with_delay(self):
        """Test initialization with delay."""
        executor = MockActionExecutor(simulate_delay=0.1)
        assert executor.simulate_delay == 0.1

    def test_tap_recording(self, executor):
        """Test tap action recording."""
        result = executor.tap(0.5, 0.7)

        assert result is True
        assert executor.get_operation_count() == 1

        operation = executor.get_last_action()
        assert operation["action_type"] == "tap"
        assert operation["target_info"]["x"] == 0.5
        assert operation["target_info"]["y"] == 0.7
        assert operation["result"] == "success"

    def test_click_recording(self, executor):
        """Test click action recording."""
        result = executor.click("button_123", extra_param="test")

        assert result is True
        operation = executor.get_last_action()
        assert operation["action_type"] == "click"
        assert operation["target_info"]["element_id"] == "button_123"
        assert operation["target_info"]["extra_param"] == "test"

    def test_scroll_recording(self, executor):
        """Test scroll action recording."""
        result = executor.scroll("down", distance=5)

        assert result is True
        operation = executor.get_last_action()
        assert operation["action_type"] == "scroll"
        assert operation["target_info"]["direction"] == "down"
        assert operation["target_info"]["distance"] == 5

    def test_swipe_recording(self, executor):
        """Test swipe action recording."""
        result = executor.swipe((0.2, 0.3), (0.8, 0.9), duration=0.5)

        assert result is True
        operation = executor.get_last_action()
        assert operation["action_type"] == "swipe"
        assert operation["target_info"]["start"] == [0.2, 0.3]
        assert operation["target_info"]["end"] == [0.8, 0.9]
        assert operation["target_info"]["duration"] == 0.5

    def test_input_text_recording(self, executor):
        """Test text input recording."""
        result = executor.input_text("Hello World", element_id="input_field")

        assert result is True
        operation = executor.get_last_action()
        assert operation["action_type"] == "input_text"
        assert operation["target_info"]["text"] == "Hello World"
        assert operation["target_info"]["element_id"] == "input_field"

    def test_go_back_recording(self, executor):
        """Test go back action recording."""
        result = executor.go_back(from_page="settings")

        assert result is True
        operation = executor.get_last_action()
        assert operation["action_type"] == "go_back"
        assert operation["target_info"]["from_page"] == "settings"

    def test_press_back_recording(self, executor):
        """Test press back action recording."""
        result = executor.press_back()

        assert result is True
        operation = executor.get_last_action()
        assert operation["action_type"] == "go_back"  # Maps to go_back

    def test_press_home_recording(self, executor):
        """Test press home action recording."""
        result = executor.press_home()

        assert result is True
        operation = executor.get_last_action()
        assert operation["action_type"] == "press_home"

    def test_context_integration(self, executor):
        """Test context setting and integration."""
        context = MockContext(current_node="settings", current_path=["root", "settings"], depth=1)
        executor.set_context(context)

        # Perform action with context
        executor.click("test_button")

        operation = executor.get_last_action()
        assert operation["current_node"] == "settings"
        assert operation["current_path"] == ["root", "settings"]

    def test_page_context_integration(self, executor):
        """Test page context setting."""
        page_context = {
            "page_name": "SettingsPage",
            "page_type": "settings",
            "elements_count": 5
        }
        executor.set_page_context(page_context)

        # Perform action
        executor.click("test_button")

        operation = executor.get_last_action()
        assert operation["page_context"] == page_context
        assert operation["page_context"]["page_name"] == "SettingsPage"

    def test_node_stack_management(self, executor):
        """Test node stack push and pop operations."""
        # Push nodes
        executor.push_node("root")
        executor.push_node("settings")
        executor.push_node("display")

        # Perform action to check stack
        executor.click("test_button")
        operation = executor.get_last_action()
        assert operation["node_stack"] == ["root", "settings", "display"]

        # Pop node
        popped = executor.pop_node()
        assert popped == "display"

        # Check stack after pop
        executor.click("another_button")
        operation = executor.get_last_action()
        assert operation["node_stack"] == ["root", "settings"]

    def test_pop_empty_stack(self, executor):
        """Test popping from empty stack."""
        popped = executor.pop_node()
        assert popped is None

    def test_get_history(self, executor):
        """Test getting action history."""
        executor.tap(0.1, 0.2)
        executor.click("button1")
        executor.scroll("down", 3)

        history = executor.get_history()
        assert len(history) == 3
        assert history[0]["action_type"] == "tap"
        assert history[1]["action_type"] == "click"
        assert history[2]["action_type"] == "scroll"

    def test_get_history_returns_copy(self, executor):
        """Test that get_history returns a copy, not reference."""
        executor.tap(0.5, 0.5)
        history1 = executor.get_history()
        history2 = executor.get_history()

        # Modify one copy
        history1.append({"test": "modification"})

        # Other copy should be unchanged
        assert len(history2) == 1
        assert len(history1) == 2

    def test_get_operations_by_type(self, executor):
        """Test filtering operations by type."""
        executor.click("button1")
        executor.click("button2")
        executor.scroll("down", 1)
        executor.click("button3")
        executor.go_back()

        click_operations = executor.get_operations_by_type("click")
        assert len(click_operations) == 3

        scroll_operations = executor.get_operations_by_type("scroll")
        assert len(scroll_operations) == 1

        back_operations = executor.get_operations_by_type("go_back")
        assert len(back_operations) == 1

    def test_get_operation_count(self, executor):
        """Test getting operation count."""
        assert executor.get_operation_count() == 0

        executor.tap(0.1, 0.2)
        assert executor.get_operation_count() == 1

        executor.click("button1")
        assert executor.get_operation_count() == 2

        executor.scroll("down", 1)
        assert executor.get_operation_count() == 3

    def test_reset_functionality(self, executor):
        """Test reset functionality."""
        # Set some state
        executor.set_context(MockContext())
        executor.set_page_context({"test": "context"})
        executor.push_node("test_node")
        executor.tap(0.5, 0.5)

        assert executor.get_operation_count() == 1
        assert len(executor._node_stack) == 1

        # Reset
        executor.reset()

        # Check everything is cleared
        assert executor.get_operation_count() == 0
        assert executor._operation_context == {}
        assert executor._page_context is None
        assert executor._node_stack == []

    def test_comprehensive_recording_structure(self, executor):
        """Test that operations have comprehensive structure."""
        context = MockContext(current_node="settings", current_path=["root", "settings"])
        executor.set_context(context)

        page_context = {"page_name": "SettingsPage", "page_type": "settings"}
        executor.set_page_context(page_context)

        executor.push_node("root")
        executor.push_node("settings")

        executor.click("brightness_slider", duration=1.5)

        operation = executor.get_last_action()

        # Check all required fields
        assert "action_type" in operation
        assert "timestamp" in operation
        assert "result" in operation
        assert "current_node" in operation
        assert "current_path" in operation
        assert "page_context" in operation
        assert "target_info" in operation
        assert "metadata" in operation
        assert "node_stack" in operation

        # Check values
        assert operation["action_type"] == "click"
        assert operation["result"] == "success"
        assert operation["current_node"] == "settings"
        assert operation["current_path"] == ["root", "settings"]
        assert operation["page_context"]["page_name"] == "SettingsPage"
        assert operation["target_info"]["element_id"] == "brightness_slider"
        assert operation["target_info"]["duration"] == 1.5
        assert operation["node_stack"] == ["root", "settings"]

    def test_delay_simulation(self):
        """Test that delay is simulated during operations."""
        executor = MockActionExecutor(simulate_delay=0.05)

        start_time = time.time()
        executor.tap(0.5, 0.5)
        elapsed_time = time.time() - start_time

        # Should have taken at least the delay time
        assert elapsed_time >= 0.05
        assert elapsed_time < 0.2  # But not too long

    def test_legacy_compatibility(self, executor):
        """Test legacy methods for backward compatibility."""
        # Test legacy tap method
        result = executor.tap(0.5, 0.7)
        assert result is True

        # Test legacy swipe method
        result = executor.swipe((0.1, 0.2), (0.8, 0.9), 0.3)
        assert result is True

        # Test legacy methods work
        assert executor.get_action_count() == 2

    def test_multiple_actions_context_preservation(self, executor):
        """Test that context is preserved across multiple actions."""
        context = MockContext(current_node="settings")
        executor.set_context(context)

        executor.click("button1")
        executor.click("button2")
        executor.scroll("down", 1)

        # All actions should have the same context
        for operation in executor.get_history():
            assert operation["current_node"] == "settings"

    def test_operation_timestamps(self, executor):
        """Test that operations have correct timestamps."""
        before_time = time.time()
        executor.tap(0.5, 0.5)
        after_time = time.time()

        operation = executor.get_last_action()
        assert before_time <= operation["timestamp"] <= after_time

    def test_empty_target_info_handling(self, executor):
        """Test operations with minimal target info."""
        executor.go_back()  # No target info needed

        operation = executor.get_last_action()
        assert operation["action_type"] == "go_back"
        assert operation["target_info"] == {}

    def test_complex_target_info(self, executor):
        """Test operations with complex target info."""
        complex_params = {
            "element_id": "complex_button",
            "x": 0.5,
            "y": 0.7,
            "duration": 1.5,
            "pressure": 0.8,
            "size": 20
        }
        executor.click("complex_button", **complex_params)

        operation = executor.get_last_action()
        for key, value in complex_params.items():
            assert operation["target_info"][key] == value